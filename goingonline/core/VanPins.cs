using System.Collections.Generic;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GO-VAN-12 (user 2026-11-10: "die geleasten Service Wagen sollen nicht fahrbar sein, nur die gekauften ...
    /// die geleasten Service Wagen können als abschaltbar unter der Karte mit Going Online Logo in der Karte als Pin
    /// umherfahren"): the leased vans are no vehicles of the game any more (Van.Leases gave back those there were) - each is
    /// a point on the city map with Going Online's logo, the map list's row "Service vans (Going Online)" (GameUi.MapFilters)
    /// shows or hides the points (IspState.ShowVanPins), and its button takes the map to one van after the other.
    /// GO-VAN-16 (user 2026-11-10, asked what the point without a van should be: "Wagen dazu zeigen"): where the point is,
    /// there is a service van too - a model of the van in the company's livery, not a vehicle of the game (it cannot be driven
    /// or entered): parked in a free street parking slot by the office or at the building its technician works at, and on
    /// the way it drives along the traffic lanes (Roads) at town speed (12 m/s, the game's time scale: it stops while the game
    /// is paused), its wheels turning; at a customer its roof beacon flashes. A parked one is solid (a box on the vehicles'
    /// layer, so the next one takes the next slot), a driving one is not. The vans at the office share one point on the map
    /// ("23 Servicewagen an der Kopfstelle"). Main thread.</summary>
    internal static class VanPins
    {
        internal const string FilterName = "goingonline_vans";
        const float Speed = 12f;                 // m/s on the road (game time scale)
        const float WheelR = 0.305f;

        sealed class Pin
        {
            public GameObject Obj = null!;       // what the map's point follows
            public PointOfInterest? Poi;
            public string Dest = "", Text = "";
            public List<Vector3> Path = new List<Vector3>();
            public float[] Cum = System.Array.Empty<float>();
            public double Start, Minutes;
            // GO-VAN-16: the van model
            public GameObject? Car;
            public MeshRenderer? Body;
            public BoxCollider? Box;
            public Transform[] Wheels = System.Array.Empty<Transform>();
            public float Dist, Spin;
            public int Seg = 1;
            public bool Parked, Arrived, AtCustomer;
        }

        static readonly List<Pin> _pins = new List<Pin>();
        static PointOfInterest? _officePoi;
        static GameObject? _officeObj;
        static string _officeText = "";
        static int _focusAt = -1;
        static string _officeKey = "";   // GO-VAN-12c: the office point's street spot, looked up once per office
        static object? _map;
        static GameObject? _root;
        static Sprite? _icon;
        static float _next;
        static bool _logged, _carLogged;
        static int _vehLayer = -2;
        static readonly Collider[] _hits = new Collider[16];
        static float _clearNext;
        static int _clearedLogged;
        static readonly Color Teal = new Color(0.10f, 0.62f, 0.75f, 1f);

        static double Now()
        {
            var g = SaveGameManager.Current;
            return g == null ? 0.0 : (g.Day * 24.0 + g.Hour) * 60.0 + g.Minute;
        }

        /// <summary>Every frame in the city: the vans move; four times a second their destinations, the parking and the points.</summary>
        internal static void Tick(IspState? s)
        {
            if (Application.isBatchMode || Sim.Test) return;
            Move();
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.25f;
            CityMap? map = null;
            try { map = InstanceBehavior<CityManager>.Instance?.cityMap; } catch { }
            if (map == null || !ReferenceEquals(map, _map)) { Clear(); _map = map; }
            if (map == null || s == null || s.LeaseVans <= 0 || string.IsNullOrEmpty(s.HeadendKey)) { Clear(); return; }
            var office = City.PosOf(s.HeadendKey);
            if (office == Vector3.zero) return;
            while (_pins.Count > s.LeaseVans) Drop(_pins.Count - 1);
            double now = Now();
            bool added = false, routed = false, parkedOne = false, show = s.ShowVanPins;
            int idle = 0;
            for (int i = 0; i < s.LeaseVans; i++)
            {
                if (i >= _pins.Count) _pins.Add(Make(i, office));
                var p = _pins[i];
                // where this van should be: at its technician's job, else at the office
                Tech? tech = null;
                string carId = "lease-van-" + i;
                foreach (var t in s.Techs) if (t != null && t.CarId == carId) { tech = t; break; }
                string dest = s.HeadendKey;
                if (tech != null && tech.Ticket != 0)
                {
                    var job = s.Tickets.Find(k => k != null && k.Id == tech.Ticket);
                    if (job != null)
                    {
                        string site = job.Site.Length > 0 ? job.Site : City.HoodSite(s, job.Hood);
                        if (site.Length > 0 && City.PosOf(site) != Vector3.zero) dest = site;
                    }
                }
                if (dest != p.Dest && !routed)
                {   // a new drive from where the van is now, to the street by the building - one route per round at most
                    routed = true;
                    var to = City.PosOf(dest);
                    var road = Roads.Snap(to) ?? to;
                    Unpark(p);
                    p.Path = Roads.Route(p.Obj.transform.position, road);
                    p.Cum = Lengths(p.Path);
                    float len = p.Cum.Length > 0 ? p.Cum[p.Cum.Length - 1] : 0f;
                    p.Minutes = System.Math.Max(0.5, len / Speed);   // game minutes: a real second each at the game's speed 1
                    p.Start = now;
                    p.Dest = dest;
                    p.Dist = 0f; p.Seg = 1; p.Arrived = false;
                }
                // a jump in the game's time (sleeping, the time machine): the drive is over
                if (!p.Arrived && p.Path.Count > 0 && now - p.Start > p.Minutes + 2.0) { p.Dist = p.Cum[p.Cum.Length - 1]; Place(p); }
                if (p.Arrived && !p.Parked && !parkedOne) { parkedOne = true; Park(p, p.Dest != s.HeadendKey); }
                bool home = p.Dest == s.HeadendKey && p.Arrived;
                if (home) idle++;
                // the map's point: one per van on the road or at a customer; the vans at the office share one
                if (!show || home)
                {
                    if (p.Poi != null) { try { UnityEngine.Object.Destroy(p.Poi.gameObject); } catch { } p.Poi = null; p.Text = ""; }
                    continue;
                }
                string text = Go.T("go_pin_van", "Service van") + (tech != null && tech.Name.Length > 0 ? " · " + tech.Name : "");
                if (p.Poi == null)
                {
                    _icon ??= Icon.Load("GO_AppIcon.png");
                    if (_icon == null) continue;
                    p.Poi = map.AddPoi(p.Obj.transform, _icon, Teal, text);
                    p.Poi.offset = new Vector3(0f, 2.6f, 0f);
                    p.Text = text;
                    added = true;
                }
                else if (p.Text != text) { p.Poi.SetText(text); p.Text = text; }
                // GO-VAN-12b: the game's map shows its extra points only when something switches them on (its
                // TogglePois only switches them off, on closing) - so on, while the map is open
                if (CityMap.IsOpen && !p.Poi.gameObject.activeSelf) p.Poi.gameObject.SetActive(true);
            }
            // GO-VAN-16b: the parking lanes fill their slots again now and then - parked cars of the game that stand in a
            // parked van go, once a second, for the vans near the camera
            if (Time.unscaledTime >= _clearNext)
            {
                _clearNext = Time.unscaledTime + 1f;
                var cam = Camera.main;
                foreach (var pp in _pins)
                    if (pp.Parked && pp.Car != null && cam != null && (pp.Car.transform.position - cam.transform.position).sqrMagnitude < 200f * 200f)
                        ClearAround(pp);
            }
            added |= OfficePin(map, office, show ? idle : 0);
            if (added)
            {
                map.UpdateNonePermanentPointOfInterests();
                if (!_logged) { _logged = true; Go.Log($"van pins: {_pins.Count} leased van(s) on the city map, {idle} of them at the office (GO-VAN-12)."); }
            }
        }

        // ── GO-VAN-16: the van models ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Every frame: the vans on the road go on along their way (the game's scaled time - not while paused).</summary>
        static void Move()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _pins.Count == 0) return;
            foreach (var p in _pins)
            {
                if (p.Arrived || p.Path.Count == 0) continue;
                p.Dist += Speed * dt;
                p.Spin = (p.Spin + Speed * dt / WheelR * Mathf.Rad2Deg) % 360f;
                Place(p);
            }
        }

        /// <summary>The van and its point where it is on its way; at the end of the way it has arrived.</summary>
        static void Place(Pin p)
        {
            var path = p.Path; var cum = p.Cum;
            if (path.Count == 0) return;
            float total = cum[cum.Length - 1];
            if (p.Dist >= total || total < 0.01f)
            {
                p.Dist = total;
                p.Obj.transform.position = path[path.Count - 1];
                if (p.Car != null) p.Car.transform.position = path[path.Count - 1];
                p.Arrived = true;
                return;
            }
            while (p.Seg < path.Count - 1 && cum[p.Seg] < p.Dist) p.Seg++;
            while (p.Seg > 1 && cum[p.Seg - 1] > p.Dist) p.Seg--;
            int i = Mathf.Clamp(p.Seg, 1, path.Count - 1);
            float seg = cum[i] - cum[i - 1];
            var a = path[i - 1]; var b = path[i];
            var pos = seg <= 0.0001f ? b : Vector3.Lerp(a, b, (p.Dist - cum[i - 1]) / seg);
            p.Obj.transform.position = pos;
            if (p.Car == null) return;
            var dir = b - a; dir.y = 0f;
            p.Car.transform.position = pos;
            if (dir.sqrMagnitude > 0.0004f)
                p.Car.transform.rotation = Quaternion.Slerp(p.Car.transform.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), 0.25f);
            foreach (var w in p.Wheels) if (w != null) w.localRotation = Quaternion.Euler(p.Spin, 0f, 0f);
            if (!p.Car.activeSelf) p.Car.SetActive(true);
        }

        /// <summary>Arrived: into the nearest free street parking slot (the next van takes the next one); without one at the
        /// side of the lane.</summary>
        static void Park(Pin p, bool atCustomer)
        {
            p.Parked = true;
            p.AtCustomer = atCustomer;
            if (p.Car == null) return;
            var end = p.Obj.transform.position;
            if (Van.Parking.TryFind(end, 5.2f, p.Car, out var pos, out var rot, 60f))
                p.Car.transform.SetPositionAndRotation(pos, rot);
            else
                p.Car.transform.position = end + p.Car.transform.right * 2.6f;   // no slot: at the side of the lane, the way it came
            p.Obj.transform.position = p.Car.transform.position;
            if (p.Box != null) p.Box.enabled = true;
            if (!p.Car.activeSelf) p.Car.SetActive(true);
            if (p.Body != null) Van.Working(p.Body, atCustomer);
            ClearAround(p);
        }

        /// <summary>GO-VAN-16b (user 2026-11-10: "andere Autos spawnen über die Servicefahrzeuge"): the game's parked cars in
        /// a parked van model go back to their pool - as the game does for the player's own vehicles
        /// (VehicleHelper.DestroyBlockingVehicles with LayerHelper.vehicleConflictMask): its slot search
        /// (freeParkingSpotDetectionMask) leaves the parked cars out, and the lanes fill their slots by chance.</summary>
        static void ClearAround(Pin p)
        {
            if (p.Car == null) return;
            var t = p.Car.transform;
            int n;
            try
            {
                Physics.SyncTransforms();
                n = Physics.OverlapBoxNonAlloc(t.TransformPoint(new Vector3(0f, 1.0f, 0.05f)), new Vector3(1.1f, 1.1f, 2.6f), _hits, t.rotation,
                                               Helpers.LayerHelper.vehicleConflictMask, QueryTriggerInteraction.Ignore);
            }
            catch { return; }
            int gone = 0;
            for (int i = 0; i < n && i < _hits.Length; i++)
            {
                var c = _hits[i];
                if (c == null || c.transform.IsChildOf(t) || c.gameObject.layer != Helpers.LayerHelper.ParkedVehiclesLayerIndex) continue;
                try { Helpers.ParkingSimulator.ReleaseParkedVehicle(c.gameObject); gone++; } catch { }
            }
            if (gone > 0 && _clearedLogged++ < 3) Go.Log($"van pins: {gone} parked car(s) of the game made room for a parked service van (GO-VAN-16b).");
        }

        static void Unpark(Pin p)
        {
            if (p.Parked && p.Car != null) p.Obj.transform.position = p.Car.transform.position;
            p.Parked = false;
            p.AtCustomer = false;
            if (p.Box != null) p.Box.enabled = false;
            if (p.Body != null) Van.Working(p.Body, false);
        }

        /// <summary>A van model: the service van's body and wheels in the shape of a car of the game (CarHolder/Body/
        /// CheapSaloon, so VanSkin paints the company's livery onto it, white paint, the number plates "GO-name").</summary>
        static GameObject? MakeCar(int i)
        {
            var model = Models.Get(Van.Id);
            if (model == null || _root == null) return null;
            Models.Model? wl = Models.Get("govanwheel_l"), wr = Models.Get("govanwheel_r");
            var car = new GameObject("GO_LeasedVan_" + i);
            car.SetActive(false);
            car.transform.SetParent(_root.transform, false);
            var holder = new GameObject("CarHolder").transform;
            holder.SetParent(car.transform, false);
            var bodyT = new GameObject("Body").transform;
            bodyT.SetParent(holder, false);
            var body = new GameObject("CheapSaloon");
            body.transform.SetParent(bodyT, false);
            body.AddComponent<MeshFilter>().sharedMesh = model.Mesh;
            var mr = body.AddComponent<MeshRenderer>();
            mr.sharedMaterials = Models.MaterialsFor(model, Van.Id, Van.Accent, null);
            if (wl != null && wr != null)
                foreach (float z in new[] { 1.407f, -1.294f })
                    foreach (bool left in new[] { true, false })
                    {
                        var w = new GameObject(left ? "WheelL" : "WheelR");
                        w.transform.SetParent(holder, false);
                        w.transform.localPosition = new Vector3(left ? -0.752f : 0.752f, WheelR, z);
                        var m = left ? wl : wr;
                        w.AddComponent<MeshFilter>().sharedMesh = m.Mesh;
                        w.AddComponent<MeshRenderer>().sharedMaterials = Models.MaterialsFor(m, left ? "govanwheel_l" : "govanwheel_r", Van.Accent, null);
                    }
            var skin = car.AddComponent<VanSkin>();
            skin.FixedPaint = Livery.White;
            if (_vehLayer == -2) _vehLayer = LayerMask.NameToLayer("Vehicles");
            var box = car.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.0f, 0.05f);
            box.size = new Vector3(1.9f, 2.0f, 4.6f);
            box.enabled = false;
            if (_vehLayer >= 0) car.layer = _vehLayer;
            if (!_carLogged) { _carLogged = true; Go.Log("van pins: the leased vans show as van models in the city - parked by the office and at the customers, driving the lanes in between (GO-VAN-16)."); }
            return car;
        }

        static Pin Make(int i, Vector3 office)
        {
            if (_root == null) { _root = new GameObject("GO_VanPins"); UnityEngine.Object.DontDestroyOnLoad(_root); }
            var go = new GameObject("GO_VanPin_" + i);
            go.transform.SetParent(_root.transform, false);
            var start = Roads.Snap(office) ?? office;
            go.transform.position = start;
            var p = new Pin { Obj = go, Path = new List<Vector3> { start, start }, Cum = new[] { 0f, 0f } };
            p.Car = MakeCar(i);
            if (p.Car != null)
            {
                p.Car.transform.position = start;
                p.Body = p.Car.GetComponentInChildren<MeshRenderer>(true);
                p.Box = p.Car.GetComponent<BoxCollider>();
                var ws = new List<Transform>();
                foreach (var t in p.Car.GetComponentsInChildren<Transform>(true)) if (t.name == "WheelL" || t.name == "WheelR") ws.Add(t);
                p.Wheels = ws.ToArray();
            }
            return p;
        }

        static float[] Lengths(List<Vector3> path)
        {
            var c = new float[path.Count];
            for (int i = 1; i < path.Count; i++) c[i] = c[i - 1] + Vector3.Distance(path[i - 1], path[i]);
            return c;
        }

        static void Drop(int i)
        {
            var p = _pins[i];
            try { if (p.Poi != null) UnityEngine.Object.Destroy(p.Poi.gameObject); } catch { }   // its OnDestroy leaves the map's list
            try { if (p.Body != null) Van.Working(p.Body, false); } catch { }
            try { if (p.Car != null) UnityEngine.Object.Destroy(p.Car); } catch { }
            try { if (p.Obj != null) UnityEngine.Object.Destroy(p.Obj); } catch { }
            _pins.RemoveAt(i);
        }

        internal static void Clear()
        {
            for (int i = _pins.Count - 1; i >= 0; i--) Drop(i);
            if (_officePoi != null) { try { UnityEngine.Object.Destroy(_officePoi.gameObject); } catch { } _officePoi = null; _officeText = ""; }
            _officeKey = "";
        }

        /// <summary>GO-VAN-12b: one point beside the office for the vans standing there (none when all are out).</summary>
        static bool OfficePin(CityMap map, Vector3 office, int idle)
        {
            if (idle <= 0)
            {
                if (_officePoi != null) { try { UnityEngine.Object.Destroy(_officePoi.gameObject); } catch { } _officePoi = null; _officeText = ""; }
                return false;
            }
            bool added = false;
            if (_officeObj == null)
            {
                if (_root == null) { _root = new GameObject("GO_VanPins"); UnityEngine.Object.DontDestroyOnLoad(_root); }
                _officeObj = new GameObject("GO_VanPin_Office");
                _officeObj.transform.SetParent(_root.transform, false);
            }
            string key = office.ToString("F1");
            if (key != _officeKey)
            {   // in the street, not on the office's own point - looked up once (GO-VAN-12c)
                _officeKey = key;
                _officeObj.transform.position = Roads.Snap(office) ?? office + new Vector3(6f, 0f, 6f);
            }
            string text = Go.T("go_pin_vans_office", "{0} service van(s) at the headend", idle);
            if (_officePoi == null)
            {
                _icon ??= Icon.Load("GO_AppIcon.png");
                if (_icon == null) return false;
                _officePoi = map.AddPoi(_officeObj.transform, _icon, Teal, text);
                _officePoi.offset = new Vector3(0f, 2.6f, 0f);
                _officeText = text;
                added = true;
            }
            else if (_officeText != text) { _officePoi.SetText(text); _officeText = text; }
            if (CityMap.IsOpen && !_officePoi.gameObject.activeSelf) _officePoi.gameObject.SetActive(true);
            return added;
        }

        /// <summary>GO-VAN-12b (user 2026-11-10: "unter der Karte Servicewagen geht zum Büro, nicht zum Servicewagen" - and
        /// "nach dem letzten fängt man beim ersten wieder an"): the map list row's button - each press the next van on the road
        /// or at a customer, then the ones at the office, and round again.</summary>
        internal static Vector3? Focus()
        {
            var at = new List<Vector3>();
            foreach (var p in _pins) if (p.Poi != null && p.Obj != null) at.Add(p.Obj.transform.position);
            if (_officePoi != null && _officeObj != null) at.Add(_officeObj.transform.position);
            if (at.Count == 0) return null;
            _focusAt = (_focusAt + 1) % at.Count;
            return at[_focusAt];
        }

        /// <summary>GameUi: the map list's row - its tick shows or hides the points (kept in the save as IspState.ShowVanPins);
        /// the van models stay in the city either way.</summary>
        internal static void Hook(Component row)
        {
            UnityEngine.UI.Toggle? toggle = null;
            try { toggle = row.GetComponentInChildren<UnityEngine.UI.Toggle>(true); } catch { }
            if (toggle == null) return;
            var s = Core.S;
            toggle.SetIsOnWithoutNotify(s == null || s.ShowVanPins);
            toggle.onValueChanged.AddListener(on => { var st = Core.S; if (st != null) st.ShowVanPins = on; _next = 0f; });
        }
    }
}
