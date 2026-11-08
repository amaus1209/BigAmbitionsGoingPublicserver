using System.Text.Json;

namespace GpsManager;

/// <summary>manager/config.json - the manager's own settings (the game's settings are in bamp-server.json).
/// GPS_WEB_PORT / GPS_WEB_BIND override the web address for this run (Docker), without being saved.</summary>
internal sealed class ManagerConfig
{
    public int WebPort { get; set; } = 8080;
    public string WebBind { get; set; } = "0.0.0.0";
    /// <summary>Start the game with the manager (once it is installed).</summary>
    public bool AutoStart { get; set; } = true;
    /// <summary>The Steam account SteamCMD logs in with (the password is never stored; SteamCMD keeps its own login).</summary>
    public string SteamUser { get; set; } = "";
    /// <summary>Linux: the GE-Proton release, e.g. GE-Proton10-25; empty = the newest.</summary>
    public string ProtonVersion { get; set; } = "";
    /// <summary>Daily save backup at this hour (0-23), -1 = off.</summary>
    public int BackupHour { get; set; } = 4;
    public int BackupKeep { get; set; } = 14;
    /// <summary>"de" / "en" for the manager's own texts; empty = from the system.</summary>
    public string Language { get; set; } = "";
    /// <summary>Where updates of the server files come from (GitHub releases).</summary>
    public string ReleaseRepo { get; set; } = "amaus1209/BigAmbitionsGoingPublicServer";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    internal static ManagerConfig Current { get; private set; } = new();

    internal static int Port => int.TryParse(Environment.GetEnvironmentVariable("GPS_WEB_PORT"), out int p) && p > 0 && p < 65536 ? p : Current.WebPort;
    internal static string Bind => Environment.GetEnvironmentVariable("GPS_WEB_BIND") is { Length: > 0 } b ? b : Current.WebBind;

    internal static bool Exists => File.Exists(Paths.ConfigFile);

    internal static void Load()
    {
        try
        {
            if (File.Exists(Paths.ConfigFile))
                Current = JsonSerializer.Deserialize<ManagerConfig>(File.ReadAllText(Paths.ConfigFile), Options) ?? new ManagerConfig();
        }
        catch (Exception e)
        {
            Log.Warn($"manager/config.json: {e.Message} - defaults for now (the file stays as it is).");
            Current = new ManagerConfig();
            return;
        }
        if (Current.WebPort is <= 0 or > 65535) Current.WebPort = 8080;
        if (Current.BackupKeep < 1) Current.BackupKeep = 14;
        Save();   // new keys of a newer version get their default in the file
    }

    internal static void Save()
    {
        try { Files.WriteAtomic(Paths.ConfigFile, JsonSerializer.Serialize(Current, Options)); }
        catch (Exception e) { Log.Warn($"manager/config.json: {e.Message}"); }
    }
}

/// <summary>Small file helpers.</summary>
internal static class Files
{
    internal static void WriteAtomic(string path, string text, bool secret = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, text, new System.Text.UTF8Encoding(false));
        if (secret && !OperatingSystem.IsWindows())
            File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(tmp, path, true);
    }

    /// <summary>Copies a folder over another (files are replaced, extra files in the target stay).</summary>
    internal static int CopyTree(string from, string to)
    {
        int n = 0;
        Directory.CreateDirectory(to);
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)), true);
            n++;
        }
        return n;
    }

    internal static long DirSize(string dir)
    {
        try { return Directory.Exists(dir) ? new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0; }
        catch { return 0; }
    }
}
