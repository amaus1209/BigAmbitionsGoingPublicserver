using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using HarmonyLib;
using Streets.Pedestrians;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>PED-LIGHTS-1 (user 2026-11-07: "Können wir im Mod einen intelligenten Verkehr einbauen, auch so, dass die
    /// Fußgänger nicht über den Zebrastreifen laufen, wenn die Autos grün haben" - "die Autos sollen ja bei Grün fahren, aber
    /// immer laufen dann noch Personen dabei rüber"). The game's street pedestrians stop at a crosswalk with a traffic light
    /// while its cars do not have red (PedestrianMovementHandler.CanCrossCrosswalk: the linked red lamp is lit) - but they look
    /// only in that moment, and the lights (Gley's TrafficLightsIntersection) give one road green after the other with no
    /// all-red phase. Whoever reached the curb a second before the cars got green walked across in front of them.
    /// Now a pedestrian starts only when the red left for that road lasts the whole way across (the crosswalk's length at the
    /// pedestrian's own speed, plus a margin), else waits for the next red; one who is already on the crosswalk when the red
    /// is about to end hurries until off it. The cars keep their green. The phase clock is read from the lights' own colours
    /// (TrafficLightsIntersection.ApplyColorChanges - the local timer's changes and the host's lights on a client alike) and
    /// the intersection's green and yellow times, on the lights' clock (Time.deltaTime). Pedestrians run on every machine of
    /// their own; nothing is sent.
    /// PED-YIELD-1 (user 2026-11-07, "ja" to: cars stop for pedestrians, turning cars let them go first): a car of this
    /// machine's traffic with a pedestrian on a crosswalk in its front sensor stops as it does for a player in front
    /// (VehicleEvents' object-in-trigger event, StopInDistance) and drives on once the crosswalk is clear - turning cars and
    /// a car whose green comes while someone is still on the way alike. Where the cars are the host's (a client near other
    /// players sees the host's traffic as ghosts, which cannot stop for this machine's pedestrians) the pedestrians look
    /// out instead: they start only when no car is on the crosswalk or about to drive onto it.</summary>
    internal static class PedestrianLights
    {
        const string Tag = "[PedLights]";
        const float Margin = 2f;          // seconds to spare on top of the walk
        const float FallbackSpeed = 1.3f; // m/s when a pedestrian reports none
        const float HurryFactor = 1.8f, HurryMax = 3.4f;

        /// <summary>Per intersection: the road that is green or yellow, whether yellow, and since when (Time.time; -1 =
        /// not seen begin - then the phase is taken as ending now, the safe side).</summary>
        sealed class Phase { public int Road = -1; public bool Yellow; public float Since = -1f; }
        static readonly Dictionary<TrafficLightsIntersection, Phase> _phase = new Dictionary<TrafficLightsIntersection, Phase>();

        /// <summary>A crosswalk's red lamp (the cars' red, CrosswalkToTrafficLightLink.redTrafficLight) → its intersection
        /// and road.</summary>
        static readonly Dictionary<GameObject, (TrafficLightsIntersection ti, int road)> _byLamp = new Dictionary<GameObject, (TrafficLightsIntersection, int)>();
        static object? _mappedFor;
        static float _mapRetryAt;

        sealed class Crossing { public CrosswalkToTrafficLightLink Link = null!; public float BaseSpeed, Started, Need; public bool Hurried; public bool WasInside; }
        static readonly Dictionary<PedestrianMovementHandler, Crossing> _crossing = new Dictionary<PedestrianMovementHandler, Crossing>();

        static int _held, _hurried;
        static float _reportAt;
        static bool _loggedMap;

        // ── the lights' clock ─────────────────────────────────────────────────────────────────────────────────────────

        internal static void OnColorsApplied(TrafficLightsIntersection ti)
        {
            var st = ti.intersectionState;
            if (st == null) return;
            int road = -1; bool yellow = false;
            for (int i = 0; i < st.Length; i++)
                if (st[i] != TrafficLightsColor.Red) { road = i; yellow = st[i] == TrafficLightsColor.Yellow; break; }
            if (!_phase.TryGetValue(ti, out var ph)) { _phase[ti] = new Phase { Road = road, Yellow = yellow, Since = -1f }; return; }
            if (ph.Road == road && ph.Yellow == yellow) return;   // a repeat (the host re-asserts its lights)
            ph.Road = road; ph.Yellow = yellow; ph.Since = Time.time;
        }

        /// <summary>Seconds the road keeps its red (0: not red or unknown - the safe side); -1: no such road.</summary>
        internal static float RedLeft(TrafficLightsIntersection ti, int road)   // also PED-SIGNALS-1
        {
            var st = ti.intersectionState;
            if (st == null || road < 0 || road >= st.Length) return -1f;
            if (st[road] != TrafficLightsColor.Red) return 0f;
            if (!_phase.TryGetValue(ti, out var ph) || ph.Road < 0) return 0f;
            int n = st.Length, k = ((road - ph.Road) % n + n) % n;
            if (k == 0) return 0f;
            float g = Mathf.Max(0f, ti.greenLightTime), y = Mathf.Max(0f, ti.yellowLightTime);
            float el = ph.Since < 0f ? float.MaxValue : Time.time - ph.Since;
            float cur = ph.Yellow ? Mathf.Max(0f, y - el) : Mathf.Max(0f, g - el) + y;
            return cur + (k - 1) * (g + y);
        }

        /// <summary>PED-LIGHTS-2 (user 2026-11-08, a screenshot: "Fußgängerampel ist grün und die Leute laufen nicht rüber"; in the
        /// morning already "die Leute laufen nicht bei Rot über den Streifen"): the city's lights are quick - 8 s green, 1 s yellow
        /// (the scene's TrafficComponent), so a road's red lasts 9 s at a crossing of two roads and 18 s at three. The walk across
        /// (12 m and more at 1.3 m/s plus 2 s) almost never fitted, and the people waited for good. Now they start while at least
        /// 70 % of the road's whole red is left even when the walk is longer: early in the red, and those still on the crosswalk
        /// when it is about to end hurry (WhileCrossing), and the cars wait for them (PED-YIELD-1). The pedestrian lights show
        /// the same window (PED-SIGNALS-1/2).</summary>
        internal static float StartNeed(TrafficLightsIntersection ti, float need)
        {
            int n = ti.intersectionState != null ? ti.intersectionState.Length : 0;
            float full = Mathf.Max(0, n - 1) * (Mathf.Max(0f, ti.greenLightTime) + Mathf.Max(0f, ti.yellowLightTime));
            return full > 0f ? Mathf.Min(need, full * 0.7f) : need;
        }

        internal static bool Lookup(CrosswalkToTrafficLightLink? link, out TrafficLightsIntersection ti, out int road)   // also PED-SIGNALS-1
        {
            ti = null!; road = -1;
            var lamp = link != null ? link.redTrafficLight : null;
            if (lamp == null) return false;
            var im = TrafficManager.Instance != null ? TrafficManager.Instance.intersectionManager : null;
            if (im == null) return false;
            if (!ReferenceEquals(im, _mappedFor) || (!_byLamp.ContainsKey(lamp) && Time.unscaledTime >= _mapRetryAt)) Map(im);
            if (!_byLamp.TryGetValue(lamp, out var hit)) return false;
            ti = hit.ti; road = hit.road;
            return ti != null;
        }

        static void Map(IntersectionManager im)
        {
            _mappedFor = im;
            _mapRetryAt = Time.unscaledTime + 10f;
            _byLamp.Clear();
            var all = im.allIntersections;
            if (all == null) return;
            int lights = 0;
            foreach (var el in all)
            {
                if (!(el is TrafficLightsIntersection ti) || ti.stopWaypoints == null) continue;
                for (int r = 0; r < ti.stopWaypoints.Count; r++)
                {
                    var reds = ti.stopWaypoints[r]?.redLightObjects;
                    if (reds == null) continue;
                    foreach (var go in reds) if (go != null) { _byLamp[go] = (ti, r); lights++; }
                }
            }
            if (!_loggedMap && lights > 0)
            {
                _loggedMap = true;
                Plugin.Logger.LogInfo($"{Tag} {lights} red lamps of {all.Length} intersections mapped - pedestrians start early in the red; who is late hurries, cars wait (PED-LIGHTS-1, PED-LIGHTS-2).");
            }
        }

        /// <summary>The walk across: the crosswalk's long side at the pedestrian's speed, plus the margin.</summary>
        static float Need(PedestrianMovementHandler h, CrosswalkToTrafficLightLink link, out float speed)
        {
            speed = FallbackSpeed;
            try { var a = h._agent; if (a != null && a.speed > 0.4f) speed = a.speed; } catch { }
            float len = 8f;
            try
            {
                var col = link.GetComponent<Collider>();
                if (col != null) { var s = col.bounds.size; len = Mathf.Clamp(Mathf.Max(s.x, s.z), 2f, 40f); }
            }
            catch { }
            return len / speed + Margin;
        }

        // ── the decision at the curb ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>The game's answer stands when the cars have no red; with red, only if it lasts the walk.</summary>
        internal static bool MayCross(PedestrianMovementHandler h, bool game)
        {
            if (!game) return false;
            var link = h._crosswalkLink;
            if (!Lookup(link, out var ti, out int road)) return true;
            float left = RedLeft(ti, road);
            if (left >= 0f && left < StartNeed(ti, Need(h, link!, out _))) return false;   // PED-LIGHTS-2: early in a short red is enough
            return !GhostComing(h, link!);
        }

        // ── PED-YIELD-1: the pedestrians look out for the host's cars (ghosts) ──────────────────────────────────────

        static readonly Dictionary<PedestrianMovementHandler, float> _lookingSince = new Dictionary<PedestrianMovementHandler, float>();

        /// <summary>A ghost car is on the crosswalk or will be within 3 s on its course (after 25 s of waiting the pedestrian
        /// goes anyway, so a busy corner cannot hold them for ever).</summary>
        static bool GhostComing(PedestrianMovementHandler h, CrosswalkToTrafficLightLink link)
        {
            if (TrafficSync.GhostCount == 0) { _lookingSince.Remove(h); return false; }
            var col = link.GetComponent<Collider>();
            if (col == null) return false;
            var b = col.bounds; b.Expand(new Vector3(1.5f, 4f, 1.5f));
            var c = b.center; float r = Mathf.Max(b.extents.x, b.extents.z) + 30f;
            bool coming = false;
            foreach (var (pos, vel) in TrafficSync.GhostMotions())
            {
                var d = pos - c; d.y = 0f;
                if (d.sqrMagnitude > r * r) continue;
                for (float t = 0f; t <= 3f && !coming; t += 0.25f)
                {
                    var q = pos + new Vector3(vel.x, 0f, vel.z) * t; q.y = c.y;
                    if (b.Contains(q)) coming = true;
                }
                if (coming) break;
            }
            if (!coming) { _lookingSince.Remove(h); return false; }
            if (!_lookingSince.TryGetValue(h, out float since)) { _lookingSince[h] = Time.time; _looked++; return true; }
            if (Time.time - since > 25f) { _lookingSince.Remove(h); return false; }
            return true;
        }

        // ── PED-YIELD-1: this machine's cars stop for people on a crosswalk ─────────────────────────────────────────

        static float _yieldAt;
        static readonly HashSet<int> _carsHeld = new HashSet<int>();
        static readonly Dictionary<GleyTrafficSystem.VehicleComponent, BoxCollider?> _sensors = new Dictionary<GleyTrafficSystem.VehicleComponent, BoxCollider?>();
        static readonly List<Vector3> _onWalk = new List<Vector3>();
        static int _yielded, _looked;

        /// <summary>Five times a second while the game's traffic runs on this machine.</summary>
        internal static void TickYield()
        {
            if (Time.time < _yieldAt) return;
            _yieldAt = Time.time + 0.2f;
            var tm = TrafficManager.Instance;
            var list = tm != null && tm.trafficVehicles != null ? tm.trafficVehicles.GetVehicleList() : null;
            if (list == null) return;
            _onWalk.Clear();
            foreach (var kv in _crossing)
            {
                var a = kv.Key._agent;
                if (a != null && a.isActiveAndEnabled) _onWalk.Add(a.transform.position);
            }
            if (_onWalk.Count == 0 && _carsHeld.Count == 0) return;
            foreach (var v in list)
            {
                if (v == null) continue;
                int idx = v.GetIndex();
                if (!v.gameObject.activeInHierarchy) { _carsHeld.Remove(idx); continue; }
                bool front = false;
                if (_onWalk.Count > 0)
                {
                    var box = Sensor(v);
                    if (box != null) foreach (var p in _onWalk) if (InBox(box, p)) { front = true; break; }
                }
                if (front)
                {
                    // as for a player in front: stop at a distance (re-said each beat - the car's own sensor may have
                    // cleared its stop actions in between)
                    VehicleEvents.TriggeerNewObjectInTriggerEvent(true, idx, false, -1, false, false, AvailableInteractions.PlayerInteraction);
                    if (_carsHeld.Add(idx)) _yielded++;
                }
                else if (_carsHeld.Remove(idx))
                {
                    // drive on - unless the car's own sensor still sees something (then its own exit lets it go)
                    var own = v.obstacleList;
                    if (own == null || own.Count == 0) VehicleEvents.TriggeerNewObjectInTriggerEvent(false, idx, false, -1, false, false, null);
                }
            }
        }

        static BoxCollider? Sensor(GleyTrafficSystem.VehicleComponent v)
        {
            if (_sensors.TryGetValue(v, out var b)) return b;
            BoxCollider? box = null;
            try { if (v.frontTrigger != null && v.frontTrigger.childCount > 0) box = v.frontTrigger.GetChild(0).GetComponent<BoxCollider>(); } catch { }
            _sensors[v] = box;
            return box;
        }

        static bool InBox(BoxCollider b, Vector3 p)
        {
            var l = b.transform.InverseTransformPoint(p) - b.center;
            var e = b.size * 0.5f;
            return Mathf.Abs(l.x) <= e.x + 0.6f && Mathf.Abs(l.z) <= e.z + 0.5f && Mathf.Abs(l.y) <= e.y + 2f;
        }

        internal static void StartCrossing(PedestrianMovementHandler h)
        {
            var link = h._crosswalkLink;
            if (link == null || !Lookup(link, out _, out _)) return;
            float need = Need(h, link, out float speed);
            _crossing[h] = new Crossing { Link = link, BaseSpeed = speed, Started = Time.time, Need = need };
        }

        /// <summary>Every frame for a pedestrian on a crosswalk: hurry when the red ends before the walk; done once off it.</summary>
        internal static void WhileCrossing(PedestrianMovementHandler h)
        {
            if (!_crossing.TryGetValue(h, out var c)) return;
            var a = h._agent;
            if (a == null || c.Link == null) { Done(h, c); return; }
            bool inside;
            try { var b = c.Link.GetComponent<Collider>().bounds; b.Expand(1f); inside = b.Contains(a.transform.position); }
            catch { inside = false; }
            if (inside) c.WasInside = true;
            float t = Time.time - c.Started;
            if ((c.WasInside && !inside) || t > 45f || (!c.WasInside && t > 6f)) { Done(h, c); return; }
            if (c.Hurried || !Lookup(c.Link, out var ti, out int road)) return;
            float left = RedLeft(ti, road);
            if (left < 0f) return;
            float walkLeft = Mathf.Max(0f, c.Need - Margin - t);
            if (left < walkLeft + 0.5f)
            {
                c.Hurried = true; _hurried++;
                try { a.speed = Mathf.Min(HurryMax, c.BaseSpeed * HurryFactor); } catch { }
            }
        }

        static void Done(PedestrianMovementHandler h, Crossing c)
        {
            if (c.Hurried) { try { if (h._agent != null) h._agent.speed = c.BaseSpeed; } catch { } }
            _crossing.Remove(h);
        }

        internal static void Forget(PedestrianMovementHandler h)
        {
            if (_crossing.TryGetValue(h, out var c)) Done(h, c);
            _lookingSince.Remove(h);
        }

        internal static void Report()
        {
            if (Time.unscaledTime < _reportAt) return;
            _reportAt = Time.unscaledTime + 600f;
            if (_held == 0 && _hurried == 0 && _yielded == 0 && _looked == 0) return;
            Plugin.Logger.LogInfo($"{Tag} last 10 min: {_held} times a pedestrian waited for a longer red, {_hurried} hurried off a crosswalk (PED-LIGHTS-1); {_yielded} cars stopped for people on a crosswalk, {_looked} times a pedestrian waited for the host's car to pass (PED-YIELD-1).");
            _held = 0; _hurried = 0; _yielded = 0; _looked = 0;
        }

        // ── the game's hooks ──────────────────────────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(TrafficLightsIntersection), "ApplyColorChanges")]
        public static class Patch_TLI_ApplyColorChanges_Clock
        {
            static void Postfix(TrafficLightsIntersection __instance)
            {
                try { OnColorsApplied(__instance); } catch { }
            }
        }

        /// <summary>PED-YIELD-1: the traffic's own loop drives the beat (it runs while this machine's traffic does).</summary>
        [HarmonyPatch(typeof(IntersectionManager), nameof(IntersectionManager.UpdateIntersections))]
        public static class Patch_IM_UpdateIntersections_Yield
        {
            static void Postfix()
            {
                try { TickYield(); } catch { }
            }
        }

        [HarmonyPatch(typeof(PedestrianMovementHandler), "CanCrossCrosswalk")]
        public static class Patch_PMH_CanCrossCrosswalk_RedLasts
        {
            static void Postfix(PedestrianMovementHandler __instance, ref bool __result)
            {
                try { __result = MayCross(__instance, __result); } catch { }
            }
        }

        /// <summary>Entering a crosswalk: the game stops the pedestrian or lets it go; one that goes is followed.</summary>
        [HarmonyPatch(typeof(PedestrianMovementHandler), nameof(PedestrianMovementHandler.OnTriggerEnter))]
        public static class Patch_PMH_OnTriggerEnter_Follow
        {
            static void Postfix(PedestrianMovementHandler __instance, Collider other)
            {
                try
                {
                    if (other == null || !other.gameObject.CompareTag("Crosswalk")) return;
                    var link = other.GetComponent<CrosswalkToTrafficLightLink>();
                    if (link == null || __instance._crosswalkLink != link) return;
                    if (!__instance._waitingSemaphore) StartCrossing(__instance);
                    else if (link.redTrafficLight != null && link.redTrafficLight.activeSelf) _held++;   // the game would have let it go
                }
                catch { }
            }
        }

        /// <summary>Each frame: a waiting pedestrian that goes now is followed; one on a crosswalk may hurry.</summary>
        [HarmonyPatch(typeof(PedestrianMovementHandler), nameof(PedestrianMovementHandler.Update))]
        public static class Patch_PMH_Update_Crossing
        {
            static void Prefix(PedestrianMovementHandler __instance, out bool __state) => __state = __instance._waitingSemaphore;
            static void Postfix(PedestrianMovementHandler __instance, bool __state)
            {
                try
                {
                    if (__state && !__instance._waitingSemaphore) StartCrossing(__instance);
                    else if (_crossing.Count > 0) WhileCrossing(__instance);
                }
                catch { }
            }
        }

        /// <summary>A pedestrian back in the pool (despawned): its normal speed back, not followed any more.</summary>
        [HarmonyPatch(typeof(PedestrianMovementHandler), nameof(PedestrianMovementHandler.Disable))]
        public static class Patch_PMH_Disable_Forget
        {
            static void Prefix(PedestrianMovementHandler __instance)
            {
                try { Forget(__instance); Report(); } catch { }
            }
        }
    }
}
