using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GpsManager;

/// <summary>STANDALONE-1 (1.0.16, user 2026-11-01: "a server version for Windows and Linux, Linux with an install script,
/// and a Docker template - all with a login and a web interface"): Going Public Server without AMP.
///
///   GoingPublicServer [run] [--data DIR] [--port N] [--bind ADDR]   the manager: web interface + the game
///   GoingPublicServer setup [--data DIR] [--port N] [--lang de|en]  first setup (install.sh): config + access, shown once
///   GoingPublicServer reset-password [--data DIR] [--user NAME]     a new password, shown once
///   GoingPublicServer version | help
///
/// The access data appear once, when they are made: on Windows in a window of their own, on Linux in the terminal
/// (install.sh runs "setup" in the foreground; Docker: the container's output).</summary>
public static class Program
{
    internal static readonly string Version =
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";

    public static async Task<int> Main(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
        if (OperatingSystem.IsWindows()) Win32Window.PrepareConsole();
        string cmd = "run";
        string? data = Environment.GetEnvironmentVariable("GPS_DATA"), port = null, bind = null, lang = null, user = null;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string? Next() => i + 1 < args.Length ? args[++i] : null;
            switch (a)
            {
                case "--data": data = Next(); break;
                case "--port": port = Next(); break;
                case "--bind": bind = Next(); break;
                case "--lang": lang = Next(); break;
                case "--user": user = Next(); break;
                case "-h": case "--help": cmd = "help"; break;
                case "--version": cmd = "version"; break;
                default:
                    if (!a.StartsWith('-')) cmd = a;
                    else { Console.Error.WriteLine($"unknown option: {a}"); return 2; }
                    break;
            }
        }
        if (cmd == "version") { Console.WriteLine($"Going Public Server {Version}"); return 0; }
        if (cmd == "help") { PrintHelp(); return 0; }

        Paths.Init(data);
        Log.Init(Paths.ManagerLog);
        ManagerConfig.Load();
        var conf = ManagerConfig.Current;
        if (port != null && int.TryParse(port, out int p) && p is > 0 and < 65536) conf.WebPort = p;
        if (bind != null) conf.WebBind = bind;
        if (lang is "de" or "en") conf.Language = lang;
        ManagerConfig.Save();
        Lang.Init(conf.Language);

        switch (cmd)
        {
            case "setup":
            {
                bool made = !Users.Any();
                if (made) Credentials.Show(Users.FirstName(), Users.NewPasswordFor("admin"), created: true)?.Join();
                else Console.WriteLine(Lang.T($"Zugang besteht schon ({Users.FirstName()}). Neues Passwort: reset-password", $"An account exists already ({Users.FirstName()}). New password: reset-password"));
                return 0;
            }
            case "reset-password":
            {
                string name = user ?? Users.FirstName();
                var window = Credentials.Show(name, Users.NewPasswordFor(name), created: false);
                if (window != null)
                {
                    Console.WriteLine(Lang.T("(Fenster schließen zum Beenden)", "(close the window to finish)"));
                    window.Join();   // the window would close with this process
                }
                return 0;
            }
            case "run":
                return await Run();
            default:
                Console.Error.WriteLine($"unknown command: {cmd}");
                PrintHelp();
                return 2;
        }
    }

    private static async Task<int> Run()
    {
        FileStream? lockFile;
        try { lockFile = new FileStream(Path.Combine(Paths.Manager, "manager.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException)
        {
            Fail(Lang.T($"Für {Paths.Data} läuft die Verwaltung schon.", $"The manager is running for {Paths.Data} already."));
            return 1;
        }
        Log.Info($"Going Public Server {Version} - {Platform.Describe()} - {Lang.T("Daten", "data")}: {Paths.Data}");
        if (!Users.Any()) Credentials.Show(Users.FirstName(), Users.NewPasswordFor("admin"), created: true);

        WebApplication app;
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = Paths.App, Args = Array.Empty<string>() });
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(new ManagerLogProvider());
            builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(260));
            builder.Services.AddHostedService<Supervisor>();
            builder.WebHost.ConfigureKestrel(k =>
            {
                k.AddServerHeader = false;
                k.Limits.MaxRequestBodySize = 1_000_000;
                string b = ManagerConfig.Bind;
                if (b is "0.0.0.0" or "*" or "") k.ListenAnyIP(ManagerConfig.Port);
                else k.Listen(IPAddress.Parse(b), ManagerConfig.Port);
            });
            app = builder.Build();
            Web.Map(app);
            await app.StartAsync();
        }
        catch (Exception e)
        {
            Fail(Lang.T($"Die Weboberfläche konnte nicht starten (Port {ManagerConfig.Port} belegt?): {e.Message}",
                        $"The web interface could not start (port {ManagerConfig.Port} in use?): {e.Message}"));
            return 1;
        }
        Log.Info(Lang.T("Weboberfläche: ", "Web interface: ") + string.Join("  ", Credentials.Urls()));

        // Windows: closing the console window gives a program 5 seconds - enough to ask the game to stop; it saves and
        // ends on its own (it is not a child that dies with this window).
        using var close = OperatingSystem.IsWindows()
            ? PosixSignalRegistration.Create(PosixSignal.SIGHUP, _ => { if (GameServer.Alive) GameServer.Stop(Lang.T("Fenster geschlossen", "window closed")); })
            : null;
        await app.WaitForShutdownAsync();
        lockFile.Dispose();
        return 0;
    }

    private static void Fail(string message)
    {
        Log.Error(message);
        if (OperatingSystem.IsWindows() && !Console.IsInputRedirected)
        {
            Console.WriteLine(Lang.T("Enter drücken zum Schließen.", "Press Enter to close."));
            try { Console.ReadLine(); } catch { }
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine($"""
            Going Public Server {Version} - Big Ambitions multiplayer server (web interface + game)

              GoingPublicServer [run] [--data DIR] [--port N] [--bind ADDR]
              GoingPublicServer setup [--data DIR] [--port N] [--lang de|en]
              GoingPublicServer reset-password [--data DIR] [--user NAME]
              GoingPublicServer version

            --data   folder for the game, saves and settings (default: the program folder; GPS_DATA)
            --port   web interface port (default 8080; GPS_WEB_PORT)
            """);
    }
}

/// <summary>The beat: once a second the game's status and watch, once a minute the daily backup; at the start a
/// leftover game of this server is stopped and the game starts if wanted; at the end the game is stopped (it saves).</summary>
internal sealed class Supervisor : IHostedService
{
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _loop = Task.Run(Loop);
        return Task.CompletedTask;
    }

    private async Task Loop()
    {
        var ct = _cts.Token;
        try
        {
            try { ServerFiles.SyncFromPackage(); ServerJson.EnsureDefaults(); Manifest.Ensure(); ServerKey.Ensure(); } catch (Exception e) { Log.Warn(e.Message); }
            await GameServer.StopLeftover();
            _ = Releases.Check(ct);
            if (ManagerConfig.Current.AutoStart && GameServer.Installed && (OperatingSystem.IsWindows() || ProtonGE.Installed))
            {
                string why = GameServer.Start(Lang.T("Autostart", "auto start"));
                if (why.Length > 0) Log.Info(Lang.T("Kein Autostart: ", "No auto start: ") + why);
            }
            else if (!GameServer.Installed)
                Log.Info(Lang.T("Das Spiel ist noch nicht installiert: in der Weboberfläche unter „Installation“ mit dem Steam-Konto herunterladen.",
                                "The game is not installed yet: download it under \"Installation\" in the web interface, with the Steam account."));
            int tick = 0;
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct);
                tick++;
                try { GameServer.Tick(); } catch (Exception e) { Log.Warn("tick: " + e.Message); }
                if (tick % 60 == 0) { try { Backups.Tick(); } catch (Exception e) { Log.Warn("backup: " + e.Message); } }
                if (tick % 21600 == 0) _ = Releases.Check(ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        Log.Info(Lang.T("Verwaltung wird beendet ...", "Manager stopping ..."));
        await GameServer.StopAndWait(Lang.T("Verwaltung wird beendet", "manager stopping"), TimeSpan.FromSeconds(240));
        Log.Info(Lang.T("Beendet.", "Stopped."));
    }
}

/// <summary>ASP.NET Core's own messages: warnings and errors into the manager log, nothing else.</summary>
internal sealed class ManagerLogProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new L(categoryName);
    public void Dispose() { }

    private sealed class L : ILogger
    {
        private readonly string _cat;
        public L(string cat) => _cat = cat;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? e, Func<TState, Exception?, string> format)
        {
            if (!IsEnabled(level)) return;
            string msg = $"{_cat}: {format(state, e)}{(e != null ? " - " + e.Message : "")}";
            if (level >= LogLevel.Error) GpsManager.Log.Error(msg);
            else GpsManager.Log.Warn(msg);
        }
    }
}
