using System.Collections.Generic;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>NAV-LANES-1 (user 2026-11-01: "in the harbour the route drives to the end before it turns, though it could
    /// turn on the way; there are 3 or 4 lanes and it wants to turn from the middle one"): two gaps in the traffic graph
    /// the car route runs on (the game's waypoints, MPNavigationModule's road graph):
    ///   * lane changes: the graph links only some lanes to their neighbours (Gley's otherLanes) - of the straight pieces
    ///     with a lane of the same way beside them, a third have no link (the harbour road Road_3: lane 3 none at all).
    ///     A route on such a lane could not change lanes and went on to the next junction its own lane turns at, or to the
    ///     end of the road. Every straight lane waypoint now gets a link, on each side the graph left open, to the lane of
    ///     the same way beside it (2-4.5 m aside, 6-14 m ahead: a soft change the self-driving's look-ahead can follow).
    ///     Tried on the game's graph: 236 links on 46 lanes; of 230 random routes from those lanes 18 got shorter (one by
    ///     3.4 km), none longer by more than 13 m.
    ///   * turns: where a junction can be entered from an inner lane (its "-Out" point, into a connector turning more than
    ///     60 degrees) while another lane of the same way lies on the turn's side, that entry costs 15 m extra - the route
    ///     moves to the outer lane first and turns from there; where only the inner lane turns, it still serves. (40 m made
    ///     detours of up to 93 m in the test.)
    ///   * keep right (user 2026-11-01: "the self-driving has to know how many lanes the road has, then it knows how to
    ///     turn and drive"): a metre on a lane with another lane of the same way to its right costs 2 % more - on a road of
    ///     several lanes the route keeps to the right one and moves over only for a left turn ahead.
    /// The waypoints are fixed game data (the streets do not change), so all of it is worked out once per scene, on the
    /// main thread, with the graph snapshot.</summary>
    internal static class NavLanes
    {
        private const float Cell = 10f;
        internal const float InnerTurnCost = 15f;
        internal const float KeepRightFactor = 1.02f;

        private static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        private static Dictionary<long, List<int>> Grid(Vector3[] pos)
        {
            var g = new Dictionary<long, List<int>>();
            for (int i = 0; i < pos.Length; i++)
            {
                long k = Key(Mathf.FloorToInt(pos[i].x / Cell), Mathf.FloorToInt(pos[i].z / Cell));
                if (!g.TryGetValue(k, out var l)) g[k] = l = new List<int>();
                l.Add(i);
            }
            return g;
        }

        /// <summary>"Road_3-Lane_2-Waypoint_6" -> "Road_3-Lane_2".</summary>
        private static string LaneOf(string name)
        {
            int cut = name.LastIndexOf('-');
            return cut > 0 ? name.Substring(0, cut) : name;
        }

        private static bool Flat(Vector3 from, Vector3 to, out Vector3 dir)
        {
            dir = to - from; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return false;
            dir.Normalize();
            return true;
        }

        /// <summary>The way a waypoint's lane runs at it: from its previous waypoint, else towards its next.</summary>
        private static Vector3[] InHeadings(Vector3[] pos, int[][] next, int[][] prev, out bool[] has)
        {
            int n = pos.Length;
            var h = new Vector3[n];
            has = new bool[n];
            for (int i = 0; i < n; i++)
            {
                int p = prev[i].Length > 0 ? prev[i][0] : -1;
                if (p >= 0 && p < n && Flat(pos[p], pos[i], out var d)) { h[i] = d; has[i] = true; continue; }
                int q = next[i].Length > 0 ? next[i][0] : -1;
                if (q >= 0 && q < n && Flat(pos[i], pos[q], out d)) { h[i] = d; has[i] = true; }
            }
            return h;
        }

        /// <summary>Right of the way: + (Unity: x right, z ahead, y up), left: -.</summary>
        private static float Lateral(Vector3 v, Vector3 h) => v.x * h.z - v.z * h.x;
        private static float Along(Vector3 v, Vector3 h) => v.x * h.x + v.z * h.z;

        /// <summary>The graph's lane links plus one per open side of every straight lane waypoint.</summary>
        internal static int[][] AddLaneLinks(Vector3[] pos, int[][] next, int[][] lanes, int[][] prev, string[] names, out int added)
        {
            added = 0;
            int n = pos.Length;
            var head = InHeadings(pos, next, prev, out var has);
            var lane = new string[n];
            var straight = new bool[n];
            for (int i = 0; i < n; i++)
            {
                lane[i] = LaneOf(names[i]);
                // a lane waypoint (not a junction's connector) whose lane runs on straight from it
                if (!has[i] || next[i].Length != 1 || names[i].IndexOf("-Waypoint_", System.StringComparison.Ordinal) < 0) continue;
                int q = next[i][0];
                straight[i] = q >= 0 && q < n && has[q] && Vector3.Dot(head[i], head[q]) > 0.996f;
            }
            var grid = Grid(pos);
            var result = new int[n][];
            for (int i = 0; i < n; i++)
            {
                result[i] = lanes[i];
                if (!straight[i]) continue;
                Vector3 h = head[i];
                bool left = false, right = false;
                foreach (int j in lanes[i])
                {
                    if (j < 0 || j >= n) continue;
                    float lat = Lateral(pos[j] - pos[i], h);
                    if (lat > 0.5f) right = true; else if (lat < -0.5f) left = true;
                }
                if (left && right) continue;
                int bestL = -1, bestR = -1;
                float scoreL = float.MaxValue, scoreR = float.MaxValue;
                int cx = Mathf.FloorToInt(pos[i].x / Cell), cz = Mathf.FloorToInt(pos[i].z / Cell);
                for (int gx = cx - 2; gx <= cx + 2; gx++)
                    for (int gz = cz - 2; gz <= cz + 2; gz++)
                    {
                        if (!grid.TryGetValue(Key(gx, gz), out var cell)) continue;
                        foreach (int k in cell)
                        {
                            if (k == i || !straight[k] || lane[k] == lane[i]) continue;
                            if (Vector3.Dot(head[k], h) < 0.98f || Mathf.Abs(pos[k].y - pos[i].y) > 1f) continue;
                            Vector3 v = pos[k] - pos[i];
                            float lat = Lateral(v, h), along = Along(v, h), a = Mathf.Abs(lat);
                            if (a < 2f || a > 4.5f || along < 6f || along > 14f) continue;
                            float score = Mathf.Abs(along - 10f) + (a - 3.2f) * (a - 3.2f);
                            if (lat > 0f) { if (!right && score < scoreR) { scoreR = score; bestR = k; } }
                            else if (!left && score < scoreL) { scoreL = score; bestL = k; }
                        }
                    }
                if (bestL < 0 && bestR < 0) continue;
                var list = new List<int>(lanes[i]);
                if (bestL >= 0) { list.Add(bestL); added++; }
                if (bestR >= 0) { list.Add(bestR); added++; }
                result[i] = list.ToArray();
            }
            return result;
        }

        /// <summary>Per next[] link: InnerTurnCost for a turn into a junction from a lane with another lane on the turn's side.</summary>
        internal static float[][] TurnPenalties(Vector3[] pos, int[][] next, int[][] prev, string[] names, out int count)
        {
            count = 0;
            int n = pos.Length;
            var head = InHeadings(pos, next, prev, out var has);
            var lane = new string[n];
            for (int i = 0; i < n; i++) lane[i] = LaneOf(names[i]);
            var grid = Grid(pos);
            var pen = new float[n][];
            for (int u = 0; u < n; u++)
            {
                var nu = next[u];
                pen[u] = nu.Length == 0 ? System.Array.Empty<float>() : new float[nu.Length];
                // only where a lane enters a junction: its last point ("-Out") into a connector
                if (nu.Length == 0 || !has[u] || !names[u].EndsWith("-Out", System.StringComparison.Ordinal)) continue;
                Vector3 hin = head[u];
                for (int k = 0; k < nu.Length; k++)
                {
                    int v = nu[k];
                    if (v < 0 || v >= n || names[v].IndexOf("Connect", System.StringComparison.Ordinal) < 0) continue;   // not into a junction
                    // the way out of the junction: along the connector ~30 m (its first branch)
                    int a = u, b = v;
                    float run = 0f;
                    for (int step = 0; step < 16 && run < 30f; step++)
                    {
                        run += Vector3.Distance(pos[a], pos[b]);
                        if (next[b].Length == 0) break;
                        int c = next[b][0];
                        if (c < 0 || c >= n) break;
                        a = b; b = c;
                    }
                    if (!Flat(pos[a], pos[b], out var hout)) continue;
                    if (Vector3.Dot(hin, hout) > 0.5f) continue;   // straight on, or a bend under 60 degrees
                    bool rightTurn = hin.z * hout.x - hin.x * hout.z > 0f;
                    if (!LaneBeside(u, rightTurn, pos, head, has, lane, names, grid)) continue;   // already the outer lane
                    pen[u][k] = InnerTurnCost;
                    count++;
                }
            }
            return pen;
        }

        /// <summary>Per waypoint, the cost factor of a metre driven from it: KeepRightFactor on a lane waypoint with another
        /// lane of the same way to its right, else 1.</summary>
        internal static float[] KeepRight(Vector3[] pos, int[][] next, int[][] prev, string[] names, out int count)
        {
            count = 0;
            int n = pos.Length;
            var head = InHeadings(pos, next, prev, out var has);
            var lane = new string[n];
            for (int i = 0; i < n; i++) lane[i] = LaneOf(names[i]);
            var grid = Grid(pos);
            var f = new float[n];
            for (int u = 0; u < n; u++)
            {
                f[u] = 1f;
                if (!has[u] || names[u].IndexOf("-Waypoint_", System.StringComparison.Ordinal) < 0) continue;
                if (!LaneBeside(u, true, pos, head, has, lane, names, grid)) continue;
                f[u] = KeepRightFactor;
                count++;
            }
            return f;
        }

        /// <summary>Another lane of the same way next to waypoint u on the right (or left) - 2 to 8 m aside, within 10 m.</summary>
        private static bool LaneBeside(int u, bool right, Vector3[] pos, Vector3[] head, bool[] has, string[] lane, string[] names, Dictionary<long, List<int>> grid)
        {
            Vector3 h = head[u];
            int cx = Mathf.FloorToInt(pos[u].x / Cell), cz = Mathf.FloorToInt(pos[u].z / Cell);
            for (int gx = cx - 1; gx <= cx + 1; gx++)
                for (int gz = cz - 1; gz <= cz + 1; gz++)
                {
                    if (!grid.TryGetValue(Key(gx, gz), out var cell)) continue;
                    foreach (int k in cell)
                    {
                        if (k == u || !has[k] || lane[k] == lane[u] || names[k].IndexOf("Connect", System.StringComparison.Ordinal) >= 0) continue;   // lanes, not a junction's connectors
                        if (Vector3.Dot(head[k], h) < 0.95f || Mathf.Abs(pos[k].y - pos[u].y) > 1f) continue;
                        Vector3 v = pos[k] - pos[u];
                        float lat = Lateral(v, h);
                        if (Mathf.Abs(Along(v, h)) > 10f || Mathf.Abs(lat) < 2f || Mathf.Abs(lat) > 8f) continue;
                        if (right ? lat > 0f : lat < 0f) return true;
                    }
                }
            return false;
        }
    }
}
