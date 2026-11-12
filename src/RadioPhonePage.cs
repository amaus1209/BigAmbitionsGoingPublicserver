using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BigAmbitionsMP
{
    /// <summary>RADIO-TAB-1 (user 2026-11-01: "build a tab of its own in the phone to choose the radio station"): "Radio" in
    /// the phone's full menu, built the way the Business app is (MPHubNativePage: the top row's own button template
    /// cloned, the page an AppsContainer sibling, so the shell around it is the game's). The page lists the game's
    /// stations, the player's own streams (streams.txt and the playlists beside it) and the server's list
    /// (RADIO-STREAMS-2) in three columns; a click plays the station on the game's radio and marks it. The radio still
    /// sounds only where the game lets it - in a car with a radio, or with headphones - and a rider's radio is the
    /// driver's (PASSENGER-RADIO-1), so for him the page only shows what plays.
    /// RADIO-SHOP-1 / RADIO-ONFOOT-1 (user 2026-11-01): a row of switches under what plays - "radio on foot", and, in a
    /// building whose loudspeakers this player may choose, which of the two a pick tunes: his own radio or the
    /// loudspeakers (opened there, the page starts on the loudspeakers). The song the station sends shows under its name.
    /// RADIO-SORT-1: a third switch puts the streams in alphabetical order (RadioStreams.SortAz).
    /// RADIO-FAV-1 (user 2026-11-01): a heart on every station marks it as a favourite; the favourites stand in a section of
    /// their own at the top (in the order marked, by name with "Alphabetical"), for the car radio and the loudspeakers
    /// alike.
    /// RADIO-SEARCH-1 (user 2026-11-01): a search field at the end of the switches - the lists show only the stations whose
    /// name holds what is typed (upper/lower case and accents alike: "o3" finds "Ö3").</summary>
    internal static class RadioPhonePage
    {
        // the Business page's palette (MPBusinessPage B_ROW, B_FIELDFOC, B_SUB), the same shell behind both
        private static readonly Color RowBg = new Color(118f / 255f, 124f / 255f, 132f / 255f, 0.62f);
        private static readonly Color RowOn = new Color(59f / 255f, 140f / 255f, 240f / 255f, 0.95f);
        private static readonly Color Sub = new Color(1f, 1f, 1f, 0.72f);
        private static readonly Color HeartOn = new Color(1f, 0.32f, 0.42f, 1f);
        private static readonly Color HeartOff = new Color(1f, 1f, 1f, 0.5f);
        private static Sprite? _heartFull, _heartLine;

        private static readonly Dictionary<string, string> LanguageNames = new Dictionary<string, string>
        {
            ["ar"] = "العربية", ["cs"] = "Čeština", ["da"] = "Dansk", ["de"] = "Deutsch", ["el"] = "Ελληνικά", ["en"] = "English",
            ["es"] = "Español", ["fi"] = "Suomi", ["fr"] = "Français", ["hu"] = "Magyar", ["it"] = "Italiano", ["ja"] = "日本語",
            ["ko"] = "한국어", ["lt"] = "Lietuvių", ["nl"] = "Nederlands", ["pl"] = "Polski", ["pt"] = "Português", ["ro"] = "Română",
            ["ru"] = "Русский", ["tr"] = "Türkçe", ["uk"] = "Українська", ["zh-cn"] = "简体中文", ["zh-tw"] = "繁體中文",
        };

        internal static bool Ready { get; private set; }
        internal static bool PageActive => _page != null && _page.activeInHierarchy;

        private static GameObject? _page, _button, _selectedIcon;
        private static TextMeshProUGUI? _appNameLbl, _status;
        private static Behaviour? _appNameLoc;   // the native localization on the menu's title
        private static TMP_FontAsset? _font;
        private static RectTransform? _list;
        private static ScrollRect? _scroll;
        private static readonly List<(string key, Image bg)> _rows = new List<(string key, Image bg)>();
        private static string _builtSig = "";
        private static float _nextTryAt, _graceUntil, _nextRefresh;
        private static int _tries;
        private static bool _wasActive;

        private sealed class Switch { public GameObject Go = null!; public Image Bg = null!; public TextMeshProUGUI Label = null!; }
        private static Switch? _onFootSw, _sortSw, _ownSw, _shopSw;
        private static TMP_InputField? _search;
        private static string _query = "";
        private static bool _toTop;
        private static bool _shopTarget;   // a pick tunes the loudspeakers here, not the player's own radio

        /// <summary>Scene reset: the page died with the menu (MPCanvasUI, with the Business app's reset).</summary>
        internal static void Reset()
        {
            Ready = false;
            _page = _button = _selectedIcon = null;
            _appNameLbl = _status = null; _appNameLoc = null; _font = null;
            _list = null; _scroll = null; _rows.Clear(); _builtSig = "";
            _tries = 0; _nextTryAt = 0f; _graceUntil = 0f; _wasActive = false;
            _onFootSw = _sortSw = _ownSw = _shopSw = null; _shopTarget = false; _search = null;
        }

        /// <summary>MAIN THREAD, every frame (MPCanvasUI, next to the Business app's tick) - single player too.</summary>
        internal static void Tick()
        {
            try
            {
                if (MPConfig.DedicatedHost) return;   // nobody listens on a dedicated server
                RadioStreams.TickOnFoot();   // RADIO-ONFOOT-1
                ShopRadio.Tick();            // RADIO-SHOP-1
                if (!Ready) { TryInject(); return; }
                if (_page == null) { Reset(); return; }   // the scene died
                bool active = PageActive;
                if (active)
                {
                    // hold the menu's title while the page is up (the native localization would write its own)
                    if (_appNameLoc != null && _appNameLoc.enabled) _appNameLoc.enabled = false;
                    string title = Loc.T("gps_app_radio", "Radio");
                    if (_appNameLbl != null && _appNameLbl.text != title) _appNameLbl.text = title;
                    if (_selectedIcon != null && !_selectedIcon.activeSelf) _selectedIcon.SetActive(true);
                    if (Time.unscaledTime >= _nextRefresh) { _nextRefresh = Time.unscaledTime + 1f; Refresh(); }
                }
                else if (_wasActive) Release();
                _wasActive = active;
            }
            catch { }
        }

        /// <summary>PHONE-PAGES-1: the phone's Radio app - the full menu open, on this page.</summary>
        internal static void OpenFromPhone()
        {
            try
            {
                if (!Ready || _page == null || !MPHubNativePage.OpenMenu()) return;
                ShowPage();
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Radio] open from the phone: {ex.Message}"); }
        }

        /// <summary>A native app was selected (MPPatches' FullMenu.ShowApp postfix): the page steps aside.</summary>
        internal static void HidePage()
        {
            try
            {
                if (_page != null && _page.activeSelf) _page.SetActive(false);
                if (_wasActive) Release();
                _wasActive = false;
            }
            catch { }
        }

        private static void Release()
        {
            if (_selectedIcon != null) _selectedIcon.SetActive(false);
            if (_appNameLoc != null && !MPHubNativePage.PageActive) _appNameLoc.enabled = true;   // the native title again
        }

        private static void TryInject()
        {
            // never during loading (the Business app's lesson: a clone in the row before FullMenu's own start-up ran over it)
            if (TimeSync.IsStartupHeld || MPCanvasUI.IsLoadingOverlayUp()) { _graceUntil = 0f; return; }
            if (_graceUntil == 0f) { _graceUntil = Time.unscaledTime + 5f; return; }
            if (Time.unscaledTime < _graceUntil) return;
            if (_tries >= 30 || Time.unscaledTime < _nextTryAt) return;
            _nextTryAt = Time.unscaledTime + 2f;
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
                    Plugin.Logger.LogWarning("[RadioTab] FullMenu paths not found (canvas/row/container/template).");
                    return;
                }
                var appName = canvasT.Find("Topbar/AppName");
                _appNameLbl = appName != null ? appName.GetComponent<TextMeshProUGUI>() : null;
                if (appName != null)
                    foreach (var b in appName.GetComponents<Behaviour>())
                        if (b != null && b.GetType().Name.Contains("Localization")) { _appNameLoc = b; break; }

                // the top-row button: a clone of the game's own template
                _button = UnityEngine.Object.Instantiate(template.gameObject, row);
                _button.name = "BAMP_Radio";
                foreach (var c in _button.GetComponents<Component>())
                    if (c != null && c.GetType().Name == "FullMenuAppButton") UnityEngine.Object.Destroy(c);
                foreach (var b in _button.GetComponentsInChildren<Behaviour>(true))
                    if (b != null && b.GetType().Name.Contains("Localization")) b.enabled = false;
                var titleT = _button.transform.Find("Title");
                var titleLbl = titleT != null ? titleT.GetComponent<TextMeshProUGUI>() : null;
                if (titleLbl != null) { titleLbl.text = Loc.T("gps_app_radio", "Radio"); _font = titleLbl.font; }
                var logoT = _button.transform.Find("Logo");
                var logoImg = logoT != null ? logoT.GetComponent<Image>() : null;
                var icon = MPPhoneButton.LoadIconFile("BAMP_RadioIcon.png");
                if (logoImg != null && icon != null) { logoImg.sprite = icon; logoImg.color = Color.white; logoImg.preserveAspect = true; }
                _selectedIcon = _button.transform.Find("SelectedIcon")?.gameObject;
                if (_selectedIcon != null) _selectedIcon.SetActive(false);
                var btn = _button.GetComponent<Button>();
                if (btn != null)
                {
                    btn.interactable = true; btn.enabled = true;   // a clone inherits the template's state; keep ours live
                    btn.onClick = new Button.ButtonClickedEvent();
                    btn.onClick.AddListener((UnityAction)ShowPage);
                }
                _button.SetActive(true);

                // the page: an AppsContainer sibling, content in the Business page's 1920x875 space at scale 2
                _page = new GameObject("BAMP_RadioApp", typeof(RectTransform), typeof(CanvasGroup));
                _page.transform.SetParent(container, false);
                Stretch((RectTransform)_page.transform);
                var root = new GameObject("Root", typeof(RectTransform));
                root.transform.SetParent(_page.transform, false);
                var rrt = (RectTransform)root.transform;
                rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
                rrt.sizeDelta = new Vector2(1920f, 875f);
                rrt.localScale = new Vector3(2f, 2f, 1f);
                Build(rrt);
                _page.SetActive(false);
                Ready = true;
                Plugin.Logger.LogInfo("[RadioTab] injected: top-row button + page.");
            }
            catch (System.Exception ex) { Plugin.Logger.LogWarning($"[RadioTab] inject: {ex.Message}"); }
        }

        private static void Build(RectTransform root)
        {
            _status = Text(root, "", 26f, FontStyles.Bold, Color.white);
            var srt = _status.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f); srt.pivot = new Vector2(0.5f, 1f);
            srt.offsetMin = new Vector2(60f, -136f); srt.offsetMax = new Vector2(-60f, -20f);   // what plays, its song, a hint

            var bar = new GameObject("Switches", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bar.transform.SetParent(root, false);
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(0.5f, 1f);
            brt.offsetMin = new Vector2(60f, -206f); brt.offsetMax = new Vector2(-60f, -150f);
            var h = bar.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 12f; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = true;
            _onFootSw = MakeSwitch(bar.transform, 400f, ToggleOnFoot);
            _sortSw = MakeSwitch(bar.transform, 360f, ToggleSort);
            _ownSw = MakeSwitch(bar.transform, 300f, () => SetTarget(false));
            _shopSw = MakeSwitch(bar.transform, 360f, () => SetTarget(true));
            MakeSearch(bar.transform);   // RADIO-SEARCH-1

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(root, false);
            var sct = (RectTransform)scrollGo.transform;
            sct.anchorMin = Vector2.zero; sct.anchorMax = Vector2.one;
            sct.offsetMin = new Vector2(60f, 30f); sct.offsetMax = new Vector2(-60f, -222f);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);   // takes the wheel and drags between rows

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollGo.transform, false);
            Stretch((RectTransform)viewport.transform);

            var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            list.transform.SetParent(viewport.transform, false);
            _list = (RectTransform)list.transform;
            _list.anchorMin = new Vector2(0f, 1f); _list.anchorMax = new Vector2(1f, 1f); _list.pivot = new Vector2(0.5f, 1f);
            _list.sizeDelta = Vector2.zero;
            var vlg = list.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f; vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            list.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.viewport = (RectTransform)viewport.transform;
            _scroll.content = _list;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 45f;
        }

        private static void ShowPage()
        {
            try
            {
                if (_page == null) return;
                var container = _page.transform.parent;
                for (int i = 0; i < container.childCount; i++)
                {
                    var ch = container.GetChild(i).gameObject;
                    if (ch != _page && ch.activeSelf) ch.SetActive(false);
                }
                MPHubNativePage.HidePage();   // the Business app's dot and title
                var row = _button != null ? _button.transform.parent : null;
                if (row != null)
                    for (int i = 0; i < row.childCount; i++)
                    {
                        var ch = row.GetChild(i);
                        if (ch.gameObject == _button) continue;
                        var dot = ch.Find("SelectedIcon");
                        if (dot != null && dot.gameObject.activeSelf) dot.gameObject.SetActive(false);
                    }
                RadioStreams.Load();   // the files as they are now
                _shopTarget = ShopRadio.CanChooseHere(out _);   // opened in one's own shop: its loudspeakers
                _builtSig = "";
                _nextRefresh = 0f;
                _page.SetActive(true);
                if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
            }
            catch (System.Exception ex) { Plugin.Logger.LogWarning($"[RadioTab] show: {ex.Message}"); }
        }

        /// <summary>Once a second while the page is up: the rows (rebuilt when the list changed) and the mark.</summary>
        private static void Refresh()
        {
            var rp = InstanceBehavior<GameManager>.Instance?.radioPlayer;
            if (rp == null || _list == null || _status == null) return;
            if (RadioStreams.NewListWaiting) RadioStreams.Load();   // GitHub's list just landed
            var streams = RadioStreams.Stations;
            var games = new List<RadioStation>();
            foreach (RadioStation st in System.Enum.GetValues(typeof(RadioStation)))
                try { if (rp.GetRadioStationData(st).HasPlayableClips) games.Add(st); } catch { }
            string lang = RadioStreams.ServerListLanguage;

            var sig = new System.Text.StringBuilder(lang);
            foreach (var st in games) sig.Append('|').Append((int)st);
            foreach (var s in streams) sig.Append('|').Append(s.Url);
            sig.Append(RadioStreams.SortAz ? "|az" : "").Append("|fav:").Append(string.Join(",", RadioStreams.Favorites));   // RADIO-FAV-1
            sig.Append("|q:").Append(_query);   // RADIO-SEARCH-1
            if (sig.ToString() != _builtSig)
            {
                _builtSig = sig.ToString();
                var keep = _list.anchoredPosition;   // a heart pressed down in the list: the list stays where it was
                Rebuild(rp, games, streams, lang);
                _list.anchoredPosition = keep;
                if (_toTop && _scroll != null) _scroll.verticalNormalizedPosition = 1f;   // a new search starts at the top
                _toTop = false;
            }

            // RADIO-SHOP-1: in a building whose loudspeakers this player may choose, a pick tunes them or his own radio
            bool shopOk = ShopRadio.CanChooseHere(out _);
            if (!shopOk) _shopTarget = false;
            bool onFoot = RadioStreams.OnFoot;
            Paint(_onFootSw, Loc.T("gps_radio_onfoot", "Radio on foot: {0}", onFoot ? Loc.T("gps_radio_on", "on") : Loc.T("gps_radio_off", "off")), onFoot, true);
            bool az = RadioStreams.SortAz;
            Paint(_sortSw, Loc.T("gps_radio_sort", "Alphabetical: {0}", az ? Loc.T("gps_radio_on", "on") : Loc.T("gps_radio_off", "off")), az, true);
            Paint(_ownSw, Loc.T("gps_radio_target_own", "My radio"), !_shopTarget, shopOk);
            Paint(_shopSw, Loc.T("gps_radio_target_shop", "Shop loudspeakers"), _shopTarget, shopOk);

            string playing, now, song, hint;
            if (_shopTarget && ShopRadio.NowHere(out var sst, out var sname, out var surl))
            {
                playing = surl.Length > 0 ? "url:" + surl : "game:" + (int)sst;
                now = Loc.T("gps_radio_now_shop", "Shop loudspeakers: {0}", surl.Length > 0 ? sname : RadioStreams.GameName(sst));
                song = surl.Length > 0 ? ShopRadio.TitleHere : "";
                hint = ShopRadio.ProblemHere.Length > 0 ? ShopRadio.ProblemHere   // RADIO-FORMAT-1
                     : ShopRadio.MutedHere ? Loc.T("gps_radio_shop_off", "The loudspeakers are off - switch them on at a loudspeaker.")
                     : Loc.T("gps_radio_shop_all", "Everybody in this shop hears it, and the server keeps it.");
            }
            else
            {
                playing = RadioStreams.Active ? "url:" + RadioStreams.CurrentUrl : "game:" + (int)rp.GetCurrentStation();
                now = Loc.T("gps_radio_now", "Playing: {0}", RadioStreams.Active ? RadioStreams.CurrentName : RadioStreams.GameName(rp.GetCurrentStation()));
                song = RadioStreams.Active && !rp.IsMuted ? RadioStreams.CurrentTitle : "";
                bool inside = false;
                try { inside = BuildingManager.IsInsideBuilding; } catch { }
                hint = PassengerRide.IsSeated ? Loc.T("gps_radio_rider", "You are riding along - the driver picks the station.")
                     : RadioStreams.Active && RadioStreams.Problem.Length > 0 ? RadioStreams.Problem   // RADIO-FORMAT-1
                     : !rp.IsMuted ? ""
                     : onFoot && inside ? Loc.T("gps_radio_muted_inside", "Inside a building your radio is silent (not with headphones) - the loudspeakers play here.")
                     : Loc.T("gps_radio_muted", "You hear it in a car with a radio, with headphones, or with \"Radio on foot\".");
            }
            foreach (var (key, bg) in _rows) if (bg != null) bg.color = key == playing ? RowOn : RowBg;
            if (hint.Length == 0 && lang.Length > 0 && !HasServerStations(streams))
                hint = RadioStreams.ListUnreachable ? Loc.T("gps_radio_unreachable", "The server's station list can't be reached right now.")
                                                    : Loc.T("gps_radio_loading", "Loading the server's station list ...");
            _status.text = now
                + (song.Length > 0 ? "\n<size=85%><color=#FFFFFFE0>" + song + "</color></size>" : "")
                + (hint.Length > 0 ? "\n<size=80%><color=#FFFFFFB8>" + hint + "</color></size>" : "");
        }

        /// <summary>RADIO-SEARCH-1: the search field, taking the rest of the switches' row.</summary>
        private static void MakeSearch(Transform parent)
        {
            var go = new GameObject("Search", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minWidth = 260f; le.flexibleWidth = 1f;
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.35f);
            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            vp.transform.SetParent(go.transform, false);
            var vrt = (RectTransform)vp.transform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(16f, 4f); vrt.offsetMax = new Vector2(-16f, -4f);
            var text = Text(vp.transform, "", 22f, FontStyles.Normal, Color.white);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var ph = Text(vp.transform, Loc.T("gps_radio_search", "Search a station ..."), 22f, FontStyles.Italic, Sub);
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            ph.textWrappingMode = TextWrappingModes.NoWrap;
            _search = go.AddComponent<TMP_InputField>();
            _search.textViewport = vrt; _search.textComponent = text; _search.placeholder = ph; _search.targetGraphic = bg;
            _search.lineType = TMP_InputField.LineType.SingleLine; _search.characterLimit = 40;
            _search.caretWidth = 2; _search.customCaretColor = true; _search.caretColor = Color.white;
            _search.text = _query;
            _search.onValueChanged.AddListener(v => { _query = (v ?? "").Trim(); _toTop = true; _nextRefresh = 0f; });
            // GO-INPUT-1: on the game's UI layer, so the game knows it is typed in (GameManager.HasInputSelected) and keeps
            // its shortcuts out of the station search
            int ui = LayerMask.NameToLayer("UI");
            if (ui < 0) ui = 5;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ui;
        }

        /// <summary>RADIO-SEARCH-1: the station's name holds the search (case and accents alike), or there is none.</summary>
        private static bool Match(string name) =>
            _query.Length == 0 || CultureInfo.InvariantCulture.CompareInfo.IndexOf(name, _query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

        private static Switch MakeSwitch(Transform parent, float width, UnityAction click)
        {
            var go = new GameObject("Switch", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = width;
            var bg = go.GetComponent<Image>();
            bg.color = RowBg;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(click);
            var t = Text(go.transform, "", 22f, FontStyles.Normal, Color.white);
            t.rectTransform.offsetMin = new Vector2(14f, 0f); t.rectTransform.offsetMax = new Vector2(-14f, 0f);
            t.alignment = TextAlignmentOptions.Center;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return new Switch { Go = go, Bg = bg, Label = t };
        }

        private static void Paint(Switch? s, string text, bool on, bool visible)
        {
            if (s == null || s.Go == null) return;
            if (s.Go.activeSelf != visible) s.Go.SetActive(visible);
            if (s.Label.text != text) s.Label.text = text;
            s.Bg.color = on ? RowOn : RowBg;
        }

        private static void ToggleOnFoot()
        {
            RadioStreams.OnFoot = !RadioStreams.OnFoot;
            _nextRefresh = 0f;
        }

        private static void ToggleSort()
        {
            RadioStreams.SortAz = !RadioStreams.SortAz;   // re-reads the list; the rows follow at the refresh (their order is in the signature)
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
            _nextRefresh = 0f;
        }

        private static void SetTarget(bool shop)
        {
            _shopTarget = shop && ShopRadio.CanChooseHere(out _);
            _nextRefresh = 0f;
        }

        private static bool HasServerStations(IReadOnlyList<RadioStreams.Station> streams)
        {
            foreach (var s in streams) if (s.Server) return true;
            return false;
        }

        private static void Rebuild(RadioPlayer rp, List<RadioStation> games, IReadOnlyList<RadioStreams.Station> streams, string lang)
        {
            for (int i = _list!.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_list.GetChild(i).gameObject);
            _rows.Clear();
            int shown = 0;
            // RADIO-FAV-1: the favourites first - a station no longer in the lists waits (it comes back with them)
            var favs = RadioStreams.ResolvedFavorites(rp).FindAll(f => Match(f.Name));
            if (favs.Count > 0)
            {
                var fav = Section(Loc.T("gps_radio_favorites", "Favourites"));
                foreach (var f in favs)
                {
                    var ff = f;
                    Row(fav, ff.Key, ff.Name, ff.Game ? (UnityAction)(() => PickGame(ff.Station)) : () => PickUrl(ff.Name, ff.Url));
                    shown++;
                }
            }
            else if (_query.Length == 0)
                Text(_list, Loc.T("gps_radio_fav_hint", "Tap a station's heart to put it up here as a favourite."), 20f, FontStyles.Normal, Sub)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            Transform? grid = null, own = null, server = null;
            foreach (var st in games)
            {
                var s = st;
                string name = RadioStreams.GameName(s);
                if (!Match(name)) continue;
                Row(grid ??= Section(Loc.T("gps_radio_game", "The game's stations")), "game:" + (int)s, name, () => PickGame(s));
                shown++;
            }
            foreach (var s in streams)
            {
                var st = s;
                if (!Match(st.Name)) continue;
                Transform host;
                if (st.Server) host = server ??= Section(Loc.T("gps_radio_server", "The server's station list ({0})", LanguageNames.TryGetValue(lang, out var ln) ? ln : lang));
                else host = own ??= Section(Loc.T("gps_radio_own", "Your own streams (streams.txt)"));
                Row(host, "url:" + st.Url, st.Name, () => PickStream(st));
                shown++;
            }
            if (_query.Length > 0 && shown == 0)
                Text(_list, Loc.T("gps_radio_search_none", "No station found for \"{0}\".", _query), 22f, FontStyles.Normal, Sub)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            else if (streams.Count == 0)
                Text(_list, Loc.T("gps_radio_none", "No streams yet. Add some to streams.txt in the game's Radio folder."), 22f, FontStyles.Normal, Sub)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
        }

        private static Transform Section(string title)
        {
            var head = Text(_list!, title, 24f, FontStyles.Bold, Sub);
            head.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;
            head.alignment = TextAlignmentOptions.BottomLeft;
            var go = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            go.transform.SetParent(_list, false);
            var g = go.GetComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(592f, 60f);
            g.spacing = new Vector2(12f, 10f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 3;
            return go.transform;
        }

        private static void Row(Transform parent, string key, string name, UnityAction pick)
        {
            var go = new GameObject("Station", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var bg = go.GetComponent<Image>();
            bg.color = RowBg;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(pick);
            var t = Text(go.transform, name, 24f, FontStyles.Normal, Color.white);
            t.rectTransform.offsetMin = new Vector2(20f, 0f); t.rectTransform.offsetMax = new Vector2(-64f, 0f);   // room for the heart
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            _rows.Add((key, bg));

            // RADIO-FAV-1: the heart on the right marks a favourite (its own button: the row still plays the station)
            var hgo = new GameObject("Favorite", typeof(RectTransform), typeof(Image), typeof(Button));
            hgo.transform.SetParent(go.transform, false);
            var hrt = (RectTransform)hgo.transform;
            hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(1f, 0.5f);
            hrt.sizeDelta = new Vector2(44f, 44f);
            hrt.anchoredPosition = new Vector2(-10f, 0f);
            bool fav = RadioStreams.IsFavorite(key);
            var himg = hgo.GetComponent<Image>();
            himg.sprite = Heart(fav);
            himg.color = fav ? HeartOn : HeartOff;
            himg.preserveAspect = true;
            var hb = hgo.GetComponent<Button>();
            hb.targetGraphic = himg;
            hb.onClick.AddListener(() => ToggleFav(key));
        }

        private static void ToggleFav(string key)
        {
            RadioStreams.ToggleFavorite(key);
            _builtSig = "";   // the favourites section and the hearts again
            _nextRefresh = 0f;
        }

        /// <summary>RADIO-FAV-1: the heart, drawn here (the game has no heart picture to borrow) - filled, or an outline.</summary>
        private static Sprite Heart(bool filled)
        {
            var have = filled ? _heartFull : _heartLine;
            if (have != null) return have;   // Unity's null: also after the scene's unused assets went
            const int S = 64, SS = 4;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "BAMP_Heart" };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float fx = ((x + (sx + 0.5f) / SS) / S - 0.5f) * 2.7f, fy = ((y + (sy + 0.5f) / SS) / S - 0.5f) * 2.7f + 0.12f;
                            if (InHeart(fx, fy) && (filled || !InHeart(fx / 0.7f, (fy - 0.1f) / 0.7f + 0.1f))) hit++;
                        }
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(255 * hit / (SS * SS)));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            tex.hideFlags = HideFlags.HideAndDontSave;   // as the mod's icons (MPPhoneButton.LoadIconFile)
            var sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.HideAndDontSave;
            if (filled) _heartFull = sp; else _heartLine = sp;
            return sp;
        }

        /// <summary>The heart curve (x^2 + y^2 - 1)^3 - x^2 y^3 = 0 and what lies inside it.</summary>
        private static bool InHeart(float x, float y)
        {
            float a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }

        private static TextMeshProUGUI Text(Transform parent, string text, float size, FontStyles style, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
            t.raycastTarget = false;
            Stretch(t.rectTransform);
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }


        private static void PickGame(RadioStation st)
        {
            if (_shopTarget)
            {
                ShopRadio.PickGame(st);
                Plugin.Logger.LogInfo($"[RadioTab] picked the game's station {st} for the loudspeakers here.");
                _nextRefresh = 0f;
                return;
            }
            var rp = InstanceBehavior<GameManager>.Instance?.radioPlayer;
            if (rp == null || PassengerRide.IsSeated) return;   // a rider's radio is the driver's
            if (RadioStreams.Active) RadioStreams.End(rp, st); else rp.PlayStation(st);
            PlayerPrefSettings.RadioStation = (int)st;
            Plugin.Logger.LogInfo($"[RadioTab] picked the game's station {st}.");
            _nextRefresh = 0f;
        }

        private static void PickUrl(string name, string url)
        {
            if (_shopTarget) { ShopRadio.PickStream(name, url); _nextRefresh = 0f; return; }
            var rp = InstanceBehavior<GameManager>.Instance?.radioPlayer;
            if (rp == null || PassengerRide.IsSeated) return;
            RadioStreams.Resume(rp, name, url);
            _nextRefresh = 0f;
        }

        private static void PickStream(RadioStreams.Station s)
        {
            if (_shopTarget) { ShopRadio.PickStream(s.Name, s.Url); _nextRefresh = 0f; return; }
            var rp = InstanceBehavior<GameManager>.Instance?.radioPlayer;
            if (rp == null || PassengerRide.IsSeated) return;
            RadioStreams.PlayListed(rp, s);
            _nextRefresh = 0f;
        }
    }
}
