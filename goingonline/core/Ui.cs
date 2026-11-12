using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GoingOnline
{
    /// <summary>The page's building blocks, in the look of the game's full menu (the palette of Going Public Server's
    /// Business and Radio pages). Everything that changes while the page is open registers a refresher in
    /// <see cref="Live"/>; the page calls them twice a second and rebuilds only when its layout has to change.</summary>
    internal static class Ui
    {
        public static readonly Color RowBg = new Color(118f / 255f, 124f / 255f, 132f / 255f, 0.62f);
        public static readonly Color RowOn = new Color(59f / 255f, 140f / 255f, 240f / 255f, 0.95f);
        public static readonly Color Off = new Color(118f / 255f, 124f / 255f, 132f / 255f, 0.25f);
        public static readonly Color Sub = new Color(1f, 1f, 1f, 0.72f);
        public static readonly Color Faint = new Color(1f, 1f, 1f, 0.45f);
        public static readonly Color Good = new Color(0.45f, 0.86f, 0.56f, 1f);
        public static readonly Color Warn = new Color(1f, 0.76f, 0.32f, 1f);
        public static readonly Color Bad = new Color(1f, 0.45f, 0.45f, 1f);
        public static readonly Color Card = new Color(0f, 0f, 0f, 0.22f);

        public static TMP_FontAsset? Font;
        public static readonly List<System.Action> Live = new List<System.Action>();

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        /// <summary>GO-INPUT-1 (user 2026-11-06: "können nicht festgelegt werden die Preise"): the game counts a selected
        /// text field as "typing" only on its UI layer (GameManager.HasInputSelected) - on the default layer every key typed
        /// into a price or the street search went to the game's shortcuts as well, and the field lost its focus.</summary>
        public static void OnUiLayer(GameObject go)
        {
            int ui = LayerMask.NameToLayer("UI");
            if (ui < 0) ui = 5;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ui;
        }

        static LayoutElement Le(GameObject go, float w, float h, float flex = -1f)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (w >= 0f) { le.preferredWidth = w; le.minWidth = w; }
            if (h >= 0f) { le.preferredHeight = h; le.minHeight = h; }
            if (flex >= 0f) le.flexibleWidth = flex;
            return le;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, FontStyles style, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
            t.raycastTarget = false;
            t.richText = true;
            Stretch(t.rectTransform);
            return t;
        }

        /// <summary>A vertical group inside the list (a card when it has a background).</summary>
        public static RectTransform Column(Transform parent, float spacing = 8f, bool card = false, float pad = 0f)
        {
            var go = new GameObject("Column", typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            int p = Mathf.RoundToInt(card ? Mathf.Max(pad, 18f) : pad);
            v.padding = new RectOffset(p, p, p, p);
            if (card) { var img = go.AddComponent<Image>(); img.color = Card; img.raycastTarget = false; }
            return (RectTransform)go.transform;
        }

        public static RectTransform Row(Transform parent, float height = 52f, float spacing = 12f)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = true;
            Le(go, -1f, height);
            return (RectTransform)go.transform;
        }

        /// <summary>A label in a row: a fixed width, or the rest of the row with width &lt; 0.</summary>
        public static TextMeshProUGUI Label(Transform parent, string text, float width, float size = 22f, Color? color = null,
                                             FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color ?? Color.white;
            t.alignment = align; t.raycastTarget = false; t.richText = true;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var le = Le(go, width >= 0f ? width : -1f, -1f, width >= 0f ? 0f : 1f);
            if (width < 0f) le.minWidth = 40f;
            return t;
        }

        public static TextMeshProUGUI LiveLabel(Transform parent, System.Func<string> text, float width, float size = 22f, Color? color = null,
                                                 FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var t = Label(parent, Safe(text), width, size, color, style, align);
            Live.Add(() => { if (t == null) return; string v = Safe(text); if (t.text != v) t.text = v; });
            return t;
        }

        /// <summary>A paragraph over the whole width that wraps (its height follows the text).</summary>
        public static TextMeshProUGUI Para(Transform parent, string text, float size = 21f, Color? color = null, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject("Para", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color ?? Sub;
            t.raycastTarget = false; t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.alignment = TextAlignmentOptions.TopLeft;
            return t;
        }

        public static TextMeshProUGUI LivePara(Transform parent, System.Func<string> text, float size = 21f, Color? color = null)
        {
            var t = Para(parent, Safe(text), size, color);
            Live.Add(() => { if (t == null) return; string v = Safe(text); if (t.text != v) t.text = v; });
            return t;
        }

        public static void Head(Transform parent, string text, string sub = "")
        {
            var t = Para(parent, text, 27f, Color.white, FontStyles.Bold);
            t.margin = new Vector4(0f, 10f, 0f, 0f);
            if (sub.Length > 0) Para(parent, sub, 20f, Sub);
        }

        public static void Gap(Transform parent, float h)
        {
            var go = new GameObject("Gap", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Le(go, -1f, h);
        }

        static string Safe(System.Func<string> f) { try { return f() ?? ""; } catch { return ""; } }

        /// <summary>A button: optional live label, enabled state and highlight.</summary>
        public static Button Btn(Transform parent, string text, float width, UnityAction click, System.Func<bool>? enabled = null,
                                 System.Func<string>? live = null, System.Func<bool>? on = null, float size = 21f)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Le(go, width, -1f);
            var bg = go.GetComponent<Image>();
            bg.color = RowBg;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => { try { click(); } catch (System.Exception ex) { Go.Warn("button: " + ex); } App.Dirty(); });
            var t = Text(go.transform, live != null ? Safe(live) : text, size, FontStyles.Normal, Color.white);
            t.rectTransform.offsetMin = new Vector2(12f, 0f); t.rectTransform.offsetMax = new Vector2(-12f, 0f);
            t.alignment = TextAlignmentOptions.Center;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            System.Action paint = () =>
            {
                if (btn == null) return;
                bool en = true;
                try { en = enabled == null || enabled(); } catch { en = false; }
                bool hi = false;
                try { hi = on != null && on(); } catch { }
                if (btn.interactable != en) btn.interactable = en;
                var c = !en ? Off : hi ? RowOn : RowBg;
                if (bg.color != c) bg.color = c;
                t.color = en ? Color.white : Faint;
                if (live != null) { string v = Safe(live); if (t.text != v) t.text = v; }
            };
            paint();
            Live.Add(paint);
            return btn;
        }

        /// <summary>GO-INPUT-3 (user 2026-11-09: "die Preise für die Tarife ... kann ich nicht eintragen", "weil ich die Zahlen
        /// nur markieren konnte, aber nie anklicken"): after GO-INPUT-1 (UI layer) and GO-INPUT-2 (keeping the selection) the
        /// game's text field component still took no keys inside the game's full menu - his save kept every tariff price and
        /// name at its start value while the +/- buttons worked, and the same component in the multiplayer chat (outside that
        /// menu) types fine. So the page's boxes no longer use the component: a box is an image with a label, a click on it
        /// starts the edit, the keys come from Input.inputString (as the boxes on Going Public Server's own pages in that menu),
        /// and meanwhile the box is the EventSystem's selected object on the UI layer - the game's own test for "typing"
        /// (GameManager.HasInputSelected) - so the game's shortcuts stay off. The first key replaces the value (shown marked),
        /// Backspace deletes, Del empties, Ctrl+V pastes; Enter, Tab or a click elsewhere keeps it, Esc goes back.</summary>
        public static RectTransform Field(Transform parent, string value, float width, System.Action<string> commit, int limit = 24,
                                          string placeholder = "")
        {
            var go = new GameObject("Field", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Le(go, width, -1f, width < 0f ? 1f : -1f);
            var bg = go.GetComponent<Image>();
            bg.color = Typing.Idle;
            bg.raycastTarget = true;
            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            vp.transform.SetParent(go.transform, false);
            var vrt = (RectTransform)vp.transform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(12f, 4f); vrt.offsetMax = new Vector2(-12f, -4f);
            var text = Text(vp.transform, "", 21f, FontStyles.Normal, Color.white);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            var box = new Typing.Box
            {
                Rt = (RectTransform)go.transform, Bg = bg, Label = text, View = vrt, Value = value ?? "", Commit = commit,
                Limit = Mathf.Max(1, limit), Placeholder = placeholder ?? ""
            };
            Typing.Add(box);
            box.Paint();
            OnUiLayer(go);
            return box.Rt;
        }

        /// <summary>GO-INPUT-3: the page's text boxes and the one being edited. Main thread; Tick runs every frame (App.Tick).
        /// How an edit starts and ends is logged a few times per session.</summary>
        internal static class Typing
        {
            internal static readonly Color Idle = new Color(0f, 0f, 0f, 0.38f);
            static readonly Color Editing = new Color(0.10f, 0.22f, 0.40f, 0.85f);

            internal sealed class Box
            {
                public RectTransform Rt = null!, View = null!;
                public Image Bg = null!;
                public TextMeshProUGUI Label = null!;
                public string Value = "", Buffer = "", Placeholder = "";
                public System.Action<string> Commit = null!;
                public int Limit;
                public bool Fresh;                    // the first key replaces the whole value (shown marked)
                string _shown = "\u0001";

                public bool Alive => Rt != null && Label != null && Bg != null;

                public void Paint()
                {
                    if (!Alive) return;
                    bool on = ReferenceEquals(_on, this);
                    string s;
                    if (!on) s = Value.Length > 0 ? "<noparse>" + Value + "</noparse>"
                               : Placeholder.Length > 0 ? "<i><color=#FFFFFF73>" + Placeholder + "</color></i>" : "";
                    else
                    {
                        bool caret = Mathf.FloorToInt(Time.unscaledTime * 2.5f) % 2 == 0;
                        string body = Buffer.Length == 0 ? "" : Fresh ? "<mark=#3B8CF0B0><noparse>" + Buffer + "</noparse></mark>"
                                                                      : "<noparse>" + Buffer + "</noparse>";
                        s = body + (caret ? "|" : "<color=#FFFFFF00>|</color>");
                    }
                    if (s == _shown) return;
                    _shown = s;
                    Label.text = s;
                    // the end of a long text stays in sight while it is typed
                    var want = TextAlignmentOptions.MidlineLeft;
                    if (on && View != null && Label.preferredWidth > View.rect.width) want = TextAlignmentOptions.MidlineRight;
                    if (Label.alignment != want) Label.alignment = want;
                }
            }

            static readonly List<Box> _boxes = new List<Box>();
            static Box? _on;
            static GameObject? _held;                 // the box selected for the game's typing test
            static int _releaseFrame = -1, _logged;

            /// <summary>A box of ours is being edited (the page waits with its rebuild).</summary>
            public static bool Busy => _on != null && _on.Alive && _on.Rt.gameObject.activeInHierarchy;

            public static void Add(Box b)
            {
                _boxes.RemoveAll(x => !x.Alive);
                _boxes.Add(b);
            }

            static int _frame = -1;

            public static void Tick()
            {
                if (Time.frameCount == _frame) return;   // once a frame, whoever calls it
                _frame = Time.frameCount;
                try { Step(); }
                catch (System.Exception ex) { Go.Warn("text box: " + ex.Message + " (GO-INPUT-3)."); End(false, "an error"); }
            }

            static void Step()
            {
                bool left = Input.GetMouseButtonDown(0), right = Input.GetMouseButtonDown(1);
                if (left || right)
                {
                    Box? hit = null;
                    if (left)
                        foreach (var b in _boxes)
                            if (b.Alive && b.Rt.gameObject.activeInHierarchy && Over(b.Rt)) { hit = b; break; }
                    if (_on != null && !ReferenceEquals(hit, _on)) End(true, "a click elsewhere");
                    if (hit != null)
                    {
                        if (ReferenceEquals(hit, _on)) hit.Fresh = false;   // a second click: go on typing at the end
                        else Begin(hit);
                    }
                }
                var on = _on;
                if (on == null) { Release(); return; }
                if (!on.Alive || !on.Rt.gameObject.activeInHierarchy) { End(true, "the page closing"); return; }
                if (!Hold(on)) return;
                if (Input.GetKeyDown(KeyCode.Escape)) { End(false, "Esc"); return; }
                if (Input.GetKeyDown(KeyCode.Tab)) { End(true, "Tab"); return; }
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { End(true, "Enter"); return; }
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                            || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
                if (ctrl && Input.GetKeyDown(KeyCode.V))
                {
                    string clip = "";
                    try { clip = GUIUtility.systemCopyBuffer ?? ""; } catch { }
                    Type(on, clip.Replace("\r", "").Replace("\n", " "));
                }
                if (Input.GetKeyDown(KeyCode.Delete)) { on.Buffer = ""; on.Fresh = false; }
                foreach (char c in Input.inputString)
                {
                    if (c == '\n' || c == '\r') { End(true, "Enter"); return; }
                    if (c == '\b')
                    {
                        if (on.Fresh) on.Buffer = "";
                        else if (on.Buffer.Length > 0) on.Buffer = on.Buffer.Substring(0, on.Buffer.Length - 1);
                        on.Fresh = false;
                        continue;
                    }
                    if (ctrl || char.IsControl(c)) continue;   // Ctrl+V and the like are no text
                    Type(on, c.ToString());
                }
                on.Paint();
            }

            static void Type(Box b, string s)
            {
                if (s.Length == 0) return;
                if (b.Fresh) { b.Buffer = ""; b.Fresh = false; }
                foreach (char c in s)
                {
                    if (b.Buffer.Length >= b.Limit) break;
                    if (!char.IsControl(c)) b.Buffer += c;
                }
            }

            static void Begin(Box b)
            {
                if (_on != null) End(true, "another box");
                _on = b;
                b.Buffer = b.Value;
                b.Fresh = b.Value.Length > 0;
                b.Bg.color = Editing;
                if (_logged < 6) { _logged++; Go.Log("text box: an edit started (GO-INPUT-3)."); }
                Hold(b);
                b.Paint();
            }

            static void End(bool keep, string how)
            {
                var b = _on;
                _on = null;
                if (b == null) return;
                _releaseFrame = Time.frameCount + 1;   // the game's own Esc handler may still read the selection this frame
                if (_logged < 6) { _logged++; Go.Log($"text box: an edit ended by {how}{(keep ? "" : ", not kept")} (GO-INPUT-3)."); }
                if (b.Bg != null) b.Bg.color = Idle;
                if (keep && b.Buffer != b.Value)
                {
                    b.Value = b.Buffer;
                    try { b.Commit(b.Buffer); } catch (System.Exception ex) { Go.Warn("text box: " + ex.Message); }
                    App.Dirty();
                }
                b.Paint();
            }

            /// <summary>The box being edited stays the EventSystem's selected object (the game then takes the keys as
            /// typing). Another text field taking it over (the chat) ends our edit.</summary>
            static bool Hold(Box b)
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es == null) return true;
                var want = b.Rt.gameObject;
                var cur = es.currentSelectedGameObject;
                if (cur == want) { _held = want; return true; }
                if (cur != null && cur != _held && (cur.GetComponent<TMP_InputField>() != null || cur.GetComponent<InputField>() != null))
                {
                    End(true, "another text field");
                    return false;
                }
                es.SetSelectedGameObject(want);
                _held = want;
                return true;
            }

            static void Release()
            {
                if (_held == null || Time.frameCount < _releaseFrame) return;
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && es.currentSelectedGameObject == _held) es.SetSelectedGameObject(null);
                _held = null;
            }

            /// <summary>The mouse is on the box and inside every mask around it (a box scrolled out of the list does not count).</summary>
            static bool Over(RectTransform rt)
            {
                try
                {
                    var canvas = rt.GetComponentInParent<Canvas>();
                    var root = canvas != null ? canvas.rootCanvas : null;
                    Camera? cam = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
                    Vector2 m = Input.mousePosition;
                    if (!RectTransformUtility.RectangleContainsScreenPoint(rt, m, cam)) return false;
                    foreach (var mask in rt.GetComponentsInParent<RectMask2D>())
                        if (!RectTransformUtility.RectangleContainsScreenPoint(mask.rectTransform, m, cam)) return false;
                    return true;
                }
                catch { return false; }
            }
        }

        /// <summary>A bar from 0 to 1 (a little beyond shows as full and red).</summary>
        /// <summary>GO-LIVERY-1: a colour to pick - a square in that colour, framed white while it is the chosen one.</summary>
        /// <summary>GO-LIVERY-4: a slider (Unity's UI slider) from min to max in steps; set() on every change. The page is
        /// not rebuilt while it moves - labels and pictures follow through their Live refreshers.</summary>
        public static Slider Slider(Transform parent, float width, int min, int max, int step, System.Func<int> get, System.Action<int> set)
        {
            step = Mathf.Max(1, step);
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            Le(go, width, 40f);
            RectTransform Child(string name, Transform under, Color? color, out Image? img)
            {
                var c = new GameObject(name, typeof(RectTransform));
                c.transform.SetParent(under, false);
                img = null;
                if (color != null) { img = c.AddComponent<Image>(); img.color = color.Value; }
                return (RectTransform)c.transform;
            }
            var bg = Child("Background", go.transform, RowBg, out var bgImg);
            bg.anchorMin = new Vector2(0f, 0.5f); bg.anchorMax = new Vector2(1f, 0.5f); bg.sizeDelta = new Vector2(0f, 10f);
            var area = Child("Fill Area", go.transform, null, out _);
            area.anchorMin = new Vector2(0f, 0.5f); area.anchorMax = new Vector2(1f, 0.5f); area.sizeDelta = new Vector2(0f, 10f);
            var fill = Child("Fill", area, RowOn, out var fillImg);
            fill.sizeDelta = Vector2.zero;
            var hArea = Child("Handle Slide Area", go.transform, null, out _);
            hArea.anchorMin = Vector2.zero; hArea.anchorMax = Vector2.one; hArea.offsetMin = new Vector2(10f, 0f); hArea.offsetMax = new Vector2(-10f, 0f);
            var handle = Child("Handle", hArea, Color.white, out var hImg);
            handle.sizeDelta = new Vector2(20f, -14f);   // the slider stretches it over the area's height (40): 26 high
            if (bgImg != null) bgImg.raycastTarget = true;
            if (fillImg != null) fillImg.raycastTarget = false;
            var sl = go.GetComponent<Slider>();
            sl.fillRect = fill; sl.handleRect = handle; sl.targetGraphic = hImg;
            sl.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            sl.minValue = min / step; sl.maxValue = max / step; sl.wholeNumbers = true;
            int now = min;
            try { now = get(); } catch { }
            sl.SetValueWithoutNotify(Mathf.Clamp(now, min, max) / step);
            sl.onValueChanged.AddListener(v => { try { set(Mathf.Clamp(Mathf.RoundToInt(v) * step, min, max)); } catch (System.Exception ex) { Go.Warn("slider: " + ex); } });
            System.Action follow = () =>
            {   // from elsewhere (the default look): the knob follows
                if (sl == null) return;
                int v = min;
                try { v = get(); } catch { }
                float want = Mathf.Clamp(v, min, max) / step;
                if (Mathf.Abs(sl.value - want) > 0.01f && !Input.GetMouseButton(0)) sl.SetValueWithoutNotify(want);
            };
            Live.Add(follow);
            return sl;
        }

        public static Button Swatch(Transform parent, Color color, float size, UnityAction click, System.Func<bool> on)
        {
            var go = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Le(go, size, size);
            var frame = go.GetComponent<Image>();
            frame.color = Color.clear;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = frame;
            btn.onClick.AddListener(() => { try { click(); } catch (System.Exception ex) { Go.Warn("swatch: " + ex); } App.Dirty(); });
            var inner = new GameObject("Colour", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(go.transform, false);
            var rt = (RectTransform)inner.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(5f, 5f); rt.offsetMax = new Vector2(-5f, -5f);
            var img = inner.GetComponent<Image>();
            img.color = color; img.raycastTarget = false;
            System.Action paint = () =>
            {
                if (frame == null) return;
                bool hi = false;
                try { hi = on(); } catch { }
                var c = hi ? Color.white : new Color(1f, 1f, 1f, 0.12f);
                if (frame.color != c) frame.color = c;
            };
            paint();
            Live.Add(paint);
            OnUiLayer(go);
            return btn;
        }

        /// <summary>GO-LIVERY-1: a picture of a fixed size (a texture, or part of one).</summary>
        public static RawImage Picture(Transform parent, float width, float height)
        {
            var go = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            Le(go, width, height);
            var raw = go.GetComponent<RawImage>();
            raw.color = Color.white;
            raw.raycastTarget = false;
            return raw;
        }

        public static void Bar(Transform parent, float width, float height, System.Func<float> frac, System.Func<Color>? color = null)
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Le(go, width, height);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(go.transform, false);
            var frt = (RectTransform)fill.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0f, 1f); frt.offsetMin = frt.offsetMax = Vector2.zero;
            var img = fill.GetComponent<Image>();
            System.Action paint = () =>
            {
                if (frt == null) return;
                float v = 0f;
                try { v = frac(); } catch { }
                frt.anchorMax = new Vector2(Mathf.Clamp01(v), 1f);
                img.color = color != null ? color() : v >= 1f ? Bad : v >= 0.8f ? Warn : Good;
            };
            paint();
            Live.Add(paint);
        }

        /// <summary>24 bars, one per hour of the day (the load of a neighbourhood: green, orange from 80 %, red when full).</summary>
        public static void Chart(Transform parent, float width, float height, System.Func<float[]> values, System.Func<int> nowHour)
        {
            var go = new GameObject("Chart", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Le(go, width, height);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);
            var bars = new Image[24];
            float w = 1f / 24f;
            for (int i = 0; i < 24; i++)
            {
                var b = new GameObject("H" + i, typeof(RectTransform), typeof(Image));
                b.transform.SetParent(go.transform, false);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(i * w, 0f); rt.anchorMax = new Vector2((i + 1) * w, 0f);
                rt.offsetMin = new Vector2(1f, 0f); rt.offsetMax = new Vector2(-1f, 0f);
                bars[i] = b.GetComponent<Image>();
            }
            System.Action paint = () =>
            {
                if (go == null) return;
                float[] v;
                try { v = values() ?? new float[24]; } catch { v = new float[24]; }
                int now = -1;
                try { now = nowHour(); } catch { }
                for (int i = 0; i < 24 && i < v.Length; i++)
                {
                    float f = Mathf.Clamp(v[i], 0f, 1.25f) / 1.25f;
                    var rt = bars[i].rectTransform;
                    rt.anchorMax = new Vector2(rt.anchorMax.x, Mathf.Max(0.02f, f));
                    var c = v[i] >= 1f ? Bad : v[i] >= 0.8f ? Warn : Good;
                    if (i == now) c = Color.Lerp(c, Color.white, 0.35f);
                    bars[i].color = c;
                }
            };
            paint();
            Live.Add(paint);
        }

        /// <summary>A row of a key and its live value (the overview's lines).</summary>
        public static void Pair(Transform parent, string key, System.Func<string> value, float keyWidth = 420f, System.Func<Color>? color = null)
        {
            var r = Row(parent, 40f);
            Label(r, key, keyWidth, 21f, Sub);
            var v = LiveLabel(r, value, -1f, 22f);
            if (color != null) Live.Add(() => { if (v != null) v.color = color(); });
        }

        /// <summary>A technician's skill. The game's font may lack star glyphs, so as a number.</summary>
        public static string Stars(int n) => Go.T("go_skill", "skill {0}/5", Mathf.Clamp(n, 1, 5));
    }
}
