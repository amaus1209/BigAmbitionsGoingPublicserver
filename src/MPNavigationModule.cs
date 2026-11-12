using System.Threading.Tasks;
using Helpers;
using Newtonsoft.Json;
using TMPro;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BigAmbitionsMP
{
    /// <summary>
    /// MODULE-NAVIGATION-1 (Going Public Server 1.0.12, module 6 of the server's own features; replaces the VoogleRoute mod
    /// and its two libraries with own code): a destination and the way there.
    ///   Destination - from the Navigation window (Shift+F11: search every business by name or street, your own places,
    ///     your bookmarks) or on the city map: a left click (press and release without moving - holding and dragging
    ///     still moves the map, user ruling 2026-10-30) or a middle click on a building or a spot.
    ///   Route - on foot the game's own NavMesh path (NavMesh.CalculatePath with the player's query filter); in a car an
    ///     A* search over the game's traffic waypoint graph (GleyTrafficSystem: allWaypoints, their neighbours in driving
    ///     direction and the lanes beside them), worked out on a background thread from a snapshot of the graph. Worked
    ///     out again when the mode changes or the player leaves the route.
    ///   Shown as a line drawn over the picture (screen-space, so it works with the game's HDRP and on the city map alike),
    ///     a pin with the distance at the destination, a line at the top of the screen and a marker on the city map
    ///     (CityMap.AddPoi). "Walk there" hands the way to the game's own click-to-walk (PlayerController.SetNewDestination).
    /// Only this machine's view and its own character's walk - nothing is sent. Not (yet) here from VoogleRoute: auto-drive,
    /// fast travel, subway legs, the way out of buildings.
    /// </summary>
    internal static class Navigator
    {
        internal static bool HasDest;
        internal static Vector3 Dest;
        internal static string DestName = "";
        /// <summary>NAV-DEVICE-4: counts the destinations set - the navigation device starts its bar of the way done over.</summary>
        internal static int DestSerial;
        /// <summary>AUTO-DRIVE-1 (1.0.16): the destination is a car park's entrance - the self-driving car drives in.</summary>
        internal static bool DestIsGarage;
        /// <summary>AUTO-DRIVE-1 (1.0.16): the destination follows this player (a friend): it moves with him, and arriving
        /// near him does not end the route.</summary>
        internal static string FollowPid = "";
        private static bool _destMoved;
        private static bool _localLine = true;
        /// <summary>SERVER-SETTINGS-1: on a server the admin decides the route line (AMP "NavRouteLine"), otherwise the player.</summary>
        internal static bool ShowLine => ServerRules.FromServer ? ServerRules.Flag("NavRouteLine") : _localLine;
        internal static void ToggleLine() { if (!ServerRules.FromServer) _localLine = !_localLine; }
        /// <summary>NAV-PHONE-1 (user 2026-11-02: "a switch in AMP: the sat-nav in the phone on/off"): while a destination is
        /// set the navigation device fills the BizPhone's screen (NAV-PHONE-2, NavDevice.Place); off (AMP "NavPhone") = it
        /// stands on top of the phone, the phone's apps in sight. Single player: on.</summary>
        internal static bool InPhone => ServerRules.Flag("NavPhone");
        internal static readonly List<Vector3> Route = new List<Vector3>();
        internal static bool RouteCar;
        internal static int Passed;   // index of the route point the player has reached

        private static float _nextCalcAt;
        private static Task<List<Vector3>?>? _job;
        private static bool _jobCar, _wasCar, _wasInside, _noRoadNoted, _loaded;
        private static Vector3[]? _pos;
        private static int[][]? _next, _lanes;
        private static float[][]? _pen;   // NAV-LANES-1: extra cost per next[] link (a turn from an inner lane)
        private static float[]? _keep;    // NAV-LANES-1: cost factor per metre from a waypoint (keep right)
        private static object? _graphOf;
        private static GameObject? _anchor;
        private static Component? _poi;
        private static Sprite? _icon;
        private static readonly Color PinColour = new Color(0.10f, 0.62f, 0.95f, 1f);

        internal sealed class Bookmark { public string n = ""; public float x, y, z; }
        internal static readonly List<Bookmark> Bookmarks = new List<Bookmark>();

        internal static bool InCar()
        {
            try { var pc = PlayerHelper.PlayerController; return pc != null && !pc.IsOnNavmesh(); } catch { return false; }
        }

        internal static bool Inside() => BuildingManager.IsInsideBuilding || Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking;

        internal static Vector3 Me() { try { return PlayerHelper.GetPosition(); } catch { return Vector3.zero; } }

        internal static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        internal static string Distance(float m) => m < 1000f ? $"{Mathf.RoundToInt(m / 10f) * 10} m" : $"{m / 1000f:0.0} km";

        internal static void Set(Vector3 p, string name)
        {
            HasDest = true; Dest = p; DestName = name ?? ""; DestSerial++;
            DestIsGarage = false; FollowPid = "";
            Route.Clear(); Passed = 0; _nextCalcAt = 0f; _noRoadNoted = false; _job = null;
            MapMarker();
            Plugin.Logger.LogInfo($"[Nav] destination '{DestName}' at ({p.x:0}, {p.z:0}).");
        }

        internal static void Clear()
        {
            HasDest = false; Route.Clear(); Passed = 0; _job = null;
            DestIsGarage = false; FollowPid = "";
            DropMarker();
        }

        /// <summary>Main thread, every frame in the world (MPCanvasUI.Update).</summary>
        internal static void Tick()
        {
            if (!_loaded) Load();
            if (!HasDest) return;
            if (!ServerRules.Module("ModuleNavigation")) { Clear(); return; }
            if (_job != null && _job.IsCompleted)
            {
                List<Vector3>? r = null;
                try { if (_job.Status == TaskStatus.RanToCompletion) r = _job.Result; } catch { }
                bool car = _jobCar;
                _job = null;
                if (r != null && r.Count >= 2) SetRoute(r, car);
                else
                {
                    SetRoute(new List<Vector3> { Me(), Dest }, car);
                    if (car && !_noRoadNoted) { _noRoadNoted = true; Notice(Loc.T("gps_nav_no_route", "No road route found - showing the direct line.")); }
                }
            }
            if (Inside()) { _wasInside = true; return; }
            if (_wasInside) { _wasInside = false; _nextCalcAt = 0f; }
            Vector3 me = Me();
            bool inCar = InCar();
            if (FollowPid.Length > 0)
            {
                // AUTO-DRIVE-1: the destination is wherever the friend is now
                if (!PlayerPos(FollowPid, out var fp))
                {
                    Notice(Loc.T("gps_nav_follow_gone", "{0} is not around any more - following ended.", DestName));
                    Clear();
                    return;
                }
                if (Flat(fp, Dest) > 12f) { Dest = fp; _destMoved = true; if (_anchor != null) _anchor.transform.position = fp; }
            }
            else if (Flat(me, Dest) < (inCar ? 25f : 8f))
            {
                Notice(Loc.T("gps_nav_arrived", "You have arrived: {0}.", DestName));
                Plugin.Logger.LogInfo($"[Nav] arrived at '{DestName}'.");
                Clear();
                return;
            }
            if (inCar != _wasCar) { _wasCar = inCar; _nextCalcAt = 0f; }
            UpdatePassed(me);
            bool need = Route.Count < 2 || RouteCar != inCar || OffRoute(me) > (inCar ? 30f : 12f) || _destMoved;
            if (need && _job == null && Time.unscaledTime >= _nextCalcAt)
            {
                _destMoved = false;
                _nextCalcAt = Time.unscaledTime + 3f;
                Calc(me, inCar);
            }
        }

        private static void SetRoute(List<Vector3> r, bool car)
        {
            Route.Clear();
            Vector3 last = new Vector3(float.MaxValue, 0f, 0f);
            for (int i = 0; i < r.Count; i++)
            {
                if (i > 0 && i < r.Count - 1 && (r[i] - last).sqrMagnitude < 16f) continue;   // keep every ~4 m at most
                Route.Add(r[i]); last = r[i];
            }
            RouteCar = car; Passed = 0;
        }

        // The traffic lanes in a grid: the navigation device's streets (RoadSegmentsNear) and the self-driving's speed limits
        // (LimitAt). NAV-ROAD-2 (user 2026-11-01: "in the car blue, on foot orange - in the map [M] too"): a route is coloured
        // by how it goes, a drive in blue, a walk in orange - NAV-ROAD-1's split of a walk into footpath (blue) and road
        // (orange) pieces is gone with it.
        private const float RoadHalfWidth = 1.9f, RoadCell = 8f;
        private static Dictionary<long, List<int>>? _laneGrid;
        private static Vector3[]? _segA, _segB;
        private static float[]? _segLimit, _limit;
        private static object? _laneGridOf;

        private static long CellKey(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        /// <summary>Main thread: every lane piece (waypoint -> next waypoint) in an 8 m grid, widened by the road half width.</summary>
        private static bool EnsureLaneGrid()
        {
            EnsureGraph();
            if (_pos == null || _next == null) return false;
            if (_laneGrid != null && ReferenceEquals(_laneGridOf, _graphOf)) return true;
            var a = new List<Vector3>(); var b = new List<Vector3>(); var lim = new List<float>();
            var grid = new Dictionary<long, List<int>>();
            for (int i = 0; i < _pos.Length; i++)
                foreach (int j in _next[i])
                {
                    if (j < 0 || j >= _pos.Length) continue;
                    int s = a.Count;
                    a.Add(_pos[i]); b.Add(_pos[j]); lim.Add(_limit != null && i < _limit.Length ? _limit[i] : 0f);
                    int x0 = Mathf.FloorToInt((Mathf.Min(_pos[i].x, _pos[j].x) - RoadHalfWidth) / RoadCell), x1 = Mathf.FloorToInt((Mathf.Max(_pos[i].x, _pos[j].x) + RoadHalfWidth) / RoadCell);
                    int z0 = Mathf.FloorToInt((Mathf.Min(_pos[i].z, _pos[j].z) - RoadHalfWidth) / RoadCell), z1 = Mathf.FloorToInt((Mathf.Max(_pos[i].z, _pos[j].z) + RoadHalfWidth) / RoadCell);
                    if ((x1 - x0) * (z1 - z0) > 400) continue;   // a broken link across the map
                    for (int cx = x0; cx <= x1; cx++)
                        for (int cz = z0; cz <= z1; cz++)
                        {
                            long k = CellKey(cx, cz);
                            if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                            l.Add(s);
                        }
                }
            _segA = a.ToArray(); _segB = b.ToArray(); _segLimit = lim.ToArray(); _laneGrid = grid; _laneGridOf = _graphOf;
            Plugin.Logger.LogInfo($"[Nav] road grid: {a.Count} lane piece(s) in {grid.Count} cell(s).");
            return true;
        }

        private static int[]? _segStamp;
        private static int _stampNo;

        /// <summary>NAV-DEVICE-1: every lane piece within radius of a point, each once (indexes for SegA / SegB).</summary>
        internal static void RoadSegmentsNear(Vector3 c, float radius, List<int> into)
        {
            into.Clear();
            if (!EnsureLaneGrid() || _laneGrid == null || _segA == null) return;
            if (_segStamp == null || _segStamp.Length != _segA.Length) _segStamp = new int[_segA.Length];
            _stampNo++;
            int x0 = Mathf.FloorToInt((c.x - radius) / RoadCell), x1 = Mathf.FloorToInt((c.x + radius) / RoadCell);
            int z0 = Mathf.FloorToInt((c.z - radius) / RoadCell), z1 = Mathf.FloorToInt((c.z + radius) / RoadCell);
            for (int cx = x0; cx <= x1; cx++)
                for (int cz = z0; cz <= z1; cz++)
                {
                    if (!_laneGrid.TryGetValue(CellKey(cx, cz), out var list)) continue;
                    foreach (int s in list)
                    {
                        if (_segStamp[s] == _stampNo) continue;
                        _segStamp[s] = _stampNo;
                        into.Add(s);
                    }
                }
        }

        internal static Vector3 SegA(int s) => _segA![s];
        internal static Vector3 SegB(int s) => _segB![s];

        /// <summary>AUTO-DRIVE-1: where another player is right now - his car while he drives it, else his character.</summary>
        internal static bool PlayerPos(string pid, out Vector3 p)
        {
            p = default;
            if (VehicleManager.DrivenCarPos(pid, out p)) return true;
            foreach (var kv in RemotePlayerManager.Spawned())
                if (kv.Key == pid && kv.Value != null && kv.Value.activeInHierarchy) { p = kv.Value.transform.position; return true; }
            return false;
        }

        /// <summary>AUTO-DRIVE-1 (1.0.16): the speed limit of the lane at <paramref name="p"/> in km/h (the traffic system's
        /// Waypoint.maxSpeed); 0 = no lane there or none set.</summary>
        internal static float LimitAt(Vector3 p)
        {
            if (_laneGrid == null || _segA == null || _segB == null || _segLimit == null) return 0f;
            if (!_laneGrid.TryGetValue(CellKey(Mathf.FloorToInt(p.x / RoadCell), Mathf.FloorToInt(p.z / RoadCell)), out var list)) return 0f;
            float best = 3f, lim = 0f;
            foreach (int s in list)
            {
                float d = SegDist(p, _segA[s], _segB[s]);
                if (d < best) { best = d; lim = _segLimit[s]; }
            }
            return lim;
        }

        /// <summary>NAV-TURN-1 (1.0.16, user: "turn hints in the navigation"): the next turn on the route ahead - the first
        /// point where the way bends by more than 50 degrees within 20 m; metres to it and its side (-1 left, 1 right).
        /// False = straight on for the next 300 m.</summary>
        internal static bool NextTurn(Vector3 me, out float dist, out int side)
        {
            dist = 0f; side = 0;
            if (Route.Count < 3 || Passed + 1 >= Route.Count) return false;
            float acc = Flat(me, Route[Passed + 1]);
            for (int i = Passed + 1; i < Route.Count - 1 && acc < 300f; i++)
            {
                float turn = 0f, span = 0f;
                for (int j = i; j < Route.Count - 1 && span < 20f; j++)
                {
                    Vector3 x = Route[j] - Route[j - 1], y = Route[j + 1] - Route[j];
                    x.y = y.y = 0f;
                    if (x.sqrMagnitude > 0.01f && y.sqrMagnitude > 0.01f) turn += Vector3.SignedAngle(x, y, Vector3.up);
                    span += Flat(Route[j], Route[j + 1]);
                }
                if (Mathf.Abs(turn) > 50f) { dist = acc; side = turn > 0f ? 1 : -1; return true; }
                acc += Flat(Route[i], Route[i + 1]);
            }
            return false;
        }

        /// <summary>NAV-TURN-1: "Turn right in 80 m" / "Turn left now" for the line at the top; "" = straight on.</summary>
        internal static string TurnHint(Vector3 me)
        {
            if (!NextTurn(me, out float d, out int side)) return "";
            if (d < (RouteCar ? 18f : 6f))
                return side > 0 ? Loc.T("gps_nav_turn_right_now", "Turn right now") : Loc.T("gps_nav_turn_left_now", "Turn left now");
            string m = Distance(Mathf.Max(10f, Mathf.Round(d / 10f) * 10f));
            return side > 0 ? Loc.T("gps_nav_turn_right_in", "Turn right in {0}", m) : Loc.T("gps_nav_turn_left_in", "Turn left in {0}", m);
        }

        private static void UpdatePassed(Vector3 me)
        {
            if (Route.Count < 2) return;
            int best = Passed; float bd = float.MaxValue;
            for (int i = Passed; i < Math.Min(Route.Count - 1, Passed + 40); i++)
            {
                float d = SegDist(me, Route[i], Route[i + 1]);
                if (d < bd) { bd = d; best = i; }
            }
            Passed = best;
        }

        private static float OffRoute(Vector3 me)
        {
            if (Route.Count < 2) return float.MaxValue;
            float bd = float.MaxValue;
            for (int i = Passed; i < Math.Min(Route.Count - 1, Passed + 40); i++) bd = Mathf.Min(bd, SegDist(me, Route[i], Route[i + 1]));
            return bd;
        }

        internal static float SegDistPublic(Vector3 p, Vector3 a, Vector3 b) => SegDist(p, a, b);   // AUTO-DRIVE-1
        internal static void EnsureRoadData() => EnsureLaneGrid();                                    // AUTO-DRIVE-1: speed limits

        private static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            p.y = a.y = b.y = 0f;
            Vector3 ab = b - a; float l = ab.sqrMagnitude;
            float t = l > 0.0001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        private static void Calc(Vector3 me, bool car)
        {
            if (!car)
            {
                try
                {
                    var filter = PlayerController.navMeshQueryFilter;
                    Vector3 start = me, end = Dest;
                    if (NavMesh.SamplePosition(me, out var h1, 5f, filter)) start = h1.position;
                    if (NavMesh.SamplePosition(Dest, out var h2, 30f, filter)) end = h2.position;
                    var path = new NavMeshPath();
                    if (NavMesh.CalculatePath(start, end, filter, path) && path.status != NavMeshPathStatus.PathInvalid && path.corners.Length >= 2)
                    {
                        var r = new List<Vector3>(path.corners);
                        if (Flat(end, Dest) > 1f) r.Add(Dest);
                        SetRoute(r, false);
                        return;
                    }
                }
                catch (Exception ex) { Plugin.Logger.LogWarning($"[Nav] foot route: {ex.Message}"); }
                SetRoute(new List<Vector3> { me, Dest }, false);
                return;
            }
            EnsureGraph();
            if (_pos == null || _next == null || _lanes == null) { SetRoute(new List<Vector3> { me, Dest }, true); return; }
            Vector3 fwd = Vector3.forward;
            try { var v = InstanceBehavior<GameManager>.Instance.selectedVehicle; if (v != null) fwd = v.transform.forward; } catch { }
            var pos = _pos; var next = _next; var lanes = _lanes; var pen = _pen; var keep = _keep; var dest = Dest;
            _jobCar = true;
            _job = Task.Run(() => RoadRoute(pos, next, lanes, pen, keep, me, fwd, dest));
        }

        /// <summary>Main thread: a snapshot of the traffic waypoint graph (positions, next waypoints, neighbouring lanes).</summary>
        private static void EnsureGraph()
        {
            try
            {
                var sd = GleyUrbanAssets.CurrentSceneData.GetSceneInstance();
                if (sd != null && ReferenceEquals(sd, _graphOf) && _pos != null) return;
                var wps = sd?.allWaypoints;
                if (wps == null || wps.Length == 0) { _pos = null; return; }
                int n = wps.Length;
                var pos = new Vector3[n]; var next = new int[n][]; var lanes = new int[n][]; var limit = new float[n];
                var prev = new int[n][]; var names = new string[n];
                for (int i = 0; i < n; i++)
                {
                    var w = wps[i];
                    if (w == null) { next[i] = Array.Empty<int>(); lanes[i] = Array.Empty<int>(); prev[i] = Array.Empty<int>(); names[i] = ""; continue; }
                    pos[i] = w.position;
                    limit[i] = w.maxSpeed;   // AUTO-DRIVE-1: km/h, 0 = none
                    next[i] = w.neighbors != null ? w.neighbors.ToArray() : Array.Empty<int>();
                    lanes[i] = w.otherLanes != null ? w.otherLanes.ToArray() : Array.Empty<int>();
                    prev[i] = w.prev != null ? w.prev.ToArray() : Array.Empty<int>();
                    names[i] = w.name ?? "";
                }
                var lanesPlus = NavLanes.AddLaneLinks(pos, next, lanes, prev, names, out int linked);   // NAV-LANES-1
                var pen = NavLanes.TurnPenalties(pos, next, prev, names, out int inner);
                var keep = NavLanes.KeepRight(pos, next, prev, names, out int leftish);
                _pos = pos; _next = next; _lanes = lanesPlus; _pen = pen; _keep = keep; _limit = limit; _graphOf = sd;
                Plugin.Logger.LogInfo($"[Nav] road graph: {n} waypoints; {linked} lane link(s) added where the graph had none, {inner} junction turn(s) from an inner lane cost extra, {leftish} waypoint(s) left of another lane (keep right).");
            }
            catch (Exception ex) { _pos = null; Plugin.Logger.LogWarning($"[Nav] road graph: {ex.Message}"); }
        }

        /// <summary>Background thread. NAV-SHORT-1 (user 2026-11-01: "the navigation does not find the shortest route"):
        /// it used to start on ONE lane (the nearest one facing the car's way) and aim at ONE waypoint (the nearest to the
        /// destination) - on the far side of the street that meant a loop around the block, every time. Now every lane
        /// around the car that runs its way is a start (getting onto a farther one costs a little more), every lane close
        /// to the destination is a goal, and the search picks the cheapest whole way - lanes plus the last metres to the
        /// destination. Waypoints link in their traffic direction, so one-way streets stay one-way.</summary>
        private static List<Vector3>? RoadRoute(Vector3[] pos, int[][] next, int[][] lanes, float[][]? pen, float[]? keep, Vector3 me, Vector3 fwd, Vector3 dest)
        {
            int n = pos.Length;
            fwd.y = 0f; fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
            float bestAligned = float.MaxValue, bestAny = float.MaxValue, bestGoal = float.MaxValue;
            int any = -1;
            var aligned = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (next[i].Length == 0) continue;
                float dm = Mathf.Sqrt(Sq(pos[i], me)), dd = Mathf.Sqrt(Sq(pos[i], dest));
                if (dd < bestGoal) bestGoal = dd;
                if (dm < bestAny) { bestAny = dm; any = i; }
                int j = next[i][0];
                if (dm < 60f && j >= 0 && j < n)
                {
                    Vector3 dir = pos[j] - pos[i]; dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f && Vector3.Dot(dir.normalized, fwd) > 0.3f) { aligned[i] = true; if (dm < bestAligned) bestAligned = dm; }
                }
            }
            if (bestGoal > 250f || any < 0) return null;
            var starts = new List<(int wp, float cost)>();
            var goals = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                if (next[i].Length == 0) continue;
                float dm = Mathf.Sqrt(Sq(pos[i], me)), dd = Mathf.Sqrt(Sq(pos[i], dest));
                if (aligned[i] && dm <= bestAligned + 20f) starts.Add((i, dm * 1.5f));
                if (dd <= bestGoal + 35f) goals.Add(i);
            }
            if (starts.Count == 0) starts.Add((any, bestAny * 1.5f));   // nothing facing the car's way nearby: the nearest lane
            var path = AStarMulti(pos, next, lanes, pen, keep, starts, goals, dest);
            if (path == null) return null;
            var r = new List<Vector3>(path.Count + 2) { me };
            foreach (int i in path) r.Add(pos[i]);
            r.Add(dest);
            return r;
        }

        /// <summary>NAV-SHORT-1: A* from several starts (each with its cost to get there) to the first of several goals,
        /// the goal's own distance to the destination included. h = flat distance to the destination: it never
        /// overestimates (a lane piece costs at least its length), so the first goal taken off the heap is the cheapest
        /// whole way.</summary>
        private static List<int>? AStarMulti(Vector3[] pos, int[][] next, int[][] lanes, float[][]? pen, float[]? keep, List<(int wp, float cost)> starts, HashSet<int> goals, Vector3 dest)
        {
            int n = pos.Length;
            var gs = new float[n]; var came = new int[n]; var closed = new bool[n];
            for (int i = 0; i < n; i++) { gs[i] = float.MaxValue; came[i] = -1; }
            var heap = new MinHeap();
            foreach (var (wp, cost) in starts)
            {
                if (cost >= gs[wp]) continue;
                gs[wp] = cost;
                heap.Push(cost + Mathf.Sqrt(Sq(pos[wp], dest)), wp);
            }
            int found = -1, iter = 0;
            while (heap.Count > 0 && iter++ < 500000)
            {
                int u = heap.Pop();
                if (closed[u]) continue;
                if (goals.Contains(u)) { found = u; break; }
                closed[u] = true;
                for (int pass = 0; pass < 2; pass++)
                {
                    var list = pass == 0 ? next[u] : lanes[u];
                    float extra = pass == 0 ? 0f : 6f;   // a lane change costs a little
                    var pu = pass == 0 && pen != null ? pen[u] : null;   // NAV-LANES-1: a turn from an inner lane costs more
                    float fu = pass == 0 && keep != null ? keep[u] : 1f;  // NAV-LANES-1: keep right
                    for (int k = 0; k < list.Length; k++)
                    {
                        int v = list[k];
                        if (v < 0 || v >= n || closed[v]) continue;
                        float c = gs[u] + Vector3.Distance(pos[u], pos[v]) * fu + extra + (pu != null && k < pu.Length ? pu[k] : 0f);
                        if (c >= gs[v]) continue;
                        gs[v] = c; came[v] = u;
                        heap.Push(c + Mathf.Sqrt(Sq(pos[v], dest)), v);
                    }
                }
            }
            if (found < 0) return null;
            var path = new List<int>();
            for (int v = found; v >= 0; v = came[v]) path.Add(v);
            path.Reverse();
            return path;
        }

        private static float Sq(Vector3 a, Vector3 b) { float x = a.x - b.x, z = a.z - b.z; return x * x + z * z; }

        private sealed class MinHeap
        {
            private readonly List<(float k, int v)> _a = new List<(float, int)>();
            public int Count => _a.Count;
            public void Push(float k, int v)
            {
                _a.Add((k, v));
                int i = _a.Count - 1;
                while (i > 0) { int p = (i - 1) / 2; if (_a[p].k <= _a[i].k) break; (_a[p], _a[i]) = (_a[i], _a[p]); i = p; }
            }
            public int Pop()
            {
                int top = _a[0].v;
                _a[0] = _a[_a.Count - 1]; _a.RemoveAt(_a.Count - 1);
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < _a.Count && _a[l].k < _a[m].k) m = l;
                    if (r < _a.Count && _a[r].k < _a[m].k) m = r;
                    if (m == i) break;
                    (_a[m], _a[i]) = (_a[i], _a[m]); i = m;
                }
                return top;
            }
        }

        // ── the city map: a marker, and a click sets the destination ──
        private static Vector2 _downAt;
        private static float _downTime;
        private static bool _down;

        private static bool OverUi()
        {
            try { return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(); } catch { return false; }
        }

        /// <summary>Main thread, every frame while the city map is open.</summary>
        internal static void TickMap()
        {
            if (!CityMap.IsOpen || !ServerRules.Module("ModuleNavigation")) return;
            if (_poi != null && !_poi.gameObject.activeSelf) _poi.gameObject.SetActive(true);
            // a left CLICK (pressed and released on the same spot, quickly) sets it; held and dragged, the map moves as before.
            // Over the map's own icons and windows (UI) the click stays theirs.
            Vector2 mouse = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) { _down = !OverUi(); _downAt = mouse; _downTime = Time.unscaledTime; }
            bool click = Input.GetMouseButtonDown(2) && !OverUi();
            if (Input.GetMouseButtonUp(0) && _down)
            {
                _down = false;
                if ((mouse - _downAt).magnitude <= 6f && Time.unscaledTime - _downTime <= 0.4f && !OverUi()) click = true;
            }
            if (!click) return;
            var cam = GameManager.GetMainCamera();
            if (cam == null) return;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            Vector3 point; string name;
            if (Physics.Raycast(ray, out var hit, 20000f, ~0, QueryTriggerInteraction.Ignore))
            {
                var cbc = hit.collider.GetComponentInParent<CityBuildingController>();
                if (cbc != null && cbc.building != null && TryBuilding(cbc.building.Address, out point, out name)) { Set(point, name); Notice(Loc.T("gps_nav_set", "Destination set: {0}.", name)); return; }
                point = hit.point;
            }
            else
            {
                var plane = new Plane(Vector3.up, Vector3.zero);
                if (!plane.Raycast(ray, out float t)) return;
                point = ray.GetPoint(t);
            }
            name = Loc.T("gps_nav_map_point", "Point on the map");
            Set(point, name);
            Notice(Loc.T("gps_nav_set", "Destination set: {0}.", name));
        }

        /// <summary>A building's way in (its first entrance door) and its name: the business, else the address.</summary>
        internal static bool TryBuilding(Address? a, out Vector3 point, out string name)
        {
            point = Vector3.zero; name = "";
            if (a == null) return false;
            // NAV-NAME-1 (user screenshot 2026-10-31: "Ziel gesetzt: ba:street_secondstreet 44."): the game's own spelling of
            // the address ("44 Second Street", localized), not the raw street key
            try { name = Streets.AddressHelper.ToFormattedString(a); } catch { name = a.ToString(); }
            try
            {
                var cbc = InstanceBehavior<CityManager>.Instance?.FindCityBuildingController(a);
                if (cbc == null) return false;
                var door = cbc.entranceDoors != null && cbc.entranceDoors.Length > 0 ? cbc.entranceDoors[0]?.doorTransform : null;
                point = door != null ? door.position : cbc.transform.position;
                var reg = BuildingHelper.GetBuildingRegistration(a);
                if (reg != null && !string.IsNullOrWhiteSpace(reg.BusinessName)) name = reg.BusinessName;
                return true;
            }
            catch { return false; }
        }

        private static void MapMarker()
        {
            DropMarker();
            try
            {
                var map = InstanceBehavior<CityManager>.Instance?.cityMap;
                if (map == null) return;
                _anchor = new GameObject("BAMP_NavDestination");
                _anchor.transform.position = Dest;
                _icon ??= MPPhoneButton.LoadIconFile("BAMP_HubIcon.png");
                var poi = map.AddPoi(_anchor.transform, _icon!, PinColour, DestName);
                poi.offset = new Vector3(0f, 2.2f, 0f);
                if (CityMap.IsOpen) poi.gameObject.SetActive(true);
                map.UpdateNonePermanentPointOfInterests();
                _poi = poi;
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Nav] map marker: {ex.Message}"); }
        }

        private static void DropMarker()
        {
            try { if (_poi != null) UnityEngine.Object.Destroy(_poi.gameObject); } catch { }   // its OnDestroy leaves the map's list
            try { if (_anchor != null) UnityEngine.Object.Destroy(_anchor); } catch { }
            _poi = null; _anchor = null;
        }

        /// <summary>The game's own click-to-walk, to a point of the NavMesh near the destination.</summary>
        internal static string WalkThere()
        {
            if (!HasDest) return "";
            if (InCar()) return Loc.T("gps_nav_walk_car", "Get out of the vehicle to walk.");
            if (CityMap.IsOpen) return Loc.T("gps_nav_walk_map_open", "Close the city map first.");
            try
            {
                var pc = PlayerHelper.PlayerController;
                if (pc == null) return "";
                Vector3 t = Dest;
                if (NavMesh.SamplePosition(Dest, out var h, 30f, PlayerController.navMeshQueryFilter)) t = h.position;
                // NAV-WALK-2 (user 2026-11-08: "das automatische Laufen zu Fuß funktioniert nicht mehr", a screenshot with the
                // building card of the house beside the player up): PlayerController.SetNewDestination drops a destination
                // without a word while a building's detailed card is up (OverlayManager.blockPlayerNavigation - the pointer
                // rests on a building once the map closes) and when the new target is the last one it took (_lastSafeTarget:
                // a second "walk there" to the same place after the first walk stopped). Both are cleared first; a walk that
                // still does not start says why.
                try
                {
                    var om = InstanceBehavior<Player.HUD.ItemInfoOverlays.OverlayManager>.Instance;
                    if (om != null) { om.HideOverlays(); om.blockPlayerNavigation = false; }
                }
                catch { }
                UnityEngine.AI.NavMeshAgent? ag = null;
                try { ag = pc.Character?.navmeshAgent; } catch { }
                try { if (ag != null && ag.enabled && !ag.hasPath && !ag.pathPending) pc.ResetNavigation(); } catch { }
                pc.SetNewDestination(t, false);
                bool moving = false;
                try { moving = ag != null && ag.enabled && (ag.hasPath || ag.pathPending); } catch { }
                if (!moving)
                {
                    string why = "no path", text;
                    bool keys = false, blocked = false;
                    try { blocked = pc.NavigationDisabled; } catch { }
                    try { keys = pc._inputVelocity.sqrMagnitude > 0f; } catch { }
                    if (blocked) { why = "the game blocks walking right now"; text = Loc.T("gps_nav_walk_blocked", "The game does not let you walk right now - try again in a moment."); }
                    else if (keys) { why = "movement keys held"; text = Loc.T("gps_nav_walk_keys", "Let go of the movement keys - then the walk starts."); }
                    else text = Loc.T("gps_nav_walk_nopath", "No footpath to {0} found from here.", DestName);
                    Plugin.Logger.LogInfo($"[Nav] the walk to '{DestName}' did not start: {why} (NAV-WALK-2).");
                    return text;
                }
                bool paused = false;
                try { paused = InstanceBehavior<UI.UIs>.Instance?.gameSpeed?.Paused ?? false; } catch { }
                Plugin.Logger.LogInfo($"[Nav] walking to '{DestName}'{(paused ? " - the game is paused, the walk starts when it goes on" : "")}.");
                if (paused) return Loc.T("gps_nav_walk_paused", "The game is paused - you walk to {0} as soon as it goes on.", DestName);
                return Loc.T("gps_nav_walking", "Walking to {0}.", DestName);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Nav] walk: {ex.Message}"); return ex.Message; }
        }

        // ── bookmarks (this machine, MPConfig "NavBookmarks") ──

        private static void Load()
        {
            _loaded = true;
            try
            {
                var list = JsonConvert.DeserializeObject<List<Bookmark>>(MPConfig.NavBookmarks);
                if (list != null) Bookmarks.AddRange(list.Where(b => b != null));
            }
            catch { }
        }

        internal static void SaveBookmarks()
        {
            try { MPConfig.SetNavBookmarks(JsonConvert.SerializeObject(Bookmarks)); } catch { }
        }

        internal static void AddBookmark(string name, Vector3 p)
        {
            if (string.IsNullOrWhiteSpace(name)) name = Loc.T("gps_nav_bm_default", "Bookmark {0}", Bookmarks.Count + 1);
            Bookmarks.Add(new Bookmark { n = name.Trim(), x = p.x, y = p.y, z = p.z });
            SaveBookmarks();
        }

        internal static void Notice(string text)
        {
            try { UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, text); } catch { }
        }
    }

    /// <summary>MODULE-NAVIGATION-1: draws the route over the picture after the cameras have moved (its own overlay canvas,
    /// below the game's UI), plus the pin at the destination and the line at the top of the screen.</summary>
    [DefaultExecutionOrder(32000)]
    internal sealed class RouteOverlay : MonoBehaviour
    {
        internal TMP_FontAsset? Font;
        private readonly List<RectTransform> _segs = new List<RectTransform>();
        private readonly List<Image> _segImgs = new List<Image>();
        private RectTransform? _pin, _pinBg, _hudBg;
        private TextMeshProUGUI? _pinText, _hud;
        private NavDevice? _device;   // NAV-DEVICE-1
        private Vector3 _lastMe;
        private float _speedKmh;
        private static readonly Color Line = new Color(0.10f, 0.72f, 1f, 0.85f);
        private static readonly Color WalkLine = new Color(1f, 0.55f, 0.10f, 0.9f);   // NAV-ROAD-2: a walk in orange
        private const int MaxSegs = 400;

        private void Start()
        {
            for (int i = 0; i < MaxSegs; i++)
            {
                var go = new GameObject("Seg", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var img = go.GetComponent<Image>(); img.color = Line; img.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f);
                go.SetActive(false);
                _segs.Add(rt);
                _segImgs.Add(img);
            }
            _device = new NavDevice(transform, Font);   // NAV-DEVICE-1
            var pin = new GameObject("Pin", typeof(RectTransform), typeof(Image));
            pin.transform.SetParent(transform, false);
            var pimg = pin.GetComponent<Image>(); pimg.color = new Color(1f, 0.85f, 0.25f, 0.95f); pimg.raycastTarget = false;
            _pin = (RectTransform)pin.transform;
            _pin.anchorMin = _pin.anchorMax = Vector2.zero; _pin.sizeDelta = new Vector2(16f, 16f); _pin.localRotation = Quaternion.Euler(0f, 0f, 45f);
            pin.SetActive(false);
            _pinBg = MakeBg("PinBg");
            _pinText = MakeText("PinText", 15f);
            _hudBg = MakeBg("HudBg");
            _hud = MakeText("Hud", 18f);
        }

        /// <summary>A dark plate behind a text (a TMP outline would change the font's shared material for the whole game).</summary>
        private RectTransform MakeBg(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<Image>(); img.color = new Color(0f, 0f, 0f, 0.55f); img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f);
            go.SetActive(false);
            return rt;
        }

        private static void Place(TextMeshProUGUI t, RectTransform bg, Vector2 at)
        {
            ((RectTransform)t.transform).anchoredPosition = at;
            bg.anchoredPosition = at;
            bg.sizeDelta = new Vector2(Mathf.Min(t.preferredWidth, 880f) + 20f, t.fontSize + 12f);
        }

        private TextMeshProUGUI MakeText(string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = Color.white; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(900f, 30f);
            go.SetActive(false);
            return t;
        }

        private void LateUpdate()
        {
            int used = 0;
            bool pinOn = false, hudOn = false, deviceOn = false;
            try
            {
                if (Navigator.HasDest && ServerRules.Module("ModuleNavigation") && SaveGameManager.Current != null && !LoadScenePending())
                {
                    var cam = GameManager.GetMainCamera();
                    Vector3 me = Navigator.Me();
                    float dist = Navigator.Flat(me, Navigator.Dest);
                    float dt = Mathf.Max(0.001f, Time.unscaledDeltaTime);   // NAV-DEVICE-1: the speed for its zoom
                    float v = Mathf.Min(Navigator.Flat(me, _lastMe) / dt * 3.6f, 250f);
                    _lastMe = me;
                    _speedKmh = Mathf.Lerp(_speedKmh, v, Mathf.Clamp01(dt * 2f));
                    // NAV-DEVICE-1: in the street view the device tells all of it; the line at the top stays for the map and indoors
                    if (_device != null && !CityMap.IsOpen && !Navigator.Inside()) { _device.Show(me, _speedKmh); deviceOn = true; }
                    if (_hud != null && _hudBg != null && !deviceOn)
                    {
                        _hud.text = Navigator.Inside()
                            ? Loc.T("gps_nav_hud_inside", "Leave the building to see the route to {0}.", Navigator.DestName)
                            : Loc.T("gps_nav_hud", "{0} · {1}", Navigator.DestName, Navigator.Distance(dist))
                              + TurnPart(me);   // NAV-TURN-1
                        Place(_hud, _hudBg, new Vector2(Screen.width / 2f, Screen.height - 58f));
                        hudOn = true;
                    }
                    if (cam != null && !Navigator.Inside())
                    {
                        if (Navigator.ShowLine && Navigator.Route.Count >= 2)
                        {
                            // NAV-LINE-2 (user 2026-11-01: "the path rises into the sky in the distance - the whole of it on the map"):
                            // a line over the screen is never hidden by the houses, so the far part of a long route stood up over the
                            // city. The map still draws all of it; the street view shows the navigation device instead (NavDevice).
                            if (CityMap.IsOpen)
                            {
                                Vector3 prev = me;
                                for (int i = Navigator.Passed + 1; i < Navigator.Route.Count && used < MaxSegs; i++)
                                {
                                    Vector3 cur = Navigator.Route[i];
                                    if (Seg(cam, prev + Vector3.up * 0.4f, cur + Vector3.up * 0.4f, 6f, used, Navigator.RouteCar ? Line : WalkLine)) used++;   // NAV-ROAD-2
                                    prev = cur;
                                }
                            }
                        }
                        Vector3 sp = cam.WorldToScreenPoint(Navigator.Dest + Vector3.up * 2.5f);
                        if (sp.z > 0.5f && _pin != null && _pinText != null && _pinBg != null)
                        {
                            _pin.anchoredPosition = new Vector2(sp.x, sp.y);
                            _pinText.text = Navigator.Distance(dist);
                            Place(_pinText, _pinBg, new Vector2(sp.x, sp.y + 28f));
                            pinOn = true;
                        }
                    }
                }
            }
            catch { }
            for (int i = used; i < _segs.Count; i++) if (_segs[i].gameObject.activeSelf) _segs[i].gameObject.SetActive(false);
            if (_device != null && !deviceOn) _device.Hide();
            if (_pin != null && _pin.gameObject.activeSelf != pinOn) _pin.gameObject.SetActive(pinOn);
            if (_pinText != null && _pinText.gameObject.activeSelf != pinOn) _pinText.gameObject.SetActive(pinOn);
            if (_pinBg != null && _pinBg.gameObject.activeSelf != pinOn) _pinBg.gameObject.SetActive(pinOn);
            if (_hud != null && _hud.gameObject.activeSelf != hudOn) _hud.gameObject.SetActive(hudOn);
            if (_hudBg != null && _hudBg.gameObject.activeSelf != hudOn) _hudBg.gameObject.SetActive(hudOn);
        }

        private static bool LoadScenePending() { try { return UI.Load.LoadScene.isLoading; } catch { return false; } }

        private static string TurnPart(Vector3 me)
        {
            string t = Navigator.TurnHint(me);
            return t.Length == 0 ? "" : "  ·  <b>" + t + "</b>";
        }

        /// <summary>One piece of the line, clipped at the camera's near side; false when nothing of it is on screen.</summary>
        private bool Seg(Camera cam, Vector3 a, Vector3 b, float thick, int slot, Color colour)
        {
            Vector3 sa = cam.WorldToScreenPoint(a), sb = cam.WorldToScreenPoint(b);
            const float near = 0.5f;
            if (sa.z < near && sb.z < near) return false;
            if (sa.z < near) sa = cam.WorldToScreenPoint(Vector3.Lerp(a, b, (near - sa.z) / (sb.z - sa.z)));
            else if (sb.z < near) sb = cam.WorldToScreenPoint(Vector3.Lerp(b, a, (near - sb.z) / (sa.z - sb.z)));
            float w = Screen.width, h = Screen.height;
            if ((sa.x < 0 && sb.x < 0) || (sa.x > w && sb.x > w) || (sa.y < 0 && sb.y < 0) || (sa.y > h && sb.y > h)) return false;
            Vector2 p = new Vector2(sa.x, sa.y), q = new Vector2(sb.x, sb.y);
            Vector2 d = q - p;
            float len = d.magnitude;
            if (len < 0.5f || len > 4f * (w + h)) return false;
            var rt = _segs[slot];
            rt.anchoredPosition = (p + q) / 2f;
            rt.sizeDelta = new Vector2(len + thick * 0.5f, thick);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            if (slot < _segImgs.Count && _segImgs[slot].color != colour) _segImgs[slot].color = colour;
            if (!rt.gameObject.activeSelf) rt.gameObject.SetActive(true);
            return true;
        }
    }

    public partial class MPCanvasUI
    {
        private const int NAV_ROWS = 11;
        private GameObject? _navRoot, _navDyn, _navOverlay;
        private TMP_InputField? _navSearch;
        private bool _navOpen;
        private int _navTab, _navPage;
        private string _navQuery = "", _navStatus = "";
        private readonly List<(RectTransform rt, int act, int idx)> _navHits = new List<(RectTransform, int, int)>();
        private readonly List<(string name, string where, Vector3 pos, int bookmark)> _navItems = new List<(string, string, Vector3, int)>();
        private readonly Dictionary<int, string> _navFollowPid = new Dictionary<int, string>();   // AUTO-DRIVE-1: row -> friend to follow
        private const int NavGarage = -2;                                                          // AUTO-DRIVE-1: a car park entrance row

        private static bool NavKey() => ModKeys.Navigation.Down();   // MOD-KEYS-1: Shift+F11 unless the player chose another key

        /// <summary>Main thread, every frame from Update: the route, the map click, and Shift+F11 toggles the window.</summary>
        private void TickNavigation()
        {
            if (HeadlessHost.Active) return;
            try
            {
                if (_navOverlay == null)
                {
                    _navOverlay = new GameObject("BAMP_RouteOverlay");
                    DontDestroyOnLoad(_navOverlay);
                    var c = _navOverlay.AddComponent<Canvas>();
                    c.renderMode = RenderMode.ScreenSpaceOverlay;
                    c.sortingOrder = -100;   // over the picture, under the game's own UI
                    _navOverlay.AddComponent<RouteOverlay>().Font = IsAlive(_gameFont) ? _gameFont : null;
                }
                bool inWorld = SaveGameManager.Current != null && PlayerHelper.PlayerController != null && !GameManager.isCitySceneBeingUnloaded;
                if (!inWorld) { if (_navOpen) NavClose(); if (Navigator.HasDest) Navigator.Clear(); return; }
                Navigator.Tick();
                Navigator.TickMap();
                if (!_navOpen)
                {
                    if (NavKey() && !MPChat.SuppressGameInput && !GameManager.HasInputSelected() && !FullMenu.IsOpen)
                    {
                        if (ServerRules.Module("ModuleNavigation")) NavOpen();
                        else ServerRules.ModuleOff(Loc.T("gps_nav_title", "Navigation"));
                    }
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Escape) || NavKey()) { NavClose(); ConsumeEscape(); return; }
                if (!Input.GetMouseButtonDown(0)) return;
                var mp = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                foreach (var (rt, act, idx) in _navHits)
                    if (rt != null && rt.gameObject.activeInHierarchy && RectHit(rt, mp)) { NavAct(act, idx); return; }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Nav] tick: {ex.Message}"); }
        }

        private void NavOpen()
        {
            _navOpen = true; _navStatus = ""; _navPage = 0;
            NavBuild();
            Plugin.Logger.LogInfo("[Nav] window opened.");
        }

        private void NavClose()
        {
            _navOpen = false;
            ShieldPressUntilRelease();   // NAV-WALK-CLICK-1: the release of the press that closed it is not a world click
            _navHits.Clear();
            _navSearch = null;
            if (_navRoot != null) { try { Destroy(_navRoot); } catch { } _navRoot = null; _navDyn = null; }
        }

        // acts: 1 tab (idx) · 2 pick item (idx) · 3 delete bookmark (idx) · 4 clear destination · 5 walk there
        //       6 route line on/off · 7 bookmark my position · 8 bookmark the destination · 9/10 page -/+ · 11 close
        private void NavAct(int act, int idx)
        {
            switch (act)
            {
                case 1: _navTab = idx; _navPage = 0; break;
                case 2:
                    if (idx >= 0 && idx < _navItems.Count)
                    {
                        var it = _navItems[idx];
                        Navigator.Set(it.pos, it.name);
                        if (it.bookmark == NavGarage) Navigator.DestIsGarage = true;   // AUTO-DRIVE-1: the car drives in
                        if (_navFollowPid.TryGetValue(idx, out var fpid))
                        {
                            // AUTO-DRIVE-1: follow a friend - the destination moves with him; in the car, the car drives behind him
                            Navigator.FollowPid = fpid;
                            Navigator.DestName = MPNames.Resolve(fpid);
                            Navigator.Notice(Loc.T("gps_nav_follow_on", "Following {0}.", Navigator.DestName));
                            NavClose();
                            if (Navigator.InCar()) AutoDrive.Start();
                            return;
                        }
                        Navigator.Notice(Loc.T("gps_nav_set", "Destination set: {0}.", it.name));
                        NavClose();
                        return;
                    }
                    break;
                case 3:
                    if (idx >= 0 && idx < Navigator.Bookmarks.Count) { Navigator.Bookmarks.RemoveAt(idx); Navigator.SaveBookmarks(); }
                    break;
                case 4: Navigator.Clear(); break;
                case 5:
                    _navStatus = Navigator.WalkThere();
                    if (_navStatus.Length > 0 && !Navigator.InCar() && !CityMap.IsOpen) { NavClose(); Navigator.Notice(_navStatus); return; }
                    break;
                case 6:
                    if (ServerRules.FromServer) ServerRules.ServerSet(Loc.T("gps_nav_line_name", "Route line"));
                    else Navigator.ToggleLine();
                    break;
                case 7: Navigator.AddBookmark(_navQuery, Navigator.Me()); _navTab = 1; break;
                case 8: if (Navigator.HasDest) { Navigator.AddBookmark(_navQuery.Length > 0 ? _navQuery : Navigator.DestName, Navigator.Dest); _navTab = 1; } break;
                case 9: _navPage = Math.Max(0, _navPage - 1); break;
                case 10: _navPage++; break;
                case 11: NavClose(); return;
            }
            NavDyn();
        }

        private const float NAV_W = 980f, NAV_H = 680f;

        /// <summary>The window: card and the search field once (typing must keep its focus), the rest in NavDyn.</summary>
        private void NavBuild()
        {
            if (_canvasGO == null) return;
            if (_navRoot != null) { try { Destroy(_navRoot); } catch { } }
            _navRoot = LMakeRoot("BAMP_Navigation");
            _navRoot.transform.SetAsLastSibling();
            var card = LMakeCard(_navRoot.transform, "Card", Loc.T("gps_nav_title", "Navigation"), NAV_W, NAV_H);
            card.sub.text = ModKeys.Navigation.Display();   // MOD-KEYS-1
            var ct = card.go.transform;

            var input = MakeGO("Search", ct);
            var irt = input.GetComponent<RectTransform>();
            LPos(irt, 26f, 150f, NAV_W - 52f, 36f);
            var img = input.AddComponent<Image>(); img.color = C_FIELD;
            var viewport = MakeGO("TextArea", input.transform);
            var vrt = viewport.GetComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(12f * LS, 6f * LS); vrt.offsetMax = new Vector2(-12f * LS, -6f * LS);
            viewport.AddComponent<RectMask2D>();
            TextMeshProUGUI Txt(string name, Color c)
            {
                var go = MakeGO(name, viewport.transform);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var t = go.AddComponent<TextMeshProUGUI>();
                t.fontSize = 14f * LS; t.color = c; t.alignment = TextAlignmentOptions.Left;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                ApplyFont(t);
                return t;
            }
            var text = Txt("Text", C_WHITE);
            var ph = Txt("Placeholder", L_MUTED);
            ph.text = Loc.T("gps_nav_search_hint", "Search a business or a street...");
            _navSearch = input.AddComponent<TMP_InputField>();
            _navSearch.textViewport = vrt; _navSearch.textComponent = text; _navSearch.placeholder = ph; _navSearch.targetGraphic = img;
            _navSearch.lineType = TMP_InputField.LineType.SingleLine; _navSearch.characterLimit = 40;
            _navSearch.caretWidth = 2; _navSearch.customCaretColor = true; _navSearch.caretColor = C_WHITE;
            _navSearch.text = _navQuery;
            _navSearch.onValueChanged.AddListener(v => { _navQuery = v ?? ""; _navPage = 0; if (_navQuery.Length > 0 && _navTab == 0) _navTab = 2; NavDyn(); });

            _navDyn = MakeGO("Dyn", ct);
            LStretch(_navDyn.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            NavDyn();
        }

        private void NavDyn()
        {
            if (_navDyn == null) return;
            foreach (Transform c in _navDyn.transform) { try { Destroy(c.gameObject); } catch { } }
            _navHits.Clear();
            var ct = _navDyn.transform;
            const float LX = 26f;
            float W = NAV_W, H = NAV_H;

            // destination line + its buttons
            string dl = Navigator.HasDest
                ? Loc.T("gps_nav_dest_line", "Destination: {0}", Navigator.DestName) + "  ·  " + Navigator.Distance(Navigator.Flat(Navigator.Me(), Navigator.Dest))
                : Loc.T("gps_nav_dest_none", "No destination.");
            var dt = LText(ct, dl, 14f, C_WHITE, LX, 58f, W - 52f - 3 * 168f, 34f, TextAlignmentOptions.Left, true);
            dt.overflowMode = TextOverflowModes.Ellipsis;
            float bx = W - 26f - 3 * 164f;
            var lb = LButton(ct, "Line", Navigator.ShowLine ? Loc.T("gps_nav_line_on", "Route line: on") : Loc.T("gps_nav_line_off", "Route line: off"), bx, 58f, 156f, 34f,
                ServerRules.FromServer ? 4 : Navigator.ShowLine ? 1 : 0, 11f); _navHits.Add((lb.rt, 6, 0));   // on a server: greyed, a click says who decides
            var wb = LButton(ct, "Walk", Loc.T("gps_nav_walk", "Walk there"), bx + 164f, 58f, 156f, 34f, Navigator.HasDest ? 0 : 4, 11f); if (Navigator.HasDest) _navHits.Add((wb.rt, 5, 0));
            var cb = LButton(ct, "Clear", Loc.T("gps_nav_clear", "Clear"), bx + 328f, 58f, 156f, 34f, Navigator.HasDest ? 3 : 4, 11f); if (Navigator.HasDest) _navHits.Add((cb.rt, 4, 0));
            LText(ct, Loc.T("gps_nav_map_hint", "On the city map, click a building or a spot to set the destination - hold and drag to move the map."), 12f, L_MUTED, LX, 100f, W - 52f, 40f, TextAlignmentOptions.TopLeft, false, false, 0f, true);

            // tabs
            string[] tabs = { Loc.T("gps_nav_tab_mine", "My places"), Loc.T("gps_nav_tab_bookmarks", "Bookmarks"), Loc.T("gps_nav_tab_city", "City") };
            for (int i = 0; i < tabs.Length; i++)
            {
                var t = LButton(ct, "Tab" + i, tabs[i], LX + i * 172f, 196f, 164f, 32f, i == _navTab ? 1 : 0, 11f);
                _navHits.Add((t.rt, 1, i));
            }
            if (_navTab == 1)
            {
                var ab = LButton(ct, "AddHere", Loc.T("gps_nav_add_here", "Bookmark my position"), W - 26f - 2 * 214f - 8f, 196f, 214f, 32f, 0, 10f); _navHits.Add((ab.rt, 7, 0));
                var ad = LButton(ct, "AddDest", Loc.T("gps_nav_add_dest", "Bookmark the destination"), W - 26f - 214f, 196f, 214f, 32f, Navigator.HasDest ? 0 : 4, 10f); if (Navigator.HasDest) _navHits.Add((ad.rt, 8, 0));
            }

            NavFill();
            float y = 238f;
            int pages = Math.Max(1, (_navItems.Count + NAV_ROWS - 1) / NAV_ROWS);
            _navPage = Mathf.Clamp(_navPage, 0, pages - 1);
            if (_navItems.Count == 0)
                LText(ct, _navTab == 1 ? Loc.T("gps_nav_bm_hint", "No bookmarks yet. Type a name in the search field, then press a bookmark button.") : Loc.T("gps_nav_none_found", "Nothing found."),
                    13f, L_MUTED, LX, y + 4f, W - 52f, 40f, TextAlignmentOptions.TopLeft, false, false, 0f, true);
            Vector3 me = Navigator.Me();
            for (int r = 0; r < NAV_ROWS; r++)
            {
                int i = _navPage * NAV_ROWS + r;
                if (i >= _navItems.Count) break;
                var it = _navItems[i];
                float rw = W - 52f - (it.bookmark >= 0 ? 44f : 0f);
                var bg = LImg(ct, "Row", L_ROW, LRoundSprite(false), 3f);
                LPos(bg.rectTransform, LX, y + r * 32f, rw, 29f);
                var nt = LText(bg.transform, it.name, 13f, C_WHITE, 10f, 0f, rw * 0.5f, 29f, TextAlignmentOptions.Left);
                nt.overflowMode = TextOverflowModes.Ellipsis;
                var wt = LText(bg.transform, it.where, 12f, L_MUTED, rw * 0.5f + 10f, 0f, rw * 0.5f - 130f, 29f, TextAlignmentOptions.Left);
                wt.overflowMode = TextOverflowModes.Ellipsis;
                LText(bg.transform, Navigator.Distance(Navigator.Flat(me, it.pos)), 12f, L_MUTED, rw - 116f, 0f, 106f, 29f, TextAlignmentOptions.Right);
                _navHits.Add((bg.rectTransform, 2, i));
                if (it.bookmark >= 0)
                {
                    var del = LButton(ct, "Del" + i, "x", LX + rw + 6f, y + r * 32f, 38f, 29f, 3, 11f);
                    _navHits.Add((del.rt, 3, it.bookmark));
                }
            }
            if (pages > 1) SgPagerTo(_navHits, ct, LX, y + NAV_ROWS * 32f + 4f, 300f, _navPage, pages, 9, 10);
            if (_navStatus.Length > 0) LText(ct, _navStatus, 12f, L_NOTICE, LX, H - 56f, W - 240f, 36f, TextAlignmentOptions.TopLeft, false, false, 0f, true);
            var cl = LButton(ct, "Close", Loc.T("gps_ui_close", "Close"), W - 26f - 150f, H - 54f, 150f, 36f, 0, 12f); _navHits.Add((cl.rt, 11, 0));
        }

        /// <summary>The rows of the current tab, filtered by the search text; nearest first.</summary>
        private void NavFill()
        {
            _navItems.Clear();
            string q = _navQuery.Trim();
            bool Match(string a, string b) => q.Length == 0 || a.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || b.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            var gi = SaveGameManager.Current;
            if (_navTab == 1)
            {
                for (int i = 0; i < Navigator.Bookmarks.Count; i++)
                {
                    var b = Navigator.Bookmarks[i];
                    if (Match(b.n, "")) _navItems.Add((b.n, "", new Vector3(b.x, b.y, b.z), i));
                }
                return;
            }
            var seen = new HashSet<string>();
            if (gi?.BuildingRegistrations != null)
            {
                foreach (var reg in gi.BuildingRegistrations)
                {
                    try
                    {
                        if (reg?.Address == null) continue;
                        bool mine = reg.RentedByPlayer && !GameStatePatcher.IsForeignPlayerBusiness(reg);
                        if (_navTab == 0 && !mine) continue;
                        if (_navTab == 2 && string.IsNullOrWhiteSpace(reg.BusinessName)) continue;
                        string addr = reg.Address.ToString();
                        bool home = BuildingHelper.GetBuilding(reg.Address)?.BuildingType == "ba:buildingtype_residential";
                        string name = !string.IsNullOrWhiteSpace(reg.BusinessName) ? reg.BusinessName : home ? Loc.T("gps_nav_home", "Home") : addr;
                        if (!Match(name, addr)) continue;
                        if (!Navigator.TryBuilding(reg.Address, out var p, out _)) continue;
                        if (seen.Add(addr)) _navItems.Add((name, addr, p, -1));
                    }
                    catch { }
                }
            }
            if (_navTab == 0 && gi?.realEstate != null)
            {
                foreach (var re in gi.realEstate)
                {
                    try
                    {
                        if (re?.address == null) continue;
                        string addr = re.address.ToString();
                        if (seen.Contains(addr)) continue;
                        string name = Loc.T("gps_nav_owned", "Your building");
                        if (!Match(name, addr) || !Navigator.TryBuilding(re.address, out var p, out _)) continue;
                        seen.Add(addr);
                        _navItems.Add((name, addr, p, -1));
                    }
                    catch { }
                }
            }
            if (_navTab == 0)
            {
                // AUTO-DRIVE-1 (1.0.16): car parks (the self-driving car drives in) ...
                try
                {
                    foreach (var e in UnityEngine.Object.FindObjectsOfType<Parking.UndergroundParking.UndergroundParkingEntrance>())
                    {
                        if (e == null) continue;
                        string name = Loc.T("gps_nav_garage", "Car park {0}", e.parkingNumber);
                        string where = "";
                        try { if (e.parentCbc != null && e.parentCbc.building != null) where = Streets.AddressHelper.ToFormattedString(e.parentCbc.building.Address); } catch { }
                        if (Match(name, where)) _navItems.Add((name, where, e.transform.position, NavGarage));
                    }
                }
                catch { }
            }
            Vector3 me = Navigator.Me();
            _navItems.Sort((a, b) => Navigator.Flat(me, a.pos).CompareTo(Navigator.Flat(me, b.pos)));
            // ... and the friends who are online, on top: "Follow <name>"
            _navFollowPid.Clear();
            if (_navTab == 0)
            {
                try
                {
                    var friends = new List<(string name, string where, Vector3 pos, int bookmark)>();
                    var pids = new List<string>();
                    foreach (var kv in RemotePlayerManager.Spawned())
                    {
                        if (kv.Value == null || !Friends.IsFriend(kv.Key) || !Navigator.PlayerPos(kv.Key, out var fp)) continue;
                        string name = Loc.T("gps_nav_follow", "Follow {0}", MPNames.Resolve(kv.Key));
                        if (!Match(name, "")) continue;
                        friends.Add((name, Loc.T("gps_nav_follow_where", "friend"), fp, -1));
                        pids.Add(kv.Key);
                    }
                    _navItems.InsertRange(0, friends);
                    for (int i = 0; i < pids.Count; i++) _navFollowPid[i] = pids[i];
                }
                catch { }
            }
        }
    }
}
