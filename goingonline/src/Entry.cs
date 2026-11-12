using System.Threading.Tasks;
using BAModAPI;
using BigAmbitions.Mods;
using UnityEngine;

// The loader finds mod classes only through this attribute (see Going Public Server's Plugin.cs).
[assembly: RegisterModClass(typeof(GoingOnline.Standalone.Entry))]

namespace GoingOnline.Standalone
{
    /// <summary>Going Online as a mod of its own (single player): loaded once at the game's start and kept for the
    /// whole session. The game's own events and objects carry the provider; since 0.8.1 the patches of core/GamePatches.cs
    /// put it into the game's own pages too (BizMan, deliveries, recruitment - src/Patcher.cs, GO-PATCHES-1). With Going
    /// Public Server loaded as well, it stays idle: the server mod carries the same provider as a module.</summary>
    [ModEntryOnInitializationLoad]
    public sealed class Entry : ModBigAmbitionsBase
    {
        internal static string ModId = "GoingOnline";
        internal static string Root = "";
        internal static IModLogger? Logger;
        GameObject? _runner;

        public override Task OnLoadAsync(ModContext context)
        {
            Logger = context.Logger;
            Root = context.ModRootPath ?? "";
            ModId = string.IsNullOrEmpty(context.ModId) ? "GoingOnline" : context.ModId;
            Go.Host = new Host();
            Settings.Register();
            _runner = new GameObject("GoingOnline.Runner");
            UnityEngine.Object.DontDestroyOnLoad(_runner);
            _runner.hideFlags = HideFlags.HideAndDontSave;
            _runner.AddComponent<Runner>();
            Go.Log($"loaded (version {typeof(Entry).Assembly.GetName().Version}).");
            return Task.CompletedTask;
        }

        public override Task OnUnloadAsync()
        {
            try { if (_runner != null) UnityEngine.Object.Destroy(_runner); } catch { }
            _runner = null;
            return Task.CompletedTask;
        }
    }

    /// <summary>Calls the provider every frame.</summary>
    internal sealed class Runner : MonoBehaviour
    {
        void Update()
        {
            if (Host.ServerModPresent()) return;
            Patcher.Tick();     // GO-PATCHES-1: the provider in the game's own pages (once, in a loaded game)
            Core.Tick();
            PhoneTile.Tick();
        }
    }

    /// <summary>The mod's settings in the game's "Mods" options (stored by the game in PlayerPrefs).</summary>
    internal static class Settings
    {
        internal static readonly (string id, string label, int min, int max, int def)[] Sliders =
        {
            ("faults", "go_opt_faults", 0, 300, 100),
            ("growth", "go_opt_growth", 25, 300, 100),
            ("unlock31", "go_opt_unlock31", 0, 300, 100),
            ("unlockfiber", "go_opt_unlockfiber", 0, 300, 100),
            ("rival", "go_opt_rival", 0, 300, 100),
        };

        public static void Register()
        {
            try
            {
                var o = new ModOptions().AddHeader("go_opt_header");
                foreach (var (id, label, min, max, def) in Sliders) o.AddSlider(id, label, min, max, def, null, "go_opt_percent");
                OptionsService.Register(Entry.ModId, o);
            }
            catch (System.Exception ex) { Go.Warn("options: " + ex.Message); }
        }

        public static int Get(string id, int fallback)
        {
            try
            {
                foreach (var s in Sliders)
                    if (s.id == id) return UnityEngine.PlayerPrefs.GetInt("m:" + Entry.ModId + ":" + id, s.def);
            }
            catch { }
            return fallback;
        }
    }
}
