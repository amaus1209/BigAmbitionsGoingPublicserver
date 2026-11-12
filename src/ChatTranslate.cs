#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>
    /// CHAT-TRANSLATE-1 (user 2026-11-08: "für den Chat eine Live-Übersetzung für Spieler, z. B. ich schreibe in Deutsch und
    /// der andere erhält es in Englisch, er schreibt in Englisch, ich erhalte es in Deutsch"). The user's choices: the
    /// server translates (no account, the chat never leaves it), German and English, the translation large with the
    /// original small and grey below it, on for all and switchable (Options, tab Mods; AMP switch ChatTranslate).
    ///
    /// Dedicated server: bamp-start.sh runs the translator next to the game (bamp-tools/bamp-translate.py: Argos Translate
    /// models on CTranslate2, set up once in bigambitions/bamp-translate/, outside the game folder AMP backs up). It writes
    /// where it listens (127.0.0.1, a token) into headless/translate.json. Each player's game tells the server its language
    /// and the Mods tab's choice (ChatPrefs). While a connected player wants translations, every chat line - the players'
    /// public and private lines, the admin's "[Server]" lines, the server's "—" notices - goes through ONE worker thread in
    /// arrival order. The translator finds the line's language (the sender's game language breaks a tie) and translates it
    /// into the languages wanted. Each player gets the line in their language with the original in ChatPayload.Orig; the
    /// sender and everybody else get it as written. With nobody wanting translations, and without the translator (the
    /// first start still setting up, a Windows server, the switch off), lines go out at once as before - except while
    /// earlier lines are still in the worker, so the order never changes. A line waits at most 3 s for the translator;
    /// after a failure lines go out as written until it answers again.
    ///
    /// Player: sends ChatPrefs when the server announces the rule (ServerRules.ServerTranslatesChat) and whenever the
    /// choice or the game language changes; the chat window shows Orig small and grey under the line
    /// (MPChatWindow.CwRender). Older servers never get ChatPrefs; older clients show the translation only.
    /// The JSON types are named ...Dto so JsonWarmup prepares them on the main thread (JSON-WARMUP-1).
    /// </summary>
    internal static class ChatTranslate
    {
        internal const string Tag = "(CHAT-TRANSLATE-1)";
        private const int MaxChars = 300;            // longer lines go out as written
        private const int TimeoutMs = 3000;
        private const int PerSenderPer10s = 8;       // more lines from one sender in 10 s go out as written
        private static readonly string[] Langs = { "de", "en" };

        private static bool Translatable(string lang) => lang == "de" || lang == "en";

        // ── the player's game ──────────────────────────────────────────────────────────────────────────────────────

        private static string _told = "";            // "lang|1" this connection last told the server ("" = nothing yet)
        private static bool _wasConnected;
        private static float _nextClientCheck;

        private static string PrefKey => string.IsNullOrEmpty(PlayerColours.ModId) ? "gps_chat_translate" : "m:" + PlayerColours.ModId + ":chat_translate";

        /// <summary>The Mods tab's "Translate chat", on by default. The game keeps a toggle as PlayerPrefs int 1/0 under
        /// "m:" + mod id + ":" + option id (ModOptionPrefs).</summary>
        internal static bool Wanted
        {
            get { try { return UnityEngine.PlayerPrefs.GetInt(PrefKey, 1) != 0; } catch { return true; } }
        }

        /// <summary>The game's language as its locale files name it ("de", "en", "zh-cn" ...), "" if unknown.</summary>
        internal static string GameLang()
        {
            try { return (Localizor.LocalizorManager.LoadedLocale ?? "").Trim().ToLowerInvariant(); }
            catch { return ""; }
        }

        /// <summary>The Mods tab's row (PlayerColours.RegisterOptions puts it into the mod's "Multiplayer" section).</summary>
        internal static void AddOptions(BigAmbitions.Mods.ModOptions o)
        {
            o.AddToggle("chat_translate", Loc.T("gps_chattr_option", "Translate the chat (German - English)"), true, v =>
            {
                try
                {
                    Plugin.Logger.LogInfo($"[ChatTr] translate the chat: {(v ? "on" : "off")} (options) {Tag}.");
                    _nextClientCheck = 0f;
                }
                catch { }
            });
        }

        /// <summary>MAIN THREAD, every frame (MPCanvasUI.Update): the player's side, or the dedicated server's.</summary>
        internal static void Tick()
        {
            try
            {
                if (HeadlessHost.Active) HostTick();
                else ClientTick();
            }
            catch (Exception ex) { WarnOnce("tick", $"[ChatTr] tick: {ex.Message} {Tag}."); }
        }

        private static void ClientTick()
        {
            if (!MPClient.IsConnected)
            {
                if (_wasConnected) { _wasConnected = false; _told = ""; }
                return;
            }
            _wasConnected = true;
            float now = Time.unscaledTime;
            if (now < _nextClientCheck) return;
            _nextClientCheck = now + 2f;
            if (!ServerRules.ServerTranslatesChat) return;
            string lang = GameLang();
            bool on = Wanted;
            string key = lang + "|" + (on ? "1" : "0");
            if (key == _told) return;
            _told = key;
            MPClient.SendChatPrefs(lang, on);
            Plugin.Logger.LogInfo($"[ChatTr] the server translates the chat: told it game language '{lang}', translated lines {(on ? "on" : "off")} {Tag}.");
        }

        // ── the dedicated server ───────────────────────────────────────────────────────────────────────────────────

        private sealed class Pref
        {
            public string Lang = "";
            public bool On = true;
        }

        private sealed class Job
        {
            public string From = "", To = "", Text = "", Prefix = "";
            public bool Private, Console;
        }

        private static readonly ConcurrentDictionary<string, Pref> _prefs = new ConcurrentDictionary<string, Pref>();
        private static readonly BlockingCollection<Job> _jobs = new BlockingCollection<Job>(new ConcurrentQueue<Job>(), 64);
        private static readonly object _workerLock = new object();
        private static Thread? _worker;
        private static int _pending;                       // lines queued or being sent (the order rule)
        private static volatile bool _usable;              // the main thread's verdict: switch on, translator known and answering
        private static volatile bool _answering;           // the translator's last answer (any thread)
        private static volatile string _url = "", _token = "", _serverLang = "en";
        private static volatile bool _toldReady;
        private static bool _hostOn = true, _toldMissing, _toldOff;
        private static float _nextOnCheck, _nextState, _nextWindow, _firstTick = -1f, _nextPing;
        private static int _pinging;
        private static readonly Stopwatch _clock = Stopwatch.StartNew();
        private static readonly ConcurrentDictionary<string, long[]> _rate = new ConcurrentDictionary<string, long[]>();
        private static int _wTranslated, _wAsIs, _wFailed, _wCalls;   // the 10-minute console line
        private static long _wMs;
        private static readonly ConcurrentDictionary<string, int> _wPairs = new ConcurrentDictionary<string, int>();
        private static readonly ConcurrentDictionary<string, byte> _warned = new ConcurrentDictionary<string, byte>();

        /// <summary>NETWORK THREAD: a player's ChatPrefs.</summary>
        internal static void HostReceivePrefs(string pid, ChatPrefsPayload p)
        {
            if (string.IsNullOrEmpty(pid) || p == null) return;
            string lang = (p.Lang ?? "").Trim().ToLowerInvariant();
            if (lang.Length > 12) lang = lang.Substring(0, 12);
            _prefs[pid] = new Pref { Lang = lang, On = p.On };
            Plugin.Logger.LogInfo($"[ChatTr] '{pid}': game language '{lang}', translated chat {(p.On ? "on" : "off")}{(Translatable(lang) ? "" : " (no translation into this language - lines come as written)")} {Tag}.");
        }

        /// <summary>A connected player other than <paramref name="from"/> (or exactly <paramref name="only"/>) wants lines
        /// translated into a language the translator has.</summary>
        private static bool AnyoneWants(string from, string? only = null)
        {
            if (only != null) return only != from && _prefs.TryGetValue(only, out var q) && q.On && Translatable(q.Lang);
            foreach (var pid in MPServer.ConnectedPlayerIds())
                if (pid != from && _prefs.TryGetValue(pid, out var p) && p.On && Translatable(p.Lang)) return true;
            return false;
        }

        /// <summary>NETWORK THREAD (MPServer's Chat case): a player's public line. True = taken over (it goes out from the
        /// worker, translated where wanted); false = broadcast it as before.</summary>
        internal static bool HostRelayPublic(ChatPayload cp)
        {
            if (cp == null || !HeadlessHost.Active) return false;
            string from = cp.PlayerId ?? "";
            return Take(new Job { From = from, Text = cp.Text ?? "" }, AnyoneWants(from));
        }

        /// <summary>ANY THREAD (MPServer.BroadcastChat without a sender): the admin's "[Server] text" or a notice "— text".</summary>
        internal static bool HostRelaySystem(string text)
        {
            if (!HeadlessHost.Active || string.IsNullOrEmpty(text)) return false;
            SplitPrefix(text, out string prefix, out string body);
            return Take(new Job { From = "", Text = body, Prefix = prefix }, AnyoneWants(""));
        }

        /// <summary>ANY THREAD (MPServer.SendChatPrivate): a private line - a player's, or the server's to one player. The
        /// Business Hub's notes (MPChat.HubSender) are no chat and stay as they are.</summary>
        internal static bool HostRelayPrivate(string from, string to, string text)
        {
            if (!HeadlessHost.Active || string.IsNullOrEmpty(to) || from == MPChat.HubSender) return false;
            SplitPrefix(text ?? "", out string prefix, out string body);
            return Take(new Job { From = from ?? "", To = to, Text = body, Prefix = prefix, Private = true }, AnyoneWants(from ?? "", to));
        }

        private static void SplitPrefix(string text, out string prefix, out string body)
        {
            prefix = ""; body = text;
            foreach (var p in new[] { "[Server] ", "— " })
                if (text.StartsWith(p, StringComparison.Ordinal)) { prefix = p; body = text.Substring(p.Length); return; }
        }

        /// <summary>Queue a line for the worker when it needs translating, or when earlier lines are still in the worker (so
        /// the order never changes). False = send it at once, as before.</summary>
        private static bool Take(Job job, bool wanted)
        {
            if (!(_usable && wanted) && Volatile.Read(ref _pending) == 0) return false;
            EnsureWorker();
            Interlocked.Increment(ref _pending);
            if (_jobs.TryAdd(job)) return true;
            Interlocked.Decrement(ref _pending);   // 64 lines waiting: this one goes out as written
            return false;
        }

        private static void EnsureWorker()
        {
            if (_worker != null) return;
            lock (_workerLock)
            {
                if (_worker != null) return;
                var t = new Thread(WorkerLoop) { IsBackground = true, Name = "BAMP-ChatTr" };
                t.Start();
                _worker = t;
            }
        }

        private static void WorkerLoop()
        {
            foreach (var job in _jobs.GetConsumingEnumerable())
            {
                try { if (job.Console) ConsoleWork(job); else Work(job); }
                catch (Exception ex) { WarnOnce("work", $"[ChatTr] {ex.GetType().Name}: {ex.Message} {Tag}."); }
                finally { Interlocked.Decrement(ref _pending); }
            }
        }

        /// <summary>WORKER: translate one line where wanted and send it to everyone it is for, in their language.</summary>
        private static void Work(Job job)
        {
            var recipients = job.Private ? new List<string> { job.To } : MPServer.ConnectedPlayerIds();
            var wanted = new HashSet<string>();
            foreach (var pid in recipients)
                if (pid != job.From && _prefs.TryGetValue(pid, out var p) && p.On && Translatable(p.Lang)) wanted.Add(p.Lang);
            ChatTranslateAnswerDto? ans = null;
            if (wanted.Count > 0 && _usable && _answering && job.Text.Trim().Length > 0 && job.Text.Length <= MaxChars && RateOk(job.From))
            {
                string hint = _prefs.TryGetValue(job.From, out var sp) ? sp.Lang : job.From.Length == 0 ? _serverLang : "";
                ans = Call(job.Text, hint, wanted.ToList());
            }
            int translated = 0;
            if (job.Private)
            {
                var cp = Payload(job, job.To, ans);
                if (cp.Orig.Length > 0) translated++;
                MPServer.SendToPid(job.To, MessageEnvelope.Create(MessageType.Chat, "host", cp));
            }
            else
            {
                MPServer.SendChatEach(pid =>
                {
                    var cp = Payload(job, pid, ans);
                    if (cp.Orig.Length > 0) translated++;
                    return cp;
                });
            }
            if (translated > 0) Interlocked.Increment(ref _wTranslated);
            else Interlocked.Increment(ref _wAsIs);
        }

        /// <summary>The line for one player: translated into the player's language when the player wants it and the
        /// translator had it (never for the sender), otherwise as written.</summary>
        private static ChatPayload Payload(Job job, string pid, ChatTranslateAnswerDto? ans)
        {
            var cp = new ChatPayload { PlayerId = job.From, To = job.To, Text = job.Prefix + job.Text };
            if (ans?.Out != null && pid.Length > 0 && pid != job.From && _prefs.TryGetValue(pid, out var p) && p.On
                && ans.Out.TryGetValue(p.Lang, out var t) && !string.IsNullOrWhiteSpace(t))
            {
                cp.Text = job.Prefix + t.Trim();
                cp.Orig = job.Text;
                cp.Lang = ans.Src ?? "";
            }
            return cp;
        }

        private static bool RateOk(string from)
        {
            long now = _clock.ElapsedMilliseconds;
            var w = _rate.GetOrAdd(from, _ => new long[2]);
            if (now - w[0] > 10000) { w[0] = now; w[1] = 0; }
            return ++w[1] <= PerSenderPer10s;
        }

        /// <summary>WORKER: one request to the translator (127.0.0.1 only, the token from translate.json).</summary>
        private static ChatTranslateAnswerDto? Call(string text, string hint, List<string> targets)
        {
            string url = _url, token = _token;
            if (url.Length == 0) return null;
            var sw = Stopwatch.StartNew();
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url + "/translate");
                req.Method = "POST";
                req.Proxy = null;
                req.KeepAlive = false;
                req.Timeout = TimeoutMs;
                req.ReadWriteTimeout = TimeoutMs;
                req.ContentType = "application/json; charset=utf-8";
                req.Headers["X-Token"] = token;
                byte[] body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new ChatTranslateRequestDto { Text = text, Hint = hint ?? "", Targets = targets }));
                req.ContentLength = body.Length;
                using (var s = req.GetRequestStream()) s.Write(body, 0, body.Length);
                string json;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    json = rd.ReadToEnd();
                var a = JsonConvert.DeserializeObject<ChatTranslateAnswerDto>(json);
                if (!_answering) { _answering = true; Plugin.Logger.LogInfo($"[ChatTr] the translator answers again {Tag}."); }
                if (a?.Out != null && a.Out.Count > 0)
                {
                    Interlocked.Increment(ref _wCalls);
                    Interlocked.Add(ref _wMs, sw.ElapsedMilliseconds);
                    foreach (var k in a.Out.Keys) _wPairs.AddOrUpdate((string.IsNullOrEmpty(a.Src) ? "?" : a.Src) + "->" + k, 1, (_, n) => n + 1);
                }
                return a;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _wFailed);
                if (_answering)
                {
                    _answering = false;
                    Plugin.Logger.LogWarning($"[ChatTr] the translator did not answer ({ex.GetType().Name}: {ex.Message}) - lines go out as written until it answers again {Tag}.");
                }
                return null;
            }
        }

        private static void HostTick()
        {
            if (!MPServer.IsRunning) return;
            float now = Time.unscaledTime;
            if (_firstTick < 0f) { _firstTick = now; _nextWindow = now + 600f; }
            if (now >= _nextOnCheck)
            {
                _nextOnCheck = now + 10f;
                _hostOn = MPConfig.GetOption("ChatTranslate", "true").Trim().ToLowerInvariant() != "false";
                string sl = MPConfig.GetOption("ServerLanguage", "en").Trim().ToLowerInvariant();
                _serverLang = Translatable(sl) ? sl : "";
            }
            if (!_hostOn)
            {
                _usable = false;
                if (!_toldOff) { _toldOff = true; Plugin.Logger.LogInfo($"[ChatTr] chat translation is off (AMP switch ChatTranslate) {Tag}."); }
                return;
            }
            _toldOff = false;
            if (now >= _nextState) { _nextState = now + (_url.Length > 0 && _answering ? 60f : 10f); ReadState(); }
            if (_url.Length > 0 && !_answering && now >= _nextPing) { _nextPing = now + 15f; Ping(); }
            _usable = _url.Length > 0 && _answering;
            if (!_usable && !_toldMissing && now - _firstTick > 240f)
            {
                _toldMissing = true;
                Plugin.Logger.LogInfo($"[ChatTr] the translator is not running (yet): the first start sets it up in bigambitions/bamp-translate (the console's [BAMP-Start] lines say how far); until then chat goes out as written {Tag}.");
            }
            if (now >= _nextWindow) { _nextWindow = now + 600f; WindowLine(); }
        }

        /// <summary>MAIN THREAD: where the translator listens (headless/translate.json, written by bamp-translate.py once
        /// its models are loaded, removed when it ends).</summary>
        private static void ReadState()
        {
            string url = "", token = "";
            try
            {
                string f = Path.Combine(HeadlessHost.DirPath, "translate.json");
                if (File.Exists(f))
                {
                    var st = JsonConvert.DeserializeObject<ChatTranslateStateDto>(File.ReadAllText(f));
                    if (st != null && st.Port > 0 && st.Port < 65536 && !string.IsNullOrEmpty(st.Token))
                    {
                        url = "http://127.0.0.1:" + st.Port.ToString(CultureInfo.InvariantCulture);
                        token = st.Token;
                    }
                }
            }
            catch (Exception ex) { WarnOnce("state", $"[ChatTr] headless/translate.json: {ex.Message} {Tag}."); }
            if (url == _url && token == _token) return;
            bool had = _url.Length > 0;
            _token = token;
            _url = url;
            _answering = false;   // a new translator (or none): ask it first
            _nextPing = 0f;
            if (url.Length == 0 && had && _toldReady)
            {
                _toldReady = false;
                Plugin.Logger.LogInfo($"[ChatTr] the translator has stopped - chat goes out as written {Tag}.");
            }
        }

        /// <summary>MAIN THREAD: asks the translator whether it is there (a pool thread does the request).</summary>
        private static void Ping()
        {
            if (Interlocked.Exchange(ref _pinging, 1) == 1) return;
            string url = _url, token = _token;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var req = (HttpWebRequest)WebRequest.Create(url + "/health");
                    req.Method = "GET";
                    req.Proxy = null;
                    req.KeepAlive = false;
                    req.Timeout = TimeoutMs;
                    req.ReadWriteTimeout = TimeoutMs;
                    req.Headers["X-Token"] = token;
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    {
                        if (resp.StatusCode != HttpStatusCode.OK || url != _url) return;
                    }
                    _answering = true;
                    if (!_toldReady)
                    {
                        _toldReady = true;
                        Plugin.Logger.LogInfo($"[ChatTr] chat translation is on: the translator answers ({sw.ElapsedMilliseconds} ms), German <-> English {Tag}.");
                    }
                }
                catch (Exception ex) { WarnOnce("ping", $"[ChatTr] the translator does not answer yet ({ex.GetType().Name}: {ex.Message}) {Tag}."); }
                finally { Interlocked.Exchange(ref _pinging, 0); }
            });
        }

        /// <summary>MAIN THREAD (console "translate &lt;text&gt;"): a test - the translator's answer comes as console lines
        /// "translate de -> en: ..." once it has answered.</summary>
        internal static string ConsoleTranslate(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return "ERR usage: translate <text>";
            if (!HeadlessHost.Active) return "ERR only on a dedicated server";
            if (!_hostOn) return "ERR chat translation is off (AMP switch ChatTranslate)";
            if (_url.Length == 0) return "ERR the translator is not running (yet) - see the [BAMP-Start] lines";
            if (text.Length > MaxChars) return $"ERR at most {MaxChars} characters";
            EnsureWorker();
            Interlocked.Increment(ref _pending);
            if (!_jobs.TryAdd(new Job { Text = text, Console = true })) { Interlocked.Decrement(ref _pending); return "ERR busy, try again"; }
            return "OK asked the translator";
        }

        private static void ConsoleWork(Job job)
        {
            var a = Call(job.Text, _serverLang, new List<string>(Langs));
            if (a == null) { HeadlessHost.Emit("OUT", "translate: the translator did not answer"); return; }
            string src = string.IsNullOrEmpty(a.Src) ? "?" : a.Src;
            if (a.Out == null || a.Out.Count == 0) { HeadlessHost.Emit("OUT", $"translate: language '{src}', nothing to translate"); return; }
            foreach (var kv in a.Out) HeadlessHost.Emit("OUT", $"translate {src} -> {kv.Key}: {kv.Value}");
        }

        /// <summary>MAIN THREAD, every 10 minutes when lines were translated: one console line.</summary>
        private static void WindowLine()
        {
            int tr = Interlocked.Exchange(ref _wTranslated, 0), asis = Interlocked.Exchange(ref _wAsIs, 0);
            int failed = Interlocked.Exchange(ref _wFailed, 0), calls = Interlocked.Exchange(ref _wCalls, 0);
            long ms = Interlocked.Exchange(ref _wMs, 0);
            var pairs = _wPairs.ToArray();
            _wPairs.Clear();
            if (tr + failed == 0) return;
            string p = string.Join(", ", pairs.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + " " + kv.Value));
            string avg = calls > 0 ? (ms / (double)calls / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + " s each" : "-";
            Plugin.Logger.LogInfo($"[ChatTr] last 10 min: {tr} line(s) translated ({p}), {asis} as written, {avg}{(failed > 0 ? $", {failed} without an answer" : "")} {Tag}.");
        }

        private static void WarnOnce(string what, string message)
        {
            if (_warned.TryAdd(what, 0)) { try { Plugin.Logger.LogWarning(message); } catch { } }
        }
    }

    /// <summary>CHAT-TRANSLATE-1: the server's request to the translator (bamp-translate.py).</summary>
    public class ChatTranslateRequestDto
    {
        [JsonProperty("text")] public string Text { get; set; } = "";
        [JsonProperty("hint")] public string Hint { get; set; } = "";
        [JsonProperty("targets")] public List<string> Targets { get; set; } = new List<string>();
    }

    /// <summary>CHAT-TRANSLATE-1: the translator's answer - the line's language ("" = none found or nothing to translate)
    /// and the translations that differ from the line.</summary>
    public class ChatTranslateAnswerDto
    {
        [JsonProperty("src")] public string Src { get; set; } = "";
        [JsonProperty("out")] public Dictionary<string, string> Out { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>CHAT-TRANSLATE-1: headless/translate.json - where the translator listens.</summary>
    public class ChatTranslateStateDto
    {
        [JsonProperty("port")] public int Port { get; set; }
        [JsonProperty("token")] public string Token { get; set; } = "";
    }
}
