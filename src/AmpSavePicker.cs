using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>
    /// AMP-PICK-1 (Going Public Server 1.0.9): the world and the save of a dedicated server are chosen from dropdowns on
    /// AMP's settings page instead of typed. AMP's "tool discovery" lists stayed empty in the field (AMP 2.8), so the
    /// choices are written as plain enum values into the instance's settings manifest (&lt;instance&gt;/configmanifest.json,
    /// next to the game's root folder), rebuilt at start, at READY and after every save:
    ///   DedicatedLoadSave: "-" = (newest save), then every save, newest first, labelled "name · day D · time"
    ///   DedicatedSession:  "-" = (world played last), then every world
    /// AMP reads the manifest when the instance starts - a load choice applies at the next start anyway.
    /// HOST side, main thread (DedicatedHost.Tick); only on a headless server.
    /// </summary>
    internal static class AmpSavePicker
    {
        private const string Sep = " · ";
        private const int MaxSaves = 40;
        private static volatile bool _dirty = true;
        private static Dictionary<string, string>? _texts;   // the mod's Locales/<server language>.json
        private static float _nextAt;
        private static bool _failedLogged;

        /// <summary>Any thread: a save was written - list it.</summary>
        public static void MarkDirty() => _dirty = true;

        /// <summary>A setting's value -> the world or save name: "-" = "" (newest / played last), a name passes unchanged,
        /// and a 1.0.9 tool-discovery path gives the name from its folder.</summary>
        public static string FromPicker(string? value)
        {
            string v = (value ?? "").Trim();
            if (v == "-") return "";
            if (v.IndexOf('/') < 0 && v.IndexOf('\\') < 0) return v;
            var segs = v.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (segs.Length < 2) return v;
            var parts = segs[segs.Length - 2].Split(new[] { Sep }, StringSplitOptions.None);
            if (parts.Length < 2) return segs[segs.Length - 2];
            return parts[0] == "00" ? "" : parts[1];
        }

        /// <summary>Main thread, once a second from DedicatedHost.Tick.</summary>
        public static void Tick()
        {
            if (!_dirty || !HeadlessHost.Active || HeadlessHost.DirPath.Length == 0) return;
            if (Time.unscaledTime < _nextAt) return;
            _dirty = false;
            _nextAt = Time.unscaledTime + 5f;
            try
            {
                string root = Path.GetDirectoryName(HeadlessHost.DirPath.TrimEnd('/', '\\')) ?? "";
                if (root.Length == 0) return;
                var runs = MPSaveManager.ListPlaythroughs();   // newest playthrough first
                var saves = new List<MPSaveManager.MpVariant>();
                var worlds = new JObject { ["-"] = Text("gps_saves_lastworld", "(world played last)") };
                foreach (var run in runs)
                {
                    if (worlds[run.Base] == null)
                        worlds[run.Base] = $"{run.Base}{Sep}{Text("gps_saves_day", "day {0}", run.NewestDay)}{Sep}{Stamp(run.NewestUnix)}";
                    foreach (var v in run.Variants)
                        if (v.Kind == "Main" || v.Kind == "Checkpoint" || v.Kind == "Autosave" || v.Kind == "Disconnect" || v.Kind == "Recover")
                            saves.Add(v);
                }
                saves.Sort((a, b) => b.SavedAtUnix.CompareTo(a.SavedAtUnix));
                var saveList = new JObject { ["-"] = Text("gps_saves_newest", "(newest save)") };
                for (int i = 0; i < saves.Count && saveList.Count <= MaxSaves; i++)
                    if (saveList[saves[i].SessionName] == null)
                        saveList[saves[i].SessionName] = $"{saves[i].SessionName}{Sep}{Text("gps_saves_day", "day {0}", saves[i].Day)}{Sep}{Stamp(saves[i].SavedAtUnix)}";
                bool changed = WriteManifest(Path.Combine(Path.GetDirectoryName(root) ?? "", "configmanifest.json"),
                                             new Dictionary<string, JObject> { ["DedicatedLoadSave"] = saveList, ["DedicatedSession"] = worlds });
                foreach (var old in new[] { "bamp-saves", "bamp-worlds" })   // the 1.0.9 tool-discovery folders
                    try { if (Directory.Exists(Path.Combine(root, old))) Directory.Delete(Path.Combine(root, old), true); } catch { }
                Plugin.Logger.LogInfo($"[AmpPick] AMP dropdowns: {saveList.Count - 1} save(s), {worlds.Count - 1} world(s){(changed ? " - written, AMP shows them after the next instance restart" : " - unchanged")}.");
            }
            catch (Exception ex)
            {
                if (!_failedLogged) { _failedLogged = true; Plugin.Logger.LogWarning($"[AmpPick] {ex.Message}"); }
            }
        }

        /// <summary>A label in the server language. The game registers the mod's texts only where its mod loader runs
        /// normally - not on a headless server - so the mod's own Locales/&lt;ServerLanguage&gt;.json is read directly.</summary>
        private static string Text(string key, string english, params object[] args)
        {
            if (_texts == null)
            {
                _texts = new Dictionary<string, string>();
                try
                {
                    string lang = MPConfig.GetOption("ServerLanguage", "en").Trim().ToLowerInvariant();
                    string dir = Path.GetDirectoryName(typeof(AmpSavePicker).Assembly.Location) ?? "";
                    string f = Path.Combine(dir, "Locales", lang + ".json");
                    if (File.Exists(f))
                        _texts = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(f)) ?? _texts;
                }
                catch { }
            }
            string s = _texts.TryGetValue(key, out var t) && !string.IsNullOrEmpty(t) ? t : english;
            try { return args.Length == 0 ? s : string.Format(s, args); } catch { return string.Format(english, args); }
        }

        private static string Stamp(long unix)
            => unix > 0 ? DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("yyyy-MM-dd HH.mm") : "?";

        /// <summary>Sets the EnumValues of the named fields in AMP's settings manifest (and drops a tool-discovery entry,
        /// whose empty result would replace them). Writes only on a change; true = written.</summary>
        private static bool WriteManifest(string path, Dictionary<string, JObject> enums)
        {
            if (!File.Exists(path)) return false;
            string before = File.ReadAllText(path);
            var specs = JArray.Parse(before.TrimStart('\uFEFF'));
            foreach (var spec in specs.OfType<JObject>())
            {
                string field = (string?)spec["FieldName"] ?? "";
                if (!enums.TryGetValue(field, out var values)) continue;
                spec["EnumValues"] = values;
                spec["InputType"] = "enum";
                spec.Remove("ToolDiscovery");
            }
            string after = specs.ToString(Formatting.Indented);
            if (after == before.TrimStart('\uFEFF')) return false;
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, after, new UTF8Encoding(false));
            File.Copy(tmp, path, true);
            File.Delete(tmp);
            return true;
        }
    }
}
