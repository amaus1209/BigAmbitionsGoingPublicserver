using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BigAmbitionsMP
{
    /// <summary>LOGO-PICKER-1 (user 2026-11-08, BizMan, Settings, "Logo anpassen": "oben links ist ein kleines Fenster -
    /// erstelle das Fenster so, dass es zum Spiel passt und größer in der Mitte; wenn man sein eigenes Bild auswählen möchte,
    /// muss man das Bild anklicken"). The editor's "Browse files" used to open the game's own icons folder
    /// (LogoHelper.GetCustomIconsFolderPath) in the system's file manager. Under Proton that is a tiny Wine window in the top
    /// left corner, and nothing can be picked in it: pictures had to be copied there by hand. This window is in the style of
    /// our other windows, large and centred, and shows the player's folders with a preview of every picture. On Proton the
    /// places are the Linux folders (WINEHOMEDIR), not the empty ones of the Wine prefix. A click on a picture puts it into
    /// that icons folder (square, at most 512 px, PNG) and selects it in the editor; "Save logo changes" keeps it, as with
    /// any shape.
    /// The game tints a shape with the logo colour (BusinessLogoGenerator: SpriteRenderer.color), so a picture in colour
    /// shows as a black block under the default black. There are two ways to take a picture: "Keep colours" (the logo
    /// colour becomes white when the picture has colour) and "Black and white" (it becomes a white figure on a clear ground
    /// like the game's shapes: its outline, or its darker part, and the logo colour colours it).
    /// Paths stay out of the log - Player.log goes to the server with error reports (PLAYER-REPORTS-1).</summary>
    public partial class MPCanvasUI
    {
        private GameObject? _lpRoot;
        private bool _lpOpen, _lpBypass;
        private UI.Smartphone.Apps.BizMan.LogoShapes? _lpShapes;
        private BuildingRegistration? _lpSignReg;           // SIGN-FX-1: the picture goes onto this business's sign
        private string _lpDir = "";
        private static string _lpLastDir = "";
        private static bool _lpBw;                       // false: the picture keeps its colours
        private int _lpPage, _lpPerPage = 18;
        private string _lpStatus = "";
        private bool _lpStatusBad;
        private readonly List<(string path, bool dir, string label)> _lpEntries = new();
        private readonly List<(string path, string label)> _lpPlaces = new();
        private readonly List<(RectTransform rt, int act, int idx)> _lpHits = new();
        private readonly List<Image> _lpTiles = new();
        private readonly List<(RawImage img, TextMeshProUGUI wait, string path)> _lpSlots = new();
        private readonly Dictionary<string, Texture2D?> _lpThumbs = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _lpThumbOrder = new();
        private readonly Queue<string> _lpQueue = new();
        private const string LP_DRIVES = "*drives*";
        private const long LP_THUMB_MAX = 8L * 1024 * 1024, LP_PICK_MAX = 40L * 1024 * 1024;
        private const int LP_THUMB_PX = 160, LP_SHAPE_PX = 512, LP_CACHE = 120, LP_MAX_DIRS = 400, LP_MAX_FILES = 800;
        private static readonly Color LP_WELL = LC(0x8A939B), LP_HOVER = LC(0x6F7D88), LP_FOLDER = LC(0xE3B341), LP_FOLDER_LIP = LC(0xF2C95C);

        /// <summary>From the editor's "Browse files" (LogoShapesExtra): opens the window; false = let the game do its own.</summary>
        internal bool LogoPickerOpen(UI.Smartphone.Apps.BizMan.LogoShapes shapes)
        {
            if (_lpBypass) return false;
            if (!_built || _canvasGO == null || shapes == null) return false;
            _lpShapes = shapes;
            _lpSignReg = null;
            _lpOpen = true;
            LpPlaces();
            string start = _lpLastDir;
            bool ok = start == LP_DRIVES;
            if (!ok && start.Length > 0) { try { ok = Directory.Exists(start); } catch { } }
            if (!ok) start = _lpPlaces.Count > 0 ? _lpPlaces[0].path : LP_DRIVES;
            LpGo(start);
            bool wine = false;
            try { wine = ModEntry.RunningOnWine(); } catch { }
            Plugin.Logger.LogInfo($"[Logo] picture window opened: {_lpPlaces.Count} places{(wine ? " (Proton/Wine: the Linux folders)" : "")} (LOGO-PICKER-1).");
            return true;
        }

        /// <summary>SIGN-FX-1: the same window for the face of a business's sign (the game's sign window, "Own picture").</summary>
        internal bool SignPickerOpen(BuildingRegistration reg)
        {
            if (!_built || _canvasGO == null || reg == null) return false;
            _lpShapes = null;
            _lpSignReg = reg;
            _lpOpen = true;
            LpPlaces();
            string start = _lpLastDir;
            bool ok = start == LP_DRIVES;
            if (!ok && start.Length > 0) { try { ok = Directory.Exists(start); } catch { } }
            if (!ok) start = _lpPlaces.Count > 0 ? _lpPlaces[0].path : LP_DRIVES;
            LpGo(start);
            Plugin.Logger.LogInfo("[Logo] picture window opened for a sign (SIGN-FX-1).");
            return true;
        }

        /// <summary>Main thread, every frame from Update.</summary>
        private void TickLogoPicker()
        {
            if (!_lpOpen) return;
            try
            {
                bool logoGone = _lpSignReg == null && (_lpShapes == null || !IsAlive(_lpShapes) || !_lpShapes.gameObject.activeInHierarchy);
                if (logoGone || SaveGameManager.Current == null)
                {
                    LpClose();
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Escape)) { LpClose(); ConsumeEscape(); return; }
                LpThumbStep();
                var mp = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                foreach (var t in _lpTiles)
                {
                    if (t == null) continue;
                    var c = RectHit(t.rectTransform, mp) ? LP_HOVER : L_ROW;
                    if (t.color != c) t.color = c;
                }
                float wheel = Input.mouseScrollDelta.y;
                if (wheel != 0f)
                {
                    int pages = Math.Max(1, (_lpEntries.Count + _lpPerPage - 1) / _lpPerPage);
                    int np = Mathf.Clamp(_lpPage + (wheel < 0f ? 1 : -1), 0, pages - 1);
                    if (np != _lpPage) { _lpPage = np; LpRebuild(); return; }
                }
                if (!Input.GetMouseButtonDown(0)) return;
                foreach (var (rt, act, idx) in _lpHits)
                    if (rt != null && rt.gameObject.activeInHierarchy && RectHit(rt, mp)) { LpAct(act, idx); return; }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Logo] picture window: {ex.GetType().Name}: {ex.Message}"); }
        }

        private void LpAct(int act, int idx)
        {
            switch (act)
            {
                case 1: if (idx >= 0 && idx < _lpPlaces.Count) LpGo(_lpPlaces[idx].path); break;
                case 2: LpUp(); break;
                case 3:
                    if (idx < 0 || idx >= _lpEntries.Count) break;
                    if (_lpEntries[idx].dir) LpGo(_lpEntries[idx].path); else LpPick(_lpEntries[idx].path);
                    break;
                case 4: if (_lpPage > 0) { _lpPage--; LpRebuild(); } break;
                case 5: _lpPage++; LpRebuild(); break;
                case 6: if (_lpBw) { _lpBw = false; LpRebuild(); } break;
                case 7: if (!_lpBw) { _lpBw = true; LpRebuild(); } break;
                case 8: LpFileManager(); break;
                case 9: LpClose(); break;
            }
        }

        private void LpClose()
        {
            _lpOpen = false;
            ShieldPressUntilRelease();   // NAV-WALK-CLICK-1
            _lpHits.Clear(); _lpTiles.Clear(); _lpSlots.Clear(); _lpQueue.Clear();
            if (_lpRoot != null) { try { Destroy(_lpRoot); } catch { } _lpRoot = null; }
            foreach (var t in _lpThumbs.Values) if (t != null) { try { Destroy(t); } catch { } }
            _lpThumbs.Clear(); _lpThumbOrder.Clear();
            _lpShapes = null;
            _lpSignReg = null;
        }

        private void LpStatus(string msg, bool bad)
        {
            _lpStatus = msg; _lpStatusBad = bad;
            if (_lpOpen) LpRebuild();
        }

        // ── places and folders ──

        private void LpPlaces()
        {
            _lpPlaces.Clear();
            bool wine = false;
            try { wine = ModEntry.RunningOnWine(); } catch { }
            string home = wine ? LpLinuxHome() : "";
            if (home.Length == 0) home = LpSpecial(Environment.SpecialFolder.UserProfile);
            string H(string sub) => home.Length > 0 ? Path.Combine(home, sub) : "";
            LpAddPlace(Loc.T("gps_logopick_pictures", "Pictures"), H("Bilder"), H("Pictures"), LpSpecial(Environment.SpecialFolder.MyPictures));
            LpAddPlace(Loc.T("gps_logopick_downloads", "Downloads"), H("Downloads"));
            LpAddPlace(Loc.T("gps_logopick_desktop", "Desktop"), H("Schreibtisch"), H("Desktop"), LpSpecial(Environment.SpecialFolder.DesktopDirectory));
            LpAddPlace(Loc.T("gps_logopick_documents", "Documents"), H("Dokumente"), H("Documents"), LpSpecial(Environment.SpecialFolder.MyDocuments));
            LpAddPlace(Loc.T("gps_logopick_home", "Home folder"), home);
            string own = "";
            try { own = LogoHelper.GetCustomIconsFolderPath(); Directory.CreateDirectory(own); } catch { }
            LpAddPlace(Loc.T("gps_logopick_own", "Your logo pictures"), own);
            _lpPlaces.Add((LP_DRIVES, Loc.T("gps_logopick_drives", "Drives")));
        }

        private void LpAddPlace(string label, params string[] paths)
        {
            foreach (var p in paths)
            {
                if (string.IsNullOrEmpty(p)) continue;
                try { if (!Directory.Exists(p)) continue; } catch { continue; }
                foreach (var have in _lpPlaces) if (LpSame(have.path, p)) return;
                _lpPlaces.Add((p, label));
                return;
            }
        }

        private static string LpSpecial(Environment.SpecialFolder f)
        {
            try { return Environment.GetFolderPath(f) ?? ""; } catch { return ""; }
        }

        /// <summary>Proton/Wine: the Linux home folder as a Windows path (Z:\home\name). Wine keeps it in WINEHOMEDIR
        /// ("\??\Z:\home\name"); the folders of the Wine prefix (C:\users\steamuser) are mostly empty.</summary>
        private static string LpLinuxHome()
        {
            try
            {
                string w = Environment.GetEnvironmentVariable("WINEHOMEDIR") ?? "";
                if (w.StartsWith("\\??\\", StringComparison.Ordinal)) w = w.Substring(4);
                if (w.Length > 0 && Directory.Exists(w)) return w;
                string h = Environment.GetEnvironmentVariable("HOME") ?? "";
                if (h.StartsWith("/", StringComparison.Ordinal))
                {
                    string z = "Z:" + h.Replace('/', '\\');
                    if (Directory.Exists(z)) return z;
                }
                if (Directory.Exists("Z:\\home"))
                {
                    var homes = Directory.GetDirectories("Z:\\home");
                    if (homes.Length == 1) return homes[0];
                }
            }
            catch { }
            return "";
        }

        private static bool LpSame(string a, string b)
        {
            if (a == b) return true;
            if (a == LP_DRIVES || b == LP_DRIVES || a.Length == 0 || b.Length == 0) return false;
            try
            {
                return string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private void LpGo(string path)
        {
            _lpDir = path;
            _lpLastDir = path;
            _lpPage = 0;
            _lpStatus = ""; _lpStatusBad = false;
            LpScan();
            LpRebuild();
        }

        private void LpUp()
        {
            if (_lpDir == LP_DRIVES) return;
            string? parent = null;
            try
            {
                string root = Path.GetPathRoot(_lpDir) ?? "";
                if (!LpSame(root, _lpDir)) parent = Directory.GetParent(_lpDir.TrimEnd('\\', '/'))?.FullName;
            }
            catch { }
            LpGo(string.IsNullOrEmpty(parent) ? LP_DRIVES : parent!);
        }

        private void LpScan()
        {
            _lpEntries.Clear();
            if (_lpDir == LP_DRIVES)
            {
                try
                {
                    foreach (var d in DriveInfo.GetDrives())
                    {
                        try { if (!d.IsReady) continue; } catch { continue; }
                        _lpEntries.Add((d.RootDirectory.FullName, true, d.Name.TrimEnd('\\', '/')));
                    }
                }
                catch { }
                return;
            }
            try
            {
                var dirs = new List<string>();
                foreach (var d in Directory.EnumerateDirectories(_lpDir))
                {
                    string n = Path.GetFileName(d);
                    if (n.Length == 0 || n[0] == '.' || n[0] == '$') continue;
                    try { if ((File.GetAttributes(d) & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue; } catch { continue; }
                    dirs.Add(d);
                    if (dirs.Count >= LP_MAX_DIRS) break;
                }
                dirs.Sort(StringComparer.OrdinalIgnoreCase);
                var files = new List<string>();
                foreach (var f in Directory.EnumerateFiles(_lpDir))
                {
                    string e = Path.GetExtension(f).ToLowerInvariant();
                    if (e != ".png" && e != ".jpg" && e != ".jpeg") continue;
                    if (Path.GetFileName(f).StartsWith(".", StringComparison.Ordinal)) continue;
                    files.Add(f);
                    if (files.Count >= LP_MAX_FILES) break;
                }
                files.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (var d in dirs) _lpEntries.Add((d, true, Path.GetFileName(d)));
                foreach (var f in files) _lpEntries.Add((f, false, Path.GetFileName(f)));
            }
            catch (Exception ex)
            {
                _lpStatus = Loc.T("gps_logopick_noaccess", "This folder cannot be opened.");
                _lpStatusBad = true;
                Plugin.Logger.LogInfo($"[Logo] picture window: a folder could not be read ({ex.GetType().Name}).");
            }
        }

        /// <summary>The game's own way, kept as a button: its icons folder in the system's file manager.</summary>
        private void LpFileManager()
        {
            var shapes = _lpShapes;
            if (shapes == null) return;
            try
            {
                _lpBypass = true;
                shapes.BrowseFiles();
                LpStatus(Loc.T("gps_logopick_fm_open", "The folder is open in the file manager. Pictures put there show under \"Your logo pictures\"."), false);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Logo] picture window: file manager: {ex.GetType().Name}"); }
            finally { _lpBypass = false; }
        }

        // ── the window ──

        private static string LpShow(string dir)
        {
            if (dir == LP_DRIVES) return Loc.T("gps_logopick_drives", "Drives");
            return dir.Length > 90 ? "..." + dir.Substring(dir.Length - 87) : dir;
        }

        private void LpRebuild()
        {
            if (_canvasGO == null) return;
            if (_lpRoot != null) { try { Destroy(_lpRoot); } catch { } _lpRoot = null; }
            _lpHits.Clear(); _lpTiles.Clear(); _lpSlots.Clear(); _lpQueue.Clear();
            _lpRoot = LMakeRoot("BAMP_LogoPicker");
            _lpRoot.transform.SetAsLastSibling();

            float scale = Mathf.Max(0.1f, UiScale) * LS;
            float W = Mathf.Min(1040f, Screen.width / scale - 60f), H = Mathf.Min(660f, Screen.height / scale - 50f);
            const float LX = 24f, LW = 210f, TW = 118f, TH = 136f, GX = 8f, GY = 8f, GY0 = 104f;
            float RX = LX + LW + 24f, RW = W - RX - 24f;
            int cols = Mathf.Max(1, Mathf.FloorToInt((RW + GX) / (TW + GX)));
            int rows = Mathf.Max(1, Mathf.FloorToInt((H - 118f - GY0 + GY) / (TH + GY)));
            _lpPerPage = cols * rows;

            bool sign = _lpSignReg != null;
            var card = LMakeCard(_lpRoot.transform, "Card", sign ? Loc.T("gps_signfx_pick_title", "Picture for the sign") : Loc.T("gps_logopick_title", "Picture for the logo"), W, H);
            var ct = card.go.transform;

            // places
            LText(ct, Loc.T("gps_logopick_places", "Places"), 15f, C_WHITE, LX, 58f, LW, 24f, TextAlignmentOptions.Left, true);
            float py = 88f;
            for (int i = 0; i < _lpPlaces.Count; i++)
            {
                var b = LButton(ct, "Place" + i, _lpPlaces[i].label, LX, py, LW, 34f, LpSame(_lpPlaces[i].path, _lpDir) ? 1 : 0, 11f);
                _lpHits.Add((b.rt, 1, i));
                py += 40f;
            }

            // how the picture is taken (the logo only - a sign shows the picture as it is)
            py += 14f;
            if (!sign) {
            LText(ct, Loc.T("gps_logopick_mode", "Use as"), 15f, C_WHITE, LX, py, LW, 24f, TextAlignmentOptions.Left, true);
            var mc = LButton(ct, "Colour", Loc.T("gps_logopick_colour", "Keep colours"), LX, py + 30f, LW, 34f, _lpBw ? 0 : 1, 11f);
            _lpHits.Add((mc.rt, 6, 0));
            var mb = LButton(ct, "Bw", Loc.T("gps_logopick_bw", "Black and white"), LX, py + 70f, LW, 34f, _lpBw ? 1 : 0, 11f);
            _lpHits.Add((mb.rt, 7, 0));
            LText(ct, _lpBw ? Loc.T("gps_logopick_bw_hint", "The picture becomes a shape like the game's logos; the logo colour colours it.")
                            : Loc.T("gps_logopick_colour_hint", "The picture keeps its colours; the logo colour becomes white."),
                  12f, L_MUTED, LX, py + 112f, LW, 60f, TextAlignmentOptions.TopLeft, false, false, 0f, true);
            }

            // the folder
            bool canUp = _lpDir != LP_DRIVES;
            var up = LButton(ct, "Up", Loc.T("gps_logopick_up", "Up"), RX, 56f, 140f, 34f, canUp ? 0 : 4, 11f);
            if (canUp) _lpHits.Add((up.rt, 2, 0));
            var pt = LText(ct, LpShow(_lpDir), 13f, L_MUTED, RX + 152f, 56f, RW - 152f, 34f, TextAlignmentOptions.Left);
            pt.overflowMode = TextOverflowModes.Ellipsis;

            int pages = Math.Max(1, (_lpEntries.Count + _lpPerPage - 1) / _lpPerPage);
            _lpPage = Mathf.Clamp(_lpPage, 0, pages - 1);
            if (_lpEntries.Count == 0 && !_lpStatusBad)
                LText(ct, Loc.T("gps_logopick_empty", "No pictures and no folders here."), 14f, L_MUTED, RX, GY0 + 6f, RW, 24f, TextAlignmentOptions.Left);
            for (int k = 0; k < _lpPerPage; k++)
            {
                int i = _lpPage * _lpPerPage + k;
                if (i >= _lpEntries.Count) break;
                var e = _lpEntries[i];
                float x = RX + (k % cols) * (TW + GX), y = GY0 + (k / cols) * (TH + GY);
                var tile = LImg(ct, "Tile", L_ROW, LRoundSprite(false), 4f);
                LPos(tile.rectTransform, x, y, TW, TH);
                _lpTiles.Add(tile);
                _lpHits.Add((tile.rectTransform, 3, i));
                if (e.dir) LpFolderIcon(tile.transform, TW);
                else
                {
                    var well = LImg(tile.transform, "Well", LP_WELL, LRoundSprite(false), 3f);
                    LPos(well.rectTransform, 8f, 8f, TW - 16f, TW - 16f);
                    var ri = MakeGO("Thumb", well.transform).AddComponent<RawImage>();
                    ri.raycastTarget = false;
                    LStretch(ri.rectTransform, 4f * LS, 4f * LS, 4f * LS, 4f * LS);
                    var wait = LText(well.transform, "...", 16f, C_WHITE, 0f, 0f, TW - 16f, TW - 16f, TextAlignmentOptions.Center);
                    if (_lpThumbs.TryGetValue(e.path, out var th))
                    {
                        if (th != null) { ri.texture = th; wait.gameObject.SetActive(false); }
                        else { ri.enabled = false; wait.text = "?"; }
                    }
                    else { ri.enabled = false; _lpQueue.Enqueue(e.path); }
                    _lpSlots.Add((ri, wait, e.path));
                }
                var nm = LText(tile.transform, e.label, 11f, C_WHITE, 6f, TW - 6f, TW - 12f, 24f, TextAlignmentOptions.Center);
                nm.overflowMode = TextOverflowModes.Ellipsis;
            }
            if (pages > 1)
            {
                float yy = GY0 + rows * (TH + GY) + 2f;
                var pb = LButton(ct, "Prev", "<", RX, yy, 50f, 30f, _lpPage > 0 ? 0 : 4, 12f);
                if (_lpPage > 0) _lpHits.Add((pb.rt, 4, 0));
                LText(ct, $"{_lpPage + 1} / {pages}", 13f, L_MUTED, RX + 56f, yy, RW - 112f, 30f, TextAlignmentOptions.Center);
                var nb = LButton(ct, "Next", ">", RX + RW - 50f, yy, 50f, 30f, _lpPage < pages - 1 ? 0 : 4, 12f);
                if (_lpPage < pages - 1) _lpHits.Add((nb.rt, 5, 0));
            }

            // hint or status, buttons
            float by = H - 54f, bw1 = 150f, bw2 = 250f;
            var cl = LButton(ct, "Close", Loc.T("gps_ui_close", "Close"), W - 24f - bw1, by, bw1, 40f, 0, 13f);
            _lpHits.Add((cl.rt, 9, 0));
            if (!sign)
            {
                var fm = LButton(ct, "FileManager", Loc.T("gps_logopick_filemanager", "Folder in the file manager"), W - 24f - bw1 - 12f - bw2, by, bw2, 40f, 0, 11f);
                _lpHits.Add((fm.rt, 8, 0));
            }
            bool st = _lpStatus.Length > 0;
            string hint = sign ? Loc.T("gps_signfx_pick_hint", "Click a picture to put it on the sign. A wide picture (8 to 1) fills the sign best.")
                               : Loc.T("gps_logopick_hint", "Click a picture to use it for the logo. A PNG with a clear background looks best.");
            LText(ct, st ? _lpStatus : hint,
                  12f, st ? (_lpStatusBad ? L_ERR : L_NOTICE) : L_MUTED, RX, by - 4f, W - 24f - bw1 - 12f - bw2 - 16f - RX, 48f,
                  TextAlignmentOptions.Left, false, false, 0f, true);
        }

        private void LpFolderIcon(Transform tile, float tw)
        {
            var tab = LImg(tile, "Tab", LP_FOLDER, LRoundSprite(false), 3f);
            LPos(tab.rectTransform, 24f, 30f, 36f, 16f);
            var body = LImg(tile, "Back", LP_FOLDER, LRoundSprite(false), 4f);
            LPos(body.rectTransform, 24f, 38f, tw - 48f, 58f);
            var lip = LImg(tile, "Front", LP_FOLDER_LIP, LRoundSprite(false), 4f);
            LPos(lip.rectTransform, 24f, 48f, tw - 48f, 48f);
        }

        // ── previews ──

        /// <summary>One picture a frame: big photos take a moment to read, and the window stays smooth.</summary>
        private void LpThumbStep()
        {
            while (_lpQueue.Count > 0)
            {
                string path = _lpQueue.Dequeue();
                if (_lpThumbs.ContainsKey(path)) { LpFill(path); continue; }
                Texture2D? th = null;
                try
                {
                    var fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > 0 && fi.Length <= LP_THUMB_MAX)
                    {
                        var src = new Texture2D(2, 2, TextureFormat.RGBA32, true) { hideFlags = HideFlags.HideAndDontSave };
                        try { if (src.LoadImage(File.ReadAllBytes(path))) th = LpSquare(src, LP_THUMB_PX, false); }
                        finally { Destroy(src); }
                    }
                }
                catch { th = null; }
                _lpThumbs[path] = th;
                _lpThumbOrder.Add(path);
                LpTrimCache();
                LpFill(path);
                return;
            }
        }

        private void LpFill(string path)
        {
            if (!_lpThumbs.TryGetValue(path, out var th)) return;
            foreach (var (img, wait, p) in _lpSlots)
            {
                if (!string.Equals(p, path, StringComparison.OrdinalIgnoreCase) || img == null) continue;
                if (th != null) { img.texture = th; img.enabled = true; if (wait != null) wait.gameObject.SetActive(false); }
                else if (wait != null) wait.text = "?";
            }
        }

        private void LpTrimCache()
        {
            int guard = _lpThumbOrder.Count;
            while (_lpThumbOrder.Count > LP_CACHE && guard-- > 0)
            {
                string old = _lpThumbOrder[0];
                _lpThumbOrder.RemoveAt(0);
                if (_lpSlots.Exists(s => string.Equals(s.path, old, StringComparison.OrdinalIgnoreCase))) { _lpThumbOrder.Add(old); continue; }
                if (_lpThumbs.TryGetValue(old, out var t) && t != null) { try { Destroy(t); } catch { } }
                _lpThumbs.Remove(old);
            }
        }

        /// <summary>The picture centred on a clear square (the game stretches a shape to a square), at most max pixels, read
        /// from the mipmap level nearest the new size.</summary>
        private static Texture2D LpSquare(Texture2D src, int max, bool bw)
        {
            int w = Mathf.Max(1, src.width), h = Mathf.Max(1, src.height);
            int size = Mathf.Clamp(Mathf.Max(w, h), 16, max);
            float scale = (float)size / Mathf.Max(w, h);
            int dw = Mathf.Clamp(Mathf.RoundToInt(w * scale), 1, size), dh = Mathf.Clamp(Mathf.RoundToInt(h * scale), 1, size);
            int ox = (size - dw) / 2, oy = (size - dh) / 2;
            int mip = 0;
            for (float f = 1f / scale; f >= 2f && mip < src.mipmapCount - 1; f *= 0.5f) mip++;
            var px = new Color32[size * size];
            for (int y = 0; y < dh; y++)
            {
                float v = (y + 0.5f) / dh;
                int row = (oy + y) * size + ox;
                for (int x = 0; x < dw; x++) px[row + x] = src.GetPixelBilinear((x + 0.5f) / dw, v, mip);
            }
            if (bw) LpToShape(px, size, ox, oy, dw, dh);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>Black and white: a white figure on a clear ground, as the game's shapes are, so the logo colour colours it.
        /// A picture with a clear background keeps its outline; otherwise the darker part becomes the figure (the lighter one
        /// on a dark background).</summary>
        private static void LpToShape(Color32[] px, int size, int ox, int oy, int dw, int dh)
        {
            int n = 0, clear = 0, bn = 0;
            double border = 0;
            for (int y = 0; y < dh; y++)
                for (int x = 0; x < dw; x++)
                {
                    var c = px[(oy + y) * size + ox + x];
                    n++;
                    if (c.a < 230) clear++;
                    if ((x == 0 || y == 0 || x == dw - 1 || y == dh - 1) && c.a >= 128) { border += (0.299 * c.r + 0.587 * c.g + 0.114 * c.b) / 255.0; bn++; }
                }
            bool byAlpha = clear > n * 0.03;
            bool darkGround = bn > 0 && border / bn < 0.45;
            for (int y = 0; y < dh; y++)
                for (int x = 0; x < dw; x++)
                {
                    int i = (oy + y) * size + ox + x;
                    var c = px[i];
                    float a;
                    if (byAlpha) a = c.a / 255f;
                    else
                    {
                        float lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                        a = Mathf.Clamp01(((darkGround ? lum : 1f - lum) - 0.3f) / 0.35f);
                    }
                    px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
        }

        /// <summary>Whether a picture shows anything but white: one that is only white, like the game's shapes, keeps the logo
        /// colour.</summary>
        private static bool LpHasColour(Texture2D tex)
        {
            var px = tex.GetPixels32();
            int seen = 0, other = 0;
            for (int i = 0; i < px.Length; i += 7)
            {
                var c = px[i];
                if (c.a < 64) continue;
                seen++;
                if (c.r < 225 || c.g < 225 || c.b < 225) other++;
            }
            return seen > 0 && other > seen * 0.04;
        }

        // ── taking a picture ──

        private void LpPick(string path)
        {
            if (_lpSignReg != null) { LpPickForSign(path, _lpSignReg); return; }
            var shapes = _lpShapes;
            if (shapes == null) { LpClose(); return; }
            Texture2D? src = null, sq = null;
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists || fi.Length == 0 || fi.Length > LP_PICK_MAX) { LpStatus(Loc.T("gps_logopick_bad", "This picture cannot be read."), true); return; }
                src = new Texture2D(2, 2, TextureFormat.RGBA32, true) { hideFlags = HideFlags.HideAndDontSave };
                if (!src.LoadImage(File.ReadAllBytes(path))) { LpStatus(Loc.T("gps_logopick_bad", "This picture cannot be read."), true); return; }
                bool bw = _lpBw;
                sq = LpSquare(src, LP_SHAPE_PX, bw);
                bool white = !bw && LpHasColour(sq);
                string dir = LogoHelper.GetCustomIconsFolderPath();
                Directory.CreateDirectory(dir);
                string name = LpFreeName(dir, Path.GetFileNameWithoutExtension(path) + (bw ? "-sw" : ""));
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), sq.EncodeToPNG());
                int pw = src.width, ph = src.height;
                LpClose();
                shapes.RefreshLogoShapes();
                shapes.SelectShape(name);
                if (white) LpLogoWhite(shapes);
                Plugin.Logger.LogInfo($"[Logo] picture window: a picture of {pw} x {ph} px is in the logo now " +
                                      $"({(bw ? "black and white, the logo colour colours it" : white ? "in its colours, the logo colour white" : "white, the logo colour stays")}); " +
                                      "the editor's button saves it (LOGO-PICKER-1).");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[Logo] picture window: the picture could not be taken ({ex.GetType().Name}).");
                if (_lpOpen) LpStatus(Loc.T("gps_logopick_bad", "This picture cannot be read."), true);
            }
            finally
            {
                if (src != null) Destroy(src);
                if (sq != null) Destroy(sq);
            }
        }

        /// <summary>A file name for the game's icons folder: plain letters, never one of the game's own shape names.</summary>
        private static string LpFreeName(string dir, string raw)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch0 in raw)
            {
                if (sb.Length >= 40) break;
                char ch = char.ToLowerInvariant(ch0);
                if (ch < 128 && (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')) sb.Append(ch);
                else if (ch == ' ' || ch == '.') sb.Append('_');
                else if (ch == 'ä') sb.Append("ae");
                else if (ch == 'ö') sb.Append("oe");
                else if (ch == 'ü') sb.Append("ue");
                else if (ch == 'ß') sb.Append("ss");
            }
            string b = sb.ToString().Trim('_', '-');
            if (b.Length == 0) b = "logo";
            var builtIn = LogoHelper.AvailableIcons;
            string n = b;
            int k = 2;
            while (File.Exists(Path.Combine(dir, n + ".png")) || (builtIn != null && builtIn.Contains(n))) n = b + "-" + k++;
            return n;
        }

        // ── SIGN-FX-1: a picture or the business logo on the face of a sign ──

        private const int SignW = 1024, SignH = 128;   // the game's WideSign image (MPSignManager.SgTypes)

        private void LpPickForSign(string path, BuildingRegistration reg)
        {
            Texture2D? src = null, fit = null;
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists || fi.Length == 0 || fi.Length > LP_PICK_MAX) { LpStatus(Loc.T("gps_logopick_bad", "This picture cannot be read."), true); return; }
                src = new Texture2D(2, 2, TextureFormat.RGBA32, true) { hideFlags = HideFlags.HideAndDontSave };
                if (!src.LoadImage(File.ReadAllBytes(path))) { LpStatus(Loc.T("gps_logopick_bad", "This picture cannot be read."), true); return; }
                int pw = src.width, ph = src.height;
                string k = GameStateReader.AddressKey(reg);
                int beside = (SignFx.Get(k) & SignFx.LogoMask) >> 4;
                if (beside != 0)
                {   // SIGN-FX-3 (user 2026-11-08: "geht nur entweder eigenes Bild oder Firmenlogo"): with a side chosen, the own
                    // picture is the logo beside the name - kept for the business, the sign is made anew with it
                    string? logoFile = SignLogoFile(reg.BusinessName ?? "");
                    if (logoFile != null) { Directory.CreateDirectory(Path.GetDirectoryName(logoFile)!); File.WriteAllBytes(logoFile, src.EncodeToPNG()); }
                    LpClose();
                    SignPutGameLogo(reg, beside);
                    Plugin.Logger.LogInfo($"[SignFx] a picture of {pw} x {ph} px is the logo beside the name now (SIGN-FX-3).");
                    return;
                }
                fit = LpFit(src, SignW, SignH);
                bool ok = SignWrite(reg, LogoSize.WideSign, fit.EncodeToJPG(92));
                LpClose();
                Plugin.Logger.LogInfo($"[SignFx] a picture of {pw} x {ph} px is on the sign now: {(ok ? "done" : "failed")} (SIGN-FX-1).");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[SignFx] the picture could not go on the sign ({ex.GetType().Name}).");
                if (_lpOpen) LpStatus(Loc.T("gps_logopick_bad", "This picture cannot be read."), true);
            }
            finally
            {
                if (src != null) Destroy(src);
                if (fit != null) Destroy(fit);
            }
        }

        /// <summary>The picture inside w x h on black, its proportions kept (a sign face is wide).</summary>
        private static Texture2D LpFit(Texture2D src, int w, int h)
        {
            int sw = Mathf.Max(1, src.width), sh = Mathf.Max(1, src.height);
            float scale = Mathf.Min((float)w / sw, (float)h / sh);
            int dw = Mathf.Clamp(Mathf.RoundToInt(sw * scale), 1, w), dh = Mathf.Clamp(Mathf.RoundToInt(sh * scale), 1, h);
            int ox = (w - dw) / 2, oy = (h - dh) / 2;
            int mip = 0;
            for (float f = 1f / scale; f >= 2f && mip < src.mipmapCount - 1; f *= 0.5f) mip++;
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 255);
            for (int y = 0; y < dh; y++)
            {
                float v = (y + 0.5f) / dh;
                int row = (oy + y) * w + ox;
                for (int x = 0; x < dw; x++)
                {
                    Color c = src.GetPixelBilinear((x + 0.5f) / dw, v, mip);
                    var under = new Color(0f, 0f, 0f, 1f);
                    px[row + x] = Color.Lerp(under, new Color(c.r, c.g, c.b, 1f), c.a);   // a clear picture lies on the black face
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>Writes one sign image of a business (the old one into SignBackups first) and lets the game and the other
        /// players show it (GameStatePatcher.RefreshOwnLogo), as the shop signs window F7 does.</summary>
        private static bool SignWrite(BuildingRegistration reg, LogoSize size, byte[] jpg)
        {
            try
            {
                string name = reg.BusinessName ?? "";
                string? dir = BusinessSync.SafeLogoDir(name);
                if (dir == null) return false;
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, size.ToString() + ".jpg");
                if (File.Exists(file))
                {
                    string bdir = Path.Combine(MPConfig.DataRootPath, "SignBackups", LogoHelper.GetBusinessNamePathSafe(name));
                    Directory.CreateDirectory(bdir);
                    File.Copy(file, Path.Combine(bdir, $"{size}-{DateTime.Now:yyyyMMdd-HHmmss}.jpg"), true);
                }
                File.WriteAllBytes(file, jpg);
                GameStatePatcher.RefreshOwnLogo(name, GameStateReader.AddressKey(reg));
                return true;
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[SignFx] sign picture: {ex.Message}"); return false; }
        }

        /// <summary>SIGN-FX-1 "Business logo": the logo from BizMan's editor on the sign again - the game makes all its sizes, the
        /// two other ones are put back as they were (LOGO-GO-1, MPSignManager).</summary>
        internal void SignPutGameLogo(BuildingRegistration reg, int beside = 0)
        {
            try
            {
                string name = reg.BusinessName ?? "";
                string? dir = BusinessSync.SafeLogoDir(name);
                if (dir == null || BusinessLogoGenerator.Instance == null) return;
                Directory.CreateDirectory(dir);
                var keep = new Dictionary<string, byte[]>();
                foreach (var other in new[] { LogoSize.SquareSign, LogoSize.Billboard })
                {
                    string f = Path.Combine(dir, other.ToString() + ".jpg");
                    if (File.Exists(f)) keep[f] = File.ReadAllBytes(f);
                }
                string file = Path.Combine(dir, LogoSize.WideSign.ToString() + ".jpg");
                if (File.Exists(file))
                {
                    string bdir = Path.Combine(MPConfig.DataRootPath, "SignBackups", LogoHelper.GetBusinessNamePathSafe(name));
                    Directory.CreateDirectory(bdir);
                    File.Copy(file, Path.Combine(bdir, $"{LogoSize.WideSign}-{DateTime.Now:yyyyMMdd-HHmmss}.jpg"), true);
                }
                string key = GameStateReader.AddressKey(reg);
                var settings = reg.logoSettings;
                Action putBack = () =>
                {
                    try { foreach (var kv in keep) File.WriteAllBytes(kv.Key, kv.Value); } catch { }
                    if (beside != 0) { try { ComposeBeside(file, reg.logoSettings, beside, SignLogoFile(name)); } catch (Exception ex) { Plugin.Logger.LogWarning($"[SignFx] logo beside the name: {ex.Message}"); } }
                    try { GameStatePatcher.RefreshOwnLogo(name, key); } catch { }
                };
                var type = Helpers.BusinessTypeHelper.GetData(reg.businessTypeName);
                if ((settings == null || string.IsNullOrEmpty(settings.logoShape)) && type?.logoShapes != null && type.logoShapes.Count > 0)
                    settings = reg.logoSettings = LogoHelper.GenerateLogoSetting(reg.businessTypeName);
                if (settings != null && !string.IsNullOrEmpty(settings.logoShape))
                    BusinessLogoGenerator.Create(reg.BusinessName, settings, dir, true, () => putBack());
                else if (type != null) { BusinessLogoGenerator.Instance.GenerateWarehouseLogo(reg.BusinessName, type, dir, true); putBack(); }
                Plugin.Logger.LogInfo("[SignFx] the business logo is on the sign again (SIGN-FX-1).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[SignFx] business logo on the sign: {ex.Message}"); }
        }

        /// <summary>SIGN-FX-2 (user 2026-11-08: "Auswahl, wo das Firmenlogo ist neben dem Firmennamen - links, rechts oder beide
        /// Seiten"): the game's wide sign shows the name only. After the game made it anew, the name is cut out of it and laid
        /// out again with the logo shape (in the logo's colour on the sign's ground) on its left (1), right (2) or both sides
        /// (3); everything is made smaller when it would not fit.</summary>
        /// <summary>SIGN-FX-3: the own picture chosen as the logo beside the name, kept per business (null = no place for it).</summary>
        internal static string? SignLogoFile(string businessName)
        {
            if (string.IsNullOrWhiteSpace(businessName)) return null;
            try { return Path.Combine(MPConfig.DataRootPath, "SignLogos", LogoHelper.GetBusinessNamePathSafe(businessName) + ".png"); }
            catch { return null; }
        }

        private static void ComposeBeside(string wideFile, LogoSettings? settings, int mode, string? ownLogo = null)
        {
            bool own = ownLogo != null && File.Exists(ownLogo);
            if (settings == null || (!own && string.IsNullOrEmpty(settings.logoShape)) || !File.Exists(wideFile)) return;
            Texture2D? wide = null, shape = null, outTex = null;
            try
            {
                wide = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                if (!wide.LoadImage(File.ReadAllBytes(wideFile))) return;
                if (own)
                {   // SIGN-FX-3: the player's own picture, as it is (its colours, its clear parts)
                    shape = new Texture2D(2, 2, TextureFormat.RGBA32, true) { hideFlags = HideFlags.HideAndDontSave };
                    if (!shape.LoadImage(File.ReadAllBytes(ownLogo!))) { Destroy(shape); shape = null; own = false; }
                }
                if (shape == null && !string.IsNullOrEmpty(settings.logoShape))
                {
                    string shapePath = LogoHelper.AvailableIcons != null && LogoHelper.AvailableIcons.Contains(settings.logoShape)
                        ? Path.Combine(LogoHelper.GetBuildInIconsFolder(), settings.logoShape + ".png") : LogoHelper.GetCustomIconPath(settings.logoShape);
                    shape = LogoHelper.GetLogoShapeTexture(shapePath);   // LOGO-SHAPES-1 finds ours in the mod folder
                }
                if (shape == null) return;
                int w = wide.width, h = wide.height;
                var src = wide.GetPixels32();
                Color bgC = settings.backgroundColor, logoC = settings.logoColor;
                var bg = (Color32)new Color(bgC.r, bgC.g, bgC.b, 1f);
                var corner = src[0];
                // the name's box: every pixel that differs clearly from the sign's ground
                int minX = w, maxX = -1, minY = h, maxY = -1;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var c = src[y * w + x];
                        if (Mathf.Abs(c.r - corner.r) + Mathf.Abs(c.g - corner.g) + Mathf.Abs(c.b - corner.b) < 60) continue;
                        if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
                if (maxX < 0) { minX = 0; maxX = w - 1; minY = 0; maxY = h - 1; }
                int nameW = maxX - minX + 1, nameH = maxY - minY + 1;
                int logos = mode == 3 ? 2 : 1;
                float logo = h * 0.8f, gap = h * 0.18f;
                float total = nameW + logos * (logo + gap);
                float f = Mathf.Min(1f, (w - h * 0.3f) / total);
                int nW = Mathf.Max(1, Mathf.RoundToInt(nameW * f)), nH = Mathf.Max(1, Mathf.RoundToInt(nameH * f));
                int lS = Mathf.Max(1, Mathf.RoundToInt(logo * f)), g = Mathf.RoundToInt(gap * f);
                int x0 = (w - (nW + logos * (lS + g))) / 2;
                var dst = new Color32[w * h];
                for (int i = 0; i < dst.Length; i++) dst[i] = corner;   // the ground as the game painted it
                int at = x0;
                if (mode == 1 || mode == 3) { Shape(dst, w, h, shape, at, (h - lS) / 2, lS, own ? Color.white : logoC, corner, own); at += lS + g; }
                for (int y = 0; y < nH; y++)
                {
                    int ty = (h - nH) / 2 + y;
                    if (ty < 0 || ty >= h) continue;
                    float v = (minY + (y + 0.5f) / nH * nameH) / h;
                    for (int x = 0; x < nW; x++)
                    {
                        int tx = at + x;
                        if (tx < 0 || tx >= w) continue;
                        float u = (minX + (x + 0.5f) / nW * nameW) / w;
                        dst[ty * w + tx] = (Color32)wide.GetPixelBilinear(u, v);
                    }
                }
                at += nW + g;
                if (mode == 2 || mode == 3) Shape(dst, w, h, shape, at, (h - lS) / 2, lS, own ? Color.white : logoC, corner, own);
                outTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                outTex.SetPixels32(dst);
                outTex.Apply(false, false);
                File.WriteAllBytes(wideFile, outTex.EncodeToJPG(92));
                Plugin.Logger.LogInfo($"[SignFx] the logo is beside the name on the sign ({(mode == 1 ? "left" : mode == 2 ? "right" : "both sides")}) (SIGN-FX-2).");
            }
            finally
            {
                if (wide != null) Destroy(wide);
                if (shape != null) Destroy(shape);
                if (outTex != null) Destroy(outTex);
            }
        }

        /// <summary>The logo shape (white on clear) in the logo's colour on the ground, size x size at (x, y).</summary>
        private static void Shape(Color32[] dst, int w, int h, Texture2D shape, int x0, int y0, int size, Color logo, Color32 ground, bool picture = false)
        {
            var g = (Color)ground;
            // SIGN-FX-3: a picture keeps its proportions inside the square
            float sw = Mathf.Max(1, shape.width), sh = Mathf.Max(1, shape.height);
            float fw = picture ? Mathf.Min(1f, sw / sh) : 1f, fh = picture ? Mathf.Min(1f, sh / sw) : 1f;
            int dw = Mathf.Max(1, Mathf.RoundToInt(size * fw)), dh = Mathf.Max(1, Mathf.RoundToInt(size * fh));
            int ox = x0 + (size - dw) / 2, oy = y0 + (size - dh) / 2;
            for (int y = 0; y < dh; y++)
            {
                int ty = oy + y;
                if (ty < 0 || ty >= h) continue;
                for (int x = 0; x < dw; x++)
                {
                    int tx = ox + x;
                    if (tx < 0 || tx >= w) continue;
                    var c = shape.GetPixelBilinear((x + 0.5f) / dw, (y + 0.5f) / dh);
                    if (picture) { dst[ty * w + tx] = (Color32)Color.Lerp(g, new Color(c.r, c.g, c.b, 1f), c.a); continue; }
                    float a = c.a * Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    dst[ty * w + tx] = (Color32)Color.Lerp(g, new Color(logo.r * c.r, logo.g * c.g, logo.b * c.b, 1f), a > 0.02f ? c.a : 0f);
                }
            }
        }

        /// <summary>"Keep colours": the logo colour becomes white, so the shape's own colours show (the game tints it).</summary>
        private static void LpLogoWhite(UI.Smartphone.Apps.BizMan.LogoShapes shapes)
        {
            try
            {
                UI.Smartphone.Apps.BizMan.LogoCustomizer? cust = shapes.GetComponentInParent<UI.Smartphone.Apps.BizMan.LogoCustomizer>(true);
                if (cust == null)
                    foreach (var c in Resources.FindObjectsOfTypeAll<UI.Smartphone.Apps.BizMan.LogoCustomizer>())
                        if (c != null && c.logoShapes == shapes) { cust = c; break; }
                if (cust == null) return;
                cust.SelectLogoColor(Color.white);
                cust.logoColorList.SetUp();   // its swatches follow the colour
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Logo] picture window: logo colour: {ex.GetType().Name}"); }
        }
    }
}
