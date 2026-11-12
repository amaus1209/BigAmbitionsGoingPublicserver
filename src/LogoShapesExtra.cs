using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>LOGO-SHAPES-1 (user 2026-11-08, BizMan's logo editor of the cable provider: "füge noch passende Logos in schwarz
    /// weiß hinzu für die Firmen"): black-and-white logo shapes for our business types - the cable provider (dish, plug,
    /// router, TV, fibre, home internet), IT support, software development, cloud services, cleaning company and laundromat.
    /// They are drawn like the game's own (a white figure on a clear ground, 512 x 512, tools/make_logos.py) and ship in
    /// assets/logos. They go first into the game's list of built-in shapes (LogoHelper.AvailableIcons, which the editor's grid
    /// shows). The game loads a built-in shape from StreamingAssets/LogoShapes/name.png through LogoHelper.GetLogoShapeTexture;
    /// for ours that reads the file in the mod folder. Our types start with their own shapes: the cable provider in GameTypes,
    /// the business pack in BizPack, and ApplyToTypes for a type that was made before the list.</summary>
    internal static class LogoShapesExtra
    {
        static readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly string[] Groups = { "gps_cable", "gps_itsupport", "gps_softwaredev", "gps_cloud", "gps_cleaning", "gps_laundromat" };
        static bool _done;
        static float _next;

        /// <summary>Main thread, every frame (cheap once done): puts the shapes into the game's list, then every ten seconds
        /// gives a type of ours its own shapes if it lacks them.</summary>
        internal static void Ensure()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + (_done ? 10f : 5f);
            if (_done) { ApplyToTypes(); return; }
            try
            {
                string dir = Path.Combine(MPConfig.ModRootPath, "assets", "logos");
                if (!Directory.Exists(dir)) { _done = true; Plugin.Logger.LogWarning("[Logo] the mod has no folder assets/logos - no logo shapes of our own (LOGO-SHAPES-1)."); return; }
                var list = LogoHelper.AvailableIcons;
                if (list == null) { _done = true; Plugin.Logger.LogWarning("[Logo] the game has no list of logo shapes (LOGO-SHAPES-1)."); return; }
                var names = new List<string>();
                foreach (var f in Directory.GetFiles(dir, "gps_*.png"))
                {
                    string n = Path.GetFileNameWithoutExtension(f);
                    _files[n] = f;
                    names.Add(n);
                }
                names.Sort(Order);
                int at = 0;
                foreach (var n in names) if (!list.Contains(n)) list.Insert(at++, n);
                _done = true;
                ApplyToTypes();
                Plugin.Logger.LogInfo($"[Logo] {names.Count} black-and-white logo shapes of our business types are first in the game's shape list (LOGO-SHAPES-1).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Logo] logo shapes: {ex.Message}"); }
        }

        static int Rank(string n)
        {
            for (int i = 0; i < Groups.Length; i++) if (n.StartsWith(Groups[i], StringComparison.OrdinalIgnoreCase)) return i;
            return Groups.Length;
        }

        static int Order(string a, string b)
        {
            int c = Rank(a).CompareTo(Rank(b));
            return c != 0 ? c : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The shapes for a business type of ours, in order; empty for other types or before Ensure found them.</summary>
        internal static List<string> ForType(string? businessType)
        {
            var r = new List<string>();
            string? g = businessType switch
            {
                GoingOnline.GameTypes.Cable or GoingOnline.GameTypes.Center => "gps_cable",
                BizPack.ItSupport => "gps_itsupport",
                BizPack.SoftDev => "gps_softwaredev",
                BizPack.Cloud => "gps_cloud",
                BizPack.Cleaning => "gps_cleaning",
                BizPack.Laundromat => "gps_laundromat",
                _ => null
            };
            if (g == null) return r;
            foreach (var n in _files.Keys)
                if (n.Length > g.Length && n.StartsWith(g, StringComparison.OrdinalIgnoreCase) && char.IsDigit(n[g.Length])) r.Add(n);
            r.Sort(Order);
            return r;
        }

        /// <summary>A type of ours whose shapes are not its own yet gets them; a new business's logo takes one at random.</summary>
        static void ApplyToTypes()
        {
            if (_files.Count == 0) return;
            foreach (var type in new[] { GoingOnline.GameTypes.Cable, GoingOnline.GameTypes.Center, BizPack.ItSupport, BizPack.SoftDev, BizPack.Cloud, BizPack.Cleaning, BizPack.Laundromat })
            {
                try
                {
                    var t = Helpers.BusinessTypeHelper.GetData(type);
                    if (t == null) continue;
                    var own = ForType(type);
                    if (own.Count == 0) continue;
                    if (t.logoShapes != null && t.logoShapes.Count > 0 && t.logoShapes.TrueForAll(own.Contains)) continue;
                    t.logoShapes = own;
                }
                catch { }
            }
        }

        /// <summary>LOGO-SHAPES-1: the game reads a shape of its list from its own folder; ours come from the mod folder.</summary>
        [HarmonyPatch(typeof(LogoHelper), nameof(LogoHelper.GetLogoShapeTexture))]
        public static class Patch_LogoHelper_GetLogoShapeTexture_Ours
        {
            static void Prefix(ref string path)
            {
                try
                {
                    if (string.IsNullOrEmpty(path) || _files.Count == 0) return;
                    if (_files.TryGetValue(Path.GetFileNameWithoutExtension(path), out var ours) && !File.Exists(path)) path = ours;
                }
                catch { }
            }
        }

        /// <summary>LOGO-PICKER-1 (user 2026-11-08: "oben links ist ein kleines Fenster - erstelle das Fenster so, dass es zum
        /// Spiel passt und größer in der Mitte; wenn man sein eigenes Bild auswählen möchte, muss man das Bild anklicken"): the
        /// editor's "Browse files" opened the game's icons folder in the system's file manager - under Proton a tiny Wine
        /// window in the corner, where nothing could be picked. It opens our picture window instead (MPLogoPicker.cs); the
        /// file manager stays one button away in it.</summary>
        [HarmonyPatch(typeof(UI.Smartphone.Apps.BizMan.LogoShapes), nameof(UI.Smartphone.Apps.BizMan.LogoShapes.BrowseFiles))]
        public static class Patch_LogoShapes_BrowseFiles_Picker
        {
            static bool Prefix(UI.Smartphone.Apps.BizMan.LogoShapes __instance)
            {
                try
                {
                    var ui = MPCanvasUI.Instance;
                    if (ui != null && ui.LogoPickerOpen(__instance)) return false;
                }
                catch (Exception ex) { Plugin.Logger.LogWarning($"[Logo] picture window: {ex.Message}"); }
                return true;   // the game's own way: the folder in the file manager
            }
        }
    }
}
