using System.Diagnostics;
using System.Text.Json.Nodes;

namespace GpsManager;

internal enum GameState { Stopped, Starting, Running, Stopping, Crashed }

/// <summary>The game process: start, stop (the server's own "stop" - it saves, tells the players and ends itself),
/// restart, commands, its console and status.json. Exit codes as under AMP: 0 stop, 10 restart wanted, 2-5 fatal
/// (HEADLESS.md); any other end starts it again after 15, 30, ... 75 s, at most five times in a row. A game that does not
/// end after "stop" (Unity's teardown hangs under Proton) is ended 30 s after "stopped" or 180 s after "stopping"
/// (SHUTDOWN-HANG-1, as bamp-start.sh). Commands: stdin on Linux (as AMP's console), headless/command.txt on Windows.</summary>
internal static class GameServer
{
    private static readonly object _lock = new();
    private static Process? _proc;
    private static StreamWriter? _stdin;
    private static bool _stopWanted, _restartWanted, _stopResent;
    private static DateTime _stopSince, _startedAt, _statusAt;
    private static DateTime? _stoppedSeen;
    private static DateTime _restartAt = DateTime.MaxValue;
    private static string _restartWhy = "";
    private static int _crashes, _gen;   // _gen: a start that was stopped while being prepared never launches
    private static StreamWriter? _log;
    private static TaskCompletionSource _exitHandled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal static GameState State { get; private set; } = GameState.Stopped;
    internal static string Detail { get; private set; } = "";
    internal static int? LastExit { get; private set; }
    internal static JsonObject? Status { get; private set; }
    internal static string ServerState => (string?)Status?["state"] ?? "";
    internal static DateTime StartedAt => _startedAt;

    internal static bool Installed => File.Exists(Paths.GameExe);

    /// <summary>A world of this server exists: _BAMP_MP/&lt;store&gt;/&lt;playthrough&gt; (MPSaveManager).</summary>
    internal static bool HasWorld()
    {
        try { return Directory.Exists(Paths.MpSaves) && Directory.EnumerateDirectories(Paths.MpSaves).Any(s => Directory.EnumerateDirectories(s).Any()); }
        catch { return false; }
    }

    internal static bool Alive
    {
        get { lock (_lock) return _proc != null && !HasExited(_proc); }
    }

    internal static int Pid
    {
        get { lock (_lock) return _proc != null && !HasExited(_proc) ? _proc.Id : 0; }
    }

    /// <summary>"" = it is starting (in the background), else why not.</summary>
    internal static string Start(string why)
    {
        int gen;
        lock (_lock)
        {
            if (!Installed) return Lang.T("Das Spiel ist noch nicht installiert (Installation).", "The game is not installed yet (Installation).");
            // a server without a world and without "new world" ends at once ("no save", exit 3) - again and again
            if (!HasWorld() && string.IsNullOrWhiteSpace((string?)ServerJson.Read()["DedicatedNewGame"]))
                return Lang.T("Noch keine Welt: unter Einstellungen → Neue Welt einen Namen und eine Schwierigkeit setzen, dann starten.",
                              "No world yet: set a name and a difficulty under Settings → New world, then start.");
            if (_proc != null || State is GameState.Starting or GameState.Stopping) return Lang.T("Der Server läuft schon.", "The server is running already.");
            if (Jobs.BlocksGame) return Lang.T("Erst die laufende Aufgabe abwarten.", "Wait for the running task first.");
            State = GameState.Starting;
            Detail = "";
            Status = null;
            _stopWanted = _restartWanted = _stopResent = false;
            _stoppedSeen = null;
            _restartAt = DateTime.MaxValue;
            _startedAt = DateTime.UtcNow;
            gen = ++_gen;
        }
        Log.Info(Lang.T($"Server startet ({why}).", $"Server starting ({why})."));
        _ = Task.Run(async () =>
        {
            try
            {
                await Prestart.Run(null, CancellationToken.None);
                Launch(gen);
            }
            catch (Exception e)
            {
                lock (_lock)
                {
                    if (gen != _gen) return;
                    if (State == GameState.Starting) State = GameState.Stopped;
                    Detail = e.Message;
                }
                Log.Error(Lang.T("Start fehlgeschlagen: ", "Start failed: ") + e.Message);
            }
        });
        return "";
    }

    private static void Launch(int gen)
    {
        var p = new Process { StartInfo = Platform.GameStart(), EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => OnLine(e.Data);
        p.ErrorDataReceived += (_, e) => OnLine(e.Data);
        p.Exited += (_, _) => Task.Run(() => OnExit(p));
        lock (_lock)
        {
            if (gen != _gen || State != GameState.Starting || _stopWanted) return;   // stopped (or started anew) while it was being prepared
            OpenLog();
            p.Start();
            _exitHandled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _proc = p;
            _stdin = p.StandardInput;
            _stdin.NewLine = "\n";
            _stdin.AutoFlush = true;
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
        }
        Log.Info(Lang.T($"Spiel gestartet (PID {p.Id}). Bis die Welt geladen ist, dauert es eine Weile.",
                        $"Game started (PID {p.Id}). Loading the world takes a while."));
    }

    private static void OnLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line) || ConsoleBuffer.IsNoise(line)) return;
        WriteLog(line);
        ConsoleBuffer.Add("game", line);
        if (line.Contains("[BAMP-HEADLESS] READY", StringComparison.Ordinal))
            lock (_lock) { if (State == GameState.Starting) State = GameState.Running; }
        int f = line.IndexOf("[BAMP-HEADLESS] FATAL", StringComparison.Ordinal);
        if (f >= 0) lock (_lock) Detail = line[(f + 21)..].Trim();
    }

    private static void OnExit(Process p)
    {
        int code;
        try { code = p.ExitCode; } catch { code = -1; }
        // Wine's helpers (wineserver, winedevice.exe) hold the game's output pipe open: end them first, then the output
        // readers see the end of it (WaitForExit() without a timeout waits for them) - never longer than 10 s
        Platform.AfterGameExit();
        try { Task.Run(() => p.WaitForExit()).Wait(10_000); } catch { }
        string restartWhy = "";
        lock (_lock)
        {
            if (_proc != p) return;
            _proc = null;
            try { _stdin?.Dispose(); } catch { }
            _stdin = null;
            CloseLog();
            LastExit = code;
            bool ranLong = DateTime.UtcNow - _startedAt > TimeSpan.FromMinutes(10);
            if (_restartWanted) restartWhy = Lang.T("Neustart", "restart");
            else if (_stopWanted) { State = GameState.Stopped; Detail = ""; }
            else if (code == 0) { State = GameState.Stopped; Detail = Lang.T("Der Server hat sich beendet (stop).", "The server ended itself (stop)."); }
            else if (code == 10) restartWhy = Lang.T("der Server wollte neu starten", "the server asked for a restart");
            else
            {
                if (ranLong) _crashes = 0;
                _crashes++;
                State = GameState.Crashed;
                if (_crashes <= 5)
                {
                    int wait = 15 * _crashes;
                    _restartAt = DateTime.UtcNow.AddSeconds(wait);
                    _restartWhy = Lang.T("nach einem Absturz", "after a crash");
                    Detail = Lang.T($"{ExitText(code)} - neuer Start in {wait} s ({_crashes}/5).", $"{ExitText(code)} - starting again in {wait} s ({_crashes}/5).");
                }
                else Detail = Lang.T($"{ExitText(code)} - fünfmal in Folge, kein weiterer Start. Konsole und Logs prüfen.",
                                     $"{ExitText(code)} - five times in a row, no further start. Check the console and the logs.");
            }
            if (restartWhy.Length > 0) { State = GameState.Stopped; Detail = ""; }
        }
        Log.Info(Lang.T($"Spiel beendet (Exit-Code {code}).", $"Game ended (exit code {code})."));
        _exitHandled.TrySetResult();
        if (restartWhy.Length > 0) Start(restartWhy);
    }

    internal static string ExitText(int code) => code switch
    {
        2 => Lang.T("Laden der Welt hing zu lange", "loading the world took too long"),
        3 => Lang.T("kein Spielstand / Auto-Start gescheitert", "no save / auto-start failed"),
        4 => Lang.T("Hauptschleife stand", "the main loop stalled"),
        5 => Lang.T("Mod konnte nicht starten", "the mod could not start"),
        _ => Lang.T($"beendet mit Code {code}", $"ended with code {code}"),
    };

    /// <summary>"" = stopping (or stopped), else why not.</summary>
    internal static string Stop(string why, bool restart = false)
    {
        bool startNow = false;
        lock (_lock)
        {
            if (_proc == null)
            {
                if (State is GameState.Starting or GameState.Crashed)
                {
                    _stopWanted = true;
                    _restartAt = DateTime.MaxValue;
                    State = GameState.Stopped;
                    Detail = "";
                    startNow = restart;
                }
                else if (restart) startNow = true;
                else return Lang.T("Der Server läuft nicht.", "The server is not running.");
            }
            else
            {
                if (restart) _restartWanted = true;
                else { _stopWanted = true; _restartWanted = false; }
                if (State != GameState.Stopping)
                {
                    State = GameState.Stopping;
                    Detail = "";
                    _stopSince = DateTime.UtcNow;
                    _stopResent = false;
                    _stoppedSeen = null;
                }
                else return "";
            }
        }
        if (startNow) return Start(why);
        Log.Info(Lang.T($"Server wird gestoppt ({why}) - er speichert, sagt es den Spielern und beendet sich.",
                        $"Stopping the server ({why}) - it saves, tells the players and ends itself."));
        Command("stop", echo: false);
        return "";
    }

    /// <summary>Ends the game at once (no saving) - for a server that hangs.</summary>
    internal static void Kill(string why)
    {
        Process? p;
        lock (_lock)
        {
            p = _proc;
            if (!_restartWanted) _stopWanted = true;
            _restartAt = DateTime.MaxValue;
            if (p == null && State == GameState.Crashed) { State = GameState.Stopped; Detail = ""; }
        }
        Log.Warn(Lang.T($"Spiel wird hart beendet ({why}).", $"Killing the game ({why})."));
        try { p?.Kill(entireProcessTree: true); } catch { }
        Platform.Kill(Platform.FindGameProcesses(), hard: true);
    }

    /// <summary>A console command for the server (status, players, save, say ..., stop). false = not running.</summary>
    internal static bool Command(string line, bool echo = true)
    {
        line = line.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (line.Length == 0) return false;
        StreamWriter? w;
        lock (_lock) w = _proc != null ? _stdin : null;
        if (w == null) return false;
        if (echo) ConsoleBuffer.Add("input", "> " + line);
        if (!OperatingSystem.IsWindows())
        {
            try { lock (w) w.WriteLine(line); return true; }
            catch { }
        }
        return CommandFile(line);
    }

    private static bool CommandFile(string line)
    {
        try
        {
            Directory.CreateDirectory(Paths.Headless);
            File.AppendAllText(Paths.CommandFile, line + "\n");
            return true;
        }
        catch (Exception e)
        {
            Log.Warn($"command.txt: {e.Message}");
            return false;
        }
    }

    /// <summary>Every second (Program's beat): status.json, the shutdown watch, the start after a crash.</summary>
    internal static void Tick()
    {
        ReadStatus();
        string why = "";
        bool kill = false;
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (_proc != null && State == GameState.Starting && ServerState is "idle" or "running") State = GameState.Running;
            if (_proc != null && State == GameState.Stopping)
            {
                var since = now - _stopSince;
                string st = ServerState;
                if (st == "stopped") _stoppedSeen ??= now;
                if (_stoppedSeen != null && now - _stoppedSeen > TimeSpan.FromSeconds(30)) { kill = true; why = "stopped + 30 s"; }
                else if (st == "stopping" && since > TimeSpan.FromSeconds(180)) { kill = true; why = "stopping + 180 s"; }
                else if (since > TimeSpan.FromSeconds(240)) { kill = true; why = "stop + 240 s"; }
                else if (st != "stopping" && st != "stopped" && since > TimeSpan.FromSeconds(20) && !_stopResent)
                {
                    _stopResent = true;
                    CommandFile("stop");   // the console line did not arrive - the file always does
                }
            }
            if (_proc == null && State == GameState.Crashed && now >= _restartAt)
            {
                _restartAt = DateTime.MaxValue;
                State = GameState.Stopped;
                why = "restart";
            }
        }
        if (kill)
        {
            Log.Warn(Lang.T($"Das Spiel beendet sich nach dem Stopp nicht ({why}) - Prozess wird beendet.", $"The game does not end after the stop ({why}) - ending the process."));
            Process? p;
            lock (_lock) p = _proc;
            try { p?.Kill(entireProcessTree: true); } catch { }
        }
        else if (why == "restart") Start(_restartWhy);
    }

    private static void ReadStatus()
    {
        try
        {
            if (!File.Exists(Paths.StatusJson)) return;
            var t = File.GetLastWriteTimeUtc(Paths.StatusJson);
            if (t == _statusAt) return;
            _statusAt = t;
            if (t < _startedAt.AddSeconds(-2)) return;   // the last run's file
            using var fs = new FileStream(Paths.StatusJson, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs);
            if (JsonNode.Parse(sr.ReadToEnd()) is JsonObject o) Status = o;
        }
        catch { }
    }

    /// <summary>At the manager's start: a game of this server that still runs without it (the manager was killed) is
    /// asked to stop through command.txt and given three minutes, then ended - one server, one game.</summary>
    internal static async Task StopLeftover()
    {
        var pids = Platform.FindGameProcesses();
        if (pids.Count == 0) return;
        Log.Warn(Lang.T($"Ein Spiel dieses Servers läuft noch ohne Verwaltung (PID {string.Join(", ", pids)}) - es wird gestoppt.",
                        $"A game of this server is still running without the manager (PID {string.Join(", ", pids)}) - stopping it."));
        CommandFile("stop");
        for (int i = 0; i < 180 && Platform.FindGameProcesses().Count > 0; i++) await Task.Delay(1000);
        var left = Platform.FindGameProcesses();
        if (left.Count > 0)
        {
            Platform.Kill(left, hard: false);
            await Task.Delay(5000);
            Platform.Kill(Platform.FindGameProcesses(), hard: true);
        }
        Platform.AfterGameExit();
    }

    /// <summary>The manager stops (or an update needs the game off): stop and wait, at most <paramref name="max"/>.</summary>
    internal static async Task StopAndWait(string why, TimeSpan max)
    {
        if (!Alive && State != GameState.Starting) return;
        Stop(why);
        var until = DateTime.UtcNow + max;
        while (Alive && DateTime.UtcNow < until)
        {
            Tick();
            await Task.Delay(1000);
        }
        if (Alive) Kill(why + " - timeout");
        for (int i = 0; i < 10 && Alive; i++) await Task.Delay(500);
        await Task.WhenAny(_exitHandled.Task, Task.Delay(15_000));   // the exit's log line and Wine's clean-up before the manager ends
        Platform.AfterGameExit();
    }

    private static bool HasExited(Process p)
    {
        try { return p.HasExited; } catch { return true; }
    }

    private static void OpenLog()
    {
        try
        {
            Directory.CreateDirectory(Paths.Logs);
            var fi = new FileInfo(Paths.ConsoleLog);
            if (fi.Exists && fi.Length > 10_000_000) File.Move(Paths.ConsoleLog, Paths.ConsoleLog + ".1", true);
            _log = new StreamWriter(new FileStream(Paths.ConsoleLog, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            _log.WriteLine($"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} start =====");
        }
        catch { _log = null; }
    }

    private static void WriteLog(string line)
    {
        var l = _log;
        if (l == null) return;
        try { lock (l) l.WriteLine($"{DateTime.Now:HH:mm:ss} {line}"); } catch { }
    }

    private static void CloseLog()
    {
        try { _log?.Dispose(); } catch { }
        _log = null;
    }
}
