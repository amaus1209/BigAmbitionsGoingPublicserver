using System.Collections.Generic;
using GoingOnline;
using HarmonyLib;
using TMPro;
using UI.ItemPanel;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

namespace BigAmbitionsMP
{
    /// <summary>CAR-LIVERY-1 (user 2026-11-10: "In Public Going jede Seite des Fahrzeugs könnte man dort einstellen, was für
    /// Farbwahl und Logo-Plätze, nachdem man das Auto gekauft hat"): "Beklebung" beside Park / Sell in the vehicle's panel
    /// (the player's own vehicle, standing) opens a window in the style of our others. On the left a live view of the
    /// vehicle from the side being edited (a camera of its own renders into the window; things between it and the car are
    /// cut away by its near plane), the four sides as tabs, the paint (the dealer's colours, changeable now), the name, a
    /// slogan and a contact line. On the right the side's look, its colour and where the logo stands, "for all sides", and
    /// whose logo and name it shows. Every change is on the vehicle at once; "Fertig" (or Escape) closes.</summary>
    public partial class MPCanvasUI
    {
        private GameObject? _lvRoot;
        private bool _lvOpen;
        private VehicleController? _lvCar;
        private string _lvVid = "", _lvType = "";
        private CarLivery _lv = new CarLivery();
        private int _lvSide, _lvFocus = -1;
        private bool _lvCaretOn, _lvDirtyText;
        private float _lvApplyAt = -1f;
        private string _lvStatus = "";
        private List<string> _lvBiz = new List<string>();
        private readonly List<(RectTransform rt, int act, int idx)> _lvHits = new List<(RectTransform, int, int)>();
        private LField? _lvNameF, _lvSloganF, _lvContactF;
        private GameObject? _lvCamGo;
        private Camera? _lvCam;
        private RenderTexture? _lvRt;
        private bool _lvCamSet, _lvHold;
        private int _lvLogged;
        // GO-LIVERY-4: the two size sliders of the side (0 lettering, 1 logo) and the one being dragged
        private int _lvDrag = -1;
        private readonly RectTransform?[] _lvSlHit = new RectTransform?[2], _lvSlFill = new RectTransform?[2], _lvSlKnob = new RectTransform?[2];
        private readonly TextMeshProUGUI?[] _lvSlVal = new TextMeshProUGUI?[2];

        private static readonly string[] LvSideKeys = { "gps_livery_side_left", "gps_livery_side_right", "gps_livery_side_back", "gps_livery_side_front" };
        private static readonly string[] LvSideEn = { "Left", "Right", "Back", "Front" };

        // ── open, close ─────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>From the vehicle panel's "Beklebung": the window for the vehicle the player sits in.</summary>
        internal void LiveryOpen(VehicleController vc)
        {
            if (!_built || _canvasGO == null || vc == null || vc.vehicleInstance == null) return;
            try
            {
                var rb = vc.GetComponent<Rigidbody>();
                if (rb != null && rb.velocity.magnitude > 1.5f)
                {
                    PassengerHud.Toast(Loc.T("gps_livery_stop", "Stop first - then you can change the livery."), 3f);
                    return;
                }
            }
            catch { }
            _lvCar = vc;
            _lvVid = vc.vehicleInstance.id ?? "";
            _lvType = vc.vehicleInstance.vehicleTypeName ?? "";
            var have = CarLiveries.Get(_lvVid);
            _lv = have != null ? have.Copy() : CarLiveries.Proposal(_lvVid, _lvType);
            // the first proposal shows on the vehicle at once - not on the service van, which keeps following the company's
            // look (the app's) until something here is changed
            if (have == null && _lvType != CarLiveries.VanType) CarLiveries.Set(_lvVid, _lv.Copy());
            _lvBiz = CarLiveries.Businesses();
            _lvSide = 0; _lvFocus = -1; _lvStatus = ""; _lvApplyAt = -1f;
            _lvOpen = true;
            LvHold(true);
            LvCamera(true);
            LvRebuild();
            if (_lvLogged++ < 5) Plugin.Logger.LogInfo($"[CarLivery] livery window opened for a {(_lvType == CarLiveries.VanType ? "service van" : _lvType)} ({(have != null ? "its livery" : "a first proposal")}, {_lvBiz.Count} business(es)) (CAR-LIVERY-1).");
        }

        private void LvClose()
        {
            try { if (_lvApplyAt >= 0f) LvApplyNow(); } catch { }   // typing not applied yet
            LvHold(false);
            _lvOpen = false;
            _lvFocus = -1;
            if (_lvRoot != null) { try { Destroy(_lvRoot); } catch { } _lvRoot = null; }
            LvCamera(false);
            _lvHits.Clear();
            ShieldPressUntilRelease();
        }

        /// <summary>While the window is open the car does not drive: the game's own vehicle block (it uses it while the car
        /// is washed: PlayerController.SetVehicleNavigationBlocker, the car's input off) - set only when no other block is
        /// on, and lifted only if it was ours.</summary>
        private void LvHold(bool on)
        {
            try
            {
                var pc = Helpers.PlayerHelper.PlayerController;
                if (pc == null) { _lvHold = false; return; }
                if (on && !_lvHold && !pc.VehicleNavigationDisabled) { pc.SetVehicleNavigationBlocker(VehicleNavigationBlocker.WashingVehicle); _lvHold = true; }
                else if (!on && _lvHold) { _lvHold = false; pc.UnsetVehicleNavigationBlocker(VehicleNavigationBlocker.WashingVehicle); }
            }
            catch (Exception ex) { _lvHold = false; Plugin.Logger.LogWarning($"[CarLivery] vehicle hold: {ex.Message}"); }
        }

        // ── every frame ─────────────────────────────────────────────────────────────────────────────────────────────

        private void TickLiveryEditor()
        {
            if (!_lvOpen) return;
            try
            {
                if (_lvCar == null || !IsAlive(_lvCar) || SaveGameManager.Current == null) { LvClose(); return; }
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (_lvFocus >= 0) { _lvFocus = -1; _lvDirtyText = true; } else LvClose();
                    ConsumeEscape();
                    return;
                }
                LvTyping();
                if (_lvApplyAt >= 0f && Time.unscaledTime >= _lvApplyAt) LvApplyNow();
                LvCameraStep();
                if (_lvDrag >= 0)
                {   // GO-LIVERY-4: a size slider follows the mouse while the button is held; painted when the hand pauses
                    if (Input.GetMouseButton(0)) LvDragTo(new Vector2(Input.mousePosition.x, Input.mousePosition.y));
                    else { _lvDrag = -1; LvApplyNow(); }
                    return;
                }
                if (!Input.GetMouseButtonDown(0)) return;
                var mp = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                if (_lv.Sides[_lvSide].Design != LiveryPaint.DNone)
                    for (int k = 0; k < 2; k++)
                        if (_lvSlHit[k] is RectTransform hr && hr != null && hr.gameObject.activeInHierarchy && RectHit(hr, mp)) { _lvDrag = k; LvDragTo(mp); return; }
                int nf = LHitF(_lvNameF, mp) ? 0 : LHitF(_lvSloganF, mp) ? 1 : LHitF(_lvContactF, mp) ? 2 : -1;
                if (nf >= 0) { if (nf != _lvFocus) { _lvFocus = nf; _lvDirtyText = true; } return; }
                foreach (var (rt, act, idx) in _lvHits)
                    if (rt != null && rt.gameObject.activeInHierarchy && RectHit(rt, mp)) { LvAct(act, idx); return; }
                if (_lvFocus >= 0) { _lvFocus = -1; _lvDirtyText = true; }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[CarLivery] window: {ex.GetType().Name}: {ex.Message}"); }
        }

        private void LvAct(int act, int idx)
        {
            var look = _lv.Sides[_lvSide];
            switch (act)
            {
                case 1: _lvSide = Mathf.Clamp(idx, 0, 3); _lvFocus = -1; LvRebuild(); return;   // another side: the camera turns
                case 2: look.Design = idx; break;
                case 3: look.Color = Livery.Palette[Mathf.Clamp(idx, 0, Livery.Palette.Length - 1)]; if (look.Design == LiveryPaint.DNone) look.Design = LiveryPaint.DPanel; break;
                case 4: look.Place = Mathf.Clamp(idx, 0, 2); break;
                case 5:   // the look and the colour on every side (each keeps its place)
                    for (int i = 0; i < 4; i++)
                    {
                        if (i == _lvSide) continue;
                        int d = look.Design;
                        if (i == 3 && (d == LiveryPaint.DGoFibre || d == LiveryPaint.DGoTech || d == LiveryPaint.DBand)) d = LiveryPaint.DLogoName;
                        _lv.Sides[i].Design = d;
                        _lv.Sides[i].Color = look.Color;
                        _lv.Sides[i].Text = look.Text; _lv.Sides[i].Logo = look.Logo;   // GO-LIVERY-4
                    }
                    _lvStatus = Loc.T("gps_livery_copied", "This side's look is now on every side.");
                    break;
                case 6: LvPaint(idx); LvRebuild(); return;
                case 7:
                {
                    var list = new List<string>(_lvBiz) { "" };
                    int at = list.IndexOf(_lv.Biz);
                    at = ((at < 0 ? 0 : at) + idx + list.Count) % list.Count;
                    _lv.Biz = list[at];
                    break;
                }
                case 8:   // the first proposal again; the service van follows the company's look again (no own livery)
                    _lv = CarLiveries.Proposal(_lvVid, _lvType);
                    if (_lvType == CarLiveries.VanType)
                    {
                        _lvApplyAt = -1f;
                        CarLiveries.Set(_lvVid, null);
                        _lvStatus = Loc.T("gps_livery_reset_van", "The van wears the company's livery from the Going Online app again.");
                        LvRebuild();
                        return;
                    }
                    _lvStatus = Loc.T("gps_livery_reset_done", "Back to the first proposal.");
                    break;
                case 9:
                    foreach (var l in _lv.Sides) l.Design = LiveryPaint.DNone;
                    _lvStatus = Loc.T("gps_livery_none_done", "The vehicle is without livery now.");
                    break;
                case 10: LvClose(); return;
                default: return;
            }
            LvApplyNow();
            LvRebuild();
        }

        /// <summary>The livery onto the vehicle (and into the save at the next save, and to the other players).</summary>
        private void LvApplyNow()
        {
            _lvApplyAt = -1f;
            if (_lvVid.Length == 0) return;
            CarLiveries.Set(_lvVid, _lv.Copy());
        }

        /// <summary>The paint: the dealer's colour, now after the purchase (the game keeps it as the vehicle's colour).</summary>
        private void LvPaint(int idx)
        {
            try
            {
                var colors = InstanceBehavior<GlobalReferences>.Instance?.vehicleColors;
                if (colors == null || idx < 0 || idx >= colors.Length || colors[idx] == null || _lvCar == null) return;
                var vc = colors[idx];
                _lvCar.vehicleInstance.vehicleColorName = vc.name;
                var cf = _lvCar.CarFeatures ?? _lvCar.GetComponent<CarFeatures>();
                if (cf != null) cf.SetColor(vc);
                CarLiveries.Set(_lvVid, _lv.Copy());   // the decals look again (their lettering follows the paint)
                Plugin.Logger.LogInfo($"[CarLivery] vehicle repainted (CAR-LIVERY-1).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[CarLivery] paint: {ex.Message}"); }
        }

        // ── typing ──────────────────────────────────────────────────────────────────────────────────────────────────

        private void LvTyping()
        {
            if (_lvFocus >= 0)
            {
                int field = _lvFocus;
                string s = field == 0 ? _lv.Name : field == 1 ? _lv.Slogan : _lv.Contact;
                int max = field == 2 ? 56 : 32;
                string before = s;
                if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.V))
                {
                    try { foreach (char ch in GUIUtility.systemCopyBuffer ?? "") if (!char.IsControl(ch) && s.Length < max) s += ch; } catch { }
                }
                else if (Input.inputString.Length > 0)
                {
                    foreach (char c in Input.inputString)
                    {
                        if (c == '\b') { if (s.Length > 0) s = s.Substring(0, s.Length - 1); }
                        else if (c == '\n' || c == '\r') { _lvFocus = -1; _lvDirtyText = true; break; }
                        else if (c == '\t') { _lvFocus = (_lvFocus + 1) % 3; _lvDirtyText = true; break; }
                        else if (!char.IsControl(c) && s.Length < max) s += c;
                    }
                }
                if (s != before)
                {
                    if (field == 0) _lv.Name = s; else if (field == 1) _lv.Slogan = s; else _lv.Contact = s;
                    _lvDirtyText = true;
                    _lvApplyAt = Time.unscaledTime + 0.7f;   // painted when the typing pauses
                }
            }
            bool on = Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0;
            if (on == _lvCaretOn && !_lvDirtyText) return;
            _lvCaretOn = on; _lvDirtyText = false;
            string bizName = CarLiveries.NameOf(new CarLivery { Biz = _lv.Biz });
            LvFieldShow(_lvNameF, _lv.Name, 0, bizName.Length > 0 ? bizName : Loc.T("gps_livery_name_none", "no name"), on);
            LvFieldShow(_lvSloganF, _lv.Slogan, 1, _lvType == CarLiveries.VanType ? Loc.T("gps_livery_slogan_van", "the company's slogan") : Loc.T("gps_livery_slogan_none", "no slogan"), on);
            LvFieldShow(_lvContactF, _lv.Contact, 2, _lvType == CarLiveries.VanType ? Loc.T("gps_livery_contact_van", "the company's contact line") : Loc.T("gps_livery_contact_none", "no contact line"), on);
        }

        private void LvFieldShow(LField? f, string text, int which, string empty, bool caretOn)
        {
            if (f == null) return;
            bool focus = _lvFocus == which;
            string shown = focus ? (caretOn ? text + "|" : text) : text.Length > 0 ? text : empty;
            LFieldText(f, shown, focus);
            try { f.lbl.color = !focus && text.Length == 0 ? L_DISTXT : C_WHITE; } catch { }
            LFieldState(f, false, focus);
        }

        // ── the window ──────────────────────────────────────────────────────────────────────────────────────────────

        private void LvRebuild()
        {
            if (_canvasGO == null) return;
            if (_lvRoot != null) { try { Destroy(_lvRoot); } catch { } _lvRoot = null; }
            _lvHits.Clear();
            _lvRoot = LMakeRoot("BAMP_LiveryEditor");
            _lvRoot.transform.SetAsLastSibling();

            float scale = Mathf.Max(0.1f, UiScale) * LS;
            float W = Mathf.Min(1160f, Screen.width / scale - 60f), H = Mathf.Min(720f, Screen.height / scale - 50f);
            // the view keeps 16:9; the left column needs the view and 310 units below it (sides, paint, three fields)
            float PW = Mathf.Clamp(W - 24f - 24f - 24f - 440f, 380f, 576f);
            float PH = Mathf.Round(Mathf.Clamp(H - 310f, 160f, PW * 9f / 16f));
            PW = Mathf.Round(PH * 16f / 9f);
            float RX = 24f + PW + 24f, RW = W - RX - 24f;
            bool van = _lvType == CarLiveries.VanType;
            string carName = "";
            try { carName = Localizor.LocalizorManager.GetLocalization(_lvType) ?? ""; } catch { }
            if (string.IsNullOrEmpty(carName) || carName == _lvType) carName = van ? Loc.T("gps_livery_van", "Service van") : "";
            var card = LMakeCard(_lvRoot.transform, "Card", Loc.T("gps_livery_title", "Livery"), W, H);
            card.sub.text = carName;
            var ct = card.go.transform;

            // the view of the vehicle
            var well = LImg(ct, "View", LC(0x20262C), LRoundSprite(false), 4f);
            LPos(well.rectTransform, 24f, 58f, PW, PH);
            var ri = MakeGO("Live", well.transform).AddComponent<RawImage>();
            ri.raycastTarget = false;
            LStretch(ri.rectTransform, 2f, 2f, 2f, 2f);
            ri.texture = _lvRt;
            ri.enabled = _lvRt != null;
            var area = LvArea(_lvSide);
            if (area == null && _lvType != CarLiveries.VanType)
                LText(well.transform, Loc.T("gps_livery_no_area", "This side of the vehicle has no free surface for a livery."), 13f, L_NOTICE, 12f, PH - 34f, PW - 24f, 28f, TextAlignmentOptions.Left);

            // the sides
            float ty = 58f + PH + 10f, tw = (PW - 3f * 8f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                var b = LButton(ct, "Side" + i, Loc.T(LvSideKeys[i], LvSideEn[i]), 24f + i * (tw + 8f), ty, tw, 34f, i == _lvSide ? 1 : 0, 11f);
                _lvHits.Add((b.rt, 1, i));
            }

            // the paint
            float y = ty + 34f + 12f;
            LText(ct, Loc.T("gps_livery_paint", "Paint"), 13f, L_MUTED, 24f, y, PW, 20f, TextAlignmentOptions.Left, true);
            y += 22f;
            try
            {
                var colors = InstanceBehavior<GlobalReferences>.Instance?.vehicleColors;
                string cur = _lvCar != null ? _lvCar.vehicleInstance.vehicleColorName ?? "" : "";
                if (colors != null)
                {
                    const float S = 24f, G = 6f;
                    int per = Mathf.Max(1, Mathf.FloorToInt((PW + G) / (S + G)));
                    for (int i = 0; i < colors.Length && i < per * 2; i++)
                    {
                        if (colors[i] == null) continue;
                        Color32 t = colors[i].tint;
                        float x = 24f + (i % per) * (S + G), yy = y + (i / per) * (S + G);
                        LvSwatch(ct, "Paint" + i, new Color32(t.r, t.g, t.b, 255), x, yy, S, colors[i].name == cur, 6, i);
                    }
                    y += (colors.Length > per ? 2 : 1) * (S + G);
                }
            }
            catch { }

            // the lettering
            y += 6f;
            float fw = (PW - 12f) / 2f;
            LText(ct, Loc.T("gps_livery_name", "Name"), 13f, L_MUTED, 24f, y, fw, 20f, TextAlignmentOptions.Left, true);
            LText(ct, Loc.T("gps_livery_slogan", "Slogan"), 13f, L_MUTED, 24f + fw + 12f, y, fw, 20f, TextAlignmentOptions.Left, true);
            y += 22f;
            _lvNameF = LMakeField(ct, "Name", 24f, y, fw, 34f);
            _lvSloganF = LMakeField(ct, "Slogan", 24f + fw + 12f, y, fw, 34f);
            y += 34f + 10f;
            LText(ct, Loc.T("gps_livery_contact", "Contact line"), 13f, L_MUTED, 24f, y, PW, 20f, TextAlignmentOptions.Left, true);
            y += 22f;
            _lvContactF = LMakeField(ct, "Contact", 24f, y, PW, 34f);
            _lvDirtyText = true;

            // the side's look
            var look = _lv.Sides[_lvSide];
            float ry = 58f;
            LText(ct, Loc.T("gps_livery_side_head", "{0}: look", Loc.T(LvSideKeys[_lvSide], LvSideEn[_lvSide])), 15f, C_WHITE, RX, ry, RW, 24f, TextAlignmentOptions.Left, true, true, 4f);
            ry += 30f;
            var designs = new List<(int d, string key, string en)>
            {
                (LiveryPaint.DNone, "gps_livery_d_none", "None"), (LiveryPaint.DLogoName, "gps_livery_d_logoname", "Logo and name"),
                (LiveryPaint.DPanel, "gps_livery_d_panel", "Colour panel"), (LiveryPaint.DBand, "gps_livery_d_band", "Colour band"),
                (LiveryPaint.DWrap, "gps_livery_d_wrap", "Full wrap"),
            };
            if (van && _lvSide != 3)
            {
                designs.Add((LiveryPaint.DGoFibre, "gps_livery_d_gofibre", "Fibre light"));
                designs.Add((LiveryPaint.DGoTech, "gps_livery_d_gotech", "Technicians at work"));
            }
            float dw = (RW - 8f) / 2f;
            for (int i = 0; i < designs.Count; i++)
            {
                var (d, key, en) = designs[i];
                var b = LButton(ct, "Design" + d, Loc.T(key, en), RX + (i % 2) * (dw + 8f), ry + (i / 2) * 40f, dw, 32f, look.Design == d ? 1 : 0, 10f);
                _lvHits.Add((b.rt, 2, d));
            }
            ry += ((designs.Count + 1) / 2) * 40f + 6f;

            // GO-LIVERY-4 (user 2026-11-10: "mach einfach unter Gestaltung einen Schieberegler für die Schriftgröße und
            // Logogröße"): the side's lettering and logo, 50 to 200 per cent
            bool sized = look.Design != LiveryPaint.DNone;
            float sw2 = (RW - 16f) / 2f;
            for (int k = 0; k < 2; k++)
            {
                float sx = RX + k * (sw2 + 16f);
                LText(ct, k == 0 ? Loc.T("gps_livery_textsize", "Text size") : Loc.T("gps_livery_logosize", "Logo size"), 13f, L_MUTED, sx, ry, sw2 - 56f, 20f, TextAlignmentOptions.Left, true);
                _lvSlVal[k] = LText(ct, "", 13f, sized ? C_WHITE : L_DISTXT, sx + sw2 - 56f, ry, 56f, 20f, TextAlignmentOptions.Right);
                var hit = LImg(ct, "Size" + k, new Color(0f, 0f, 0f, 0f), null, 0f);
                LPos(hit.rectTransform, sx, ry + 20f, sw2, 28f);
                var bar = LImg(hit.transform, "Bar", LC(0x3A424A), LRoundSprite(false), 3f);
                var brt = bar.rectTransform;
                brt.anchorMin = new Vector2(0f, 0.5f); brt.anchorMax = new Vector2(1f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
                brt.sizeDelta = new Vector2(0f, 8f * LS); brt.anchoredPosition = Vector2.zero;
                var fill = LImg(bar.transform, "Fill", sized ? LC(0x3B8CF0) : LC(0x59616A), LRoundSprite(false), 3f);
                var knob = LImg(hit.transform, "Knob", sized ? Color.white : LC(0x8A9099), LRoundSprite(false), 8f);
                _lvSlHit[k] = hit.rectTransform; _lvSlFill[k] = fill.rectTransform; _lvSlKnob[k] = knob.rectTransform;
                LvSliderShow(k);
            }
            ry += 20f + 28f + 12f;

            bool coloured = look.Design != LiveryPaint.DNone;
            LText(ct, Loc.T("gps_livery_colour", "Colour"), 13f, L_MUTED, RX, ry, RW, 20f, TextAlignmentOptions.Left, true);
            ry += 22f;
            int n = Livery.Palette.Length;
            float ss = Mathf.Min(34f, (RW - (n - 1) * 6f) / n);
            for (int i = 0; i < n; i++)
            {
                int rgb = Livery.Palette[i];
                LvSwatch(ct, "Colour" + i, new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255), RX + i * (ss + 6f), ry, ss,
                         coloured && (look.Color & 0xFFFFFF) == rgb, 3, i);
            }
            ry += ss + 14f;

            LText(ct, Loc.T("gps_livery_place", "Where the logo stands"), 13f, L_MUTED, RX, ry, RW, 20f, TextAlignmentOptions.Left, true);
            ry += 22f;
            string[] placeKeys = _lvSide == 2 ? new[] { "gps_livery_p_left", "gps_livery_p_middle", "gps_livery_p_right" } : new[] { "gps_livery_p_front", "gps_livery_p_middle", "gps_livery_p_back" };
            string[] placeEn = _lvSide == 2 ? new[] { "Left", "Middle", "Right" } : new[] { "Front", "Middle", "Back" };
            float pw3 = (RW - 16f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                bool can = coloured && (_lvSide != 3 || i == 1);
                var b = LButton(ct, "Place" + i, Loc.T(placeKeys[i], placeEn[i]), RX + i * (pw3 + 8f), ry, pw3, 32f, !can ? 4 : look.Place == i || (_lvSide == 3 && i == 1) ? 1 : 0, 10f);
                if (can) _lvHits.Add((b.rt, 4, i));
            }
            ry += 32f + 12f;
            var all = LButton(ct, "AllSides", Loc.T("gps_livery_all_sides", "This look on every side"), RX, ry, RW, 34f, 0, 10f);
            _lvHits.Add((all.rt, 5, 0));
            ry += 34f + 16f;

            LText(ct, Loc.T("gps_livery_biz", "Logo and name of"), 13f, L_MUTED, RX, ry, RW, 20f, TextAlignmentOptions.Left, true);
            ry += 22f;
            var prev = LButton(ct, "BizPrev", "<", RX, ry, 40f, 32f, 0, 12f);
            _lvHits.Add((prev.rt, 7, -1));
            var next = LButton(ct, "BizNext", ">", RX + RW - 40f, ry, 40f, 32f, 0, 12f);
            _lvHits.Add((next.rt, 7, 1));
            var bn = LText(ct, _lv.Biz.Length > 0 ? _lv.Biz : Loc.T("gps_livery_biz_none", "no logo"), 14f, _lv.Biz.Length > 0 ? C_WHITE : L_DISTXT, RX + 48f, ry, RW - 96f, 32f, TextAlignmentOptions.Center);
            bn.overflowMode = TextOverflowModes.Ellipsis;
            ry += 32f + 14f;

            // status and buttons
            float by = H - 54f, bw1 = 140f, bw2 = 190f, bw3 = 170f;
            var done = LButton(ct, "Done", Loc.T("gps_livery_done", "Done"), W - 24f - bw1, by, bw1, 40f, 1, 12f);
            _lvHits.Add((done.rt, 10, 0));
            var none = LButton(ct, "NoLivery", Loc.T("gps_livery_no_livery", "No livery"), W - 24f - bw1 - 10f - bw2, by, bw2, 40f, 0, 11f);
            _lvHits.Add((none.rt, 9, 0));
            var reset = LButton(ct, "Reset", Loc.T("gps_livery_reset", "First proposal"), W - 24f - bw1 - 10f - bw2 - 10f - bw3, by, bw3, 40f, 0, 11f);
            _lvHits.Add((reset.rt, 8, 0));
            string hint = _lvStatus.Length > 0 ? _lvStatus
                : van ? Loc.T("gps_livery_hint_van", "Every change shows on the van at once. Without its own livery, the van wears the company's from the Going Online app.")
                      : Loc.T("gps_livery_hint", "Every change shows on the vehicle at once - the other players see it too.");
            // the hint in the right column, between the business and the buttons
            LText(ct, hint, 12f, _lvStatus.Length > 0 ? L_NOTICE : L_MUTED, RX, ry, RW, Mathf.Max(20f, by - 8f - ry), TextAlignmentOptions.TopLeft, false, false, 0f, true);
            _lvStatus = "";
        }

        /// <summary>GO-LIVERY-4: a size slider shows the side's value (the fill, the knob, "120 %").</summary>
        private void LvSliderShow(int k)
        {
            var look = _lv.Sides[_lvSide];
            int v = k == 0 ? look.Text : look.Logo;
            float t = Mathf.InverseLerp(50f, 200f, v);
            if (_lvSlFill[k] is RectTransform f && f != null)
            {
                f.anchorMin = Vector2.zero; f.anchorMax = new Vector2(t, 1f); f.pivot = new Vector2(0f, 0.5f);
                f.offsetMin = Vector2.zero; f.offsetMax = Vector2.zero;
            }
            if (_lvSlKnob[k] is RectTransform kn && kn != null)
            {
                kn.anchorMin = kn.anchorMax = new Vector2(t, 0.5f); kn.pivot = new Vector2(0.5f, 0.5f);
                kn.sizeDelta = new Vector2(16f * LS, 22f * LS); kn.anchoredPosition = Vector2.zero;
            }
            if (_lvSlVal[k] is TextMeshProUGUI lbl && lbl != null) lbl.text = v + " %";
        }

        /// <summary>GO-LIVERY-4: the dragged slider to the mouse - in steps of 5 per cent.</summary>
        private void LvDragTo(Vector2 mp)
        {
            int k = _lvDrag;
            if (k < 0 || k > 1 || !(_lvSlHit[k] is RectTransform hr) || hr == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hr, mp, null, out var local);
            var r = hr.rect;
            float t = Mathf.Clamp01((local.x - r.xMin) / Mathf.Max(1f, r.width));
            int v = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(50f, 200f, t) / 5f) * 5, 50, 200);
            var look = _lv.Sides[_lvSide];
            if (k == 0) { if (look.Text == v) return; look.Text = v; }
            else { if (look.Logo == v) return; look.Logo = v; }
            LvSliderShow(k);
            _lvApplyAt = Time.unscaledTime + 0.35f;
        }

        /// <summary>A colour swatch: a rounded square, a ring when it is chosen.</summary>
        private void LvSwatch(Transform p, string name, Color32 c, float x, float y, float s, bool chosen, int act, int idx)
        {
            var img = LImg(p, name, c, LRoundSprite(false), 3f);
            LPos(img.rectTransform, x, y, s, s);
            var ring = LImg(img.transform, "Ring", chosen ? Color.white : new Color(0f, 0f, 0f, 0.35f), LRoundSprite(true), 3f);
            LStretch(ring.rectTransform, chosen ? -3f : 0f, chosen ? -3f : 0f, chosen ? -3f : 0f, chosen ? -3f : 0f);
            _lvHits.Add((img.rectTransform, act, idx));
        }

        /// <summary>The free surface of the side being edited (null: none; the van has its own fields).</summary>
        private CarArea? LvArea(int side)
        {
            if (_lvCar == null || _lvType == CarLiveries.VanType) return null;
            try
            {
                var cf = _lvCar.CarFeatures ?? _lvCar.GetComponent<CarFeatures>();
                var set = cf != null ? CarAreas.For(_lvCar.gameObject, cf, _lvType) : null;
                return set?.Side[side];
            }
            catch { return null; }
        }

        // ── the view of the vehicle ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>A camera of its own, rendering into the window while it is open.</summary>
        private void LvCamera(bool on)
        {
            if (!on)
            {
                if (_lvCam != null) { try { _lvCam.targetTexture = null; } catch { } }
                if (_lvCamGo != null) { try { Destroy(_lvCamGo); } catch { } }
                if (_lvRt != null) { try { _lvRt.Release(); Destroy(_lvRt); } catch { } }
                _lvCamGo = null; _lvCam = null; _lvRt = null; _lvCamSet = false;
                return;
            }
            if (_lvCam != null) return;
            try
            {
                _lvRt = new RenderTexture(1024, 576, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "GPS_LiveryView", antiAliasing = 1 };
                _lvRt.Create();
                _lvCamGo = new GameObject("GPS_LiveryCamera") { hideFlags = HideFlags.HideAndDontSave };
                _lvCam = _lvCamGo.AddComponent<Camera>();
                var main = Camera.main;
                if (main != null)
                {
                    _lvCam.cullingMask = main.cullingMask & ~(1 << 5);   // not the UI layer
                    _lvCam.depth = main.depth - 1f;
                }
                _lvCam.fieldOfView = 30f;
                _lvCam.nearClipPlane = 0.3f;
                _lvCam.farClipPlane = 600f;
                _lvCam.targetTexture = _lvRt;
                var hd = _lvCamGo.AddComponent<HDAdditionalCameraData>();
                var mainHd = main != null ? main.GetComponent<HDAdditionalCameraData>() : null;
                if (mainHd != null) { try { mainHd.CopyTo(hd); } catch { } }
                _lvCamSet = false;
                LvCameraStep();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[CarLivery] the window's view: {ex.Message} (CAR-LIVERY-1).");
                LvCamera(false);
            }
        }

        /// <summary>The camera looks at the side being edited (from a little above; the hood from above in front), gliding
        /// there when the side changes; the near plane starts just before the vehicle, so walls in between are cut away.</summary>
        private void LvCameraStep()
        {
            if (_lvCam == null || _lvCar == null) return;
            var t = _lvCar.transform;
            var b = new Bounds(t.position, Vector3.zero);
            bool any = false;
            try
            {
                var cf = _lvCar.CarFeatures ?? _lvCar.GetComponent<CarFeatures>();
                if (cf != null && cf.bodyMeshes != null)
                    foreach (var r in cf.bodyMeshes) if (r != null) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            }
            catch { }
            if (!any) b = new Bounds(t.position + Vector3.up, new Vector3(2f, 1.6f, 4.5f));
            float len = Mathf.Max(2f, Vector3.Scale(b.size, new Vector3(Mathf.Abs(t.forward.x), Mathf.Abs(t.forward.y), Mathf.Abs(t.forward.z))).magnitude);
            float wid = Mathf.Max(1.2f, Vector3.Scale(b.size, new Vector3(Mathf.Abs(t.right.x), Mathf.Abs(t.right.y), Mathf.Abs(t.right.z))).magnitude);
            float hgt = Mathf.Max(1f, b.size.y);
            Vector3 dir; float pitch, across, depth;
            switch (_lvSide)
            {
                case 1: dir = t.right; pitch = 10f; across = len; depth = wid; break;
                case 2: dir = -t.forward; pitch = 12f; across = wid; depth = len; break;
                case 3: dir = t.forward; pitch = 38f; across = wid; depth = len; break;
                default: dir = -t.right; pitch = 10f; across = len; depth = wid; break;
            }
            float vfov = _lvCam.fieldOfView * Mathf.Deg2Rad, hfov = 2f * Mathf.Atan(Mathf.Tan(vfov * 0.5f) * 16f / 9f);
            float dist = Mathf.Max(across * 0.62f / Mathf.Tan(hfov * 0.5f), hgt * 0.9f / Mathf.Tan(vfov * 0.5f)) + depth * 0.5f;
            var flat = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
            if (flat.sqrMagnitude < 0.01f) flat = dir;
            // tilted up by pitch, so the camera stands above the vehicle. CAR-LIVERY-2 (the user's pictures 2026-11-10: the
            // UMC Nunavut seemed to float, and "Vorne" showed its underbody from below the street): the rotation about
            // up x flat had turned the view down - the camera stood under the road, looking up.
            float pr = pitch * Mathf.Deg2Rad;
            var view = flat * Mathf.Cos(pr) + Vector3.up * Mathf.Sin(pr);
            var target = b.center + Vector3.up * (_lvSide == 3 ? 0f : hgt * 0.05f);
            var pos = target + view.normalized * dist;
            var rot = Quaternion.LookRotation(target - pos, Vector3.up);
            if (!_lvCamSet) { _lvCam.transform.SetPositionAndRotation(pos, rot); _lvCamSet = true; }
            else
            {
                float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 6f);
                _lvCam.transform.SetPositionAndRotation(Vector3.Lerp(_lvCam.transform.position, pos, k), Quaternion.Slerp(_lvCam.transform.rotation, rot, k));
            }
            float toCar = Vector3.Distance(_lvCam.transform.position, target);
            _lvCam.nearClipPlane = Mathf.Max(0.3f, toCar - Mathf.Max(len, wid) * 0.6f - 0.5f);
        }
    }

    // ── the button in the vehicle's panel ──────────────────────────────────────────────────────────────────────────

    /// <summary>CAR-LIVERY-1: "Beklebung" in the vehicle's panel (ItemPanelUI) - the player's own vehicle only.</summary>
    [HarmonyPatch(typeof(ItemPanelUI), nameof(ItemPanelUI.SetVehicle))]
    public static class Patch_ItemPanelUI_SetVehicle_Livery
    {
        static void Postfix(ItemPanelUI __instance, VehicleController vehicle)
        {
            try
            {
                if (vehicle == null || vehicle.vehicleType == null) return;   // the panel keeps the previous car (see the lock button)
                LiveryButton.Show(__instance, vehicle);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[CarLivery] SetVehicle: {ex.Message}"); }
        }
    }

    [HarmonyPatch(typeof(ItemPanelUI), nameof(ItemPanelUI.SetItemInstance))]
    public static class Patch_ItemPanelUI_SetItemInstance_Livery
    {
        static void Postfix(ItemPanelUI __instance) { try { LiveryButton.Hide(__instance); } catch { } }
    }

    [HarmonyPatch(typeof(ItemPanelUI), nameof(ItemPanelUI.SetMiniGameMode))]
    public static class Patch_ItemPanelUI_SetMiniGameMode_Livery
    {
        static void Postfix(ItemPanelUI __instance) { try { LiveryButton.Hide(__instance); } catch { } }
    }

    internal static class LiveryButton
    {
        private static readonly Dictionary<ItemPanelUI, Button> _btn = new Dictionary<ItemPanelUI, Button>();
        private static int _logged;

        public static void Show(ItemPanelUI panel, VehicleController vehicle)
        {
            if (panel == null || vehicle == null || vehicle.vehicleInstance == null) return;
            string vid = vehicle.vehicleInstance.id ?? "";
            string type = vehicle.vehicleInstance.vehicleTypeName ?? "";
            bool own = false;
            try { var list = SaveGameManager.Current?.VehicleInstances; own = list != null && list.Contains(vehicle.vehicleInstance); } catch { }
            if (vid.Length == 0 || vid.StartsWith("BAMP_", StringComparison.Ordinal) || !own || !CarLiveries.Eligible(type)) { Hide(panel); return; }
            var btn = GetOrCreate(panel);
            if (btn == null) return;
            btn.gameObject.SetActive(true);
            btn.interactable = true;
            try { var cg = btn.GetComponent<CanvasGroup>(); if (cg != null) { cg.interactable = true; cg.blocksRaycasts = true; cg.alpha = 1f; } } catch { }
            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) tmp.text = Loc.T("gps_livery_button", "Livery");
            btn.onClick.RemoveAllListeners();
            var vc = vehicle;
            btn.onClick.AddListener(() =>
            {
                try { MPCanvasUI.Instance?.LiveryOpen(vc); } catch (Exception ex) { Plugin.Logger.LogWarning($"[CarLivery] open: {ex.Message}"); }
            });
            if (_logged++ < 2) Plugin.Logger.LogInfo("[CarLivery] \"Livery\" button in the vehicle's panel (CAR-LIVERY-1).");
        }

        public static void Hide(ItemPanelUI panel)
        {
            if (panel == null) return;
            if (_btn.TryGetValue(panel, out var b) && b != null) b.gameObject.SetActive(false);
        }

        private static Button? GetOrCreate(ItemPanelUI panel)
        {
            if (_btn.TryGetValue(panel, out var existing) && existing != null) return existing;
            var src = panel.parkButton;
            if (src == null) return null;
            var clone = UnityEngine.Object.Instantiate(src.gameObject, src.transform.parent);
            clone.name = "BAMP_LiveryButton";
            clone.transform.SetAsFirstSibling();
            var btn = clone.GetComponent<Button>();
            // a fresh click event: the clone carries Park's persistent onClick (ClickPark -> leave the car) - see the lock button
            if (btn != null) btn.onClick = new Button.ButtonClickedEvent();
            foreach (var comp in clone.GetComponentsInChildren(typeof(Component), true))
                if (comp != null && comp.GetType().Name == "TextLocalizationComponent") UnityEngine.Object.Destroy(comp);
            List<ItemPanelUI>? dead = null;
            foreach (var kv in _btn) if (kv.Key == null) (dead ??= new List<ItemPanelUI>()).Add(kv.Key!);
            if (dead != null) foreach (var k in dead) _btn.Remove(k);
            if (btn != null) _btn[panel] = btn;
            return btn;
        }
    }
}
