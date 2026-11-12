using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BigAmbitionsMP
{
    /// <summary>PHONE-PAGES-1 (user 2026-11-02: "build in Aluna's Phone Pages - rebuild it and say that it is rebuilt - and
    /// put Radio and so on in it right away"): the BizPhone's apps in pages of eight (two columns, four rows), as Aluna's
    /// Phone Pages (Steam Workshop, by aluna) shows them - our own code, modelled on that mod. The game's apps fill the
    /// first page, the mod's follow (Chat, Business, Radio, Navigation, Planner, Insights, camera, shop signs); in the row
    /// under the tiles a bar with the arrows, "1 / 2" and a dot per page (a dot opens its page).
    /// A tile of another page leaves the grid's layout and turns invisible (its own CanvasGroup and LayoutElement - the
    /// game's app buttons use neither) - its active state stays the game's, so a tile the game shows or hides later finds
    /// its page by itself. The game's keyboard outline on an app turns to that app's page. A tile of a module the server
    /// switched off stays out.
    /// The server's admin switches the pages off (AMP "ModulePhonePages"): the phone then shows its apps as before - the
    /// game's, Chat and Business in five rows - and the mod's other apps stay out of it.
    /// PHONE-PAGES-2 (user 2026-11-02: "Aluna's Phone Pages also let you arrange the icons yourself, move them wherever you
    /// want"): an app dragged with the mouse goes where it is dropped - before or after the nearest app of the page shown;
    /// held over an arrow it turns the page. The order is this machine's (MPConfig "PhoneOrder"), the game's apps by their
    /// AppName, the mod's by their tile's name; an app new since then comes after the others.</summary>
    internal static class MPPhonePages
    {
        internal const int PerPage = 8;
        private const string BarName = "BAMP_PageBar";
        private static readonly Color Lit = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.32f);

        private static Transform? _grid;
        private static GridLayoutGroup? _layout;
        private static TextAnchor _alignment;
        private static RectTransform? _bar, _dotsRow;
        private static TextMeshProUGUI? _count;
        private static Image? _prevImg, _nextImg;
        private static readonly List<Image> _dots = new List<Image>();
        private static readonly List<(GameObject go, string module)> _modOnly = new List<(GameObject, string)>();
        private static readonly List<GameObject> _tiles = new List<GameObject>();
        private static int _page, _pages = 1;
        private static string _laid = "";
        private static float _nextCheck;
        private static bool _warned;
        private static Sprite? _disc, _left, _right;
        private static GameObject? _drag;   // PHONE-PAGES-2: the app being moved
        private static int _flipDir;
        private static float _flipAt;

        internal static bool On => ServerRules.Module("ModulePhonePages");

        internal static void Reset()
        {
            _grid = null; _layout = null; _bar = null; _dotsRow = null; _count = null; _prevImg = _nextImg = null;
            _dots.Clear(); _modOnly.Clear(); _tiles.Clear();
            _page = 0; _pages = 1; _laid = ""; _nextCheck = 0f;
            _drag = null; _flipDir = 0;
        }

        /// <summary>Once per phone, after the mod's tiles are in the grid: modOnly are the apps that only the pages hold
        /// (with the module that has to be on for each, "" for none).</summary>
        internal static void Attach(Transform grid, List<(GameObject go, string module)> modOnly)
        {
            Reset();
            _grid = grid;
            _modOnly.AddRange(modOnly);
            _layout = grid.GetComponent<GridLayoutGroup>();
            if (_layout != null) _alignment = _layout.childAlignment;
            try { Build(grid); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[PhonePages] bar: {ex.Message}"); }
            try { ApplySavedOrder(); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[PhonePages] saved order: {ex.Message}"); }
            Apply();
            Plugin.Logger.LogInfo($"[PhonePages] attached: {_tiles.Count} app(s) in {_pages} page(s) (pages {(On ? "on" : "off")}).");
        }

        /// <summary>Main thread, every frame once the phone is there.</summary>
        internal static void Tick()
        {
            if (_grid == null) return;
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.2f;
            Apply();
            FollowOutline();
        }

        private static void Go(int page)
        {
            page = Mathf.Clamp(page, 0, _pages - 1);
            if (page == _page) return;
            _page = page;
            Apply();
        }

        private static void Apply()
        {
            try
            {
                if (_grid == null) return;
                bool on = On;
                _tiles.Clear();
                for (int i = 0; i < _grid.childCount; i++)
                {
                    var c = _grid.GetChild(i).gameObject;
                    if (c.name == BarName || c == _drag) continue;   // the dragged app counts nowhere until it is dropped
                    int mine = _modOnly.FindIndex(m => m.go == c);
                    if (mine >= 0)
                    {
                        string module = _modOnly[mine].module;
                        bool want = on && (module.Length == 0 || ServerRules.Module(module));
                        if (c.activeSelf != want) c.SetActive(want);
                    }
                    if (!c.activeSelf) continue;   // the game's inactive template stays out by itself
                    if (c.GetComponent<PhoneTileDrag>() == null) c.AddComponent<PhoneTileDrag>();
                    _tiles.Add(c);
                }
                _pages = on ? Math.Max(1, (_tiles.Count + PerPage - 1) / PerPage) : 1;
                if (_page >= _pages) _page = _pages - 1;
                var ids = new System.Text.StringBuilder(on ? "on" : "off").Append(':').Append(_page);
                foreach (var t in _tiles) ids.Append(',').Append(t.GetInstanceID());
                string key = ids.ToString();
                if (key == _laid) return;
                _laid = key;
                for (int i = 0; i < _tiles.Count; i++) Show(_tiles[i], !on || i / PerPage == _page);
                if (_layout != null)
                {   // the rows from the top, so the bar has the last row to itself
                    var want = on ? Upper(_alignment) : _alignment;
                    if (_layout.childAlignment != want) _layout.childAlignment = want;
                }
                PaintBar(on && _pages > 1);
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)_grid);
            }
            catch (Exception ex)
            {
                if (!_warned) { _warned = true; Plugin.Logger.LogWarning($"[PhonePages] {ex.Message}"); }
            }
        }

        private static TextAnchor Upper(TextAnchor a) => a switch
        {
            TextAnchor.MiddleLeft or TextAnchor.LowerLeft => TextAnchor.UpperLeft,
            TextAnchor.MiddleCenter or TextAnchor.LowerCenter => TextAnchor.UpperCenter,
            TextAnchor.MiddleRight or TextAnchor.LowerRight => TextAnchor.UpperRight,
            _ => a,
        };

        private static void Show(GameObject tile, bool visible)
        {
            var cg = tile.GetComponent<CanvasGroup>();
            if (cg == null) cg = tile.AddComponent<CanvasGroup>();
            var le = tile.GetComponent<LayoutElement>();
            if (le == null) le = tile.AddComponent<LayoutElement>();
            cg.alpha = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
            cg.interactable = visible;
            le.ignoreLayout = !visible;
        }

        /// <summary>The game's keyboard outline (SmartphoneUI.OutlineApp) on an app of another page opens that page.</summary>
        private static void FollowOutline()
        {
            try
            {
                if (_pages < 2) return;
                var ui = InstanceBehavior<UI.UIs>.Instance?.smartphoneUI;
                if (ui == null || !ui._isThereAnAppOutlined) return;
                if (!ui.appButtons.TryGetValue(ui._currentOutlinedApp, out var b) || b == null) return;
                int i = _tiles.IndexOf(b.gameObject);
                if (i >= 0 && i / PerPage != _page) Go(i / PerPage);
            }
            catch { }
        }

        // ── PHONE-PAGES-2: moving an app ──

        internal static void BeginDrag(GameObject tile, PointerEventData e)
        {
            if (_grid == null || tile.transform.parent != _grid || _drag != null) return;
            _drag = tile;
            var cg = tile.GetComponent<CanvasGroup>();
            if (cg == null) cg = tile.AddComponent<CanvasGroup>();
            cg.alpha = 0.85f;
            cg.blocksRaycasts = false;   // the drop sees what lies under it
            var le = tile.GetComponent<LayoutElement>();
            if (le == null) le = tile.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            tile.transform.SetAsLastSibling();   // drawn over the others while it moves; its place is set on the drop
            _flipDir = 0;
            _laid = "";
            Apply();   // the others close up
            Dragging(tile, e);
        }

        internal static void Dragging(GameObject tile, PointerEventData e)
        {
            if (_drag != tile || _grid == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_grid, e.position, e.pressEventCamera, out var local))
                tile.transform.localPosition = new Vector3(local.x, local.y, 0f);
            // held over an arrow for half a second: that page
            int dir = Over(_prevImg, e) ? -1 : Over(_nextImg, e) ? 1 : 0;
            if (dir != _flipDir) { _flipDir = dir; _flipAt = Time.unscaledTime + 0.5f; }
            else if (dir != 0 && Time.unscaledTime >= _flipAt) { _flipAt = Time.unscaledTime + 0.8f; Go(_page + dir); }
        }

        internal static void EndDrag(GameObject tile, PointerEventData e)
        {
            if (_drag != tile) return;
            _drag = null;
            _flipDir = 0;
            try
            {
                if (_grid != null)
                {
                    // before or after the nearest app of the page shown (all of them with the pages off)
                    int per = On ? PerPage : int.MaxValue;
                    int from = On ? _page * PerPage : 0, to = Math.Min(_tiles.Count, from + Math.Min(per, _tiles.Count));
                    int at = to;
                    float best = float.MaxValue;
                    for (int i = from; i < to; i++)
                    {
                        var rt = (RectTransform)_tiles[i].transform;
                        Vector2 c = RectTransformUtility.WorldToScreenPoint(e.pressEventCamera, rt.TransformPoint(rt.rect.center));
                        float d = (c - e.position).sqrMagnitude;
                        if (d < best) { best = d; at = e.position.x < c.x ? i : i + 1; }
                    }
                    var order = new List<GameObject>(_tiles);   // without the moved app (Apply left it out)
                    order.Insert(Mathf.Clamp(at, 0, order.Count), tile);
                    int first = int.MaxValue;
                    foreach (var go in order) first = Math.Min(first, go.transform.GetSiblingIndex());
                    for (int k = 0; k < order.Count; k++) order[k].transform.SetSiblingIndex(first + k);
                    SaveOrder();
                }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[PhonePages] drop: {ex.Message}"); }
            finally
            {
                var cg = tile.GetComponent<CanvasGroup>();
                if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }
                _laid = "";
                Apply();
            }
        }

        private static bool Over(Image? img, PointerEventData e) =>
            img != null && img.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(img.rectTransform, e.position, e.pressEventCamera);

        /// <summary>The game's app tiles by their AppName ("app:Contacts").</summary>
        private static Dictionary<GameObject, string> NativeKeys()
        {
            var map = new Dictionary<GameObject, string>();
            try
            {
                var ui = InstanceBehavior<UI.UIs>.Instance?.smartphoneUI;
                if (ui != null)
                    foreach (var kv in ui.appButtons)
                        if (kv.Value != null) map[kv.Value.gameObject] = "app:" + kv.Key;
            }
            catch { }
            return map;
        }

        private static string KeyOf(GameObject go, Dictionary<GameObject, string> natives) =>
            natives.TryGetValue(go, out var k) ? k : go.name;

        private static void SaveOrder()
        {
            if (_grid == null) return;
            var natives = NativeKeys();
            var keys = new List<string>();
            for (int i = 0; i < _grid.childCount; i++)
            {
                var c = _grid.GetChild(i).gameObject;
                if (c.name == BarName) continue;
                string k = KeyOf(c, natives);
                if (k.Length > 0 && !keys.Contains(k)) keys.Add(k);
            }
            MPConfig.SetPhoneOrder(string.Join("|", keys));
            Plugin.Logger.LogInfo($"[PhonePages] order saved ({keys.Count} entries).");
        }

        private static void ApplySavedOrder()
        {
            string saved = MPConfig.PhoneOrder;
            if (_grid == null || saved.Length == 0) return;
            var natives = NativeKeys();
            var byKey = new Dictionary<string, GameObject>();
            var all = new List<GameObject>();
            for (int i = 0; i < _grid.childCount; i++)
            {
                var c = _grid.GetChild(i).gameObject;
                if (c.name == BarName) continue;
                all.Add(c);
                string k = KeyOf(c, natives);
                if (!byKey.ContainsKey(k)) byKey[k] = c;
            }
            var order = new List<GameObject>();
            foreach (var k in saved.Split('|'))
                if (byKey.TryGetValue(k, out var go) && !order.Contains(go)) order.Add(go);
            foreach (var c in all) if (!order.Contains(c)) order.Add(c);   // new since the order was saved: after it
            for (int k = 0; k < order.Count; k++) order[k].transform.SetSiblingIndex(k);
        }

        // ── the bar: ◄  1 / 2  ►, the dots under the count ──

        private static void Build(Transform grid)
        {
            var old = grid.Find(BarName);
            if (old != null) UnityEngine.Object.Destroy(old.gameObject);
            float cell = _layout != null ? _layout.cellSize.y : 225f;
            float h = Mathf.Round(cell * 0.72f);
            var bar = new GameObject(BarName, typeof(RectTransform)).GetComponent<RectTransform>();
            bar.SetParent(grid, false);
            bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(1f, 0f); bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, h);
            bar.anchoredPosition = new Vector2(0f, Mathf.Round((cell - h) * 0.5f));
            _bar = bar;

            float arrow = Mathf.Round(h * 0.62f);
            _prevImg = Arrow(bar, "Prev", true, arrow, () => Go(_page - 1));
            _nextImg = Arrow(bar, "Next", false, arrow, () => Go(_page + 1));

            var font = NativeFont(grid);
            var count = new GameObject("Count", typeof(RectTransform)).GetComponent<RectTransform>();
            count.SetParent(bar, false);
            count.anchorMin = count.anchorMax = new Vector2(0.5f, 1f); count.pivot = new Vector2(0.5f, 1f);
            count.sizeDelta = new Vector2(h * 2.2f, h * 0.5f);
            count.anchoredPosition = new Vector2(0f, -h * 0.06f);
            _count = count.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) _count.font = font;
            _count.fontSize = Mathf.Max(14f, h * 0.3f);
            _count.alignment = TextAlignmentOptions.Center;
            _count.color = Lit;
            _count.raycastTarget = false;
            _count.textWrappingMode = TextWrappingModes.NoWrap;

            var row = new GameObject("Dots", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(bar, false);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0f); row.pivot = new Vector2(0.5f, 0f);
            row.sizeDelta = new Vector2(h * 2.2f, h * 0.36f);
            row.anchoredPosition = new Vector2(0f, h * 0.1f);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.spacing = Mathf.Round(h * 0.12f);
            hl.childControlWidth = hl.childControlHeight = false;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            _dotsRow = row;
        }

        private static void PaintBar(bool visible)
        {
            if (_bar == null) return;
            if (_bar.gameObject.activeSelf != visible) _bar.gameObject.SetActive(visible);
            if (!visible) return;
            _bar.SetAsLastSibling();
            if (_count != null) _count.text = $"{_page + 1} / {_pages}";
            if (_prevImg != null) _prevImg.color = _page > 0 ? Lit : Dim;
            if (_nextImg != null) _nextImg.color = _page < _pages - 1 ? Lit : Dim;
            if (_dotsRow == null) return;
            float size = Mathf.Round(_bar.rect.height * 0.15f);
            while (_dots.Count < _pages)
            {
                int index = _dots.Count;
                var d = new GameObject("Dot", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                d.SetParent(_dotsRow, false);
                d.sizeDelta = new Vector2(size, size);
                var img = d.GetComponent<Image>();
                img.sprite = Disc();
                var btn = d.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener((UnityAction)(() => Go(index)));
                _dots.Add(img);
            }
            for (int i = 0; i < _dots.Count; i++)
            {
                bool show = i < _pages;
                if (_dots[i].gameObject.activeSelf != show) _dots[i].gameObject.SetActive(show);
                _dots[i].color = i == _page ? Lit : Dim;
            }
        }

        private static Image Arrow(RectTransform bar, string name, bool left, float size, Action click)
        {
            var rt = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rt.SetParent(bar, false);
            rt.anchorMin = rt.anchorMax = new Vector2(left ? 0f : 1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(left ? size * 0.9f : -size * 0.9f, 0f);
            var img = rt.GetComponent<Image>();
            img.sprite = left ? (_left ??= Triangle(true)) : (_right ??= Triangle(false));
            img.preserveAspect = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener((UnityAction)(() => click()));
            return img;
        }

        /// <summary>The font of the game's own app labels, so the count looks like the phone's text.</summary>
        private static TMP_FontAsset? NativeFont(Transform grid)
        {
            for (int i = 0; i < grid.childCount; i++)
            {
                var t = grid.GetChild(i).GetComponentInChildren<TMP_Text>(true);
                if (t != null && t.font != null) return t.font;
            }
            return null;
        }

        private static Sprite Disc()
        {
            if (_disc != null) return _disc;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
                }
            tex.Apply();
            _disc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            _disc.hideFlags = HideFlags.HideAndDontSave;
            return _disc;
        }

        /// <summary>A filled triangle pointing left or right, its edges smoothed (the game's font may lack ◄ ►).</summary>
        private static Sprite Triangle(bool left)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            Vector2[] t = left
                ? new[] { new Vector2(14f, 32f), new Vector2(46f, 56f), new Vector2(46f, 8f) }
                : new[] { new Vector2(50f, 32f), new Vector2(18f, 8f), new Vector2(18f, 56f) };
            float wind = Mathf.Sign(Cross(t[1] - t[0], t[2] - t[0]));
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = float.MaxValue;   // the distance to the nearest edge, positive inside
                    for (int k = 0; k < 3; k++)
                    {
                        Vector2 e = t[(k + 1) % 3] - t[k];
                        d = Mathf.Min(d, wind * Cross(e, p - t[k]) / e.magnitude);
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(d + 0.5f)));
                }
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }

    /// <summary>PHONE-PAGES-2: on every phone app - the mouse drags it to another place (a drag past the event system's
    /// threshold is no click, so the app does not open).</summary>
    internal sealed class PhoneTileDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public void OnBeginDrag(PointerEventData e) => MPPhonePages.BeginDrag(gameObject, e);
        public void OnDrag(PointerEventData e) => MPPhonePages.Dragging(gameObject, e);
        public void OnEndDrag(PointerEventData e) => MPPhonePages.EndDrag(gameObject, e);
    }

    public partial class MPCanvasUI
    {
        /// <summary>PHONE-PAGES-1: an app of the mod tapped on the phone opens (or closes) what its key does.</summary>
        private void OpenPhoneApp(string app)
        {
            try
            {
                switch (app)
                {
                    case "radio":
                        RadioPhonePage.OpenFromPhone();
                        break;
                    case "goingonline":
                        GoingOnlineModule.OpenFromPhone();   // GOING-ONLINE-1
                        break;
                    case "nav":
                        if (_navOpen) NavClose();
                        else if (ServerRules.Module("ModuleNavigation")) NavOpen();
                        else ServerRules.ModuleOff(Loc.T("gps_nav_title", "Navigation"));
                        break;
                    case "planner":
                        if (_plOpen) PlClose();
                        else if (ServerRules.Module("ModulePlanner")) PlOpen();
                        else ServerRules.ModuleOff(Loc.T("gps_plan_title", "Planner"));
                        break;
                    case "insights":
                        if (_inOpen) InClose();
                        else if (ServerRules.Module("ModuleInsights")) InOpen();
                        else ServerRules.ModuleOff(Loc.T("gps_ins_title", "Insights"));
                        break;
                    case "camera":
                        if (_camOpen) CamClose();
                        else if (ServerRules.Module("ModuleCamera")) CamOpen();
                        else ServerRules.ModuleOff(Loc.T("gps_cam_title", "RPG camera"));
                        break;
                    case "signs":
                        if (_sgOpen) SgClose();
                        else if (ServerRules.Module("ModuleShopSigns")) SgOpen();
                        else ServerRules.ModuleOff(Loc.T("gps_signs_title", "Shop signs"));
                        break;
                }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[PhonePages] open '{app}': {ex.Message}"); }
        }
    }
}
