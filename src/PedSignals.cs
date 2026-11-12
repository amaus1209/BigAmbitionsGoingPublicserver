using System.Collections.Generic;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>
    /// PED-SIGNALS-1 (user 2026-11-08: "die Fußgängerampel könnten grün anzeigen, damit die Leute wissen, dass sie laufen dürfen";
    /// chosen: "Eigene Ampeln am Übergang"). The game's pedestrian lights do not light up: its crossings switch only the cars'
    /// lamps.
    /// PED-SIGNALS-2 (user, a screenshot with our poles circled green and the game's pole box circled red: "kannst du die
    /// Fußgängerampel in die Ampel im roten Kreis einbauen und die grünen Ampeln wegnehmen"): no poles of our own any more. The
    /// game's traffic light pole (mesh "SM_TrafficLights", its variants with the boxes) carries two pedestrian boxes, one
    /// facing along the pole's arm (+Z), one to its side (+X) - fixed in the model, never lit. Each box that looks along a
    /// crosswalk with a traffic light (CrosswalkToTrafficLightLink), from beside its near end towards its far end, gets a plate
    /// on its face: the red standing figure above, the green walking figure below, as LED dots:
    ///   green:          the people may start now (PedestrianLights.StartNeed - the same rule they follow, PED-LIGHTS-2);
    ///   green blinking: the cars still have red, but nobody starts any more;
    ///   red:            the cars have green or yellow, or the phase is not known yet.
    /// The plates are children of the game's pole (they move and vanish with it), no colliders, HDRP/Lit with emission as Going
    /// Online's LEDs; shown within 160 m of the camera only. Every machine shows its own lights from its own light clock (the
    /// host's lights on a client) - nothing is sent. Not on a dedicated server. Options, tab Mods: "Pedestrian lights".
    /// </summary>
    internal static class PedSignals
    {
        private const string Tag = "(PED-SIGNALS-6)";
        private const float ShowDistance = 160f, UpdateEvery = 0.2f, CullEvery = 1f, BlinkHalf = 0.4f;
        private const float Walk = 1.3f, Margin = 2f;   // as PedestrianLights (FallbackSpeed, Margin)
        private const string PoleMesh = "SM_TrafficLights";
        private const int PoleMinVertices = 1500;      // the variant with 1010 vertices has no boxes
        private const string BigPoleMesh = "SM_TrafficLight_LOD1";   // PED-SIGNALS-6: the outer districts' big mast arm poles
        private const float MastReach = 6f, HeadW = 0.40f, HeadH = 0.42f, HeadDepth = 0.22f, PostHeight = 2.9f, PostRadius = 0.055f;

        /// <summary>The two boxes' display windows in the pole mesh's own space: centre (3 mm in front of the window) and outward
        /// normal; each window is 0.33 x 0.34 m. PED-SIGNALS-5 (user 2026-11-08, two screenshots: the red figure on the side of
        /// the box, "so wie es aussieht falsch gesetzt"): read from the texture this time - the black window texels of the
        /// traffic light pole mesh (SM_TrafficLights, 3238 and 5282 vertices, alike; the 1010-vertex one is a street light).
        /// The box along the arm shows to +X, the side box to +Z; the faces taken before were the boxes' sides.</summary>
        private static readonly (Vector3 center, Vector3 normal)[] Boxes =
        {
            (new Vector3(0.1206f, 2.4227f, 0.6031f), Vector3.right),      // the box along the arm
            (new Vector3(-0.5876f, 2.7663f, 0.1574f), Vector3.forward),   // the side box
        };
        private const float FaceW = 0.32f, FaceH = 0.32f, FigureOffset = 0.08f, FigureHeight = 0.13f;

        private enum Lamp : byte { Unknown, Red, Green, Blink }

        private sealed class Crossing
        {
            public CrosswalkToTrafficLightLink Link = null!;
            public float Need;
            public Vector3 Center, EndA, EndB, Dir;
            public float Len, Width = 3f, Ground;
            public Lamp State = Lamp.Unknown;
            public bool BlinkOn;
            public bool Shown = true;
            public readonly List<GameObject> Heads = new List<GameObject>(2);
            public readonly List<(MeshRenderer red, MeshRenderer green)> Faces = new List<(MeshRenderer, MeshRenderer)>(2);
        }

        private static readonly List<Crossing> _all = new List<Crossing>();
        private static bool _built;
        private static object? _builtFor;
        private static float _nextUpdate, _nextCull, _nextTry;
        private static bool _warned;
        private static Material? _back, _redOn, _redOff, _greenOn, _greenOff, _housing;
        private static Mesh? _stand, _walk, _plate, _cube, _cylinder;
        private static GameObject? _root;

        // LED figures, 7 x 13 dots: standing (red) and walking (green)
        private static readonly string[] StandDots =
            { "..###..", "..###..", ".......", ".#####.", "#######", "#.###.#", "#.###.#", "..###..", "..#.#..", "..#.#..", "..#.#..", "..#.#..", ".##.##." };
        private static readonly string[] WalkDots =
            { "...###.", "...###.", ".......", "..####.", ".######", "#.###.#", "..###..", "..####.", ".##..#.", ".#...##", "##....#", "#.....#", "#......" };

        // ── the option ─────────────────────────────────────────────────────────────────────────────────────────────────

        private static string PrefKey => string.IsNullOrEmpty(PlayerColours.ModId) ? "gps_ped_signals" : "m:" + PlayerColours.ModId + ":ped_signals";

        /// <summary>Options, tab Mods: "Pedestrian lights" (the game keeps a toggle as PlayerPrefs int 1/0), on by default.</summary>
        internal static bool Wanted
        {
            get { try { return UnityEngine.PlayerPrefs.GetInt(PrefKey, 1) != 0; } catch { return true; } }
        }

        internal static void AddOptions(BigAmbitions.Mods.ModOptions o)
        {
            o.AddToggle("ped_signals", Loc.T("gps_pedsig_option", "Pedestrian lights at the crossings"), true, v =>
            {
                try
                {
                    Plugin.Logger.LogInfo($"[PedSignals] pedestrian lights {(v ? "on" : "off")} (options) {Tag}.");
                    if (!v) Clear();
                    _nextTry = 0f;
                }
                catch { }
            });
        }

        // ── the frame ──────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>MAIN THREAD, every frame (MPCanvasUI.Update).</summary>
        internal static void Tick()
        {
            try
            {
                if (HeadlessHost.Active) { Survey(); return; }
                CityManager? city = null;
                try { city = InstanceBehavior<CityManager>.Instance; } catch { }
                if (city == null || !Wanted)
                {
                    if (_built) Clear();
                    return;
                }
                float now = Time.unscaledTime;
                if (!_built || !ReferenceEquals(city, _builtFor))
                {
                    if (now < _nextTry) return;
                    _nextTry = now + 10f;   // the city may still be loading its crosswalks
                    Build(city);
                    return;
                }
                if (now >= _nextCull) { _nextCull = now + CullEvery; Cull(); }
                if (now >= _nextUpdate) { _nextUpdate = now + UpdateEvery; Refresh(now); }
            }
            catch (System.Exception ex)
            {
                if (!_warned) { _warned = true; Plugin.Logger.LogWarning($"[PedSignals] {ex.GetType().Name}: {ex.Message} {Tag}."); }
            }
        }

        private static void Build(CityManager city)
        {
            Clear();
            var crossings = Crossings(out _, out _);
            if (crossings.Count == 0) return;
            var poles = Poles();
            var spots = Plan(crossings, poles, out int boxPoles, out int unmatched, out int behind);
            MakeMaterials();
            _stand ??= Dots(StandDots, FigureHeight);
            _walk ??= Dots(WalkDots, FigureHeight);
            _plate ??= Rect(FaceW, FaceH);
            int inBox = 0, onMast = 0, onPost = 0;
            foreach (var sp in spots)
            {
                try
                {
                    var holder = new GameObject("BAMP_PedLight");
                    if (sp.Kind == Mount.Box)
                    {
                        holder.transform.SetParent(sp.Pole!, false);
                        holder.transform.localPosition = sp.Pos;                   // just in front of the window
                        holder.transform.localRotation = sp.Rot;                   // the figures' side (local -Z) looks out
                        Face(holder.transform, Vector3.zero, _plate!, _back!);
                        inBox++;
                    }
                    else
                    {
                        holder.transform.SetPositionAndRotation(sp.Pos, sp.Rot);
                        if (sp.Kind == Mount.Mast) { holder.transform.SetParent(sp.Pole!, true); onMast++; }
                        else { holder.transform.SetParent(Root(), true); onPost++; Post(holder.transform, sp.Foot); }
                        Housing(holder.transform);
                        Face(holder.transform, new Vector3(0f, 0f, -HeadDepth * 0.5f - 0.002f), _plate!, _back!);
                    }
                    float z = sp.Kind == Mount.Box ? -0.0015f : -HeadDepth * 0.5f - 0.0035f;
                    var red = Face(holder.transform, new Vector3(0f, FigureOffset, z), _stand!, _redOff!);
                    var green = Face(holder.transform, new Vector3(0f, -FigureOffset, z), _walk!, _greenOff!);
                    sp.X.Heads.Add(holder);
                    sp.X.Faces.Add((red, green));
                }
                catch (System.Exception ex)
                {
                    if (!_warned) { _warned = true; Plugin.Logger.LogWarning($"[PedSignals] a light could not be placed: {ex.Message} {Tag}."); }
                }
            }
            foreach (var x in crossings) if (x.Faces.Count > 0) _all.Add(x);
            _built = true;
            _builtFor = city;
            _nextCull = 0f; _nextUpdate = 0f;
            Plugin.Logger.LogInfo($"[PedSignals] {inBox + onMast + onPost} pedestrian lights at {_all.Count} of {crossings.Count} crossings: {inBox} in the boxes of {boxPoles} traffic light poles ({behind} show the crossing behind their box, {unmatched} boxes have none), {onMast} on a mast, {onPost} on a post of their own {Tag}.");
        }

        // ── PED-SIGNALS-6: a light at both ends of every crosswalk ──────────────────────────────────────────────────────

        private enum Mount : byte { Box, Mast, Post }

        /// <summary>One light to place: in a pole's box (local pose on the pole), on a mast or on a post of its own (world pose).</summary>
        private sealed class Spot
        {
            public Crossing X = null!;
            public Mount Kind;
            public Transform? Pole;
            public Vector3 Pos, At, Foot;     // At: where it stands in the world (for "a light at this end")
            public Quaternion Rot;
        }

        private sealed class Pole { public Transform T = null!; public bool Boxes; public float Radius, Height; }

        private static List<Crossing> Crossings(out int noRed, out int noGeo)
        {
            noRed = noGeo = 0;
            var list = new List<Crossing>();
            var links = UnityEngine.Object.FindObjectsOfType<CrosswalkToTrafficLightLink>();
            if (links == null) return list;
            foreach (var link in links)
            {
                if (link == null) continue;
                if (link.redTrafficLight == null) { noRed++; continue; }
                if (!Geometry(link, out var c, out var d, out float len, out float w, out float ground)) { noGeo++; continue; }
                list.Add(new Crossing { Link = link, Need = len / Walk + Margin, Center = c, Dir = d, Len = len, Width = w, Ground = ground,
                                        EndA = c + d * (len * 0.5f), EndB = c - d * (len * 0.5f) });
            }
            return list;
        }

        /// <summary>The city's poles that can carry a light. Measured in the game files 2026-11-09: every one has its mast at the
        /// model's origin. "SM_TrafficLights" (3238 and 5282 vertices) and the outer districts' big mast arm pole
        /// "SM_TrafficLight" (10 m, a 5.3 m arm; its LOD1 mesh stands for the pole) carry the two pedestrian boxes at the same
        /// place, to the millimetre; the 1010-vertex "SM_TrafficLights", the street lamps and the simple poles only a mast.</summary>
        private static List<Pole> Poles()
        {
            var list = new List<Pole>();
            foreach (var mf in UnityEngine.Object.FindObjectsOfType<MeshFilter>())
            {
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null) continue;
                string nm = mesh.name ?? "";
                if (nm == PoleMesh) list.Add(new Pole { T = mf.transform, Boxes = mesh.vertexCount >= PoleMinVertices, Radius = 0.13f, Height = 2.55f });
                else if (nm.StartsWith(BigPoleMesh, System.StringComparison.Ordinal)) list.Add(new Pole { T = mf.transform, Boxes = true, Radius = 0.13f, Height = 2.55f });
                else if (nm.StartsWith("SM_StreetLamp_LOD1", System.StringComparison.Ordinal)) list.Add(new Pole { T = mf.transform, Radius = 0.12f, Height = 2.45f });
                else if (nm == "SM_Simple Pole") list.Add(new Pole { T = mf.transform, Radius = 0.05f, Height = 2.15f });
            }
            return list;
        }

        /// <summary>Where the lights go: first into the poles' boxes (as before, now also in the big poles of the outer
        /// districts), then every end of a crosswalk still without one gets a light on the nearest mast within 6 m (not out on
        /// the street), else on a post of its own beside the crosswalk. Each looks across to the people at the other end.</summary>
        private static List<Spot> Plan(List<Crossing> crossings, List<Pole> poles, out int boxPoles, out int unmatched, out int behind)
        {
            var spots = new List<Spot>();
            boxPoles = unmatched = behind = 0;
            foreach (var pole in poles)
            {
                if (!pole.Boxes || pole.T == null) continue;
                boxPoles++;
                var t = pole.T;
                foreach (var (lc, ln) in Boxes)
                {
                    // PED-SIGNALS-4: the normal through TransformVector - a pole placed mirrored (negative scale) shows its
                    // boxes mirrored, which TransformDirection (rotation only) misses
                    Vector3 p = t.TransformPoint(lc), n = t.TransformVector(ln);
                    n.y = 0f;
                    if (n.sqrMagnitude < 1e-6f) continue;
                    n.Normalize();
                    var x = Serves(crossings, p, n, out bool back);
                    if (x == null) { unmatched++; continue; }
                    if (back) behind++;
                    spots.Add(new Spot { X = x, Kind = Mount.Box, Pole = t, Pos = lc, Rot = Quaternion.LookRotation(-ln, Vector3.up), At = p });
                }
            }
            var byCrossing = new Dictionary<Crossing, List<Spot>>();
            foreach (var sp in spots)
            {
                if (!byCrossing.TryGetValue(sp.X, out var l)) byCrossing[sp.X] = l = new List<Spot>();
                l.Add(sp);
            }
            foreach (var x in crossings)
                for (int e = 0; e < 2; e++)
                {
                    Vector3 end = e == 0 ? x.EndA : x.EndB, other = e == 0 ? x.EndB : x.EndA;
                    if (byCrossing.TryGetValue(x, out var have) && have.Exists(sp => Flat(sp.At - end).sqrMagnitude < Flat(sp.At - other).sqrMagnitude
                                                                                      && Flat(sp.At - end).sqrMagnitude < 81f)) continue;
                    Vector3 d = Flat(other - end).normalized;   // the light looks across, to the people waiting at the other end
                    Pole? best = null;
                    float bd = MastReach * MastReach;
                    foreach (var pole in poles)
                    {
                        if (pole.T == null) continue;
                        Vector3 rel = Flat(pole.T.position - end);
                        float dd = rel.sqrMagnitude;
                        if (dd >= bd || Vector3.Dot(rel, d) > 1.0f) continue;   // never a mast out on the street
                        bd = dd; best = pole;
                    }
                    Spot sp2;
                    if (best != null)
                    {
                        Vector3 a = best.T.position;
                        float hgt = best.Height;
                        if (best.Boxes)
                        {
                            // a pole with boxes: towards a box (its +Z arm side or -X side, 2.06-3.13 m) the light hangs below it
                            Vector3 ld = best.T.InverseTransformVector(d);
                            ld.y = 0f;
                            if (ld.sqrMagnitude > 1e-6f) { ld.Normalize(); if (ld.z > 0.6f || ld.x < -0.6f) hgt = 1.78f; }
                        }
                        var pos = new Vector3(a.x, a.y + hgt, a.z) + d * (best.Radius + HeadDepth * 0.5f);
                        sp2 = new Spot { X = x, Kind = Mount.Mast, Pole = best.T, Pos = pos, Rot = Quaternion.LookRotation(-d, Vector3.up), At = pos };
                    }
                    else
                    {
                        // beside the crosswalk, a step back from its end, out of the walking path
                        Vector3 side = Vector3.Cross(Vector3.up, d).normalized;
                        var foot = end - d * 0.6f + side * (x.Width * 0.5f + 0.45f);
                        foot.y = x.Ground;
                        var pos = foot + Vector3.up * (PostHeight - HeadH * 0.5f - 0.05f) + d * (PostRadius + HeadDepth * 0.5f);
                        sp2 = new Spot { X = x, Kind = Mount.Post, Pos = pos, Rot = Quaternion.LookRotation(-d, Vector3.up), At = pos, Foot = foot };
                    }
                    spots.Add(sp2);
                    if (!byCrossing.TryGetValue(x, out var l2)) byCrossing[x] = l2 = new List<Spot>();
                    l2.Add(sp2);
                }
            return spots;
        }

        private static Transform Root()
        {
            if (_root == null) _root = new GameObject("BAMP_PedSignals");
            return _root.transform;
        }

        /// <summary>A light's housing: a dark box behind the plate (the head's centre, the plate on its local -Z face).</summary>
        private static void Housing(Transform holder)
        {
            _cube ??= PrimitiveMesh(PrimitiveType.Cube);
            var go = new GameObject("Housing");
            go.transform.SetParent(holder, false);
            go.transform.localScale = new Vector3(HeadW, HeadH, HeadDepth);
            go.AddComponent<MeshFilter>().sharedMesh = _cube;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _housing;
        }

        /// <summary>A slim post from the ground to the light (a child of the light, so it shows and hides with it).</summary>
        private static void Post(Transform holder, Vector3 foot)
        {
            _cylinder ??= PrimitiveMesh(PrimitiveType.Cylinder);
            var go = new GameObject("Post");
            go.transform.SetParent(holder, true);
            float h = PostHeight;
            go.transform.position = new Vector3(foot.x, foot.y + h * 0.5f, foot.z);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var parentScale = holder.lossyScale;
            go.transform.localScale = new Vector3(PostRadius * 2f / Mathf.Max(1e-4f, parentScale.x), h * 0.5f / Mathf.Max(1e-4f, parentScale.y), PostRadius * 2f / Mathf.Max(1e-4f, parentScale.z));
            go.AddComponent<MeshFilter>().sharedMesh = _cylinder;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _housing;
        }

        /// <summary>Unity's built-in cube or cylinder mesh, without the collider a primitive brings along.</summary>
        private static Mesh? PrimitiveMesh(PrimitiveType type)
        {
            var tmp = GameObject.CreatePrimitive(type);
            var mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            var col = tmp.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            UnityEngine.Object.Destroy(tmp);
            return mesh;
        }

        // ── PED-SIGNALS-6 survey (dedicated server only) ───────────────────────────────────────────────────────────────

        private static bool _surveyed;
        private static float _surveyNext;

        /// <summary>PED-SIGNALS-6 (user 2026-11-09: "die Fußgängerampeln sind zwar in der Innenstadt, aber nicht ringsherum, also
        /// fehlen noch viele Ampeln"): the outer districts' traffic lights are another pole model, whose boxes were not used.
        /// Once a city stands on a dedicated server (no graphics, the same city) the plan is made without placing anything and
        /// logged - the check that every crosswalk end gets a light.</summary>
        private static void Survey()
        {
            if (_surveyed) return;
            float now = Time.unscaledTime;
            if (now < _surveyNext) return;
            _surveyNext = now + 20f;
            CityManager? city = null;
            try { city = InstanceBehavior<CityManager>.Instance; } catch { }
            if (city == null) return;
            var crossings = Crossings(out int noRed, out int noGeo);
            if (crossings.Count == 0) return;
            _surveyed = true;
            var poles = Poles();
            var spots = Plan(crossings, poles, out int boxPoles, out int unmatched, out int behind);
            int inBox = 0, onMast = 0, onPost = 0;
            var lit = new HashSet<Crossing>();
            foreach (var sp in spots)
            {
                lit.Add(sp.X);
                if (sp.Kind == Mount.Box) inBox++; else if (sp.Kind == Mount.Mast) onMast++; else onPost++;
            }
            int big = 0;
            foreach (var pole in poles) if (pole.Boxes && pole.T != null && pole.T.GetComponent<MeshFilter>()?.sharedMesh?.name?.StartsWith(BigPoleMesh) == true) big++;
            Plugin.Logger.LogInfo($"[PedSignals] plan (dedicated server, nothing placed): {crossings.Count} crosswalks with traffic lights ({noRed} without a red light, {noGeo} without a usable trigger) - {spots.Count} lights at {lit.Count} of them: {inBox} in the boxes of {boxPoles} poles ({big} of them the outer districts' big poles; {behind} show the crossing behind their box, {unmatched} boxes have none), {onMast} on a mast, {onPost} on a post of their own {Tag}.");
        }

        /// <summary>The crosswalk a pole box shows: one along the box's direction with its near end beside the pole (within
        /// 9 m). First the one in front of the box (the box faces the people across the street); PED-SIGNALS-4 (user
        /// 2026-11-08: "auf die Fußgängerampel ist nichts mehr sichtbar" - with the side box measured right only 174 of 510
        /// boxes found one, mirrored 333): else the one behind it, as some of the game's boxes face their own corner.</summary>
        private static Crossing? Serves(List<Crossing> crossings, Vector3 p, Vector3 n, out bool back)
        {
            Crossing? best = null;
            float bestD = 9f;
            bool bestFront = false;
            foreach (var x in crossings)
            {
                if (Mathf.Abs(Vector3.Dot(n, x.Dir)) < 0.85f) continue;
                Vector3 toA = Flat(x.EndA - p), toB = Flat(x.EndB - p);
                bool aNear = toA.sqrMagnitude <= toB.sqrMagnitude;
                float dn = (aNear ? toA : toB).magnitude;
                if (dn >= 9f) continue;
                float along = Vector3.Dot(aNear ? toB : toA, n);
                bool front = along >= x.Len * 0.5f;
                if (!front && along > -x.Len * 0.5f) continue;
                if (best != null && bestFront && !front) continue;               // in front beats behind
                if (best != null && bestFront == front && dn >= bestD) continue;  // then the nearer one
                best = x; bestD = dn; bestFront = front;
            }
            back = best != null && !bestFront;
            return best;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>The crosswalk from its trigger: centre, the walking direction, its length, its width and the ground.</summary>
        private static bool Geometry(CrosswalkToTrafficLightLink link, out Vector3 c, out Vector3 d, out float len, out float width, out float ground)
        {
            c = d = Vector3.zero; len = 0f; width = 3f; ground = 0f;
            var col = link.GetComponent<Collider>();
            if (col == null) return false;
            Vector3 a1, a2;
            if (col is BoxCollider bc)
            {
                var t = bc.transform;
                c = t.TransformPoint(bc.center);
                var axes = new[]
                {
                    t.TransformVector(new Vector3(bc.size.x, 0f, 0f)),
                    t.TransformVector(new Vector3(0f, bc.size.y, 0f)),
                    t.TransformVector(new Vector3(0f, 0f, bc.size.z)),
                };
                float up = 0f;
                foreach (var ax in axes) up += Mathf.Abs(ax.y);
                ground = c.y - up * 0.5f;
                for (int i = 0; i < 3; i++) axes[i].y = 0f;
                System.Array.Sort(axes, (u, v) => v.sqrMagnitude.CompareTo(u.sqrMagnitude));
                a1 = axes[0]; a2 = axes[1];
            }
            else
            {
                var b = col.bounds;
                c = b.center;
                ground = b.min.y;
                a1 = b.size.x >= b.size.z ? new Vector3(b.size.x, 0f, 0f) : new Vector3(0f, 0f, b.size.z);
                a2 = b.size.x >= b.size.z ? new Vector3(0f, 0f, b.size.z) : new Vector3(b.size.x, 0f, 0f);
            }
            len = a1.magnitude;
            if (len < 2f || len > 60f) return false;
            d = a1 / len;
            width = Mathf.Clamp(a2.magnitude, 1.5f, 8f);
            return true;
        }

        private static MeshRenderer Face(Transform parent, Vector3 pos, Mesh mesh, Material m)
        {
            var go = new GameObject("Face");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return mr;
        }

        /// <summary>A mesh of square dots for a figure ('#' = a dot), facing -Z (Unity's quad winding), centred, this tall.</summary>
        private static Mesh Dots(string[] rows, float height)
        {
            int h = rows.Length, w = rows[0].Length;
            float pitch = height / h, hd = pitch * 0.82f * 0.5f;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (rows[y][x] != '#') continue;
                    float cx = (x - (w - 1) * 0.5f) * pitch, cy = ((h - 1) * 0.5f - y) * pitch;
                    Quad(v, n, t, cx - hd, cy - hd, cx + hd, cy + hd);
                }
            return Make("BAMP_PedLightDots", v, n, t);
        }

        /// <summary>A plain rectangle facing -Z, centred.</summary>
        private static Mesh Rect(float w, float h)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            Quad(v, n, t, -w * 0.5f, -h * 0.5f, w * 0.5f, h * 0.5f);
            return Make("BAMP_PedLightPlate", v, n, t);
        }

        private static void Quad(List<Vector3> v, List<Vector3> n, List<int> t, float x0, float y0, float x1, float y1)
        {
            int i = v.Count;
            v.Add(new Vector3(x0, y0, 0f)); v.Add(new Vector3(x1, y0, 0f)); v.Add(new Vector3(x0, y1, 0f)); v.Add(new Vector3(x1, y1, 0f));
            for (int k = 0; k < 4; k++) n.Add(Vector3.back);
            t.Add(i); t.Add(i + 3); t.Add(i + 1);
            t.Add(i + 3); t.Add(i); t.Add(i + 2);
        }

        private static Mesh Make(string name, List<Vector3> v, List<Vector3> n, List<int> t)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return m;
        }

        private static void MakeMaterials()
        {
            if (_back != null) return;
            var sh = Shader.Find("HDRP/Lit");
            Material M(string name, Color c, float smooth, Color? glow)
            {
                var m = sh != null ? new Material(sh) : new Material(Shader.Find("Standard"));
                m.name = "BAMP_PedLight_" + name;
                m.hideFlags = HideFlags.DontUnloadUnusedAsset;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                if (glow.HasValue)
                {
                    var lin = glow.Value.linear;   // as Going Online's LEDs (Models.Glow, LedGlow 22)
                    // GO-LIGHTS-1 / PED-SIGNALS-5: on screen as bright by day as by night (HDRP's exposure weight 0)
                    bool flat = m.HasProperty("_EmissiveExposureWeight");
                    if (flat) m.SetFloat("_EmissiveExposureWeight", 0f);
                    float k = flat ? 3f : 22f;
                    if (m.HasProperty("_EmissiveColor")) m.SetVector("_EmissiveColor", new Vector4(lin.r * k, lin.g * k, lin.b * k, 1f));
                    if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", glow.Value * 2.5f); }
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                m.enableInstancing = true;
                return m;
            }
            _back = M("plate", new Color(0.015f, 0.015f, 0.02f), 0.85f, null);
            _redOn = M("red_on", new Color(0.95f, 0.12f, 0.08f), 0.6f, new Color(1f, 0.12f, 0.06f));
            _redOff = M("red_off", new Color(0.16f, 0.03f, 0.03f), 0.6f, null);
            _greenOn = M("green_on", new Color(0.15f, 0.95f, 0.45f), 0.6f, new Color(0.15f, 1f, 0.45f));
            _greenOff = M("green_off", new Color(0.03f, 0.15f, 0.07f), 0.6f, null);
            _housing = M("housing", new Color(0.09f, 0.09f, 0.10f), 0.35f, null);
        }

        // ── state ──────────────────────────────────────────────────────────────────────────────────────────────────────

        private static void Refresh(float now)
        {
            bool blinkOn = ((int)(now / BlinkHalf) & 1) == 0;
            foreach (var x in _all)
            {
                if (!x.Shown) continue;
                var st = Lamp.Red;
                if (PedestrianLights.Lookup(x.Link, out var ti, out int road))
                {
                    float left = PedestrianLights.RedLeft(ti, road);
                    st = left <= 0f ? Lamp.Red : left >= PedestrianLights.StartNeed(ti, x.Need) ? Lamp.Green : Lamp.Blink;
                }
                if (st == x.State && (st != Lamp.Blink || blinkOn == x.BlinkOn)) continue;
                x.State = st;
                x.BlinkOn = blinkOn;
                bool green = st == Lamp.Green || (st == Lamp.Blink && blinkOn), red = st == Lamp.Red;
                foreach (var (r, g) in x.Faces)
                {
                    if (r != null) r.sharedMaterial = red ? _redOn : _redOff;
                    if (g != null) g.sharedMaterial = green ? _greenOn : _greenOff;
                }
            }
        }

        private static void Cull()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 cp = cam.transform.position;
            float max2 = ShowDistance * ShowDistance;
            foreach (var x in _all)
            {
                bool show = (x.Center - cp).sqrMagnitude <= max2;
                if (show == x.Shown) continue;
                x.Shown = show;
                foreach (var head in x.Heads) if (head != null) head.SetActive(show);
                if (show) x.State = Lamp.Unknown;   // its colours again at the next refresh
            }
        }

        /// <summary>Takes the plates off the game's poles (switched off in the options, or a new city).</summary>
        private static void Clear()
        {
            foreach (var x in _all)
                foreach (var head in x.Heads)
                    if (head != null) { try { UnityEngine.Object.Destroy(head); } catch { } }
            _all.Clear();
            _built = false;
            _builtFor = null;
        }
    }
}
