namespace GpsManager;

/// <summary>STANDALONE-1: where everything lives. The data folder is the "instance": the manager's own files, SteamCMD,
/// backups and server/ - laid out like AMP's bigambitions/ folder (1331550 = the game, headless, telemetry-data), so the
/// mod, the ranking page and the server zip of a GitHub release work unchanged. configmanifest.json sits next to server/
/// as AMP's does: the mod writes the worlds and saves of this server into it (AMP-PICK-1), the settings page reads them.
///   Windows: the data folder is the program folder (portable), unless --data says otherwise.
///   Linux:   install.sh: program /opt/goingpublicserver, data /var/lib/goingpublicserver.   Docker: /data.</summary>
internal static class Paths
{
    internal static string App { get; private set; } = "";
    internal static string Data { get; private set; } = "";

    internal static void Init(string? data)
    {
        App = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        Data = Path.GetFullPath(string.IsNullOrWhiteSpace(data) ? App : data);
        Directory.CreateDirectory(Manager);
    }

    internal static string Payload => Path.Combine(App, "payload");
    internal static string PayloadVersion => Path.Combine(Payload, "VERSION");

    internal static string Manager => Path.Combine(Data, "manager");
    internal static string ConfigFile => Path.Combine(Manager, "config.json");
    internal static string UsersFile => Path.Combine(Manager, "users.json");
    internal static string ManagerLog => Path.Combine(Manager, "manager.log");
    internal static string ServerFilesStamp => Path.Combine(Manager, "server-files.txt");
    internal static string ManifestStamp => Path.Combine(Manager, "manifest-source.txt");
    internal static string BackupStamp => Path.Combine(Manager, "backup-last.txt");
    internal static string Manifest => Path.Combine(Data, "configmanifest.json");
    internal static string SteamCmd => Path.Combine(Data, "steamcmd");
    internal static string Backups => Path.Combine(Data, "backups");

    internal static string Root => Path.Combine(Data, "server");
    internal static string Game => Path.Combine(Root, "1331550");
    internal static string GameExe => Path.Combine(Game, "Big Ambitions.exe");
    internal static string ServerJson => Path.Combine(Game, "bamp-server.json");
    internal static string PreloaderLog => Path.Combine(Game, "bamp-preloader", "bamp-preloader.log");
    internal static string Headless => Path.Combine(Root, "headless");
    internal static string StatusJson => Path.Combine(Headless, "status.json");
    internal static string EventsLog => Path.Combine(Headless, "events.log");
    internal static string CommandFile => Path.Combine(Headless, "command.txt");
    internal static string PlayersJson => Path.Combine(Headless, "players.json");
    internal static string Telemetry => Path.Combine(Root, "telemetry-data");
    internal static string Web => Path.Combine(Telemetry, "web");
    internal static string KeyFile => Path.Combine(Telemetry, "key.txt");
    internal static string Mods => Path.Combine(Root, "mods");
    internal static string ModDir => Path.Combine(Mods, "GoingPublicServer");
    internal static string ModDll => Path.Combine(ModDir, "BigAmbitionsMP.dll");
    internal static string Tools => Path.Combine(Root, "bamp-tools");
    internal static string ToolsManifest => Path.Combine(Tools, "bigambitionsconfig.json");
    internal static string Logs => Path.Combine(Root, "logs");
    internal static string ConsoleLog => Path.Combine(Logs, "console.log");

    // Linux: Proton GE, its prefix (STEAM_COMPAT_DATA_PATH) and the HOME that Proton and SteamCMD see
    internal static string Proton => Path.Combine(Root, "proton");
    internal static string ProtonScript => Path.Combine(Proton, "proton");
    internal static string Prefix => Path.Combine(Root, "prefix");
    internal static string Home => Path.Combine(Root, "home");

    /// <summary>The game's Application.persistentDataPath: saves and the mod's data.</summary>
    internal static string GameData => OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Hovgaard Games", "Big Ambitions")
        : Path.Combine(Prefix, "pfx", "drive_c", "users", "steamuser", "AppData", "LocalLow", "Hovgaard Games", "Big Ambitions");

    /// <summary>The multiplayer saves of every world this server hosts (MPSaveManager).</summary>
    internal static string MpSaves => Path.Combine(GameData, "SaveGames", "_BAMP_MP");

    /// <summary>A path as the game sees it: on Windows as it is, on Linux through Wine's Z: drive.</summary>
    internal static string ForGame(string path) => OperatingSystem.IsWindows() ? path : "Z:" + path;

    internal static bool InDocker => File.Exists("/.dockerenv") || Environment.GetEnvironmentVariable("GPS_DOCKER") == "1";
}
