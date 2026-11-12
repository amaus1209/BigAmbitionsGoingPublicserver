using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Helpers;
using Localizor.LanguageChangeEvent;
using TMPro;
using UnityEngine;
using UI.ItemPanel;
using UnityEngine.UI;

namespace BigAmbitionsMP
{
    /// <summary>NAV-AUTOWALK-1 (user 2026-10-31: "a building clicked on the map gets, under Set destination, a button to
    /// walk there automatically"): the map's building card (UI.InGameUI.BuildingResume) shows our copy of its own
    /// "Set destination" button right under it, visible whenever that one is. The copy does what the game's does - the
    /// waypoint, the map closes - and the character then walks there (Navigator.WalkThere, the game's click-to-walk).
    /// SetButtons is the card's own place that switches its buttons (not a tiny method).</summary>
    [HarmonyPatch(typeof(UI.InGameUI.BuildingResume), "SetButtons")]
    public static class Patch_BuildingResume_SetButtons_AutoWalk
    {
        static void Postfix(UI.InGameUI.BuildingResume __instance) => NavAutoWalk.SyncCardButton(__instance);
    }

    internal static class NavAutoWalk
    {
        private static readonly ConditionalWeakTable<UI.InGameUI.BuildingResume, GameObject> _cardButtons = new ConditionalWeakTable<UI.InGameUI.BuildingResume, GameObject>();
        private static bool _walkPending, _warned, _drive;
        private static float _walkPendingUntil;

        internal static bool On => ServerRules.Module("ModuleNavigation");

        internal static void SyncCardButton(UI.InGameUI.BuildingResume card)
        {
            try
            {
                var src = card.setDestinationButton;
                if (src == null) return;
                if (!_cardButtons.TryGetValue(card, out var go) || go == null)
                {
                    go = UnityEngine.Object.Instantiate(src.gameObject, src.transform.parent);
                    go.name = "BAMP_AutoWalkButton";
                    go.transform.SetSiblingIndex(src.transform.GetSiblingIndex() + 1);
                    var btn = go.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick = new Button.ButtonClickedEvent();   // none of the original's listeners
                        btn.onClick.AddListener(() => FromCard(card));
                    }
                    _cardButtons.Remove(card);
                    _cardButtons.Add(card, go);
                }
                // the copy's localized label shows our text (the game's localizer renders an unknown key as it is);
                // AUTO-DRIVE-1: in the car it drives there
                string label = Navigator.InCar() ? Loc.T("gps_nav_autodrive", "Drive there automatically") : Loc.T("gps_nav_autowalk", "Walk there automatically");
                foreach (var loc in go.GetComponentsInChildren<TextLocalizationComponent>(true)) if (loc.Key != label) loc.Key = label;
                foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) if (t.text != label) t.text = label;
                bool show = On && src.gameObject.activeSelf;
                if (go.activeSelf != show) go.SetActive(show);
            }
            catch (Exception ex)
            {
                if (!_warned) { _warned = true; Plugin.Logger.LogWarning($"[Nav] map card button: {ex.Message}"); }
            }
        }

        private static void FromCard(UI.InGameUI.BuildingResume card)
        {
            try
            {
                var cbc = card.CityBuildingController;
                if (cbc == null || cbc.building == null) return;
                var address = cbc.building.Address;
                card.SetDestination();   // the game's own: its waypoint, and the map closes
                if (Navigator.TryBuilding(address, out var point, out var name)) Navigator.Set(point, name);
                _walkPending = true;     // the walk starts once the map is shut (Navigator.WalkThere refuses an open map)
                _walkPendingUntil = Time.unscaledTime + 3f;
                _drive = Navigator.InCar();   // AUTO-DRIVE-1: in the car it drives there instead
                Plugin.Logger.LogInfo($"[Nav] walk there automatically: '{name}' from the map (NAV-AUTOWALK-1).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Nav] walk from the map card: {ex.Message}"); }
        }

        /// <summary>NAV-CHOICE-1: a walk or a drive (in the car) is about to start once the map is shut.</summary>
        internal static bool Pending => _walkPending;

        /// <summary>NAV-CHOICE-1: the map's question answered "walk/drive there": the map shuts, then it starts (Tick).</summary>
        internal static void GoOnceMapShut()
        {
            _walkPending = true;
            _walkPendingUntil = Time.unscaledTime + 3f;
            _drive = Navigator.InCar();
            Plugin.Logger.LogInfo($"[Nav] {(_drive ? "drive" : "walk")} there: '{Navigator.DestName}' from the map's question (NAV-CHOICE-1).");
            try { if (CityMap.IsOpen) InstanceBehavior<CityManager>.Instance.cityMap.Toggle(); } catch { }
        }

        /// <summary>Main thread, every frame (MPCanvasUI.Update).</summary>
        internal static void Tick()
        {
            TickRightClickClear();
            AutoDrive.Tick();
            if (!_walkPending) return;
            if (Time.unscaledTime > _walkPendingUntil) { _walkPending = false; return; }
            if (CityMap.IsOpen) return;
            _walkPending = false;
            if (_drive && Navigator.InCar()) { AutoDrive.Start(); return; }   // AUTO-DRIVE-1
            string msg = Navigator.WalkThere();
            if (msg.Length > 0) Navigator.Notice(msg);
        }

        /// <summary>The waypoint the HUD button walks to: the navigation's own destination, else the game's (the map's
        /// "Set destination").</summary>
        internal static bool HasTarget()
        {
            if (Navigator.HasDest) return true;
            try { return SaveGameManager.Current?.customDestination != null; } catch { return false; }
        }

        internal static void WalkToTarget()
        {
            try
            {
                if (AutoDrive.Active) { AutoDrive.Stop(Loc.T("gps_nav_autodrive_off", "Self-driving off - you drive.")); return; }   // AUTO-DRIVE-1
                if (!Navigator.HasDest)
                {
                    var a = SaveGameManager.Current?.customDestination;
                    if (a == null || !Navigator.TryBuilding(a, out var point, out var name)) return;
                    Navigator.Set(point, name);
                }
                if (Navigator.InCar()) { AutoDrive.Start(); return; }   // AUTO-DRIVE-1: in the car it drives there
                string msg = Navigator.WalkThere();
                if (msg.Length > 0) Navigator.Notice(msg);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Nav] walk to the waypoint: {ex.Message}"); }
        }

        // NAV-RCLEAR-1 (user 2026-10-31: "when the route is marked, a right click should delete it again - but only when the
        // map is not open"): a short right click in the world (no drag - a held right button turns the camera) clears the
        // waypoint, the navigation's own and the game's ("Set destination"). Only on nothing the game reacts to itself: a
        // right click on a building or object opens its options there (MouseController -> OnIoRightClick). Not with the map
        // open, over the UI, in a menu, in the interior designer or while typing.
        private static float _rDownAt = -1f;
        private static Vector2 _rDownPos;
        private const float RClickMaxSeconds = 0.35f, RClickMaxPixels = 8f;

        private static void TickRightClickClear()
        {
            try
            {
                if (Input.GetMouseButtonDown(1)) { _rDownAt = Time.unscaledTime; _rDownPos = Input.mousePosition; return; }
                if (_rDownAt < 0f || !Input.GetMouseButtonUp(1)) return;
                bool click = Time.unscaledTime - _rDownAt <= RClickMaxSeconds && ((Vector2)Input.mousePosition - _rDownPos).magnitude <= RClickMaxPixels;
                _rDownAt = -1f;
                if (!click || !On || CityMap.IsOpen || UI.Smartphone.FullMenu.IsOpen || !HasTarget()) return;
                if (MouseController.currentTargetEntity != null || UI.InteriorDesigner.InteriorDesignerUI.IsOpen) return;
                if (MPChat.SuppressGameInput || GameManager.HasInputSelected()) return;
                if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
                ClearTarget();
            }
            catch (Exception ex)
            {
                if (!_warned) { _warned = true; Plugin.Logger.LogWarning($"[Nav] right click: {ex.Message}"); }
            }
        }

        /// <summary>No waypoint any more: the navigation's route and the game's destination (the game's own way, as clicking
        /// its destination pin does - PointOfInterest.Start).</summary>
        internal static void ClearTarget()
        {
            if (AutoDrive.Active) AutoDrive.Stop(null);   // AUTO-DRIVE-1: no route, no self-driving
            // NAV-RCLEAR-2 (2026-11-01): an automatic walk there ends too - the game's click-to-walk while its path still leads
            // to the destination (a walk of the player's own elsewhere goes on)
            try
            {
                var pc = PlayerHelper.PlayerController;
                var agent = pc != null ? pc.Character.navmeshAgent : null;
                if (pc != null && agent != null && agent.hasPath && Navigator.HasDest && Navigator.Flat(agent.destination, Navigator.Dest) < 35f)
                {
                    pc.ResetNavigation();
                    Plugin.Logger.LogInfo("[Nav] the automatic walk to the deleted destination stopped.");
                }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Nav] stop the walk: {ex.Message}"); }
            Navigator.Clear();
            _walkPending = false;
            try
            {
                if (SaveGameManager.Current?.customDestination != null)
                {
                    SaveGameManager.Current.customDestination = null;
                    UI.Guiders.GuidersManager.ResetGuider(UI.Guiders.DirectionGuiderType.Destination);
                }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Nav] clear the game's destination: {ex.Message}"); }
            Navigator.Notice(Loc.T("gps_nav_cleared", "Route deleted."));
            Plugin.Logger.LogInfo("[Nav] route deleted by a right click (NAV-RCLEAR-1).");
        }

        /// <summary>The character is not already on its way (the game's click-to-walk has a path).</summary>
        internal static bool Idle()
        {
            try
            {
                var ag = PlayerHelper.PlayerController?.Character?.navmeshAgent;
                return ag == null || !ag.enabled || !ag.hasPath || ag.remainingDistance < 1.5f;
            }
            catch { return true; }
        }
    }

    /// <summary>NAV-CHOICE-1 (user 2026-11-02: "the self-driving and auto-walk button should not show at the top but some other
    /// way - when you choose the destination on the map, ask right there whether to walk or drive there; and when I sit in
    /// the car already and then choose a destination, show it at the bottom, with parking: park automatically"), in place of
    /// NAV-AUTOWALK-1's button at the top of the screen:
    ///   * on the map, a destination set while it is open (a click on it, the navigation window) asks at the bottom of the
    ///     screen: walk there (on foot) or drive there (in the car) - in the car with the switch "park automatically" - or
    ///     the route only. Walk and drive shut the map, then the character walks or the car drives. The building card's own
    ///     button ("walk/drive there automatically") stays as it is.
    ///   * in the car with a destination, a bar at the bottom of the screen: drive there (stop while it drives) and park
    ///     automatically on or off (MPConfig.AutoPark - off, the self-driving stops at the destination: AutoDrive.BeginParking).
    /// Both are buttons on the overlay canvas: over them the map and the world take no click.</summary>
    public partial class MPCanvasUI
    {
        private GameObject? _navAskGO, _navCarGO;
        private TextMeshProUGUI? _navAskTitle, _navAskGoLbl, _navAskParkLbl, _navCarGoLbl, _navCarParkLbl;
        private Image? _navAskParkImg, _navCarParkImg, _navCarGoImg;
        private int _navMapSerial = -1, _navAskDone = -1;
        private bool _navMapWasOpen;
        private float _autoWalkNextAt;

        private static readonly Color NavBlue = new Color(0.12f, 0.45f, 0.85f, 0.97f);
        private static readonly Color NavGrey = new Color(0.22f, 0.25f, 0.32f, 0.97f);
        private static readonly Color NavGreen = new Color(0.20f, 0.55f, 0.30f, 0.97f);
        private static readonly Color NavRed = new Color(0.62f, 0.24f, 0.20f, 0.97f);
        private static readonly Color NavPlate = new Color(0.07f, 0.08f, 0.11f, 0.92f);

        private void TickAutoWalkHud()
        {
            NavAutoWalk.Tick();
            if (_canvasGO == null || Time.unscaledTime < _autoWalkNextAt) return;
            _autoWalkNextAt = Time.unscaledTime + 0.1f;
            bool ask = false, car = false, row = false;
            try
            {
                bool world = NavAutoWalk.On && (MPServer.IsRunning || MPClient.InMpGame || SaveGameManager.Current != null) && IsInGame();
                // NAV-CHOICE-2 (user 2026-11-08, BizMan opened from the map's building card: "Navi überlagert"): the big menu
                // (BizMan, the apps) lies over the map then - the question and the car bar wait until it is shut
                bool fullMenu = false;
                try { fullMenu = UI.Smartphone.FullMenu.IsOpen; } catch { }
                world &= !fullMenu;
                bool mapOpen = CityMap.IsOpen;
                if (mapOpen && !_navMapWasOpen) _navMapSerial = Navigator.DestSerial;   // a destination from before stays unasked
                _navMapWasOpen = mapOpen;
                bool inCar = Navigator.InCar();
                ask = world && mapOpen && Navigator.HasDest && !NavAutoWalk.Pending
                      && Navigator.DestSerial != _navMapSerial && Navigator.DestSerial != _navAskDone;
                car = world && !mapOpen && !Navigator.Inside() && inCar && (AutoDrive.Active || AutoDrive.InCarWithRoute());
                if (ask) { if (_navAskGO == null) BuildNavAsk(); PaintNavAsk(inCar); }
                // NAV-CARBAR-2: the two buttons sit in the vehicle panel's own button row; the plate only stands in
                // when that row is not there
                row = car && NavCarRow.Paint(AutoDrive.Active);
                if (car && !row) { if (_navCarGO == null) BuildNavCar(); PaintNavCar(); }
            }
            catch { ask = false; car = false; row = false; }
            if (!row) NavCarRow.Hide();
            bool plate = car && !row;
            if (_navAskGO != null && _navAskGO.activeSelf != ask) _navAskGO.SetActive(ask);
            if (_navCarGO != null && _navCarGO.activeSelf != plate) _navCarGO.SetActive(plate);
        }

        private string ParkText() => MPConfig.AutoPark ? Loc.T("gps_nav_park_on", "Park automatically: on") : Loc.T("gps_nav_park_off", "Park automatically: off");

        private void PaintNavAsk(bool inCar)
        {
            if (_navAskTitle != null) _navAskTitle.text = Loc.T("gps_nav_ask_title", "Destination: {0}", Navigator.DestName);
            if (_navAskGoLbl != null) _navAskGoLbl.text = inCar ? Loc.T("gps_nav_ask_drive", "Drive there") : Loc.T("gps_nav_ask_walk", "Walk there");
            if (_navAskParkImg != null)
            {
                if (_navAskParkImg.gameObject.activeSelf != inCar) _navAskParkImg.gameObject.SetActive(inCar);
                _navAskParkImg.color = MPConfig.AutoPark ? NavGreen : NavGrey;
            }
            if (_navAskParkLbl != null) _navAskParkLbl.text = ParkText();
        }

        private void PaintNavCar()
        {
            bool driving = AutoDrive.Active;
            if (_navCarGoLbl != null) _navCarGoLbl.text = driving ? Loc.T("gps_nav_autodrive_stop_hud", "Stop self-driving") : Loc.T("gps_nav_autodrive_hud", "Drive there");
            if (_navCarGoImg != null) _navCarGoImg.color = driving ? NavRed : NavBlue;
            if (_navCarParkImg != null) _navCarParkImg.color = MPConfig.AutoPark ? NavGreen : NavGrey;
            if (_navCarParkLbl != null) _navCarParkLbl.text = ParkText();
        }

        private void BuildNavAsk()
        {
            try
            {
                _navAskGO = NavPanel("BAMP_NavAsk", new Vector2(0f, 104f), new Vector2(640f, 112f));
                _navAskTitle = MakeLabel(_navAskGO.transform, "", 18, Color.white, 0f, 0f, 640f, 44f, TextAlignmentOptions.Center);
                ApplyFont(_navAskTitle);
                _navAskTitle.raycastTarget = false;
                var trt = _navAskTitle.rectTransform;
                trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
                trt.offsetMin = new Vector2(16f, -46f); trt.offsetMax = new Vector2(-16f, -4f);
                var (_, goImg, goLbl) = NavButton(_navAskGO.transform, new Vector2(-206f, 14f), 180f, NavBlue, () =>
                {
                    _navAskDone = Navigator.DestSerial;
                    NavAutoWalk.GoOnceMapShut();   // the map shuts, then the walk or the drive starts
                });
                _navAskGoLbl = goLbl;
                var (_, parkImg, parkLbl) = NavButton(_navAskGO.transform, new Vector2(0f, 14f), 216f, NavGreen, () => MPConfig.SetAutoPark(!MPConfig.AutoPark));
                _navAskParkImg = parkImg; _navAskParkLbl = parkLbl;
                var (_, _, routeLbl) = NavButton(_navAskGO.transform, new Vector2(206f, 14f), 180f, NavGrey, () => { _navAskDone = Navigator.DestSerial; });
                routeLbl.text = Loc.T("gps_nav_ask_route", "Route only");
                _navAskGO.SetActive(false);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Nav] destination question: {ex.Message}"); _navAskGO = null; }
        }

        private void BuildNavCar()
        {
            try
            {
                // the fallback when the vehicle panel's row is not there (NAV-CARBAR-2): the drive button only - the
                // game's own "Automatic parking" button stands for the rest
                _navCarGO = NavPanel("BAMP_NavCar", new Vector2(0f, 96f), new Vector2(260f, 64f));
                var (_, goImg, goLbl) = NavButton(_navCarGO.transform, new Vector2(0f, 10f), 236f, NavBlue, NavAutoWalk.WalkToTarget);
                _navCarGoImg = goImg; _navCarGoLbl = goLbl;
                _navCarGO.SetActive(false);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Nav] car bar: {ex.Message}"); _navCarGO = null; }
        }

        /// <summary>A dark rounded plate at the bottom centre of the screen.</summary>
        private GameObject NavPanel(string name, Vector2 pos, Vector2 size)
        {
            var go = MakeGO(name, _canvasGO!.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = NavPlate;
            var sp = IsAlive(_panelSprite) ? _panelSprite : EnsureRoundedSprite();
            if (sp != null) { try { img.sprite = sp; img.type = Image.Type.Sliced; } catch { } }
            return go;
        }

        /// <summary>A button on such a plate: x from its centre, y from its bottom.</summary>
        private (RectTransform rt, Image img, TextMeshProUGUI lbl) NavButton(Transform parent, Vector2 pos, float w, Color colour, UnityEngine.Events.UnityAction click)
        {
            var go = MakeGO("Btn", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(w, 44f);
            var img = go.AddComponent<Image>();
            img.color = colour;
            var sp = IsAlive(_panelSprite) ? _panelSprite : EnsureRoundedSprite();
            if (sp != null) { try { img.sprite = sp; img.type = Image.Type.Sliced; } catch { } }
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(click);
            var lbl = MakeLabel(go.transform, "", 16, Color.white, 0f, 0f, w, 44f, TextAlignmentOptions.Center);
            ApplyFont(lbl);
            lbl.raycastTarget = false;
            lbl.fontStyle = FontStyles.Bold;
            var lrt = lbl.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = new Vector2(6f, 0f); lrt.offsetMax = new Vector2(-6f, 0f);
            return (rt, img, lbl);
        }
    }

    /// <summary>NAV-CARBAR-2 (user 2026-11-06, a screenshot: "Autofahrt stoppen und Parken überlappen die Knöpfe dahinter -
    /// baue die Knöpfe in das Feld mit ein, in die Leiste, die jetzt überdeckt wird"; then "das Doppelte kann entfernt
    /// werden"): "Drive there / Stop self-driving" is one more button in the game's own button row of the vehicle panel
    /// (ItemPanelUI "Buttons": Sell, Automatic parking, Park vehicle ...), cloned from its Park button like the lock toggle.
    /// The "park automatically" switch is not in the row - the game's own "Automatic parking" button sits there; whether
    /// the self-driving parks at the destination stays in the map's destination question. The row is a
    /// HorizontalLayoutGroup that gives every button its label's width, and the panel ("Panel", a ContentSizeFitter) grows
    /// with its content - so the panel simply gets wider and nothing lies on top of anything. Colours are the game's own
    /// button sprites (button-ws-blue from the Park button; red if the game has one loaded, else the yellow of Sell).</summary>
    internal static class NavCarRow
    {
        private static ItemPanelUI? _panel;
        private static Button? _go;
        private static Image? _goImg;
        private static TextMeshProUGUI? _goLbl;
        private static Sprite? _blue, _stop;
        private static bool _logged;

        /// <summary>Shows and paints the button; false = the panel's row is not there (the plate stands in).</summary>
        internal static bool Paint(bool driving)
        {
            ItemPanelUI? panel = null;
            try { panel = InstanceBehavior<UI.UIs>.Instance?.playerHUD?.itemPanelUI; } catch { }
            if (panel == null || panel.parkButton == null || !panel.parkButton.gameObject.activeInHierarchy) { Hide(); return false; }
            if (_panel != panel || _go == null) Create(panel);
            if (_go == null) return false;
            if (!_go.gameObject.activeSelf) _go.gameObject.SetActive(true);
            if (!_go.interactable) _go.interactable = true;
            string text = driving ? Loc.T("gps_nav_autodrive_stop_hud", "Stop self-driving") : Loc.T("gps_nav_autodrive_hud", "Drive there");
            if (_goLbl != null && _goLbl.text != text) _goLbl.text = text;
            var sprite = driving ? _stop : _blue;
            if (_goImg != null && sprite != null && _goImg.sprite != sprite) _goImg.sprite = sprite;
            return true;
        }

        internal static void Hide()
        {
            if (_go != null && _go.gameObject.activeSelf) _go.gameObject.SetActive(false);
        }

        private static void Create(ItemPanelUI panel)
        {
            try
            {
                if (_go != null) UnityEngine.Object.Destroy(_go.gameObject);
                _go = null;
                _panel = panel;
                _blue = SpriteOf(panel.parkButton);
                _stop = FindSprite("button-ws-red@2x") ?? FindSprite("button-ws-red") ?? SpriteOf(panel.sellButton) ?? _blue;
                _go = Clone(panel, "BAMP_NavGoButton", NavAutoWalk.WalkToTarget, out _goImg, out _goLbl);
                // to the right of "Park vehicle": the game's own buttons keep their order
                if (_go != null) _go.transform.SetSiblingIndex(panel.parkButton.transform.GetSiblingIndex() + 1);
                if (!_logged)
                {
                    _logged = true;
                    var row = panel.parkButton.transform.parent;
                    Plugin.Logger.LogInfo($"[NavCar] self-driving button in the vehicle panel's row '{(row != null ? row.name : "?")}' (stop sprite '{(_stop != null ? _stop.name : "-")}') (NAV-CARBAR-2).");
                }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[NavCar] row button: {ex.Message}"); _go = null; }
        }

        private static Button? Clone(ItemPanelUI panel, string name, UnityEngine.Events.UnityAction click, out Image? img, out TextMeshProUGUI? lbl)
        {
            img = null; lbl = null;
            var src = panel.parkButton;
            var go = UnityEngine.Object.Instantiate(src.gameObject, src.transform.parent);
            go.name = name;
            // the Park button's label key would write "Park vehicle" back on the next language event
            foreach (var comp in go.GetComponentsInChildren(typeof(Component), true))
                if (comp != null && comp.GetType().Name == "TextLocalizationComponent") UnityEngine.Object.Destroy(comp);
            var btn = go.GetComponent<Button>();
            if (btn == null) { UnityEngine.Object.Destroy(go); return null; }
            btn.onClick = new Button.ButtonClickedEvent();   // a fresh event: the clone's persistent one parks the car
            btn.onClick.AddListener(click);
            btn.interactable = true;
            try { var cg = go.GetComponent<CanvasGroup>(); if (cg != null) { cg.interactable = true; cg.blocksRaycasts = true; cg.alpha = 1f; } } catch { }
            img = go.GetComponent<Image>();
            lbl = go.GetComponentInChildren<TextMeshProUGUI>(true);
            go.SetActive(false);
            return btn;
        }

        private static Sprite? SpriteOf(Button? b)
        {
            try { return b != null ? b.GetComponent<Image>()?.sprite : null; } catch { return null; }
        }

        private static Sprite? FindSprite(string name)
        {
            try
            {
                foreach (var sp in Resources.FindObjectsOfTypeAll<Sprite>())
                    if (sp != null && sp.name == name) return sp;
            }
            catch { }
            return null;
        }
    }
}
