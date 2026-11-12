using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BigAmbitionsMP
{
    /// <summary>
    /// MODULE-SIGNS-1 (Going Public Server 1.0.12, module 2 of the server's own features): your own pictures on your
    /// businesses' signs. F7 opens the "Shop signs" window:
    ///   1. sign type - square sign (1024x1024), wide storefront sign (1024x128) or billboard (1024x1024);
    ///   2. a picture (JPG or PNG) from the Signs folder (&lt;data folder&gt;/Signs; "Open folder" opens it) - no system file
    ///      dialog: out of process it needs PowerShell, which Proton lacks, and in process it crashed the game;
    ///   3. one or more of your businesses.
    /// Apply writes the picture as the game's own logo file (&lt;character folder&gt;/&lt;business&gt;/&lt;size&gt;.jpg - the files
    /// the game's logo editor writes and LogoHelper reads), keeps the old file in &lt;data folder&gt;/SignBackups first, and
    /// repaints the signs. In multiplayer nothing more is needed: BusinessSync notices the changed file within ~10 s and
    /// every player gets it. Editing the logo in the game's own editor later replaces the picture, as it should.
    /// </summary>
    public partial class MPCanvasUI
    {
        private static readonly (LogoSize size, string key, string en, int w, int h)[] SgTypes =
        {
            (LogoSize.SquareSign, "gps_signs_square", "Square sign", 1024, 1024),
            (LogoSize.WideSign,   "gps_signs_wide",   "Wide sign",   1024, 128),
            (LogoSize.Billboard,  "gps_signs_billboard", "Billboard", 1024, 1024),
        };
        private const int SG_IMG_ROWS = 6, SG_BIZ_ROWS = 11;   // LOGO-GO-1: one row less for the game's logo above them

        private GameObject? _sgRoot;
        private bool _sgOpen;
        private int _sgType, _sgImgPage, _sgBizPage;
        private string _sgImage = "", _sgStatus = "";
        private bool _sgStatusBad;
        private readonly HashSet<string> _sgSel = new HashSet<string>();   // address keys
        private readonly List<string> _sgImages = new List<string>();
        private readonly List<(string key, string name, string type)> _sgBiz = new List<(string, string, string)>();
        private readonly List<(RectTransform rt, int act, int idx)> _sgHits = new List<(RectTransform, int, int)>();
        private Texture2D? _sgPreview;
        private int _sgPrevW, _sgPrevH;

        private static string SgFolder => Path.Combine(MPConfig.DataRootPath, "Signs");

        /// <summary>LOGO-GO-1 (user 2026-11-07: "besser wäre Standard-Schilder und Mod zu verbinden, für beide zur Auswahl"): the
        /// first entry of the picture list is the game's own business logo (BizMan, Settings, Logo: shape, colours, font, the
        /// game's icons folder) - applied, the game makes it again for the chosen sign type, the other sign types keep theirs.</summary>
        private const string SgGameLogo = "*game-logo*";

        /// <summary>Main thread, every frame from Update: F7 toggles the window in the world; while open it owns the mouse.</summary>
        private void TickSignManager()
        {
            try
            {
                bool inWorld = SaveGameManager.Current != null && Helpers.PlayerHelper.PlayerController != null && !GameManager.isCitySceneBeingUnloaded;
                if (!inWorld) { if (_sgOpen) SgClose(); return; }
                if (!_sgOpen)
                {
                    if (ModKeys.Signs.Down() && !MPChat.SuppressGameInput && !GameManager.HasInputSelected())
                    {
                        if (ServerRules.Module("ModuleShopSigns")) SgOpen();   // SERVER-RULES-1
                        else ServerRules.ModuleOff(Loc.T("gps_signs_title", "Shop signs"));
                    }
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Escape) || ModKeys.Signs.Down()) { SgClose(); ConsumeEscape(); return; }
                if (!Input.GetMouseButtonDown(0)) return;
                var mp = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                foreach (var (rt, act, idx) in _sgHits)
                    if (rt != null && rt.gameObject.activeInHierarchy && RectHit(rt, mp)) { SgAct(act, idx); return; }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Signs] tick: {ex.Message}"); }
        }

        private void SgOpen()
        {
            try { Directory.CreateDirectory(SgFolder); } catch { }
            _sgOpen = true; _sgStatus = ""; _sgStatusBad = false;
            SgScanImages(); SgScanBusinesses();
            SgRebuild();
            Plugin.Logger.LogInfo($"[Signs] window opened ({_sgImages.Count} picture(s) in '{SgFolder}', {_sgBiz.Count} business(es)).");
        }

        private void SgClose()
        {
            _sgOpen = false;
            ShieldPressUntilRelease();   // NAV-WALK-CLICK-1
            _sgHits.Clear();
            if (_sgRoot != null) { try { Destroy(_sgRoot); } catch { } _sgRoot = null; }
            if (_sgPreview != null) { try { Destroy(_sgPreview); } catch { } _sgPreview = null; }
        }

        private void SgScanImages()
        {
            _sgImages.Clear();
            try
            {
                foreach (var f in Directory.GetFiles(SgFolder))
                {
                    string e = Path.GetExtension(f).ToLowerInvariant();
                    if (e == ".jpg" || e == ".jpeg" || e == ".png") _sgImages.Add(f);
                }
                _sgImages.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Signs] reading '{SgFolder}': {ex.Message}"); }
            if (_sgImage != SgGameLogo && !_sgImages.Contains(_sgImage)) { _sgImage = ""; SgLoadPreview(); }
        }

        /// <summary>The businesses this machine's player runs (rented, with a name): their signs are this player's to set.</summary>
        private void SgScanBusinesses()
        {
            _sgBiz.Clear();
            var gi = SaveGameManager.Current;
            if (gi?.BuildingRegistrations == null) return;
            foreach (var reg in gi.BuildingRegistrations)
            {
                if (reg == null || !reg.RentedByPlayer || string.IsNullOrWhiteSpace(reg.BusinessName)) continue;
                if (string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == "ba:businesstype_empty") continue;
                if (BusinessSync.SafeLogoDir(reg.BusinessName) == null) continue;
                _sgBiz.Add((GameStateReader.AddressKey(reg), reg.BusinessName, reg.businessTypeName));
            }
            _sgBiz.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            _sgSel.RemoveWhere(k => !_sgBiz.Exists(b => b.key == k));
        }

        private void SgLoadPreview()
        {
            if (_sgPreview != null) { try { Destroy(_sgPreview); } catch { } _sgPreview = null; }
            _sgPrevW = _sgPrevH = 0;
            if (_sgImage.Length == 0 || _sgImage == SgGameLogo) return;
            try
            {
                var tex = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                if (!tex.LoadImage(File.ReadAllBytes(_sgImage))) { Destroy(tex); SgSetStatus(Loc.T("gps_signs_bad_image", "This picture could not be read."), true); return; }
                _sgPreview = tex; _sgPrevW = tex.width; _sgPrevH = tex.height;
            }
            catch (Exception ex) { SgSetStatus(Loc.T("gps_signs_bad_image", "This picture could not be read."), true); Plugin.Logger.LogWarning($"[Signs] preview '{_sgImage}': {ex.Message}"); }
        }

        private void SgSetStatus(string text, bool bad) { _sgStatus = text; _sgStatusBad = bad; }

        // acts: 1 sign type (idx) · 2 picture (idx) · 3 business (idx) · 4 all · 5 none · 6 open folder · 7 reload
        //       8 apply · 9 close · 10/11 picture page -/+ · 12/13 business page -/+
        private void SgAct(int act, int idx)
        {
            switch (act)
            {
                case 1: _sgType = idx; break;
                case 2: _sgImage = idx == -2 ? SgGameLogo : idx >= 0 && idx < _sgImages.Count ? _sgImages[idx] : ""; _sgStatus = ""; SgLoadPreview(); break;
                case 3: if (idx >= 0 && idx < _sgBiz.Count) { var k = _sgBiz[idx].key; if (!_sgSel.Remove(k)) _sgSel.Add(k); } break;
                case 4: foreach (var b in _sgBiz) _sgSel.Add(b.key); break;
                case 5: _sgSel.Clear(); break;
                case 6: try { Application.OpenURL("file:///" + SgFolder.Replace('\\', '/')); } catch (Exception ex) { Plugin.Logger.LogWarning($"[Signs] open folder: {ex.Message}"); } return;
                case 7: SgScanImages(); SgScanBusinesses(); break;
                case 8: SgApply(); break;
                case 9: SgClose(); return;
                case 10: _sgImgPage = Math.Max(0, _sgImgPage - 1); break;
                case 11: _sgImgPage++; break;
                case 12: _sgBizPage = Math.Max(0, _sgBizPage - 1); break;
                case 13: _sgBizPage++; break;
            }
            SgRebuild();
        }

        private void SgApply()
        {
            if (_sgImage == SgGameLogo && _sgSel.Count > 0) { SgApplyGameLogo(); return; }
            if (_sgImage.Length == 0 || _sgPreview == null || _sgSel.Count == 0)
            {
                SgSetStatus(Loc.T("gps_signs_pick_all", "Pick a sign type, a picture and at least one business."), true);
                return;
            }
            var t = SgTypes[_sgType];
            byte[] jpg;
            try { jpg = _sgPreview.EncodeToJPG(92); }
            catch (Exception ex) { SgSetStatus(Loc.T("gps_signs_bad_image", "This picture could not be read."), true); Plugin.Logger.LogWarning($"[Signs] encode: {ex.Message}"); return; }
            int ok = 0, failed = 0;
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            foreach (var b in _sgBiz)
            {
                if (!_sgSel.Contains(b.key)) continue;
                try
                {
                    string? dir = BusinessSync.SafeLogoDir(b.name);
                    if (dir == null) { failed++; continue; }
                    Directory.CreateDirectory(dir);
                    string file = Path.Combine(dir, t.size.ToString() + ".jpg");
                    if (File.Exists(file))
                    {
                        string bdir = Path.Combine(MPConfig.DataRootPath, "SignBackups", LogoHelper.GetBusinessNamePathSafe(b.name));
                        Directory.CreateDirectory(bdir);
                        File.Copy(file, Path.Combine(bdir, $"{t.size}-{stamp}.jpg"), true);
                    }
                    File.WriteAllBytes(file, jpg);
                    GameStatePatcher.RefreshOwnLogo(b.name, b.key);
                    ok++;
                }
                catch (Exception ex) { failed++; Plugin.Logger.LogWarning($"[Signs] '{b.name}': {ex.Message}"); }
            }
            Plugin.Logger.LogInfo($"[Signs] {t.size} from '{Path.GetFileName(_sgImage)}' applied to {ok} business(es), {failed} failed.");
            string msg = Loc.T("gps_signs_applied", "Applied to {0} business(es).", ok);
            if (MPServer.IsRunning || MPClient.IsConnected) msg += " " + Loc.T("gps_signs_others", "The other players see it within a few seconds.");
            if (failed > 0) msg += " " + Loc.T("gps_signs_failed", "{0} failed.", failed);
            SgSetStatus(msg, failed > 0 && ok == 0);
        }

        /// <summary>LOGO-GO-1: the game's logo back on the chosen sign type of the chosen businesses. The game's generator makes
        /// all three sizes from the business's logo settings (BizMan's logo editor; a business without any gets the game's own
        /// pick for its type, a plain warehouse its warehouse logo); the two other sizes are put back as they were, so a
        /// picture on them stays. The changed file is backed up first, as for a picture.</summary>
        private void SgApplyGameLogo()
        {
            var t = SgTypes[_sgType];
            int ok = 0, failed = 0;
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var gi = SaveGameManager.Current;
            foreach (var b in _sgBiz)
            {
                if (!_sgSel.Contains(b.key)) continue;
                try
                {
                    BuildingRegistration? reg = null;
                    if (gi?.BuildingRegistrations != null)
                        foreach (var r in gi.BuildingRegistrations) if (r != null && GameStateReader.AddressKey(r) == b.key) { reg = r; break; }
                    string? dir = BusinessSync.SafeLogoDir(b.name);
                    if (reg == null || dir == null || BusinessLogoGenerator.Instance == null) { failed++; continue; }
                    Directory.CreateDirectory(dir);
                    var keep = new Dictionary<string, byte[]>();
                    foreach (var other in SgTypes)
                    {
                        if (other.size == t.size) continue;
                        string f = Path.Combine(dir, other.size.ToString() + ".jpg");
                        if (File.Exists(f)) keep[f] = File.ReadAllBytes(f);
                    }
                    string file = Path.Combine(dir, t.size.ToString() + ".jpg");
                    if (File.Exists(file))
                    {
                        string bdir = Path.Combine(MPConfig.DataRootPath, "SignBackups", LogoHelper.GetBusinessNamePathSafe(b.name));
                        Directory.CreateDirectory(bdir);
                        File.Copy(file, Path.Combine(bdir, $"{t.size}-{stamp}.jpg"), true);
                    }
                    string name = b.name, key = b.key;
                    System.Action putBack = () =>
                    {
                        try { foreach (var kv in keep) File.WriteAllBytes(kv.Key, kv.Value); } catch (Exception ex) { Plugin.Logger.LogWarning($"[Signs] '{name}' keeping the other signs: {ex.Message}"); }
                        try { GameStatePatcher.RefreshOwnLogo(name, key); } catch { }
                    };
                    var settings = reg.logoSettings;
                    var type = Helpers.BusinessTypeHelper.GetData(reg.businessTypeName);
                    bool hasShapes = type?.logoShapes != null && type.logoShapes.Count > 0;
                    if ((settings == null || string.IsNullOrEmpty(settings.logoShape)) && hasShapes)
                        settings = reg.logoSettings = LogoHelper.GenerateLogoSetting(reg.businessTypeName);
                    if (settings != null && !string.IsNullOrEmpty(settings.logoShape))
                        BusinessLogoGenerator.Create(reg.BusinessName, settings, dir, true, () => putBack());
                    else if (type != null)
                    {
                        BusinessLogoGenerator.Instance.GenerateWarehouseLogo(reg.BusinessName, type, dir, true);
                        putBack();
                    }
                    else { failed++; continue; }
                    ok++;
                }
                catch (Exception ex) { failed++; Plugin.Logger.LogWarning($"[Signs] game logo for '{b.name}': {ex.Message}"); }
            }
            Plugin.Logger.LogInfo($"[Signs] {t.size}: the game's logo put back on {ok} business(es), {failed} failed (LOGO-GO-1).");
            string msg = Loc.T("gps_signs_gamelogo_applied", "The game's logo is back on {0} business(es).", ok);
            if (MPServer.IsRunning || MPClient.IsConnected) msg += " " + Loc.T("gps_signs_others", "The other players see it within a few seconds.");
            if (failed > 0) msg += " " + Loc.T("gps_signs_failed", "{0} failed.", failed);
            SgSetStatus(msg, failed > 0 && ok == 0);
        }

        // ── the window (lobby kit, mock-up units) ──
        private void SgRebuild()
        {
            if (_canvasGO == null) return;
            if (_sgRoot != null) { try { Destroy(_sgRoot); } catch { } }
            _sgHits.Clear();
            _sgRoot = LMakeRoot("BAMP_SignManager");
            _sgRoot.transform.SetAsLastSibling();
            const float W = 960f, H = 660f, LX = 26f, LW = 300f, RX = 350f, RW = W - RX - 26f;
            var card = LMakeCard(_sgRoot.transform, "Card", Loc.T("gps_signs_title", "Shop signs"), W, H);
            card.sub.text = ModKeys.Signs.Display();   // MOD-KEYS-1
            var ct = card.go.transform;

            // 1 · sign type
            LText(ct, Loc.T("gps_signs_step1", "1 · Sign type"), 15f, C_WHITE, LX, 60f, LW, 24f, TextAlignmentOptions.Left, true);
            for (int i = 0; i < SgTypes.Length; i++)
            {
                var ty = SgTypes[i];
                var b = LButton(ct, "Type" + i, Loc.T(ty.key, ty.en) + $"  {ty.w}x{ty.h}", LX, 88f + i * 46f, LW, 38f, i == _sgType ? 1 : 0, 12f);
                _sgHits.Add((b.rt, 1, i));
            }

            // 2 · picture
            float y2 = 88f + SgTypes.Length * 46f + 14f;
            LText(ct, Loc.T("gps_signs_step2", "2 · Picture"), 15f, C_WHITE, LX, y2, LW, 24f, TextAlignmentOptions.Left, true);
            LText(ct, Loc.T("gps_signs_folder_hint", "Put JPG or PNG pictures into the Signs folder:"), 12f, L_MUTED, LX, y2 + 24f, LW, 18f, TextAlignmentOptions.Left);
            var ob = LButton(ct, "OpenFolder", Loc.T("gps_signs_open_folder", "Open folder"), LX, y2 + 46f, 146f, 32f, 0, 11f); _sgHits.Add((ob.rt, 6, 0));
            var rb = LButton(ct, "Reload", Loc.T("gps_signs_reload", "Reload"), LX + 154f, y2 + 46f, 146f, 32f, 0, 11f); _sgHits.Add((rb.rt, 7, 0));
            float iy = y2 + 86f;
            // LOGO-GO-1: the game's own logo first, the pictures of the folder under it
            var gl = SgRow(ct, LX, iy, LW, 27f, "★  " + Loc.T("gps_signs_gamelogo", "The game's business logo"), _sgImage == SgGameLogo);
            _sgHits.Add((gl, 2, -2));
            iy += 30f;
            int ipages = Math.Max(1, (_sgImages.Count + SG_IMG_ROWS - 1) / SG_IMG_ROWS);
            _sgImgPage = Mathf.Clamp(_sgImgPage, 0, ipages - 1);
            if (_sgImages.Count == 0)
                LText(ct, Loc.T("gps_signs_no_pictures", "No pictures in the folder yet."), 13f, L_MUTED, LX, iy, LW, 22f, TextAlignmentOptions.Left);
            for (int r = 0; r < SG_IMG_ROWS; r++)
            {
                int i = _sgImgPage * SG_IMG_ROWS + r;
                if (i >= _sgImages.Count) break;
                var row = SgRow(ct, LX, iy + r * 30f, LW, 27f, Path.GetFileName(_sgImages[i]), _sgImages[i] == _sgImage);
                _sgHits.Add((row, 2, i));
            }
            if (ipages > 1) SgPager(ct, LX, iy + SG_IMG_ROWS * 30f + 2f, LW, _sgImgPage, ipages, 10, 11);

            // 3 · businesses
            LText(ct, Loc.T("gps_signs_step3", "3 · Your businesses"), 15f, C_WHITE, RX, 60f, RW - 240f, 24f, TextAlignmentOptions.Left, true);
            var ab = LButton(ct, "All", Loc.T("gps_signs_all", "All"), RX + RW - 230f, 56f, 110f, 30f, 0, 11f); _sgHits.Add((ab.rt, 4, 0));
            var nb = LButton(ct, "None", Loc.T("gps_signs_none", "None"), RX + RW - 112f, 56f, 112f, 30f, 0, 11f); _sgHits.Add((nb.rt, 5, 0));
            int bpages = Math.Max(1, (_sgBiz.Count + SG_BIZ_ROWS - 1) / SG_BIZ_ROWS);
            _sgBizPage = Mathf.Clamp(_sgBizPage, 0, bpages - 1);
            if (_sgBiz.Count == 0)
                LText(ct, Loc.T("gps_signs_no_business", "You have no businesses yet."), 13f, L_MUTED, RX, 96f, RW, 22f, TextAlignmentOptions.Left);
            for (int r = 0; r < SG_BIZ_ROWS; r++)
            {
                int i = _sgBizPage * SG_BIZ_ROWS + r;
                if (i >= _sgBiz.Count) break;
                var row = SgRow(ct, RX, 96f + r * 30f, RW, 27f, (_sgSel.Contains(_sgBiz[i].key) ? "✓  " : "     ") + _sgBiz[i].name, _sgSel.Contains(_sgBiz[i].key));
                _sgHits.Add((row, 3, i));
            }
            if (bpages > 1) SgPager(ct, RX, 96f + SG_BIZ_ROWS * 30f + 2f, RW, _sgBizPage, bpages, 12, 13);

            // preview + size check
            float py = 96f + SG_BIZ_ROWS * 30f + 44f;
            var t = SgTypes[_sgType];
            float pw = t.w == t.h ? 120f : 480f, ph = t.w == t.h ? 120f : 60f;
            var frame = LImg(ct, "PreviewFrame", L_SECT, LRoundSprite(false), 4f).rectTransform;
            LPos(frame, RX, py, pw + 8f, ph + 8f);
            if (_sgPreview != null)
            {
                var ri = MakeGO("Preview", frame).AddComponent<RawImage>();
                ri.texture = _sgPreview; ri.raycastTarget = false;
                LStretch(ri.rectTransform, 4f * LS, 4f * LS, 4f * LS, 4f * LS);
                string fit = _sgPrevW == t.w && _sgPrevH == t.h
                    ? Loc.T("gps_signs_size_ok", "{0} x {1} - fits.", _sgPrevW, _sgPrevH)
                    : Loc.T("gps_signs_size_off", "{0} x {1} - best is {2} x {3}; it will be stretched.", _sgPrevW, _sgPrevH, t.w, t.h);
                LText(ct, fit, 12f, _sgPrevW == t.w && _sgPrevH == t.h ? L_MUTED : L_NOTICE, RX + pw + 20f, py, RW - pw - 20f, ph + 8f, TextAlignmentOptions.Left, false, false, 0f, true);
            }
            else if (_sgImage == SgGameLogo)
                LText(ct, Loc.T("gps_signs_gamelogo_hint", "The logo from BizMan, Settings, Logo (shape, colours, font, your own icons). Apply puts it back on the chosen sign type; the other signs keep theirs."),
                      12f, L_MUTED, RX + pw + 20f, py, RW - pw - 20f, ph + 8f, TextAlignmentOptions.Left, false, false, 0f, true);

            // status + buttons
            if (_sgStatus.Length > 0)
                LText(ct, _sgStatus, 13f, _sgStatusBad ? L_ERR : L_NOTICE, LX, H - 92f, W - 52f, 36f, TextAlignmentOptions.Left, false, false, 0f, true);
            var ap = LButton(ct, "Apply", Loc.T("gps_signs_apply", "Apply"), W - 26f - 150f - 12f - 150f, H - 54f, 150f, 40f, 1, 13f); _sgHits.Add((ap.rt, 8, 0));
            var cl = LButton(ct, "Close", Loc.T("gps_ui_close", "Close"), W - 26f - 150f, H - 54f, 150f, 40f, 0, 13f); _sgHits.Add((cl.rt, 9, 0));
        }

        private RectTransform SgRow(Transform p, float x, float y, float w, float h, string text, bool on)
        {
            var bg = LImg(p, "Row", on ? L_BTN[1] : L_ROW, LRoundSprite(false), 3f);
            LPos(bg.rectTransform, x, y, w, h);
            var t = LText(bg.transform, text, 13f, C_WHITE, 10f, 0f, w - 20f, h, TextAlignmentOptions.Left);
            t.overflowMode = TextOverflowModes.Ellipsis;
            return bg.rectTransform;
        }

        private void SgPager(Transform p, float x, float y, float w, int page, int pages, int actPrev, int actNext)
        {
            var pb = LButton(p, "Prev", "<", x, y, 44f, 28f, page > 0 ? 0 : 4, 12f); if (page > 0) _sgHits.Add((pb.rt, actPrev, 0));
            LText(p, $"{page + 1} / {pages}", 12f, L_MUTED, x + 50f, y, w - 100f, 28f, TextAlignmentOptions.Center);
            var nb = LButton(p, "Next", ">", x + w - 44f, y, 44f, 28f, page < pages - 1 ? 0 : 4, 12f); if (page < pages - 1) _sgHits.Add((nb.rt, actNext, 0));
        }
    }
}
