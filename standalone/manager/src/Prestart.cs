using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace GpsManager;

/// <summary>Before every start of the game - what bamp-start.sh does under AMP: the server files, bamp-server.json with
/// every key, the server key, the list of extra mods for the players, and on Linux the Proton home.</summary>
internal static class Prestart
{
    internal static async Task Run(Job? job, CancellationToken ct)
    {
        ServerFiles.SyncFromPackage();
        foreach (var d in new[] { Paths.Headless, Paths.Logs, Paths.Mods, Paths.Web })
            Directory.CreateDirectory(d);
        if (!File.Exists(Paths.ModDll) || !File.Exists(Path.Combine(Paths.Game, "winhttp.dll")))
            throw new InvalidOperationException(Lang.T(
                "Die Server-Dateien fehlen (Mod oder Vorlader). Unter „Installation“ die Server-Dateien aktualisieren oder das Programmpaket neu entpacken.",
                "The server files are missing (mod or preloader). Update the server files under \"Installation\" or unpack the program package again."));
        ServerJson.EnsureDefaults();
        Manifest.Ensure();
        string key = ServerKey.Ensure();
        string kdir = Path.Combine(Paths.Web, ServerKey.Folder(key));
        Directory.CreateDirectory(kdir);
        string ranking = Path.Combine(kdir, "ranking.json");
        if (!File.Exists(ranking))
            Files.WriteAtomic(ranking, $"{{\"updatedUnix\": {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}, \"players\": []}}\n");
        ExtraMods.Publish(kdir);
        if (!OperatingSystem.IsWindows())
        {
            if (!File.Exists(Paths.ProtonScript))
                throw new InvalidOperationException(Lang.T("Proton fehlt - unter „Installation“ herunterladen.", "Proton is missing - download it under \"Installation\"."));
            ProtonGE.EnsureHome();
            if (!Directory.Exists(Path.Combine(Paths.Prefix, "pfx", "drive_c")))
                await ProtonGE.SetupPrefix(job, ct);
        }
    }
}

/// <summary>SERVER-KEY-1 as under AMP: the key players join with - the setting TelemetryKey, else telemetry-data/key.txt,
/// made once (12 hex characters). The mod reads the same two places (DedicatedHost.ServerJoinKey).</summary>
internal static class ServerKey
{
    internal static string Current()
    {
        string key = Clean((string?)ServerJson.Read()["TelemetryKey"]);
        if (key.Length == 0 && File.Exists(Paths.KeyFile)) key = Clean(File.ReadAllText(Paths.KeyFile));
        return key;
    }

    internal static string Ensure()
    {
        string key = Current();
        if (key.Length > 0) return key;
        key = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        Directory.CreateDirectory(Paths.Telemetry);
        Files.WriteAtomic(Paths.KeyFile, key + "\n", secret: true);
        Log.Info(Lang.T("Neuer Server-Schlüssel erstellt.", "New server key created."));
        return key;
    }

    /// <summary>DEDICATED-KEY-1: the first 16 hex digits of SHA-256(lower-case key) - the folder of key-protected files.</summary>
    internal static string Folder(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim().ToLowerInvariant()))).ToLowerInvariant()[..16];

    private static string Clean(string? s) => new string((s ?? "").Where(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-' || ch == '_').Take(64).ToArray());
}

/// <summary>SERVER-MODS-1 as under AMP: every folder in server/mods but Going Public Server's own is a mod the players
/// need; the LIST (never the files, SEC-MODS-1) goes to telemetry-data/web/&lt;key folder&gt;/mods/mods.json. A Workshop mod's
/// folder is its item id. Blueprints (Layout.json, no DLL) stay with the players and are left out.</summary>
internal static class ExtraMods
{
    internal static List<(string Folder, bool Workshop)> List()
    {
        var list = new List<(string, bool)>();
        if (!Directory.Exists(Paths.Mods)) return list;
        foreach (var dir in Directory.GetDirectories(Paths.Mods).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileName(dir);
            if (name == "GoingPublicServer") continue;
            if (File.Exists(Path.Combine(dir, "Layout.json")) && Directory.GetFiles(dir, "*.dll").Length == 0) continue;
            list.Add((name, name.All(char.IsAsciiDigit)));
        }
        return list;
    }

    internal static void Publish(string keyDir)
    {
        var mods = List();
        string outDir = Path.Combine(keyDir, "mods");
        Directory.CreateDirectory(outDir);
        foreach (var f in Directory.GetFiles(outDir))
            if (Path.GetFileName(f) != "mods.json") File.Delete(f);
        var arr = new JsonArray();
        foreach (var (folder, ws) in mods) arr.Add(new JsonObject { ["folder"] = folder, ["workshop"] = ws });
        Files.WriteAtomic(Path.Combine(outDir, "mods.json"), new JsonObject { ["mods"] = arr }.ToJsonString() + "\n");
        if (mods.Count > 0)
            Log.Info(Lang.T($"{mods.Count} zusätzliche Mod(s) auf dem Server - Spieler brauchen sie auch: ", $"{mods.Count} extra mod(s) on the server - players need them too: ")
                     + string.Join(", ", mods.Select(m => m.Folder)));
    }
}
