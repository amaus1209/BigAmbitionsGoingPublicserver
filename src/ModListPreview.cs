using System;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>MOD-THUMB-1 (user 2026-11-06, a screenshot of the game's mod list: "Bild für Mod fehlt"): the game gives a mod
    /// in ModsLocal no picture at all - ModDiscoveryRegistry.GetLocalMods sets only its folder and title, and
    /// ModThumbnailLoader.LoadThumbnailAsync returns at once for steamItemId 0, so the card stays white. Only a Workshop
    /// copy gets Steam's preview image. For our OWN folder the loader now gets our preview.png (the picture the Workshop
    /// page shows) before it looks; every other mod is left as the game has it.</summary>
    [HarmonyPatch(typeof(BigAmbitions.ModsInternal.ModThumbnailLoader), nameof(BigAmbitions.ModsInternal.ModThumbnailLoader.LoadThumbnailAsync))]
    public static class Patch_ModThumbnailLoader_LocalPreview
    {
        private static Sprite? _sprite;
        private static bool _tried;

        static void Prefix(BigAmbitions.ModInfo modInfo)
        {
            try
            {
                if (modInfo == null || modInfo.thumbnail != null || modInfo.steamItemId != 0UL || string.IsNullOrEmpty(modInfo.modFolder)) return;
                string mine = MPConfig.ModRootPath;
                if (string.IsNullOrEmpty(mine) || mine == "." || !Same(modInfo.modFolder, mine)) return;
                if (!_tried)
                {
                    _tried = true;
                    _sprite = Load(Path.Combine(mine, "preview.png"));
                    Plugin.Logger.LogInfo(_sprite != null ? "[ModList] local copy: our preview.png is its picture (MOD-THUMB-1)." : "[ModList] local copy: no preview.png to show.");
                }
                if (_sprite != null) modInfo.thumbnail = _sprite;
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[ModList] picture: {ex.Message}"); }
        }

        private static bool Same(string a, string b)
        {
            try
            {
                string x = Path.GetFullPath(a).TrimEnd('\\', '/'), y = Path.GetFullPath(b).TrimEnd('\\', '/');
                return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static Sprite? Load(string path)
        {
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path), false)) { UnityEngine.Object.Destroy(tex); return null; }
            tex.name = "GoingPublicServer-preview";
            var sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sp.name = tex.name;
            UnityEngine.Object.DontDestroyOnLoad(tex);
            return sp;
        }
    }
}
