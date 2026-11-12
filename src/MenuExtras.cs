using System.Reflection;
using TMPro;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>The main menu's additions that need no Harmony patch - they also show in banner mode (patches dead):
    /// the mod's version next to the game's (MENU-VERSION-1) and the mod's cards in the game's News panel (MOD-NEWS-1).
    /// The load status console is BootConsole (BOOT-TERMINAL-2), opened by the load itself.</summary>
    public partial class MPCanvasUI
    {
        private float _menuExtrasNextAt;

        private static readonly FieldInfo? _fVersioning =
            typeof(MainMenuController).GetField("versioning", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        /// <summary>Per frame from Update, ahead of the banner-mode return; twice a second on the main menu (each visit -
        /// the menu scene builds its label and its News panel anew).</summary>
        private void TickMenuExtras()
        {
            if (HeadlessHost.Active || _canvasGO == null) return;
            float now = Time.unscaledTime;
            if (now < _menuExtrasNextAt) return;
            _menuExtrasNextAt = now + 0.5f;
            if (IsInGame()) return;
            MainMenuController? mmc = null;
            try { mmc = UnityEngine.Object.FindObjectOfType<MainMenuController>(); } catch { }
            if (mmc == null) return;
            ShowModVersion(mmc);
            ModNews.StartFetch();
            ModNews.Inject(mmc);
        }

        /// <summary>MENU-VERSION-1 (user: "always one version up, so the current one can be seen"): the mod's version
        /// after the game's own in the main menu's version label. The game writes that label in its Start; a label
        /// without our mark (new menu visit, or written after us) gets it again.</summary>
        private static void ShowModVersion(MainMenuController mmc)
        {
            try
            {
                if (_fVersioning?.GetValue(mmc) is not TextMeshProUGUI label || label == null) return;
                string mark = MyPluginInfo.SHORT_NAME + " " + MyPluginInfo.PLUGIN_VERSION;
                string t = label.text ?? "";
                if (t.Length == 0 || t.Contains(mark)) return;   // empty = the game has not written its own yet
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.text = t + "  |  " + mark;
            }
            catch { }
        }
    }
}
