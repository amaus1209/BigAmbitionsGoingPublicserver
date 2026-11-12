using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GoingOnline
{
    /// <summary>"Going Online" as an app of the game's full menu, built the way Going Public Server's Radio page is: a
    /// clone of the menu's own top-row button and a page beside the game's app pages, so the shell around it is the game's.
    /// The page has a list of tabs on the left and the tab's content on the right. Shift+F12 opens and closes it.</summary>
    public static class App
    {
        public static bool Ready { get; private set; }
        public static bool Visible => _page != null && _page.activeInHierarchy;

        static GameObject? _page, _button, _selectedIcon;
        static TextMeshProUGUI? _appNameLbl;
        static Behaviour? _appNameLoc;
        static Transform? _container;
        static RectTransform? _nav, _list, _root;
        static ScrollRect? _scroll;
        const float NavWidth = 360f, RightMargin = 40f, ListPadRight = 24f;   // the scroll area's offsets in Build, the list's padding

        /// <summary>GO-LAYOUT-1: the width the tab content has (root units) - the rows that would not fit go onto two lines.</summary>
        internal static float ListWidth => (_root != null ? _root.sizeDelta.x : 1920f) - NavWidth - RightMargin - ListPadRight;
        static float _graceUntil, _nextTry, _nextRefresh;
        static int _tries, _showFrames;
        static bool _wasActive, _dirty;
        static string _sig = "";
        internal static int Tab;

        internal static readonly string[] TabKeys =
            { "go_tab_overview", "go_tab_network", "go_tab_buildings", "go_tab_tariffs", "go_tab_devices", "go_tab_tv", "go_tab_orders", "go_tab_staff", "go_tab_finance", "go_tab_rival", "go_tab_van" };
        internal static readonly string[] TabEnglish =
            { "Overview", "Network", "Buildings", "Tariffs", "Devices", "TV", "Orders", "Staff", "Finance", "Competition", "Service van" };

        public static void Reset()
        {
            Ready = false;
            _page = _button = _selectedIcon = null; _appNameLbl = null; _appNameLoc = null; _container = null;
            _nav = _list = _root = null; _scroll = null;
            _graceUntil = _nextTry = _nextRefresh = 0f; _tries = 0; _showFrames = 0; _wasActive = false; _sig = "";
            Ui.Live.Clear();
        }

        /// <summary>Something changed: the page rebuilds at its next refresh.</summary>
        internal static void Dirty() { _dirty = true; _nextRefresh = 0f; }

        public static void Tick()
        {
            if (!Ready) { TryInject(); return; }
            if (_page == null) { Reset(); return; }
            Hotkey();
            if (_showFrames > 0) { _showFrames--; ShowPage(); }
            Ui.Typing.Tick();   // GO-INPUT-3: the text boxes - clicks, keys and the selection
            bool active = Visible;
            if (active)
            {
                if (OtherPageActive()) { HidePage(); return; }   // a native app was chosen in the menu
                if (_appNameLoc != null && _appNameLoc.enabled) _appNameLoc.enabled = false;
                string title = Go.T("go_app", "Going Online");
                if (_appNameLbl != null && _appNameLbl.text != title) _appNameLbl.text = title;
                if (_selectedIcon != null && !_selectedIcon.activeSelf) _selectedIcon.SetActive(true);
                FitWidth();
                if (Time.unscaledTime >= _nextRefresh) { _nextRefresh = Time.unscaledTime + 0.5f; Refresh(); }
            }
            else if (_wasActive) Release();
            _wasActive = active;
        }

        /// <summary>GO-LAYOUT-1 (user 2026-11-06: "der Bildschirm ist verschoben", the buildings table cut off on the right):
        /// the page was a fixed 1920 wide in the middle of the menu - on a 16:9 screen 83 % of it, while the game's own apps
        /// use the whole width, and a buildings row (1660, with fiber 2100) did not fit the 1500 left beside the tabs. The
        /// page now takes the menu's whole width (at least the old 1920); a change rebuilds the tab for the new room.</summary>
        static void FitWidth()
        {
            if (_root == null || _page == null) return;
            float have = ((RectTransform)_page.transform).rect.width / Mathf.Max(0.01f, _root.localScale.x);
            if (have < 100f) return;   // not laid out yet
            float want = Mathf.Max(1920f, have - 20f);   // never narrower than before
            if (Mathf.Abs(_root.sizeDelta.x - want) < 2f) return;
            _root.sizeDelta = new Vector2(want, _root.sizeDelta.y);
            Dirty();
        }

        static void Hotkey()
        {
            try
            {
                if (!Input.GetKeyDown(KeyCode.F12) || !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) return;
                if (Visible) Close(); else Open(-1);
            }
            catch { }
        }

        /// <summary>Opens the page (on a tab, -1 keeps the last one); the full menu opens first if it is closed.</summary>
        public static void Open(int tab)
        {
            try
            {
                if (tab >= 0 && tab < TabKeys.Length) { Tab = tab; _sig = ""; }
                if (!Ready || _page == null) return;
                if (!UI.Smartphone.FullMenu.IsOpen) Go.Host?.OpenFullMenu();
                ShowPage();
                _showFrames = 3;   // the menu may select its own app a frame later
            }
            catch (System.Exception ex) { Go.Warn("open: " + ex.Message); }
        }

        public static void Close()
        {
            HidePage();
            try { InstanceBehavior<UI.UIs>.Instance?.fullMenu?.CloseFullMenu(); } catch { }
        }

        public static void HidePage()
        {
            try
            {
                _showFrames = 0;
                if (_page != null && _page.activeSelf) _page.SetActive(false);
                if (_wasActive) Release();
                _wasActive = false;
            }
            catch { }
        }

        static void Release()
        {
            if (_selectedIcon != null) _selectedIcon.SetActive(false);
            if (_appNameLoc != null) _appNameLoc.enabled = true;
        }

        static bool OtherPageActive()
        {
            if (_container == null || _showFrames > 0) return false;
            for (int i = 0; i < _container.childCount; i++)
            {
                var ch = _container.GetChild(i).gameObject;
                if (ch != _page && ch.activeSelf) return true;
            }
            return false;
        }

        static void TryInject()
        {
            if (_graceUntil == 0f) { _graceUntil = Time.unscaledTime + 3f; return; }
            if (Time.unscaledTime < _graceUntil || _tries >= 30 || Time.unscaledTime < _nextTry) return;
            _nextTry = Time.unscaledTime + 2f;
            _tries++;
            try
            {
                var menu = UnityEngine.Object.FindObjectOfType<UI.Smartphone.FullMenu>(true);
                if (menu == null) return;
                var canvasT = menu.transform.Find("Canvas");
                var row = canvasT != null ? canvasT.Find("Topbar/AppButtons") : null;
                var container = canvasT != null ? canvasT.Find("AppsContainer") : null;
                var template = row != null ? row.Find("AppButtonTemplate") : null;
                if (canvasT == null || row == null || container == null || template == null)
                {
                    Go.Warn("full menu paths not found (canvas/row/container/template).");
                    return;
                }
                var old = row.Find("GO_App");
                if (old != null) UnityEngine.Object.Destroy(old.gameObject);
                var oldPage = container.Find("GO_AppPage");
                if (oldPage != null) UnityEngine.Object.Destroy(oldPage.gameObject);

                var appName = canvasT.Find("Topbar/AppName");
                _appNameLbl = appName != null ? appName.GetComponent<TextMeshProUGUI>() : null;
                if (appName != null)
                    foreach (var b in appName.GetComponents<Behaviour>())
                        if (b != null && b.GetType().Name.Contains("Localization")) { _appNameLoc = b; break; }

                _button = UnityEngine.Object.Instantiate(template.gameObject, row);
                _button.name = "GO_App";
                foreach (var c in _button.GetComponents<Component>())
                    if (c != null && c.GetType().Name == "FullMenuAppButton") UnityEngine.Object.Destroy(c);
                foreach (var b in _button.GetComponentsInChildren<Behaviour>(true))
                    if (b != null && b.GetType().Name.Contains("Localization")) b.enabled = false;
                var titleT = _button.transform.Find("Title");
                var titleLbl = titleT != null ? titleT.GetComponent<TextMeshProUGUI>() : null;
                if (titleLbl != null) { titleLbl.text = Go.T("go_app", "Going Online"); Ui.Font = titleLbl.font; }
                var logoT = _button.transform.Find("Logo");
                var logoImg = logoT != null ? logoT.GetComponent<Image>() : null;
                var icon = Icon.Load("GO_AppIcon.png");
                if (logoImg != null && icon != null) { logoImg.sprite = icon; logoImg.color = Color.white; logoImg.preserveAspect = true; }
                _selectedIcon = _button.transform.Find("SelectedIcon")?.gameObject;
                if (_selectedIcon != null) _selectedIcon.SetActive(false);
                var btn = _button.GetComponent<Button>();
                if (btn != null)
                {
                    btn.interactable = true; btn.enabled = true;
                    btn.onClick = new Button.ButtonClickedEvent();
                    btn.onClick.AddListener((UnityAction)(() => Open(-1)));
                }
                _button.SetActive(true);

                _container = container;
                _page = new GameObject("GO_AppPage", typeof(RectTransform), typeof(CanvasGroup));
                _page.transform.SetParent(container, false);
                Ui.Stretch((RectTransform)_page.transform);
                var root = new GameObject("Root", typeof(RectTransform));
                root.transform.SetParent(_page.transform, false);
                var rrt = (RectTransform)root.transform;
                rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
                rrt.sizeDelta = new Vector2(1920f, 875f);
                rrt.localScale = new Vector3(2f, 2f, 1f);
                _root = rrt;
                Build(rrt);
                _page.SetActive(false);
                Ready = true;
                Go.Log("app injected into the full menu.");
            }
            catch (System.Exception ex) { Go.Warn("inject: " + ex.Message); }
        }

        static void Build(RectTransform root)
        {
            var nav = new GameObject("Tabs", typeof(RectTransform), typeof(VerticalLayoutGroup));
            nav.transform.SetParent(root, false);
            _nav = (RectTransform)nav.transform;
            _nav.anchorMin = new Vector2(0f, 0f); _nav.anchorMax = new Vector2(0f, 1f); _nav.pivot = new Vector2(0f, 1f);
            _nav.offsetMin = new Vector2(40f, 30f); _nav.offsetMax = new Vector2(330f, -30f);
            var v = nav.GetComponent<VerticalLayoutGroup>();
            v.spacing = 10f; v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(root, false);
            var sct = (RectTransform)scrollGo.transform;
            sct.anchorMin = Vector2.zero; sct.anchorMax = Vector2.one;
            sct.offsetMin = new Vector2(360f, 30f); sct.offsetMax = new Vector2(-40f, -30f);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollGo.transform, false);
            Ui.Stretch((RectTransform)viewport.transform);
            var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            list.transform.SetParent(viewport.transform, false);
            _list = (RectTransform)list.transform;
            _list.anchorMin = new Vector2(0f, 1f); _list.anchorMax = new Vector2(1f, 1f); _list.pivot = new Vector2(0.5f, 1f);
            _list.sizeDelta = Vector2.zero;
            var vlg = list.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f; vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0, 24, 0, 24);
            list.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.viewport = (RectTransform)viewport.transform;
            _scroll.content = _list;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 45f;
        }

        static void ShowPage()
        {
            try
            {
                if (_page == null || _container == null) return;
                for (int i = 0; i < _container.childCount; i++)
                {
                    var ch = _container.GetChild(i).gameObject;
                    if (ch != _page && ch.activeSelf) ch.SetActive(false);
                }
                Go.Host?.HideOtherPages();
                var row = _button != null ? _button.transform.parent : null;
                if (row != null)
                    for (int i = 0; i < row.childCount; i++)
                    {
                        var ch = row.GetChild(i);
                        if (ch.gameObject == _button) continue;
                        var dot = ch.Find("SelectedIcon");
                        if (dot != null && dot.gameObject.activeSelf) dot.gameObject.SetActive(false);
                    }
                bool was = _page.activeSelf;
                _page.SetActive(true);
                if (!was) { Dirty(); if (_scroll != null) _scroll.verticalNormalizedPosition = 1f; }
            }
            catch (System.Exception ex) { Go.Warn("show: " + ex.Message); }
        }

        internal static void SetTab(int tab)
        {
            Tab = tab;
            Dirty();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        static void Refresh()
        {
            if (_list == null || _nav == null) return;
            var s = Core.S;
            string sig = (s == null ? "new" : "isp") + "|" + Tab + "|" + Tabs.Signature(s, Tab);
            if (Ui.Typing.Busy && sig.StartsWith((s == null ? "new" : "isp") + "|" + Tab + "|")) return;   // GO-INPUT-3: not while a box is edited (same tab)
            if (_dirty || sig != _sig)
            {
                _dirty = false;
                _sig = sig;
                var keep = _list.anchoredPosition;
                Ui.Live.Clear();
                Clear(_list);
                Clear(_nav);
                try
                {
                    if (s == null) Tabs.Founding(_list);
                    else
                    {
                        Nav(s);
                        Tabs.Build(s, Tab, _list);
                    }
                }
                catch (System.Exception ex) { Go.Warn($"tab {Tab}: {ex}"); Ui.Para(_list, "Error: " + ex.Message, 20f, Ui.Bad); }
                _list.anchoredPosition = keep;
                return;
            }
            for (int i = 0; i < Ui.Live.Count; i++)
            {
                try { Ui.Live[i](); } catch { }
            }
        }

        /// <summary>The old rows leave the layout at once (Destroy only takes effect at the end of the frame).</summary>
        static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var ch = t.GetChild(i).gameObject;
                ch.SetActive(false);
                ch.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(ch);
            }
        }

        static void Nav(IspState s)
        {
            var title = Ui.Para(_nav!, s.Company, 26f, Color.white, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.Normal;
            Ui.LivePara(_nav!, () => Go.T("go_nav_customers", "{0} customers", Go.Num(Sim.Customers(s))), 19f, Ui.Sub);
            Ui.Gap(_nav!, 8f);
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int tab = i;
                var row = Ui.Row(_nav!, 54f);
                System.Func<string> label = () =>
                {
                    string t = Go.T(TabKeys[tab], TabEnglish[tab]);
                    if (tab == 6)
                    {
                        int n = 0;
                        foreach (var k in s.Tickets) if (Cat.IsFault(k.Type)) n++;
                        if (n > 0) t += $"  <color={Ui.Hex(Ui.Warn)}>{n}</color>";
                    }
                    return t;
                };
                Ui.Btn(row, "", -1f, () => SetTab(tab), null, label, () => Tab == tab, 22f).GetComponent<LayoutElement>().flexibleWidth = 1f;
            }
        }
    }

    /// <summary>The mod's icons from its assets\ folder.</summary>
    internal static class Icon
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite? Load(string file)
        {
            if (Cache.TryGetValue(file, out var have) && have != null) return have;
            try
            {
                string root = Go.Host?.ModRoot ?? "";
                string path = System.IO.Path.Combine(root, "assets", file);
                if (!System.IO.File.Exists(path)) path = System.IO.Path.Combine(root, file);
                if (!System.IO.File.Exists(path)) { Go.Warn("icon not found: " + path); return null; }
                var tex = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                if (!ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(path))) return null;
                var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                sp.hideFlags = HideFlags.HideAndDontSave;
                Cache[file] = sp;
                return sp;
            }
            catch (System.Exception ex) { Go.Warn($"icon {file}: {ex.Message}"); return null; }
        }
    }
}
