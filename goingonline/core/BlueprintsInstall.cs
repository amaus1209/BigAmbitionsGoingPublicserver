using System.IO;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GO-BLUEPRINT-2 (user 2026-11-10: "denk an die Blaupausen, dass man die buchen kann"): the mod's templates
    /// (assets/blueprints/&lt;name&gt;/ - Metadata.json, Layout.json, Thumbnail.png, made by tools/blueprints.py) go into the
    /// game's own Blueprints folder (&lt;persistentDataPath&gt;/Blueprints, Blueprints.BlueprintsFolderLoader), so the interior
    /// installation firms offer them with nothing to copy by hand: the offices at Kingdom Office (43 4th Avenue), the customer center at Kristian Bahood (2 1st Avenue;
    /// it does shops) - for a building of that size whose business is ours, or an empty one. Once a session, from the main
    /// menu on; a template already there is rewritten only when the mod's differs, and the game's blueprint list is read
    /// again (BlueprintsFolderLoader.ClearCache). Not on a dedicated server.</summary>
    internal static class BlueprintsInstall
    {
        /// <summary>GO-BLUEPRINT-4: the templates' revision. New or changed templates alone (assets/blueprints) leave both
        /// DLLs - and so the build ids in the package names - as they were; bumping this with each new set changes them, and
        /// the log line shows it (as Models.VanModelRev does for the van).</summary>
        internal const string Rev = "GO-BLUEPRINT-7 (11 templates: centers A1 A2 C1, offices A3 C1 D2 J1; D2 with 18 workplaces; desks linked to their computers and chairs; at the counters the staff on the office chair, the customers in the armchair)";

        static bool _done;

        internal static void Run()
        {
            if (_done || Application.isBatchMode) return;
            _done = true;
            try
            {
                string src = Path.Combine(Go.Host?.ModRoot ?? "", "assets", "blueprints");
                if (!Directory.Exists(src)) return;
                string dst = Path.Combine(Application.persistentDataPath, "Blueprints");
                Directory.CreateDirectory(dst);
                int added = 0, renewed = 0, same = 0;
                foreach (var dir in Directory.GetDirectories(src))
                {
                    string name = Path.GetFileName(dir);
                    string to = Path.Combine(dst, name);
                    bool had = Directory.Exists(to), changed = false;
                    Directory.CreateDirectory(to);
                    foreach (var f in Directory.GetFiles(dir))
                    {
                        string t = Path.Combine(to, Path.GetFileName(f));
                        if (File.Exists(t) && Same(t, f)) continue;
                        File.Copy(f, t, true);
                        changed = true;
                    }
                    if (!had) added++; else if (changed) renewed++; else same++;
                }
                if (added + renewed > 0)
                    try { Blueprints.BlueprintsFolderLoader.ClearCache(); } catch { }
                Go.Log($"blueprints: {added} new, {renewed} renewed, {same} as they were in the game's Blueprints folder - the interior installation firms offer them; set {Rev} (GO-BLUEPRINT-2).");
            }
            catch (System.Exception ex) { Go.Warn("blueprints: " + ex.Message + " (GO-BLUEPRINT-2)."); }
        }

        static bool Same(string a, string b)
        {
            if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
            byte[] x = File.ReadAllBytes(a), y = File.ReadAllBytes(b);
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }
    }
}
