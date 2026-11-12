using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BigAmbitionsMP
{
    /// <summary>SIGN-FX-1 (user 2026-11-08, the game's "Sign appearance" window and photos of the city's own shop signs:
    /// "Aussehen des Außenschilds mit eigenen Bildern und mehr Optionen wie Animation", "und Hinzufügen des ausgewählten
    /// Logos"; chosen: chasing bulbs, pulsing/flickering neon, colour cycle, blinking). The window gets two more parts:
    /// "Picture on the sign" - Own picture (our picture window, LOGO-PICKER-1) and Business logo (the logo from BizMan) on the
    /// sign's face (the WideSign image, as the shop signs window F7 does it) - and "Animation" - pulse, colour cycle, blink.
    /// The animation drives the sign's two light materials (neon and bulbs, BuildingSignController._lightMaterials) and its
    /// bulb light. The choice is kept per business in the save (an entry in GameInstance.CollapsedCitymapFilterCategories,
    /// as Going Online keeps its state there) and goes to the other players with the business (BusinessInfo.SignFx).
    /// Chasing bulbs need the bulbs one by one; the game's sign meshes cannot be read at run time, so that comes later.</summary>
    internal static class SignFx
    {
        internal const int Chase = 1, Pulse = 2, Cycle = 4, Blink = 8;
        // SIGN-FX-2: where the logo stands beside the name on the wide sign (bits 16 left, 32 right, both = 48; 0 = the game's)
        internal const int LogoLeft = 16, LogoRight = 32, LogoMask = LogoLeft | LogoRight;
        const string Prefix = "bamp:signfx:";
        static readonly Dictionary<string, int> _own = new Dictionary<string, int>(StringComparer.Ordinal);
        static readonly Dictionary<string, int> _remote = new Dictionary<string, int>(StringComparer.Ordinal);
        static object? _loadedFor;
        internal static readonly int EmissiveId = Shader.PropertyToID("_EmissiveColor");

        static void LoadOwn()
        {
            var gi = SaveGameManager.Current;
            if (ReferenceEquals(gi, _loadedFor)) return;
            _loadedFor = gi;
            _own.Clear();
            var l = gi?.CollapsedCitymapFilterCategories;
            if (l == null) return;
            foreach (var e in l)
            {
                if (e == null || !e.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                int eq = e.LastIndexOf('=');
                if (eq > Prefix.Length && int.TryParse(e.Substring(eq + 1), out int f)) _own[e.Substring(Prefix.Length, eq - Prefix.Length)] = f;
            }
        }

        /// <summary>The animation of a business's sign (own or another player's); 0 = none.</summary>
        internal static int Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            try { LoadOwn(); } catch { }
            if (_own.TryGetValue(key, out var f)) return f;
            return _remote.TryGetValue(key, out var r) ? r : 0;
        }

        /// <summary>The player's own choice for their business: kept in the save, sent with the business.</summary>
        internal static void SetOwn(string key, int flags)
        {
            var gi = SaveGameManager.Current;
            if (gi == null || string.IsNullOrEmpty(key)) return;
            LoadOwn();
            gi.CollapsedCitymapFilterCategories ??= new List<string>();
            var l = gi.CollapsedCitymapFilterCategories;
            l.RemoveAll(e => e != null && e.StartsWith(Prefix + key + "=", StringComparison.Ordinal));
            if (flags != 0) { l.Add(Prefix + key + "=" + flags); _own[key] = flags; }
            else _own.Remove(key);
            _remote.Remove(key);
            Plugin.Logger.LogInfo($"[SignFx] '{key}': animation {Describe(flags)} (SIGN-FX-1).");
        }

        /// <summary>Another player's choice, from the business info.</summary>
        internal static void SetRemote(string key, int flags)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (flags != 0) _remote[key] = flags; else _remote.Remove(key);
        }

        internal static string Describe(int f)
        {
            if (f == 0) return "off";
            var parts = new List<string>();
            if ((f & Pulse) != 0) parts.Add("pulse");
            if ((f & Cycle) != 0) parts.Add("colour cycle");
            if ((f & Blink) != 0) parts.Add("blink");
            if ((f & Chase) != 0) parts.Add("chase");
            return string.Join(" + ", parts);
        }

        // ── the sign in the world ──

        /// <summary>After the game (re)builds a sign: the driver takes the sign's light materials as they are now.</summary>
        [HarmonyPatch(typeof(BuildingSignController), nameof(BuildingSignController.ConfigureSign))]
        public static class Patch_BuildingSign_Configure_Fx
        {
            static void Postfix(BuildingSignController __instance, BuildingRegistration registration)
            {
                try
                {
                    if (__instance == null || registration == null) return;
                    var d = __instance.GetComponent<SignFxDriver>() ?? __instance.gameObject.AddComponent<SignFxDriver>();
                    d.Ctrl = __instance;
                    d.Key = GameStateReader.AddressKey(registration);
                    d.Capture();
                }
                catch (Exception ex) { Plugin.Logger.LogWarning($"[SignFx] sign: {ex.Message}"); }
            }
        }

        // ── the window ──

        /// <summary>The window opens for a building: our part below the colours, set to this business.</summary>
        [HarmonyPatch(typeof(SignAppearance), nameof(SignAppearance.SetBuilding))]
        public static class Patch_SignAppearance_SetBuilding_Extras
        {
            static void Postfix(SignAppearance __instance, BuildingRegistration registration)
            {
                try { SignFxUi.Ensure(__instance, registration); }
                catch (Exception ex) { Plugin.Logger.LogWarning($"[SignFx] window: {ex.Message}"); }
            }
        }
    }

    /// <summary>SIGN-FX-1: animates one sign's light materials (neon, bulbs) and its bulb light.</summary>
    internal sealed class SignFxDriver : MonoBehaviour
    {
        internal BuildingSignController? Ctrl;
        internal string Key = "";
        Material[]? _mats;
        Color[]? _base;
        byte[]? _kind;           // 0 other, 1 neon, 2 bulbs
        float _lightBase = -1f;
        int _applied;
        float _flickerUntil, _nextFlicker;

        internal void Capture()
        {
            _mats = null; _base = null; _kind = null; _lightBase = -1f; _applied = 0;
            var c = Ctrl;
            if (c == null) return;
            var mats = c._lightMaterials;
            if (mats == null || mats.Length == 0) return;
            _mats = mats;
            _base = new Color[mats.Length];
            _kind = new byte[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                _kind[i] = m.name == "SignNeonMaterial (Instance)" ? (byte)1 : m.name == "SignBulbMaterial (Instance)" ? (byte)2 : (byte)0;
                if (_kind[i] != 0 && m.HasProperty(SignFx.EmissiveId)) _base[i] = m.GetColor(SignFx.EmissiveId);
            }
            try { if (c.pointLight != null) _lightBase = c.pointLight.intensity; } catch { }
        }

        void Restore()
        {
            if (_mats == null || _base == null || _kind == null) return;
            for (int i = 0; i < _mats.Length; i++)
                if (_kind[i] != 0 && _mats[i] != null) _mats[i].SetColor(SignFx.EmissiveId, _base[i]);
            try { if (Ctrl != null && Ctrl.pointLight != null && _lightBase >= 0f) Ctrl.pointLight.intensity = _lightBase; } catch { }
        }

        void Update()
        {
            int f = SignFx.Get(Key) & ~SignFx.Chase;   // chasing bulbs: not yet
            if (f == 0)
            {
                if (_applied != 0) { Restore(); _applied = 0; }
                return;
            }
            _applied = f;
            if (_mats == null || _base == null || _kind == null) return;
            var cam = Camera.main;
            if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > 180f * 180f) return;

            float t = Time.unscaledTime;
            bool off = (f & SignFx.Blink) != 0 && (t % 1.2f) > 0.75f;
            float neonK = 1f;
            if ((f & SignFx.Pulse) != 0)
            {
                neonK = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(t * 2.4f));
                if (t > _nextFlicker) { _flickerUntil = t + UnityEngine.Random.Range(0.04f, 0.13f); _nextFlicker = t + UnityEngine.Random.Range(1.5f, 5.5f); }
                if (t < _flickerUntil) neonK *= 0.1f;
            }
            for (int i = 0; i < _mats.Length; i++)
            {
                var m = _mats[i];
                if (m == null || _kind[i] == 0) continue;
                var b = _base[i];
                float a = b.a;
                Color c = b;
                if ((f & SignFx.Cycle) != 0)
                {
                    float h = (t * 0.07f + (_kind[i] == 2 ? 0.5f : 0f)) % 1f;
                    c = Color.HSVToRGB(h, 0.85f, 1f) * Mathf.Max(0.05f, b.maxColorComponent);
                }
                float k = off ? 0f : _kind[i] == 1 ? neonK : 1f;
                var v = c * k;
                v.a = a;
                m.SetColor(SignFx.EmissiveId, v);
            }
            try { if (Ctrl != null && Ctrl.pointLight != null && _lightBase >= 0f) Ctrl.pointLight.intensity = off ? 0f : _lightBase; } catch { }
        }
    }

    /// <summary>SIGN-FX-1: our part of the game's sign window - the picture buttons and the animation switches.</summary>
    internal static class SignFxUi
    {
        static readonly Color Btn = new Color(0.20f, 0.22f, 0.25f, 0.95f), BtnOn = new Color(0.23f, 0.55f, 0.94f, 1f);
        static BuildingRegistration? _reg;
        static readonly List<(Image bg, int flag)> _toggles = new List<(Image, int)>();
        static readonly List<(Image bg, int bits)> _besides = new List<(Image, int)>();   // SIGN-FX-2

        internal static void Ensure(SignAppearance win, BuildingRegistration reg)
        {
            _reg = reg;
            var anchor = win.bulbColorList != null ? win.bulbColorList.transform as RectTransform : null;
            var content = anchor != null ? anchor.parent as RectTransform : null;
            if (content == null) return;
            var have = content.Find("BAMP_SignExtras");
            if (have == null) Build(win, content);
            Paint();
        }

        static void Build(SignAppearance win, RectTransform content)
        {
            _toggles.Clear(); _besides.Clear();
            var box = new GameObject("BAMP_SignExtras", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            box.transform.SetParent(content, false);
            box.transform.SetAsLastSibling();
            var v = box.GetComponent<VerticalLayoutGroup>();
            v.spacing = 6f; v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            v.padding = new RectOffset(0, 0, 8, 6);
            box.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var labelTpl = FindLabel(win, content);
            Label(box.transform, labelTpl, Loc.T("gps_signfx_picture", "Picture on the sign"));
            var r1 = Row(box.transform);
            Button(r1, labelTpl, Loc.T("gps_signfx_own", "Own picture"), () => OnOwnPicture());
            Button(r1, labelTpl, Loc.T("gps_signfx_logo", "Business logo"), () => OnLogo());
            // SIGN-FX-2 (user 2026-11-08: "Auswahl, wo das Firmenlogo ist neben dem Firmennamen - links, rechts oder beide Seiten")
            Label(box.transform, labelTpl, Loc.T("gps_signfx_beside", "Logo beside the name"));
            var rb = Row(box.transform);
            _besides.Add((Button(rb, labelTpl, Loc.T("gps_signfx_left", "Left"), () => Beside(SignFx.LogoLeft)), SignFx.LogoLeft));
            _besides.Add((Button(rb, labelTpl, Loc.T("gps_signfx_right", "Right"), () => Beside(SignFx.LogoRight)), SignFx.LogoRight));
            _besides.Add((Button(rb, labelTpl, Loc.T("gps_signfx_both", "Both sides"), () => Beside(SignFx.LogoMask)), SignFx.LogoMask));
            Label(box.transform, labelTpl, Loc.T("gps_signfx_anim", "Animation"));
            var r2 = Row(box.transform);
            _toggles.Add((Button(r2, labelTpl, Loc.T("gps_signfx_pulse", "Pulse"), () => Toggle(SignFx.Pulse)), SignFx.Pulse));
            _toggles.Add((Button(r2, labelTpl, Loc.T("gps_signfx_cycle", "Colours"), () => Toggle(SignFx.Cycle)), SignFx.Cycle));
            _toggles.Add((Button(r2, labelTpl, Loc.T("gps_signfx_blink", "Blink"), () => Toggle(SignFx.Blink)), SignFx.Blink));

            // a window without a layout group: put our part under the last row and make the window taller
            if (content.GetComponent<LayoutGroup>() == null)
            {
                float bottom = 0f;
                foreach (RectTransform ch in content)
                    if (ch != box.transform && ch.gameObject.activeSelf) bottom = Mathf.Min(bottom, ch.anchoredPosition.y - ch.rect.height * (1f - ch.pivot.y));
                var rt = (RectTransform)box.transform;
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, bottom - 4f);
                rt.sizeDelta = new Vector2(-16f, 0f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                if (win.panel != null) win.panel.sizeDelta += new Vector2(0f, rt.rect.height + 8f);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            if (win.panel != null) LayoutRebuilder.ForceRebuildLayoutImmediate(win.panel);
            Plugin.Logger.LogInfo("[SignFx] the sign window got the picture and animation buttons (SIGN-FX-1).");
        }

        /// <summary>The window's own row label ("Neon colour") as the model for ours: a text among the rows of the colour lists,
        /// not in a list, a button or the dropdown - never the title on its light bar.</summary>
        static TMP_Text? FindLabel(SignAppearance win, RectTransform content)
        {
            foreach (Transform ch in content)
            {
                if (ch == null || ch.name == "BAMP_SignExtras") continue;
                if (ch.GetComponentInChildren<ColorListUI>(true) != null || ch.GetComponentInChildren<UnityEngine.UI.Button>(true) != null) continue;
                var t = ch.GetComponentInChildren<TMP_Text>(true);
                if (t != null && !string.IsNullOrEmpty(t.text)) return t;
            }
            foreach (var t in win.GetComponentsInChildren<TMP_Text>(true))
                if (t != null && !string.IsNullOrEmpty(t.text) && t.GetComponentInParent<ColorListUI>() == null && t.GetComponentInParent<UnityEngine.UI.Button>() == null && t.transform.IsChildOf(content)) return t;
            return null;
        }

        static void Label(Transform parent, TMP_Text? tpl, string text)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 22f;
            var t = go.AddComponent<TextMeshProUGUI>();
            if (tpl != null) { t.font = tpl.font; t.fontSize = tpl.fontSize; t.color = tpl.color; t.fontStyle = tpl.fontStyle; t.characterSpacing = tpl.characterSpacing; }
            else { t.fontSize = 16f; t.color = Color.white; t.fontStyle = FontStyles.Bold | FontStyles.UpperCase; }
            t.text = text.ToUpperInvariant();
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.raycastTarget = false;
        }

        static Transform Row(Transform parent)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 6f; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            go.GetComponent<LayoutElement>().preferredHeight = 34f;
            return go.transform;
        }

        static Image Button(Transform parent, TMP_Text? tpl, string text, Action click)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().flexibleWidth = 1f;
            var bg = go.GetComponent<Image>();
            bg.color = Btn;
            var b = go.GetComponent<UnityEngine.UI.Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => { try { click(); } catch (Exception ex) { Plugin.Logger.LogWarning($"[SignFx] button: {ex.Message}"); } });
            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(4f, 0f); trt.offsetMax = new Vector2(-4f, 0f);
            var t = tgo.AddComponent<TextMeshProUGUI>();
            if (tpl != null) t.font = tpl.font;
            t.fontSize = tpl != null ? Mathf.Max(11f, tpl.fontSize * 0.85f) : 13f;
            t.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            t.color = Color.white;
            t.text = text;
            t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = true; t.fontSizeMin = 8f; t.fontSizeMax = t.fontSize;
            t.raycastTarget = false;
            return bg;
        }

        static void Paint()
        {
            int f = _reg != null ? SignFx.Get(GameStateReader.AddressKey(_reg)) : 0;
            foreach (var (bg, flag) in _toggles) if (bg != null) bg.color = (f & flag) != 0 ? BtnOn : Btn;
            foreach (var (bg, bits) in _besides) if (bg != null) bg.color = (f & SignFx.LogoMask) == bits ? BtnOn : Btn;
        }

        static void Toggle(int flag)
        {
            if (_reg == null) return;
            string key = GameStateReader.AddressKey(_reg);
            SignFx.SetOwn(key, SignFx.Get(key) ^ flag);
            Paint();
        }

        static void OnOwnPicture()
        {
            if (_reg == null) return;
            var ui = MPCanvasUI.Instance;
            if (ui == null || !ui.SignPickerOpen(_reg)) Plugin.Logger.LogWarning("[SignFx] the picture window could not open.");
        }

        static void OnLogo()
        {
            if (_reg == null) return;
            var ui = MPCanvasUI.Instance;
            int f = SignFx.Get(GameStateReader.AddressKey(_reg));
            // SIGN-FX-3: "Business logo" means BizMan's logo again - an own picture chosen beside the name steps back
            try
            {
                string? own = MPCanvasUI.SignLogoFile(_reg.BusinessName ?? "");
                if (own != null && System.IO.File.Exists(own)) System.IO.File.Move(own, own + "." + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak");
            }
            catch { }
            if (ui != null) ui.SignPutGameLogo(_reg, (f & SignFx.LogoMask) >> 4);   // SIGN-FX-2: with the logo where it was chosen
        }

        /// <summary>SIGN-FX-2: the logo beside the name - the same choice again takes it away (the game's sign as it makes it).</summary>
        static void Beside(int bits)
        {
            if (_reg == null) return;
            string key = GameStateReader.AddressKey(_reg);
            int f = SignFx.Get(key);
            int now = (f & SignFx.LogoMask) == bits ? 0 : bits;
            SignFx.SetOwn(key, (f & ~SignFx.LogoMask) | now);
            var ui = MPCanvasUI.Instance;
            if (ui != null) ui.SignPutGameLogo(_reg, now >> 4);
            Paint();
        }
    }
}
