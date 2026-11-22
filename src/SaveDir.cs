using System;
using System.IO;
using HarmonyLib;

namespace BigAmbitionsMP
{
    /// <summary>SAVE-DIR-1 (user 2026-11-13, first AMP test on Windows: "Ja, umbauen" - the saves of a Windows server lay in
    /// C:\Windows\ServiceProfiles\NetworkService\AppData\LocalLow\Hovgaard Games\Big Ambitions, outside the AMP instance:
    /// missing from AMP's backups and shared by every instance on that PC). Under Linux each AMP instance has its own Proton
    /// prefix, so there the question never came up.
    ///
    /// Started with "-bamp-data-dir &lt;folder&gt;" (the AMP template passes "{{$FullRootDir}}gamedata" on Windows), the
    /// game keeps its saves in &lt;folder&gt;\SaveGames and the mod its own data (settings file with the stable id, bans,
    /// logs) in &lt;folder&gt;\BigAmbitionsMP: SaveGamePathHelper.SaveGameFolderPath (decompile SaveGamePathHelper.cs:9,
    /// Application.persistentDataPath + "SaveGames") answers the folder - every save path of the game and of our MP stores
    /// (_BAMP_MP beside the version folder) builds on it - and the game's ApplySaveGameFolderFix (MainMenuController,
    /// it builds the old path by hand and moves loose saves) is left out. The same mechanism as the dev rig's RIG-SAVEROOT,
    /// applied before MPSaveManager caches the version folder (Plugin.OnLoadAsync).
    ///
    /// Nothing is moved from the old place: a fresh instance would otherwise take over another instance's world. Without
    /// the switch (players, Linux servers, the stand-alone server) nothing changes and nothing is logged.</summary>
    internal static class SaveDir
    {
        internal const string Arg = "-bamp-data-dir";
        private static bool _read, _patched;
        private static string? _root;

        /// <summary>The data folder from the command line (full path, created), or null without the switch.</summary>
        internal static string? Root
        {
            get
            {
                if (_read) return _root;
                _read = true;
                string v = "";
                try
                {
                    var args = Environment.GetCommandLineArgs();
                    for (int i = 0; i + 1 < args.Length; i++)
                        if (string.Equals(args[i], Arg, StringComparison.OrdinalIgnoreCase)) { v = args[i + 1]; break; }
                }
                catch { v = ""; }
                v = v.Trim().Trim('"').Trim();
                if (v.Length == 0) return null;
                try
                {
                    if (!Path.IsPathRooted(v)) { Plugin.Logger.LogError($"[SaveDir] {Arg} '{v}' is not a full path - the saves stay in the game's own folder (SAVE-DIR-1)."); return null; }
                    string dir = Path.GetFullPath(v);
                    Directory.CreateDirectory(Path.Combine(dir, "SaveGames"));
                    _root = dir;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"[SaveDir] {Arg} '{v}' cannot be used ({ex.Message}) - the saves stay in the game's own folder (SAVE-DIR-1).");
                    _root = null;
                }
                return _root;
            }
        }

        /// <summary>The game's save root: the data folder's SaveGames, else the game's own (persistentDataPath\SaveGames).</summary>
        internal static string SaveGames
        {
            get
            {
                var r = Root;
                return r != null ? Path.Combine(r, "SaveGames") : Path.Combine(UnityEngine.Application.persistentDataPath, "SaveGames");
            }
        }

        /// <summary>Plugin.OnLoadAsync, before MPSaveManager.EnsureVersionCached: point the game's save root at the folder.</summary>
        internal static void Apply()
        {
            var r = Root;
            if (r == null || _patched) return;
            try
            {
                var h = new Harmony("com.bamp.bigambitionsmp.savedir");
                h.Patch(AccessTools.PropertyGetter(typeof(SaveGamePathHelper), nameof(SaveGamePathHelper.SaveGameFolderPath)),
                        prefix: new HarmonyMethod(typeof(SaveDir), nameof(SaveGameFolderPathPrefix)));
                var fix = AccessTools.Method(typeof(MainMenuController), nameof(MainMenuController.ApplySaveGameFolderFix));
                if (fix != null) h.Patch(fix, prefix: new HarmonyMethod(typeof(SaveDir), nameof(SkipPrefix)));
                _patched = true;
                string ver = "";
                try { ver = SaveGamePathHelper.CurrentVersionFolderPath() ?? ""; } catch (Exception ex) { ver = "(threw " + ex.GetType().Name + ")"; }
                if (ver.StartsWith(r, StringComparison.OrdinalIgnoreCase))
                    Plugin.Logger.LogInfo($"[SaveDir] saves in {Path.Combine(r, "SaveGames")}, the mod's data in {Path.Combine(r, "BigAmbitionsMP")} (SAVE-DIR-1).");
                else
                    Plugin.Logger.LogError($"[SaveDir] the game's version folder still answers '{ver}' - the redirect to {r} did NOT reach it (SAVE-DIR-1).");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[SaveDir] redirect failed - the saves stay in the game's own folder: {ex.GetType().Name}: {ex.Message} (SAVE-DIR-1)");
            }
        }

        private static bool SkipPrefix() => !_patched;

        private static bool SaveGameFolderPathPrefix(ref string __result)
        {
            var r = _root;
            if (r == null) return true;
            __result = Path.Combine(r, "SaveGames");
            return false;
        }
    }
}
