using System;
using System.Collections;
using System.Collections.Generic;
using Helpers;
using UnityEngine;
using UnityEngine.Events;

namespace BigAmbitionsMP
{
    /// <summary>AUTO-DRIVE-1: the game's steering assist (Options: steering assist) pulls the car onto the street grid's
    /// angles whenever the steering input is small - against the self-driving's own steering in turns and lane changes.
    /// Off while it drives.</summary>
    [HarmonyLib.HarmonyPatch(typeof(AngleAssist), nameof(AngleAssist.Run))]
    public static class Patch_AngleAssist_Run_AutoDrive
    {
        static bool Prefix(AngleAssist __instance)
        {
            if (!AutoDrive.Active) return true;
            try { __instance._vehicleController.steering.externallyAddedAngle = 0f; } catch { }
            return false;
        }
    }

    /// <summary>AUTO-DRIVE-1 (user 2026-10-31: "in the car, the car should drive to the building"; "the automatic driving
    /// in the car and the automatic parking of VoogleRoute are missing"): the player's own car drives itself along the
    /// navigation's road route - own code - braking for cars, pedestrians and players ahead and at red lights, and at the
    /// destination it parks: the game's own auto-park when it offers a spot there, else the nearest free slot of a street
    /// parking lane (ParkingSpots), with the game's fade. The controls are set in NWH's inputModifyCallback - right after
    /// the player's own input was read - so the moment the player steers, accelerates or brakes, he drives again.
    ///
    /// AUTO-DRIVE-2 (user 2026-11-01 after a test: "drives over red lights", "takes the curves too tight", "drives into
    /// hydrants when turning and gets stuck", "steers only slightly", "the blinkers don't work"):
    ///  * steering: pure pursuit from the REAR axle on a short point (3 m in turns, up to 12 m on a straight) - the rear
    ///    wheels follow the lane and the front swings out as a driver's does; the old 6-18 m from the car's centre turned
    ///    early and gently and cut every corner onto the kerb. NWH narrows the wheel angle with speed (its
    ///    speedSensitiveSteeringCurve: about 55-70 % at 40 km/h) - the input asks for that much more (SteerInput).
    ///  * speed: a curve profile over the next 45 m (lateral 2.2 m/s², at least 13 km/h) instead of one angle test.
    ///  * red lights: the light intersection's own colour per road (what the lamps show; a client gets it from the
    ///    server's light sync), the stop lines matched to the route by position AND driving direction; the front
    ///    bumper stops a metre before the line and stays until green - a car that crept 1.5 m close forgot the light.
    ///  * kerbside objects (hydrants, posts, trees): a short box cast along the route at bumper height; a car that is
    ///    stuck anyway backs off along its arc (up to three times a minute) and tries again.
    ///  * blinkers: the game's own (CarController.blinkers, VehicleBlinker - the blinker keys); NWH's lights manager
    ///    has no blinker lamps in these cars.
    /// Everything else (sync to the others, passengers) is the normal driving: only the inputs come from here.</summary>
    internal static class AutoDrive
    {
        private const float CruiseSpeed = 11f;          // m/s (~40 km/h) where the lane names no limit
        private const float CurveMin = 3.6f;            // m/s (~13 km/h), the slowest a curve asks for
        private const float LatAccel = 2.2f;            // m/s² in curves
        private const float Decel = 2.2f;               // m/s² for the end of the route and curves ahead
        private const float LightDecel = 2.6f;          // m/s² for red lights
        private const float TakeOver = 0.25f;           // player throttle / brake that ends it
        private const float TakeOverSteer = 0.45f;      // player steering that ends it (mouse steering wobbles a little)
        private const float StopShort = 3.5f;           // metres before a car, person or player
        private const float StaticShort = 0.9f;         // metres before a hydrant, post, tree
        // AUTO-DRIVE-4: bridges and long free straights
        private const float FreeSpeed = 16.7f;          // m/s (60 km/h) there, where the lane names no limit
        // AUTO-DRIVE-5 (user 2026-11-08: "die 60 km/h auf der Brücke erhöhen, dass die Wartezeit nicht zu lange ist"; the game's
        // skip-bridge button is gone in multiplayer, SKIP-BRIDGE-OFF-1): on a bridge up to 90 km/h. The braking distance for cars
        // and people grows with the speed (Obstacle), the 200 m look-ahead brings it back to ~40 km/h before the bridge's end;
        // a car slower than that drives its own top speed.
        private const float BridgeSpeed = 25f;          // m/s (90 km/h) on a bridge
        private const float FreeLength = 150f;          // metres without a traffic light, junction or sharp turn: a free stretch
        private const float FreeDecel = 1.6f;           // m/s² back to the cruise speed by 20 m before the next one
        private const float ObstacleDecel = 3f;         // m/s² for a car or person ahead when faster than the cruise speed

        internal static bool Active { get; private set; }
        private enum Phase { Drive, Parking, Entering }
        private static Phase _phase;
        private static CarController? _car;
        private static NWH.VehiclePhysics2.VehicleController? _vc;
        private static UnityAction? _hook;
        private static readonly List<Vector3> _route = new List<Vector3>();
        private static readonly List<float> _along = new List<float>();
        private sealed class StopLine
        {
            public float At;                                            // metres along the route
            public GleyTrafficSystem.TrafficLightsIntersection? Ti;     // its intersection (the live colour per road)
            public int Road;
            public GleyTrafficSystem.Waypoint? Wp;                      // the stop waypoint (fallback: its live stop flag)
            public bool WasRed, DriveOn;
        }
        private static readonly List<StopLine> _lights = new List<StopLine>();
        private static readonly List<float> _junctions = new List<float>();   // AUTO-DRIVE-4: give-way junctions on the route (metres along it)
        private static bool _fast;                                            // AUTO-DRIVE-4: on a free stretch
        private static float _freeAhead, _freeS, _freeNext;
        private static int _routeSig, _passed;
        private static string _destName = "";
        private static Vector3 _dest;
        private static float _wheelBase = 2.7f, _maxSteer = 30f, _halfLength = 2.3f, _halfWidth = 0.9f;
        private static float _slowSince = -1f, _blockedSince = -1f, _startedAt, _enterSince;
        // 1.0.16: a car park as destination (drive in), or a friend to follow (the route end moves with him)
        private static bool _garage, _follow;
        private static Vector3 _garagePos;
        private static bool _blockedNow, _pushing, _staticNow;
        private static int _mask = -1, _staticMask = -1;
        private static readonly RaycastHit[] _hits = new RaycastHit[16];
        // AUTO-DRIVE-2: backing off when stuck
        private static float _stuckSince = -1f, _recoverUntil = -1f, _recoverSteer, _lastSteer;
        private static readonly List<float> _recoveries = new List<float>();
        private static float _blinkerAt;

        internal static bool InCarWithRoute()
        {
            try
            {
                var car = VehicleHelper.GetCurrentVehicleBase() as CarController;
                return car != null && car.controlledByPlayer && car.vehicleController != null && Navigator.HasDest;
            }
            catch { return false; }
        }

        internal static void Start()
        {
            try
            {
                if (Active) return;
                if (!ServerRules.Module("ModuleNavigation")) { ServerRules.ModuleOff(Loc.T("gps_nav_title", "Navigation")); return; }
                var car = VehicleHelper.GetCurrentVehicleBase() as CarController;
                if (car == null || !car.controlledByPlayer || car.vehicleController == null) { Navigator.Notice(Loc.T("gps_nav_autodrive_nocar", "Get into your car first.")); return; }
                if (!Navigator.HasDest) { Navigator.Notice(Loc.T("gps_nav_autodrive_nodest", "Set a destination first.")); return; }
                _car = car;
                _vc = car.vehicleController;
                _dest = Navigator.Dest;
                _destName = Navigator.DestName;
                _garage = Navigator.DestIsGarage;
                _garagePos = Navigator.Dest;
                _follow = Navigator.FollowPid.Length > 0;
                Navigator.EnsureRoadData();   // the lanes' speed limits
                try { _wheelBase = Mathf.Clamp(_vc.steering.CalculateWheelbase(), 1.5f, 6f); } catch { _wheelBase = 2.7f; }
                try { _maxSteer = Mathf.Clamp(_vc.steering.maximumSteerAngle, 10f, 50f); } catch { _maxSteer = 30f; }
                _halfLength = ParkingSpots.CarLength(car.gameObject) * 0.5f;
                _halfWidth = HalfWidth(car.gameObject);
                _route.Clear(); _along.Clear(); _lights.Clear(); _junctions.Clear(); _routeSig = 0; _passed = 0;
                _fast = false; _freeNext = 0f;
                _phase = Phase.Drive;
                _slowSince = _blockedSince = _stuckSince = _recoverUntil = -1f;
                _recoveries.Clear();
                _startedAt = Time.unscaledTime;
                _hook = OnInput;
                _vc.input.inputModifyCallback.AddListener(_hook);
                Active = true;
                Navigator.Notice(Loc.T("gps_nav_autodrive_on", "Driving to {0} by itself - steer, accelerate or brake to take over.", _destName));
                Plugin.Logger.LogInfo($"[AutoDrive] started to '{_destName}' (wheelbase {_wheelBase:F1} m, max steer {_maxSteer:F0}°, {_halfWidth * 2f:F1} m wide, {_halfLength * 2f:F1} m long) (AUTO-DRIVE-3).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[AutoDrive] start: {ex.Message}"); Stop(null); }
        }

        /// <summary>Ends it; <paramref name="notice"/> (null = quietly) tells the player why.</summary>
        internal static void Stop(string? notice)
        {
            bool was = Active;
            Active = false;
            try { if (_vc != null && _hook != null) _vc.input.inputModifyCallback.RemoveListener(_hook); } catch { }
            try { if (_vc != null) { _vc.input.Throttle = 0f; _vc.input.Handbrake = 0f; _vc.input.Brakes = 0f; } } catch { }
            SetBlinkers(_car, 0, force: true);
            _hook = null; _vc = null; _car = null;
            _route.Clear(); _along.Clear(); _lights.Clear(); _junctions.Clear();
            if (!was) return;
            if (!string.IsNullOrEmpty(notice)) Navigator.Notice(notice!);
            Plugin.Logger.LogInfo($"[AutoDrive] stopped{(string.IsNullOrEmpty(notice) ? "" : ": " + notice)}.");
        }

        /// <summary>Main thread, every frame (NavAutoWalk.Tick): the route copy, arrival, parking, giving up.</summary>
        internal static void Tick()
        {
            if (!Active) return;
            try
            {
                var car = VehicleHelper.GetCurrentVehicleBase() as CarController;
                if (car == null || car != _car || !car.controlledByPlayer || _vc == null) { Stop(null); return; }
                if (!ServerRules.Module("ModuleNavigation")) { Stop(null); return; }
                if (_phase == Phase.Parking) return;
                if (_phase == Phase.Entering)
                {
                    if (Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking) { Stop(Loc.T("gps_nav_autodrive_garage", "In the car park.")); return; }
                    if (Time.unscaledTime - _enterSince > 20f) Stop(Loc.T("gps_nav_autodrive_noentry", "Self-driving stopped: the car park entrance could not be reached."));
                    return;
                }
                if (_follow && !Navigator.HasDest) { Stop(null); return; }   // the friend left (the navigation said so)
                // the navigation's road route without its last point (the door, on the pavement)
                if (Navigator.HasDest && Navigator.RouteCar && Navigator.Route.Count >= 2)
                {
                    int sig = Navigator.Route.Count * 7919 + Navigator.Route[Navigator.Route.Count - 1].GetHashCode();
                    if (sig != _routeSig) CopyRoute(sig);
                }
                else if (!Navigator.HasDest && Navigator.Flat(Navigator.Me(), _dest) > 45f) { Stop(Loc.T("gps_nav_autodrive_cleared", "Self-driving off - the route was deleted.")); return; }
                if (_route.Count < 2) return;
                float speed = Speed();
                float remain = Remaining();
                if (_garage && remain < 6f) { _phase = Phase.Entering; _enterSince = Time.unscaledTime; return; }   // into the car park
                if (remain < 2.5f && Mathf.Abs(speed) < 0.6f)
                {
                    if (_follow) return;   // waits behind the friend until he drives on
                    BeginParking();
                    return;
                }
                // giving up: blocked for long (a queue, a parked car in the lane), or stuck after three back-offs
                if (Mathf.Abs(speed) < 0.3f && Time.unscaledTime - _startedAt > 4f && Time.unscaledTime > _recoverUntil)
                {
                    if (_slowSince < 0f) _slowSince = Time.unscaledTime;
                    if (_blockedNow) { if (_blockedSince < 0f) _blockedSince = Time.unscaledTime; }
                    else _blockedSince = -1f;
                    if (_blockedSince >= 0f && Time.unscaledTime - _blockedSince > 60f) { Stop(Loc.T("gps_nav_autodrive_blocked", "Self-driving stopped: the way stays blocked.")); return; }
                }
                else { _slowSince = -1f; _blockedSince = -1f; }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[AutoDrive] tick: {ex.Message}"); Stop(null); }
        }

        private static void CopyRoute(int sig)
        {
            _routeSig = sig;
            _route.Clear(); _along.Clear(); _lights.Clear(); _junctions.Clear();
            _freeNext = 0f;
            var r = Navigator.Route;
            int last = r.Count > 2 ? r.Count - 1 : r.Count;   // the final piece leads onto the pavement
            for (int i = 0; i < last; i++) _route.Add(r[i]);
            float acc = 0f;
            for (int i = 0; i < _route.Count; i++)
            {
                if (i > 0) acc += Navigator.Flat(_route[i - 1], _route[i]);
                _along.Add(acc);
            }
            _passed = 0;
            FindLights();
        }

        /// <summary>The stop lines of the light intersections on this route: every road's stop waypoints of every
        /// TrafficLightsIntersection, kept when one lies on the route (1.8 m) and its lane runs the route's way (so a
        /// crossing road's line inside a turn never counts). Without an intersection manager: the waypoints flagged as
        /// an intersection's entry, with their live stop flag.</summary>
        private static void FindLights()
        {
            try
            {
                var wps = GleyUrbanAssets.CurrentSceneData.GetSceneInstance()?.allWaypoints;
                if (wps == null) return;
                var all = GleyTrafficSystem.TrafficManager.Instance?.intersectionManager?.allIntersections;
                if (all != null)
                    foreach (var el in all)
                    {
                        if (!(el is GleyTrafficSystem.TrafficLightsIntersection ti) || ti.stopWaypoints == null) continue;
                        for (int road = 0; road < ti.stopWaypoints.Count; road++)
                        {
                            var list = ti.stopWaypoints[road]?.roadWaypoints;
                            if (list == null) continue;
                            foreach (int idx in list)
                                if (idx >= 0 && idx < wps.Length && wps[idx] != null && OnRoute(wps[idx], wps, out float at))
                                    _lights.Add(new StopLine { At = at, Ti = ti, Road = road, Wp = wps[idx] });
                        }
                    }
                bool byState = _lights.Count > 0;
                if (!byState)
                    foreach (var w in wps)
                        if (w != null && w.enter && OnRoute(w, wps, out float at)) _lights.Add(new StopLine { At = at, Wp = w });
                _lights.Sort((a, b) => a.At.CompareTo(b.At));
                for (int i = _lights.Count - 1; i > 0; i--)   // several lanes' points of one line
                    if (_lights[i].At - _lights[i - 1].At < 2f && _lights[i].Ti == _lights[i - 1].Ti) _lights.RemoveAt(i);
                // AUTO-DRIVE-4: the junctions without lights (give way) end a free stretch as well
                if (all != null)
                    foreach (var el in all)
                    {
                        if (el == null || el is GleyTrafficSystem.TrafficLightsIntersection) continue;
                        var roads = el.GetWaypoints();
                        if (roads == null) continue;
                        foreach (var road in roads)
                        {
                            var list = road?.roadWaypoints;
                            if (list == null) continue;
                            foreach (int idx in list)
                                if (idx >= 0 && idx < wps.Length && wps[idx] != null && OnRoute(wps[idx], wps, out float at)) _junctions.Add(at);
                        }
                    }
                _junctions.Sort();
                Plugin.Logger.LogInfo($"[AutoDrive] {_lights.Count} traffic light(s) and {_junctions.Count} give-way point(s) on the route ({(byState ? "intersection colours" : "stop flags")}).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[AutoDrive] traffic lights on the route: {ex.Message}"); }
        }

        private static bool OnRoute(GleyTrafficSystem.Waypoint w, GleyTrafficSystem.Waypoint[] wps, out float at)
        {
            at = 0f;
            Vector3 p = w.position;
            Vector3 lane = Vector3.zero;
            try
            {
                if (w.neighbors != null && w.neighbors.Count > 0 && w.neighbors[0] >= 0 && w.neighbors[0] < wps.Length && wps[w.neighbors[0]] != null)
                    lane = wps[w.neighbors[0]].position - p;
            }
            catch { }
            lane.y = 0f;
            for (int i = 0; i + 1 < _route.Count; i++)
            {
                if (Navigator.SegDistPublic(p, _route[i], _route[i + 1]) > 1.8f) continue;
                Vector3 seg = _route[i + 1] - _route[i];
                seg.y = 0f;
                if (lane.sqrMagnitude > 0.01f && seg.sqrMagnitude > 0.01f && Vector3.Dot(lane.normalized, seg.normalized) < 0.6f) continue;
                at = _along[i] + Navigator.Flat(_route[i], p);
                return true;
            }
            return false;
        }

        /// <summary>Red or yellow for this stop line: the intersection's colour of its road (Gley stops on anything but
        /// green, TrafficLightsIntersection.ApplyColorChanges), else the waypoint's live stop flag.</summary>
        private static bool IsRed(StopLine l)
        {
            try
            {
                var st = l.Ti?.intersectionState;
                if (st != null && l.Road >= 0 && l.Road < st.Length) return st[l.Road] != GleyTrafficSystem.TrafficLightsColor.Green;
                return l.Wp != null && l.Wp.stop;
            }
            catch { return false; }
        }

        private static float Speed()
        {
            try { return _vc != null && _car != null ? Vector3.Dot(_vc.vehicleRigidbody.velocity, _car.transform.forward) : 0f; } catch { return 0f; }
        }

        /// <summary>Where the car is along the route (metres) - the nearest piece around the last one.</summary>
        private static float Along(Vector3 me)
        {
            int best = _passed; float bd = float.MaxValue;
            for (int i = _passed; i < Math.Min(_route.Count - 1, _passed + 30); i++)
            {
                float d = Navigator.SegDistPublic(me, _route[i], _route[i + 1]);
                if (d < bd) { bd = d; best = i; }
            }
            _passed = best;
            Vector3 a = _route[best], b = _route[best + 1];
            a.y = b.y = me.y = 0f;
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(me - a, ab) / ab.sqrMagnitude) : 0f;
            return _along[best] + ab.magnitude * t;
        }

        private static float Remaining()
        {
            if (_car == null || _route.Count < 2) return 0f;
            return _along[_along.Count - 1] - Along(_car.transform.position);
        }

        private static Vector3 PointAt(float s)
        {
            if (s <= 0f) return _route[0];
            for (int i = 1; i < _route.Count; i++)
                if (_along[i] >= s)
                {
                    float seg = _along[i] - _along[i - 1];
                    return seg > 0.001f ? Vector3.Lerp(_route[i - 1], _route[i], (s - _along[i - 1]) / seg) : _route[i];
                }
            return _route[_route.Count - 1];
        }

        private static Vector3 HeadingAt(float s)
        {
            Vector3 d = PointAt(s + 1.5f) - PointAt(s - 1.5f);
            d.y = 0f;
            return d;
        }

        /// <summary>How much the route turns between here and <paramref name="d"/> metres on (degrees).</summary>
        private static float TurnAhead(float s, float d)
        {
            Vector3 a = HeadingAt(s), b = HeadingAt(s + d);
            return a.sqrMagnitude > 0.01f && b.sqrMagnitude > 0.01f ? Vector3.Angle(a, b) : 0f;
        }

        /// <summary>The fastest speed from which every curve of the next 45 m (faster: the braking distance and 10 m) can still
        /// be taken at LatAccel.</summary>
        private static float CurveLimit(float s, float cruise)
        {
            float vt = cruise;
            float horizon = Mathf.Max(45f, (cruise * cruise - CurveMin * CurveMin) / (2f * Decel) + 10f);   // AUTO-DRIVE-4: from 60 km/h
            for (float d = 0f; d <= horizon; d += 3f)
            {
                Vector3 a = PointAt(s + d) - PointAt(s + d - 3f), b = PointAt(s + d + 3f) - PointAt(s + d);
                a.y = b.y = 0f;
                if (a.sqrMagnitude < 0.25f || b.sqrMagnitude < 0.25f) continue;
                float k = Vector3.Angle(a, b) * Mathf.Deg2Rad / 3f;     // curvature, 1/m
                if (k < 0.004f) continue;
                float vCurve = Mathf.Max(CurveMin, Mathf.Sqrt(LatAccel / k));
                vt = Mathf.Min(vt, Mathf.Sqrt(vCurve * vCurve + 2f * Decel * d));
            }
            return vt;
        }

        /// <summary>The steering input that gives this wheel angle: NWH narrows the angle with speed
        /// (speedSensitiveSteeringCurve over Speed/50) and maps the input through its linearity curve.</summary>
        private static float SteerInput(float deg)
        {
            float factor = 1f;
            try { factor = Mathf.Clamp(_vc!.steering.speedSensitiveSteeringCurve.Evaluate(_vc.Speed / 50f), 0.1f, 1f); } catch { }
            float r = Mathf.Clamp01(Mathf.Abs(deg) / Mathf.Max(1f, _maxSteer * factor));
            float x = r;
            try
            {
                var lin = _vc!.steering.linearity;
                if (lin != null && lin.length >= 2)
                {
                    float lo = 0f, hi = 1f;
                    for (int i = 0; i < 12; i++) { float mid = (lo + hi) * 0.5f; if (lin.Evaluate(mid) < r) lo = mid; else hi = mid; }
                    x = hi;
                }
            }
            catch { }
            return Mathf.Sign(deg) * Mathf.Clamp01(x);
        }

        /// <summary>NWH inputModifyCallback: the player's input of this frame was just read - his own steering, throttle or
        /// brake takes over; otherwise the car's controls are set here.</summary>
        private static void OnInput()
        {
            if (!Active || _vc == null || _car == null) return;
            var input = _vc.input;
            try
            {
                if (Mathf.Abs(input.Steering) > TakeOverSteer || input.Throttle > TakeOver || input.Brakes > TakeOver || input.Handbrake > 0.5f)
                {
                    Stop(Loc.T("gps_nav_autodrive_off", "Self-driving off - you drive."));
                    return;
                }
                if (Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking) { Hold(input); return; }   // the car park takes over
                if (_phase == Phase.Entering) { Enter(input); return; }
                if (_phase == Phase.Parking || _route.Count < 2) { Hold(input); return; }
                if (Time.unscaledTime < _recoverUntil) { BackOff(input); return; }

                var t = _car.transform;
                float v = Speed();
                float s = Along(t.position);
                float total = _along[_along.Count - 1];
                float remain = total - s;

                // steering: pure pursuit from the rear axle; short in turns, longer on a straight
                float sharp = TurnAhead(s, 12f);
                float look = Mathf.Lerp(Mathf.Clamp(3f + 0.6f * Mathf.Max(v, 0f), 4f, 15f), 3f, Mathf.InverseLerp(15f, 60f, sharp));   // AUTO-DRIVE-4: up to 15 m (only above ~54 km/h)
                float rearS = s - _wheelBase * 0.5f;
                Vector3 local = t.InverseTransformPoint(PointAt(rearS + look));
                local.z += _wheelBase * 0.5f;                       // seen from the rear axle
                float ld2 = Mathf.Max(local.x * local.x + local.z * local.z, 1f);
                float steerDeg = Mathf.Atan(2f * _wheelBase * local.x / ld2) * Mathf.Rad2Deg;
                if (local.z < 0.5f) steerDeg = local.x >= 0f ? _maxSteer : -_maxSteer;   // the point is beside or behind: full lock
                input.Steering = SteerInput(steerDeg);
                _lastSteer = input.Steering;
                Blinkers(s);

                // speed: the lane's limit (else ~40 km/h, on bridges and free straights up to 60 - AUTO-DRIVE-4; never over 60),
                // the curves ahead, the end of the route (a friend being followed: 10 m behind them), cars and people, kerbside
                // objects, red lights
                float lim = Navigator.LimitAt(PointAt(s + 6f));
                float cruise = lim >= 10f ? Mathf.Min(lim, 60f) / 3.6f : FreeCruise(s, total);
                float vt = CurveLimit(s, cruise);
                if (!_garage) vt = Mathf.Min(vt, Mathf.Sqrt(2f * Decel * Mathf.Max(0f, remain - (_follow ? 10f : 1.5f))));
                Vector3 dir = PointAt(s + _halfLength + 6f) - t.position;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 0.5f ? dir.normalized : t.forward;
                float obstacle = Obstacle(t, v, dir);
                _blockedNow = obstacle < float.MaxValue && obstacle < StopShort + 3f;
                if (obstacle < float.MaxValue)
                {
                    float gap = Mathf.Max(0f, obstacle - StopShort);
                    float vo = gap * 0.9f;
                    if (cruise > CruiseSpeed + 0.5f || v > CruiseSpeed + 0.5f) vo = Mathf.Min(vo, Mathf.Sqrt(2f * ObstacleDecel * gap));   // AUTO-DRIVE-4: braked for in time from 60 km/h
                    vt = Mathf.Min(vt, vo);
                }
                float kerb = StaticObstacle(t, v, dir);
                _staticNow = kerb < float.MaxValue && kerb < StaticShort + 1.2f;
                if (kerb < float.MaxValue) vt = Mathf.Min(vt, Mathf.Max(0f, (kerb - StaticShort) * 0.8f));
                vt = Mathf.Min(vt, RedLightLimit(s + _halfLength, v));

                // throttle and brakes; standing: the handbrake (holding the brake would make the automatic gearbox reverse)
                try { if (_vc.powertrain.transmission.Gear < 0) _vc.powertrain.transmission.ShiftInto(1); } catch { }
                if (vt < 0.4f && v < 0.8f)
                {
                    Hold(input);
                    WatchStuck(v, wanted: _staticNow);   // stopped by a hydrant or a post: back off
                    return;
                }
                input.Handbrake = 0f;
                float err = vt - v;
                if (err > 0f) { input.Throttle = Mathf.Clamp(err * 0.35f + 0.15f, 0f, 0.8f); input.Brakes = 0f; _pushing = input.Throttle > 0.3f; }
                else if (err < -0.6f) { input.Throttle = 0f; input.Brakes = Mathf.Clamp(-err * 0.3f, 0.05f, 1f); _pushing = false; }
                else { input.Throttle = 0f; input.Brakes = 0f; _pushing = false; }
                WatchStuck(v, wanted: _pushing && !_blockedNow);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[AutoDrive] controls: {ex.Message}"); Stop(null); }
        }

        /// <summary>AUTO-DRIVE-4 (user 2026-11-06, two screenshots of a bridge crossed at 41 km/h, and the choice "Schneller auf
        /// freier Strecke: auf Brücken und langen Geraden ohne Tempolimit bis etwa 60 km/h statt 40"): where the lane names
        /// no limit the car cruises at ~40 km/h; on a bridge (the game's skip-bridge zone), or with FreeLength of free road
        /// ahead, it goes up to 60 km/h and comes back to ~40 km/h smoothly (FreeDecel) by 20 m before the next traffic
        /// light, give-way junction or sharp turn. Curves, cars, people and red lights still limit it as before.</summary>
        private static float FreeCruise(float s, float total)
        {
            float now = Time.unscaledTime;
            if (now >= _freeNext) { _freeAhead = FreeAhead(s, total); _freeS = s; _freeNext = now + 0.25f; }
            float free = Mathf.Max(0f, _freeAhead - (s - _freeS));
            bool bridge = false;
            try { bridge = Vehicles.SkipBridgeHelper.skipBridgeEnabled; } catch { }
            if (!_fast && (bridge || free >= FreeLength))
            {
                _fast = true;
                Plugin.Logger.LogInfo($"[AutoDrive] {(bridge ? "on a bridge" : "free road")} - {free:F0} m without a light, junction or turn: up to {(bridge ? BridgeSpeed : FreeSpeed) * 3.6f:F0} km/h ({(bridge ? "AUTO-DRIVE-5" : "AUTO-DRIVE-4")}).");
            }
            if (!_fast) return CruiseSpeed;
            float cap = Mathf.Sqrt(CruiseSpeed * CruiseSpeed + 2f * FreeDecel * Mathf.Max(0f, free - 20f));
            if (cap <= CruiseSpeed + 0.05f && !bridge) { _fast = false; return CruiseSpeed; }
            return Mathf.Clamp(cap, CruiseSpeed, bridge ? BridgeSpeed : FreeSpeed);   // AUTO-DRIVE-5: faster on a bridge
        }

        /// <summary>AUTO-DRIVE-4: metres of free road ahead - to the next traffic light, give-way junction or sharp turn
        /// (over 30 degrees within 15 m), at most 200 m and never past the route's end.</summary>
        private static float FreeAhead(float s, float total)
        {
            float free = Mathf.Clamp(total - s, 0f, 200f);
            foreach (var l in _lights) { float a = l.At - s; if (a < -0.8f) continue; free = Mathf.Min(free, Mathf.Max(0f, a)); break; }
            foreach (float j in _junctions) { float a = j - s; if (a < -0.8f) continue; free = Mathf.Min(free, Mathf.Max(0f, a)); break; }
            for (float d = 5f; d < free; d += 10f)
                if (TurnAhead(s + d, 15f) > 30f) { free = d; break; }
            return free;
        }

        /// <summary>The speed the next red light allows: the front bumper stops a metre before its line and waits for
        /// green. A light that turns red so close that stopping would need over 4.5 m/s² is driven through (the yellow
        /// phase is for that) - decided once, when it changes.</summary>
        private static float RedLightLimit(float front, float v)
        {
            foreach (var l in _lights)
            {
                float ahead = l.At - front;
                if (ahead < -0.8f) continue;     // the bumper is over the line: in the crossing
                if (ahead > 70f) break;
                if (!IsRed(l)) { l.WasRed = false; l.DriveOn = false; return float.MaxValue; }
                if (!l.WasRed)
                {
                    l.WasRed = true;
                    float need = v > 0.5f ? v * v / (2f * Mathf.Max(ahead - 1f, 0.1f)) : 0f;
                    l.DriveOn = need > 4.5f;
                    Plugin.Logger.LogInfo(l.DriveOn
                        ? $"[AutoDrive] the light turned red {ahead:F0} m ahead at {v * 3.6f:F0} km/h - too close to stop, drives on."
                        : $"[AutoDrive] red light {ahead:F0} m ahead - stopping at the line.");
                }
                if (l.DriveOn) return float.MaxValue;
                return Mathf.Sqrt(2f * LightDecel * Mathf.Max(0f, ahead - 1f));
            }
            return float.MaxValue;
        }

        /// <summary>Pushing without moving (or held by a kerbside object) for 1.5 s: back off along the arc it came, for
        /// 1.6 s, then drive again. Three times a minute at most - then the self-driving gives up.</summary>
        private static void WatchStuck(float v, bool wanted)
        {
            float now = Time.unscaledTime;
            if (!wanted || Mathf.Abs(v) > 0.4f || now - _startedAt < 3f) { _stuckSince = -1f; return; }
            if (_stuckSince < 0f) { _stuckSince = now; return; }
            if (now - _stuckSince < 1.5f) return;
            _stuckSince = -1f;
            _recoveries.RemoveAll(x => now - x > 60f);
            if (_recoveries.Count >= 3) { Stop(Loc.T("gps_nav_autodrive_stuck", "Self-driving stopped: the car is stuck.")); return; }
            _recoveries.Add(now);
            _recoverUntil = now + 1.6f;
            _recoverSteer = Mathf.Clamp(_lastSteer * 0.6f, -1f, 1f);
            Plugin.Logger.LogInfo($"[AutoDrive] stuck ({(_staticNow ? "kerbside object" : "no progress")}) - backing off ({_recoveries.Count}/3).");
        }

        /// <summary>Backing off: NWH's automatic gearbox reverses on the brake input when standing, and in reverse the
        /// brake input drives.</summary>
        private static void BackOff(NWH.VehiclePhysics2.Input.VehicleInputHandler input)
        {
            input.Handbrake = 0f;
            input.Throttle = 0f;
            input.Brakes = 0.45f;
            input.Steering = _recoverSteer;
            _pushing = false;
        }

        /// <summary>The blinker of the turn ahead: on about 30 m before a turn of more than 30 degrees, and while turning; off
        /// once the road runs straight again (Vector3.SignedAngle around up: + = right).</summary>
        private static void Blinkers(float s)
        {
            Vector3 now = PointAt(s + 2f) - PointAt(s);
            Vector3 ahead = PointAt(s + 32f) - PointAt(s + 28f);
            Vector3 back = PointAt(s - 2f) - PointAt(s - 8f);
            Vector3 front = PointAt(s + 8f) - PointAt(s + 2f);
            now.y = ahead.y = back.y = front.y = 0f;
            float soon = now.sqrMagnitude > 0.01f && ahead.sqrMagnitude > 0.01f ? Vector3.SignedAngle(now, ahead, Vector3.up) : 0f;
            float turning = back.sqrMagnitude > 0.01f && front.sqrMagnitude > 0.01f ? Vector3.SignedAngle(back, front, Vector3.up) : 0f;
            float a = Mathf.Abs(turning) > 18f ? turning : soon;
            int want = a > 30f || (Mathf.Abs(turning) > 18f && turning > 0f) ? 1 : a < -30f || (Mathf.Abs(turning) > 18f && turning < 0f) ? -1 : 0;
            SetBlinkers(_car, want, force: false);
        }

        /// <summary>-1 left, 1 right, 0 off - the game's own blinkers (CarController.blinkers, what the blinker keys toggle).</summary>
        private static void SetBlinkers(CarController? car, int want, bool force)
        {
            try
            {
                if (car == null || (!force && Time.unscaledTime < _blinkerAt)) return;   // one toggle at a time
                var b = car.blinkers;
                if (b == null) return;
                bool left = b._isLeftBlinkerOn, right = b._isRightBlinkerOn;
                if (want < 0 && !left) b.ToggleLeftBlinker();
                else if (want > 0 && !right) b.ToggleRightBlinker();
                else if (want == 0 && (left || right)) { if (left) b.ToggleLeftBlinker(); if (b._isRightBlinkerOn) b.ToggleRightBlinker(); }
                else return;
                _blinkerAt = Time.unscaledTime + 0.3f;
            }
            catch { }
        }

        /// <summary>The last metres into a car park: slowly straight at its entrance (its trigger takes the car in).</summary>
        private static void Enter(NWH.VehiclePhysics2.Input.VehicleInputHandler input)
        {
            if (_car == null || _vc == null) return;
            var t = _car.transform;
            float v = Speed();
            Vector3 local = t.InverseTransformPoint(_garagePos);
            local.z += _wheelBase * 0.5f;
            float ld2 = Mathf.Max(local.x * local.x + local.z * local.z, 1f);
            float steerDeg = Mathf.Atan(2f * _wheelBase * local.x / ld2) * Mathf.Rad2Deg;
            if (local.z < 0.5f) steerDeg = local.x >= 0f ? _maxSteer : -_maxSteer;
            input.Steering = SteerInput(steerDeg);
            float vt = 2.5f;
            float obstacle = Obstacle(t, v, t.forward);
            if (obstacle < float.MaxValue) vt = Mathf.Min(vt, Mathf.Max(0f, (obstacle - StopShort) * 0.9f));
            try { if (_vc.powertrain.transmission.Gear < 0) _vc.powertrain.transmission.ShiftInto(1); } catch { }
            if (vt < 0.4f && v < 0.8f) { Hold(input); return; }
            input.Handbrake = 0f;
            float err = vt - v;
            if (err > 0f) { input.Throttle = Mathf.Clamp(err * 0.35f + 0.15f, 0f, 0.6f); input.Brakes = 0f; }
            else { input.Throttle = 0f; input.Brakes = err < -0.6f ? Mathf.Clamp(-err * 0.3f, 0.05f, 1f) : 0f; }
        }

        private static void Hold(NWH.VehiclePhysics2.Input.VehicleInputHandler input)
        {
            input.Throttle = 0f;
            input.Brakes = 0f;
            input.Handbrake = 1f;
            input.Steering = 0f;
            _pushing = false;
        }

        /// <summary>Distance to the nearest car, pedestrian or player ahead - a sphere cast from the bonnet along the
        /// route's direction (in a turn the car's own forward looks into the kerb); MaxValue = free.</summary>
        private static float Obstacle(Transform t, float v, Vector3 dir)
        {
            try
            {
                if (_mask == -1)
                {
                    int m = 0;
                    foreach (int l in new[] { LayerHelper.AiVehiclesLayerIndex, LayerHelper.VehiclesLayerIndex, LayerHelper.PlayerVehiclesLayerIndex, LayerHelper.HumanLayerIndex, LayerHelper.PlayerLayerIndex })
                        if (l >= 0 && l < 32) m |= 1 << l;
                    _mask = m;
                }
                float vp = Mathf.Max(v, 0f);
                float dist = 5f + vp * 1.8f;
                if (_fast && vp > CruiseSpeed + 0.5f) dist = Mathf.Max(dist, vp * vp / (2f * ObstacleDecel) + 8f);   // AUTO-DRIVE-4: the braking distance from 60 km/h (free stretch only: a long straight cast in a city curve would see the next lane)
                Vector3 origin = t.position + Vector3.up * 0.9f + t.forward * Mathf.Max(0.5f, _halfLength - 1f);
                int n = Physics.SphereCastNonAlloc(origin, 0.9f, dir, _hits, dist, _mask, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    var c = _hits[i].collider;
                    if (c == null || c.transform.IsChildOf(t)) continue;   // our own car (and its passengers)
                    if (_hits[i].distance <= 0f) continue;                   // touching at the start: overlap, not ahead
                    if (_hits[i].distance < best) best = _hits[i].distance;
                }
                return best;
            }
            catch { return float.MaxValue; }
        }

        /// <summary>AUTO-DRIVE-2: distance to a hydrant, post, tree or wall right in the way - a flat box as wide as the car
        /// at bumper height (above the kerb) along the route's direction, everything but the moving layers; floors and
        /// ramps (an upward face) do not count. MaxValue = free.</summary>
        private static float StaticObstacle(Transform t, float v, Vector3 dir)
        {
            try
            {
                if (_staticMask == -1)
                {
                    int m = Physics.DefaultRaycastLayers;
                    foreach (int l in new[] { LayerHelper.AiVehiclesLayerIndex, LayerHelper.VehiclesLayerIndex, LayerHelper.PlayerVehiclesLayerIndex, LayerHelper.HumanLayerIndex, LayerHelper.PlayerLayerIndex })
                        if (l >= 0 && l < 32) m &= ~(1 << l);
                    _staticMask = m;
                }
                float dist = 1.5f + Mathf.Max(v, 0f) * 0.9f;
                Vector3 origin = t.position + Vector3.up * 0.55f + t.forward * Mathf.Max(0.3f, _halfLength - 0.4f);
                int n = Physics.BoxCastNonAlloc(origin, new Vector3(_halfWidth * 0.95f, 0.22f, 0.05f), dir, _hits, Quaternion.LookRotation(dir), dist, _staticMask, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    var h = _hits[i];
                    if (h.collider == null || h.collider.transform.IsChildOf(t) || h.distance <= 0f) continue;
                    if (h.normal.y > 0.65f) continue;                        // ground, a ramp
                    if (h.rigidbody != null && !h.rigidbody.isKinematic) continue;   // something that moves: the other cast's job
                    if (h.distance < best) best = h.distance;
                }
                return best;
            }
            catch { return float.MaxValue; }
        }

        /// <summary>Half the car's width across its own right axis. AUTO-DRIVE-3 (user 2026-11-06 with a Player.log: "Navi hat
        /// Probleme noch an verschiedenen Stellen" - the AF90 read "2.8 m wide" on most starts and "2.2 m" on others, and the
        /// drives ended in "stuck (kerbside object)"): Renderer.bounds is a box along the WORLD axes - a car turned by 45° is
        /// about as wide in it as it is long, the 1.4 m clamp made that 2.8 m, and the kerbside cast swept the posts and
        /// hydrants beside the lane. Now the game's body collider (VehicleHelper.TryGetBodyColliderBounds) plus the
        /// mirrors, else the meshes' own boxes, both measured along the car's right axis.</summary>
        private static float HalfWidth(GameObject car)
        {
            var root = car.transform;
            float min = float.MaxValue, max = float.MinValue;
            try
            {
                if (VehicleHelper.TryGetBodyColliderBounds(root, out var b, out var col) && col != null)
                {
                    Across(root, col.transform, b, ref min, ref max);
                    if (max - min > 1f) return Mathf.Clamp((max - min) * 0.5f + 0.08f, 0.6f, 1.4f);
                }
            }
            catch { }
            try
            {
                min = float.MaxValue; max = float.MinValue;
                foreach (var mf in car.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = mf != null ? mf.sharedMesh : null;
                    var mr = mf != null ? mf.GetComponent<MeshRenderer>() : null;
                    if (mesh == null || mr == null || !mr.enabled) continue;
                    Across(root, mf.transform, mesh.bounds, ref min, ref max);
                }
                return min < max ? Mathf.Clamp((max - min) * 0.5f, 0.6f, 1.4f) : 0.9f;
            }
            catch { return 0.9f; }
        }

        /// <summary>The corners of a local box (of <paramref name="t"/>) along the root's right axis, in metres.</summary>
        private static void Across(Transform root, Transform t, Bounds b, ref float min, ref float max)
        {
            Vector3 o = root.position, right = root.right;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                float x = Vector3.Dot(t.TransformPoint(c) - o, right);
                if (x < min) min = x;
                if (x > max) max = x;
            }
        }

        // ── parking at the destination ──

        private static void BeginParking()
        {
            if (_car == null) { Stop(null); return; }
            // NAV-CHOICE-1: "park automatically" off (MPConfig.AutoPark) - it stops at the destination, the player parks
            if (!MPConfig.AutoPark) { Stop(Loc.T("gps_nav_autodrive_here", "Arrived at {0}.", _destName)); return; }
            _phase = Phase.Parking;
            SetBlinkers(_car, 0, force: true);
            var car = _car;
            string name = _destName;
            try
            {
                var helper = car.GetComponentInChildren<VehicleParkingHelper>(true);
                bool supported = car.vehicleType == null || car.vehicleType.autoParkSupported;
                if (helper == null || !supported) { Stop(Loc.T("gps_nav_autodrive_arrived", "Arrived at {0} - no free parking spot nearby.", name)); return; }
                Vector3 here = car.transform.position, door = _dest;
                // AUTOPARK-3 (user 2026-11-06, a screenshot 70 m before the destination: "parkt zu weit vom Ziel weg"): the
                // road route ends where driving on would cost more than walking (NAV-SHORT-1), up to 35 m farther from the
                // door than the nearest lane - and the spot was looked for around THAT point. Now the free spot nearest
                // to the DOOR (within 60 m of it and 110 m of the car - the game's fade carries the car there, as its own
                // automatic parking does); only without one the spot nearest to the car, as before.
                var blocked = new List<Vector3>();
                bool haveNear = false; Vector3 nearPos = default; Quaternion nearRot = Quaternion.identity;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    if (!ParkingSpots.TryFind(door, _halfLength * 2f, car.gameObject, c => blocked.Exists(b => Navigator.Flat(b, c) < 1f) || Navigator.Flat(c, here) > 110f, out var pos, out var rot)) break;
                    if (Navigator.Flat(pos, door) > 60f) break;
                    if (SpotClear(car, pos, rot, out var what)) { haveNear = true; nearPos = pos; nearRot = rot; break; }
                    Plugin.Logger.LogInfo($"[AutoDrive] parking spot at ({pos.x:F0}, {pos.z:F0}) by the door blocked by {what} - the next one (AUTOPARK-3).");
                    blocked.Add(pos);
                }
                // the game's own spot (its automatic parking, the fade included) when it is at least as close to the door
                if (helper.availableAutoParkSpot != null)
                {
                    var spot = helper.availableAutoParkSpot;
                    Vector3 sp = spot.transform.position;
                    Quaternion sr = spot.transform.parent != null ? spot.transform.parent.rotation : spot.transform.rotation;   // as the game places the car
                    if (!haveNear || Navigator.Flat(sp, door) <= Navigator.Flat(nearPos, door) + 4f)
                    {
                        if (SpotClear(car, sp, sr, out var what))
                        {
                            Plugin.Logger.LogInfo($"[AutoDrive] parks in the game's spot {Navigator.Flat(sp, door):F0} m from the door (AUTOPARK-3).");
                            helper.StartCoroutine(GameParkThenStop(helper, name));
                            return;
                        }
                        Plugin.Logger.LogInfo($"[AutoDrive] the game's parking spot at ({sp.x:F0}, {sp.z:F0}) is blocked by {what} - looking for another (AUTOPARK-2).");
                    }
                }
                if (haveNear)
                {
                    Plugin.Logger.LogInfo($"[AutoDrive] parks {Navigator.Flat(nearPos, door):F0} m from the door, {Navigator.Flat(nearPos, here):F0} m from where the route ended (AUTOPARK-3).");
                    helper.StartCoroutine(ParkThenStop(car, nearPos, nearRot, name));
                    return;
                }
                blocked.Clear();
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    if (!ParkingSpots.TryFind(here, _halfLength * 2f, car.gameObject, c => blocked.Exists(b => Navigator.Flat(b, c) < 1f), out var pos, out var rot)) break;
                    if (Navigator.Flat(pos, here) >= 35f) break;   // farther is not "at the destination" any more
                    if (SpotClear(car, pos, rot, out var what))
                    {
                        Plugin.Logger.LogInfo($"[AutoDrive] no free spot by the door - parks {Navigator.Flat(pos, door):F0} m from it (AUTOPARK-3).");
                        helper.StartCoroutine(ParkThenStop(car, pos, rot, name));
                        return;
                    }
                    Plugin.Logger.LogInfo($"[AutoDrive] parking spot at ({pos.x:F0}, {pos.z:F0}) blocked by {what} - the next one (AUTOPARK-2).");
                    blocked.Add(pos);
                }
                Stop(Loc.T("gps_nav_autodrive_arrived", "Arrived at {0} - no free parking spot nearby.", name));
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[AutoDrive] parking: {ex.Message}"); Stop(null); }
        }

        /// <summary>AUTOPARK-2 (user 2026-11-01: "driving to the harbour the car was put somewhere else and stuck in the hydrant,
        /// between a car, the hydrant and a mailbox"): the game's free-spot test (VehicleParkingHelper.IsSpotOccupied,
        /// freeParkingSpotDetectionMask) sees vehicles and people only - a spot next to a hydrant, a mailbox or a parked car
        /// passed, and the car was teleported into them - and the mod's own lane slot (ParkingSpots, the same layers) put the
        /// car ~50 m away onto a parked car's place (the ParkedVehicles layer is not among them; field log: "player-vehicle
        /// AnselmoAF90 X parked-ghost bima320", then 17 hits). Before the self-driving parks, the car's own footprint at the spot
        /// (0.35-1.85 m above the ground - clear of the kerb -, a little wider and longer than the car) must hold no
        /// vehicle of any kind, no person, no street prop and no building; else the next spot (within 35 m), else it stays on
        /// the road.</summary>
        private static readonly int ParkClearMask = LayerMask.GetMask("PlayerVehicles", "Vehicles", "AiVehicles", "ParkedVehicles", "Human", "StreetProps", "Buildings", "BuildingWalls", "Default");
        private static readonly Collider[] _parkHits = new Collider[32];

        private static bool SpotClear(CarController car, Vector3 pos, Quaternion rot, out string what)
        {
            what = "";
            try
            {
                var half = new Vector3(_halfWidth + 0.15f, 0.75f, _halfLength + 0.25f);
                int n = Physics.OverlapBoxNonAlloc(pos + rot * Vector3.up * 1.1f, half, _parkHits, rot, ParkClearMask, QueryTriggerInteraction.Ignore);
                if (n >= _parkHits.Length) { what = "too many things"; return false; }
                for (int i = 0; i < n; i++)
                {
                    var c = _parkHits[i];
                    if (c == null || c.transform.IsChildOf(car.transform)) continue;
                    if (c.bounds.size.x > 40f || c.bounds.size.z > 40f) continue;   // a block's big static mesh, not a thing in the spot
                    if (c.GetComponentInParent<PlayerController>() != null) continue;
                    what = $"'{c.name}' ({LayerMask.LayerToName(c.gameObject.layer)})";
                    return false;
                }
                return SpotLevel(car, pos, rot, out what);
            }
            catch (Exception ex) { what = ex.Message; return false; }
        }

        /// <summary>AUTOPARK-4 (user 2026-11-08 "Auto steckt aktuell fest", the Player.log: every spot by the door taken, the car
        /// put 54 m away at the edge of a bridge ramp - "the game reads the wheels off its parking lane" - where it tipped 30
        /// degrees and stuck): the ground under the car's four corners must be there and level within 25 cm, or the spot is
        /// not taken. Cars and people under a corner do not count as ground.</summary>
        private static readonly RaycastHit[] _groundHits = new RaycastHit[8];

        private static bool SpotLevel(CarController car, Vector3 pos, Quaternion rot, out string what)
        {
            what = "";
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var corner = pos + rot * new Vector3((i & 1) == 0 ? -_halfWidth : _halfWidth, 0f, (i & 2) == 0 ? -_halfLength : _halfLength);
                int n = Physics.RaycastNonAlloc(corner + Vector3.up * 3f, Vector3.down, _groundHits, 6f, ~0, QueryTriggerInteraction.Ignore);
                float top = float.MinValue;
                for (int k = 0; k < n; k++)
                {
                    var h = _groundHits[k];
                    if (h.collider == null || h.collider.attachedRigidbody != null || h.collider.transform.IsChildOf(car.transform)) continue;
                    if (h.point.y > top && h.point.y < pos.y + 1.5f) top = h.point.y;
                }
                if (top == float.MinValue) { what = "no ground under a corner"; return false; }
                if (top < lo) lo = top;
                if (top > hi) hi = top;
            }
            if (hi - lo > 0.25f) { what = $"uneven ground ({hi - lo:F2} m between the corners)"; return false; }
            return true;
        }

        private static IEnumerator GameParkThenStop(VehicleParkingHelper helper, string name)
        {
            yield return helper.ParkInClosestSpot();
            Stop(Loc.T("gps_nav_autodrive_parked", "Parked at {0}.", name));
        }

        private static IEnumerator ParkThenStop(CarController car, Vector3 pos, Quaternion rot, string name)
        {
            bool parked = true;
            yield return UI.UiFader.Fade();
            try
            {
                if (car != null && car.controlledByPlayer)
                {
                    Vector3 backPos = car.transform.position; Quaternion backRot = car.transform.rotation;   // AUTOPARK-4: where the route ended
                    var backState = car.vehicleInstance != null ? car.vehicleInstance.parkingState : ParkingState.Legal;
                    VehicleHelper.TeleportVehicleToGround(car, pos, rot);
                    if (car.vehicleInstance != null) car.vehicleInstance.parkingState = ParkingState.Legal;
                    try { car.UpdateParkingZone(); } catch { }   // the HUD's parking sign for the car the player sits in
                    try
                    {
                        var info = InstanceBehavior<UI.UIs>.Instance.playerHUD.itemPanelUI.vehicleInfo;
                        if (info != null && info.currentParkingState == ParkingState.Illegal)
                        {
                            // AUTOPARK-4: the game reads the wheels off its parking lane - such a spot is no spot (the field case
                            // tipped at a bridge ramp and stuck). Back onto the road where the route ended; the player parks.
                            Plugin.Logger.LogWarning($"[AutoDrive] the spot at ({pos.x:F0}, {pos.z:F0}) is off the parking lane - the car goes back onto the road, the player parks (AUTOPARK-4).");
                            VehicleHelper.TeleportVehicleToGround(car, backPos, backRot);
                            if (car.vehicleInstance != null) car.vehicleInstance.parkingState = backState;
                            try { car.UpdateParkingZone(); } catch { }
                            parked = false;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[AutoDrive] park: {ex.Message}"); }
            yield return UI.UiFader.UnFade();
            Stop(parked ? Loc.T("gps_nav_autodrive_parked", "Parked at {0}.", name)
                        : Loc.T("gps_nav_autodrive_arrived", "Arrived at {0} - no free parking spot nearby.", name));
        }
    }
}
