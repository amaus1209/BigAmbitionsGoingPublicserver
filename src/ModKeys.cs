using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Localizor.LanguageChangeEvent;
using UnityEngine;
using UnityEngine.UI;

namespace BigAmbitionsMP
{
    /// <summary>MOD-KEYS-1 (user 2026-10-31: "the mod keys should be assignable and changeable under Options, Controls"):
    /// the six keys of the built-in modules, each with the player's own key (and Shift or not). Kept in PlayerPrefs, like
    /// the game keeps its own rebinds. Every module asks <see cref="Bind.Down"/> instead of a fixed key; texts and window
    /// cards that name a key show the bound one (<see cref="Fix"/>).</summary>
    internal static class ModKeys
    {
        internal sealed class Bind
        {
            public readonly string Id;
            public readonly KeyCode DefKey;
            public readonly bool DefShift;
            public KeyCode Key;
            public bool Shift;
            private readonly Func<string> _label;

            public Bind(string id, KeyCode key, bool shift, Func<string> label)
            {
                Id = id; DefKey = Key = key; DefShift = Shift = shift; _label = label;
            }

            public string Label => _label();

            /// <summary>Pressed this frame, Shift exactly as bound. Never while a key is being assigned.</summary>
            public bool Down()
            {
                EnsureLoaded();
                return Capturing == null && Time.frameCount > _captureEndFrame + 1 && Key != KeyCode.None
                       && Input.GetKeyDown(Key) && ShiftHeld() == Shift;
            }

            public string Display() { EnsureLoaded(); return Key == KeyCode.None ? "-" : (Shift ? "Shift+" : "") + KeyName(Key); }
            public string DefaultDisplay() => (DefShift ? "Shift+" : "") + KeyName(DefKey);
        }

        internal static readonly Bind Camera = new Bind("camera", KeyCode.F6, false,
            () => Loc.T("gps_cam_title", "RPG camera") + " " + Loc.T("gps_key_toggle", "on/off"));
        internal static readonly Bind CameraSettings = new Bind("camera_settings", KeyCode.F8, false,
            () => Loc.T("gps_cam_title", "RPG camera") + " - " + Loc.T("gps_key_settings", "settings"));
        internal static readonly Bind Signs = new Bind("signs", KeyCode.F7, false, () => Loc.T("gps_signs_title", "Shop signs"));
        internal static readonly Bind Planner = new Bind("planner", KeyCode.F9, true, () => Loc.T("gps_plan_title", "Planner"));
        internal static readonly Bind Insights = new Bind("insights", KeyCode.F10, true, () => Loc.T("gps_ins_title", "Insights"));
        internal static readonly Bind Navigation = new Bind("navigation", KeyCode.F11, true, () => Loc.T("gps_nav_title", "Navigation"));
        internal static readonly Bind[] All = { Camera, CameraSettings, Signs, Planner, Insights, Navigation };

        /// <summary>The bind whose key is being assigned right now (Options, Controls), else null.</summary>
        internal static Bind? Capturing;
        private static int _captureEndFrame = -10;
        private static bool _loaded;

        internal static bool ShiftHeld() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private static string PrefKey(Bind b) => "BAMP_Key_" + b.Id;

        /// <summary>Main thread (PlayerPrefs). Called by the first use.</summary>
        internal static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            foreach (var b in All)
            {
                try
                {
                    string v = UnityEngine.PlayerPrefs.GetString(PrefKey(b), "");
                    if (v.Length == 0) continue;
                    var parts = v.Split('|');
                    if (Enum.TryParse(parts[0], out KeyCode k)) b.Key = k;
                    b.Shift = parts.Length > 1 && parts[1] == "1";
                }
                catch { }
            }
        }

        private static void Save(Bind b)
        {
            try
            {
                if (b.Key == b.DefKey && b.Shift == b.DefShift) UnityEngine.PlayerPrefs.DeleteKey(PrefKey(b));
                else UnityEngine.PlayerPrefs.SetString(PrefKey(b), b.Key + "|" + (b.Shift ? "1" : "0"));
                UnityEngine.PlayerPrefs.Save();
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Keys] save '{b.Id}': {ex.Message}"); }
        }

        /// <summary>Assigns a key; another mod key bound the same way gets this one's old key (a swap, no duplicates).</summary>
        internal static void Assign(Bind b, KeyCode key, bool shift)
        {
            EnsureLoaded();
            var other = All.FirstOrDefault(x => x != b && x.Key == key && x.Shift == shift);
            if (other != null) { other.Key = b.Key; other.Shift = b.Shift; Save(other); }
            b.Key = key; b.Shift = shift;
            Save(b);
            Plugin.Logger.LogInfo($"[Keys] '{b.Id}' = {b.Display()}{(other != null ? $" ('{other.Id}' took {other.Display()})" : "")} (MOD-KEYS-1).");
        }

        internal static void ResetAll()
        {
            foreach (var b in All) { b.Key = b.DefKey; b.Shift = b.DefShift; Save(b); }
            Plugin.Logger.LogInfo("[Keys] mod keys back to F6 / F8 / F7 / Shift+F9 / Shift+F10 / Shift+F11.");
        }

        internal static void EndCapture()
        {
            Capturing = null;
            _captureEndFrame = Time.frameCount;
        }

        /// <summary>A text that names the default keys, with the bound ones (the translations keep "F6", "Shift+F9" ...).</summary>
        internal static string Fix(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            EnsureLoaded();
            foreach (var b in All.OrderByDescending(x => x.DefaultDisplay().Length))
            {
                if (b.Key == b.DefKey && b.Shift == b.DefShift) continue;
                text = text.Replace(b.DefaultDisplay(), b.Display());
            }
            return text;
        }

        internal static string KeyName(KeyCode k)
        {
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((int)(k - KeyCode.Alpha0)).ToString();
            if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return "Num " + (int)(k - KeyCode.Keypad0);
            switch (k)
            {
                case KeyCode.Return: return "Enter";
                case KeyCode.Escape: return "Esc";
                case KeyCode.BackQuote: return "^";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.PageUp: return "Page Up";
                case KeyCode.PageDown: return "Page Down";
            }
            return k.ToString();
        }

        /// <summary>Keyboard keys a mod key may take: no mouse, no joystick, no modifier alone.</summary>
        internal static bool Assignable(KeyCode k)
        {
            if (k == KeyCode.None || (int)k >= (int)KeyCode.Mouse0) return false;
            switch (k)
            {
                case KeyCode.LeftShift: case KeyCode.RightShift: case KeyCode.LeftControl: case KeyCode.RightControl:
                case KeyCode.LeftAlt: case KeyCode.RightAlt: case KeyCode.AltGr: case KeyCode.LeftCommand: case KeyCode.RightCommand:
                case KeyCode.LeftWindows: case KeyCode.RightWindows: case KeyCode.CapsLock: case KeyCode.Numlock: case KeyCode.ScrollLock:
                case KeyCode.Escape:
                    return false;
            }
            return true;
        }

        internal static readonly KeyCode[] Keyboard = ((KeyCode[])Enum.GetValues(typeof(KeyCode))).Where(Assignable).Distinct().ToArray();
    }

    /// <summary>MOD-KEYS-1: the mod's rows on the game's own Controls page (Scenes.MainMenu.Options, in the main menu and in
    /// the game's options): copies of the game's own binding row, so they look like the game's. A click on the key button
    /// waits for the next key (Shift counts, Esc cancels); the page's "reset to defaults" resets them too.</summary>
    [HarmonyPatch(typeof(Scenes.MainMenu.Options), "OnEnable")]
    public static class Patch_Options_OnEnable_ModKeys
    {
        static void Postfix(Scenes.MainMenu.Options __instance) => ModKeysUI.Inject(__instance);
    }

    [HarmonyPatch(typeof(Scenes.MainMenu.Options), "ResetControlsSettingsToDefaults")]
    public static class Patch_Options_ResetControls_ModKeys
    {
        static void Postfix()
        {
            ModKeys.ResetAll();
            ModKeysUI.RefreshAll();
        }
    }

    internal static class ModKeysUI
    {
        private const string RowPrefix = "BAMP_KeyRow_";
        private static readonly List<ModKeyRow> _rows = new List<ModKeyRow>();
        private static bool _warned;

        internal static void Inject(Scenes.MainMenu.Options options)
        {
            try
            {
                ModKeys.EnsureLoaded();
                var bindings = options.bindings;
                if (bindings == null) return;
                _rows.RemoveAll(r => r == null);
                if (bindings.GetComponentsInChildren<ModKeyRow>(true).Length > 0) { RefreshAll(); return; }
                var rbType = AccessTools.TypeByName("UnityEngine.InputSystem.Samples.RebindUI.RebindActionUI");
                if (rbType == null) return;
                var game = bindings.GetComponentsInChildren(rbType, true);
                if (game.Length == 0) return;
                var template = game[game.Length - 1];
                var fLabel = AccessTools.Field(rbType, "m_ActionLabel");
                var fText = AccessTools.Field(rbType, "m_BindingText");
                var fOverlay = AccessTools.Field(rbType, "m_RebindOverlay");

                var header = MakeRow(template, rbType, fLabel, fText, fOverlay, RowPrefix + "Header");
                if (header != null)
                {
                    header.SetLabel(MyPluginInfo.DISPLAY_NAME);
                    header.HideButtons();
                }
                foreach (var b in ModKeys.All)
                {
                    var row = MakeRow(template, rbType, fLabel, fText, fOverlay, RowPrefix + b.Id);
                    if (row == null) continue;
                    row.Bind = b;
                    row.Refresh();
                    _rows.Add(row);
                }
                Plugin.Logger.LogInfo($"[Keys] {_rows.Count} mod key row(s) on the Controls page (MOD-KEYS-1).");
            }
            catch (Exception ex)
            {
                if (!_warned) { _warned = true; Plugin.Logger.LogWarning($"[Keys] Controls page rows: {ex.Message}"); }
            }
        }

        private static ModKeyRow? MakeRow(Component template, Type rbType, System.Reflection.FieldInfo? fLabel, System.Reflection.FieldInfo? fText,
                                          System.Reflection.FieldInfo? fOverlay, string name)
        {
            var go = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            go.name = name;
            go.transform.SetAsLastSibling();
            var rb = go.GetComponent(rbType);
            var row = go.AddComponent<ModKeyRow>();
            if (rb != null)
            {
                row.LabelText = fLabel?.GetValue(rb) as TextLocalizationComponent;
                row.KeyText = fText?.GetValue(rb) as TextLocalizationComponent;
                var overlay = fOverlay?.GetValue(rb) as GameObject;
                if (overlay != null && overlay.transform.IsChildOf(go.transform)) overlay.SetActive(false);
                UnityEngine.Object.DestroyImmediate(rb);   // the game's rebind logic must not run on our row
            }
            // every button of the copy: none of the game's listeners; the one around the key text assigns, any other resets
            foreach (var btn in go.GetComponentsInChildren<Button>(true))
            {
                btn.onClick = new Button.ButtonClickedEvent();
                bool keyButton = row.KeyText != null && row.KeyText.transform.IsChildOf(btn.transform);
                if (keyButton) { row.KeyButton = btn; btn.onClick.AddListener(row.StartCapture); }
                else btn.onClick.AddListener(row.ResetToDefault);
                row.Buttons.Add(btn);
            }
            return row;
        }

        internal static void RefreshAll()
        {
            _rows.RemoveAll(r => r == null);
            foreach (var r in _rows) r.Refresh();
        }
    }

    internal sealed class ModKeyRow : MonoBehaviour
    {
        internal ModKeys.Bind? Bind;
        internal TextLocalizationComponent? LabelText, KeyText;
        internal Button? KeyButton;
        internal readonly List<Button> Buttons = new List<Button>();

        internal void SetLabel(string text)
        {
            if (LabelText != null) LabelText.SetValue(text, clearKey: true);
        }

        internal void HideButtons()
        {
            foreach (var b in Buttons) if (b != null) b.gameObject.SetActive(false);
        }

        internal void Refresh()
        {
            if (Bind == null) return;
            SetLabel(Bind.Label);
            SetKey(ModKeys.Capturing == Bind ? Loc.T("gps_key_press", "Press a key ... (Esc: cancel)") : Bind.Display());
        }

        private void SetKey(string text)
        {
            if (KeyText == null) return;
            KeyText.Key = "";
            KeyText.SetValue(text);
        }

        internal void StartCapture()
        {
            if (Bind == null) return;
            ModKeys.Capturing = Bind;
            ModKeysUI.RefreshAll();
        }

        internal void ResetToDefault()
        {
            if (Bind == null) return;
            ModKeys.Assign(Bind, Bind.DefKey, Bind.DefShift);
            ModKeysUI.RefreshAll();
        }

        private void Update()
        {
            if (Bind == null || ModKeys.Capturing != Bind) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { ModKeys.EndCapture(); ModKeysUI.RefreshAll(); return; }
            foreach (var k in ModKeys.Keyboard)
            {
                if (!Input.GetKeyDown(k)) continue;
                ModKeys.Assign(Bind, k, ModKeys.ShiftHeld());
                ModKeys.EndCapture();
                ModKeysUI.RefreshAll();
                return;
            }
        }

        private void OnDisable()
        {
            if (Bind != null && ModKeys.Capturing == Bind) { ModKeys.EndCapture(); Refresh(); }
        }
    }
}
