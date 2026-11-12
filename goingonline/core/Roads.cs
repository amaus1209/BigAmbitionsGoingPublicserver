using System.Collections.Generic;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GO-VAN-12: the city's driving lanes - the game's traffic waypoints (GleyUrbanAssets.CurrentSceneData: each
    /// waypoint's position and the next ones in driving direction) - and the shortest drive between two points along them
    /// (A*), for the leased vans' points on the city map. A snapshot once per city scene; main thread.</summary>
    internal static class Roads
    {
        static Vector3[]? _pos;
        static int[][]? _next;
        static object? _of;
        static bool _warned;

        static float _retryAt = -100f;

        static bool Ensure()
        {
            // GO-VAN-12c (user 2026-11-10: "Spiel stockt" - the Player.log: a 125 ms frame every 0.25 s): the game's
            // CurrentSceneData.GetSceneInstance() runs Object.FindObjectsOfType over the whole city - asked once per scene
            // now, not on every call; without lanes it is asked again only every 30 s
            if (_pos != null && _of is UnityEngine.Object live && live != null) return true;
            if (Time.unscaledTime < _retryAt) return false;
            _retryAt = Time.unscaledTime + 30f;
            try
            {
                var sd = GleyUrbanAssets.CurrentSceneData.GetSceneInstance();
                if (sd == null) return false;
                if (ReferenceEquals(sd, _of) && _pos != null) return true;
                var wps = sd.allWaypoints;
                if (wps == null || wps.Length == 0) return false;
                int n = wps.Length;
                var pos = new Vector3[n];
                var next = new int[n][];
                for (int i = 0; i < n; i++)
                {
                    var w = wps[i];
                    if (w == null) { next[i] = System.Array.Empty<int>(); continue; }
                    pos[i] = w.position;
                    next[i] = w.neighbors != null ? w.neighbors.ToArray() : System.Array.Empty<int>();
                }
                _pos = pos; _next = next; _of = sd;
                Go.Log($"van pins: the city's lanes ({n} waypoints) for the leased vans' drives (GO-VAN-12).");
                return true;
            }
            catch (System.Exception ex)
            {
                if (!_warned) { _warned = true; Go.Warn("van pins: no traffic lanes (" + ex.Message + ") - the pins go straight (GO-VAN-12)."); }
                return false;
            }
        }

        static int Nearest(Vector3 p)
        {
            int best = -1;
            float bd = float.MaxValue;
            var pos = _pos!;
            for (int i = 0; i < pos.Length; i++)
            {
                float dx = pos[i].x - p.x, dz = pos[i].z - p.z, d = dx * dx + dz * dz;
                if (d < bd && _next![i].Length > 0) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>GO-VAN-12b: the lane point nearest to p (a point in the street by a building), null without lanes.</summary>
        internal static Vector3? Snap(Vector3 p)
        {
            if (!Ensure()) return null;
            int i = Nearest(p);
            return i >= 0 ? _pos![i] : (Vector3?)null;
        }

        /// <summary>The drive from a to b along the lanes (both ends included); a straight line without lanes or a way.</summary>
        internal static List<Vector3> Route(Vector3 a, Vector3 b)
        {
            var path = new List<Vector3> { a };
            if (Ensure())
            {
                int s = Nearest(a), g = Nearest(b);
                if (s >= 0 && g >= 0 && s != g)
                {
                    var way = AStar(s, g);
                    if (way != null) foreach (int i in way) path.Add(_pos![i]);
                }
            }
            path.Add(b);
            return path;
        }

        static List<int>? AStar(int start, int goal)
        {
            var pos = _pos!; var next = _next!;
            int n = pos.Length;
            var gs = new float[n];
            var from = new int[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) { gs[i] = float.MaxValue; from[i] = -1; }
            var heap = new List<(float f, int i)>();
            gs[start] = 0f;
            Push(heap, (Vector3.Distance(pos[start], pos[goal]), start));
            int guard = 0;
            while (heap.Count > 0 && guard++ < 400000)
            {
                var (_, cur) = Pop(heap);
                if (done[cur]) continue;
                if (cur == goal) break;
                done[cur] = true;
                foreach (int nb in next[cur])
                {
                    if (nb < 0 || nb >= n || done[nb]) continue;
                    float ng = gs[cur] + Vector3.Distance(pos[cur], pos[nb]);
                    if (ng >= gs[nb]) continue;
                    gs[nb] = ng; from[nb] = cur;
                    Push(heap, (ng + Vector3.Distance(pos[nb], pos[goal]), nb));
                }
            }
            if (from[goal] < 0) return null;
            var way = new List<int>();
            for (int c = goal; c >= 0 && way.Count < n; c = from[c]) { way.Add(c); if (c == start) break; }
            way.Reverse();
            return way;
        }

        static void Push(List<(float f, int i)> h, (float f, int i) x)
        {
            h.Add(x);
            int c = h.Count - 1;
            while (c > 0)
            {
                int p = (c - 1) / 2;
                if (h[p].f <= h[c].f) break;
                (h[p], h[c]) = (h[c], h[p]);
                c = p;
            }
        }

        static (float f, int i) Pop(List<(float f, int i)> h)
        {
            var top = h[0];
            int last = h.Count - 1;
            h[0] = h[last];
            h.RemoveAt(last);
            int c = 0;
            while (true)
            {
                int l = c * 2 + 1, r = l + 1, m = c;
                if (l < h.Count && h[l].f < h[m].f) m = l;
                if (r < h.Count && h[r].f < h[m].f) m = r;
                if (m == c) break;
                (h[m], h[c]) = (h[c], h[m]);
                c = m;
            }
            return top;
        }
    }
}
