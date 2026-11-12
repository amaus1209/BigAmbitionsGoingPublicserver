using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>
    /// PLAYER-REPORTS-1 (user 2026-11-08: "Wir könnten im Mod noch eine Art Telemetrie einbauen, was zum Server schickt, der
    /// verbunden ist, um die Player.log zu schicken und gleich auszuwerten" - "im Live-Spiel". The user's choices: ask every
    /// player once; the important lines live, the whole log only after a crash or when the admin asks; the results as files
    /// on the server; the AMP switch on by default).
    ///
    /// In the player's game a hook on Unity's log (Application.logMessageReceivedThreaded) keeps exceptions and errors of any
    /// source, our mods' warnings ("[BAMP]" lines) and our check lines (lines that end with a feature tag such as
    /// "(GO-MODELS-1)."), counted by kind - a fault that repeats every frame is one line with a count. While the player is in
    /// the world of a server that collects (the rule PlayerReports, sent by that server itself) and has agreed (asked once
    /// with the game's own confirm dialog; Options, tab Mods: "Error reports to the server" ask / send / do not send), the
    /// new ones go to the server every 30 s. Before anything leaves the PC, Clean() takes out the Windows user name in paths,
    /// IP addresses, Steam ids, e-mail addresses and keys. After a session that did not end cleanly
    /// (MPBugReport.PendingCrashDetected) the previous session's Player-prev.log goes along once, and the admin can ask for
    /// the whole Player.log (console "playerlog &lt;player&gt;") - cleaned, gzip'd, in pieces; the player gets a notice.
    ///
    /// On a dedicated server the reports land in bigambitions/player-reports, beside the instance's other folders and not in
    /// the web folder (nothing of it is public): summary.json (every kind of line - how often, which players, builds and
    /// systems, first and last time, text and stack), one .jsonl per player and day with everything received, and logs/ with
    /// the Player.logs. The server deletes them after 14 days (and keeps the folder under 300 MB). Every 10 minutes one line
    /// in the console; the console command "reports" lists the most frequent kinds. A player's own game hosting friends never
    /// collects. Before, no report left the PC at all (BUG-REPORT-OFF-1, 2026-10-31: the old receiver belonged to someone
    /// else); these go only to the server the player is playing on.
    /// </summary>
    internal static class PlayerReports
    {
        private const string Tag = "(PLAYER-REPORTS-1)";
        private const float ReportEverySeconds = 30f;
        private const int MaxQueued = 4000;            // raw lines waiting for the main thread
        private const int MaxKinds = 400;              // kinds of lines kept per game start
        private const int MaxLinesPerReport = 150;
        private const int MaxCharsPerReport = 60000;
        private const int MaxText = 600, MaxStack = 2400;
        private const long MaxLogBytes = 8L * 1024 * 1024;   // the tail of a Player.log that is sent
        private const int ChunkBytes = 160 * 1024;
        private const int KeepDays = 14;
        private const long MaxFolderBytes = 300L * 1024 * 1024;

        // ── cleaning (both sides) ─────────────────────────────────────────────────────────────────────────────────────

        private const RegexOptions Rx = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        private static readonly Regex RxWebhook = new Regex(@"(?i)https?://[^\s""'<>]*/api/webhooks/[^\s""'<>]+", Rx);
        private static readonly Regex RxUserPath = new Regex(@"(?i)([a-z]:[\\/]+(?:users|documents and settings)[\\/]+)[^\\/:*?""<>|\r\n]+", Rx);
        private static readonly Regex RxHome = new Regex(@"(/(?:home|Users)/)[^/\s""']+", Rx);
        private static readonly Regex RxSecret = new Regex(@"(?i)(\w*(?:key|schluessel|schlüssel|token|passwor[dt]|secret|webhook))(\s*[=:]\s*)(""?)[^\s""',;|]+", Rx);
        private static readonly Regex RxMail = new Regex(@"[\w.+-]+@[\w-]+(?:\.[\w-]+)+", Rx);
        private static readonly Regex RxSteamId = new Regex(@"(?<!\d)7656119\d{10}(?!\d)", Rx);
        private static readonly Regex RxIp6 = new Regex(@"(?<![\w:])(?:[0-9a-fA-F]{1,4}:){4,7}[0-9a-fA-F]{1,4}(?![\w:])", Rx);
        // four numbers with dots; versions like 1.0.16.3 stay (no address starts with 0. or 1.0.)
        private static readonly Regex RxIp4 = new Regex(@"(?<![\d.])(?!0\.|1\.0\.)(?:\d{1,3}\.){3}\d{1,3}(?::\d{1,5})?(?![\d.])", Rx);

        /// <summary>Takes personal data out of a log text: the Windows user name in paths, IP addresses, Steam ids, e-mail
        /// addresses, keys and passwords ("…key=", "Schluessel=", tokens, webhook addresses). Thread-safe.</summary>
        internal static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            try
            {
                s = RxWebhook.Replace(s, "<hidden>");
                s = RxUserPath.Replace(s, "$1<user>");
                s = RxHome.Replace(s, "$1<user>");
                s = RxSecret.Replace(s, "$1$2$3<hidden>");
                s = RxMail.Replace(s, "<mail>");
                s = RxSteamId.Replace(s, "<steamid>");
                s = RxIp6.Replace(s, "<ip>");
                s = RxIp4.Replace(s, "<ip>");
            }
            catch { }
            return s;
        }

        private static readonly Regex RxQuoted = new Regex(@"'[^']{0,120}'|""[^""]{0,120}""", Rx);
        private static readonly Regex RxHexLit = new Regex(@"0x[0-9a-fA-F]+", Rx);   // IL offsets "[0x0011f]" differ by build
        private static readonly Regex RxHexId = new Regex(@"\b[0-9a-fA-F]{8,}\b", Rx);
        private static readonly Regex RxNumber = new Regex(@"\d+(?:[.,]\d+)*", Rx);

        /// <summary>The same key for the same fault on every machine: the kind, the text without names, ids and numbers, and
        /// the first three frames of the stack.</summary>
        private static string KeyOf(string kind, string text, string stack)
        {
            string frames = string.Join("|", stack.Split('\n').Select(f => f.Trim()).Where(f => f.Length > 0).Take(3));
            string n = kind + "|" + text + "|" + frames;
            n = RxQuoted.Replace(n, "'.'");
            n = RxHexLit.Replace(n, "0x");
            n = RxHexId.Replace(n, "h");
            n = RxNumber.Replace(n, "#");
            return Hash8(n);
        }

        private static string Hash8(string s)
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h.ToString("x8");
        }

        private static string Cut(string s, int max) => string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s.Substring(0, max) + "…";

        // ── the player's game ─────────────────────────────────────────────────────────────────────────────────────────

        private sealed class Raw { public byte Kind; public string Text = ""; public string Stack = ""; public float At; }

        private sealed class Agg
        {
            public string Kind = "", Text = "", Stack = "";
            public string? SentText;
            public bool StackSent;
            public int Count, Sent;
            public float First, Last;
        }

        private static readonly ConcurrentQueue<Raw> _in = new ConcurrentQueue<Raw>();
        private static int _queued, _droppedRaw, _droppedKinds;
        private static readonly Dictionary<string, Agg> _agg = new Dictionary<string, Agg>();
        private static readonly List<string> _order = new List<string>();   // keys in the order they first came
        private static readonly Stopwatch _clock = Stopwatch.StartNew();
        private static bool _hooked, _askedThisRun, _crashQueued;
        private static string _session = "", _platform = "";
        private static float _nextReport, _inWorldSince = -1f, _askRetryAt, _nextChunkAt;

        private static readonly ConcurrentQueue<string> _logRequests = new ConcurrentQueue<string>();   // from the network thread
        private static readonly Queue<(string rid, string kind)> _uploads = new Queue<(string, string)>();
        private static readonly ConcurrentQueue<List<PlayerLogChunkPayload>> _packed = new ConcurrentQueue<List<PlayerLogChunkPayload>>();
        private static readonly Queue<PlayerLogChunkPayload> _outChunks = new Queue<PlayerLogChunkPayload>();
        private static volatile bool _packing;
        private static volatile string _packError = "";
        private static string _uploadNotice = "";

        /// <summary>At the mod's load (Plugin), in the player's game only: from here on the log's lines are counted.</summary>
        internal static void InstallHook()
        {
            if (_hooked) return;
            try
            {
                if (Application.isBatchMode || MPConfig.DedicatedHost) return;   // a dedicated server has its own console
                _session = Guid.NewGuid().ToString("N").Substring(0, 8);
                Application.logMessageReceivedThreaded += OnLog;
                _hooked = true;
            }
            catch { }
        }

        /// <summary>ANY THREAD (Unity calls it for every log line): keeps only what matters, as cheaply as possible.</summary>
        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            try
            {
                if (string.IsNullOrEmpty(condition)) return;
                byte kind;
                switch (type)
                {
                    case LogType.Exception: kind = 1; break;
                    case LogType.Error:
                    case LogType.Assert: kind = 2; break;
                    case LogType.Warning:
                        if (condition.IndexOf("[BAMP]", StringComparison.Ordinal) < 0) return;
                        kind = 3; break;
                    default:
                        if (!EndsWithTag(condition) || condition.IndexOf("[BAMP]", StringComparison.Ordinal) < 0) return;
                        kind = 4; break;
                }
                if (condition.IndexOf(Tag, StringComparison.Ordinal) >= 0) return;   // our own lines never feed back
                if (Interlocked.Increment(ref _queued) > MaxQueued)
                {
                    Interlocked.Decrement(ref _queued);
                    Interlocked.Increment(ref _droppedRaw);
                    return;
                }
                _in.Enqueue(new Raw { Kind = kind, Text = condition, Stack = kind <= 2 ? (stackTrace ?? "") : "", At = (float)_clock.Elapsed.TotalSeconds });
            }
            catch { }
        }

        /// <summary>"… (GO-MODELS-1)." - the line ends with a feature tag: an upper-case name, a dash, a number.</summary>
        private static bool EndsWithTag(string s)
        {
            int i = s.Length - 1;
            while (i >= 0 && (s[i] == '.' || s[i] == ' ' || s[i] == '\r' || s[i] == '\n')) i--;
            if (i < 4 || s[i] != ')') return false;
            i--;
            int digits = 0;
            while (i >= 0 && s[i] >= '0' && s[i] <= '9') { i--; digits++; }
            if (digits == 0 || i < 3 || s[i] != '-') return false;
            i--;
            int letters = 0;
            while (i >= 0 && ((s[i] >= 'A' && s[i] <= 'Z') || (s[i] >= '0' && s[i] <= '9') || s[i] == '-')) { i--; letters++; }
            return letters >= 2 && i >= 0 && s[i] == '(';
        }

        /// <summary>0 = ask, 1 = send, 2 = do not send - the Mods tab's choice, kept by the game in PlayerPrefs under its own
        /// key scheme ("m:" + mod id + ":" + option id), so the dialog's answer and the tab are one value.</summary>
        internal static int Consent
        {
            get
            {
                try { int v = UnityEngine.PlayerPrefs.GetInt(PrefKey, 0); return v < 0 || v > 2 ? 0 : v; }
                catch { return 0; }
            }
        }

        private static string PrefKey => string.IsNullOrEmpty(PlayerColours.ModId) ? "gps_player_reports" : "m:" + PlayerColours.ModId + ":player_reports";

        private static void SetConsent(int v, string how)
        {
            try { UnityEngine.PlayerPrefs.SetInt(PrefKey, v); UnityEngine.PlayerPrefs.Save(); } catch { }
            Plugin.Logger.LogInfo($"[Reports] error reports to the server: {(v == 1 ? "send" : v == 2 ? "do not send" : "ask")} ({how}) {Tag}.");
            if (how == "asked")
            {
                try { PlayerColours.RefreshOptions(); } catch { }
                if (v == 1) Notify(Loc.T("gps_reports_thanks", "Thanks! Error reports go to the server you play on. You can change it under Options, tab Mods."));
            }
        }

        /// <summary>The Mods tab's row (PlayerColours.RegisterOptions puts it into the mod's section).</summary>
        internal static void AddOptions(BigAmbitions.Mods.ModOptions o)
        {
            o.AddDropdown("player_reports", Loc.T("gps_reports_option", "Error reports to the server"),
                new[] { "gps_reports_choice_ask", "gps_reports_choice_send", "gps_reports_choice_no" }, 0,
                v => { try { if (v >= 0 && v <= 2 && v != Consent) SetConsent(v, "options"); } catch { } });
        }

        /// <summary>NETWORK THREAD: the server asks for the whole Player.log.</summary>
        internal static void OnLogRequest(string requestId)
        {
            if (string.IsNullOrEmpty(requestId) || requestId.Length > 40) return;
            if (_logRequests.Count < 8) _logRequests.Enqueue(requestId);
        }

        /// <summary>MAIN THREAD, every frame (MPCanvasUI.Update): the player's side, or the dedicated server's.</summary>
        internal static void Tick()
        {
            try
            {
                if (HeadlessHost.Active) { HostTick(); return; }
                if (_hooked) ClientTick();
            }
            catch (Exception ex) { WarnOnce("tick", ex.Message); }
        }

        private static bool _labelLocalized;

        private static void ClientTick()
        {
            Drain(300);
            if (!_labelLocalized && HasKey("gps_reports_option"))
            {
                // the Mods tab row was registered at load, often before the game had the mod's texts: once more now
                _labelLocalized = true;
                try { PlayerColours.RefreshOptions(); } catch { }
            }
            float now = Time.unscaledTime;
            bool inWorld = MPClient.IsConnected && MPClient.IsClientInWorld && ServerRules.ServerCollectsReports;
            if (!inWorld)
            {
                _inWorldSince = -1f;
                _outChunks.Clear();
                while (_logRequests.TryDequeue(out _)) { }
                return;
            }
            if (_inWorldSince < 0f) { _inWorldSince = now; _nextReport = now + 10f; }
            int consent = Consent;
            if (consent == 0) MaybeAsk(now);
            while (_logRequests.TryDequeue(out var rid))
            {
                if (consent == 1) _uploads.Enqueue((rid, "request"));
                else Refuse(rid, "refused");
            }
            if (consent != 1) return;
            if (!_crashQueued && MPBugReport.PendingCrashDetected && now - _inWorldSince > 30f)
            {
                _crashQueued = true;
                _uploads.Enqueue(("crash-" + _session, "crash"));
            }
            PumpUploads(now);
            if (now >= _nextReport)
            {
                _nextReport = now + ReportEverySeconds;
                SendReport();
            }
        }

        private static void Drain(int max)
        {
            while (max-- > 0 && _in.TryDequeue(out var r))
            {
                Interlocked.Decrement(ref _queued);
                try { Add(r); } catch { }
            }
        }

        private static void Add(Raw r)
        {
            string kind = r.Kind == 1 ? "exception" : r.Kind == 2 ? "error" : r.Kind == 3 ? "warning" : "check";
            string text = Clean(Cut(r.Text.Replace("\r", ""), MaxText));
            string stack = r.Stack.Length > 0 ? Clean(Cut(r.Stack.Replace("\r", ""), MaxStack)) : "";
            string key = KeyOf(kind, text, stack);
            if (!_agg.TryGetValue(key, out var a))
            {
                if (_agg.Count >= MaxKinds) { _droppedKinds++; return; }
                a = new Agg { Kind = kind, Text = text, Stack = stack, First = r.At };
                _agg[key] = a;
                _order.Add(key);
            }
            else if (kind == "check") a.Text = text;   // a check line keeps its newest wording (its numbers change)
            a.Count++;
            a.Last = r.At;
        }

        private static void MaybeAsk(float now)
        {
            if (_askedThisRun || now < _askRetryAt || now - _inWorldSince < 20f) return;
            // the game's dialog confirms by itself when it has no window, and skips the question while its "without
            // confirmation" key is held - neither may happen to a consent
            if (HudConfirm.isOpen || HudConfirm.onShow == null) { _askRetryAt = now + 10f; return; }
            bool keys = HasKey("gps_reports_ask_body") && HasKey("gps_reports_ask_title") && HasKey("gps_reports_ask_yes") && HasKey("gps_reports_ask_no");
            string title = keys ? "gps_reports_ask_title" : "Send error reports to this server?";
            string body = keys ? "gps_reports_ask_body" : AskBodyEnglish;
            string yes = keys ? "gps_reports_ask_yes" : "Yes, send";
            string no = keys ? "gps_reports_ask_no" : "No";
            HudConfirm.Show(title, body, () => SetConsent(1, "asked"), () => SetConsent(2, "asked"), yes, no, false, false);
            if (HudConfirm.isOpen) _askedThisRun = true;   // closed without an answer: asked again at the next game start
            else _askRetryAt = now + 30f;                  // the dialog was not shown here (another screen) - later
        }

        private const string AskBodyEnglish =
            "This server collects error messages to find faults in the mod faster. May your game send errors, warnings and check lines of Going Public Server to the server you are playing on? After a crash, or when the server asks, your whole Player.log too. Your Windows user name, IP addresses, Steam IDs and keys are taken out first, and nothing goes to anybody else. You can change it any time under Options, tab Mods.";

        private static bool HasKey(string key)
        {
            try { return Localizor.LocalizorManager.IsLocalizedKey(key); } catch { return false; }
        }

        private static void Notify(string text)
        {
            try { UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, text); } catch { }
        }

        private static string Platform()
        {
            if (_platform.Length > 0) return _platform;
            string os = "";
            try { os = SystemInfo.operatingSystem ?? ""; } catch { }
            bool wine = false;
            try { wine = ModEntry.RunningOnWine(); } catch { }
            _platform = Cut((wine ? "Proton/Wine - " : "") + os, 120);
            return _platform;
        }

        private static void SendReport()
        {
            int droppedRaw = Interlocked.Exchange(ref _droppedRaw, 0);
            var p = new PlayerReportPayload
            {
                PlayerId = MPConfig.PlayerId, Session = _session, Build = MyPluginInfo.BuildId, Version = MyPluginInfo.PLUGIN_VERSION,
                Game = MPContentFingerprint.GameBuildId ?? "", Platform = Platform(), Uptime = (float)_clock.Elapsed.TotalSeconds,
                Dropped = droppedRaw + _droppedKinds,
            };
            _droppedKinds = 0;
            int chars = 0;
            foreach (var key in _order)
            {
                var a = _agg[key];
                bool newCount = a.Count > a.Sent, newText = a.Text != a.SentText, newStack = a.Stack.Length > 0 && !a.StackSent;
                if (!newCount && !newText && !newStack) continue;
                if (p.Lines.Count >= MaxLinesPerReport || chars >= MaxCharsPerReport) break;   // the rest goes next time
                var l = new PlayerReportLineDto { Kind = a.Kind, Key = key, Count = a.Count - a.Sent, First = a.First, Last = a.Last };
                if (newText) { l.Text = a.Text; chars += a.Text.Length; }
                if (newStack) { l.Stack = a.Stack; chars += a.Stack.Length; }
                p.Lines.Add(l);
                a.Sent = a.Count;
                a.SentText = a.Text;
                if (newStack) a.StackSent = true;
            }
            if (p.Lines.Count == 0 && p.Dropped == 0) return;
            MPClient.SendPlayerReport(p);
        }

        private static void Refuse(string rid, string kind)
        {
            MPClient.SendPlayerLogChunk(new PlayerLogChunkPayload { PlayerId = MPConfig.PlayerId, RequestId = rid, Kind = kind });
            Plugin.Logger.LogInfo($"[Reports] the server asked for the Player.log - not sent ({kind}) {Tag}.");
        }

        /// <summary>One upload at a time: pack it in the background, then one piece every half second.</summary>
        private static void PumpUploads(float now)
        {
            while (_packed.TryDequeue(out var pieces)) foreach (var c in pieces) _outChunks.Enqueue(c);
            if (_packError.Length > 0) { WarnOnce("pack", _packError); _packError = ""; }
            if (_outChunks.Count > 0)
            {
                if (now < _nextChunkAt) return;
                _nextChunkAt = now + 0.5f;
                MPClient.SendPlayerLogChunk(_outChunks.Dequeue());
                if (_outChunks.Count == 0 && _uploadNotice.Length > 0)
                {
                    Notify(_uploadNotice);
                    _uploadNotice = "";
                    Plugin.Logger.LogInfo($"[Reports] the Player.log went to the server {Tag}.");
                }
                return;
            }
            if (_packing || _uploads.Count == 0) return;
            var (rid, kind) = _uploads.Dequeue();
            string path;
            try
            {
                string current = Application.consoleLogPath ?? "";   // main thread only
                path = kind == "crash" ? Path.Combine(Path.GetDirectoryName(current) ?? "", "Player-prev.log") : current;
            }
            catch { path = ""; }
            if (path.Length == 0 || !File.Exists(path)) { Refuse(rid, "missing"); return; }
            _uploadNotice = kind == "crash"
                ? Loc.T("gps_reports_sent_crash", "The Player.log of your last session (it did not end cleanly) went to the server.")
                : Loc.T("gps_reports_sent_request", "Your Player.log went to the server - it asked for it.");
            string pid = MPConfig.PlayerId;
            _packing = true;
            System.Threading.Tasks.Task.Run(() =>
            {
                try { _packed.Enqueue(Pack(path, rid, kind, pid)); }
                catch (Exception ex) { _packError = ex.Message; }
                finally { _packing = false; }
            });
        }

        /// <summary>BACKGROUND: the last 8 MB of the file, every line cleaned, gzip'd, cut into pieces.</summary>
        private static List<PlayerLogChunkPayload> Pack(string path, string rid, string kind, string pid)
        {
            byte[] raw;
            bool cut;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long start = Math.Max(0, fs.Length - MaxLogBytes);
                cut = start > 0;
                fs.Seek(start, SeekOrigin.Begin);
                raw = new byte[fs.Length - start];
                int read = 0;
                while (read < raw.Length) { int n = fs.Read(raw, read, raw.Length - read); if (n <= 0) break; read += n; }
                if (read < raw.Length) Array.Resize(ref raw, read);
            }
            var sb = new StringBuilder(raw.Length + 512);
            sb.Append("# Player.log (").Append(kind).Append("), cleaned by Going Public Server ").Append(MyPluginInfo.PLUGIN_VERSION)
              .Append(" - PLAYER-REPORTS-1").Append(cut ? " - only the last 8 MB" : "").Append('\n');
            foreach (var line in Encoding.UTF8.GetString(raw).Split('\n')) sb.Append(Clean(line.TrimEnd('\r'))).Append('\n');
            byte[] gz;
            using (var ms = new MemoryStream())
            {
                using (var z = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    var b = Encoding.UTF8.GetBytes(sb.ToString());
                    z.Write(b, 0, b.Length);
                }
                gz = ms.ToArray();
            }
            int count = Math.Max(1, (gz.Length + ChunkBytes - 1) / ChunkBytes);
            if (count > 64) throw new InvalidOperationException("the log is too big even packed");
            var list = new List<PlayerLogChunkPayload>(count);
            for (int i = 0; i < count; i++)
            {
                int off = i * ChunkBytes, len = Math.Min(ChunkBytes, gz.Length - off);
                list.Add(new PlayerLogChunkPayload { PlayerId = pid, RequestId = rid, Kind = kind, Index = i, Count = count, Data = Convert.ToBase64String(gz, off, len) });
            }
            return list;
        }

        // ── the dedicated server ──────────────────────────────────────────────────────────────────────────────────────

        private sealed class Upload
        {
            public byte[]?[] Parts = Array.Empty<byte[]?>();
            public int Have;
            public long Bytes;
            public string Kind = "", Name = "", Id = "";
            public float Started;
        }

        private static readonly ConcurrentQueue<(string pid, PlayerReportPayload p)> _hostIn = new ConcurrentQueue<(string, PlayerReportPayload)>();
        private static readonly ConcurrentQueue<(string pid, PlayerLogChunkPayload c)> _hostChunks = new ConcurrentQueue<(string, PlayerLogChunkPayload)>();
        private static int _hostInCount, _hostChunkCount;
        private static string _root = "";
        private static bool _hostStarted, _offLogged, _hostOn = true;
        private static float _nextOnCheck, _nextSummaryAt, _nextLineAt, _nextPruneAt;
        private static bool _summaryDirty;
        private static readonly Dictionary<string, PlayerReportKindDto> _kinds = new Dictionary<string, PlayerReportKindDto>();
        private static readonly Dictionary<string, PlayerReportPlayerDto> _players = new Dictionary<string, PlayerReportPlayerDto>();
        private static readonly Dictionary<string, float> _lastReportAt = new Dictionary<string, float>();
        private static readonly Dictionary<string, (string rid, float at)> _requests = new Dictionary<string, (string, float)>();
        private static readonly Dictionary<string, Upload> _uploadsIn = new Dictionary<string, Upload>();
        private static readonly Dictionary<string, int> _crashLogsToday = new Dictionary<string, int>();
        private static string _crashDay = "";
        private static int _windowLines, _windowNewKinds, _rateDropped;
        private static readonly HashSet<string> _windowPlayers = new HashSet<string>();
        private static string _windowNewText = "";

        /// <summary>NETWORK THREAD: a player's report (the sender is verified by MPServer); the main thread files it.</summary>
        internal static void HostReceive(string senderPid, PlayerReportPayload p)
        {
            if (p == null || string.IsNullOrEmpty(senderPid)) return;
            if (Interlocked.Increment(ref _hostInCount) > 2000) { Interlocked.Decrement(ref _hostInCount); return; }
            _hostIn.Enqueue((senderPid, p));
        }

        /// <summary>NETWORK THREAD: a piece of a player's Player.log.</summary>
        internal static void HostReceiveChunk(string senderPid, PlayerLogChunkPayload c)
        {
            if (c == null || string.IsNullOrEmpty(senderPid)) return;
            if (Interlocked.Increment(ref _hostChunkCount) > 200) { Interlocked.Decrement(ref _hostChunkCount); return; }
            _hostChunks.Enqueue((senderPid, c));
        }

        /// <summary>The AMP switch "Player error reports" (bamp-server.json PlayerReports, on by default).</summary>
        private static bool HostOn
        {
            get
            {
                float now = Time.unscaledTime;
                if (now >= _nextOnCheck)
                {
                    _nextOnCheck = now + 10f;
                    _hostOn = MPConfig.GetOption("PlayerReports", "true").Trim().ToLowerInvariant() != "false";
                }
                return _hostOn;
            }
        }

        private static void HostTick()
        {
            if (!MPServer.IsRunning) return;
            float now = Time.unscaledTime;
            if (!HostOn)
            {
                while (_hostIn.TryDequeue(out _)) Interlocked.Decrement(ref _hostInCount);
                while (_hostChunks.TryDequeue(out _)) Interlocked.Decrement(ref _hostChunkCount);
                if (!_offLogged && MPServer.WorldRunning)
                {
                    _offLogged = true;
                    Plugin.Logger.LogInfo($"[Reports] players' error reports are off (AMP: Player error reports) {Tag}.");
                }
                return;
            }
            if (!_hostStarted && !StartHost()) return;
            for (int n = 0; n < 50 && _hostIn.TryDequeue(out var item); n++)
            {
                Interlocked.Decrement(ref _hostInCount);
                try { HostAdd(item.pid, item.p, now); } catch (Exception ex) { WarnOnce("report", ex.Message); }
            }
            for (int n = 0; n < 20 && _hostChunks.TryDequeue(out var ch); n++)
            {
                Interlocked.Decrement(ref _hostChunkCount);
                try { HostChunk(ch.pid, ch.c, now); } catch (Exception ex) { WarnOnce("chunk", ex.Message); }
            }
            if (now >= _nextSummaryAt) { _nextSummaryAt = now + 60f; if (_summaryDirty) WriteSummary(); }
            if (now >= _nextLineAt) { _nextLineAt = now + 600f; WindowLine(); }
            if (now >= _nextPruneAt) { _nextPruneAt = now + 3600f; ExpireSummary(); Post(Prune); }
            foreach (var k in _uploadsIn.Where(kv => now - kv.Value.Started > 600f).Select(kv => kv.Key).ToList()) _uploadsIn.Remove(k);
            foreach (var k in _requests.Where(kv => now - kv.Value.at > 900f).Select(kv => kv.Key).ToList()) _requests.Remove(k);
            string err = _writerError;
            if (err.Length > 0) { _writerError = ""; WarnOnce("files", err); }
        }

        private static bool StartHost()
        {
            string dir = HeadlessHost.DirPath ?? "";
            if (dir.Length == 0) return false;
            _root = Path.Combine(Path.GetDirectoryName(dir.TrimEnd('/', '\\')) ?? "", "player-reports");
            try
            {
                Directory.CreateDirectory(_root);
                string readme = Path.Combine(_root, "LIESMICH.txt");
                if (!File.Exists(readme)) File.WriteAllText(readme, ReadmeText, new UTF8Encoding(false));
                LoadSummary();
            }
            catch (Exception ex) { WarnOnce("start", ex.Message); }
            _hostStarted = true;
            _nextLineAt = Time.unscaledTime + 600f;
            _nextPruneAt = Time.unscaledTime + 3600f;
            Post(Prune);
            Plugin.Logger.LogInfo($"[Reports] players' error reports are on: files in bigambitions/player-reports, kept {KeepDays} days {Tag}.");
            return true;
        }

        private const string ReadmeText =
            "Fehlerberichte der Spieler (Going Public Server, PLAYER-REPORTS-1)\n" +
            "Hier landen Fehler, Warnungen und Prüfzeilen aus den Spielen der Leute, die auf diesem Server spielen und dem einmal\n" +
            "zugestimmt haben (AMP-Einstellung \"Fehlerberichte der Spieler\"). Windows-Benutzernamen, IP-Adressen, Steam-IDs,\n" +
            "E-Mail-Adressen und Schlüssel entfernt schon das Spiel.\n" +
            "- summary.json: jede Art von Zeile mit Anzahl, Personen, Builds, Systemen, erstem und letztem Mal, Text und Ablauf\n" +
            "- <Datum>/<Name>_<Kennung>.jsonl: alles, was an diesem Tag von dieser Person kam\n" +
            "- logs/<Datum>/: ganze Player.logs (nach einem Absturz, oder mit dem Konsolenbefehl playerlog <Spieler>)\n" +
            "Der Server löscht alles nach 14 Tagen und hält den Ordner unter 300 MB.\n\n" +
            "Players' error reports (Going Public Server, PLAYER-REPORTS-1)\n" +
            "Errors, warnings and check lines from the games of the people who play on this server and agreed once (AMP setting\n" +
            "\"Player error reports\"). Their games take out Windows user names, IP addresses, Steam IDs, e-mail addresses and keys.\n" +
            "- summary.json: every kind of line with its count, players, builds, systems, first and last time, text and stack\n" +
            "- <date>/<name>_<id>.jsonl: everything that came from that player that day\n" +
            "- logs/<date>/: whole Player.logs (after a crash, or with the console command playerlog <player>)\n" +
            "The server deletes everything after 14 days and keeps the folder under 300 MB.\n";

        private static string SafeName(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "")
            {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-') sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
                if (sb.Length >= 24) break;
            }
            string r = sb.ToString().Trim('_');
            return r.Length == 0 ? "player" : r;
        }

        private static string SafeKey(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 16) return "";
            foreach (char c in s) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return "";
            return s;
        }

        private static string PlatformShort(string p)
        {
            if (p.StartsWith("Proton/Wine", StringComparison.Ordinal)) return "Proton/Wine";
            var w = p.Split(' ');
            return w.Length >= 2 ? w[0] + " " + w[1] : p;
        }

        private static void AddCapped(List<string> list, string v)
        {
            if (string.IsNullOrEmpty(v) || list.Contains(v)) return;
            if (list.Count < 20) list.Add(v);
        }

        private static (string name, string id) Who(string pid)
        {
            string stable = MPServer.StableIdByPlayer.TryGetValue(pid, out var s) && !string.IsNullOrEmpty(s) ? s : pid;
            string name = "";
            try { name = MPServer.DisplayNameFor(pid); } catch { }
            return (SafeName(string.IsNullOrEmpty(name) ? pid : name), Hash8("stable|" + stable));
        }

        private static void HostAdd(string pid, PlayerReportPayload p, float now)
        {
            if (_lastReportAt.TryGetValue(pid, out var last) && now - last < 10f) { _rateDropped++; return; }   // at most one report per 10 s
            _lastReportAt[pid] = now;
            var (name, id) = Who(pid);
            var lines = p.Lines ?? new List<PlayerReportLineDto>();
            if (lines.Count > 200) lines = lines.GetRange(0, 200);
            string nowText = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
            if (!_players.TryGetValue(id, out var pl)) _players[id] = pl = new PlayerReportPlayerDto { Id = id };
            pl.Name = name;
            pl.Build = Cut(p.Build, 16);
            pl.Version = Cut(p.Version, 24);
            pl.Game = Cut(p.Game, 32);
            pl.Platform = Cut(Clean(p.Platform), 120);
            pl.LastSeen = nowText;
            pl.Lines += lines.Count;
            pl.Dropped += Math.Max(0, Math.Min(1_000_000, p.Dropped));
            var clean = new List<PlayerReportLineDto>(lines.Count);
            foreach (var l in lines)
            {
                if (l == null) continue;
                string key = SafeKey(l.Key);
                if (key.Length == 0) continue;
                string kind = l.Kind == "exception" || l.Kind == "error" || l.Kind == "warning" || l.Kind == "check" ? l.Kind : "other";
                int count = Math.Max(0, Math.Min(1_000_000, l.Count));
                string text = Cut(Clean(l.Text ?? ""), MaxText), stack = Cut(Clean(l.Stack ?? ""), MaxStack);
                if (!_kinds.TryGetValue(key, out var k))
                {
                    if (_kinds.Count >= 2000) continue;
                    _kinds[key] = k = new PlayerReportKindDto { Key = key, Kind = kind, FirstSeen = nowText };
                    _windowNewKinds++;
                    if (_windowNewText.Length == 0 && kind != "check" && text.Length > 0) _windowNewText = kind + " '" + Cut(text.Split('\n')[0], 100) + "'";
                }
                if (text.Length > 0) k.Text = text;
                if (stack.Length > 0 && k.Stack.Length == 0) k.Stack = stack;
                k.Count += count;
                k.LastSeen = nowText;
                AddCapped(k.Players, name);
                AddCapped(k.Builds, pl.Build);
                AddCapped(k.Platforms, PlatformShort(pl.Platform));
                clean.Add(new PlayerReportLineDto { Kind = kind, Key = key, Text = text, Stack = stack, Count = count, First = l.First, Last = l.Last });
            }
            _windowLines += clean.Count;
            _windowPlayers.Add(name);
            _summaryDirty = true;
            var rec = new PlayerReportRecordDto
            {
                At = nowText, Player = name, Id = id, Session = SafeKey(p.Session), Build = pl.Build, Version = pl.Version, Game = pl.Game,
                Platform = pl.Platform, Uptime = p.Uptime, Dropped = p.Dropped, Lines = clean,
            };
            string json = JsonConvert.SerializeObject(rec, Formatting.None);
            string file = Path.Combine(_root, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), name + "_" + id + ".jsonl");
            Post(() => AppendLine(file, json));
        }

        private static void HostChunk(string pid, PlayerLogChunkPayload c, float now)
        {
            var (name, id) = Who(pid);
            string kind = c.Kind ?? "", rid = c.RequestId ?? "";
            if (kind == "refused" || kind == "missing")
            {
                if (_requests.TryGetValue(pid, out var asked) && asked.rid == rid) _requests.Remove(pid);
                Plugin.Logger.LogInfo($"[Reports] {name}: the Player.log was not sent ({(kind == "refused" ? "no consent" : "no such file")}) {Tag}.");
                return;
            }
            if (kind != "request" && kind != "crash") return;
            if (kind == "request" && (!_requests.TryGetValue(pid, out var rq) || rq.rid != rid)) return;   // nobody asked
            if (c.Count < 1 || c.Count > 64 || c.Index < 0 || c.Index >= c.Count) return;
            byte[] piece;
            try { piece = Convert.FromBase64String(c.Data ?? ""); } catch { return; }
            if (piece.Length == 0 || piece.Length > ChunkBytes + 1024) return;
            string akey = pid + "|" + rid;
            if (!_uploadsIn.TryGetValue(akey, out var up))
            {
                if (kind == "crash" && !CrashAllowed(id)) return;   // at most 3 a day per player
                if (_uploadsIn.Count >= 16) return;
                _uploadsIn[akey] = up = new Upload { Parts = new byte[]?[c.Count], Kind = kind, Name = name, Id = id, Started = now };
            }
            if (up.Parts.Length != c.Count) return;
            if (up.Parts[c.Index] == null) { up.Parts[c.Index] = piece; up.Have++; up.Bytes += piece.Length; }
            if (up.Bytes > 12L * 1024 * 1024) { _uploadsIn.Remove(akey); return; }
            if (up.Have < up.Parts.Length) return;
            _uploadsIn.Remove(akey);
            if (kind == "request") _requests.Remove(pid);
            var all = new byte[up.Bytes];
            int off = 0;
            foreach (var part in up.Parts)
            {
                var b = part ?? Array.Empty<byte>();
                Buffer.BlockCopy(b, 0, all, off, b.Length);
                off += b.Length;
            }
            string day = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string dir = Path.Combine(_root, "logs", day);
            string file = Path.Combine(dir, $"{up.Name}_{up.Id}_{DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture)}_{kind}.log.gz");
            Post(() => { Directory.CreateDirectory(dir); WriteAtomic(file, all); });
            Plugin.Logger.LogInfo($"[Reports] {up.Name}: Player.log received ({kind}, {Math.Max(1, all.Length / 1024)} KB packed) -> player-reports/logs/{day}/{Path.GetFileName(file)} {Tag}.");
        }

        private static bool CrashAllowed(string id)
        {
            string day = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (day != _crashDay) { _crashDay = day; _crashLogsToday.Clear(); }
            _crashLogsToday.TryGetValue(id, out int n);
            if (n >= 3) return false;
            _crashLogsToday[id] = n + 1;
            return true;
        }

        /// <summary>MAIN THREAD (console "playerlog &lt;player&gt;"): asks a connected player for the whole Player.log.</summary>
        internal static string RequestLog(string pid)
        {
            if (!HeadlessHost.Active) return "ERR only on a dedicated server";
            if (!HostOn) return "ERR player error reports are off (AMP: Player error reports)";
            if (string.IsNullOrEmpty(pid) || !MPServer.IsPlayerConnected(pid)) return $"ERR '{pid}' is not connected";
            string rid = Guid.NewGuid().ToString("N").Substring(0, 8);
            _requests[pid] = (rid, Time.unscaledTime);
            MPServer.SendToPid(pid, MessageEnvelope.Create(MessageType.PlayerLogRequest, "host", new PlayerLogRequestPayload { RequestId = rid }));
            return $"OK asked '{pid}' for the Player.log - it lands in player-reports/logs if the player agreed to error reports";
        }

        /// <summary>MAIN THREAD (console "reports"): the most frequent kinds of lines.</summary>
        internal static List<string> ConsoleSummary()
        {
            var r = new List<string>();
            if (!HeadlessHost.Active) { r.Add("ERR only on a dedicated server"); return r; }
            if (!HostOn) { r.Add("player error reports: off (AMP: Player error reports)"); return r; }
            int errors = _kinds.Values.Count(k => k.Kind != "check");
            r.Add($"player error reports: on - {_players.Count} player(s), {errors} kind(s) of errors and warnings, {_kinds.Count - errors} check line(s); files in bigambitions/player-reports");
            foreach (var k in _kinds.Values.Where(k => k.Kind != "check").OrderByDescending(k => k.Count).Take(8))
                r.Add($"{k.Count,7}x {k.Kind} [{k.Key}] {Cut(k.Text.Split('\n')[0], 110)} - {string.Join(", ", k.Players.Take(3))}{(k.Players.Count > 3 ? " ..." : "")}");
            return r;
        }

        private static void WindowLine()
        {
            if (_windowLines == 0 && _rateDropped == 0) return;
            Plugin.Logger.LogInfo($"[Reports] last 10 min: {_windowPlayers.Count} player(s), {_windowLines} line(s), {_windowNewKinds} new kind(s)" +
                                  (_windowNewText.Length > 0 ? $", e.g. {_windowNewText}" : "") +
                                  (_rateDropped > 0 ? $", {_rateDropped} report(s) too soon - left out" : "") + $" {Tag}.");
            _windowLines = 0; _windowNewKinds = 0; _rateDropped = 0; _windowNewText = ""; _windowPlayers.Clear();
        }

        private static void LoadSummary()
        {
            string file = Path.Combine(_root, "summary.json");
            if (!File.Exists(file)) return;
            var s = JsonConvert.DeserializeObject<PlayerReportSummaryDto>(File.ReadAllText(file));
            if (s == null) return;
            foreach (var k in s.Kinds ?? new List<PlayerReportKindDto>()) if (SafeKey(k.Key).Length > 0) _kinds[k.Key] = k;
            foreach (var pl in s.Players ?? new List<PlayerReportPlayerDto>()) if (!string.IsNullOrEmpty(pl.Id)) _players[pl.Id] = pl;
            ExpireSummary();
        }

        /// <summary>The summary keeps nobody longer than the files: players and kinds of lines not seen for 14 days go, and
        /// so do the names of players who went from the kinds' lists.</summary>
        private static void ExpireSummary()
        {
            var cutoff = DateTimeOffset.Now.AddDays(-KeepDays);
            bool Old(string s) => DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) && t < cutoff;
            int removed = 0;
            foreach (var id in _players.Where(kv => Old(kv.Value.LastSeen)).Select(kv => kv.Key).ToList()) { _players.Remove(id); removed++; }
            foreach (var key in _kinds.Where(kv => Old(kv.Value.LastSeen)).Select(kv => kv.Key).ToList()) { _kinds.Remove(key); removed++; }
            var names = new HashSet<string>(_players.Values.Select(p => p.Name));
            foreach (var k in _kinds.Values) removed += k.Players.RemoveAll(n => !names.Contains(n));
            if (removed > 0) _summaryDirty = true;
        }

        private static void WriteSummary()
        {
            _summaryDirty = false;
            var dto = new PlayerReportSummaryDto
            {
                Updated = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
                Note = "Going Public Server PLAYER-REPORTS-1: errors, warnings and check lines from the players' games (cleaned of personal data), most frequent first; kept 14 days.",
                Players = _players.Values.OrderByDescending(p => p.LastSeen, StringComparer.Ordinal).ToList(),
                Kinds = _kinds.Values.OrderBy(k => k.Kind == "check" ? 1 : 0).ThenByDescending(k => k.Count).Take(1000).ToList(),
            };
            string json = JsonConvert.SerializeObject(dto, Formatting.Indented);
            string file = Path.Combine(_root, "summary.json");
            Post(() => WriteAtomic(file, new UTF8Encoding(false).GetBytes(json)));
        }

        // ── files: one background thread, file work only ──────────────────────────────────────────────────────────────

        private static readonly ConcurrentQueue<Action> _jobs = new ConcurrentQueue<Action>();
        private static readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private static Thread? _writer;
        private static volatile string _writerError = "";

        private static void Post(Action job)
        {
            _jobs.Enqueue(job);
            if (_writer == null)
            {
                _writer = new Thread(WriterLoop) { IsBackground = true, Name = "BAMP-Reports" };
                _writer.Start();
            }
            _wake.Set();
        }

        private static void WriterLoop()
        {
            while (true)
            {
                try
                {
                    _wake.WaitOne(10000);
                    while (_jobs.TryDequeue(out var job))
                    {
                        try { job(); } catch (Exception ex) { _writerError = ex.Message; }
                    }
                }
                catch { Thread.Sleep(1000); }
            }
        }

        private static void AppendLine(string file, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file) ?? _root);
            var fi = new FileInfo(file);
            if (fi.Exists && fi.Length > 20L * 1024 * 1024) return;   // one player's day: 20 MB at most
            File.AppendAllText(file, json + "\n", new UTF8Encoding(false));
        }

        private static void WriteAtomic(string file, byte[] data)
        {
            string tmp = file + ".part";
            File.WriteAllBytes(tmp, data);
            if (File.Exists(file)) File.Delete(file);
            File.Move(tmp, file);
        }

        /// <summary>BACKGROUND: day folders older than 14 days go, then the oldest ones while the folder holds over 300 MB
        /// (the Player.logs first) - never today's, and nothing outside player-reports.</summary>
        private static void Prune()
        {
            if (_root.Length == 0 || !string.Equals(Path.GetFileName(_root.TrimEnd('/', '\\')), "player-reports", StringComparison.Ordinal)) return;
            var cutoff = DateTime.Now.Date.AddDays(-KeepDays);
            string today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var days = new List<(DateTime day, string dir, bool log)>();
            foreach (var (parent, log) in new[] { (Path.Combine(_root, "logs"), true), (_root, false) })
            {
                if (!Directory.Exists(parent)) continue;
                foreach (var d in Directory.GetDirectories(parent))
                {
                    string n = Path.GetFileName(d);
                    if (!DateTime.TryParseExact(n, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) continue;
                    if (day < cutoff) { try { Directory.Delete(d, true); } catch { } continue; }
                    if (n != today) days.Add((day, d, log));
                }
            }
            long total = 0;
            try { total = new DirectoryInfo(_root).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); } catch { }
            foreach (var (_, dir, _) in days.OrderByDescending(x => x.log).ThenBy(x => x.day))
            {
                if (total <= MaxFolderBytes) break;
                long size = 0;
                try { size = new DirectoryInfo(dir).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); Directory.Delete(dir, true); } catch { continue; }
                total -= size;
            }
        }

        private static readonly HashSet<string> _warned = new HashSet<string>();

        private static void WarnOnce(string what, string message)
        {
            if (!_warned.Add(what)) return;
            Plugin.Logger.LogWarning($"[Reports] {what}: {Clean(message)} {Tag}.");
        }
    }

    /// <summary>PLAYER-REPORTS-1: bigambitions/player-reports/summary.json on the dedicated server.</summary>
    public class PlayerReportSummaryDto
    {
        public string Updated { get; set; } = "";
        public string Note { get; set; } = "";
        public List<PlayerReportPlayerDto> Players { get; set; } = new List<PlayerReportPlayerDto>();
        public List<PlayerReportKindDto> Kinds { get; set; } = new List<PlayerReportKindDto>();
    }

    /// <summary>PLAYER-REPORTS-1: a player who sent reports (the id is a hash of the stable id, not the Steam id).</summary>
    public class PlayerReportPlayerDto
    {
        public string Name { get; set; } = "";
        public string Id { get; set; } = "";
        public string Build { get; set; } = "";
        public string Version { get; set; } = "";
        public string Game { get; set; } = "";
        public string Platform { get; set; } = "";
        public string LastSeen { get; set; } = "";
        public long Lines { get; set; }
        public long Dropped { get; set; }
    }

    /// <summary>PLAYER-REPORTS-1: one kind of line over all players.</summary>
    public class PlayerReportKindDto
    {
        public string Key { get; set; } = "";
        public string Kind { get; set; } = "";
        public long Count { get; set; }
        public string Text { get; set; } = "";
        public string Stack { get; set; } = "";
        public List<string> Players { get; set; } = new List<string>();
        public List<string> Builds { get; set; } = new List<string>();
        public List<string> Platforms { get; set; } = new List<string>();
        public string FirstSeen { get; set; } = "";
        public string LastSeen { get; set; } = "";
    }

    /// <summary>PLAYER-REPORTS-1: one received report, one line of a player's day file (.jsonl).</summary>
    public class PlayerReportRecordDto
    {
        public string At { get; set; } = "";
        public string Player { get; set; } = "";
        public string Id { get; set; } = "";
        public string Session { get; set; } = "";
        public string Build { get; set; } = "";
        public string Version { get; set; } = "";
        public string Game { get; set; } = "";
        public string Platform { get; set; } = "";
        public float Uptime { get; set; }
        public int Dropped { get; set; }
        public List<PlayerReportLineDto> Lines { get; set; } = new List<PlayerReportLineDto>();
    }
}
