using System.Collections.Generic;
using Helpers;
using Streets;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BigAmbitionsMP
{
    /// <summary>NAV-DEVICE-1 (user 2026-11-01: "instead of the route arrow, show it like a navigation device - the route on the
    /// piece of the map you are driving through"), NAV-DEVICE-3 ("make it look like a navigation app, like the photo"),
    /// NAV-DEVICE-4 ("change the look so it does not remind so much of Google Maps" - in the game's own UI: the slate of its
    /// panels with their amber caps headings, the city map's near-black and its green player marker): while a destination
    /// is set (Shift+F11, the self-driving too):
    ///   * the top panel: the next turn's arrow on a badge in the route's colour, the distance to it and the street it turns
    ///     into in caps ("TOWARDS 12TH STREET"; no turn within 600 m: straight on and the street the player is in); in its
    ///     corner "THEN" with the turn after it when that one follows within 250 m;
    ///   * the map: the streets around the player (the game's traffic lanes), turned with his heading and zooming out with
    ///     speed, the route with white chevrons pointing the way - in blue, on foot in orange (user 2026-11-01: "in the car
    ///     blue, on foot orange") -, the destination as the yellow diamond the street view marks it with, the street's name
    ///     in caps at the turn, the player as the city map's green marker;
    ///   * the bottom panel: the way done as a bar like the music player's, the speed, the time left and "distance - arrival"
    ///     on the game's clock (one real second is one game minute; a car is reckoned at 33 km/h, a walk at the player's own
    ///     pace - 3.5 m/s, his jog, while he stands), and an X that ends the navigation.
    /// Where it stands: Place (NAV-DEVICE-2). It has a canvas of its own, sorted just above the phone's - so it can lie in
    /// the phone - and it steps aside for the phone's full menu, the city map (which still draws the whole route) and
    /// buildings. The street names come from the house doors next to the route (their addresses).</summary>
    internal sealed class NavDevice
    {
        private const float MeshRadius = 420f, RebuildAfter = 120f, RoadWidth = 3.6f;
        private const float CarMps = 33f / 3.6f, JogMps = 3.5f;   // a drive with its stops; on foot the player jogs by default
        private static readonly Color32 Panel = new Color32(60, 68, 80, 246);   // the game's panels (its map filter)
        private static readonly Color32 Amber = new Color32(230, 174, 97, 255);  // their caps headings
        private const string AmberHex = "#E6AE61", SubHex = "#C4CBD4";
        private static readonly Color32 MapBg = new Color32(11, 15, 18, 255);   // the city map's near-black
        private static readonly Color32 Road = new Color32(47, 55, 66, 255);
        private static readonly Color32 Casing = new Color32(4, 6, 8, 215);
        private static readonly Color32 Route = new Color32(31, 205, 235, 255);
        private static readonly Color32 FootRoute = new Color32(255, 146, 32, 255);
        private static readonly Color32 Chevron = new Color32(255, 255, 255, 215);
        private static readonly Color32 Me = new Color32(16, 199, 39, 255);   // the city map's player marker
        private static readonly Color32 Shade = new Color32(0, 0, 0, 80);
        private static readonly Color32 DestFill = new Color32(255, 217, 64, 255);   // the street view's destination pin
        private static readonly Color32 DestRim = new Color32(0, 0, 0, 170);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Plate = new Color32(6, 8, 10, 210);
        private static readonly Color32 Track = new Color32(28, 32, 39, 255);
        private static readonly Color32 Divider = new Color32(255, 255, 255, 38);
        private static readonly Color32 Button = new Color32(40, 46, 56, 255);

        private readonly RectTransform _root, _view, _content, _banner, _badge, _chip, _bubble, _bar, _track, _fill, _knob, _divider, _close;
        private readonly Canvas _canvas;
        private readonly CanvasGroup _group;   // NAV-PHONE-2: see-through over the phone's apps
        private const float PhoneGrace = 0.3f;   // NAV-PHONE-2: a frame without the phone's rects keeps the last place
        private bool _inPhone;
        private float _phoneAt = -1f;
        private UnityEngine.Rect _phoneSpot;
        private int _phoneOrder = 100;
        private readonly NavMeshGraphic _roads, _route, _marker;
        private readonly Image _turnIcon, _chipIcon, _badgeImg, _fillImg;
        private readonly TextMeshProUGUI _bannerText, _chipText, _bubbleText, _speedText, _barText, _closeText;
        private readonly List<int> _segs = new List<int>();
        private readonly Turn[] _turns = new Turn[2];
        private Vector3 _origin;
        private bool _built;
        private float _routeAt, _zoom = 200f, _zoomWant = 200f, _heading, _w = -1f, _h = -1f, _trackW, _total;
        private int _totalOf = -1;
        private static Sprite? _arrowTurn, _arrowStraight, _disc;
        private static readonly Dictionary<int, Sprite> _rounds = new Dictionary<int, Sprite>();

        private struct Turn { public float Dist; public int Side, Index; }

        internal NavDevice(Transform parent, TMP_FontAsset? font)
        {
            _root = Rect("BAMP_NavDevice", parent);
            _root.anchorMin = _root.anchorMax = Vector2.zero;
            _root.pivot = new Vector2(1f, 1f);
            _canvas = _root.gameObject.AddComponent<Canvas>();
            _root.gameObject.AddComponent<GraphicRaycaster>();   // the X
            _group = _root.gameObject.AddComponent<CanvasGroup>();

            _view = Rect("Map", _root);
            Stretch(_view, 0f);
            _view.gameObject.AddComponent<RectMask2D>();
            Img(_view, MapBg, RoundSprite(6));
            _content = Rect("Content", _view);
            _content.anchorMin = _content.anchorMax = _content.pivot = new Vector2(0.5f, 0.5f);
            _content.sizeDelta = Vector2.zero;
            // in metres, as large as the mesh: RectMask2D culls a whole graphic whose RECT lies outside the map (it does not
            // look at the mesh) - a zero rect at the mesh's origin vanished once the player had walked ~45 m from it (2026-11-01)
            _roads = Mesh("Roads", _content, MeshRadius * 2.4f);
            _route = Mesh("Route", _content, MeshRadius * 2.4f);
            _marker = Mesh("Me", _view, 0f);
            Stretch((RectTransform)_marker.transform, 0f);

            _bubble = Rect("Street", _view);
            _bubble.anchorMin = _bubble.anchorMax = new Vector2(0.5f, 0.5f);
            _bubble.pivot = new Vector2(0.5f, 0f);
            Img(_bubble, Plate, RoundSprite(3));
            _bubbleText = Text("Text", _bubble, font, TextAlignmentOptions.Center, White);
            Stretch(_bubbleText.rectTransform, 0f);

            // the top panel: the turn's arrow on a badge, the distance and the street; "THEN" and the turn after it in its corner
            _banner = Rect("Banner", _root);
            _banner.anchorMin = new Vector2(0f, 1f); _banner.anchorMax = new Vector2(1f, 1f); _banner.pivot = new Vector2(0.5f, 1f);
            Img(_banner, Panel, RoundSprite(6));
            _badge = Rect("Badge", _banner);
            _badgeImg = Img(_badge, Route, RoundSprite(6));
            _turnIcon = Icon("Arrow", _badge);
            _bannerText = Text("Text", _banner, font, TextAlignmentOptions.MidlineLeft, White);
            _chip = Rect("Then", _banner);
            _chipText = Text("Text", _chip, font, TextAlignmentOptions.MidlineRight, Amber);
            _chipIcon = Icon("Arrow", _chip);

            // the bottom panel: the way done, the speed, the time left, the X
            _bar = Rect("Bar", _root);
            _bar.anchorMin = new Vector2(0f, 0f); _bar.anchorMax = new Vector2(1f, 0f); _bar.pivot = new Vector2(0.5f, 0f);
            Img(_bar, Panel, RoundSprite(6));
            _track = Rect("Track", _bar);
            Img(_track, Track, null);
            _fill = Rect("Done", _track);
            _fill.anchorMin = _fill.anchorMax = _fill.pivot = new Vector2(0f, 0.5f);
            _fillImg = Img(_fill, Route, null);
            _knob = Rect("Knob", _track);
            _knob.anchorMin = _knob.anchorMax = new Vector2(0f, 0.5f); _knob.pivot = new Vector2(0.5f, 0.5f);
            Img(_knob, White, DiscSprite());
            _speedText = Text("Speed", _bar, font, TextAlignmentOptions.Center, White);
            _divider = Rect("Divider", _bar);
            Img(_divider, Divider, null);
            _barText = Text("Text", _bar, font, TextAlignmentOptions.MidlineLeft, White);
            _close = Rect("Close", _bar);
            var closeImg = Img(_close, Button, RoundSprite(5));
            closeImg.raycastTarget = true;
            var btn = _close.gameObject.AddComponent<Button>();
            btn.targetGraphic = closeImg;
            btn.onClick.AddListener((UnityAction)EndNavigation);
            _closeText = Text("X", _close, font, TextAlignmentOptions.Center, White);
            Stretch(_closeText.rectTransform, 0f);
            _closeText.text = "<b>X</b>";

            _root.gameObject.SetActive(false);
        }

        internal void Hide() { if (_root.gameObject.activeSelf) _root.gameObject.SetActive(false); }

        private static void EndNavigation()
        {
            Plugin.Logger.LogInfo("[Nav] navigation ended from the device's X.");
            Navigator.Clear();
            Navigator.Notice(Loc.T("gps_nav_dev_ended", "Navigation ended."));
        }

        /// <summary>Main thread, every frame while a destination is set and the street view shows.</summary>
        internal void Show(Vector3 me, float speedKmh)
        {
            bool menu = false;
            try { menu = UI.Smartphone.FullMenu.IsOpen; } catch { }
            if (menu || !Place()) { Hide(); return; }
            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);
            SeeThrough();
            bool car = Navigator.RouteCar;
            Color32 accent = car ? Route : FootRoute;
            Tint(_badgeImg, accent);
            Tint(_fillImg, accent);

            // heading up: from the car (or the player) - smoothed, so a wobble does not shake the map
            float yaw = _heading;
            try
            {
                var fwd = car && Navigator.InCar() ? InstanceBehavior<GameManager>.Instance.selectedVehicle.transform.forward : PlayerHelper.PlayerController.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.01f) yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            }
            catch { }
            _heading = Mathf.LerpAngle(_heading, yaw, Mathf.Clamp01(Time.unscaledDeltaTime * 6f));

            // zoom with speed: metres across the map
            float want = !car ? 90f : speedKmh < 25f ? 180f : speedKmh < 55f ? 260f : 360f;
            _zoomWant = want;
            _zoom = Mathf.Lerp(_zoom, want, Mathf.Clamp01(Time.unscaledDeltaTime * 1.5f));
            float w = _view.rect.width, h = _view.rect.height;
            float s = w / Mathf.Max(30f, _zoom);

            if (!_built || Navigator.Flat(me, _origin) > RebuildAfter) BuildRoads(me);
            int turns = FindTurns(me, _turns);
            if (Time.unscaledTime >= _routeAt) { _routeAt = Time.unscaledTime + 0.25f; BuildRoute(me, s); }

            // the map: the player at the puck, heading up, scaled to the zoom
            float bannerH = _banner.rect.height, barH = _bar.rect.height;
            var puckAt = new Vector2(0f, h * 0.5f - (bannerH + (h - bannerH - barH) * 0.74f));
            var rot = Quaternion.Euler(0f, 0f, _heading);
            Vector2 turned = rot * (new Vector2(me.x - _origin.x, me.z - _origin.z) * s);
            _content.localScale = new Vector3(s, s, 1f);
            _content.localRotation = rot;
            _content.anchoredPosition = puckAt - turned;
            Marker(puckAt, w, rot, s);

            // the top panel, the bubble
            if (turns > 0)
            {
                var t = _turns[0];
                bool now = t.Dist < (car ? 18f : 6f);
                string street = StreetAfter(t.Index);
                string dist = now ? Loc.T("gps_nav_dev_now", "now") : Navigator.Distance(Mathf.Max(10f, Mathf.Round(t.Dist / 10f) * 10f));
                string what = street.Length > 0 ? Loc.T("gps_nav_dev_towards", "towards {0}", street)
                            : t.Side > 0 ? Loc.T("gps_nav_dev_right", "turn right") : Loc.T("gps_nav_dev_left", "turn left");
                SetText(_bannerText, Head(dist, what));
                SetIcon(_turnIcon, false, t.Side);
                if (turns > 1 && _turns[1].Dist - t.Dist < 250f)
                {
                    if (!_chip.gameObject.activeSelf) _chip.gameObject.SetActive(true);
                    SetText(_chipText, Caps(Loc.T("gps_nav_dev_then", "Then:").TrimEnd(':', ' ')));
                    SetIcon(_chipIcon, false, _turns[1].Side);
                }
                else if (_chip.gameObject.activeSelf) _chip.gameObject.SetActive(false);
                PlaceBubble(street, t.Index, rot, s);
            }
            else
            {
                string here = "";
                try { here = AddressHelper.GetStreetNameLocalized(SaveGameManager.Current.CurrentStreetName); } catch { }
                if (here.Length == 0) here = NavStreets.Near(me);   // on foot the game keeps no current street
                SetText(_bannerText, Head(Loc.T("gps_nav_dev_straight", "straight on"), here));
                SetIcon(_turnIcon, true, 1);
                if (_chip.gameObject.activeSelf) _chip.gameObject.SetActive(false);
                if (_bubble.gameObject.activeSelf) _bubble.gameObject.SetActive(false);
            }

            // speed, the way done, time left, arrival
            SetText(_speedText, "<b>" + Mathf.RoundToInt(speedKmh) + "</b>\n<size=48%><color=" + AmberHex + ">" + Caps("km/h") + "</color></size>");
            float left = Remaining(me);
            Progress(left);
            float pace = car ? CarMps : speedKmh > 4f ? Mathf.Clamp(speedKmh / 3.6f, 1.2f, 8f) : JogMps;   // on foot: his own pace
            int minutes = Mathf.Max(1, Mathf.RoundToInt(left / pace));   // one real second = one game minute
            string arrive = "";
            try
            {
                int at = (TimeHelper.CurrentHour * 60 + Mathf.FloorToInt(TimeHelper.CurrentMinute) + minutes) % (24 * 60);
                arrive = $"{at / 60:00}:{at % 60:00}";
            }
            catch { }
            string time = minutes < 60 ? $"{minutes} min" : $"{minutes / 60} h {minutes % 60} min";
            SetText(_barText, "<b>" + time + "</b>\n<size=64%><color=" + SubHex + ">"
                + Navigator.Distance(left) + (arrive.Length > 0 ? "  •  " + arrive : "") + "</color></size>");
        }

        /// <summary>The top panel's text: the distance (or "straight on") large, under it the street in caps.</summary>
        private static string Head(string big, string small) =>
            "<size=150%><b>" + big + "</b></size>" + (small.Length > 0 ? "\n<size=86%><color=" + SubHex + ">" + Caps(small) + "</color></size>" : "");

        /// <summary>Caps with a little room between the letters, as the game writes its headings and the city map its streets.</summary>
        private static string Caps(string s) => "<b><uppercase><cspace=0.06em>" + s + "</cspace></uppercase></b>";

        private static void Tint(Image img, Color32 c)
        {
            Color32 have = img.color;
            if (have.r != c.r || have.g != c.g || have.b != c.b || have.a != c.a) img.color = c;
        }

        /// <summary>The bar of the way done: the way left against the longest seen for this destination (leaving the route
        /// can make the new one longer); a new destination starts it over.</summary>
        private void Progress(float left)
        {
            if (_totalOf != Navigator.DestSerial || _total < 1f) { _totalOf = Navigator.DestSerial; _total = left; }
            if (left > _total) _total = left;
            float x = Mathf.Round(_trackW * (_total > 1f ? Mathf.Clamp01(1f - left / _total) : 0f));
            if (Mathf.Abs(_fill.sizeDelta.x - x) < 0.5f) return;
            _fill.sizeDelta = new Vector2(x, _fill.sizeDelta.y);
            _knob.anchoredPosition = new Vector2(x, 0f);
        }

        // ── where and how big ──

        /// <summary>NAV-PHONE-2 (user 2026-11-02: "no navi above the BizPhone, not while driving or walking by itself either -
        /// only in the phone, also with a waypoint set"; before, it went into the phone only while the self-driving drove or
        /// the character walked there, and the game's click-to-walk drops its path for a frame whenever it plans anew - so the
        /// device jumped between the two places, at 59 FPS it looked doubled): while a destination is set it lies in the phone,
        /// over its app tiles from below its music player down to its bottom edge; a collapsed phone shows none.
        /// The server's admin can switch that off (NAV-PHONE-1, Navigator.InPhone) - then as NAV-DEVICE-2 had it (user
        /// 2026-11-01: "it can be smaller and stand on top of the BizPhone"): right on top of the phone, its right edge on the
        /// phone's, as large as the room up to the game's notifications allows; no room above the phone: beside it; no phone:
        /// low in the right corner. The rects are read off the screen (the overlay counts in pixels). False: not to show.</summary>
        private bool Place()
        {
            int H = Screen.height;
            float gap = Mathf.Round(H * 0.012f), ceiling = H - Mathf.Round(H * 0.16f);
            ReadPhone(out var phone, out var tiles, out var player, out int order, out bool collapsed);
            float w, h;
            Vector2 topRight;
            _inPhone = Navigator.InPhone;
            if (_inPhone)
            {
                if (collapsed) { _phoneAt = -1f; return false; }
                if (tiles.HasValue && phone.HasValue && tiles.Value.width > 120f)
                {
                    // user 2026-11-01: "in the BizPhone it can be longer, down to the bottom edge" - "higher and lower": the phone's
                    // whole screen below its music player (below its title bar when no player shows) - the frame less its bezel at
                    // the sides and the bottom
                    float bezel = Mathf.Round(phone.Value.width * 0.038f);
                    float left = phone.Value.xMin + bezel, right = phone.Value.xMax - bezel, bottom = Mathf.Max(phone.Value.yMin, 0f) + bezel;
                    float top = player.HasValue && player.Value.yMin > tiles.Value.yMax - 4f ? player.Value.yMin - 3f : tiles.Value.yMax;
                    w = Mathf.Round(right - left);
                    h = Mathf.Round(Mathf.Max(120f, top - bottom));
                    topRight = new Vector2(right, top);
                    _phoneSpot = new UnityEngine.Rect(right - w, top - h, w, h);
                    _phoneOrder = order;
                    _phoneAt = Time.unscaledTime;
                }
                else if (_phoneAt >= 0f && Time.unscaledTime - _phoneAt < PhoneGrace)
                {
                    w = _phoneSpot.width; h = _phoneSpot.height;
                    topRight = new Vector2(_phoneSpot.xMax, _phoneSpot.yMax);
                    order = _phoneOrder;
                }
                else return false;
            }
            else if (phone.HasValue)
            {
                float room = ceiling - gap - (phone.Value.yMax + gap);
                w = Mathf.Min(Mathf.Round(H * 0.2f), Mathf.Floor(room / 1.2f));
                if (w >= H * 0.15f) { h = Mathf.Min(room, Mathf.Round(w * 1.35f)); topRight = new Vector2(phone.Value.xMax, phone.Value.yMax + gap + h); }
                else { w = Mathf.Round(H * 0.2f); h = Mathf.Round(w * 1.4f); topRight = new Vector2(phone.Value.xMin - gap, ceiling); }
            }
            else
            {
                w = Mathf.Round(H * 0.2f);
                h = Mathf.Round(w * 1.4f);
                topRight = new Vector2(Screen.width - gap, Mathf.Round(H * 0.1f) + h);
            }
            Resize(w, h);
            if ((_root.anchoredPosition - topRight).sqrMagnitude > 0.25f) _root.anchoredPosition = topRight;
            _canvas.overrideSorting = true;
            if (_canvas.sortingOrder != order) _canvas.sortingOrder = order;   // just above the phone
            return true;
        }

        /// <summary>NAV-PHONE-2: in the phone it covers the phone's apps - with the mouse over it, it fades so they show and take
        /// the clicks (its pictures take none, and faded the X neither); over the X it stays, so the X can end the way.</summary>
        private void SeeThrough()
        {
            bool fade = false;
            if (_inPhone)
            {
                Vector2 m = Input.mousePosition;
                Vector2 tr = _root.anchoredPosition;   // its top right corner (pivot), in pixels
                fade = new UnityEngine.Rect(tr.x - _w, tr.y - _h, _w, _h).Contains(m) && ScreenRect(_close)?.Contains(m) != true;
            }
            float want = fade ? 0.12f : 1f;
            if (_group.alpha != want) _group.alpha = Mathf.MoveTowards(_group.alpha, want, Time.unscaledDeltaTime * 8f);
            if (_group.blocksRaycasts == fade) _group.blocksRaycasts = !fade;
        }

        private void Resize(float w, float h)
        {
            if (Mathf.Abs(w - _w) < 0.5f && Mathf.Abs(h - _h) < 0.5f) return;
            _w = w; _h = h;
            _root.sizeDelta = new Vector2(w, h);
            float bannerH = Mathf.Round(w * 0.28f), barH = Mathf.Round(w * 0.25f), pad = Mathf.Round(w * 0.03f);
            // the top panel: the badge at the left, the text beside it, "THEN" on the text's first line at the right
            _banner.sizeDelta = new Vector2(0f, bannerH);
            _banner.anchoredPosition = Vector2.zero;
            float badge = Mathf.Round(bannerH * 0.7f);
            Place(_badge, new Vector2(0f, 0.5f), new Vector2(pad + badge * 0.5f, 0f), new Vector2(badge, badge));
            Place(_turnIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(badge * 0.74f, badge * 0.74f));
            var bt = _bannerText.rectTransform;
            bt.anchorMin = new Vector2(0f, 0f); bt.anchorMax = new Vector2(1f, 1f);
            bt.offsetMin = new Vector2(pad + badge + Mathf.Round(pad * 1.3f), 2f); bt.offsetMax = new Vector2(-pad, -2f);
            float fs = Mathf.Max(10f, bannerH * 0.19f);
            _bannerText.fontSize = fs;
            float icon = Mathf.Round(fs * 1.35f);
            _chip.anchorMin = _chip.anchorMax = _chip.pivot = new Vector2(1f, 0.5f);
            _chip.sizeDelta = new Vector2(Mathf.Round(w * 0.4f), icon);
            _chip.anchoredPosition = new Vector2(-pad, Mathf.Round(fs * 0.6f));
            var ct = _chipText.rectTransform;
            ct.anchorMin = Vector2.zero; ct.anchorMax = Vector2.one;
            ct.offsetMin = Vector2.zero; ct.offsetMax = new Vector2(-icon - Mathf.Round(fs * 0.3f), 0f);
            _chipText.fontSize = Mathf.Max(8f, fs * 0.78f);
            Place(_chipIcon.rectTransform, new Vector2(1f, 0.5f), new Vector2(-icon * 0.5f, 0f), new Vector2(icon, icon));

            // the bottom panel: the bar of the way done along its top, under it the speed | the time left ... the X
            _bar.sizeDelta = new Vector2(0f, barH);
            _bar.anchoredPosition = Vector2.zero;
            float t = Mathf.Max(2f, Mathf.Round(w * 0.012f)), trackTop = Mathf.Round(pad * 1.2f);
            _trackW = w - 2f * pad;
            Place(_track, new Vector2(0f, 1f), new Vector2(pad + _trackW * 0.5f, -trackTop - t * 0.5f), new Vector2(_trackW, t));
            _fill.anchoredPosition = Vector2.zero;
            _fill.sizeDelta = new Vector2(0f, t);
            float knob = Mathf.Max(6f, Mathf.Round(w * 0.038f));
            _knob.sizeDelta = new Vector2(knob, knob);
            _knob.anchoredPosition = Vector2.zero;
            float rowBottom = Mathf.Round(pad * 0.7f), rowH = barH - trackTop - t - Mathf.Round(pad * 0.6f) - rowBottom, rowY = rowBottom + rowH * 0.5f;
            float spdW = Mathf.Round(w * 0.17f), xs = Mathf.Round(barH * 0.46f);
            Place(_speedText.rectTransform, Vector2.zero, new Vector2(pad + spdW * 0.5f, rowY), new Vector2(spdW + pad, rowH));
            _speedText.fontSize = Mathf.Max(9f, barH * 0.3f);
            Place(_divider, Vector2.zero, new Vector2(pad + spdW + Mathf.Round(pad * 0.5f), rowY), new Vector2(1f, Mathf.Max(4f, rowH - 4f)));
            float textX = pad + spdW + Mathf.Round(pad * 1.4f), textW = w - textX - xs - 2f * pad;
            Place(_barText.rectTransform, Vector2.zero, new Vector2(textX + textW * 0.5f, rowY), new Vector2(textW, rowH));
            _barText.fontSize = Mathf.Max(9f, barH * 0.25f);
            Place(_close, new Vector2(1f, 0f), new Vector2(-pad - xs * 0.5f, rowY), new Vector2(xs, xs));
            _closeText.fontSize = Mathf.Max(9f, xs * 0.48f);
            _bubbleText.fontSize = Mathf.Max(9f, w * 0.044f);
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>The phone's rect and its app tiles' rect on the screen (pixels, y up; null when not on screen), the sorting
        /// order just above the phone's canvas, and whether the player collapsed the phone (it then slides down off the
        /// screen, its tiles still active).</summary>
        private static void ReadPhone(out UnityEngine.Rect? phone, out UnityEngine.Rect? tiles, out UnityEngine.Rect? player, out int order, out bool collapsed)
        {
            phone = tiles = player = null;
            order = 100;
            collapsed = false;
            try
            {
                var uis = InstanceBehavior<UI.UIs>.Instance;
                var ui = uis != null ? uis.smartphoneUI : null;
                if (ui == null || !ui.gameObject.activeInHierarchy) return;
                var cw = uis!.smartphoneCollapsibleWindow;
                collapsed = cw != null && cw.IsCollapsed;
                var canvas = ui.GetComponentInParent<Canvas>();
                if (canvas != null) order = canvas.rootCanvas.sortingOrder + 1;
                // the phone as it shows: its frame picture (SmartPhoneFrame.frameImage) - the SmartphoneUI object itself spans
                // the screen (2026-11-01: so the device took the no-phone place over the tiles)
                var frame = (ui._smartPhoneFrame ?? ui.GetComponent<Player.HUD.SmartphoneUI.SmartPhoneFrame>())?.frameImage;
                UnityEngine.Rect? p = frame != null && frame.gameObject.activeInHierarchy ? ScreenRect(frame.rectTransform) : null;
                if (!Usable(p)) p = ScreenRect(ui.transform as RectTransform);
                if (Usable(p)) phone = p;
                var grid = ui.appButtonTemplate != null ? ui.appButtonTemplate.parent as RectTransform : null;   // the tiles' container
                var t = grid != null && grid.gameObject.activeInHierarchy ? ScreenRect(grid) : null;
                if (t.HasValue && phone.HasValue && phone.Value.Overlaps(t.Value)) tiles = t;
                // the music player at the phone's top, while it shows (it fades its canvas group out when the radio is off)
                var rc = ui.radioControls;
                if (rc != null && rc.gameObject.activeInHierarchy && (rc._radioCanvasGroup == null || rc._radioCanvasGroup.alpha > 0.5f))
                {
                    var pr = ScreenRect(rc.transform as RectTransform);
                    if (pr.HasValue && phone.HasValue && phone.Value.Overlaps(pr.Value)) player = pr;
                }
                string seen = $"{(phone.HasValue ? R(phone.Value) : "none")} tiles {(tiles.HasValue ? R(tiles.Value) : "none")} player {(player.HasValue ? R(player.Value) : "none")}{(collapsed ? " collapsed" : "")}";
                if (seen != _seenPhone) { _seenPhone = seen; Plugin.Logger.LogInfo($"[NavDevice] phone {seen} (screen {Screen.width}x{Screen.height}, order {order})."); }
            }
            catch { }
        }

        private static string _seenPhone = "";
        private static string R(UnityEngine.Rect r) => $"x{r.xMin:0}-{r.xMax:0} y{r.yMin:0}-{r.yMax:0}";

        /// <summary>A phone-sized rect on the screen (not one spanning it).</summary>
        private static bool Usable(UnityEngine.Rect? r) =>
            r.HasValue && r.Value.width < Screen.width * 0.5f && r.Value.height < Screen.height * 0.95f && r.Value.yMax > 0f && r.Value.yMin < Screen.height;

        private static UnityEngine.Rect? ScreenRect(RectTransform? rt)
        {
            if (rt == null) return null;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera? cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]), b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            var r = UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            return r.width > 1f && r.height > 1f ? r : (UnityEngine.Rect?)null;
        }

        // ── the route ahead ──

        /// <summary>Up to into.Length turns of more than 50 degrees within 600 m (the way Navigator.NextTurn finds the first).</summary>
        private static int FindTurns(Vector3 me, Turn[] into)
        {
            var r = Navigator.Route;
            int count = 0, i = Navigator.Passed + 1;
            if (r.Count < 3 || i >= r.Count) return 0;
            float acc = Navigator.Flat(me, r[i]);
            while (i < r.Count - 1 && acc < 600f && count < into.Length)
            {
                float turn = 0f, span = 0f;
                int j = i;
                for (; j < r.Count - 1 && span < 20f; j++)
                {
                    Vector3 x = r[j] - r[j - 1], y = r[j + 1] - r[j];
                    x.y = y.y = 0f;
                    if (x.sqrMagnitude > 0.01f && y.sqrMagnitude > 0.01f) turn += Vector3.SignedAngle(x, y, Vector3.up);
                    span += Navigator.Flat(r[j], r[j + 1]);
                }
                if (Mathf.Abs(turn) > 50f)
                {
                    into[count++] = new Turn { Dist = acc, Side = turn > 0f ? 1 : -1, Index = i };
                    for (; i < j && i < r.Count - 1; i++) acc += Navigator.Flat(r[i], r[i + 1]);   // past this turn
                    continue;
                }
                acc += Navigator.Flat(r[i], r[i + 1]);
                i++;
            }
            return count;
        }

        /// <summary>The street the route runs in some 25 m after a turn ("" when no door tells).</summary>
        private static string StreetAfter(int index)
        {
            var r = Navigator.Route;
            float run = 0f;
            for (int i = index; i < r.Count - 1; i++)
            {
                run += Navigator.Flat(r[i], r[i + 1]);
                if (run >= 25f) return NavStreets.Near(r[i + 1]);
            }
            return r.Count > 0 ? NavStreets.Near(r[r.Count - 1]) : "";
        }

        private static float Remaining(Vector3 me)
        {
            var r = Navigator.Route;
            int i = Navigator.Passed + 1;
            if (i >= r.Count) return Navigator.Flat(me, Navigator.Dest);
            float d = Navigator.Flat(me, r[i]);
            for (; i < r.Count - 1; i++) d += Navigator.Flat(r[i], r[i + 1]);
            return d;
        }

        // ── the map ──

        /// <summary>The streets within MeshRadius of the player, in metres from him.</summary>
        private void BuildRoads(Vector3 me)
        {
            _origin = me;
            _built = true;
            _roads.Begin();
            Navigator.RoadSegmentsNear(me, MeshRadius, _segs);
            float hw = RoadWidth * 0.5f;
            foreach (int k in _segs)
            {
                if (_roads.Count > 60000) break;   // a UI mesh holds 65k vertices
                Vector3 a = Navigator.SegA(k), b = Navigator.SegB(k);
                Quad(_roads, new Vector2(a.x - me.x, a.z - me.z), new Vector2(b.x - me.x, b.z - me.z), hw, hw * 0.6f, Road);
            }
            _roads.End();
        }

        /// <summary>The route ahead (as far as the streets are drawn), its width in screen pixels: a dark casing, the line in
        /// blue (on foot in orange) and white chevrons along it - spaced from the route's start, so they stay on the road
        /// while the player drives over them.</summary>
        private void BuildRoute(Vector3 me, float s)
        {
            _route.Begin();
            var r = Navigator.Route;
            float px = Mathf.Max(0.05f, s);
            float edge = Mathf.Max(2.6f, _w * 0.024f) / px, inner = Mathf.Max(1.6f, _w * 0.015f) / px;   // half widths, in pixels on the map
            Color32 col = Navigator.RouteCar ? Route : FootRoute;
            if (Navigator.ShowLine && r.Count >= 2)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    float hw = pass == 0 ? edge : inner;
                    Vector2 prev = new Vector2(me.x - _origin.x, me.z - _origin.z);
                    for (int i = Navigator.Passed + 1; i < r.Count; i++)
                    {
                        var cur = new Vector2(r[i].x - _origin.x, r[i].z - _origin.z);
                        Quad(_route, prev, cur, hw, hw, pass == 0 ? Casing : col);
                        if (cur.magnitude > MeshRadius) break;
                        prev = cur;
                    }
                }
                Chevrons(r, me, inner);
            }
            _route.End();
        }

        /// <summary>A ">" every 12 % of the map's width (by the zoom it is heading for, so a change of speed does not let them
        /// wander), pointing along the route, from 9 % ahead of the player on.</summary>
        private void Chevrons(List<Vector3> r, Vector3 me, float inner)
        {
            float step = 0.12f * _zoomWant, a = inner * 0.5f, b = inner * 0.62f, t = inner * 0.17f, ahead = 0.09f * _zoomWant;
            float run = 0f;   // along the route from its start
            for (int i = 0; i < Navigator.Passed + 1 && i < r.Count - 1; i++) run += Navigator.Flat(r[i], r[i + 1]);
            float at = Navigator.Passed + 1 < r.Count ? run - Navigator.Flat(me, r[Navigator.Passed + 1]) : run;   // the player
            float next = Mathf.Ceil((at + ahead) / step) * step;
            Vector2 prev = new Vector2(me.x - _origin.x, me.z - _origin.z);
            float pos = at;
            for (int i = Navigator.Passed + 1; i < r.Count && _route.Count < 60000; i++)
            {
                var cur = new Vector2(r[i].x - _origin.x, r[i].z - _origin.z);
                Vector2 d = cur - prev;
                float len = d.magnitude;
                if (len > 0.01f)
                {
                    d /= len;
                    var n = new Vector2(-d.y, d.x);
                    for (; next <= pos + len; next += step)
                    {
                        Vector2 p = prev + d * (next - pos), tip = p + d * a;
                        Quad(_route, p - d * a + n * b, tip, t, t, Chevron);
                        Quad(_route, p - d * a - n * b, tip, t, t, Chevron);
                    }
                }
                pos += len;
                if (cur.magnitude > MeshRadius) break;
                prev = cur;
            }
        }

        /// <summary>The player as the city map's marker - a green disc in a white ring, a white arrow pointing up -, fixed in
        /// the map's frame; the destination as the yellow diamond, upright, while it is in sight.</summary>
        private void Marker(Vector2 at, float w, Quaternion rot, float s)
        {
            float r = Mathf.Max(7f, w * 0.055f);
            _marker.Begin();
            Vector2 dest = _content.anchoredPosition + (Vector2)(rot * (new Vector2(Navigator.Dest.x - _origin.x, Navigator.Dest.z - _origin.z) * s));
            float hw = _view.rect.width * 0.5f, hh = _view.rect.height * 0.5f, dr = Mathf.Max(6f, w * 0.042f);
            if (Mathf.Abs(dest.x) < hw && dest.y < hh - _banner.rect.height && dest.y > -hh + _bar.rect.height)
            {
                Diamond(_marker, dest, dr * 1.3f, DestRim);
                Diamond(_marker, dest, dr, DestFill);
            }
            Disc(_marker, at, r * 1.5f, Shade);
            Disc(_marker, at, r * 1.28f, White);
            Disc(_marker, at, r, Me);
            Tri(_marker, at + new Vector2(0f, r * 0.62f), at + new Vector2(-r * 0.48f, -r * 0.44f), at + new Vector2(0f, -r * 0.2f), White);
            Tri(_marker, at + new Vector2(0f, r * 0.62f), at + new Vector2(0f, -r * 0.2f), at + new Vector2(r * 0.48f, -r * 0.44f), White);
            _marker.End();
        }

        /// <summary>The street's name in a bubble standing on the turn - upright, wherever the map is turned.</summary>
        private void PlaceBubble(string street, int index, Quaternion rot, float s)
        {
            var r = Navigator.Route;
            if (street.Length == 0 || index < 0 || index >= r.Count) { if (_bubble.gameObject.activeSelf) _bubble.gameObject.SetActive(false); return; }
            Vector2 at = _content.anchoredPosition + (Vector2)(rot * (new Vector2(r[index].x - _origin.x, r[index].z - _origin.z) * s));
            float hw = _view.rect.width * 0.5f, hh = _view.rect.height * 0.5f;
            float roof = hh - _banner.rect.height - _bubbleText.fontSize * 1.9f;   // under the top panel
            bool inside = Mathf.Abs(at.x) < hw - 6f && at.y > -hh + _bar.rect.height && at.y < roof;
            if (!inside) { if (_bubble.gameObject.activeSelf) _bubble.gameObject.SetActive(false); return; }
            if (!_bubble.gameObject.activeSelf) _bubble.gameObject.SetActive(true);
            SetText(_bubbleText, Caps(street));
            float tw = Mathf.Min(_bubbleText.preferredWidth + 12f, _view.rect.width * 0.8f), th = _bubbleText.fontSize * 1.75f;
            _bubble.sizeDelta = new Vector2(tw, th);
            _bubble.anchoredPosition = new Vector2(Mathf.Clamp(at.x, -hw + tw * 0.5f + 4f, hw - tw * 0.5f - 4f), at.y + 5f);
        }

        // ── mesh pieces ──

        /// <summary>A band from a to b, half width hw, with a square cap of ext at each end (pieces join without gaps).</summary>
        private static void Quad(NavMeshGraphic g, Vector2 a, Vector2 b, float hw, float ext, Color32 c)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return;
            d /= len;
            var n = new Vector2(-d.y, d.x) * hw;
            a -= d * ext; b += d * ext;
            int i0 = g.Add(a - n, c), i1 = g.Add(a + n, c), i2 = g.Add(b + n, c), i3 = g.Add(b - n, c);
            g.Tri(i0, i1, i2); g.Tri(i0, i2, i3);
        }

        private static void Tri(NavMeshGraphic g, Vector2 a, Vector2 b, Vector2 c, Color32 col)
        {
            g.Tri(g.Add(a, col), g.Add(b, col), g.Add(c, col));
        }

        private static void Disc(NavMeshGraphic g, Vector2 at, float r, Color32 col)
        {
            int c = g.Add(at, col), first = -1, prev = -1;
            for (int k = 0; k <= 20; k++)
            {
                float t = k / 20f * Mathf.PI * 2f;
                int v = k == 20 ? first : g.Add(at + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r, col);
                if (k == 0) first = v; else g.Tri(c, prev, v);
                prev = v;
            }
        }

        private static void Diamond(NavMeshGraphic g, Vector2 at, float r, Color32 col)
        {
            int up = g.Add(at + new Vector2(0f, r), col), right = g.Add(at + new Vector2(r, 0f), col);
            int down = g.Add(at - new Vector2(0f, r), col), left = g.Add(at - new Vector2(r, 0f), col);
            g.Tri(up, right, down); g.Tri(up, down, left);
        }

        // ── pictures drawn here: the turn arrows, a rounded plate, a disc ──

        private static void SetIcon(Image img, bool straight, int side)
        {
            var sp = TurnSprite(straight);
            if (img.sprite != sp) img.sprite = sp;
            img.rectTransform.localScale = new Vector3(!straight && side < 0 ? -1f : 1f, 1f, 1f);   // a left turn: mirrored
        }

        private static Sprite TurnSprite(bool straight)
        {
            var have = straight ? _arrowStraight : _arrowTurn;
            if (have != null) return have;
            var sp = Draw(96, p => straight ? InStraight(p) : InTurn(p), Vector4.zero);
            if (straight) _arrowStraight = sp; else _arrowTurn = sp;
            return sp;
        }

        /// <summary>A white plate with corners rounded by r pixels, nine-sliced (the game's panels round theirs a little).</summary>
        private static Sprite RoundSprite(int r)
        {
            if (_rounds.TryGetValue(r, out var have)) return have;
            const float R = 1f / 3f;   // r of the 3r pixels
            var sp = Draw(r * 3, p =>
            {
                float x = Mathf.Clamp(p.x, R, 1f - R), y = Mathf.Clamp(p.y, R, 1f - R);
                return (p - new Vector2(x, y)).magnitude <= R;
            }, new Vector4(r, r, r, r));
            _rounds[r] = sp;
            return sp;
        }

        private static Sprite DiscSprite()
        {
            if (_disc != null) return _disc;
            _disc = Draw(64, p => (p - new Vector2(0.5f, 0.5f)).magnitude <= 0.5f, Vector4.zero);
            return _disc;
        }

        private static Sprite Draw(int S, System.Func<Vector2, bool> inside, Vector4 border)
        {
            const int SS = 3;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "BAMP_NavDevice" };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                            if (inside(new Vector2((x + (sx + 0.5f) / SS) / S, (y + (sy + 0.5f) / SS) / S))) hit++;
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(255 * hit / (SS * SS)));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            tex.hideFlags = HideFlags.HideAndDontSave;   // as the mod's icons (MPPhoneButton.LoadIconFile)
            var sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        private const float Thick = 0.07f;

        private static bool InTurn(Vector2 p)
        {
            if (SegDist(p, new Vector2(0.32f, 0.06f), new Vector2(0.32f, 0.44f)) < Thick) return true;   // the stem
            Vector2 v = p - new Vector2(0.57f, 0.44f);                                                    // the bend
            if (v.x <= 0.0f && v.y >= 0.0f && Mathf.Abs(v.magnitude - 0.25f) < Thick) return true;
            if (SegDist(p, new Vector2(0.57f, 0.69f), new Vector2(0.70f, 0.69f)) < Thick) return true;   // on to the head
            return InTriangle(p, new Vector2(0.97f, 0.69f), new Vector2(0.67f, 0.90f), new Vector2(0.67f, 0.48f));
        }

        private static bool InStraight(Vector2 p)
        {
            if (SegDist(p, new Vector2(0.5f, 0.06f), new Vector2(0.5f, 0.64f)) < Thick) return true;
            return InTriangle(p, new Vector2(0.5f, 0.96f), new Vector2(0.28f, 0.62f), new Vector2(0.72f, 0.62f));
        }

        private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (a + ab * t - p).magnitude;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        private static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        // ── UI pieces ──

        private static void SetText(TextMeshProUGUI t, string text) { if (t.text != text) t.text = text; }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static Image Img(RectTransform rt, Color32 c, Sprite? sprite)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            if (sprite != null) { img.sprite = sprite; img.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple; }
            return img;
        }

        private static Image Icon(string name, RectTransform parent)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = Color.white;
            return img;
        }

        private static NavMeshGraphic Mesh(string name, RectTransform parent, float size)
        {
            var rt = Rect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var g = rt.gameObject.AddComponent<NavMeshGraphic>();
            g.raycastTarget = false;
            return g;
        }

        private static TextMeshProUGUI Text(string name, RectTransform parent, TMP_FontAsset? font, TextAlignmentOptions align, Color32 color)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.richText = true;
            return t;
        }
    }

    /// <summary>NAV-DEVICE-3: the street of a point on the road - the address street of the house doors next to it (a vote by
    /// nearness, so a corner house of the cross street does not win), localized as the game shows it.</summary>
    internal static class NavStreets
    {
        private const float Cell = 30f, Reach = 32f;
        private static Dictionary<long, List<(Vector2 p, string street)>>? _grid;
        private static object? _of;
        private static readonly Dictionary<string, float> _votes = new Dictionary<string, float>();

        private static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        internal static string Near(Vector3 at)
        {
            try
            {
                if (!Ensure() || _grid == null) return "";
                _votes.Clear();
                int cx = Mathf.FloorToInt(at.x / Cell), cz = Mathf.FloorToInt(at.z / Cell);
                for (int x = cx - 1; x <= cx + 1; x++)
                    for (int z = cz - 1; z <= cz + 1; z++)
                    {
                        if (!_grid.TryGetValue(Key(x, z), out var list)) continue;
                        foreach (var (p, street) in list)
                        {
                            float d = Vector2.Distance(p, new Vector2(at.x, at.z));
                            if (d > Reach) continue;
                            _votes.TryGetValue(street, out float v);
                            _votes[street] = v + 1f / (d + 5f);
                        }
                    }
                string best = "";
                float top = 0f;
                foreach (var kv in _votes) if (kv.Value > top) { top = kv.Value; best = kv.Key; }
                return best.Length > 0 ? AddressHelper.GetStreetNameLocalized(best) : "";
            }
            catch { return ""; }
        }

        private static bool Ensure()
        {
            var cbcs = InstanceBehavior<CityManager>.Instance?.cityBuildingControllers;
            if (cbcs == null || cbcs.Length == 0) return false;
            if (_grid != null && ReferenceEquals(_of, cbcs)) return true;
            var g = new Dictionary<long, List<(Vector2, string)>>();
            foreach (var cbc in cbcs)
            {
                try
                {
                    if (cbc == null || cbc.entranceDoors == null || cbc.entranceDoors.Length == 0 || cbc.building == null) continue;
                    string street = cbc.building.Address?.streetName ?? "";
                    if (street.Length == 0) continue;
                    Vector3 p = cbc.entranceDoors[0].doorTransform.position;
                    long k = Key(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));
                    if (!g.TryGetValue(k, out var l)) g[k] = l = new List<(Vector2, string)>();
                    l.Add((new Vector2(p.x, p.z), street));
                }
                catch { }
            }
            _grid = g;
            _of = cbcs;
            return true;
        }
    }

    /// <summary>A free mesh on a UI canvas - the triangles handed to it, in its own local units.</summary>
    internal sealed class NavMeshGraphic : MaskableGraphic
    {
        private readonly List<UIVertex> _v = new List<UIVertex>();
        private readonly List<int> _t = new List<int>();

        internal int Count => _v.Count;

        internal void Begin() { _v.Clear(); _t.Clear(); }

        internal int Add(Vector2 p, Color32 c)
        {
            var v = UIVertex.simpleVert;
            v.position = p;
            v.color = c;
            _v.Add(v);
            return _v.Count - 1;
        }

        internal void Tri(int a, int b, int c) { _t.Add(a); _t.Add(b); _t.Add(c); }

        internal void End() => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i < _v.Count; i++) vh.AddVert(_v[i]);
            for (int i = 0; i + 2 < _t.Count; i += 3) vh.AddTriangle(_t[i], _t[i + 1], _t[i + 2]);
        }
    }
}
