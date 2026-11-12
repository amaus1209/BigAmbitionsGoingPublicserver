using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GO-LIVERY-1 (user 2026-11-10: "Ja" - Entwurf 1 of the service van's livery into the game, adjustable in the
    /// app; the earlier proposal it answers: the colour panel with the business logo and the fibre light, the slogan, the
    /// offers by the equipment that stands, "Techniker im Einsatz" (user 2026-11-10: not "Technik im Einsatz"), the cable as an option, the colour from the dealer's
    /// colour choice, the van in the dealers' showrooms, other players see one's vans in one's livery).
    /// The Unity side of <see cref="LiveryPaint"/>: it loads the font and the pictures from assets/, makes a texture and a
    /// material for each livery and paint colour (one a frame at most, kept while vans show it), and knows the player's
    /// own livery (the app's settings, IspState.Style) and - from Going Public Server - the other players' ones.
    /// Main thread; nothing of it on a dedicated server.</summary>
    internal static class Livery
    {
        public const int White = 0xF2F2F2;
        public static readonly int[] Palette =
            { 0x1E5AA8, 0x0E8C9B, 0x2E8B57, 0x7CB342, 0xE0A800, 0xE06A10, 0xC62828, 0xAD1457, 0x5E35B1, 0x37474F, 0x151515 };

        static LiveryPaint.Font? _font;
        static LiveryPaint.Art? _art;
        static LiveryPaint.Layout? _layout;
        static bool _tried, _ok;

        sealed class Entry { public Texture2D? Tex; public Material Mat = null!; public float Used; }
        static readonly Dictionary<string, Entry> _cache = new Dictionary<string, Entry>();
        static int _paintedFrame = -1, _painted;
        static float _paintMs, _trimAt;
        static bool _warned;

        // ── the assets ──────────────────────────────────────────────────────────────────────────────────────────────

        public static bool Load()
        {
            if (_tried) return _ok;
            _tried = true;
            try
            {
                string root = Path.Combine(Go.Host?.ModRoot ?? "", "assets");
                var fontImg = ReadPng(Path.Combine(root, "GO_Livery_Font.png"));
                var artImg = ReadPng(Path.Combine(root, "GO_Livery_Art.png"));
                string ft = Path.Combine(root, "GO_Livery_Font.txt"), at = Path.Combine(root, "GO_Livery_Art.txt"), lt = Path.Combine(root, "models", "govan.uv.txt");
                if (fontImg != null && File.Exists(ft)) _font = LiveryPaint.ParseFont(File.ReadAllText(ft), fontImg);
                if (artImg != null && File.Exists(at)) _art = LiveryPaint.ParseArt(File.ReadAllText(at), artImg);
                if (File.Exists(lt)) _layout = LiveryPaint.ParseLayout(File.ReadAllText(lt));
                _ok = _font != null && _art != null && _layout != null;
                Go.Log(_ok ? $"van livery: font ({_font!.G.Count} glyphs), pictures ({_art!.Parts.Count}) and the van's texture fields ({_layout!.R.Count}) loaded (GO-LIVERY-1)."
                           : $"van livery: assets missing (font {_font != null}, pictures {_art != null}, fields {_layout != null}) - the vans stay plain (GO-LIVERY-1).");
            }
            catch (System.Exception ex) { _ok = false; Go.Warn("van livery: " + ex.Message + " (GO-LIVERY-1)."); }
            return _ok;
        }

        /// <summary>CAR-LIVERY-1: the font and the pictures (the cars of the game need no texture fields).</summary>
        internal static bool Art(out LiveryPaint.Font? font, out LiveryPaint.Art? art)
        {
            Load();
            font = _font; art = _art;
            return _font != null && _art != null;
        }

        static LiveryPaint.Img? ReadPng(string path)
        {
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path))) return null;
                return FromTexture2D(tex);
            }
            finally { UnityEngine.Object.Destroy(tex); }
        }

        static LiveryPaint.Img FromTexture2D(Texture2D tex)
        {
            var px = tex.GetPixels32();   // rows from the bottom
            var b = new byte[px.Length * 4];
            for (int i = 0, j = 0; i < px.Length; i++, j += 4) { b[j] = px[i].r; b[j + 1] = px[i].g; b[j + 2] = px[i].b; b[j + 3] = px[i].a; }
            return new LiveryPaint.Img { W = tex.width, H = tex.height, Px = b };
        }

        /// <summary>Any texture's pixels (also one that cannot be read: copied through a render texture), at most 256 px.</summary>
        internal static LiveryPaint.Img? ReadTexture(Texture? src)
        {
            if (src == null) return null;
            int w = Mathf.Min(256, src.width), h = Mathf.Min(256, src.height);
            if (w < 4 || h < 4) return null;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply(false);
                return FromTexture2D(tex);
            }
            catch { return null; }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(tex);
            }
        }

        // ── the player's own livery ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>The offers on the van: what the equipment in the headend makes possible.</summary>
        public static int ServicesOf(IspState s)
        {
            int bits = 0;
            if (s.Sat) bits |= 1;          // satellite headend: TV
            if (!s.NoCmts) bits |= 2;      // a CMTS: internet
            if (s.Iptv) bits |= 4;
            if (s.Voice) bits |= 8;
            if (s.Olt) bits |= 16;         // the OLT: fibre to the home
            if (s.Cas) bits |= 32;         // the conditional access system: pay TV
            return bits;
        }

        public static string CompanyName(IspState s)
        {
            if (City.BusinessName(s.HeadendKey, out var n) && n.Trim().Length > 0) return n.Trim();
            return (s.Company ?? "").Trim();
        }

        public static string DefaultContact() => Go.T("go_livery_contact_default", "Customer service around the clock");

        /// <summary>The player's livery (null: no provider yet).</summary>
        public static LiveryPaint.Spec? LocalSpec()
        {
            var s = Core.S;
            if (s == null || string.IsNullOrEmpty(s.HeadendKey)) return null;
            var st = s.Style ?? new VanStyle();
            string name = CompanyName(s);
            return new LiveryPaint.Spec
            {
                Motif = Mathf.Clamp(st.Motif, 0, 3), Panel = st.Panel & 0xFFFFFF, Name = name,
                Slogan = (st.Slogan ?? "").Trim().Length > 0 ? st.Slogan!.Trim() : LiveryPaint.DefaultSlogan(name),
                Contact = (st.Contact ?? "").Trim().Length > 0 ? st.Contact!.Trim() : DefaultContact(),
                Services = ServicesOf(s), Tech = st.Tech, Cable = st.Cable, Logo = st.Logo,
                TextPct = Mathf.Clamp(st.TextPct, 50, 200), LogoPct = Mathf.Clamp(st.LogoPct, 50, 200),   // GO-LIVERY-4
            };
        }

        /// <summary>The showroom van of a player without a provider: Going Online's sample with every offer.</summary>
        public static LiveryPaint.Spec DemoSpec() => new LiveryPaint.Spec
        {
            Motif = 0, Panel = Palette[0], Name = "Going Online", Slogan = "#GOINGONLINE",
            Contact = Go.T("go_livery_demo_contact", "Your livery: in the app Going Online, tab \"Service van\""), Services = 63, Logo = false,
        };

        /// <summary>The player's logo (the business's square sign) and its key; null until the game has made it.</summary>
        static Texture? LocalLogo(out string key)
        {
            key = "";
            try
            {
                var m = ServiceVans.Logo();
                var t = m != null && m.HasProperty("_BaseColorMap") ? m.GetTexture("_BaseColorMap") : null;
                if (t != null) { key = "own:" + t.GetInstanceID(); return t; }
            }
            catch { }
            return null;
        }

        static readonly Dictionary<string, LiveryPaint.Img?> _logoPx = new Dictionary<string, LiveryPaint.Img?>();

        internal static LiveryPaint.Img? LogoPixels(string key, Texture? t)
        {
            if (key.Length == 0 || t == null) return null;
            if (_logoPx.TryGetValue(key, out var have)) return have;
            var img = ReadTexture(t);
            if (img != null) img = LiveryPaint.TrimMargins(img);   // GO-LIVERY-4: the logo fills its square
            _logoPx[key] = img;
            return img;
        }

        // ── other players' liveries (Going Public Server) ───────────────────────────────────────────────────────────

        /// <summary>Going Public Server: the owner of a car of another player (its player id), else null.</summary>
        public static System.Func<GameObject, string?>? OwnerOf;
        /// <summary>CAR-LIVERY-1 (Going Public Server): a van's own livery, set after it was bought (each side its look) - on
        /// top of the company's (null when there is none; may also be null). Returns null to keep the company's.</summary>
        public delegate LiveryPaint.Spec? VehicleLook(GameObject car, LiveryPaint.Spec? company, ref Texture? logo, ref string logoKey);
        public static VehicleLook? PerVehicle;
        /// <summary>GO-PLATE-1 (Going Public Server): the player's name on the plates.</summary>
        public static System.Func<string?>? PlayerName;
        /// <summary>GO-PLATE-1 (Going Public Server): another player's van's plate as that player sent it (null: not known).</summary>
        public static System.Func<GameObject, string?>? RemotePlate;
        /// <summary>GO-PLATE-1: the player's own service vans' plates (vehicle id -> text), for the other players.</summary>
        public static Dictionary<string, string> LocalPlates() => Plates.Local();
        /// <summary>Raised when a livery changed (Going Public Server's editor): the vans look again at once.</summary>
        public static int Version;
        /// <summary>Going Public Server: another player's livery as packed by <see cref="LiveryPaint.Spec.Pack"/> and their
        /// logo (JPG), or null while it is not known.</summary>
        public static System.Func<string, (string pack, byte[]? logo)?>? RemoteOf;

        sealed class Remote { public string Pack = ""; public LiveryPaint.Spec? Spec; public Texture2D? Logo; public int LogoLen = -1; }
        static readonly Dictionary<string, Remote> _remote = new Dictionary<string, Remote>();

        static LiveryPaint.Spec? RemoteSpec(string owner, out Texture? logo, out string logoKey)
        {
            logo = null; logoKey = "";
            (string pack, byte[]? logo)? got = null;
            try { got = RemoteOf?.Invoke(owner); } catch { }
            if (got == null) return null;
            if (!_remote.TryGetValue(owner, out var r)) _remote[owner] = r = new Remote();
            if (r.Pack != got.Value.pack) { r.Pack = got.Value.pack; r.Spec = LiveryPaint.Spec.Unpack(r.Pack); }
            var bytes = got.Value.logo;
            int len = Hash(bytes);
            if (len != r.LogoLen)
            {
                r.LogoLen = len;
                if (r.Logo != null) UnityEngine.Object.Destroy(r.Logo);
                r.Logo = null;
                if (bytes != null && bytes.Length > 0)
                {
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                    if (ImageConversion.LoadImage(t, bytes)) r.Logo = t; else UnityEngine.Object.Destroy(t);
                }
            }
            if (r.Logo != null) { logo = r.Logo; logoKey = "p:" + owner + ":" + r.LogoLen; }
            return r.Spec;
        }

        /// <summary>GO-STREET-2: a provider's logo as pixels - the player's own ("" or null), else another player's as sent
        /// with their livery; null while it is not known. key: changes with the logo.</summary>
        internal static LiveryPaint.Img? LogoImg(string? owner, out string key)
        {
            key = "";
            Texture? t;
            if (string.IsNullOrEmpty(owner)) t = LocalLogo(out key);
            else { RemoteSpec(owner!, out t, out key); }
            return t == null ? null : LogoPixels(key, t);
        }

        /// <summary>A short fingerprint of a logo's bytes (FNV-1a), 0 for none.</summary>
        static int Hash(byte[]? b)
        {
            if (b == null || b.Length == 0) return 0;
            unchecked
            {
                uint h = 2166136261;
                foreach (var x in b) { h ^= x; h *= 16777619; }
                return (int)(h | 1);
            }
        }

        /// <summary>For Going Public Server: the player's own livery to send to the others ("" = none) and the logo as a
        /// small JPG (made once per logo).</summary>
        public static string LocalPack(out byte[]? logoJpg)
        {
            logoJpg = null;
            var spec = LocalSpec();
            if (spec == null) return "";
            var t = LocalLogo(out var key);
            if (spec.Logo && t != null)
            {
                if (key != _jpgKey) { _jpgKey = key; _jpg = Jpg(t); }
                logoJpg = _jpg;
            }
            return spec.Pack();
        }
        static string _jpgKey = "";
        static byte[]? _jpg;

        internal static byte[]? Jpg(Texture src)
        {
            var rt = RenderTexture.GetTemporary(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            var tex = new Texture2D(128, 128, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                tex.Apply(false);
                return ImageConversion.EncodeToJPG(tex, 85);
            }
            catch { return null; }
            finally { RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.Destroy(tex); }
        }

        // ── textures and materials ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>Which livery a van shows: its owner's (another player's), the player's own, the sample in the
        /// showroom - null for a plain van (no provider, an unknown owner).</summary>
        public static LiveryPaint.Spec? SpecFor(GameObject car, bool showroom, out Texture? logo, out string logoKey)
        {
            logo = null; logoKey = "";
            string? owner = null;
            try { owner = OwnerOf?.Invoke(car); } catch { }
            LiveryPaint.Spec? spec;
            if (!string.IsNullOrEmpty(owner)) spec = RemoteSpec(owner!, out logo, out logoKey);
            else
            {
                spec = LocalSpec();
                if (spec != null) logo = LocalLogo(out logoKey);
                else if (showroom) return DemoSpec();
            }
            if (PerVehicle != null && !showroom)
            {
                try
                {
                    var own = PerVehicle(car, spec, ref logo, ref logoKey);
                    if (own != null) return own;
                }
                catch (System.Exception ex) { WarnOnce("a van's own livery: " + ex.Message); }
            }
            return spec;
        }

        /// <summary>A van without a livery (none, "logo only", or every side without a look).</summary>
        public static bool Plain(LiveryPaint.Spec? spec)
        {
            if (spec == null) return true;
            if (spec.Sides == null) return spec.Motif == 3;
            foreach (var l in spec.Sides) if (l != null && l.Design != LiveryPaint.DNone) return false;
            return true;
        }

        /// <summary>The key a van's paint material is cached under.</summary>
        public static string KeyOf(LiveryPaint.Spec? spec, int paint, string logoKey)
            => (Plain(spec) ? "plain|" + paint.ToString("X6")
             : spec!.Key() + "|" + paint.ToString("X6") + "|" + (spec.Logo ? logoKey : "")) + (_gameMat != null ? "|g" : "");

        // ── GO-LIVERY-3: the game cars' paint ───────────────────────────────────────────────────────────────────────
        // (user 2026-11-10: "so schön wie die anderen Autos aus dem Spiel", then "Weiter machen"): the van's paint is a copy
        // of the game car's own body material (the template car's SH_Vehicle - the shader every car of the game uses, after
        // the official SDK's Example-Vehicle and SH_Vehicle.shadergraph): our picture as its "Diffuse+mask" (ComposeMasked:
        // alpha 1 = bare paint, tinted with the vehicle's colour and its fresnel by CarFeatures.SetColor), the paint's
        // metal/AO/smoothness as the SDK car has them on paint (0, 240, -, 176), a flat normal, and black lamp masks - the
        // van's lamps are parts of their own (VanLights). Without that material (not found yet): HDRP/Lit as before.
        internal const string DiffuseProp = "Texture2D_3d0babbacbb04731a784ae3943688e65", MaskProp = "Texture2D_196dcf5fb385498aa0919d56d7cd684d",
            NormalProp = "Texture2D_9b32f246716c41009146a43593b55e3f", TintProp = "Color_3d0f0cdbe6b74be28a1a5be5bab71dea",
            FresnelProp = "Color_f78fac473bac467092fb27521e9f71ea", PowerProp = "Vector1_481fa2a8a5e94165a039319bfd512b76";
        static Material? _gameMat;
        static Texture2D? _maskTex, _normalTex, _blackTex, _plainTex, _prevTex;
        static string _prevKey = "";

        /// <summary>Van.Dress: the car's own body material before ours replaces it - the first one in the game's vehicle shader.</summary>
        internal static void NoteGameMaterial(Material? m)
        {
            if (_gameMat != null || m == null) return;
            try
            {
                if (!m.HasProperty(DiffuseProp) || !m.HasProperty("_EmissionMask") || !m.HasProperty(TintProp)) return;
                _gameMat = new Material(m) { name = "GO_van_game_paint", hideFlags = HideFlags.HideAndDontSave };
                Go.Log($"van paint: the game cars' material ({m.shader?.name}) for the van's paint (GO-LIVERY-3).");
            }
            catch (System.Exception ex) { Go.Warn("van paint: " + ex.Message + " (GO-LIVERY-3)."); }
        }

        internal static bool GamePaint => _gameMat != null;

        static Texture2D Small(ref Texture2D? t, Color32 c, bool linear, string name)
        {
            if (t != null) return t;
            t = new Texture2D(4, 4, TextureFormat.RGBA32, false, linear) { name = name, hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = c;
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        static void GameMaps(Material mat, Texture diffuse)
        {
            mat.SetTexture(DiffuseProp, diffuse);
            mat.SetTexture(MaskProp, Small(ref _maskTex, new Color32(0, 240, 0, 176), true, "GO_VanPaintMask"));
            mat.SetTexture(NormalProp, Small(ref _normalTex, new Color32(128, 128, 255, 255), true, "GO_VanFlatNormal"));
            var black = Small(ref _blackTex, new Color32(0, 0, 0, 0), true, "GO_VanNoLamps");
            foreach (var t in new[] { "_EmissionMask", "_IndicatorsMask", "_EmissionTexture" }) if (mat.HasProperty(t)) mat.SetTexture(t, black);
        }

        /// <summary>The traffic's vans keep their own white: the tint the game would take from the traffic car, ours.</summary>
        internal static void Tint(Renderer r, int rgb)
        {
            if (_gameMat == null || r == null) return;
            _tintBlock ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_tintBlock);
            var c = new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
            _tintBlock.SetColor(TintProp, c);
            _tintBlock.SetColor(FresnelProp, Color.Lerp(c, Color.white, 0.5f));
            _tintBlock.SetFloat(PowerProp, 2.04f);
            r.SetPropertyBlock(_tintBlock);
        }
        static MaterialPropertyBlock? _tintBlock;

        /// <summary>The paint material for a livery and a paint colour; null while it cannot be made in this frame (one a
        /// frame) - the van asks again.</summary>
        public static Material? MaterialFor(LiveryPaint.Spec? spec, int paint, Texture? logo, string logoKey, Material template)
        {
            string key = KeyOf(spec, paint, logoKey);
            if (_cache.TryGetValue(key, out var have) && have.Mat != null) { have.Used = Time.unscaledTime; return have.Mat; }
            if (_paintedFrame == Time.frameCount) return null;
            _paintedFrame = Time.frameCount;
            Trim();
            bool game = _gameMat != null;   // GO-LIVERY-3
            var mat = new Material(game ? _gameMat! : template) { name = game ? "GO_van_livery" : "GO_van_white_livery", hideFlags = HideFlags.HideAndDontSave };
            var c = new Color32((byte)(paint >> 16), (byte)(paint >> 8), (byte)paint, 255);
            Texture2D? tex = null;
            if (spec != null && !Plain(spec) && Load())
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Labels();
                spec.Paint = paint;
                var lg = spec.Logo ? LogoPixels(logoKey, logo) : null;
                var px = game ? LiveryPaint.ComposeMasked(spec, _layout!, _font!, _art!, lg) : LiveryPaint.Compose(spec, _layout!, _font!, _art!, lg);
                int n = _layout!.Size;
                tex = new Texture2D(n, n, game ? TextureFormat.RGBA32 : TextureFormat.RGB24, true, false)
                {
                    name = "GO_Livery", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                tex.SetPixelData(px, 0);
                tex.Apply(true, false);
                try { tex.Compress(false); } catch { }
                tex.Apply(false, true);
                sw.Stop();
                _painted++; _paintMs += sw.ElapsedMilliseconds;
                if (_painted <= 3 || _painted % 25 == 0)
                    Go.Log($"van livery: painted {(spec.Name.Length > 0 ? "\"" + spec.Name + "\"" : "one")} ({(spec.Sides != null ? "each side its own look" : "motif " + spec.Motif)}, paint #{paint:X6}) in {sw.ElapsedMilliseconds} ms, {tex.format}; {_painted} so far, {_cache.Count + 1} kept (GO-LIVERY-1).");
                if (game) GameMaps(mat, tex);
                else if (mat.HasProperty("_BaseColorMap")) mat.SetTexture("_BaseColorMap", tex);
                c = new Color32(255, 255, 255, 255);
            }
            else if (game) GameMaps(mat, Small(ref _plainTex, new Color32(255, 255, 255, 255), false, "GO_VanPlainPaint"));   // all paint
            if (!game)
            {
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            }
            _cache[key] = new Entry { Tex = tex, Mat = mat, Used = Time.unscaledTime };
            return mat;
        }

        /// <summary>The offers' labels and the front door's sign in the player's language.</summary>
        internal static void Labels()
        {
            LiveryPaint.OfferLabels = new[]
            {
                Go.T("go_livery_tv", "TV"), Go.T("go_livery_internet", "INTERNET"), Go.T("go_livery_fiber", "FIBRE"),
                Go.T("go_livery_iptv", "IPTV"), Go.T("go_livery_paytv", "PAY TV"), Go.T("go_livery_phone", "PHONE"),
            };
            LiveryPaint.TechLine1 = Go.T("go_livery_tech1", "TECHNICIANS");
            LiveryPaint.TechLine2 = Go.T("go_livery_tech2", "AT WORK");
        }

        /// <summary>The offers by name for the app (the same order as on the van).</summary>
        public static string OfferNames(int bits)
        {
            string[] names =
            {
                Go.T("go_offer_tv", "TV"), Go.T("go_offer_internet", "Internet"), Go.T("go_offer_fiber", "Fibre"),
                Go.T("go_offer_iptv", "IPTV"), Go.T("go_offer_paytv", "Pay TV"), Go.T("go_offer_phone", "Telephone"),
            };
            var list = new List<string>();
            for (int i = 0; i < LiveryPaint.Offers.Length; i++) if ((bits & LiveryPaint.Offers[i].bit) != 0) list.Add(names[i]);
            return string.Join(", ", list);
        }

        /// <summary>A van still shows this material: it stays.</summary>
        public static void Touch(string key) { if (_cache.TryGetValue(key, out var e)) e.Used = Time.unscaledTime; }

        /// <summary>Liveries no van showed for two minutes go (beyond the eight newest).</summary>
        static void Trim()
        {
            if (Time.unscaledTime < _trimAt || _cache.Count <= 8) return;
            _trimAt = Time.unscaledTime + 10f;
            foreach (var key in new List<string>(_cache.Keys))
            {
                var e = _cache[key];
                if (Time.unscaledTime - e.Used < 120f) continue;
                if (e.Tex != null) UnityEngine.Object.Destroy(e.Tex);
                if (e.Mat != null) UnityEngine.Object.Destroy(e.Mat);
                _cache.Remove(key);
            }
        }

        /// <summary>The app's picture: the player's livery on a white van (the left side's and the back's fields of the
        /// texture). null with none = true: there is no picture (no provider, "logo only"); null else: not painted yet.</summary>
        public static Texture? Preview(Material template, out Rect side, out Rect back, out bool none)
        {
            side = new Rect(0f, 0f, 1f, 1f); back = side;
            var spec = LocalSpec();
            none = spec == null || spec.Motif == 3 || !Load();
            if (none) return null;
            var logo = LocalLogo(out var logoKey);
            float n0 = _layout!.Size;
            if (_layout.R.TryGetValue("L", out var l0)) side = new Rect(l0.X / n0, l0.Y / n0, l0.W / n0, l0.H / n0);
            if (_layout.R.TryGetValue("B", out var b0)) back = new Rect(b0.X / n0, b0.Y / n0, b0.W / n0, b0.H / n0);
            if (_gameMat != null)
            {   // GO-LIVERY-3: the van's texture holds the paint mask in its alpha (the stickers would show see-through) -
                // the picture gets an RGB one of its own, on white paint, made again when the livery changes
                string key = KeyOf(spec, White, logoKey);
                if (_prevTex != null && _prevKey == key) return _prevTex;
                if (_paintedFrame == Time.frameCount) return null;
                _paintedFrame = Time.frameCount;
                Labels();
                spec!.Paint = White;
                var px = LiveryPaint.Compose(spec, _layout, _font!, _art!, spec.Logo ? LogoPixels(logoKey, logo) : null);
                int ns = _layout.Size;
                var tex = new Texture2D(ns, ns, TextureFormat.RGB24, true, false)
                { name = "GO_LiveryPicture", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixelData(px, 0);
                tex.Apply(true, false);
                try { tex.Compress(false); } catch { }
                tex.Apply(false, true);
                if (_prevTex != null) UnityEngine.Object.Destroy(_prevTex);
                _prevTex = tex; _prevKey = key;
                return tex;
            }
            var mat = MaterialFor(spec, White, logo, logoKey, template);
            if (mat == null || !mat.HasProperty("_BaseColorMap")) return null;
            float n = _layout!.Size;
            if (_layout!.R.TryGetValue("L", out var l)) side = new Rect(l.X / n, l.Y / n, l.W / n, l.H / n);
            if (_layout.R.TryGetValue("B", out var b)) back = new Rect(b.X / n, b.Y / n, b.W / n, b.H / n);
            return mat.GetTexture("_BaseColorMap");
        }

        // ── GO-LIVERY-2: the van itself in the app's picture ────────────────────────────────────────────────────────

        static VanPicture.Drawing?[]? _drawings;
        static readonly Texture2D?[] _drawTex = new Texture2D?[2];
        static System.Threading.Tasks.Task? _drawTask;
        static bool _drawTried, _drawWarned;

        /// <summary>GO-LIVERY-2: the van seen from the left (side 0) or from behind (1) for the app's picture: its size and
        /// where the livery's field lies in it, known at once; the pixels are painted on a worker thread, once a game start.
        /// null without the van's model or its texture fields - the picture then shows the field alone, as before.</summary>
        internal static VanPicture.Drawing? DrawingPlan(int side)
        {
            if (!_drawTried)
            {
                _drawTried = true;
                try { StartDrawings(); }
                catch (System.Exception ex) { _drawings = null; Go.Warn("van picture: " + ex.Message + " (GO-LIVERY-2)."); }
            }
            return _drawings != null && side >= 0 && side < _drawings.Length ? _drawings[side] : null;
        }

        /// <summary>The drawing as a texture once it is painted (main thread), null before.</summary>
        internal static Texture2D? DrawingTexture(int side)
        {
            var d = DrawingPlan(side);
            if (d == null) return null;
            if (_drawTex[side] != null) return _drawTex[side];
            if (_drawTask == null || !_drawTask.IsCompleted) return null;
            if (_drawTask.IsFaulted)
            {
                if (!_drawWarned) { _drawWarned = true; Go.Warn("van picture: " + (_drawTask.Exception?.InnerException?.Message ?? "failed") + " (GO-LIVERY-2)."); }
                return null;
            }
            if (d.Px.Length != d.W * d.H * 4) return null;
            var tex = new Texture2D(d.W, d.H, TextureFormat.RGBA32, false)
            { name = "GO_VanPicture" + side, hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.LoadRawTextureData(d.Px);
            tex.Apply(false, true);
            d.Px = System.Array.Empty<byte>();   // the texture has them now
            _drawTex[side] = tex;
            return tex;
        }

        /// <summary>The model's triangles - the body and the four wheels where the game's car has them - and the two plans;
        /// the painting goes to a worker thread.</summary>
        static void StartDrawings()
        {
            if (!Load() || _layout == null) return;
            var body = Models.Get("govan");
            if (body == null) return;
            Models.Model? wl = Models.Get("govanwheel_l"), wr = Models.Get("govanwheel_r");
            var v = new List<float>(80000);
            var t = new List<int>(120000);
            var k = new List<byte>(40000);
            AddModel(body, Vector3.zero, v, t, k);
            foreach (float z in new[] { VanPicture.FrontAxle, VanPicture.RearAxle })
            {
                if (wl != null) AddModel(wl, new Vector3(-VanPicture.HalfTrack, VanPicture.WheelCentre, z), v, t, k);
                if (wr != null) AddModel(wr, new Vector3(VanPicture.HalfTrack, VanPicture.WheelCentre, z), v, t, k);
            }
            float[] verts = v.ToArray();
            int[] tris = t.ToArray();
            byte[] kinds = k.ToArray();
            _layout.R.TryGetValue("L", out var fieldL);
            _layout.R.TryGetValue("B", out var fieldB);
            var d0 = VanPicture.Plan(verts, 0, fieldL, 250f);
            var d1 = VanPicture.Plan(verts, 1, fieldB, 250f);
            _drawings = new[] { d0, d1 };
            _drawTask = System.Threading.Tasks.Task.Run(() => { VanPicture.Paint(d0, verts, tris, kinds); VanPicture.Paint(d1, verts, tris, kinds); });
            Go.Log($"van picture: {kinds.Length} triangles, drawings {d0.W}x{d0.H} and {d1.W}x{d1.H} (GO-LIVERY-2).");
        }

        static void AddModel(Models.Model m, Vector3 at, List<float> v, List<int> t, List<byte> k)
        {
            int b = v.Count / 3;
            foreach (var p in m.Mesh.vertices) { v.Add(p.x + at.x); v.Add(p.y + at.y); v.Add(p.z + at.z); }
            for (int s = 0; s < m.Mesh.subMeshCount && s < m.Parts.Length; s++)
            {
                byte kind = VanPicture.KindOf(m.Parts[s].Name);
                var tri = m.Mesh.GetTriangles(s);   // indices into the whole mesh
                for (int i = 0; i + 2 < tri.Length; i += 3) { t.Add(b + tri[i]); t.Add(b + tri[i + 1]); t.Add(b + tri[i + 2]); k.Add(kind); }
            }
        }

        internal static void WarnOnce(string what)
        {
            if (_warned) return;
            _warned = true;
            Go.Warn("van livery: " + what + " (GO-LIVERY-1).");
        }
    }

    /// <summary>GO-LIVERY-1: on every van of ours - the player's vehicle model (so every bought, leased and other player's
    /// van), a traffic car wearing the van, the showroom piece. Once a second (in the showroom five times a second, so the
    /// colour choice shows at once) it checks which livery and colour the van should show and swaps the paint's material.</summary>
    internal sealed class VanSkin : MonoBehaviour
    {
        /// <summary>A colour of its own (the traffic's vans: white), else the game's colour of the vehicle.</summary>
        public int FixedPaint = -1;
        public bool Showroom;
        internal string Key = "";
        internal string Plate = "";   // GO-PLATE-1: what its plates show
        float _next;
        int _version;

        void Update()
        {
            float now = Time.unscaledTime;
            if (_version != Livery.Version) { _version = Livery.Version; _next = 0f; }   // CAR-LIVERY-1: a livery changed
            if (now < _next) return;
            _next = now + (Showroom ? 0.2f : 1f) + UnityEngine.Random.Range(0f, 0.1f);
            try { Van.Skin(this); } catch (System.Exception ex) { Livery.WarnOnce(ex.Message); }
        }
    }
}
