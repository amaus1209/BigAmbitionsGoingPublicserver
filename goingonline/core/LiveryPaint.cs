using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GoingOnline
{
    /// <summary>GO-LIVERY-1 (user 2026-11-10: "Ja" - Entwurf 1 of the service van's livery into the game, adjustable in the
    /// app): paints a van's livery into one 2048 x 2048 picture, the texture of the van's paint. The body's texture
    /// coordinates (assets/models/govan.gom, version 2) give each side, the back and the hood a field of that picture
    /// (assets/models/govan.uv.txt); all else shows the paint colour. The parts are laid out in the body's own metres, as in
    /// the Blender picture the user chose (~/Big-Ambitions-Server-Code/ba-art/goingonline/go_van.py, SIDE):
    /// - the colour panel at the back of each side (the business logo, the company's name, the fibre light or someone of
    ///   the technical staff at work), the slogan and a contact line under it;
    /// - the offers on the sliding door, one bar per offer - from the equipment that stands in the headend;
    /// - optional: the cable with light on the lower sliding door, "Techniker im Einsatz" on the front door;
    /// - the back doors: a small panel, the offers, the slogan, the contact line; the hood: the name.
    /// Glyphs come from a picture of the font (Roboto Bold, Apache License 2.0, assets/GO_Livery_Font.png), the pictures from
    /// assets/GO_Livery_Art.png - both made by ba-art/goingonline/go_livery_art.py. No Unity in here: plain arrays, so
    /// the same code also runs in a test outside the game.</summary>
    internal static class LiveryPaint
    {
        /// <summary>A picture: RGBA bytes, row 0 at the bottom (as Unity keeps textures).</summary>
        internal sealed class Img { public int W, H; public byte[] Px = Array.Empty<byte>(); }

        /// <summary>GO-LIVERY-4: a logo without its empty edges (clear or near-white, RGBA) and a little room round it - the
        /// game's logo pictures have wide clear margins, and on the white square the logo itself came out small.</summary>
        internal static Img TrimMargins(Img img)
        {
            if (img == null || img.W < 8 || img.H < 8 || img.Px.Length < img.W * img.H * 4) return img!;
            int x0 = img.W, y0 = img.H, x1 = -1, y1 = -1;
            for (int y = 0; y < img.H; y++)
                for (int x = 0; x < img.W; x++)
                {
                    int i = (y * img.W + x) * 4;
                    byte r = img.Px[i], g = img.Px[i + 1], b = img.Px[i + 2], a = img.Px[i + 3];
                    if (a <= 24 || (r > 240 && g > 240 && b > 240)) continue;
                    if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
            if (x1 < 0) return img;
            int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
            if (bw > img.W * 0.92f && bh > img.H * 0.92f) return img;
            int pad = Math.Max(1, (int)(Math.Max(bw, bh) * 0.04f));
            x0 = Math.Max(0, x0 - pad); y0 = Math.Max(0, y0 - pad); x1 = Math.Min(img.W - 1, x1 + pad); y1 = Math.Min(img.H - 1, y1 + pad);
            int nw = x1 - x0 + 1, nh = y1 - y0 + 1;
            var o = new Img { W = nw, H = nh, Px = new byte[nw * nh * 4] };
            for (int y = 0; y < nh; y++) Buffer.BlockCopy(img.Px, ((y0 + y) * img.W + x0) * 4, o.Px, y * nw * 4, nw * 4);
            return o;
        }

        internal sealed class Glyph { public int X, Y, W, H, Left, Top; public float Adv; }

        internal sealed class Font
        {
            public Img Atlas = new Img();
            public readonly Dictionary<int, Glyph> G = new Dictionary<int, Glyph>();
            public float Size = 112f, Cap = 80f;
        }

        internal sealed class Art
        {
            public Img Img = new Img();
            public readonly Dictionary<string, int[]> Parts = new Dictionary<string, int[]>();   // x, y (from the bottom), w, h
        }

        /// <summary>A field of the picture and the body's plane it shows (metres): pixel = X + (a - A0) * W / (A1 - A0).</summary>
        internal sealed class Region { public int X, Y, W, H; public float A0, B0, A1, B1; public bool Flip; }

        internal sealed class Layout
        {
            public int Size = 2048;
            public readonly Dictionary<string, Region> R = new Dictionary<string, Region>();
        }

        /// <summary>CAR-LIVERY-1 (Going Public Server, user 2026-11-10: "jede Seite des Fahrzeugs ... Farbwahl und Logo-Plätze
        /// nachdem man das Auto gekauft hat"): one side's look - a car's left, right, back or front.</summary>
        internal sealed class Look
        {
            public int Design;              // DNone .. DGoTech
            public int Color = 0x1E5AA8;    // the panel, the band or the wrap (RGB)
            public int Place = 1;           // 0 front (on the back: left), 1 middle, 2 back (on the back: right)
            /// <summary>GO-LIVERY-4 (user 2026-11-10: "von den Logos passt die Größe nicht - mach unter Gestaltung einen
            /// Schieberegler für die Schriftgröße und Logogröße"): the lettering's and the logo's size, per cent (100 = as
            /// laid out; 50..200).</summary>
            public int Text = 100, Logo = 100;
            public Look Copy() => new Look { Design = Design, Color = Color, Place = Place, Text = Text, Logo = Logo };
            /// <summary>design.colour.place - and .text.logo when one of them is not 100 (older versions read the first three).</summary>
            public string Key() => Design.ToString(CultureInfo.InvariantCulture) + "." + (Color & 0xFFFFFF).ToString("X6") + "." + Place.ToString(CultureInfo.InvariantCulture)
                + (Text != 100 || Logo != 100 ? "." + Text.ToString(CultureInfo.InvariantCulture) + "." + Logo.ToString(CultureInfo.InvariantCulture) : "");
        }

        /// <summary>The looks of a side: nothing; the logo and the name; a colour panel with them; a colour band along the
        /// side with them; the whole side in the colour (a wrap); Going Online's panel with the fibre light or the
        /// technician (the service van only).</summary>
        public const int DNone = 0, DLogoName = 1, DPanel = 2, DBand = 3, DWrap = 4, DGoFibre = 5, DGoTech = 6, DCount = 7;

        /// <summary>What a livery shows. Paint is the van's own colour (per vehicle: the colour chosen at the dealer).</summary>
        internal sealed class Spec
        {
            public int Motif;                       // 0 fibre light, 1 technician at work, 2 lettering only, 3 no livery
            public int Panel = 0x1E5AA8;            // the colour panel (RGB)
            public int Paint = 0xF2F2F2;            // the van (RGB)
            public string Name = "", Slogan = "", Contact = "";
            public int Services;                    // bits: Offers below
            public bool Tech = true, Cable = true, Logo = true;
            /// <summary>CAR-LIVERY-1: left, right, back, front each with a look of its own (one vehicle's livery from Going
            /// Public Server); null = <see cref="Motif"/> on every side (the app's one look for all vans).</summary>
            public Look[]? Sides;
            /// <summary>The name when <see cref="Name"/> is empty: Going Online's for the van, none for other cars.</summary>
            public string Fallback = "Going Online";
            /// <summary>GO-LIVERY-4: the lettering's and the logo's size, per cent (the app's look; a side's own in Look).</summary>
            public int TextPct = 100, LogoPct = 100;

            public Spec Clone()
            {
                var c = (Spec)MemberwiseClone();
                if (Sides != null) { c.Sides = new Look[Sides.Length]; for (int i = 0; i < Sides.Length; i++) c.Sides[i] = (Sides[i] ?? new Look()).Copy(); }
                return c;
            }

            /// <summary>Everything that changes the picture except the paint and the logo's pixels.</summary>
            public string Key() => string.Join("|", Motif.ToString(CultureInfo.InvariantCulture), Panel.ToString("X6"),
                Tech ? "1" : "0", Cable ? "1" : "0", Logo ? "1" : "0", Services.ToString(CultureInfo.InvariantCulture), Name, Slogan, Contact)
                + (Sides == null ? "" : "|S" + string.Join(",", Array.ConvertAll(Sides, l => l == null ? "-" : l.Key())) + "|" + Fallback)
                + (TextPct != 100 || LogoPct != 100 ? "|z" + TextPct.ToString(CultureInfo.InvariantCulture) + "." + LogoPct.ToString(CultureInfo.InvariantCulture) : "");

            /// <summary>For the other players (Going Public Server): the key without the paint, "|" in texts as "¦".</summary>
            public string Pack() => "1|" + string.Join("|", Motif.ToString(CultureInfo.InvariantCulture), Panel.ToString("X6"),
                Tech ? "1" : "0", Cable ? "1" : "0", Logo ? "1" : "0", Services.ToString(CultureInfo.InvariantCulture),
                Esc(Name), Esc(Slogan), Esc(Contact))
                + (TextPct != 100 || LogoPct != 100 ? "|" + TextPct.ToString(CultureInfo.InvariantCulture) + "|" + LogoPct.ToString(CultureInfo.InvariantCulture) : "");   // GO-LIVERY-4 (older versions read ten fields)

            public static Spec? Unpack(string? s)
            {
                if (string.IsNullOrEmpty(s)) return null;
                var p = s!.Split('|');
                if (p.Length < 10 || p[0] != "1") return null;
                try
                {
                    var sp = new Spec
                    {
                        Motif = Clamp(int.Parse(p[1], CultureInfo.InvariantCulture), 0, 3),
                        Panel = int.Parse(p[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) & 0xFFFFFF,
                        Tech = p[3] == "1", Cable = p[4] == "1", Logo = p[5] == "1",
                        Services = int.Parse(p[6], CultureInfo.InvariantCulture) & 63,
                        Name = Cut(p[7], 40), Slogan = Cut(p[8], 40), Contact = Cut(p[9], 60),
                    };
                    if (p.Length >= 12)
                    {   // GO-LIVERY-4
                        sp.TextPct = Clamp(int.Parse(p[10], CultureInfo.InvariantCulture), 50, 200);
                        sp.LogoPct = Clamp(int.Parse(p[11], CultureInfo.InvariantCulture), 50, 200);
                    }
                    return sp;
                }
                catch { return null; }
            }

            static string Esc(string t) => (t ?? "").Replace('|', '¦');
            static string Cut(string t, int n) => t.Length > n ? t.Substring(0, n) : t;
        }

        /// <summary>The offers: bit, label, the colour of its square, the symbol's picture. The labels are the viewer's
        /// language (Livery sets them from the texts before it paints).</summary>
        internal static readonly (int bit, string label, int color, string sym)[] Offers =
        {
            (1, "FERNSEHEN", 0x1E88E5, "sym_tv"), (2, "INTERNET", 0x43A047, "sym_internet"), (16, "GLASFASER", 0x00ACC1, "sym_fiber"),
            (4, "IPTV", 0x7E57C2, "sym_iptv"), (32, "PAY-TV", 0xE53935, "sym_paytv"), (8, "TELEFON", 0xFB8C00, "sym_phone"),
        };

        /// <summary>The labels in the order of <see cref="Offers"/>, and the two lines of the front door's sign.</summary>
        internal static string[] OfferLabels = { "FERNSEHEN", "INTERNET", "GLASFASER", "IPTV", "PAY-TV", "TELEFON" };
        internal static string TechLine1 = "TECHNIKER", TechLine2 = "IM EINSATZ";

        static string LabelOf(int i) => i < OfferLabels.Length && !string.IsNullOrEmpty(OfferLabels[i]) ? OfferLabels[i] : Offers[i].label;

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        // ── reading the assets ──────────────────────────────────────────────────────────────────────────────────────

        public static Font? ParseFont(string txt, Img atlas)
        {
            var f = new Font { Atlas = atlas };
            foreach (var raw in txt.Split('\n'))
            {
                var p = raw.Trim().Split(' ');
                if (p.Length >= 9 && p[0] == "g")
                {
                    int code = I(p[1]), x = I(p[2]), y = I(p[3]), w = I(p[4]), h = I(p[5]);
                    // the picture's rows from the top -> ours from the bottom
                    f.G[code] = new Glyph { X = x, Y = atlas.H - y - h, W = w, H = h, Left = I(p[6]), Top = I(p[7]), Adv = F(p[8]) };
                }
                else if (p.Length >= 4 && p[0] == "font")
                {
                    for (int i = 1; i + 1 < p.Length; i++)
                    {
                        if (p[i] == "size") f.Size = F(p[i + 1]);
                        if (p[i] == "cap") f.Cap = F(p[i + 1]);
                    }
                }
            }
            return f.G.Count > 60 ? f : null;
        }

        public static Art? ParseArt(string txt, Img img)
        {
            var a = new Art { Img = img };
            foreach (var raw in txt.Split('\n'))
            {
                var p = raw.Trim().Split(' ');
                if (p.Length < 5) continue;
                int x = I(p[1]), y = I(p[2]), w = I(p[3]), h = I(p[4]);
                a.Parts[p[0]] = new[] { x, img.H - y - h, w, h };
            }
            return a.Parts.Count > 0 ? a : null;
        }

        public static Layout? ParseLayout(string txt)
        {
            var l = new Layout();
            foreach (var raw in txt.Split('\n'))
            {
                var p = raw.Trim().Split(' ');
                if (p.Length >= 3 && p[0] == "atlas") l.Size = I(p[1]);
                if (p.Length >= 11 && p[0] == "region")
                    l.R[p[1]] = new Region { X = I(p[2]), Y = I(p[3]), W = I(p[4]), H = I(p[5]), A0 = F(p[6]), B0 = F(p[7]), A1 = F(p[8]), B1 = F(p[9]), Flip = p[10] == "1" };
            }
            return l.R.ContainsKey("L") && l.R.ContainsKey("R") ? l : null;
        }

        static int I(string s) => int.Parse(s, CultureInfo.InvariantCulture);
        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        // ── colours ─────────────────────────────────────────────────────────────────────────────────────────────────

        static float Lin(int c) { float v = c / 255f; return v <= 0.04045f ? v / 12.92f : (float)Math.Pow((v + 0.055f) / 1.055f, 2.4f); }

        public static float Luma(int rgb) => 0.2126f * Lin((rgb >> 16) & 255) + 0.7152f * Lin((rgb >> 8) & 255) + 0.0722f * Lin(rgb & 255);

        static float Contrast(int a, int b)
        {
            float la = Luma(a) + 0.05f, lb = Luma(b) + 0.05f;
            return la > lb ? la / lb : lb / la;
        }

        const int Dark = 0x1C1C1E, White = 0xFFFFFF;

        /// <summary>Lettering on the paint: the panel's colour when it stands out from the paint, else dark or white.</summary>
        public static int OnPaint(int paint, int panel) => Contrast(panel, paint) >= 2.8f ? panel : Luma(paint) > 0.35f ? Dark : White;

        /// <summary>Lettering on the colour panel.</summary>
        public static int OnPanel(int panel) => Luma(panel) > 0.45f ? Dark : White;

        // ── the canvas ──────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The picture being painted: RGB on the van's paint, or RGBA on a clear ground (CAR-LIVERY-1: a decal for a
        /// car of the game). Row 0 at the bottom.</summary>
        sealed class Canvas
        {
            public readonly int W, H, Bpp;
            public readonly byte[] Px;
            public Canvas(int w, int h, int paint, bool clear)
            {
                W = w; H = h; Bpp = clear ? 4 : 3;
                Px = new byte[w * h * Bpp];
                if (clear) return;
                byte r = (byte)(paint >> 16), g = (byte)(paint >> 8), b = (byte)paint;
                for (int i = 0; i < Px.Length; i += 3) { Px[i] = r; Px[i + 1] = g; Px[i + 2] = b; }
            }

            public void Blend(int x, int y, int r, int g, int b, float a)
            {
                if (a <= 0.002f || (uint)x >= (uint)W || (uint)y >= (uint)H) return;
                if (a > 1f) a = 1f;
                if (Bpp == 4)
                {   // "over" on a clear ground: the colour moves towards the new one by its share of the new cover
                    int j = (y * W + x) * 4;
                    float da = Px[j + 3] / 255f, oa = a + da * (1f - a), k = a / oa;
                    Px[j] = (byte)(Px[j] + (r - Px[j]) * k + 0.5f);
                    Px[j + 1] = (byte)(Px[j + 1] + (g - Px[j + 1]) * k + 0.5f);
                    Px[j + 2] = (byte)(Px[j + 2] + (b - Px[j + 2]) * k + 0.5f);
                    Px[j + 3] = (byte)(oa * 255f + 0.5f);
                    return;
                }
                int i = (y * W + x) * 3;
                if (a >= 0.998f) { Px[i] = (byte)r; Px[i + 1] = (byte)g; Px[i + 2] = (byte)b; return; }
                Px[i] = (byte)(Px[i] + (r - Px[i]) * a + 0.5f);
                Px[i + 1] = (byte)(Px[i + 1] + (g - Px[i + 1]) * a + 0.5f);
                Px[i + 2] = (byte)(Px[i + 2] + (b - Px[i + 2]) * a + 0.5f);
            }
        }

        /// <summary>A rounded rectangle's coverage of the pixel centred at (px, py), soft over one pixel.</summary>
        static float Cover(float px, float py, float x0, float y0, float x1, float y1, float r)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f, hx = (x1 - x0) * 0.5f - r, hy = (y1 - y0) * 0.5f - r;
            float qx = Math.Abs(px - cx) - hx, qy = Math.Abs(py - cy) - hy;
            float ox = Math.Max(qx, 0f), oy = Math.Max(qy, 0f);
            float d = (float)Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0f) - r;
            return d <= -0.5f ? 1f : d >= 0.5f ? 0f : 0.5f - d;
        }

        static void Box(Canvas c, float x0, float y0, float x1, float y1, float r, int rgb, float alpha = 1f)
        {
            int ix0 = Math.Max(0, (int)Math.Floor(x0)), iy0 = Math.Max(0, (int)Math.Floor(y0));
            int ix1 = Math.Min(c.W, (int)Math.Ceiling(x1)), iy1 = Math.Min(c.H, (int)Math.Ceiling(y1));
            int cr = (rgb >> 16) & 255, cg = (rgb >> 8) & 255, cb = rgb & 255;
            r = Math.Min(r, Math.Min(x1 - x0, y1 - y0) * 0.5f);
            for (int y = iy0; y < iy1; y++)
                for (int x = ix0; x < ix1; x++)
                {
                    float cov = Cover(x + 0.5f, y + 0.5f, x0, y0, x1, y1, r);
                    if (cov > 0f) c.Blend(x, y, cr, cg, cb, cov * alpha);
                }
        }

        /// <summary>A rounded clip: what a picture drawn inside the colour panel may cover.</summary>
        struct Clip { public bool On; public float X0, Y0, X1, Y1, R; }

        /// <summary>Part of a picture (sx, sy, sw, sh) into the rectangle (x0, y0)..(x1, y1), filtered (several taps per pixel
        /// when it gets smaller). tint >= 0: only the picture's alpha counts, in that colour (glyphs, symbols).</summary>
        static void Draw(Canvas c, Img img, int sx, int sy, int sw, int sh, float x0, float y0, float x1, float y1,
                         int tint = -1, float alpha = 1f, Clip clip = default)
        {
            if (sw <= 0 || sh <= 0 || x1 - x0 < 0.5f || y1 - y0 < 0.5f) return;
            float scx = sw / (x1 - x0), scy = sh / (y1 - y0);
            int n = Clamp((int)Math.Ceiling(Math.Max(scx, scy) - 0.15f), 1, 6);
            int ix0 = Math.Max(0, (int)Math.Floor(x0)), iy0 = Math.Max(0, (int)Math.Floor(y0));
            int ix1 = Math.Min(c.W, (int)Math.Ceiling(x1)), iy1 = Math.Min(c.H, (int)Math.Ceiling(y1));
            int tr = (tint >> 16) & 255, tg = (tint >> 8) & 255, tb = tint & 255;
            float inv = 1f / (n * n);
            for (int y = iy0; y < iy1; y++)
                for (int x = ix0; x < ix1; x++)
                {
                    float ar = 0f, ag = 0f, ab = 0f, aa = 0f;
                    for (int j = 0; j < n; j++)
                        for (int i = 0; i < n; i++)
                        {
                            float dx = x + (i + 0.5f) / n, dy = y + (j + 0.5f) / n;
                            if (dx < x0 || dx > x1 || dy < y0 || dy > y1) continue;
                            float u = (dx - x0) * scx - 0.5f, v = (dy - y0) * scy - 0.5f;
                            Sample(img, sx, sy, sw, sh, u, v, out float r, out float g, out float b, out float a);
                            ar += r * a; ag += g * a; ab += b * a; aa += a;
                        }
                    if (aa <= 0.0001f) continue;
                    float A = aa * inv * alpha;
                    if (clip.On) A *= Cover(x + 0.5f, y + 0.5f, clip.X0, clip.Y0, clip.X1, clip.Y1, clip.R);
                    if (tint >= 0) c.Blend(x, y, tr, tg, tb, A);
                    else c.Blend(x, y, (int)(ar / aa + 0.5f), (int)(ag / aa + 0.5f), (int)(ab / aa + 0.5f), A);
                }
        }

        /// <summary>Bilinear, inside the part only (its edge pixels repeat).</summary>
        static void Sample(Img img, int sx, int sy, int sw, int sh, float u, float v, out float r, out float g, out float b, out float a)
        {
            if (u < 0f) u = 0f; if (v < 0f) v = 0f;
            if (u > sw - 1) u = sw - 1; if (v > sh - 1) v = sh - 1;
            int x0 = (int)u, y0 = (int)v;
            int x1 = Math.Min(x0 + 1, sw - 1), y1 = Math.Min(y0 + 1, sh - 1);
            float fx = u - x0, fy = v - y0;
            var p = img.Px;
            int w = img.W;
            int i00 = ((sy + y0) * w + sx + x0) * 4, i10 = ((sy + y0) * w + sx + x1) * 4;
            int i01 = ((sy + y1) * w + sx + x0) * 4, i11 = ((sy + y1) * w + sx + x1) * 4;
            float w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy;
            float a00 = p[i00 + 3] * w00, a10 = p[i10 + 3] * w10, a01 = p[i01 + 3] * w01, a11 = p[i11 + 3] * w11;
            float at = a00 + a10 + a01 + a11;
            a = at / 255f;
            if (at <= 0.001f) { r = g = b = 0f; return; }
            r = (p[i00] * a00 + p[i10] * a10 + p[i01] * a01 + p[i11] * a11) / at;
            g = (p[i00 + 1] * a00 + p[i10 + 1] * a10 + p[i01 + 1] * a01 + p[i11 + 1] * a11) / at;
            b = (p[i00 + 2] * a00 + p[i10 + 2] * a10 + p[i01 + 2] * a01 + p[i11 + 2] * a11) / at;
        }

        static void Part(Canvas c, Art art, string name, float x0, float y0, float x1, float y1, int tint = -1, float alpha = 1f, Clip clip = default)
        {
            if (!art.Parts.TryGetValue(name, out var p)) return;
            Draw(c, art.Img, p[0], p[1], p[2], p[3], x0, y0, x1, y1, tint, alpha, clip);
        }

        static float Aspect(Art art, string name) => art.Parts.TryGetValue(name, out var p) && p[3] > 0 ? (float)p[2] / p[3] : 1f;

        // ── text ────────────────────────────────────────────────────────────────────────────────────────────────────

        static Glyph? GlyphOf(Font f, char ch)
        {
            if (f.G.TryGetValue(ch, out var g)) return g;
            if (f.G.TryGetValue('?', out g)) return g;
            return null;
        }

        /// <summary>The width of a text whose capital letters are <paramref name="cap"/> pixels high.</summary>
        static float Width(Font f, string s, float cap)
        {
            float k = cap / f.Cap, w = 0f;
            foreach (var ch in s) { var g = GlyphOf(f, ch); if (g != null) w += g.Adv * k; }
            return w;
        }

        /// <summary>The cap height that fits the text into maxW (at most cap; at least min - then it is cut).</summary>
        static float Fit(Font f, ref string s, float cap, float maxW, float min)
        {
            float w = Width(f, s, cap);
            if (w <= maxW || w <= 0f) return cap;
            float k = cap * maxW / w;
            if (k >= min) return k;
            while (s.Length > 1 && Width(f, s + "…", min) > maxW) s = s.Substring(0, s.Length - 1).TrimEnd();
            s += "…";
            return min;
        }

        /// <summary>Draws a text; align 0 left, 1 centre, 2 right of x; y is the baseline.</summary>
        static float Text(Canvas c, Font f, string s, float x, float y, float cap, int rgb, int align = 0)
        {
            if (string.IsNullOrEmpty(s) || cap < 2f) return 0f;
            float k = cap / f.Cap, w = Width(f, s, cap);
            float pen = align == 1 ? x - w * 0.5f : align == 2 ? x - w : x;
            foreach (var ch in s)
            {
                var g = GlyphOf(f, ch);
                if (g == null) continue;
                if (g.W > 0 && g.H > 0)
                {
                    float gx0 = pen + g.Left * k, gy1 = y + g.Top * k;
                    Draw(c, f.Atlas, g.X, g.Y, g.W, g.H, gx0, gy1 - g.H * k, gx0 + g.W * k, gy1, rgb);
                }
                pen += g.Adv * k;
            }
            return w;
        }

        // ── the livery ──────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The picture for a spec: RGB bytes, Size x Size, row 0 at the bottom. logo: the business logo (or null).</summary>
        public static byte[] Compose(Spec s, Layout L, Font f, Art art, Img? logo)
        {
            var c = new Canvas(L.Size, L.Size, s.Paint & 0xFFFFFF, false);
            PaintOn(c, s, L, f, art, logo);
            return c.Px;
        }

        /// <summary>GO-LIVERY-3 (the game cars' paint for the van, user 2026-11-10 "Weiter machen"): the same picture for the
        /// game's vehicle shader (SH_Vehicle, its "Diffuse+mask"): RGBA, the stickers in their own colours with alpha 0, the
        /// bare paint white with alpha 255 - the shader paints those parts in the vehicle's colour (CarFeatures.SetColor: the
        /// dealer's colour choice) with its paint's gloss and fresnel (base colour = lerp(texture, tint, smoothstep(alpha))).
        /// The lettering still picks its colour against s.Paint.</summary>
        public static byte[] ComposeMasked(Spec s, Layout L, Font f, Art art, Img? logo)
        {
            var c = new Canvas(L.Size, L.Size, 0, true);
            PaintOn(c, s, L, f, art, logo);
            var px = c.Px;
            for (int i = 0; i < px.Length; i += 4)
            {
                int cover = px[i + 3];
                if (cover == 0) { px[i] = 255; px[i + 1] = 255; px[i + 2] = 255; }
                px[i + 3] = (byte)(255 - cover);
            }
            return px;
        }

        static void PaintOn(Canvas c, Spec s, Layout L, Font f, Art art, Img? logo)
        {
            if (s.Sides != null && s.Sides.Length >= 4)
            {   // CAR-LIVERY-1: every side its own look
                string[] fields = { "L", "R", "B", "H" };
                for (int i = 0; i < 4; i++)
                    if (s.Sides[i] != null && s.Sides[i].Design != DNone && L.R.TryGetValue(fields[i], out var g))
                        VanSide(c, g, i, s, s.Sides[i], f, art, logo);
                return;
            }
            if (s.Motif == 3) return;
            foreach (var side in new[] { "L", "R" })
                if (L.R.TryGetValue(side, out var g)) Side(c, g, s, f, art, logo, 2);
            if (L.R.TryGetValue("B", out var back)) Back(c, back, s, f, art, 1);
            if (L.R.TryGetValue("H", out var hood)) Hood(c, hood, s, f, art);
        }

        /// <summary>CAR-LIVERY-1: one field of the van in a look of its own. which: 0 left, 1 right, 2 back, 3 hood.</summary>
        static void VanSide(Canvas c, Region g, int which, Spec s, Look look, Font f, Art art, Img? logo)
        {
            var t = s.Clone();
            t.Sides = null;
            t.Panel = look.Color & 0xFFFFFF;
            t.TextPct = look.Text; t.LogoPct = look.Logo;   // GO-LIVERY-4: the side's own sizes
            int place = Clamp(look.Place, 0, 2);
            if (look.Design == DGoFibre || look.Design == DGoTech)
            {
                t.Motif = look.Design == DGoTech ? 1 : 0;
                if (which <= 1) Side(c, g, t, f, art, logo, place);
                else if (which == 2) Back(c, g, t, f, art, place);
                else Hood(c, g, t, f, art);
                return;
            }
            // the logo and the name, a panel, a band or a wrap: where they may stand on the van (its plane, metres)
            (float a0, float b0, float a1, float b1) area, zone;
            float k = 1f;
            switch (which)
            {
                case 2:
                    area = (-0.62f, 0.62f, 0.62f, 1.18f);
                    zone = place == 0 ? (0.03f, 0.62f, 0.62f, 1.18f) : place == 2 ? (-0.62f, 0.62f, -0.03f, 1.18f) : area;   // left door = plane +
                    k = 0.62f;
                    break;
                case 3:
                    area = (-0.60f, -2.06f, 0.60f, -1.56f);
                    zone = area;
                    k = 0.62f;
                    break;
                default:   // under the front door's window, on the sliding door, on the load space
                    area = (-1.00f, 0.80f, 2.10f, 1.70f);
                    zone = place == 0 ? (-1.00f, 0.64f, -0.42f, 1.06f) : place == 1 ? (-0.24f, 0.80f, 0.74f, 1.68f) : (0.82f, 0.80f, 2.08f, 1.68f);
                    break;
            }
            var A = Rect(g, area.a0, area.b0, area.a1, area.b1);
            var Z = Rect(g, zone.a0, zone.b0, zone.a1, zone.b1);
            var ra = new R4(A.x0, A.y0, A.x1, A.y1);
            var rz = new R4(Z.x0, Z.y0, Z.x1, Z.y1);
            if (look.Design == DWrap)
            {   // the wrap covers the whole field (every face of that side); windows stay glass
                ra = new R4(g.X, g.Y, g.X + g.W, g.Y + g.H);
            }
            else if (look.Design == DBand && which <= 1)
            {   // the band runs along the whole side under the windows, over the wheel arches
                var bnd = Rect(g, -2.32f, 0.86f, 2.18f, 1.08f);
                ra = new R4(bnd.x0, bnd.y0, bnd.x1, bnd.y1);
                var zb = Rect(g, zone.a0, 0.70f, zone.a1, 1.24f);
                rz = new R4(zb.x0, zb.y0, zb.x1, zb.y1);
            }
            Generic(c, f, art, t, look, logo, ra, rz, A.ppm, 1, k);
        }

        /// <summary>Metres of the body's plane to pixels of a field: (x0, y0, x1, y1) and pixels per metre.</summary>
        static (float x0, float y0, float x1, float y1, float ppm) Rect(Region g, float a0, float b0, float a1, float b1)
        {
            float sx = g.W / (g.A1 - g.A0), sy = g.H / (g.B1 - g.B0);
            float xa = g.Flip ? g.X + (g.A1 - a1) * sx : g.X + (a0 - g.A0) * sx;
            float xb = g.Flip ? g.X + (g.A1 - a0) * sx : g.X + (a1 - g.A0) * sx;
            return (xa, g.Y + (b0 - g.B0) * sy, xb, g.Y + (b1 - g.B0) * sy, sx);
        }

        static string Lettering(Spec s) => s.Name.Trim().Length > 0 ? s.Name.Trim() : (s.Fallback ?? "");

        /// <summary>A rectangle in pixels.</summary>
        struct R4
        {
            public float X0, Y0, X1, Y1;
            public R4(float x0, float y0, float x1, float y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
            public float W => X1 - X0;
            public float H => Y1 - Y0;
        }

        /// <summary>The name as a word mark: the signal sign and the name, left at x (or centred), y = the middle.</summary>
        /// <summary>GO-LIVERY-4: the lettering's and the logo's size as a factor (1 = as laid out).</summary>
        static float TK(Spec s) => Clamp(s.TextPct, 50, 200) / 100f;
        static float LK(Spec s) => Clamp(s.LogoPct, 50, 200) / 100f;

        static void WordMark(Canvas c, Font f, Art art, string name, float x, float y, float cap, float maxW, int rgb, bool centre)
        {
            float sign = cap * 1.35f, gap = cap * 0.30f;
            cap = Fit(f, ref name, cap, maxW - sign - gap, cap * 0.45f);
            sign = Math.Min(sign, cap * 1.35f);
            float w = sign + gap + Width(f, name, cap);
            float x0 = centre ? x - w * 0.5f : x;
            Part(c, art, "signal", x0, y - sign * 0.5f, x0 + sign, y + sign * 0.5f, rgb);
            Text(c, f, name, x0 + sign + gap, y - cap * 0.5f, cap, rgb);
        }

        /// <summary>The colour panel: the logo, the name, the picture of the motif.</summary>
        static void Panel(Canvas c, Font f, Art art, Spec s, Img? logo, float x0, float y0, float x1, float y1, float ppm, bool small)
        {
            float rad = (small ? 0.03f : 0.04f) * ppm, h = y1 - y0, w = x1 - x0;
            Box(c, x0, y0, x1, y1, rad, s.Panel);
            var clip = new Clip { On = true, X0 = x0, Y0 = y0, X1 = x1, Y1 = y1, R = rad };
            string pic = s.Motif == 1 ? "techpic" : "burst";
            float pw = h * Aspect(art, pic);
            if (s.Motif == 1)
            {   // the scene stands on the panel's lower edge, a little in from the right
                float ph = h * 0.96f;
                pw = ph * Aspect(art, pic);
                Part(c, art, pic, x1 - pw - 0.01f * ppm, y0, x1 - 0.01f * ppm, y0 + ph, -1, 1f, clip);
            }
            else Part(c, art, pic, x1 - pw, y0, x1, y1, -1, 1f, clip);
            int ink = OnPanel(s.Panel);
            float m = 0.045f * ppm, x = x0 + m;
            if (logo != null && s.Logo && !small)
            {   // GO-LIVERY-4: the square grows with the logo's size up to the panel's height; beyond that the picture
                // grows inside it (its middle, cut at the square)
                float want = h * 0.70f * LK(s), side = Math.Min(h * 0.94f, want), b = 0.008f * ppm, ly0 = y0 + (h - side) * 0.5f;
                Box(c, x, ly0, x + side, ly0 + side, 0.015f * ppm, White);
                float zoom = Math.Max(1f, want / side);
                float iw = side - 2f * b, ih = iw * logo.H / Math.Max(1, logo.W);
                if (ih > side - 2f * b) { ih = side - 2f * b; iw = ih * logo.W / Math.Max(1, logo.H); }
                float lx = x + (side - iw) * 0.5f, ly = ly0 + (side - ih) * 0.5f;
                int sw = Math.Max(1, (int)(logo.W / zoom)), sh = Math.Max(1, (int)(logo.H / zoom));
                Draw(c, logo, (logo.W - sw) / 2, (logo.H - sh) / 2, sw, sh, lx, ly, lx + iw, ly + ih);
                x += side + 0.035f * ppm;
            }
            // the name may run into the picture's thin left part (the light's ends, the light disc behind the scene)
            float limit = x1 - pw + pw * (s.Motif == 1 ? 0.16f : 0.34f);
            string name = Lettering(s);
            float tk = TK(s);
            float maxW = limit - x, big = Math.Min(h * 0.62f, h * (small ? 0.30f : 0.27f) * tk);
            if (Width(f, name, big * 0.66f) > maxW && !small && Split(name, out var l1, out var l2))
            {   // a long name in two lines (at a space or after a hyphen)
                float c2 = Math.Min(h * 0.40f, h * 0.20f * tk), m2 = h * 0.09f * Math.Min(tk, 1f);
                float cap2 = Math.Min(c2, Math.Min(Fit(f, ref l1, c2, maxW, m2), Fit(f, ref l2, c2, maxW, m2)));
                float gap = cap2 * 0.45f, top = y0 + (h + cap2 * 2f + gap) * 0.5f;
                Text(c, f, l1, x, top - cap2, cap2, ink);
                Text(c, f, l2, x, top - cap2 * 2f - gap, cap2, ink);
                return;
            }
            float cap = Fit(f, ref name, big, maxW, h * 0.10f * Math.Min(tk, 1f));
            Text(c, f, name, x, y0 + (h - cap) * 0.5f, cap, ink);
        }

        /// <summary>A name in two lines of about equal length: at the space or hyphen nearest the middle.</summary>
        static bool Split(string name, out string l1, out string l2)
        {
            l1 = name; l2 = "";
            int best = -1;
            for (int i = 1; i < name.Length - 1; i++)
                if ((name[i] == ' ' || name[i] == '-') && (best < 0 || Math.Abs(i - name.Length / 2) < Math.Abs(best - name.Length / 2))) best = i;
            if (best < 0) return false;
            l1 = name[best] == '-' ? name.Substring(0, best + 1) : name.Substring(0, best).TrimEnd();
            l2 = name.Substring(best + 1).TrimStart();
            return l1.Length > 0 && l2.Length > 0;
        }

        /// <summary>A side of the van. place (CAR-LIVERY-1): where the colour panel stands - 2 on the load space (Entwurf 1,
        /// the app's look), 1 on the sliding door (the offers move to the load space), 0 small on the front door under its
        /// window (the name then stands large on the load space and "Techniker im Einsatz" has no room).</summary>
        static void Side(Canvas c, Region g, Spec s, Font f, Art art, Img? logo, int place)
        {
            int ink = OnPaint(s.Paint & 0xFFFFFF, s.Panel);
            // where the parts stand (the van's plane, metres)
            (float a0, float b0, float a1, float b1) panel = (0.82f, 1.255f, 2.06f, 1.675f), lines = (0.82f, 0.945f, 2.06f, 1.145f);
            (float a0, float b0, float a1, float b1) offersAt = (-0.20f, 1.24f, 0.66f, 1.64f), cableAt = (-0.33f, 0.70f, 0.72f, 1.00f);
            bool tech = s.Tech, cable = s.Cable, bigName = false;
            if (place == 1)
            {
                panel = (-0.26f, 1.255f, 0.76f, 1.675f); lines = (-0.26f, 1.03f, 0.76f, 1.20f);
                offersAt = (0.96f, 1.24f, 1.94f, 1.64f); cableAt = (0.95f, 0.80f, 2.05f, 1.10f);
            }
            else if (place == 0)
            {
                panel = (-1.00f, 0.64f, -0.42f, 1.06f); tech = false; bigName = true;
            }
            var p = Rect(g, panel.a0, panel.b0, panel.a1, panel.b1);
            if (s.Motif <= 1) Panel(c, f, art, s, logo, p.x0, p.y0, p.x1, p.y1, p.ppm, false);
            else WordMark(c, f, art, Lettering(s), p.x0 + 0.02f * p.ppm, (p.y0 + p.y1) * 0.5f, Math.Min(0.30f, 0.11f * TK(s)) * p.ppm, p.x1 - p.x0 - 0.04f * p.ppm, ink, false);
            if (bigName && s.Motif <= 1)
            {   // the panel is small on the front door: the name large on the load space
                var w = Rect(g, 0.82f, 1.255f, 2.06f, 1.675f);
                WordMark(c, f, art, Lettering(s), w.x0 + 0.02f * w.ppm, (w.y0 + w.y1) * 0.5f, Math.Min(0.30f, 0.11f * TK(s)) * w.ppm, w.x1 - w.x0 - 0.04f * w.ppm, ink, false);
            }
            var lr = Rect(g, lines.a0, lines.b0, lines.a1, lines.b1);
            float mid = (lr.x0 + lr.x1) * 0.5f, wide = (lr.x1 - lr.x0) * 0.94f, lh = lr.y1 - lr.y0;
            // the slogan and the contact line under the panel (under the rail)
            if (s.Slogan.Trim().Length > 0)
            {
                float qy0 = lr.y0 + lh * 0.55f, qy1 = lr.y1;
                string t = s.Slogan.Trim();
                float band = qy1 - qy0, want = Math.Min(band * 1.25f, band * 0.78f * TK(s));
                float cap = Fit(f, ref t, want, wide, Math.Min(want, band * 0.40f));
                Text(c, f, t, mid, qy0 + ((qy1 - qy0) - cap) * 0.5f, cap, ink, 1);
            }
            if (s.Contact.Trim().Length > 0)
            {
                float qy0 = lr.y0, qy1 = lr.y0 + lh * 0.25f;
                string t = s.Contact.Trim();
                float band = qy1 - qy0, want = Math.Min(band * 1.40f, band * 0.72f * TK(s));
                float cap = Fit(f, ref t, want, wide, Math.Min(want, band * 0.45f));
                Text(c, f, t, mid, qy0 + ((qy1 - qy0) - cap) * 0.5f, cap, ink, 1);
            }
            // the offers on the upper sliding door (or the load space)
            var offers = new List<(int bit, string label, int color, string sym)>();
            for (int i = 0; i < Offers.Length; i++) if ((s.Services & Offers[i].bit) != 0) offers.Add((Offers[i].bit, LabelOf(i), Offers[i].color, Offers[i].sym));
            if (offers.Count > 0)
            {   // up to four in one column, five or six in two
                var q = Rect(g, offersAt.a0, offersAt.b0, offersAt.a1, offersAt.b1);
                int cols = offers.Count > 4 ? 2 : 1, rows = (offers.Count + cols - 1) / cols;
                float gap = 0.016f * q.ppm, colGap = 0.02f * q.ppm;
                float bar = Math.Min(0.085f * q.ppm, ((q.y1 - q.y0) - gap * (rows - 1)) / rows);
                float colW = ((q.x1 - q.x0) * (cols == 1 ? 0.92f : 1f) - colGap * (cols - 1)) / cols;
                int pink = OnPanel(s.Panel);
                float labelCap = bar * 0.50f;
                for (int i = 0; i < offers.Count; i++)
                {
                    var o = offers[i];
                    int col = i / rows, row = i % rows;
                    float cx0 = q.x0 + col * (colW + colGap);
                    float top = q.y1 - row * (bar + gap), bot = top - bar;
                    Box(c, cx0, bot, cx0 + bar, top, 0f, o.color);
                    Part(c, art, o.sym, cx0 + bar * 0.1f, bot + bar * 0.1f, cx0 + bar * 0.9f, top - bar * 0.1f, White);
                    float lx0 = cx0 + bar + 0.006f * q.ppm, lx1 = cx0 + colW;
                    Box(c, lx0, bot, lx1, top, 0f, s.Panel);
                    string label = o.label;
                    float cap = Fit(f, ref label, labelCap, lx1 - lx0 - bar * 0.45f, labelCap * 0.6f);
                    Text(c, f, label, lx0 + bar * 0.28f, bot + (bar - cap) * 0.5f, cap, pink);
                }
            }
            // the cable with light on the lower sliding door (or the load space)
            if (cable)
            {
                var q = Rect(g, cableAt.a0, cableAt.b0, cableAt.a1, cableAt.b1);
                Part(c, art, "cable", q.x0, q.y0, q.x1, q.y1);
            }
            // "Techniker im Einsatz" on the front door, under its window
            if (tech)
            {
                var q = Rect(g, -1.02f, 0.88f, -0.58f, 1.04f);
                float h = q.y1 - q.y0, icon = h * 0.92f;
                Part(c, art, "techicon", q.x0, q.y0 + (h - icon) * 0.5f, q.x0 + icon, q.y0 + (h + icon) * 0.5f, ink);
                float tx = q.x0 + icon + h * 0.12f, maxW = q.x1 - tx;
                string l1 = TechLine1, l2 = TechLine2;
                float cap = Math.Min(Fit(f, ref l1, h * 0.30f, maxW, h * 0.15f), Fit(f, ref l2, h * 0.30f, maxW, h * 0.15f));
                Text(c, f, l1, tx, q.y0 + h * 0.56f, cap, ink);
                Text(c, f, l2, tx, q.y0 + h * 0.14f, cap, ink);
            }
        }

        /// <summary>The back doors under their windows: a small panel with the name, the offers, the slogan, the contact.
        /// place (CAR-LIVERY-1): 1 across both doors (the app's look), 0 on the left door, 2 on the right one.</summary>
        static void Back(Canvas c, Region g, Spec s, Font f, Art art, int place)
        {
            int ink = OnPaint(s.Paint & 0xFFFFFF, s.Panel);
            // the field's plane runs from the car's right (-) to its left (+): seen from behind, the left door is on the left
            var r = place == 0 ? Rect(g, 0.04f, 0.62f, 0.62f, 1.18f) : place == 2 ? Rect(g, -0.62f, 0.62f, -0.04f, 1.18f) : Rect(g, -0.60f, 0.62f, 0.60f, 1.18f);
            float h = r.y1 - r.y0, w = r.x1 - r.x0, mid = (r.x0 + r.x1) * 0.5f;
            float ph = h * 0.42f;
            if (s.Motif <= 1) Panel(c, f, art, s, null, r.x0, r.y1 - ph, r.x1, r.y1, r.ppm, true);
            else WordMark(c, f, art, Lettering(s), mid, r.y1 - ph * 0.5f, ph * 0.40f, w * 0.96f, ink, true);
            var names = new List<string>();
            for (int i = 0; i < Offers.Length; i++) if ((s.Services & Offers[i].bit) != 0) names.Add(LabelOf(i));
            string offer = string.Join(" · ", names);
            float y = r.y1 - ph - h * 0.06f;
            float tkb = TK(s);
            if (offer.Length > 0)
            {
                float cap = Fit(f, ref offer, h * 0.058f * tkb, w * 0.98f, h * 0.03f * Math.Min(tkb, 1f));
                y -= cap; Text(c, f, offer, mid, y, cap, ink, 1); y -= h * 0.07f;
            }
            string sl = s.Slogan.Trim();
            if (sl.Length > 0)
            {
                float cap = Fit(f, ref sl, h * 0.075f * tkb, w * 0.92f, h * 0.035f * Math.Min(tkb, 1f));
                y -= cap; Text(c, f, sl, mid, y, cap, ink, 1); y -= h * 0.07f;
            }
            string ct = s.Contact.Trim();
            if (ct.Length > 0)
            {
                float cap = Fit(f, ref ct, h * 0.042f * tkb, w * 0.96f, h * 0.025f * Math.Min(tkb, 1f));
                y -= cap; Text(c, f, ct, mid, y, cap, ink, 1);
            }
        }

        /// <summary>The hood: the name as a word mark, readable from the front.</summary>
        static void Hood(Canvas c, Region g, Spec s, Font f, Art art)
        {
            int ink = OnPaint(s.Paint & 0xFFFFFF, s.Panel);
            var r = Rect(g, -0.52f, -2.00f, 0.52f, -1.62f);
            WordMark(c, f, art, Lettering(s), (r.x0 + r.x1) * 0.5f, (r.y0 + r.y1) * 0.5f, (r.y1 - r.y0) * Math.Min(0.80f, 0.42f * TK(s)), (r.x1 - r.x0) * 0.98f, ink, true);
        }

        // ── CAR-LIVERY-1: the looks every car can wear ──────────────────────────────────────────────────────────────

        /// <summary>The logo on a white rounded square, side s pixels, its lower left corner at (x, y).</summary>
        static void LogoSquare(Canvas c, Img logo, float x, float y, float s, float ppm)
        {
            float b = Math.Max(1f, 0.010f * ppm);
            Box(c, x, y, x + s, y + s, Math.Min(s * 0.12f, 0.02f * ppm), White);
            float iw = s - 2f * b, ih = iw * logo.H / Math.Max(1, logo.W);
            if (ih > s - 2f * b) { ih = s - 2f * b; iw = ih * logo.W / Math.Max(1, logo.H); }
            float lx = x + (s - iw) * 0.5f, ly = y + (s - ih) * 0.5f;
            Draw(c, logo, 0, 0, logo.W, logo.H, lx, ly, lx + iw, ly + ih);
        }

        /// <summary>A side's look in a field. area: where a band or a wrap reaches; zone: where the logo and the name stand
        /// (align 0 at its left end, 1 in its middle, 2 at its right end); ppm: pixels per metre; k scales the lettering
        /// (smaller on a back or a hood).</summary>
        static void Generic(Canvas c, Font f, Art art, Spec s, Look look, Img? logo, R4 area, R4 zone, float ppm, int align, float k)
        {
            if (look.Text != s.TextPct || look.Logo != s.LogoPct) { s = s.Clone(); s.TextPct = look.Text; s.LogoPct = look.Logo; }   // GO-LIVERY-4
            int paint = s.Paint & 0xFFFFFF, col = look.Color & 0xFFFFFF;
            if (!s.Logo) logo = null;
            switch (look.Design)
            {
                case DWrap:
                    Box(c, area.X0, area.Y0, area.X1, area.Y1, 0.012f * ppm, col);
                    Block(c, f, logo, s, zone, ppm, align, OnPanel(col), -1, 0.50f * k, true);
                    break;
                case DBand:
                {
                    float bh = Math.Min(zone.H * 0.50f, 0.20f * k * ppm), mid = (zone.Y0 + zone.Y1) * 0.5f;
                    Box(c, area.X0, mid - bh * 0.5f, area.X1, mid + bh * 0.5f, 0f, col);
                    BandBlock(c, f, logo, s, zone, mid, bh, ppm, align, OnPanel(col), OnPaint(paint, col));
                    break;
                }
                case DPanel:
                    Block(c, f, logo, s, zone, ppm, align, OnPanel(col), col, 0.46f * k, false);
                    break;
                case DGoFibre:
                case DGoTech:
                {   // Going Online's panel on another car: the logo, the name and the picture of the motif
                    var t = s.Clone(); t.Motif = look.Design == DGoTech ? 1 : 0; t.Panel = col;
                    float h = Math.Min(zone.H, 0.44f * k * ppm), w = Math.Min(zone.W, h * 3.0f);
                    float x0 = align == 0 ? zone.X0 : align == 2 ? zone.X1 - w : (zone.X0 + zone.X1 - w) * 0.5f, y0 = (zone.Y0 + zone.Y1 - h) * 0.5f;
                    Panel(c, f, art, t, logo, x0, y0, x0 + w, y0 + h, ppm, false);
                    break;
                }
                default:   // DLogoName
                    Block(c, f, logo, s, zone, ppm, align, OnPaint(paint, col), -1, 0.46f * k, false);
                    break;
            }
        }

        /// <summary>The logo and the name - the slogan under the name when there is room, the contact line under all when
        /// asked - in a zone; bg >= 0: on a rounded panel of that colour. maxH: the block's height at most (pixels per
        /// metre times metres).</summary>
        static void Block(Canvas c, Font f, Img? logo, Spec s, R4 z, float ppm, int align, int ink, int bg, float maxH, bool contactLine)
        {
            string name = Lettering(s), slogan = s.Slogan.Trim(), contact = contactLine ? s.Contact.Trim() : "";
            float pad = bg >= 0 ? 0.045f * ppm : 0.01f * ppm;
            float h = Math.Min(z.H * (contact.Length > 0 ? 0.80f : 0.92f), maxH * ppm), inner = h - 2f * pad;
            if (inner < 4f) return;
            float tk = TK(s);
            float logoS = logo != null ? Math.Min(inner * 0.98f, inner * (bg >= 0 ? 0.86f : 0.92f) * LK(s)) : 0f, gap = logo != null && name.Length > 0 ? inner * 0.18f : 0f;
            float textMax = z.W - 2f * pad - logoS - gap;
            if (name.Length > 0 && textMax < 0.12f * ppm && logo != null)
            {   // no room beside the logo: the name alone
                logoS = 0f; gap = 0f; textMax = z.W - 2f * pad;
            }
            bool two = slogan.Length > 0 && name.Length > 0 && inner > 0.14f * ppm;
            float nameCap = Math.Min(inner * (two ? 0.56f : 0.90f), inner * (two ? 0.34f : 0.40f) * tk), slCap = nameCap * 0.50f;
            string n1 = name, n2 = "";
            float nc = 0f, sc = 0f, textW = 0f;
            if (name.Length > 0)
            {
                if (Width(f, name, nameCap * 0.72f) > textMax && Split(name, out var l1, out var l2))
                {   // a long name in two lines (then no slogan beside the logo)
                    two = false; n1 = l1; n2 = l2;
                    float c2 = Math.Min(inner * 0.46f, inner * 0.30f * tk), m2 = inner * 0.12f * Math.Min(tk, 1f);
                    nc = Math.Min(Fit(f, ref n1, c2, textMax, m2), Fit(f, ref n2, c2, textMax, m2));
                    textW = Math.Max(Width(f, n1, nc), Width(f, n2, nc));
                }
                else
                {
                    nc = Fit(f, ref n1, nameCap, textMax, nameCap * 0.40f);
                    textW = Width(f, n1, nc);
                }
            }
            string sl = slogan;
            if (two) { sc = Fit(f, ref sl, slCap, textMax, slCap * 0.55f); textW = Math.Max(textW, Width(f, sl, sc)); }
            float bw = 2f * pad + logoS + gap + textW;
            if (bw > z.W) bw = z.W;
            float x0 = align == 0 ? z.X0 : align == 2 ? z.X1 - bw : (z.X0 + z.X1 - bw) * 0.5f;
            float cy = contact.Length > 0 ? z.Y0 + z.H * 0.58f : (z.Y0 + z.Y1) * 0.5f, y0 = cy - h * 0.5f;
            if (bg >= 0) Box(c, x0, y0, x0 + bw, y0 + h, 0.035f * ppm, bg);
            float x = x0 + pad;
            if (logoS > 0f) { LogoSquare(c, logo!, x, y0 + (h - logoS) * 0.5f, logoS, ppm); x += logoS + gap; }
            if (n2.Length > 0)
            {
                float g2 = nc * 0.45f, top = y0 + (h + nc * 2f + g2) * 0.5f;
                Text(c, f, n1, x, top - nc, nc, ink);
                Text(c, f, n2, x, top - nc * 2f - g2, nc, ink);
            }
            else if (two)
            {
                float g2 = nc * 0.42f, total = nc + g2 + sc, top = y0 + (h + total) * 0.5f;
                Text(c, f, n1, x, top - nc, nc, ink);
                Text(c, f, sl, x, top - total, sc, ink);
            }
            else if (n1.Length > 0) Text(c, f, n1, x, y0 + (h - nc) * 0.5f, nc, ink);
            if (contact.Length > 0)
            {   // the contact line under the block, across the zone
                float room = y0 - z.Y0, cc = Math.Min(room * 0.55f, nc * 0.42f);
                if (cc >= 3f)
                {
                    float mid = align == 0 ? x0 : align == 2 ? x0 + bw : x0 + bw * 0.5f;
                    float cw = align == 1 ? z.W * 0.96f : z.W * 0.96f;
                    cc = Fit(f, ref contact, cc, cw, cc * 0.6f);
                    Text(c, f, contact, mid, z.Y0 + (room - cc) * 0.5f, cc, ink, align);
                }
            }
        }

        /// <summary>The band's lettering: the logo on its white square across the band, the name on the band, the slogan
        /// under the band on the paint.</summary>
        static void BandBlock(Canvas c, Font f, Img? logo, Spec s, R4 z, float mid, float bh, float ppm, int align, int inkBand, int inkPaint)
        {
            string name = Lettering(s), slogan = s.Slogan.Trim();
            float logoS = logo != null ? Math.Min(bh * 1.55f * LK(s), z.H * 0.96f) : 0f, gap = logo != null ? bh * 0.30f : 0f;
            float cap = Math.Min(bh * 0.88f, bh * 0.50f * TK(s)), textMax = z.W - logoS - gap - 0.04f * ppm;
            string n = name;
            float nc = n.Length > 0 ? Fit(f, ref n, cap, Math.Max(1f, textMax), cap * 0.45f) : 0f;
            float bw = logoS + gap + (n.Length > 0 ? Width(f, n, nc) : 0f);
            float x0 = align == 0 ? z.X0 + 0.02f * ppm : align == 2 ? z.X1 - 0.02f * ppm - bw : (z.X0 + z.X1 - bw) * 0.5f;
            if (logoS > 0f) LogoSquare(c, logo!, x0, mid - logoS * 0.5f, logoS, ppm);
            if (n.Length > 0) Text(c, f, n, x0 + logoS + gap, mid - nc * 0.5f, nc, inkBand);
            if (slogan.Length > 0)
            {
                float room = (mid - bh * 0.5f) - z.Y0, sc = Math.Min(room * 0.55f, nc * 0.62f);
                if (sc >= 3f)
                {
                    float sx = x0 + logoS + gap;
                    sc = Fit(f, ref slogan, sc, Math.Max(1f, z.X1 - sx), sc * 0.55f);
                    Text(c, f, slogan, sx, mid - bh * 0.5f - room * 0.5f - sc * 0.5f, sc, inkPaint);
                }
            }
        }

        /// <summary>GO-PLATE-1: a number plate - white with a dark rim, the text as large as fits, dark; RGB, rows from the
        /// bottom (as a texture of the game wants them).</summary>
        public static byte[] ComposePlate(string text, Font f, int w, int h)
        {
            var c = new Canvas(w, h, 0x1E1E1E, false);
            float rim = Math.Max(2f, h * 0.06f);
            Box(c, rim, rim, w - rim, h - rim, h * 0.10f, 0xF4F4F0);
            string t = (text ?? "").Trim();
            if (t.Length > 0)
            {
                float cap = Fit(f, ref t, h * 0.56f, w - rim * 2f - h * 0.30f, h * 0.30f);
                Text(c, f, t, w * 0.5f, (h - cap) * 0.5f, cap, 0x141414, 1);
            }
            return c.Px;
        }

        /// <summary>GO-STREET-2 (user 2026-11-11: "Verteilerkästen mit Firmenlogo und Name"): the label on a street cabinet -
        /// a white plate in a dark rim, a band in the provider's colour along its top, the business logo on the left and the
        /// company name beside it (smaller for a long name). RGB, rows from the bottom.</summary>
        public static byte[] ComposeLabel(Img? logo, string name, Font f, int w, int h, int accent)
        {
            var c = new Canvas(w, h, 0x2E2E2E, false);
            float rim = Math.Max(2f, h * 0.035f), band = h * 0.15f, pad = h * 0.11f;
            Box(c, rim, rim, w - rim, h - rim, h * 0.06f, 0xF6F6F2);
            Box(c, rim, h - rim - band, w - rim, h - rim, 0f, accent & 0xFFFFFF);
            float bottom = rim + pad, top = h - rim - band - pad * 0.7f, side = top - bottom, x = rim + pad;
            if (logo != null && logo.W > 0 && logo.H > 0 && side > 4f)
            {
                float k = Math.Min(side / logo.W, side / logo.H), lw = logo.W * k, lh = logo.H * k;
                float ly = bottom + (side - lh) * 0.5f;
                Draw(c, logo, 0, 0, logo.W, logo.H, x + (side - lw) * 0.5f, ly, x + (side - lw) * 0.5f + lw, ly + lh);
                x += side + pad;
            }
            string t = (name ?? "").Trim();
            if (t.Length > 0)
            {
                float cap = Fit(f, ref t, side * 0.5f, w - rim - pad - x, side * 0.26f);
                Text(c, f, t, x, bottom + (side - cap) * 0.5f, cap, 0x161616, 0);
            }
            return c.Px;
        }

        /// <summary>CAR-LIVERY-1: one side's picture for a car of the game - a decal over the free surface the car's model
        /// leaves there (wM x hM metres). RGBA on a clear ground, rows from the bottom; null for no look. frontAt: where the
        /// car's front is in the picture (0 left, 2 right; 1 none - the back, where place 0 is the left, and the hood).
        /// kind: 0 a side, 1 the back, 2 the hood.</summary>
        public static byte[]? ComposeDecal(Spec s, Look look, float wM, float hM, int frontAt, int kind, Font f, Art art, Img? logo,
                                           float ppmWant, out int w, out int h)
        {
            w = h = 0;
            if (look == null || look.Design == DNone || wM < 0.15f || hM < 0.08f) return null;
            float ppm = Math.Min(ppmWant, Math.Min(1024f / wM, 512f / hM));
            w = Clamp((int)Math.Round(wM * ppm), 8, 1024);
            h = Clamp((int)Math.Round(hM * ppm), 8, 512);
            var c = new Canvas(w, h, 0, true);
            float m = Math.Min(0.06f * ppm, w * 0.06f);
            var area = new R4(0f, 0f, w, h);
            var zone = new R4(m, 0f, w - m, h);
            int place = Clamp(look.Place, 0, 2);
            int align = place == 1 || kind == 2 ? 1 : frontAt == 2 ? (place == 0 ? 2 : 0) : (place == 0 ? 0 : 2);
            Generic(c, f, art, s, look, logo, area, zone, ppm, align, kind == 0 ? 1f : 0.72f);
            return c.Px;
        }

        /// <summary>The default slogan: "#" and the name in capitals, letters and digits only.</summary>
        public static string DefaultSlogan(string name)
        {
            var sb = new StringBuilder("#");
            foreach (var ch in name ?? "") if (char.IsLetterOrDigit(ch)) sb.Append(char.ToUpperInvariant(ch));
            return sb.Length > 1 ? sb.ToString() : "";
        }
    }
}
