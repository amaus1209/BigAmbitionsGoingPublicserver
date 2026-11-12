using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GoingOnline.Standalone
{
    /// <summary>The app's tile on the BizPhone: a clone of the game's own app button (SmartphoneUI.appButtonTemplate, the
    /// field the game builds its tiles from), the game's button logic and localization stripped, our label and icon. The
    /// phone shows eight apps in four rows; with ours there are five, so the phone grows by a row (GO-PHONE-2, see
    /// <see cref="Fit"/>).</summary>
    internal static class PhoneTile
    {
        static GameObject? _tile;
        static SmartphoneUI? _for;
        static float _next;

        public static void Tick()
        {
            if (!Core.InCity) { _tile = null; _for = null; return; }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 2f;
            try
            {
                var ui = InstanceBehavior<UI.UIs>.Instance?.smartphoneUI;
                if (ui == null) return;
                if (_tile != null && ReferenceEquals(ui, _for)) { Fit(_tile.transform.parent); return; }
                var field = typeof(SmartphoneUI).GetField("appButtonTemplate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var template = field?.GetValue(ui) as Transform;
                if (template == null || template.parent == null) return;
                var parent = template.parent;
                var have = parent.Find("GO_Tile");
                _tile = have != null ? have.gameObject : Make(parent, template.gameObject);
                _for = ui;
                if (_tile != null) Fit(parent);
            }
            catch (System.Exception ex) { Go.Warn("phone tile: " + ex.Message); _next = Time.unscaledTime + 30f; }
        }

        static GameObject Make(Transform parent, GameObject src)
        {
            var go = UnityEngine.Object.Instantiate(src, parent);
            go.name = "GO_Tile";
            foreach (var c in go.GetComponents<Component>())
                if (c != null && c.GetType().Name == "SmartphoneAppButton") UnityEngine.Object.Destroy(c);
            foreach (var c in go.GetComponentsInChildren<Behaviour>(true))
                if (c != null && c.GetType().Name.Contains("Localization")) c.enabled = false;
            foreach (var t in go.GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (t != null) t.text = Go.T("go_app", "Going Online");
            var icon = Icon.Load("GO_AppIcon.png");
            Image? target = null;
            var images = go.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img == null) continue;
                if (img.gameObject.name.IndexOf("icon", System.StringComparison.OrdinalIgnoreCase) >= 0) { target = img; break; }
                if (target == null || img.rectTransform.rect.width > target.rectTransform.rect.width) target = img;
            }
            if (icon != null && target != null)
            {
                target.sprite = icon; target.color = Color.white; target.type = Image.Type.Simple; target.preserveAspect = true;
            }
            foreach (var img in images)
                if (img != null && !ReferenceEquals(img, target)) img.color = new Color(1f, 1f, 1f, 0f);
            var btn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            btn.interactable = true; btn.enabled = true;
            btn.onClick = new Button.ButtonClickedEvent();
            btn.onClick.AddListener((UnityAction)(() => App.Open(-1)));
            if (target != null) btn.targetGraphic = target;
            var badge = go.transform.Find("Badge");
            if (badge != null) badge.gameObject.SetActive(false);
            go.SetActive(true);
            Go.Log("phone tile added.");
            return go;
        }

        /// <summary>GO-PHONE-2 (user 2026-11-08, a screenshot of the phone in single player without any other mod: the icons
        /// covered the names under them): the phone body has room for four rows of tiles. Until 0.8.0 five rows were
        /// squeezed into it - the rows got lower, the icons did not, and every icon sat on its name. Now the phone grows by
        /// the rows it lacks and the tiles keep the game's size, as Going Public Server does it (MPPhoneButton.TryExpandPhone):
        /// the body is the transform of the game's CollapsibleWindow (UIs.smartphoneCollapsibleWindow); it and the tile grid
        /// grow upwards, the bottom stays where it is, and the window's collapsed and peek positions move down by the same
        /// rows, so the collapsed phone shows no more than before. The game sets the body's height itself when the radio
        /// shows or hides - <see cref="PhoneGrowth"/> on the body puts the rows back on top. Without that window the rows
        /// get lower as before.</summary>
        static void Fit(Transform grid)
        {
            var g = grid.GetComponent<GridLayoutGroup>();
            if (g == null) return;
            int active = 0;
            for (int i = 0; i < grid.childCount; i++) if (grid.GetChild(i).gameObject.activeSelf) active++;
            int cols = g.constraint == GridLayoutGroup.Constraint.FixedColumnCount && g.constraintCount > 0 ? g.constraintCount : 2;
            int rows = Mathf.CeilToInt(active / (float)cols);
            if (rows <= 4) return;
            var cw = InstanceBehavior<UI.UIs>.Instance?.smartphoneCollapsibleWindow;
            var body = cw != null ? cw.transform as RectTransform : null;
            if (body == null || !grid.IsChildOf(body) || grid == body) { Squeeze(g, rows); return; }
            var pg = body.GetComponent<PhoneGrowth>() ?? body.gameObject.AddComponent<PhoneGrowth>();
            int more = rows - 4 - pg.Rows;
            if (more <= 0) return;
            float row = g.cellSize.y + g.spacing.y, add = more * row;
            var gr = (RectTransform)grid;
            float before = gr.rect.height;
            float shift = Grow(body, add);
            // the collapsed and peeking phone keeps its top where it was; the open one keeps its bottom
            cw!.unCollapsedPosition = cw.unCollapsedPosition + new Vector3(0f, shift, 0f);
            cw.collapsedPosition = cw.collapsedPosition + new Vector3(0f, shift - add, 0f);
            cw.hoverPosition = cw.hoverPosition + new Vector3(0f, shift - add, 0f);
            if (cw.IsCollapsed) body.anchoredPosition += new Vector2(0f, -add);
            float grown = gr.rect.height - before;   // a grid stretched with the body has grown already
            if (grown < add - 0.5f) Grow(gr, add - grown);
            pg.Rows += more;
            pg.Row = row;
            pg.Held = body.rect.height;
            LayoutRebuilder.MarkLayoutForRebuild(gr);
            Go.Log($"phone: {more} row(s) taller for the app tiles, the tiles keep the game's size (GO-PHONE-2).");
        }

        internal static float Grow(RectTransform rt, float extra)
        {
            float shift = extra * rt.pivot.y;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, rt.sizeDelta.y + extra);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y + shift);
            return shift;
        }

        /// <summary>Without the game's phone window: the rows get lower, so the phone keeps its height (the icons then reach
        /// into the names - the old way, only as a fallback).</summary>
        static void Squeeze(GridLayoutGroup g, int rows)
        {
            float want = 900f / rows;   // four native rows of 225
            if (g.cellSize.y > want + 0.5f) g.cellSize = new Vector2(g.cellSize.x, want);
        }
    }

    /// <summary>GO-PHONE-2: on the phone body - the rows it grew by, kept on top of the heights the game sets itself (its
    /// radio row shows or hides). It acts on a height that has stood still for half a second and is not the one it set,
    /// so it never runs after an animation and never adds twice. It lives as long as the phone does.</summary>
    internal sealed class PhoneGrowth : MonoBehaviour
    {
        public int Rows;
        public float Row = 225f, Held = -1f;
        float _last = -1f, _next;

        void Update()
        {
            if (Rows <= 0 || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            var rt = transform as RectTransform;
            if (rt == null) return;
            float h = rt.rect.height;
            bool still = Mathf.Abs(h - _last) < 1f;
            _last = h;
            if (!still || Mathf.Abs(h - Held) < 1f) return;
            float add = Rows * Row;
            PhoneTile.Grow(rt, add);
            Held = rt.rect.height;
            _last = Held;
            Go.Log($"phone: the game set its height to {h:0} - {Rows} row(s) on top again, {Held:0} (GO-PHONE-2).");
        }
    }
}
