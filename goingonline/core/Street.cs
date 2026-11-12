using System.Collections.Generic;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GO-STREET-2: a provider's network as the street shows it (Going Public Server sends them round).</summary>
    public sealed class StreetNet
    {
        public string Owner = "", Company = "";   // the player's id ("" = this machine in single player) and the company
        public int Color = 0x1E5AA8;               // the van livery panel's colour (RGB)
        public Dictionary<string, byte> Sites = new Dictionary<string, byte>();   // building key -> Street.Up / Building / AtWork
    }

    /// <summary>GO-STREET-1 (user 2026-11-11, "Spielinhalts-Verbesserungen" - chose "Netz in der Stadt sichtbar"): the network
    /// shows in the street.
    /// - A grey street cabinet (assets/models/gostreetcab.gom) on the pavement by every building of a provider that is
    ///   connected or being connected: beside its door (1.3 - 4.2 m, never in front of it), its front to the street.
    /// - Road works while the building is being connected: two barriers (their lamp blinks with the racks' amber LEDs) and
    ///   three cones in front of the cabinet; two cones by the cabinet while somebody of the technical staff works there.
    /// GO-STREET-2 (user 2026-11-11: "Für alle Spieler sichtbar, Verteilerkästen mit Firmenlogo und Name"): every player's
    /// network shows on every machine - each machine sends its own (Local, at a change, through Going Public Server) and
    /// shows the others' (Remote) - and each cabinet carries a label with its provider's business logo and company name
    /// (LiveryPaint.ComposeLabel; the logo as it comes with the van livery) and a band in its van livery's colour. Two
    /// providers in one building get two cabinets side by side (a place is free 1 m from another cabinet); the networks
    /// are placed in the order of their owners, so every machine puts them alike.
    /// Only near the camera (from 110 m, gone beyond 140 m), visual only: no colliders, so nothing can get stuck on them.
    /// A place counts when a ray finds pavement there (ground, not road), level with the door, and no building, wall or
    /// street prop stands in the cabinet's room; a building without such a place gets none. Main thread, every 1.5 s.</summary>
    internal static class Street
    {
        public const byte Up = 0, Building = 1, AtWork = 2;
        const float Near = 110f, Far = 140f, Every = 1.5f, Apart = 1.0f;
        const int LabelW = 512, LabelH = 160;

        /// <summary>Going Public Server: the other players' networks (the server's latest).</summary>
        public static System.Func<List<StreetNet>>? Remote;
        /// <summary>Going Public Server: this player's id ("" in single player).</summary>
        public static System.Func<string>? Me;

        sealed class Spot
        {
            public bool Tried, Ok;
            public Vector3 Pos, Out, Side;   // the cabinet's place; towards the street; along the facade, away from the door
            public GameObject? Cab, Works, Tech;
            public string Label = "";        // the label texture's key on the cabinet
        }

        static readonly Dictionary<string, Spot> _spots = new Dictionary<string, Spot>();
        static readonly HashSet<string> _seen = new HashSet<string>();
        static readonly List<string> _gone = new List<string>();
        static readonly Dictionary<string, (string key, Texture2D tex)> _labels = new Dictionary<string, (string, Texture2D)>();
        static readonly int BaseMap = Shader.PropertyToID("_BaseColorMap"), BaseColor = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock? _mpb;
        static GameObject? _root;
        static IspState? _for;
        static float _next;
        static int _ground = -1, _block, _roads;
        static bool _logged;

        static string MyId() { try { return Me?.Invoke() ?? ""; } catch { return ""; } }

        /// <summary>The player's own network for the others: its buildings and what happens there.</summary>
        public static StreetNet Local(IspState? s)
        {
            var n = new StreetNet { Owner = MyId() };
            if (s == null || s.Company.Length == 0) return n;
            n.Company = s.Company;
            n.Color = (s.Style?.Panel ?? 0x1E5AA8) & 0xFFFFFF;
            foreach (var site in s.Sites.Values)
            {
                if (site.Lost || !(site.Coax || site.Fiber || site.CoaxHours > 0 || site.FiberHours > 0)) continue;
                n.Sites[site.Key] = site.CoaxHours > 0 || site.FiberHours > 0 ? Building : TechAt(s, site.Key) ? AtWork : Up;
            }
            return n;
        }

        public static void Tick(IspState? s)
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Every;
            try { Run(s); }
            catch (System.Exception ex) { Go.Warn("street: " + ex.Message + " (GO-STREET-1)."); _next = Time.unscaledTime + 30f; }
        }

        static void Run(IspState? s)
        {
            if (!ReferenceEquals(s, _for) || (_spots.Count > 0 && _root == null)) { Clear(); _for = s; }
            if (Sim.Test || Application.isBatchMode) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (_ground < 0)
            {
                _ground = LayerMask.GetMask("Ground", "GroundUnplacable", "Roads");
                _roads = LayerMask.NameToLayer("Roads");
                _block = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls", "StreetProps");
            }
            string me = MyId();
            var nets = new List<StreetNet> { Local(s) };
            if (Remote != null)
                try { foreach (var r in Remote() ?? new List<StreetNet>()) if (r != null && r.Owner.Length > 0 && r.Owner != me) nets.Add(r); }
                catch { }
            nets.Sort((a, b) => string.CompareOrdinal(a.Owner, b.Owner));
            Vector3 c = cam.transform.position;
            _seen.Clear();
            int cabs = 0, works = 0, others = 0;
            foreach (var net in nets)
            {
                if (net.Sites.Count == 0) continue;
                bool own = net.Owner == me;
                Texture2D? label = null;
                string labelKey = "";
                bool labelTried = false;
                Color tint = Rgb(net.Color);
                foreach (var kv in net.Sites)
                {
                    if (!City.ByKey.TryGetValue(kv.Key, out var b) || b.Pos == Vector3.zero) continue;
                    float dx = b.Pos.x - c.x, dz = b.Pos.z - c.z, d2 = dx * dx + dz * dz;
                    string id = net.Owner + "|" + kv.Key;
                    bool have = _spots.TryGetValue(id, out var sp);
                    if (d2 > Far * Far || (!have && d2 > Near * Near)) continue;
                    _seen.Add(id);
                    if (sp == null) _spots[id] = sp = new Spot();
                    if (!sp.Tried) Place(net.Owner + kv.Key, b.Pos, sp);
                    if (!sp.Ok) continue;
                    if (_root == null) _root = new GameObject("GO_Street");   // Unity's null: also after a scene change
                    if (sp.Cab == null) { sp.Cab = Make("gostreetcab", _root.transform, sp.Pos, Quaternion.LookRotation(sp.Out), tint); sp.Label = "\u0001"; }
                    if (sp.Cab == null) continue;
                    cabs++;
                    if (!own) others++;
                    if (!labelTried) { labelTried = true; label = Label(net, own, out labelKey); }
                    if (sp.Label != labelKey) { Dress(sp.Cab, label, tint); sp.Label = labelKey; }
                    bool building = kv.Value == Building;
                    if (building && sp.Works == null) sp.Works = WorksAt(sp, tint);
                    if (!building) Kill(ref sp.Works);
                    if (sp.Works != null) works++;
                    bool tech = kv.Value == AtWork;
                    if (tech && sp.Tech == null) sp.Tech = Cones(sp, tint);
                    if (!tech) Kill(ref sp.Tech);
                }
            }
            _gone.Clear();
            foreach (var key in _spots.Keys) if (!_seen.Contains(key)) _gone.Add(key);
            foreach (var key in _gone) { var sp = _spots[key]; Kill(ref sp.Cab); Kill(ref sp.Works); Kill(ref sp.Tech); _spots.Remove(key); }
            if (!_logged && cabs > 0)
            {
                _logged = true;
                Go.Log($"street: {cabs} cabinets ({others} of other players) and {works} road works near the camera (GO-STREET-1/2).");
            }
        }

        static Color Rgb(int c) => new Color(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f, 1f);

        /// <summary>A provider's label: its logo and company name on a white plate (made again when either changes).</summary>
        static Texture2D? Label(StreetNet net, bool own, out string key)
        {
            var logo = Livery.LogoImg(own ? "" : net.Owner, out string logoKey);
            key = net.Company + "|" + net.Color.ToString("X6") + "|" + logoKey;
            if (_labels.TryGetValue(net.Owner, out var have) && have.key == key && have.tex != null) return have.tex;
            if (!Livery.Art(out var font, out _) || font == null) { key = ""; return null; }
            var px = LiveryPaint.ComposeLabel(logo, net.Company, font, LabelW, LabelH, net.Color);
            var t = new Texture2D(LabelW, LabelH, TextureFormat.RGB24, true) { name = "GO_CabinetLabel", hideFlags = HideFlags.HideAndDontSave };
            t.SetPixelData(px, 0);   // the top level only - Apply(true) makes the rest
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 4;
            t.Apply(true, true);
            if (have.tex != null) UnityEngine.Object.Destroy(have.tex);
            _labels[net.Owner] = (key, t);
            return t;
        }

        /// <summary>The cabinet's label and colour band, per cabinet (the materials stay shared).</summary>
        static void Dress(GameObject cab, Texture2D? label, Color tint)
        {
            var model = Models.Get("gostreetcab");
            var mr = cab.GetComponent<MeshRenderer>();
            if (model == null || mr == null) return;
            _mpb ??= new MaterialPropertyBlock();
            int li = model.PartIndex("card_white"), ai = model.PartIndex("accent");
            if (label != null && li >= 0 && li < mr.sharedMaterials.Length)
            {
                _mpb.Clear();
                mr.GetPropertyBlock(_mpb, li);
                _mpb.SetTexture(BaseMap, label);
                _mpb.SetColor(BaseColor, Color.white);
                mr.SetPropertyBlock(_mpb, li);
            }
            if (ai >= 0 && ai < mr.sharedMaterials.Length)
            {
                _mpb.Clear();
                mr.GetPropertyBlock(_mpb, ai);
                _mpb.SetColor(BaseColor, tint);
                mr.SetPropertyBlock(_mpb, ai);
            }
        }

        /// <summary>A place on the pavement beside the door, facing the street, 1 m from other cabinets. Without lanes yet:
        /// tried again later.</summary>
        static void Place(string seed, Vector3 door, Spot sp)
        {
            var lane = Roads.Snap(door);
            if (lane == null) return;
            sp.Tried = true;
            Vector3 d = lane.Value - door;
            d.y = 0f;
            if (d.sqrMagnitude < 0.25f) return;
            d.Normalize();
            var side = Vector3.Cross(Vector3.up, d);
            int pref = (Stable(seed) & 1) == 0 ? 1 : -1;
            var rot = Quaternion.LookRotation(d);
            foreach (int k in new[] { pref, -pref })
                foreach (float lat in new[] { 1.8f, 2.6f, 3.4f, 1.3f, 4.2f })
                    foreach (float o in new[] { 0.5f, 0.9f, 1.4f })
                    {
                        if (!Pavement(door + d * o + side * (k * lat), door.y, out var p)) continue;
                        if (Taken(p)) continue;
                        if (Physics.CheckBox(p + Vector3.up * 0.62f, new Vector3(0.46f, 0.55f, 0.22f), rot, _block, QueryTriggerInteraction.Ignore)) continue;
                        sp.Pos = p; sp.Out = d; sp.Side = side * k; sp.Ok = true;
                        return;
                    }
        }

        /// <summary>The same number for a text on every machine (string.GetHashCode differs between runs).</summary>
        static int Stable(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (char ch in s) h = h * 31 + ch;
                return h & 0x7FFFFFFF;
            }
        }

        static bool Taken(Vector3 p)
        {
            foreach (var other in _spots.Values)
                if (other.Ok && (other.Pos - p).sqrMagnitude < Apart * Apart) return true;
            return false;
        }

        /// <summary>The ground under p when it is pavement (not road) and about level with the door.</summary>
        static bool Pavement(Vector3 p, float level, out Vector3 at)
        {
            at = p;
            if (!Physics.Raycast(p + Vector3.up * 2.5f, Vector3.down, out var hit, 6f, _ground, QueryTriggerInteraction.Ignore)) return false;
            if (hit.collider.gameObject.layer == _roads || Mathf.Abs(hit.point.y - level) > 0.6f) return false;
            at = hit.point;
            return true;
        }

        static bool TechAt(IspState s, string key)
        {
            foreach (var t in s.Techs)
                if (t.Ticket != 0 && t.Travel <= 0f && t.At == key && (t.Role == Cat.RoleTech || t.Role == Cat.RoleSwitch || t.Role == Cat.RoleMaster)) return true;
            return false;
        }

        /// <summary>Road works: barriers in front of the cabinet and to one side, three cones - each only where it finds
        /// pavement and room.</summary>
        static GameObject WorksAt(Spot sp, Color tint)
        {
            var g = new GameObject("GO_RoadWorks");
            g.transform.SetParent(_root!.transform, false);
            Piece(g, "gobarrier", sp.Pos + sp.Out * 1.15f, sp.Out, tint, new Vector3(0.5f, 0.5f, 0.2f));
            Piece(g, "gobarrier", sp.Pos + sp.Out * 0.55f + sp.Side * 0.9f, sp.Side, tint, new Vector3(0.5f, 0.5f, 0.2f));
            Piece(g, "gocone", sp.Pos + sp.Out * 1.2f - sp.Side * 0.8f, sp.Out, tint, new Vector3(0.15f, 0.25f, 0.15f));
            Piece(g, "gocone", sp.Pos + sp.Out * 0.5f - sp.Side * 0.85f, sp.Out, tint, new Vector3(0.15f, 0.25f, 0.15f));
            Piece(g, "gocone", sp.Pos + sp.Out * 1.25f + sp.Side * 0.85f, sp.Out, tint, new Vector3(0.15f, 0.25f, 0.15f));
            return g;
        }

        /// <summary>Somebody works at the cabinet: two cones in front of it.</summary>
        static GameObject Cones(Spot sp, Color tint)
        {
            var g = new GameObject("GO_TechAtWork");
            g.transform.SetParent(_root!.transform, false);
            Piece(g, "gocone", sp.Pos + sp.Out * 0.9f + sp.Side * 0.7f, sp.Out, tint, new Vector3(0.15f, 0.25f, 0.15f));
            Piece(g, "gocone", sp.Pos + sp.Out * 0.9f - sp.Side * 0.7f, sp.Out, tint, new Vector3(0.15f, 0.25f, 0.15f));
            return g;
        }

        static void Piece(GameObject parent, string id, Vector3 p, Vector3 facing, Color tint, Vector3 half)
        {
            if (!Pavement(p, p.y, out var at)) return;
            var rot = Quaternion.LookRotation(facing);
            if (Physics.CheckBox(at + Vector3.up * (half.y + 0.05f), half, rot, _block, QueryTriggerInteraction.Ignore)) return;
            Make(id, parent.transform, at, rot, tint);
        }

        static GameObject? Make(string id, Transform parent, Vector3 pos, Quaternion rot, Color tint)
        {
            var model = Models.Get(id);
            if (model == null) return null;
            var go = new GameObject("GO_" + id);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = model.Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = Models.MaterialsFor(model, id, tint, null);
            return go;
        }

        static void Kill(ref GameObject? go)
        {
            if (go != null) UnityEngine.Object.Destroy(go);
            go = null;
        }

        static void Clear()
        {
            foreach (var sp in _spots.Values) { Kill(ref sp.Cab); Kill(ref sp.Works); Kill(ref sp.Tech); }
            _spots.Clear();
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
        }
    }
}
