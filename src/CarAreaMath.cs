using System;
using System.Collections.Generic;

namespace BigAmbitionsMP
{
    /// <summary>CAR-LIVERY-1: where a livery's decals may go on a car of the game - the largest free part of each side's
    /// surface, the back's, and the hood's in front of the windscreen. Free means: the body faces out that way, no window,
    /// no lamp (the model's emission masks mark the lamp triangles), no wheel, not the very ends, and on the side's plane
    /// (a mirror sticking out or a recess does not count). No Unity in here (plain floats), so a test outside the game reads
    /// the game's own car models with the same code (the scratchpad's livery test).
    /// The car's own space: x to its right, y up, z to its front (Unity's).</summary>
    internal static class CarAreaMath
    {
        public struct V3
        {
            public float X, Y, Z;
            public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
        }

        /// <summary>A triangle in the car's space, its outward face normal; Kind 0 body (paint), 1 glass, 2 lamp, 3 body
        /// without paint (the texture's paint mask: windows painted into the texture, trims, grilles, bumpers).</summary>
        public struct Tri { public V3 A, B, C, N; public int Kind; }

        public struct Box { public V3 Min, Max; public V3 Center => new V3((Min.X + Max.X) * 0.5f, (Min.Y + Max.Y) * 0.5f, (Min.Z + Max.Z) * 0.5f); }

        /// <summary>A decal's box. Pos: the middle of its outer face; Dir: where it projects (0 +x: the left side, 1 -x: the
        /// right side, 2 +z: the back, 3 -y: the hood); W x H the picture (along the car / across the back / across the
        /// hood), Depth how far it reaches in. FrontAt: where the car's front is in the picture (0 left, 2 right, 1 none);
        /// Kind 0 a side, 1 the back, 2 the hood.</summary>
        public sealed class Area
        {
            public V3 Pos;
            public int Dir, FrontAt = 1, Kind;
            public float W, H, Depth;
        }

        public const float Cell = 0.04f;

        /// <summary>Only for the test outside the game: each grid as it was decided (cells: 0 nothing, 1 free, 2 without
        /// paint, 3 blocked, 4 the chosen rectangle; rows from the bottom).</summary>
        public static Action<string, int, int, byte[]>? Debug = null;

        static void Show(string what, Grid g, bool[] free, int u0, int v0, int u1, int v1, bool found)
        {
            if (Debug == null) return;
            var c = new byte[g.Nu * g.Nv];
            for (int k = 0; k < c.Length; k++)
                c[k] = (byte)(float.IsNaN(g.Depth[k]) ? 0 : g.Block[k] ? 3 : free[k] ? 1 : g.Soft[k] ? 2 : 3);
            if (found) for (int j = v0; j <= v1; j++) for (int i = u0; i <= u1; i++) c[j * g.Nu + i] = 4;
            Debug(what, g.Nu, g.Nv, c);
        }

        /// <summary>left, right, back, hood (null where there is no room).</summary>
        public static Area?[] Find(List<Tri> tris, List<Box> wheels)
        {
            var res = new Area?[4];
            if (tris.Count < 50) return res;
            var b = new Box { Min = new V3(float.MaxValue, float.MaxValue, float.MaxValue), Max = new V3(float.MinValue, float.MinValue, float.MinValue) };
            bool any = false;
            foreach (var t in tris)
            {
                if (t.Kind != 0 && t.Kind != 3) continue;
                any = true;
                Grow(ref b, t.A); Grow(ref b, t.B); Grow(ref b, t.C);
            }
            if (!any) return res;
            res[0] = SideArea(tris, b, wheels, -1);
            res[1] = SideArea(tris, b, wheels, 1);
            res[2] = BackArea(tris, b);
            res[3] = HoodArea(tris, b, wheels);
            return res;
        }

        static void Grow(ref Box b, V3 p)
        {
            if (p.X < b.Min.X) b.Min.X = p.X; if (p.Y < b.Min.Y) b.Min.Y = p.Y; if (p.Z < b.Min.Z) b.Min.Z = p.Z;
            if (p.X > b.Max.X) b.Max.X = p.X; if (p.Y > b.Max.Y) b.Max.Y = p.Y; if (p.Z > b.Max.Z) b.Max.Z = p.Z;
        }

        /// <summary>A grid over a plane of the car: for each cell the outermost depth of the body's surface facing out that
        /// way (NaN: none), and whether something forbids the cell.</summary>
        sealed class Grid
        {
            public readonly int Nu, Nv;
            public readonly float U0, V0;
            public readonly float[] Depth;
            public readonly bool[] Block, Soft;   // Soft: body without paint - free again where it is only a thin line
            public Grid(float u0, float u1, float v0, float v1)
            {
                U0 = u0; V0 = v0;
                Nu = Math.Max(1, Math.Min(400, (int)Math.Ceiling((u1 - u0) / Cell)));
                Nv = Math.Max(1, Math.Min(400, (int)Math.Ceiling((v1 - v0) / Cell)));
                Depth = new float[Nu * Nv];
                for (int i = 0; i < Depth.Length; i++) Depth[i] = float.NaN;
                Block = new bool[Nu * Nv];
                Soft = new bool[Nu * Nv];
            }
            public float U(int i) => U0 + (i + 0.5f) * Cell;
            public float V(int j) => V0 + (j + 0.5f) * Cell;
        }

        /// <summary>Fills the cells a triangle covers ((u, v) its corners in the plane, d their depths - larger is further
        /// out). mode 0: surface (keep the outermost depth); 1: the cells are blocked; 2: surface without paint.</summary>
        static void Raster(Grid g, float au, float av, float bu, float bv, float cu, float cv, float da, float db, float dc, int mode)
        {
            float minU = Math.Min(au, Math.Min(bu, cu)), maxU = Math.Max(au, Math.Max(bu, cu));
            float minV = Math.Min(av, Math.Min(bv, cv)), maxV = Math.Max(av, Math.Max(bv, cv));
            int i0 = Math.Max(0, (int)Math.Floor((minU - g.U0) / Cell)), i1 = Math.Min(g.Nu - 1, (int)Math.Floor((maxU - g.U0) / Cell));
            int j0 = Math.Max(0, (int)Math.Floor((minV - g.V0) / Cell)), j1 = Math.Min(g.Nv - 1, (int)Math.Floor((maxV - g.V0) / Cell));
            float den = (bv - cv) * (au - cu) + (cu - bu) * (av - cv);
            if (Math.Abs(den) < 1e-10f) return;
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    float pu = g.U(i), pv = g.V(j);
                    float w0 = ((bv - cv) * (pu - cu) + (cu - bu) * (pv - cv)) / den;
                    float w1 = ((cv - av) * (pu - cu) + (au - cu) * (pv - cv)) / den;
                    float w2 = 1f - w0 - w1;
                    const float e = -0.02f;
                    if (w0 < e || w1 < e || w2 < e) continue;
                    int k = j * g.Nu + i;
                    if (mode == 1) { g.Block[k] = true; continue; }
                    float d = w0 * da + w1 * db + w2 * dc;
                    // the outermost surface decides whether the cell has paint (not an inner wall behind it)
                    if (float.IsNaN(g.Depth[k]) || d > g.Depth[k]) { g.Depth[k] = d; g.Soft[k] = mode == 2; }
                }
        }

        /// <summary>The cells off the side's plane are no part of it (a mirror sticks out, a recess lies back).</summary>
        static void KeepPlane(Grid g, float tolerance)
        {
            var list = new List<float>();
            for (int k = 0; k < g.Depth.Length; k++) if (!float.IsNaN(g.Depth[k]) && !g.Block[k]) list.Add(g.Depth[k]);
            if (list.Count < 10) return;
            list.Sort();
            float top = list[(int)(list.Count * 0.80f)];   // the outer part of the surface: the side's face, not its curving edges
            for (int k = 0; k < g.Depth.Length; k++)
                if (!float.IsNaN(g.Depth[k]) && (g.Depth[k] < top - tolerance || g.Depth[k] > top + 0.06f)) g.Block[k] = true;
        }

        /// <summary>The free cells: surface, not blocked, painted - where the paint is missing only along a thin line (a
        /// trim, a door handle, a gap: up to 4 cells), it counts as free (a closing of the free cells).</summary>
        static bool[] Free(Grid g)
        {
            int n = g.Nu * g.Nv;
            var ok = new bool[n];
            var free = new bool[n];
            for (int k = 0; k < n; k++) { ok[k] = !float.IsNaN(g.Depth[k]) && !g.Block[k]; free[k] = ok[k] && !g.Soft[k]; }
            var closed = Morph(Morph(free, g.Nu, g.Nv, 2, true), g.Nu, g.Nv, 2, false);
            for (int k = 0; k < n; k++) closed[k] &= ok[k];
            return closed;
        }

        /// <summary>A square dilation (grow) or erosion of a mask by r cells; outside the grid counts as not free.</summary>
        static bool[] Morph(bool[] m, int nu, int nv, int r, bool grow)
        {
            var o = new bool[m.Length];
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    bool v = !grow;
                    for (int dj = -r; dj <= r && v != grow; dj++)
                        for (int di = -r; di <= r; di++)
                        {
                            int x = i + di, y = j + dj;
                            bool c = x >= 0 && y >= 0 && x < nu && y < nv && m[y * nu + x];
                            if (grow ? c : !c) { v = grow; break; }
                        }
                    o[j * nu + i] = v;
                }
            return o;
        }

        /// <summary>The largest rectangle of free cells at least minU wide and minV high (row by row, the largest rectangle
        /// under a histogram).</summary>
        static bool Largest(Grid g, int minU, int minV, out int u0, out int v0, out int u1, out int v1, string what = "")
        {
            u0 = v0 = u1 = v1 = 0;
            var free = Free(g);
            var hgt = new int[g.Nu];
            int best = 0;
            var st = new Stack<int>();
            for (int j = 0; j < g.Nv; j++)
            {
                for (int i = 0; i < g.Nu; i++)
                {
                    hgt[i] = free[j * g.Nu + i] ? hgt[i] + 1 : 0;
                }
                st.Clear();
                for (int i = 0; i <= g.Nu; i++)
                {
                    int hc = i == g.Nu ? 0 : hgt[i];
                    while (st.Count > 0 && hgt[st.Peek()] >= hc)
                    {
                        int top = st.Pop(), h = hgt[top];
                        int left = st.Count == 0 ? 0 : st.Peek() + 1, width = i - left;
                        if (h >= minV && width >= minU && h * width > best)
                        {
                            best = h * width; u0 = left; u1 = i - 1; v1 = j; v0 = j - h + 1;
                        }
                    }
                    st.Push(i);
                }
            }
            Show(what, g, free, u0, v0, u1, v1, best > 0);
            return best > 0;
        }

        static void DepthRange(Grid g, int u0, int v0, int u1, int v1, out float outer, out float inner)
        {
            outer = float.MinValue; inner = float.MaxValue;
            for (int j = v0; j <= v1; j++)
                for (int i = u0; i <= u1; i++)
                {
                    float d = g.Depth[j * g.Nu + i];
                    if (float.IsNaN(d)) continue;
                    if (d > outer) outer = d;
                    if (d < inner) inner = d;
                }
            if (outer == float.MinValue) { outer = 0f; inner = 0f; }
        }

        static void BlockBox(Grid g, float u0, float u1, float v0, float v1)
        {
            int i0 = Math.Max(0, (int)Math.Floor((u0 - g.U0) / Cell)), i1 = Math.Min(g.Nu - 1, (int)Math.Floor((u1 - g.U0) / Cell));
            int j0 = Math.Max(0, (int)Math.Floor((v0 - g.V0) / Cell)), j1 = Math.Min(g.Nv - 1, (int)Math.Floor((v1 - g.V0) / Cell));
            for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) g.Block[j * g.Nu + i] = true;
        }

        /// <summary>One cell off every edge: the decal keeps clear of the free part's edges.</summary>
        static void Shrink(ref int u0, ref int v0, ref int u1, ref int v1)
        {
            if (u1 - u0 > 4) { u0++; u1--; }
            if (v1 - v0 > 4) { v0++; v1--; }
        }

        static float Centroid(float a, float b, float c) => (a + b + c) / 3f;

        /// <summary>A side (dir -1 left, +1 right): the plane z (along the car) by y (up), depth = x outwards.</summary>
        static Area? SideArea(List<Tri> tris, Box b, List<Box> wheels, int dir)
        {
            var g = new Grid(b.Min.Z, b.Max.Z, b.Min.Y, b.Max.Y);
            foreach (var t in tris)
            {
                float facing = t.N.X * dir;
                bool body = t.Kind == 0 || t.Kind == 3;
                if (body && facing >= 0.55f) Raster(g, t.A.Z, t.A.Y, t.B.Z, t.B.Y, t.C.Z, t.C.Y, t.A.X * dir, t.B.X * dir, t.C.X * dir, t.Kind == 3 ? 2 : 0);
                else if (!body && facing >= 0.2f) Raster(g, t.A.Z, t.A.Y, t.B.Z, t.B.Y, t.C.Z, t.C.Y, 0f, 0f, 0f, 1);
            }
            foreach (var w in wheels)
            {
                if (w.Center.X * dir <= 0f) continue;
                BlockBox(g, w.Min.Z - 0.05f, w.Max.Z + 0.05f, w.Min.Y - 0.05f, w.Max.Y + 0.07f);
            }
            float len = b.Max.Z - b.Min.Z;
            BlockBox(g, b.Min.Z - 1f, b.Min.Z + 0.07f * len, b.Min.Y - 1f, b.Max.Y + 1f);
            BlockBox(g, b.Max.Z - 0.07f * len, b.Max.Z + 1f, b.Min.Y - 1f, b.Max.Y + 1f);
            KeepPlane(g, 0.12f);
            if (!Largest(g, (int)Math.Ceiling(0.6f / Cell), (int)Math.Ceiling(0.18f / Cell), out int u0, out int v0, out int u1, out int v1, dir < 0 ? "left" : "right")) return null;
            Shrink(ref u0, ref v0, ref u1, ref v1);
            DepthRange(g, u0, v0, u1, v1, out float outer, out float inner);
            float z0 = g.U0 + u0 * Cell, z1 = g.U0 + (u1 + 1) * Cell, y0 = g.V0 + v0 * Cell, y1 = g.V0 + (v1 + 1) * Cell;
            return new Area
            {
                Pos = new V3((outer + 0.05f) * dir, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f),
                Dir = dir < 0 ? 0 : 1, W = z1 - z0, H = Math.Min(y1 - y0, 1.5f),
                Depth = Clamp(outer - inner + 0.14f, 0.12f, 0.6f), FrontAt = dir < 0 ? 0 : 2, Kind = 0,
            };
        }

        /// <summary>The back: the plane x by y, depth = -z outwards (the rear half's faces only).</summary>
        static Area? BackArea(List<Tri> tris, Box b)
        {
            var g = new Grid(b.Min.X, b.Max.X, b.Min.Y, b.Max.Y);
            float mid = (b.Min.Z + b.Max.Z) * 0.5f;
            foreach (var t in tris)
            {
                if (Centroid(t.A.Z, t.B.Z, t.C.Z) >= mid) continue;
                float facing = -t.N.Z;
                bool body = t.Kind == 0 || t.Kind == 3;
                if (body && facing >= 0.55f) Raster(g, t.A.X, t.A.Y, t.B.X, t.B.Y, t.C.X, t.C.Y, -t.A.Z, -t.B.Z, -t.C.Z, t.Kind == 3 ? 2 : 0);
                else if (!body && facing >= 0.2f) Raster(g, t.A.X, t.A.Y, t.B.X, t.B.Y, t.C.X, t.C.Y, 0f, 0f, 0f, 1);
            }
            KeepPlane(g, 0.10f);
            if (!Largest(g, (int)Math.Ceiling(0.4f / Cell), (int)Math.Ceiling(0.15f / Cell), out int u0, out int v0, out int u1, out int v1, "back")) return null;
            Shrink(ref u0, ref v0, ref u1, ref v1);
            DepthRange(g, u0, v0, u1, v1, out float outer, out float inner);
            float x0 = g.U0 + u0 * Cell, x1 = g.U0 + (u1 + 1) * Cell, y0 = g.V0 + v0 * Cell, y1 = g.V0 + (v1 + 1) * Cell;
            return new Area
            {
                Pos = new V3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, -(outer + 0.05f)),
                Dir = 2, W = x1 - x0, H = Math.Min(y1 - y0, 0.9f), Depth = Clamp(outer - inner + 0.14f, 0.12f, 0.6f), FrontAt = 1, Kind = 1,
            };
        }

        /// <summary>The hood: the plane x by z in front of the windscreen, depth = y upwards. It starts as high as the body's
        /// upward faces at the very front and rises at most 0.45 m from there (a sloping hood) - a roof or a cab's roof
        /// is no hood (a box truck or a forward-control van has none).</summary>
        static Area? HoodArea(List<Tri> tris, Box b, List<Box> wheels)
        {
            float centre = (b.Min.Z + b.Max.Z) * 0.5f, front = centre + (b.Max.Z - b.Min.Z) * 0.12f;   // the front part only, not the roof
            foreach (var w in wheels) if (w.Center.Z > centre) front = Math.Min(front, w.Min.Z);
            var nose = new List<float>();
            foreach (var t in tris)
                if ((t.Kind == 0 || t.Kind == 3) && t.N.Y >= 0.6f && Centroid(t.A.Z, t.B.Z, t.C.Z) > b.Max.Z - 0.8f)
                    nose.Add(Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)));
            if (nose.Count < 4) return null;
            nose.Sort();
            float top = nose[(int)(nose.Count * 0.85f)] + 0.45f;   // the hood's front edge (above the bumper's steps)
            var g = new Grid(b.Min.X, b.Max.X, front, b.Max.Z);
            foreach (var t in tris)
            {
                if (Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)) > top) continue;
                float facing = t.N.Y;
                bool body = t.Kind == 0 || t.Kind == 3;
                if (body && facing >= 0.6f) Raster(g, t.A.X, t.A.Z, t.B.X, t.B.Z, t.C.X, t.C.Z, t.A.Y, t.B.Y, t.C.Y, t.Kind == 3 ? 2 : 0);
                else if (!body && facing >= 0.15f) Raster(g, t.A.X, t.A.Z, t.B.X, t.B.Z, t.C.X, t.C.Z, 0f, 0f, 0f, 1);
            }
            if (!Largest(g, (int)Math.Ceiling(0.5f / Cell), (int)Math.Ceiling(0.3f / Cell), out int u0, out int v0, out int u1, out int v1, "hood")) return null;
            Shrink(ref u0, ref v0, ref u1, ref v1);
            DepthRange(g, u0, v0, u1, v1, out float outer, out float inner);
            float x0 = g.U0 + u0 * Cell, x1 = g.U0 + (u1 + 1) * Cell, z0 = g.V0 + v0 * Cell, z1 = g.V0 + (v1 + 1) * Cell;
            return new Area
            {
                Pos = new V3((x0 + x1) * 0.5f, outer + 0.05f, (z0 + z1) * 0.5f),
                Dir = 3, W = x1 - x0, H = z1 - z0, Depth = Clamp(outer - inner + 0.16f, 0.14f, 0.7f), FrontAt = 1, Kind = 2,
            };
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
