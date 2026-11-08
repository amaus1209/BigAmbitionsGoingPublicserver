using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GpsManager;

/// <summary>The server files - the built mod, the server-only preloader, the ranking page and the description of the
/// settings - laid out like the server zip of a GitHub release (1331550/, bamp-mod/GoingPublicServer/, bamp-tools/,
/// telemetry-data/web/). The program package carries them in payload/; at every start they go into server/ unless the
/// same ones are there already or a newer release came in from GitHub ("Update server files").</summary>
internal static class ServerFiles
{
    /// <summary>"1.0.16 fc3f378d" - version and build of the package's server files ("" without a payload).</summary>
    internal static string PackageVersion
    {
        get { try { return File.Exists(Paths.PayloadVersion) ? File.ReadAllText(Paths.PayloadVersion).Trim() : ""; } catch { return ""; } }
    }

    /// <summary>"package 1.0.16 fc3f378d" or "github v1.0.17" - what server/ holds now.</summary>
    internal static string Installed
    {
        get { try { return File.Exists(Paths.ServerFilesStamp) ? File.ReadAllText(Paths.ServerFilesStamp).Trim() : ""; } catch { return ""; } }
    }

    internal static string InstalledVersion => VersionOf(Installed);

    internal static bool Complete =>
        File.Exists(Paths.ModDll) && File.Exists(Path.Combine(Paths.Game, "winhttp.dll")) && File.Exists(Paths.ToolsManifest)
        && File.Exists(Path.Combine(Paths.Web, "index.html"));

    internal static void SyncFromPackage()
    {
        if (!Directory.Exists(Paths.Payload)) return;
        string pkg = "package " + PackageVersion;
        string now = Installed;
        if (Complete)
        {
            if (now == pkg) return;
            if (now.StartsWith("github ", StringComparison.Ordinal) && CompareVersions(VersionOf(now), VersionOf(pkg)) > 0) return;
        }
        Apply(Paths.Payload);
        Stamp(pkg);
        Log.Info(Lang.T($"Server-Dateien {PackageVersion} aus dem Programmpaket eingespielt.", $"Server files {PackageVersion} from the program package installed."));
    }

    /// <summary>A folder in the layout of the server zip into server/. The game itself is never touched, only the
    /// preloader files next to it; the mod folder is replaced as a whole.</summary>
    internal static void Apply(string src)
    {
        Directory.CreateDirectory(Paths.Root);
        string game = Path.Combine(src, "1331550");
        if (Directory.Exists(game)) Files.CopyTree(game, Paths.Game);
        string mod = Path.Combine(src, "bamp-mod", "GoingPublicServer");
        if (File.Exists(Path.Combine(mod, "BigAmbitionsMP.dll")))
        {
            if (Directory.Exists(Paths.ModDir)) Directory.Delete(Paths.ModDir, true);
            Files.CopyTree(mod, Paths.ModDir);
        }
        string tools = Path.Combine(src, "bamp-tools");
        if (Directory.Exists(tools)) Files.CopyTree(tools, Paths.Tools);
        string web = Path.Combine(src, "telemetry-data", "web");
        if (Directory.Exists(web))
        {
            Directory.CreateDirectory(Paths.Web);
            foreach (var f in Directory.GetFiles(web)) File.Copy(f, Path.Combine(Paths.Web, Path.GetFileName(f)), true);
        }
    }

    internal static void Stamp(string what) => Files.WriteAtomic(Paths.ServerFilesStamp, what + "\n");

    /// <summary>"package 1.0.16 fc3f378d" / "github v1.0.17" / "1.0.16" -> "1.0.16".</summary>
    internal static string VersionOf(string s)
    {
        foreach (var part in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string p = part.TrimStart('v', 'V');
            if (p.Length > 0 && char.IsDigit(p[0])) return p;
        }
        return "";
    }

    internal static int CompareVersions(string a, string b)
    {
        int[] Parse(string v) => v.Split('.', '-', '+').Select(x => int.TryParse(x, out int n) ? n : 0).ToArray();
        var x = Parse(a);
        var y = Parse(b);
        for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            int c = (i < x.Length ? x[i] : 0).CompareTo(i < y.Length ? y[i] : 0);
            if (c != 0) return c;
        }
        return 0;
    }
}

/// <summary>bamp-server.json - the game's server settings (read by the mod, -bamp-config).</summary>
internal static class ServerJson
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    internal static JsonObject Read()
    {
        try
        {
            if (File.Exists(Paths.ServerJson))
                return JsonNode.Parse(File.ReadAllText(Paths.ServerJson).TrimStart('﻿')) as JsonObject ?? new JsonObject();
        }
        catch (Exception e) { Log.Warn($"bamp-server.json: {e.Message}"); }
        return new JsonObject();
    }

    internal static void Write(JsonObject o) => Files.WriteAtomic(Paths.ServerJson, o.ToJsonString(Indented) + "\n");

    /// <summary>A new server gets the server mode keys (the rest comes from the settings description, Seed); every start
    /// adds the keys a newer version brought in, with their defaults (SETTINGS-SEED-1, as seed-settings.py under AMP).</summary>
    internal static void EnsureDefaults()
    {
        Directory.CreateDirectory(Paths.Game);
        var c = Read();
        bool changed = false;
        void Default(string key, JsonNode value)
        {
            if (c.ContainsKey(key)) return;
            c[key] = value;
            changed = true;
        }
        Default("Headless", true);
        Default("DedicatedHost", true);
        Default("ServerLanguage", Lang.German ? "de" : "en");
        Default("PlayerId", "Server");
        Default("HostPort", 7777);
        Default("TelemetryKey", "");
        foreach (var k in new[] { "Headless", "DedicatedHost" })   // the server mode is the point of this program
            if (c[k]?.GetValueKind() != JsonValueKind.True) { c[k] = true; changed = true; }
        var added = Seed(c, Manifest.Load(Paths.ToolsManifest));
        if (added.Count > 0)
            Log.Info(Lang.T($"bamp-server.json ergänzt um: {string.Join(", ", added)}", $"bamp-server.json: added {string.Join(", ", added)}"));
        if (changed || added.Count > 0) Write(c);
    }

    internal static List<string> Seed(JsonObject c, JsonArray manifest)
    {
        var added = new List<string>();
        foreach (var s in manifest.OfType<JsonObject>())
        {
            string key = (string?)s["ParamFieldName"] ?? "";
            if (!key.StartsWith("$.", StringComparison.Ordinal) || c.ContainsKey(key[2..])) continue;
            c[key[2..]] = Typed((string?)s["InputType"] ?? "", s["DefaultValue"]?.ToString() ?? "");
            added.Add(key[2..]);
        }
        return added;
    }

    /// <summary>A setting's text as the JSON type the mod expects (checkbox = bool, number = int or float).</summary>
    internal static JsonNode Typed(string inputType, string value)
    {
        if (inputType == "checkbox") return JsonValue.Create(value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));
        if (inputType == "number")
        {
            string v = value.Trim().Replace(',', '.');
            if (long.TryParse(v, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long l)) return JsonValue.Create(l);
            if (double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d)) return JsonValue.Create(d);
        }
        return JsonValue.Create(value);
    }
}

/// <summary>configmanifest.json next to server/ - the description of every setting (bigambitionsconfig.json of the
/// template), into which the mod writes this server's worlds and saves as choices (AMP-PICK-1). A newer description
/// replaces it; the two lists the mod wrote stay.</summary>
internal static class Manifest
{
    private static readonly string[] Picked = { "DedicatedSession", "DedicatedLoadSave" };

    internal static JsonArray Load(string path)
    {
        try
        {
            if (File.Exists(path)) return JsonNode.Parse(File.ReadAllText(path).TrimStart('﻿')) as JsonArray ?? new JsonArray();
        }
        catch (Exception e) { Log.Warn($"{Path.GetFileName(path)}: {e.Message}"); }
        return new JsonArray();
    }

    internal static void Ensure()
    {
        if (!File.Exists(Paths.ToolsManifest)) return;
        string src = File.ReadAllText(Paths.ToolsManifest);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(src)));
        bool have = File.Exists(Paths.Manifest);
        if (have && File.Exists(Paths.ManifestStamp) && File.ReadAllText(Paths.ManifestStamp).Trim() == hash) return;
        var fresh = JsonNode.Parse(src.TrimStart('﻿')) as JsonArray ?? new JsonArray();
        if (have)
        {
            var old = Load(Paths.Manifest);
            foreach (var name in Picked)
            {
                var was = old.OfType<JsonObject>().FirstOrDefault(s => (string?)s["FieldName"] == name);
                var now = fresh.OfType<JsonObject>().FirstOrDefault(s => (string?)s["FieldName"] == name);
                if (was?["EnumValues"] is JsonObject values && values.Count > 0 && now != null)
                {
                    now["EnumValues"] = values.DeepClone();
                    now["InputType"] = "enum";
                }
            }
        }
        Files.WriteAtomic(Paths.Manifest, fresh.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Files.WriteAtomic(Paths.ManifestStamp, hash + "\n");
    }
}
