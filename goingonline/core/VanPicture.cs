using System;

namespace GoingOnline
{
    /// <summary>GO-LIVERY-2 (user 2026-11-10 at the picture in the tab "Service van": "die Fahrzeug-Umrandungen fehlen? oder
    /// kommen die erst wenn das Auto gekauft ist"): the picture there was only the livery's field of the paint texture - the
    /// stickers on a white rectangle, nothing of the van. Now the van itself is drawn there, seen straight on from the left
    /// and from behind (no perspective), from its own model (assets/models/govan.gom and the two wheels): windows, black
    /// trims and bumpers, door gaps, lamps, the roof rack and the beacon, the wheels with hubcaps, dark outlines where parts
    /// meet or the van ends, a soft shadow under it. Where the paint shows inside the livery's field the drawing stays
    /// clear (only the rounded parts get a little shade), so the livery picture lying under it shows through - in the same
    /// metres, as the field is the same straight-on view of the body (ba-art/goingonline/go_van.py, uv_paint).
    /// Plain arrays, no Unity: the same code also runs in a test outside the game.</summary>
    internal static class VanPicture
    {
        // the kinds of surface, from the model's material names
        internal const byte KNone = 0, KPaint = 1, KGlass = 2, KBlack = 3, KGrey = 4, KVent = 5, KRed = 6, KAmber = 7,
            KHeadlight = 8, KChrome = 9, KBeacon = 10, KTyre = 11, KHubcap = 12, KHubDark = 13, KCard = 14, KOther = 15;

        internal static byte KindOf(string material)
        {
            switch (material)
            {
                case "van_white": return KPaint;
                case "glass_dark": return KGlass;
                case "trim_black": return KBlack;
                case "plastic_grey": return KGrey;
                case "vent": return KVent;
                case "lamp_red": return KRed;
                case "lamp_amber": case "lamp_amber_r": return KAmber;
                case "screen_headlight": case "lamp_reverse": return KHeadlight;
                case "chrome": case "silver": return KChrome;
                case "beacon": case "beacon_r": return KBeacon;
                case "tyre": return KTyre;
                case "hubcap": return KHubcap;
                case "hub_dark": return KHubDark;
                case "card_white": return KCard;
                default: return KOther;
            }
        }

        /// <summary>Where the wheels sit on the van's body (the game's axes: x right, y up, z front) - the car the van
        /// replaces, the "Honza Mimic" (go_van.py: front axle z = +1.407, rear axle z = -1.294, track 1.504 m, wheel centre
        /// 0.305 m).</summary>
        internal const float FrontAxle = 1.407f, RearAxle = -1.294f, HalfTrack = 0.752f, WheelCentre = 0.305f;

        /// <summary>A drawing: RGBA, row 0 at the bottom; the plane it shows (metres: s to the right, b up) and where the
        /// livery's field lies in it (Mirror: the field's picture runs the other way and must be shown mirrored).</summary>
        internal sealed class Drawing
        {
            public int Side, W, H;
            public float S0, B0, S1, B1;
            public float FS0, FB0, FS1, FB1;
            public bool HasField, Mirror;
            public float G0, G1;   // the van from one end to the other (for the shadow)
            public byte[] Px = Array.Empty<byte>();

            /// <summary>The field in the drawing, 0..1 from the bottom left: x, y, width, height.</summary>
            public (float x, float y, float w, float h) Field()
                => ((FS0 - S0) / (S1 - S0), (FB0 - B0) / (B1 - B0), (FS1 - FS0) / (S1 - S0), (FB1 - FB0) / (B1 - B0));

            public float Aspect => (S1 - S0) / (B1 - B0);
        }

        /// <summary>The plane's across-coordinate of a point: side 0 (seen from the left) s = -z, so the front is on the left
        /// as in the livery's field L; side 1 (seen from behind) s = x, the van's left on the left as in the field B.</summary>
        static float S(int side, float x, float z) => side == 0 ? -z : x;

        /// <summary>Size and frame of a drawing (fast: one pass over the corners), before it is painted.</summary>
        /// <param name="v">corners x, y, z</param>
        /// <param name="field">the livery's field of this view (L for the side, B for the back), or null</param>
        /// <param name="ppm">pixels per metre</param>
        internal static Drawing Plan(float[] v, int side, LiveryPaint.Region? field, float ppm)
        {
            float s0 = float.MaxValue, s1 = float.MinValue, b0 = 0f, b1 = float.MinValue;
            for (int i = 0; i + 2 < v.Length; i += 3)
            {
                float s = S(side, v[i], v[i + 2]), b = v[i + 1];
                if (s < s0) s0 = s;
                if (s > s1) s1 = s;
                if (b < b0) b0 = b;
                if (b > b1) b1 = b;
            }
            if (s0 > s1) { s0 = -1f; s1 = 1f; b1 = 1f; }
            const float m = 0.07f;
            var d = new Drawing { Side = side, S0 = s0 - m, S1 = s1 + m, B0 = b0 - m, B1 = b1 + m, G0 = s0, G1 = s1 };
            if (field != null)
            {
                // the field's own coordinate a: on the side the length (a = -z = s), on the back the width (a = -x = -s)
                if (side == 0) { d.FS0 = field.A0; d.FS1 = field.A1; d.Mirror = field.Flip; }
                else { d.FS0 = -field.A1; d.FS1 = -field.A0; d.Mirror = !field.Flip; }
                d.FB0 = field.B0; d.FB1 = field.B1;
                d.HasField = true;
                d.S0 = Math.Min(d.S0, d.FS0); d.S1 = Math.Max(d.S1, d.FS1);
                d.B0 = Math.Min(d.B0, d.FB0); d.B1 = Math.Max(d.B1, d.FB1);
            }
            d.W = Math.Max(8, (int)Math.Round((d.S1 - d.S0) * ppm));
            d.H = Math.Max(8, (int)Math.Round((d.B1 - d.B0) * ppm));
            return d;
        }

        /// <summary>Paints the drawing: every triangle (t: three corners each, k: its kind) into a depth buffer at twice the
        /// size, then the colours, the outlines and the shadow, averaged down.</summary>
        internal static void Paint(Drawing d, float[] v, int[] t, byte[] k)
        {
            const int SS = 2;
            int side = d.Side, w = d.W * SS, h = d.H * SS;
            var depth = new float[w * h];
            var kind = new byte[w * h];
            var face = new byte[w * h];
            for (int i = 0; i < depth.Length; i++) depth[i] = float.MaxValue;
            float sx = w / (d.S1 - d.S0), sy = h / (d.B1 - d.B0);
            int tc = Math.Min(t.Length / 3, k.Length);
            for (int q = 0; q < tc; q++)
            {
                int i0 = t[q * 3] * 3, i1 = t[q * 3 + 1] * 3, i2 = t[q * 3 + 2] * 3;
                if (i0 + 2 >= v.Length || i1 + 2 >= v.Length || i2 + 2 >= v.Length) continue;
                float x0 = (S(side, v[i0], v[i0 + 2]) - d.S0) * sx, y0 = (v[i0 + 1] - d.B0) * sy;
                float x1 = (S(side, v[i1], v[i1 + 2]) - d.S0) * sx, y1 = (v[i1 + 1] - d.B0) * sy;
                float x2 = (S(side, v[i2], v[i2 + 2]) - d.S0) * sx, y2 = (v[i2 + 1] - d.B0) * sy;
                // depth: the distance from the viewer - seen from the left the smaller x is nearer, from behind the smaller z
                float z0 = side == 0 ? v[i0] : v[i0 + 2], z1 = side == 0 ? v[i1] : v[i1 + 2], z2 = side == 0 ? v[i2] : v[i2 + 2];
                float ax = v[i1] - v[i0], ay = v[i1 + 1] - v[i0 + 1], az = v[i1 + 2] - v[i0 + 2];
                float bx = v[i2] - v[i0], by = v[i2 + 1] - v[i0 + 1], bz = v[i2 + 2] - v[i0 + 2];
                float nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx;
                float nl = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (nl < 1e-12f) continue;
                float f = Math.Abs(side == 0 ? nx : nz) / nl;    // 1: faces the viewer, 0: seen edge-on
                byte fb = (byte)Math.Min(255, (int)(f * 255f + 0.5f));
                float area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0);
                if (Math.Abs(area) < 1e-7f) continue;
                int minX = Math.Max(0, (int)Math.Floor(Math.Min(x0, Math.Min(x1, x2))));
                int maxX = Math.Min(w - 1, (int)Math.Ceiling(Math.Max(x0, Math.Max(x1, x2))));
                int minY = Math.Max(0, (int)Math.Floor(Math.Min(y0, Math.Min(y1, y2))));
                int maxY = Math.Min(h - 1, (int)Math.Ceiling(Math.Max(y0, Math.Max(y1, y2))));
                if (minX > maxX || minY > maxY) continue;
                float inv = 1f / area;
                byte kk = k[q];
                for (int y = minY; y <= maxY; y++)
                {
                    float py = y + 0.5f;
                    int row = y * w;
                    for (int x = minX; x <= maxX; x++)
                    {
                        float px = x + 0.5f;
                        float w0 = ((x1 - px) * (y2 - py) - (x2 - px) * (y1 - py)) * inv;
                        if (w0 < 0f) continue;
                        float w1 = ((x2 - px) * (y0 - py) - (x0 - px) * (y2 - py)) * inv;
                        if (w1 < 0f) continue;
                        float w2 = 1f - w0 - w1;
                        if (w2 < 0f) continue;
                        float z = w0 * z0 + w1 * z1 + w2 * z2;
                        int idx = row + x;
                        if (z >= depth[idx]) continue;
                        depth[idx] = z; kind[idx] = kk; face[idx] = fb;
                    }
                }
            }

            var outPx = new byte[d.W * d.H * 4];
            float fs0 = d.FS0 + 0.5f / sx, fs1 = d.FS1 - 0.5f / sx, fb0 = d.FB0 + 0.5f / sy, fb1 = d.FB1 - 0.5f / sy;
            for (int y = 0; y < d.H; y++)
                for (int x = 0; x < d.W; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int oy = 0; oy < SS; oy++)
                        for (int ox = 0; ox < SS; ox++)
                        {
                            int X = x * SS + ox, Y = y * SS + oy, idx = Y * w + X;
                            float ps = d.S0 + (X + 0.5f) / sx, pb = d.B0 + (Y + 0.5f) / sy;
                            float cr, cg, cb, ca = 1f;
                            if (Edge(kind, depth, idx, X, Y, w, h)) { cr = 24f; cg = 28f; cb = 34f; }
                            else
                            {
                                byte kk = kind[idx];
                                float fc = face[idx] / 255f;
                                if (kk == KNone)
                                {
                                    cr = 228f; cg = 232f; cb = 236f;
                                    if (pb < 0.07f && ps > d.G0 - 0.1f && ps < d.G1 + 0.1f)
                                    {   // a soft shadow on the ground under the van
                                        float sh = 0.30f * Clamp01(1f - (pb + 0.03f) / 0.10f) * Clamp01(Math.Min(ps - d.G0 + 0.1f, d.G1 + 0.1f - ps) / 0.45f);
                                        cr *= 1f - sh; cg *= 1f - sh; cb *= 1f - sh;
                                    }
                                }
                                else if (kk == KPaint)
                                {
                                    if (d.HasField && ps >= fs0 && ps <= fs1 && pb >= fb0 && pb <= fb1)
                                    {   // the livery shows through; rounded parts a little shaded
                                        cr = cg = cb = 0f;
                                        ca = Clamp01((0.92f - fc) * 0.55f);
                                        if (ca > 0.4f) ca = 0.4f;
                                    }
                                    else { float m = 0.8f + 0.2f * fc; cr = 244f * m; cg = 245f * m; cb = 246f * m; }
                                }
                                else
                                {
                                    Colour(kk, pb, out cr, out cg, out cb);
                                    float m = 0.8f + 0.2f * fc;
                                    cr *= m; cg *= m; cb *= m;
                                }
                            }
                            r += cr * ca; g += cg * ca; b += cb * ca; a += ca;
                        }
                    int o = (y * d.W + x) * 4;
                    if (a > 0.0001f)
                    {
                        outPx[o] = (byte)Math.Min(255f, r / a + 0.5f);
                        outPx[o + 1] = (byte)Math.Min(255f, g / a + 0.5f);
                        outPx[o + 2] = (byte)Math.Min(255f, b / a + 0.5f);
                    }
                    outPx[o + 3] = (byte)Math.Min(255f, a / (SS * SS) * 255f + 0.5f);
                }
            d.Px = outPx;
        }

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        /// <summary>Dark parts count as one, so no outline runs between a black bumper and a black tyre.</summary>
        static int Class(byte k) => k == KBlack || k == KGrey || k == KVent || k == KTyre || k == KHubDark ? 3 : k;

        /// <summary>An outline: a neighbour of another kind, or a step in depth (one part before another).</summary>
        static bool Edge(byte[] kind, float[] depth, int idx, int x, int y, int w, int h)
        {
            return (x > 0 && Differs(kind, depth, idx, idx - 1)) || (x < w - 1 && Differs(kind, depth, idx, idx + 1))
                || (y > 0 && Differs(kind, depth, idx, idx - w)) || (y < h - 1 && Differs(kind, depth, idx, idx + w));
        }

        static bool Differs(byte[] kind, float[] depth, int a, int b)
        {
            byte ka = kind[a], kb = kind[b];
            if (Class(ka) != Class(kb)) return true;
            return ka != KNone && kb != KNone && Math.Abs(depth[a] - depth[b]) > 0.06f;
        }

        static void Colour(byte k, float b, out float r, out float g, out float bl)
        {
            switch (k)
            {
                case KGlass:
                    {   // a little lighter towards the top, like glass that mirrors the sky
                        float u = Clamp01((b - 1.10f) / 0.65f);
                        r = 36f + 42f * u; g = 44f + 48f * u; bl = 54f + 54f * u;
                        return;
                    }
                case KBlack: r = 30f; g = 33f; bl = 37f; return;
                case KGrey: r = 45f; g = 49f; bl = 54f; return;
                case KVent: r = 52f; g = 57f; bl = 64f; return;
                case KRed: r = 165f; g = 24f; bl = 30f; return;
                case KAmber: r = 222f; g = 132f; bl = 28f; return;
                case KHeadlight: r = 205f; g = 210f; bl = 216f; return;
                case KChrome: r = 190f; g = 195f; bl = 200f; return;
                case KBeacon: r = 240f; g = 135f; bl = 30f; return;
                case KTyre: r = 26f; g = 27f; bl = 29f; return;
                case KHubcap: r = 192f; g = 196f; bl = 201f; return;
                case KHubDark: r = 40f; g = 42f; bl = 45f; return;
                case KCard: r = 246f; g = 246f; bl = 246f; return;
                default: r = 128f; g = 134f; bl = 142f; return;
            }
        }
    }
}
