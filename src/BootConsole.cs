using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BigAmbitionsMP
{
    /// <summary>BOOT-TERMINAL-1/2 (user requests 2026-10-31: "the player sees on the main screen, like in the terminal,
    /// that the mod is loading"; "the terminal window comes only when everything has loaded - it should appear first and
    /// then show what loads"; "it closes again once the mod loaded fine"). A console panel at the bottom of the screen,
    /// opened at the very start of ModEntry.OnLoadAsync, above the game's loading screen; every step of the load adds its
    /// line as it happens ([ OK ] / [WARN] / [FAIL], the game hooks counting up live). All fine: it fades out a few
    /// seconds after the load. Something failed: it stays until clicked.
    ///
    /// The game awaits OnLoadAsync from its loading screen (ModLifecycleLoader.LoadModInstanceAsync, LoadingScreen
    /// WaitForTask), so the load can give a frame back between steps (<see cref="Frame"/>: Task.Yield on Unity's
    /// synchronization context, once about every 50 ms of work) - that is what makes the lines appear one by one. Never
    /// on the dedicated server: there the load runs straight through as before. Own canvas, plain uGUI: it does not
    /// depend on MPCanvasUI (which waits for the load) or on Harmony (which may be what failed).</summary>
    internal static class BootConsole
    {
        internal const string TagOk = "<color=#4CD964>[ OK ]</color>";
        internal const string TagWarn = "<color=#FFB340>[WARN]</color>";
        internal const string TagFail = "<color=#FF5A4F>[FAIL]</color>";
        private const string TagBusy = "<color=#8A94A6>[ .. ]</color>";
        private const float Width = 820f;
        private const float FrameBudgetMs = 50f;   // work between two frames while the console is open

        private static GameObject? _go;
        private static TextMeshProUGUI? _text;
        private static RectTransform? _panel;
        private static readonly List<string> _lines = new List<string>();
        private static string? _live;              // the line that is still counting (the game hooks)
        private static readonly Stopwatch _sinceFrame = new Stopwatch();
        internal static bool Warned, Failed;

        /// <summary>True while the panel is up and the load is still running: <see cref="Frame"/> then yields.</summary>
        internal static bool Live { get; private set; }

        internal static void Open()
        {
            if (_go != null || HeadlessHost.Active || Application.isBatchMode) return;
            try
            {
                _go = new GameObject("BAMP_BootConsole");
                Object.DontDestroyOnLoad(_go);
                var canvas = _go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32000;   // above the game's loading screen and the mod's own canvas (999)
                var scaler = _go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Mathf.Max(0.5f, Screen.height / 1080f);
                _go.AddComponent<GraphicRaycaster>();
                _go.AddComponent<BootConsoleView>();

                var panelGO = new GameObject("Panel");
                panelGO.transform.SetParent(_go.transform, false);
                _panel = panelGO.AddComponent<RectTransform>();
                _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0f);
                _panel.anchoredPosition = new Vector2(0f, 26f);
                var bg = panelGO.AddComponent<Image>();
                bg.color = new Color(0.03f, 0.04f, 0.05f, 0.93f);
                bg.raycastTarget = false;   // clicks go through while it loads; a failure makes it clickable (Finish)
                panelGO.AddComponent<CanvasGroup>();

                var textGO = new GameObject("Text");
                textGO.transform.SetParent(panelGO.transform, false);
                var trt = textGO.AddComponent<RectTransform>();
                trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0f, 1f);
                trt.anchoredPosition = new Vector2(18f, -14f);
                trt.sizeDelta = new Vector2(Width - 36f, 40f);
                _text = textGO.AddComponent<TextMeshProUGUI>();
                try { var f = Object.FindObjectOfType<TextMeshProUGUI>()?.font; if (f != null && f != _text.font) _text.font = f; } catch { }
                _text.fontSize = 15;
                _text.color = new Color(0.86f, 0.88f, 0.90f, 1f);
                _text.alignment = TextAlignmentOptions.TopLeft;
                _text.richText = true;
                _text.textWrappingMode = TextWrappingModes.Normal;
                _text.overflowMode = TextOverflowModes.Overflow;
                _text.raycastTarget = false;

                _lines.Add($"<b>{MyPluginInfo.SHORT_NAME} {MyPluginInfo.PLUGIN_VERSION}</b>  <color=#8A94A6>({MyPluginInfo.BuildTag})</color>");
                Live = true;
                Render();
                _sinceFrame.Restart();
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"[Boot] console: {ex.Message}");
                Close();
            }
        }

        /// <summary>One finished step of the load.</summary>
        internal static void Line(string tag, string text)
        {
            if (_go == null) return;
            if (tag == TagWarn) Warned = true;
            if (tag == TagFail) Failed = true;
            _live = null;
            _lines.Add(tag + "  " + text);
            Render();
        }

        /// <summary>The step that is still running (replaced by the next <see cref="Line"/>).</summary>
        internal static void Busy(string text)
        {
            if (_go == null) return;
            _live = TagBusy + "  " + text;
            Render();
        }

        /// <summary>Awaited between steps of the load: hands one frame back to the game once about 50 ms of work have
        /// passed, so the panel is drawn. Without the panel (dedicated server) it completes at once.</summary>
        internal static async Task Frame(bool now = false)
        {
            if (!Live) return;
            if (!now && _sinceFrame.Elapsed.TotalMilliseconds < FrameBudgetMs) return;
            // only with Unity's synchronization context: it brings the rest of the load back to the main thread next
            // frame - without it Task.Yield would go on on a pool thread, so the load runs straight through instead
            if (System.Threading.SynchronizationContext.Current == null) return;
            await Task.Yield();
            _sinceFrame.Restart();
        }

        /// <summary>The load is over: the last line, then the panel closes by itself - or stays until clicked.</summary>
        internal static void Finish(string tag, string text, string? clickHint)
        {
            if (_go == null) return;
            Line(tag, text);
            if (Failed && !string.IsNullOrEmpty(clickHint))
            {
                _lines.Add("<color=#8A94A6>" + clickHint + "</color>");
                Render();
            }
            Live = false;
            var view = _go.GetComponent<BootConsoleView>();
            if (view != null) view.Hold(Failed ? -1f : Warned ? 10f : 4f, _panel);
        }

        // ── the steps' lines, in the player's language (English when the mod's texts are not loaded yet) ──

        internal static void SourceLine(string modRoot)
        {
            string root = (modRoot ?? "").Replace('/', '\\');
            var wm = System.Text.RegularExpressions.Regex.Match(root, @"\\workshop\\content\\1331550\\(\d+)");
            int ml = root.IndexOf("\\ModsLocal\\", System.StringComparison.OrdinalIgnoreCase);
            if (wm.Success) Line(TagOk, Loc.T("gps_boot_from_workshop", "Loaded from the Steam Workshop (item {0})", wm.Groups[1].Value));
            else Line(TagOk, Loc.T("gps_boot_from_local", "Loaded from {0}", ml >= 0 ? root.Substring(ml + 1) : root));
        }

        internal static void HarmonyLine()
        {
            if (ModEntry.BootHarmonyOwnCopy)
                Line(TagOk, Loc.T("gps_boot_harmony_own", "Harmony {0} (own copy)", ModEntry.BootHarmonyVersion));
            else
                Line(TagWarn, Loc.T("gps_boot_harmony_other", "Harmony {0} from another mod: {1}", ModEntry.BootHarmonyVersion, ModEntry.BootHarmonyFolder));
        }

        internal static void HooksBusy(int done, int total)
            => Busy(Loc.T("gps_boot_hooks_live", "Game hooks: {0} of {1} classes ...", done, total));

        internal static void HooksLine()
        {
            if (ModEntry.BootHarmonyDead)
            {
                Line(TagFail, Loc.T("gps_boot_harmony_dead", "The patching library Harmony did not start - multiplayer is off"));
                return;
            }
            int failed = ModEntry.BootFailClasses, dead = ModEntry.BootDeadClasses;
            int total = ModEntry.BootOkClasses + failed + dead;
            if (failed >= ModEntry.PatchFailHardBlock)
                Line(TagFail, Loc.T("gps_boot_hooks_fail", "Game hooks: {0} of {1} classes failed - multiplayer is off", failed, total));
            else if (failed + dead > 0)
                Line(TagWarn, Loc.T("gps_boot_hooks_some", "Game hooks: {0} of {1} classes failed - single features may be missing", failed + dead, total));
            else
                Line(TagOk, Loc.T("gps_boot_hooks_ok", "Game hooks: {0} of {1} classes, {2} methods", ModEntry.BootOkClasses, total, ModEntry.BootPatchedMethods));
            if (ModEntry.BootTinySwitch)
                Line(TagWarn, Loc.T("gps_boot_tiny", "Test switch skip-tiny-patches.txt: {0} small game methods not redirected", ModEntry.BootTinySkipped));
            if (ModEntry.BootWineDirect)   // WINE-JUMPS-1
                Line(TagOk, Loc.T("gps_boot_wine", "Proton/Wine: game hooks written as self-contained jumps ({0} pointer cells avoided)", ModEntry.WineCellsRefused));
        }

        internal static void FinishLine()
        {
            if (ModEntry.MpDisabledByPatchFailure || MPCanvasUI.FatalBootBanner != null)
            {
                Failed = true;
                Finish(TagFail, Loc.T("gps_boot_off", "Multiplayer is off - details in the game log (Player.log)"), Loc.T("gps_boot_click", "Click to close"));
            }
            else
                Finish(TagOk, Loc.T("gps_boot_ready", "Multiplayer ready: {0} -> {1}",
                    Loc.T("gps_ui_multiplayer", "Multiplayer"), Loc.T("gps_ui_join_game", "Join Game")), null);
        }

        internal static void Close()
        {
            Live = false;
            if (_go != null) { try { Object.Destroy(_go); } catch { } }
            _go = null; _text = null; _panel = null;
        }

        private static void Render()
        {
            if (_text == null || _panel == null) return;
            var all = new List<string>(_lines);
            if (_live != null) all.Add(_live);
            string s = string.Join("\n", all);
            _text.text = s;
            float h = Mathf.Ceil(_text.GetPreferredValues(s, Width - 36f, 0f).y);
            _text.rectTransform.sizeDelta = new Vector2(Width - 36f, h + 4f);
            _panel.sizeDelta = new Vector2(Width, h + 30f);
        }
    }

    /// <summary>BOOT-TERMINAL-2: the console's own clock - holds the finished panel, then fades it out; a failed load
    /// keeps it until a click.</summary>
    internal sealed class BootConsoleView : MonoBehaviour
    {
        private float _fadeAt = float.MaxValue;
        private CanvasGroup? _group;
        private const float Fade = 0.8f;

        internal void Hold(float seconds, RectTransform? panel)
        {
            _group = panel != null ? panel.GetComponent<CanvasGroup>() : null;
            if (seconds < 0f)
            {
                if (panel == null) return;
                var img = panel.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;
                var btn = panel.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(BootConsole.Close);
                return;
            }
            _fadeAt = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            if (Time.unscaledTime < _fadeAt) return;
            float a = 1f - (Time.unscaledTime - _fadeAt) / Fade;
            if (a <= 0f) { BootConsole.Close(); return; }
            if (_group != null) _group.alpha = a;
        }
    }
}
