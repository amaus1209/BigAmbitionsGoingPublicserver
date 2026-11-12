using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>
    /// HEADLESS-1: the server mode for an unattended machine (AMP, systemd, a VM without a screen). Builds on the dedicated
    /// host (DedicatedHost.cs: idle pause, auto-accept, auto-start of the newest save, same mods for everyone) and adds what
    /// a process nobody can see or click needs:
    ///
    ///   * DETECTION: on when the game runs with -batchmode (normally together with -nographics: no window, no GPU), with
    ///     -bamp-headless (e.g. under Xvfb when -nographics breaks something), or with the config key Headless = "true".
    ///     A headless process is always a dedicated host.
    ///   * CONSOLE: one-line events on stdout, prefixed "[BAMP-HEADLESS]" (BOOT, STATE, READY, JOIN, LEAVE, HEARTBEAT,
    ///     STOPPING, EXIT, FATAL) - AMP matches them for "ready" and its player list. The same lines go to events.log.
    ///   * COMMANDS: from stdin (AMP's console) and from command.txt in the headless folder (for scripts, or when stdin does
    ///     not reach the game): help, status, players, save, say, msg, kick, ban, unban, banlist, chat, stop, restart - and
    ///     "admin ..." (ADMIN-1), one JSON line for the admin page of the ranking site.
    ///   * CHAT (ADMIN-1): every public chat line as "CHAT name: text" (AMP's chat pattern), the last 100 kept for the admin
    ///     page.
    ///   * STATUS FILE: status.json in the headless folder, rewritten every 5 s.
    ///   * RESOURCES: frame cap while running (HeadlessFps), a lower one while the world waits for players (HeadlessIdleFps),
    ///     no cap while loading; audio muted; vSync off; optional: all cameras off (HeadlessDisableCameras).
    ///   * WATCHDOGS: a world load that never finishes, an auto-start that cannot succeed, a main thread that stops ticking,
    ///     and a quit that hangs all end the process with a FATAL line and a non-zero exit code, so the supervisor restarts it
    ///     instead of an unattended host idling forever. A clean "stop" exits with 0; "restart" with 10.
    ///
    /// Saving on stop is the mod's existing quit path: Application.Quit -> MPCanvasUI.OnApplicationQuit -> the host's quit
    /// checkpoint ('-disconnect' save), which the next auto-start picks up as the newest save. Once that and the mods' unload
    /// are done, the process ends itself (SHUTDOWN-HANG-1, OnQuitting): under Proton the game's own teardown never finishes.
    ///
    /// Headless folder: "-bamp-headless-dir &lt;path&gt;" on the command line, else the key HeadlessDir, else
    /// &lt;BigAmbitionsMP data folder&gt;\headless. Under Wine/Proton a Linux folder is given as Z:\path\to\folder.
    /// </summary>
    public static class HeadlessHost
    {
        private const string Tag = "[BAMP-HEADLESS]";
        private const int QuitGraceSeconds = 120;
        private const int HeartbeatSeconds = 300;
        public const int ExitStop = 0, ExitFatal = 2, ExitNeedsOperator = 3, ExitStalled = 4, ExitModBroken = 5, ExitRestart = 10;

        private static volatile bool _active;
        private static bool _batchArg, _noGraphicsArg, _flagArg;
        private static bool _inited;
        private static int  _fps = 30, _idleFps = 5, _loadTimeout = 900, _stallSeconds = 300;
        private static bool _disableCameras;
        private static string _dir = "", _statusPath = "", _cmdPath = "", _eventsPath = "";
        private static readonly ConcurrentQueue<string> _cmds = new ConcurrentQueue<string>();
        private static readonly Stopwatch _clock = Stopwatch.StartNew();
        private static long _lastTickMs;
        private static volatile bool _stopping;
        private static int _exitCode;   // what the quit was requested with
        private static readonly object _outLock = new object();
        // ADMIN-1: since when each player is on (for "players"), and the last chat lines (for the admin page)
        private static readonly Dictionary<string, DateTime> _since = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private static readonly List<(DateTime at, string who, string text)> _chat = new List<(DateTime, string, string)>();
        private const int ChatKeep = 100;
        // ADMIN-1: a restart the admin announced ("restart 5") - warnings at 5 min, 1 min, 30 s and 10 s, then the restart
        private static DateTime _restartAt = DateTime.MinValue;
        private static int _restartWarned = int.MaxValue;
        private static int _countdownCode = ExitRestart;   // what the countdown ends in: a restart, or (AMP's "stop warn") a stop
        private static string _countdownWhy = "";   // AUTO-UPDATE-1: "update to version ..." in every warning of the countdown
        private static readonly int[] RestartWarnings = { 300, 60, 30, 10 };

        private static float  _nextTickAt;
        private static int    _ticks;
        private static string _state = "starting";
        private static float  _stateSince;
        private static bool   _readyEmitted;
        private static int    _appliedFps = int.MinValue;
        private static long   _nextHeartbeatMs;
        private static HashSet<string> _players = new HashSet<string>(StringComparer.Ordinal);
        private static int    _pid;
        private static bool   _nullGfx;

        public static bool Active => _active;
        /// <summary>The headless folder (status.json, command.txt, ...); "" when not headless.</summary>
        internal static string DirPath => _dir;
        public static bool Stopping => _stopping;
        /// <summary>AUTO-UPDATE-1: an announced restart (or AMP's announced stop) is counting down.</summary>
        internal static bool RestartPending => _restartAt != DateTime.MinValue;
        /// <summary>AUTO-UPDATE-1: players on the server now.</summary>
        internal static int PlayerCount => _players.Count;
        /// <summary>AUTO-UPDATE-1: a console answer from another thread or class ("OUT ...").</summary>
        internal static void Out(string text) => Reply(text);

        /// <summary>AUTO-UPDATE-1: the notice on every screen, then the restart - after a countdown of secs (its warnings
        /// as for "restart &lt;minutes&gt;"), or at once (secs 0, the world is saved first). false when one already runs.</summary>
        internal static bool AnnounceRestart(int secs, string notice, string why = "")
        {
            if (_restartAt != DateTime.MinValue || _stopping) return false;
            _countdownWhy = why;
            try { if (MPServer.IsRunning && notice.Length > 0) MPServer.BroadcastNotice(notice); } catch { }
            Emit("OUT", notice);
            if (secs > 0) Countdown(secs, ExitRestart);
            else RequestQuit("automatic update", ExitRestart);
            return true;
        }

        // ── detection (called from MPConfig.Init, before anything else of this class) ─────────────────────────────

        public static bool Detect()
        {
            bool cfg = false;
            try
            {
                foreach (var a in Environment.GetCommandLineArgs())
                {
                    if (string.Equals(a, "-batchmode", StringComparison.OrdinalIgnoreCase)) _batchArg = true;
                    else if (string.Equals(a, "-nographics", StringComparison.OrdinalIgnoreCase)) _noGraphicsArg = true;
                    else if (string.Equals(a, "-bamp-headless", StringComparison.OrdinalIgnoreCase)) _flagArg = true;
                }
            }
            catch { }
            try { if (!_batchArg && Application.isBatchMode) _batchArg = true; } catch { }
            try { cfg = ReadBool("Headless", false); } catch { }
            _active = _batchArg || _flagArg || cfg;
            return _active;
        }

        // ── start-up (main thread, ModEntry.OnLoadAsync, after MPConfig.Init) ────────────────────────────────────

        public static void Init()
        {
            if (!_active || _inited) return;
            _inited = true;
            try
            {
                _fps          = ReadInt("HeadlessFps", 200, 60, 240);   // 60 / 120 / 200 (user 2026-10-31: no step under 60)
                _idleFps      = ReadInt("HeadlessIdleFps", 5, 1, 60);
                _loadTimeout  = ReadInt("HeadlessLoadTimeoutSeconds", 900, 0, 7200);   // 0 = off
                _stallSeconds = ReadInt("HeadlessStallSeconds", 300, 0, 3600);          // 0 = off
                _disableCameras = ReadBool("HeadlessDisableCameras", false);
                try { _pid = Process.GetCurrentProcess().Id; } catch { }

                _dir = ArgValue("-bamp-headless-dir");
                if (_dir.Length == 0) _dir = MPConfig.GetOption("HeadlessDir", "").Trim();
                if (_dir.Length == 0) _dir = Path.Combine(MPConfig.DataRootPath, "headless");
                Directory.CreateDirectory(_dir);
                _statusPath = Path.Combine(_dir, "status.json");
                _cmdPath    = Path.Combine(_dir, "command.txt");
                _eventsPath = Path.Combine(_dir, "events.log");
                try { if (File.Exists(_eventsPath) && new FileInfo(_eventsPath).Length > 5_000_000) { File.Copy(_eventsPath, _eventsPath + ".1", true); File.Delete(_eventsPath); } } catch { }
                try { if (File.Exists(_cmdPath)) File.Delete(_cmdPath); } catch { }   // a stale "stop" from the last run must not end this one

                try { Application.runInBackground = true; } catch { }
                try { QualitySettings.vSyncCount = 0; } catch { }
                try { AudioListener.volume = 0f; } catch { }
                try { _nullGfx = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null; } catch { }
                ApplyFps(true);

                Interlocked.Exchange(ref _lastTickMs, _clock.ElapsedMilliseconds);
                StartConsoleReader();
                StartStallWatchdog();
                try { Console.CancelKeyPress += OnCancelKey; } catch { }
                try { Application.quitting += OnQuitting; } catch { }   // after the mod loader's own handler (it subscribed first)

                Emit("BOOT", $"version={MyPluginInfo.PLUGIN_VERSION} batchmode={_batchArg} nographics={_noGraphicsArg} nullgfx={_nullGfx} " +
                             $"fps={_fps}/{_idleFps} session='{MPConfig.DedicatedSession}' dir='{_dir}'");
                ApplyServerLanguage();
                if (MPConfig.DedicatedSession.Length == 0)
                    Emit("INFO", "DedicatedSession is not set - hosting the world played last (a server without any save starts its first world 'Server').");
                WriteStatus();
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Headless] init: {ex}"); }
        }

        // ── per frame (main thread; MPCanvasUI.TickDedicated) ───────────────────────────────────────────────────────

        public static void Tick()
        {
            if (!_active) return;
            Interlocked.Exchange(ref _lastTickMs, _clock.ElapsedMilliseconds);
            try
            {
                while (_cmds.TryDequeue(out var line)) Execute(line);

                float now = Time.unscaledTime;
                if (now < _nextTickAt) return;
                _nextTickAt = now + 1f;
                _ticks++;

                PollCommandFile();
                TickRestart();
                try { AutoUpdate.Tick(now, _state == "idle" || _state == "running"); } catch (Exception ex) { Plugin.Logger.LogWarning("[Update] " + ex.Message); }   // AUTO-UPDATE-1

                string state = ComputeState();
                if (state != _state)
                {
                    string old = _state;
                    _state = state;
                    _stateSince = now;
                    Emit("STATE", $"{old}->{state}");
                    if (state == "menu") _readyEmitted = false;
                    if ((state == "idle" || state == "running") && !_readyEmitted)
                    {
                        _readyEmitted = true;
                        Emit("READY", $"session='{MPServer.ChosenLoadSession}' port={MPConfig.HostPort}");
                        AmpSavePicker.MarkDirty();   // AMP-PICK-1: the mod's texts are loaded by now
                    }
                    ApplyFps(true);
                    WriteStatus();
                }

                TrackPlayers();
                AmpInfoLine.Tick(now, _state, _restartAt != DateTime.MinValue);   // AMP-INFO-2: AMP's status page line
                if (_ticks % ServerStats.EverySeconds == 0) ServerStats.Tick(now);   // NET-PAGE-1: the ranking page's server numbers

                if (_ticks % 5 == 0)
                {
                    WriteStatus();
                    try { AudioListener.volume = 0f; } catch { }   // the game re-applies its own volume from the options
                    if (_disableCameras) DisableCameras();
                }

                long ms = _clock.ElapsedMilliseconds;
                if (ms >= _nextHeartbeatMs)
                {
                    _nextHeartbeatMs = ms + HeartbeatSeconds * 1000L;
                    Emit("HEARTBEAT", $"state={_state} players={_players.Count} uptime={ms / 1000}s");
                }

                // watchdogs
                if (_stopping) return;
                if (_state == "loading" && _loadTimeout > 0 && now - _stateSince > _loadTimeout)
                {
                    FatalExit($"the world did not finish loading within {_loadTimeout} s.", ExitFatal);
                    return;
                }
                if ((_state == "menu" || _state == "lobby") && DedicatedHost.NeedsOperator(out string why))
                    FatalExit(why, ExitNeedsOperator);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Headless] tick: {ex.Message}"); }
        }

        private static string ComputeState()
        {
            if (_stopping) return "stopping";
            if (!MPServer.IsRunning) return "menu";
            if (MPServer.IsInLobby) return "lobby";
            if (!MPServer.WorldRunning) return "loading";
            return MPServer.PausedByIdle ? "idle" : "running";
        }

        private static void TrackPlayers()
        {
            var now = new HashSet<string>(StringComparer.Ordinal);
            if (MPServer.IsRunning)
                foreach (var p in MPServer.ConnectedPids())
                    if (!string.IsNullOrEmpty(p)) now.Add(p);
            foreach (var p in now) if (!_players.Contains(p)) { Emit("JOIN", p); lock (_since) _since[p] = DateTime.UtcNow; }
            foreach (var p in _players) if (!now.Contains(p)) { Emit("LEAVE", p); lock (_since) _since.Remove(p); }
            if (now.Count != _players.Count || !now.SetEquals(_players)) { _players = now; WriteStatus(); }
        }

        private static void ApplyFps(bool force)
        {
            int want = _state == "idle" ? _idleFps : _state == "loading" ? -1 : _fps;
            if (!force && want == _appliedFps) return;
            _appliedFps = want;
            try { QualitySettings.vSyncCount = 0; Application.targetFrameRate = want; } catch { }
        }

        private static bool _camsLogged;
        private static void DisableCameras()
        {
            try
            {
                int n = 0;
                foreach (var c in Camera.allCameras)
                    if (c != null && c.enabled) { c.enabled = false; n++; }
                if (n > 0 && !_camsLogged) { _camsLogged = true; Plugin.Logger.LogInfo($"[Headless] {n} camera(s) switched off (HeadlessDisableCameras)."); }
            }
            catch { }
        }

        // ── commands ────────────────────────────────────────────────────────────────────────────────────────────

        private static void Execute(string line)
        {
            line = (line ?? "").Trim();
            if (line.Length == 0) return;
            string cmd = line, arg = "";
            int sp = line.IndexOf(' ');
            if (sp > 0) { cmd = line.Substring(0, sp); arg = line.Substring(sp + 1).Trim(); }
            try
            {
                switch (cmd.ToLowerInvariant())
                {
                    case "help":
                        Reply("commands: status | players | save | autosave | saves | load <#N|name> | say <text> | notice <text> | msg <player> <text> | kick <player> [reason] | ban <player> [reason] | unban <player|key|all> | banlist | chat [N] | reports | playerlog <player> | translate <text> | stop [warn] | restart [minutes|cancel] | update");
                        Reply("a name with spaces in quotes: kick \"Max Muster\" too loud");
                        break;
                    case "status":
                        Reply($"state={_state} session='{MPServer.ChosenLoadSession}' day={GameDay()} players={_players.Count}" +
                              (_players.Count > 0 ? $" [{string.Join(", ", _players)}]" : "") +
                              $" idlePause={MPServer.PausedByIdle} disconnectPause={MPServer.PausedByDisconnect} uptime={_clock.ElapsedMilliseconds / 1000}s");
                        break;
                    case "players":
                    case "list":
                    {
                        var ps = PlayerRows();
                        Reply(ps.Count == 0 ? "no players online" : $"{ps.Count} online: " + string.Join(", ", ps.ConvertAll(r =>
                            $"{r.pid} ({(r.ping >= 0 ? r.ping + " ms" : "? ms")}, {Span(r.secs)})")));
                        break;
                    }
                    case "save":   // DEDICATED-SAVES-1: the world's main save (as the pause menu's Save); 'autosave' = a rotation slot
                        if (!MPServer.IsRunning || !MPServer.WorldRunning) { Reply("ERR no world is running"); break; }
                        MPSaveCoordinator.MenuSave(false, "");
                        Reply($"OK saved '{MPSaveCoordinator.CurrentSessionBaseForDisplay()}'");
                        break;
                    case "autosave":
                        if (!MPServer.IsRunning || !MPServer.WorldRunning) { Reply("ERR no world is running"); break; }
                        MPSaveCoordinator.HostSaveNow("autosave");
                        Reply("OK save started (autosave slot)");
                        break;
                    case "saves":
                        foreach (var l in DedicatedHost.DescribeSaves()) Reply(l);
                        break;
                    case "load":
                    {
                        string msg = DedicatedHost.RequestLoad(arg, out bool ok);
                        Reply(msg);
                        if (ok) RequestQuit($"load '{arg}' requested", ExitRestart);
                        break;
                    }
                    case "say":
                        if (arg.Length == 0) { Reply("ERR usage: say <text>"); break; }
                        if (!MPServer.IsRunning) { Reply("ERR not hosting"); break; }
                        MPServer.BroadcastChat("", "[Server] " + arg);
                        Chat("Server", arg);
                        Reply("OK");
                        break;
                    case "msg":   // ADMIN-1: a private line to one player
                    {
                        ParseTarget(arg, out string who, out string text);
                        if (who.Length == 0 || text.Length == 0) { Reply("ERR usage: msg <player> <text>"); break; }
                        Reply(MPServer.MessagePlayer(who, text));
                        break;
                    }
                    case "kick":   // ADMIN-1: out of the session, may join again at once (no ban)
                    {
                        ParseTarget(arg, out string who, out string why);
                        if (who.Length == 0) { Reply("ERR usage: kick <player> [reason]"); break; }
                        Reply(MPServer.KickPlayer(who, why));
                        break;
                    }
                    case "ban":   // a saved ban, until unban (ADMIN-1: with a reason)
                    {
                        ParseTarget(arg, out string who, out string why);
                        if (who.Length == 0) { Reply("ERR usage: ban <player> [reason]"); break; }
                        Reply(MPServer.BanPlayer(who, why));
                        break;
                    }
                    case "unban":
                        if (arg.Length == 0) { Reply("ERR usage: unban <player|key|all>"); break; }
                        Reply(MPServer.UnbanPlayer(arg));
                        break;
                    case "banlist":
                    {
                        var rows = MPServer.BanList();
                        if (rows.Count == 0) { Reply("no saved bans"); break; }
                        foreach (var r in rows) Reply($"ban key={r.Key} name='{r.Name}' day={r.BanDay} since={r.BannedUtc} ({r.How}){(r.Reason.Length > 0 ? " reason: " + r.Reason : "")}");
                        break;
                    }
                    case "chat":   // ADMIN-1: the last chat lines
                    {
                        int n = int.TryParse(arg, out int k) ? Mathf.Clamp(k, 1, ChatKeep) : 20;
                        var lines = ChatLines(n);
                        if (lines.Count == 0) { Reply("no chat yet"); break; }
                        foreach (var c in lines) Reply($"{c.at.ToLocalTime():HH:mm} {c.who}: {c.text}");
                        break;
                    }
                    case "admin":   // ADMIN-1: the admin page's data, one line "DATA <id> <json>"
                        AdminData(arg);
                        break;
                    case "reports":   // PLAYER-REPORTS-1: the players' most frequent errors (files in bigambitions/player-reports)
                        foreach (var row in PlayerReports.ConsoleSummary()) Reply(row);
                        break;
                    case "translate":   // CHAT-TRANSLATE-1: a test of the chat translator (the answer follows as its own line)
                        Reply(ChatTranslate.ConsoleTranslate(arg));
                        break;
                    case "playerlog":   // PLAYER-REPORTS-1: ask a player's game for the whole (cleaned) Player.log
                    {
                        ParseTarget(arg, out string who, out string _);
                        if (who.Length == 0) { Reply("ERR usage: playerlog <player>"); break; }
                        Reply(PlayerReports.RequestLog(who));
                        break;
                    }
                    case "stop":
                    case "quit":
                    case "exit":
                        // ADMIN-1 (user 2026-11-02: "announcements and an announced restart through AMP too"): "stop warn" is what
                        // AMP sends to stop, restart or update the server (App.ExitString) - with players on, they are warned first
                        // (StopWarnSeconds, at 60, 30 and 10 s) and the world is saved after; nobody on = at once. A plain "stop"
                        // always ends at once, a countdown too.
                        if (arg.Trim().Equals("warn", StringComparison.OrdinalIgnoreCase) && _players.Count > 0 && MPConfig.StopWarnSeconds > 0 && MPServer.IsRunning)
                        {
                            if (_restartAt != DateTime.MinValue && _countdownCode == ExitStop) { Reply("OK the stop is announced already"); break; }
                            int secs = MPConfig.StopWarnSeconds;
                            Countdown(secs, ExitStop);
                            Reply($"OK stop in {secs} s - the players are warned");
                            break;
                        }
                        RequestQuit("console command", ExitStop);
                        break;
                    case "restart":   // ADMIN-1: "restart <minutes>" announces it (warnings on every screen), "restart cancel" calls it off
                    {
                        string a = arg.Trim().ToLowerInvariant();
                        if (_restartAt != DateTime.MinValue && _countdownCode == ExitStop) { Reply("ERR AMP is stopping the server - its stop cannot be changed here"); break; }
                        if (a == "cancel" || a == "abort")
                        {
                            if (_restartAt == DateTime.MinValue) { Reply("ERR no restart is announced"); break; }
                            _restartAt = DateTime.MinValue;
                            MPServer.BroadcastNotice(Loc.Server("gps_admin_restart_off", "The restart is called off."));
                            Reply("OK restart called off");
                            break;
                        }
                        if (a.Length > 0 && double.TryParse(a.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double min) && min > 0)
                        {
                            min = Math.Min(min, 24 * 60);
                            int secs = (int)Math.Round(min * 60);
                            _countdownWhy = "";
                            Countdown(secs, ExitRestart);
                            Reply($"OK restart in {Left(secs)}");
                            break;
                        }
                        RequestQuit("restart requested", ExitRestart);
                        break;
                    }
                    case "update":   // AUTO-UPDATE-1: look for a new build on GitHub now
                        Reply(AutoUpdate.Command());
                        break;
                    case "notice":   // ADMIN-1: an announcement on every player's screen
                        if (arg.Length == 0) { Reply("ERR usage: notice <text>"); break; }
                        if (!MPServer.IsRunning) { Reply("ERR not hosting"); break; }
                        MPServer.BroadcastNotice(arg);
                        Reply("OK");
                        break;
                    default:
                        Reply($"ERR unknown command '{cmd}' - try: help");
                        break;
                }
            }
            catch (Exception ex) { Reply($"ERR {cmd}: {ex.Message}"); }
        }

        private static void Reply(string text) => Emit("OUT", text);

        // ── ADMIN-1 (user 2026-11-02: "an interface for the server admins with chat, ban list and so on - or through the
        //    ranking login"): the commands and the data the ranking site's admin page sends and reads through AMP ──

        /// <summary>A public chat line (a player's, or the server's own): one console line "CHAT name: text" - AMP's chat
        /// pattern (Console.UserChatRegex) - and kept for the admin page. Any thread; not into the game's log (bug reports).</summary>
        internal static void Chat(string who, string text)
        {
            if (!_active) return;
            who = OneLine(who, 64).Replace(':', ' ').Trim();
            text = OneLine(text, 500);
            if (who.Length == 0 || text.Length == 0) return;
            lock (_chat) { _chat.Add((DateTime.UtcNow, who, text)); if (_chat.Count > ChatKeep) _chat.RemoveAt(0); }
            Emit("CHAT", who + ": " + text, false);
        }

        private static List<(DateTime at, string who, string text)> ChatLines(int n)
        {
            lock (_chat) return _chat.GetRange(Math.Max(0, _chat.Count - n), Math.Min(n, _chat.Count));
        }

        /// <summary>No line breaks or other control characters (one console line each), at most max characters.</summary>
        private static string OneLine(string s, int max)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "") sb.Append(char.IsControl(c) ? ' ' : c);
            string t = sb.ToString().Trim();
            return t.Length > max ? t.Substring(0, max) : t;
        }

        private static string Left(int secs) => secs >= 60 ? $"{(secs + 30) / 60} min" : $"{secs} s";

        /// <summary>ADMIN-1: start the countdown of an announced restart (or of AMP's stop): the first warning at once.</summary>
        private static void Countdown(int secs, int code)
        {
            _countdownCode = code;
            if (code == ExitStop) _countdownWhy = "";
            _restartAt = DateTime.UtcNow.AddSeconds(secs);
            _restartWarned = int.MaxValue;
            MPServer.BroadcastNotice(CountdownText(secs));
            foreach (int w in RestartWarnings) if (w >= secs) _restartWarned = w;   // no second warning for the same moment
            WriteStatus();
        }

        private static string CountdownText(int secs) => _countdownCode == ExitStop
            ? Loc.Server("gps_admin_stop_in", "The server goes down in {0} (restart or maintenance).", Left(secs))
            : Loc.Server("gps_admin_restart_in", "The server restarts in {0}.", Left(secs)) + (_countdownWhy.Length > 0 ? " " + _countdownWhy : "");

        /// <summary>ADMIN-1: the countdown's warnings, then the restart or stop itself (main thread, once a second).</summary>
        private static void TickRestart()
        {
            if (_restartAt == DateTime.MinValue || _stopping) return;
            int left = (int)Math.Ceiling((_restartAt - DateTime.UtcNow).TotalSeconds);
            if (left <= 0)
            {
                _restartAt = DateTime.MinValue;
                RequestQuit(_countdownCode == ExitStop ? "AMP's stop, announced" : "announced restart", _countdownCode);
                return;
            }
            foreach (int w in RestartWarnings)
                if (left <= w && _restartWarned > w)
                {
                    _restartWarned = w;
                    MPServer.BroadcastNotice(CountdownText(w));
                    break;
                }
        }

        private static string Span(int secs) => secs < 3600 ? $"{secs / 60} min" : $"{secs / 3600} h {secs / 60 % 60} min";

        /// <summary>The players on the server now: id, name, ping (-1 = unknown), seconds on.</summary>
        private static List<(string pid, string name, int ping, int secs)> PlayerRows()
        {
            var rows = new List<(string, string, int, int)>();
            if (!MPServer.IsRunning) return rows;
            foreach (var (link, pid) in MPServer.ConnectedClientPeers())
            {
                if (link == null || string.IsNullOrEmpty(pid)) continue;
                int ping = -1;
                try { NetAdapt.Reading(pid, out ping, out _); if (ping < 0) ping = link.PingMs; } catch { }
                string name = pid;
                try { string n = MPNames.Resolve(pid); if (!string.IsNullOrWhiteSpace(n)) name = n; } catch { }
                int secs = 0;
                lock (_since) if (_since.TryGetValue(pid, out var at)) secs = (int)(DateTime.UtcNow - at).TotalSeconds;
                rows.Add((pid, name, ping, secs));
            }
            return rows;
        }

        /// <summary>"name rest": a name in quotes ("Max Muster" too loud), else the longest run of words that is a player this
        /// server knows (case aside), else the first word.</summary>
        private static void ParseTarget(string arg, out string name, out string rest)
        {
            name = rest = "";
            arg = (arg ?? "").Trim();
            if (arg.Length == 0) return;
            if (arg[0] == '"')
            {
                int q = arg.IndexOf('"', 1);
                if (q > 1) { name = arg.Substring(1, q - 1).Trim(); rest = arg.Substring(q + 1).Trim(); return; }
            }
            var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try { foreach (var p in MPServer.ConnectedPids()) if (!string.IsNullOrEmpty(p)) known[p] = p; } catch { }
            try { foreach (var p in MPServer.StableIdByPlayer.Keys) if (!string.IsNullOrEmpty(p) && !known.ContainsKey(p)) known[p] = p; } catch { }
            var words = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int n = words.Length; n >= 1; n--)
            {
                string cand = string.Join(" ", words, 0, n);
                if (known.TryGetValue(cand, out var actual)) { name = actual; rest = string.Join(" ", words, n, words.Length - n); return; }
            }
            name = words[0];
            rest = string.Join(" ", words, 1, words.Length - 1);
        }

        /// <summary>"admin players|bans|chat|status [id]": one console line "DATA &lt;id&gt; &lt;json&gt;" - the admin page matches it
        /// by the id it sent. Names and texts only: the ban list never carries an address or a Steam id (BanRows).</summary>
        private static void AdminData(string arg)
        {
            var parts = (arg ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string what = parts.Length > 0 ? parts[0].ToLowerInvariant() : "", id = parts.Length > 1 ? parts[1] : "-";
            foreach (char c in id) if (!char.IsLetterOrDigit(c) && c != '-') { id = "-"; break; }
            if (id.Length > 16) id = id.Substring(0, 16);
            object? data = null;
            switch (what)
            {
                case "players":
                    data = new { players = PlayerRows().ConvertAll(r => new { id = r.pid, name = r.name, ping = r.ping, secs = r.secs }) };
                    break;
                case "bans":
                    data = new { bans = MPServer.BanList().ConvertAll(r => new { key = r.Key, name = r.Name, day = r.BanDay, utc = r.BannedUtc, how = r.How, reason = r.Reason }) };
                    break;
                case "chat":
                    data = new { chat = ChatLines(ChatKeep).ConvertAll(c => new { t = c.at.ToString("o"), who = c.who, text = c.text }) };
                    break;
                case "status":
                    data = new
                    {
                        state = _state, world = MPServer.ChosenLoadSession, day = GameDay(), players = _players.Count,
                        uptime = _clock.ElapsedMilliseconds / 1000, version = MyPluginInfo.PLUGIN_VERSION, build = MyPluginInfo.BuildId,
                        restartIn = _restartAt == DateTime.MinValue ? -1 : Math.Max(0, (int)(_restartAt - DateTime.UtcNow).TotalSeconds),
                        countdown = _restartAt == DateTime.MinValue ? "" : _countdownCode == ExitStop ? "stop" : "restart",
                    };
                    break;
            }
            if (data == null) { Reply("ERR usage: admin players|bans|chat|status [id]"); return; }
            Emit("DATA", id + " " + Newtonsoft.Json.JsonConvert.SerializeObject(data), false);
        }

        /// <summary>MULTILANG-1: the server's own game language (AMP setting ServerLanguage, e.g. "de") - the language of
        /// texts the server produces through Loc.T. Players keep their own game language. "" = the game's setting.</summary>
        private static void ApplyServerLanguage()
        {
            string lang = MPConfig.GetOption("ServerLanguage", "").Trim().ToLowerInvariant();
            if (lang.Length == 0) return;
            try
            {
                if (Array.IndexOf(Localizor.LocalizorManager.GetAvailableLanguages(), lang) < 0)
                { Emit("WARN", $"ServerLanguage '{lang}' is not a game language - keeping '{Localizor.LocalizorManager.LoadedLocale}'."); return; }
                Localizor.LocalizorManager.SetUsedLanguage(lang);
                Emit("INFO", $"server language: {lang}");
            }
            catch (Exception ex) { Emit("WARN", $"ServerLanguage '{lang}': {ex.Message}"); }
        }

        private static void PollCommandFile()
        {
            try
            {
                if (_cmdPath.Length == 0 || !File.Exists(_cmdPath)) return;
                string text = File.ReadAllText(_cmdPath);
                File.Delete(_cmdPath);
                foreach (var l in text.Split('\n'))
                    if (l.Trim().Length > 0) _cmds.Enqueue(l.Trim());
            }
            catch { }
        }

        private static void StartConsoleReader()
        {
            try
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        var input = Console.In;
                        if (input == null) return;
                        string? line;
                        while ((line = input.ReadLine()) != null)
                            if (line.Trim().Length > 0) _cmds.Enqueue(line.Trim());
                    }
                    catch { }   // no console attached: the command file still works
                }) { IsBackground = true, Name = "BAMP-HeadlessStdin" };
                t.Start();
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Headless] stdin reader: {ex.Message}"); }
        }

        private static void OnCancelKey(object? sender, ConsoleCancelEventArgs e)
        {
            // first Ctrl+C / SIGINT: save and quit cleanly; a second one is not cancelled (hard stop)
            if (_stopping) return;
            e.Cancel = true;
            _cmds.Enqueue("stop");
        }

        // ── quitting ───────────────────────────────────────────────────────────────────────────────────────────

        private static void RequestQuit(string why, int code)
        {
            if (_stopping) return;
            _stopping = true;
            _exitCode = code;
            _state = "stopping";   // status.json says so before the quit, not only at a next tick that may never come
            Emit("STOPPING", $"{why} (exit code {code}) - saving the world first");
            WriteStatus();
            StartKillTimer(QuitGraceSeconds, "quit did not finish");
            try { Application.Quit(code); } catch (Exception ex) { Plugin.Logger.LogWarning($"[Headless] quit: {ex.Message}"); }
        }

        /// <summary>End an unattended process that cannot recover by itself. Main thread.</summary>
        public static void FatalExit(string why, int code)
        {
            if (_stopping) return;
            _stopping = true;
            _exitCode = code;
            _state = "stopping";
            Emit("FATAL", why);
            WriteStatus();
            StartKillTimer(QuitGraceSeconds, "quit after a fatal error did not finish");
            try { Application.Quit(code); } catch { }
        }

        /// <summary>SHUTDOWN-HANG-1: Application.quitting - after every OnApplicationQuit (the quit checkpoint save, the farewell
        /// mirror, the players' close) and after the mod loader's unload of the mods (MPServer.Stop). Under Proton the game's
        /// own teardown that follows never finished: "Input System module state changed to: Shutdown." was its last line until
        /// AMP killed the process after its stop timeout (600 s), and the runtime had stopped the managed threads by then, the
        /// kill timer too. A server has nothing left to do here, so it ends its process now, with the requested exit code.
        /// Main thread.</summary>
        private static void OnQuitting()
        {
            if (!_active) return;
            if (!_stopping) { _stopping = true; _exitCode = ExitStop; }   // a quit that did not come from a command
            try { if (MPServer.IsRunning) MPServer.Stop(); } catch { }    // normally the mod loader's unload did it already
            _state = "stopped";
            WriteStatus();
            Emit("EXIT", $"saved and closed - ending the process (exit code {_exitCode})");
            try { Thread.Sleep(250); } catch { }   // the transport's last close packets
            HardExit(_exitCode);
        }

        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern bool TerminateProcess(IntPtr process, uint exitCode);

        /// <summary>Ends the process at once (TerminateProcess skips the teardown that hangs) with the exit code.</summary>
        private static void HardExit(int code)
        {
            try { Console.Out.Flush(); } catch { }
            try { TerminateProcess(GetCurrentProcess(), unchecked((uint)code)); } catch { }
            try { Process.GetCurrentProcess().Kill(); } catch { }   // only reached where kernel32 is missing
        }

        private static void StartKillTimer(int seconds, string why)
        {
            try
            {
                new Thread(() =>
                {
                    try
                    {
                        Thread.Sleep(seconds * 1000);
                        Emit("FATAL", $"{why} within {seconds} s - killing the process.", log: false);
                        Process.GetCurrentProcess().Kill();
                    }
                    catch { }
                }) { IsBackground = true, Name = "BAMP-HeadlessKill" }.Start();
            }
            catch { }
        }

        private static void StartStallWatchdog()
        {
            if (_stallSeconds <= 0) return;
            try
            {
                new Thread(() =>
                {
                    try
                    {
                        while (true)
                        {
                            Thread.Sleep(10_000);
                            if (_stopping) return;
                            long idle = _clock.ElapsedMilliseconds - Interlocked.Read(ref _lastTickMs);
                            if (idle > _stallSeconds * 1000L)
                            {
                                Emit("FATAL", $"the game's main loop has not ticked for {idle / 1000} s - killing the process (exit {ExitStalled}).", log: false);
                                try { Environment.ExitCode = ExitStalled; } catch { }
                                Process.GetCurrentProcess().Kill();
                                return;
                            }
                        }
                    }
                    catch { }
                }) { IsBackground = true, Name = "BAMP-HeadlessWatchdog" }.Start();
            }
            catch { }
        }

        // ── output ─────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>One console line "[BAMP-HEADLESS] KIND detail" (stdout + events.log), plus the mod log when log is true.
        /// Any thread; pass log:false off the main thread.</summary>
        public static void Emit(string kind, string detail = "", bool log = true)
        {
            string line = detail.Length == 0 ? $"{Tag} {kind}" : $"{Tag} {kind} {detail}";
            lock (_outLock)
            {
                try { Console.Out.WriteLine(line); Console.Out.Flush(); } catch { }
                try { if (_eventsPath.Length > 0) File.AppendAllText(_eventsPath, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "Z " + line + "\n"); } catch { }
            }
            if (log) { try { Plugin.Logger.LogInfo($"[Headless] {kind} {detail}"); } catch { } }
        }

        /// <summary>AMP-INFO-2: one plain console line for AMP - not into events.log, it carries the server key.</summary>
        internal static void ConsoleLine(string line)
        {
            lock (_outLock)
            {
                try { Console.Out.WriteLine(line); Console.Out.Flush(); } catch { }
            }
        }

        private static void WriteStatus()
        {
            if (_statusPath.Length == 0) return;
            try
            {
                var s = new Dictionary<string, object?>
                {
                    ["state"] = _state,
                    ["session"] = MPServer.ChosenLoadSession ?? "",
                    ["configuredSession"] = MPConfig.DedicatedSession,
                    ["gameDay"] = GameDay(),
                    ["port"] = MPConfig.HostPort,
                    ["players"] = new List<string>(_players),
                    ["playerCount"] = _players.Count,
                    ["idlePause"] = MPServer.PausedByIdle,
                    ["disconnectPause"] = MPServer.PausedByDisconnect,
                    ["uptimeSeconds"] = _clock.ElapsedMilliseconds / 1000,
                    ["modVersion"] = MyPluginInfo.PLUGIN_VERSION,
                    ["modBuild"] = MyPluginInfo.BuildId,   // BUILD-ID-1
                    // INFO-30S-2: an announced stop or restart - bamp-start.sh then stops repeating AMP's info line
                    ["countdown"] = _restartAt == DateTime.MinValue ? "" : _countdownCode == ExitStop ? "stop" : "restart",
                    ["batchMode"] = _batchArg,
                    ["nullGraphics"] = _nullGfx,
                    ["targetFps"] = _appliedFps,
                    ["pid"] = _pid,
                    ["updatedUtc"] = DateTime.UtcNow.ToString("o"),
                };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(s, Newtonsoft.Json.Formatting.Indented);
                string tmp = _statusPath + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(_statusPath)) File.Replace(tmp, _statusPath, null);
                else File.Move(tmp, _statusPath);
            }
            catch { }
        }

        private static int GameDay()
        {
            try { return SaveGameManager.Current?.Day ?? 0; } catch { return 0; }
        }

        // ── config helpers ─────────────────────────────────────────────────────────────────────────────────────

        private static string ArgValue(string name)
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < args.Length; i++)
                    if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1].Trim();
            }
            catch { }
            return "";
        }

        private static int ReadInt(string key, int def, int min, int max)
        {
            string s = MPConfig.GetOption(key, "").Trim();
            return int.TryParse(s, out int v) ? Math.Max(min, Math.Min(max, v)) : def;
        }

        private static bool ReadBool(string key, bool def)
        {
            string s = MPConfig.GetOption(key, "").Trim();
            if (s.Length == 0) return def;
            return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1";
        }
    }
}
