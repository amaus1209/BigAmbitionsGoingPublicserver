using System.Text.Json;
using System.Text.Json.Nodes;

namespace GpsManager;

/// <summary>The settings page: every setting of the description (configmanifest.json - the same fields AMP shows, with
/// this server's worlds and saves filled in by the mod), in German from bamp-tools/i18n/de.json, grouped like AMP's tabs.
/// Saving writes bamp-server.json; the server reads it at its next start.</summary>
internal static class SettingsApi
{
    // AMP-only or fixed in server mode - not on the page
    private static readonly HashSet<string> Skip = new(StringComparer.Ordinal) { "Headless", "DedicatedHost", "SpecificReleaseVersion" };

    internal static JsonObject Describe(string lang)
    {
        var manifest = Manifest.Load(File.Exists(Paths.Manifest) ? Paths.Manifest : Paths.ToolsManifest);
        JsonObject texts = new();
        if (lang == "de")
        {
            try
            {
                string f = Path.Combine(Paths.Tools, "i18n", "de.json");
                if (File.Exists(f)) texts = JsonNode.Parse(File.ReadAllText(f)) as JsonObject ?? new JsonObject();
            }
            catch { }
        }
        var cats = texts["_categories"] as JsonObject ?? new JsonObject();
        var enums = texts["_enums"] as JsonObject ?? new JsonObject();
        var conf = ServerJson.Read();
        var groups = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var s in manifest.OfType<JsonObject>())
        {
            string field = (string?)s["FieldName"] ?? "";
            string param = (string?)s["ParamFieldName"] ?? "";
            if (!param.StartsWith("$.", StringComparison.Ordinal) || Skip.Contains(field) || Skip.Contains(param[2..])) continue;
            bool hidden = s["Hidden"]?.GetValueKind() == JsonValueKind.True;
            if (hidden && field != "$GamePort") continue;   // the game port is AMP's own field; here it is a setting like the others
            string key = param[2..];
            string[] sub = ((string?)s["Subcategory"] ?? "Server:dns:1").Split(':');
            string groupName = sub[0];
            if (!groups.TryGetValue(groupName, out var g))
            {
                g = new JsonObject
                {
                    ["id"] = groupName,
                    ["name"] = (string?)cats[groupName] ?? groupName,
                    ["order"] = sub.Length > 2 && int.TryParse(sub[2], out int o) ? o : 99,
                    ["fields"] = new JsonArray(),
                };
                groups[groupName] = g;
            }
            string label = (string?)s["DisplayName"] ?? key, desc = (string?)s["Description"] ?? "";
            if (field == "$GamePort" && texts["_ports"]?["GamePort"] is JsonArray pt && pt.Count >= 2) { label = (string?)pt[0] ?? label; desc = (string?)pt[1] ?? desc; }
            else if (texts[field] is JsonArray t && t.Count >= 2) { label = (string?)t[0] ?? label; desc = (string?)t[1] ?? desc; }
            string type = (string?)s["InputType"] ?? "text";
            var choices = new JsonArray();
            if (type == "enum" && s["EnumValues"] is JsonObject ev)
                foreach (var (value, shown) in ev)
                {
                    string text = shown?.ToString() ?? value;
                    choices.Add(new JsonArray(value, (string?)enums[text] ?? text));
                }
            conf.TryGetPropertyValue(key, out var current);
            ((JsonArray)g["fields"]!).Add(new JsonObject
            {
                ["key"] = key,
                ["label"] = label,
                ["desc"] = desc,
                ["type"] = type,
                ["value"] = current?.DeepClone() ?? ServerJson.Typed(type, s["DefaultValue"]?.ToString() ?? ""),
                ["default"] = s["DefaultValue"]?.ToString() ?? "",
                ["placeholder"] = (string?)s["Placeholder"] ?? "",
                ["choices"] = choices,
            });
        }
        var list = new JsonArray();
        foreach (var g in groups.Values.OrderBy(g => (int)g["order"]!)) list.Add(g);
        return new JsonObject { ["groups"] = list, ["running"] = GameServer.Alive };
    }

    /// <summary>Writes the given values (only keys of the description) with the type the mod expects; returns how many.</summary>
    internal static int Save(JsonObject values)
    {
        var manifest = Manifest.Load(File.Exists(Paths.Manifest) ? Paths.Manifest : Paths.ToolsManifest);
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var s in manifest.OfType<JsonObject>())
        {
            string param = (string?)s["ParamFieldName"] ?? "";
            if (param.StartsWith("$.", StringComparison.Ordinal) && !Skip.Contains(param[2..])) types[param[2..]] = (string?)s["InputType"] ?? "text";
        }
        var conf = ServerJson.Read();
        int n = 0;
        foreach (var (key, value) in values)
        {
            if (!types.TryGetValue(key, out var type)) continue;
            string text = value?.GetValueKind() switch
            {
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                null => "",
                _ => value is JsonValue jv && jv.TryGetValue(out string? str) ? str ?? "" : value.ToJsonString(),
            };
            if (key == "HostPort" && (!int.TryParse(text, out int port) || port is < 1 or > 65535)) continue;
            conf[key] = ServerJson.Typed(type, text);
            n++;
        }
        if (n > 0) ServerJson.Write(conf);
        return n;
    }
}
