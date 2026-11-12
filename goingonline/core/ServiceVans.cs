using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GO-VAN-1 (user 2026-11-07: "Baue noch ein Auto ... für die Techniker, wo dann das Geschäftslogo drauf ist
    /// und im KI-Verkehr fährt"): the technicians' service van - a compact high-roof panel van of our own (no brand; the
    /// Blender source is ~/Big-Ambitions-Server-Code/ba-art/goingonline/go_van.py, the model assets/models/govan.gom) with the provider's business
    /// logo on both sides and the back, driving in the city's traffic.
    /// How: the game's traffic (Gley, ExternalPlugins) builds a fixed set of cars from its pool when a city loads and
    /// reuses them. Three of them are the compact "Honza Mimic" (wheelbase 2.70 m, like such a van). While a technician of
    /// ours is out (driving, at a customer, or on a ticket), up to three of those cars wear the van: the body mesh and its
    /// materials are swapped, the logo goes on as three small quads at the model's logo markers - the car's own wheels,
    /// physics, lights and sensors stay, and the traffic's count and order do not change (nothing for multiplayer to sync).
    /// A car is only changed while it is parked in the pool (not in sight), and changed back when fewer technicians are
    /// out. The logo is the game's own picture of the player's business (LogoHelper, the square sign; JPG, no alpha).
    /// Not on a dedicated server (no graphics). In multiplayer near other players the city's cars are the host's (ghosts),
    /// those stay as they are.</summary>
    internal static class ServiceVans
    {
        const int MaxVans = 3;
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        static readonly Color Teal = new Color(0.20f, 0.42f, 0.48f);

        sealed class Skin
        {
            public MeshFilter Mf = null!;
            public MeshRenderer Mr = null!;
            public Mesh Mesh = null!;
            public Material[] Mats = null!;
            public readonly List<GameObject> Logos = new List<GameObject>();
            public readonly List<(MeshFilter mf, Mesh mesh, MeshRenderer mr, Material[] mats)> Wheels = new List<(MeshFilter, Mesh, MeshRenderer, Material[])>();
        }

        static readonly Dictionary<Component, Skin> _skins = new Dictionary<Component, Skin>();
        static object? _manager;
        static PropertyInfo? _instance, _has;
        static FieldInfo? _vehicles;
        static bool _looked;
        static string _logoFor = "";
        static Material? _logo;
        static float _logoCheck;
        static int _logged = -1;

        public static void Tick()
        {
            if (Application.isBatchMode) return;
            var list = Vehicles(out var manager);
            if (!ReferenceEquals(manager, _manager))
            {
                _skins.Clear();                                  // a new city: the old cars are gone
                _manager = manager;
            }
            if (list == null) return;
            var model = Models.Get("govan");
            int wanted = model == null ? 0 : Wanted();
            // back to the game's car (only while it is not driving around)
            foreach (var vc in new List<Component>(_skins.Keys))
            {
                if (vc == null) { _skins.Remove(vc!); continue; }
                if (_skins.Count > wanted && !vc.gameObject.activeInHierarchy) Undo(vc);
            }
            // more vans from the idle compact cars
            _ghostVans.RemoveAll(g => g == null);
            if (model != null && _skins.Count + _ghostVans.Count < wanted)
                foreach (var vc in list)
                {
                    if (_skins.Count + _ghostVans.Count >= wanted) break;
                    if (vc == null || _skins.ContainsKey(vc) || vc.gameObject.activeInHierarchy) continue;
                    if (vc.transform.Find("CarHolder/Body/CheapSaloon") == null) continue;   // the Honza Mimic
                    Apply(vc, model);
                }
            int key = _skins.Count * 10 + wanted;
            if (key != _logged)
            {
                _logged = key;
                var logo = Logo();
                Go.Log($"service vans: {_skins.Count} in the traffic ({wanted} technicians on duty with a van, at most {MaxVans}), logo {(logo != null ? "of " + _logoFor : "Going Online's")} (GO-VAN-1/5).");
            }
        }

        static readonly List<GameObject> _ghostVans = new List<GameObject>();

        /// <summary>GO-VAN-5 (user 2026-11-09: "... auch neben anderen Spielern"): near other players the traffic is another
        /// machine's, and Going Public Server shows copies of its cars (TrafficSync). A compact one becomes one of this player's
        /// service vans while fewer than wanted show; it goes with the copy. Main thread, right after the copy is made.</summary>
        internal static void Ghost(GameObject car)
        {
            if (Application.isBatchMode || car == null) return;
            _ghostVans.RemoveAll(g => g == null);
            var body = car.transform.Find("CarHolder/Body/CheapSaloon");
            if (body == null) return;
            var model = Models.Get("govan");
            if (model == null || _skins.Count + _ghostVans.Count >= Wanted()) return;
            if (!Van.Dress(car.transform, model)) return;
            var mr = body.GetComponent<MeshRenderer>();
            if (mr != null) Van.Working(mr, true);
            Paint(car);   // GO-LIVERY-1: the player's livery, on a white van
            _ghostVans.Add(car);
            if (_ghostLogged++ < 2) Go.Log($"service vans: another player's traffic shows one too ({_ghostVans.Count} there) (GO-VAN-5).");
        }
        static int _ghostLogged;

        /// <summary>GO-VAN-5 (user 2026-11-09: "im Stadtverkehr fahren Servicewagen, solange Techniker Dienst haben"): technicians
        /// and switching staff on duty this hour with a vehicle - before, only those out on a ticket, so a quiet day showed none.</summary>
        static int Wanted()
        {
            var s = Core.S;
            if (s == null || string.IsNullOrEmpty(s.HeadendKey)) return 0;
            int n = 0;
            foreach (var t in s.Techs)
                if (t != null && Cat.NeedsVan(t.Role) && t.Missing == 0 && (!string.IsNullOrEmpty(t.CarId) || t.Ticket != 0 || t.Travel > 0f)) n++;
            return Mathf.Min(MaxVans, n);
        }

        /// <summary>The traffic's cars (TrafficManager.trafficVehicles.GetVehicleList(), by reflection: the stand-alone
        /// build has no reference to the traffic assembly).</summary>
        static List<Component>? Vehicles(out object? manager)
        {
            manager = null;
            if (!_looked)
            {
                _looked = true;
                var tm = System.Type.GetType("GleyTrafficSystem.TrafficManager, ExternalPlugins");
                _instance = tm?.GetProperty("Instance", Any);
                _has = tm?.GetProperty("HasInstance", Any);
                _vehicles = tm?.GetField("trafficVehicles", Any);
                if (_instance == null || _vehicles == null) Go.Warn("service vans: the traffic's manager is not where it was (GO-VAN-1).");
            }
            if (_instance == null || _vehicles == null) return null;
            if (_has != null && !(_has.GetValue(null) is bool has && has)) return null;
            manager = _instance.GetValue(null);
            var tv = manager != null ? _vehicles.GetValue(manager) : null;
            if (tv == null) return null;
            if (!(tv.GetType().GetMethod("GetVehicleList", Any)?.Invoke(tv, null) is System.Collections.IList raw)) return null;
            var list = new List<Component>(raw.Count);
            foreach (var o in raw) if (o is Component c) list.Add(c);
            return list;
        }

        static void Apply(Component vc, Models.Model model)
        {
            var body = vc.transform.Find("CarHolder/Body/CheapSaloon");
            var mf = body != null ? body.GetComponent<MeshFilter>() : null;
            var mr = body != null ? body.GetComponent<MeshRenderer>() : null;
            if (mf == null || mr == null || mf.sharedMesh == null) return;
            var skin = new Skin { Mf = mf, Mr = mr, Mesh = mf.sharedMesh, Mats = mr.sharedMaterials };
            if (!Van.Dress(vc.transform, model, skin.Logos, skin.Wheels)) return;
            Van.Working(mr, true);   // GO-VAN-5: its roof beacon flashes
            Paint(vc.gameObject);    // GO-LIVERY-1: the player's livery, on a white van
            _skins[vc] = skin;
        }

        /// <summary>GO-LIVERY-1: a traffic car wearing the van paints the player's livery (always on white - the
        /// technicians' vans; the traffic's own colour is the car's).</summary>
        static void Paint(GameObject car)
        {
            var k = car.GetComponent<VanSkin>();
            if (k == null) k = car.AddComponent<VanSkin>();
            k.FixedPaint = Livery.White;
            k.Key = "";
        }

        static void Undo(Component vc)
        {
            if (!_skins.TryGetValue(vc, out var s)) return;
            if (s.Mr != null) Van.Forget(s.Mr);
            if (s.Mf != null) s.Mf.sharedMesh = s.Mesh;
            if (s.Mr != null) s.Mr.sharedMaterials = s.Mats;
            foreach (var q in s.Logos) if (q != null) UnityEngine.Object.Destroy(q);
            var k = vc.GetComponent<VanSkin>();            // GO-LIVERY-1: no more livery on the game's car
            if (k != null) UnityEngine.Object.Destroy(k);
            foreach (var (wf, mesh, wr, mats) in s.Wheels)   // GO-VAN-6: the car's own wheels again
            {
                if (wf != null) wf.sharedMesh = mesh;
                if (wr != null) wr.sharedMaterials = mats;
            }
            _skins.Remove(vc);
        }

        /// <summary>A square picture facing outwards at a marker, upright and not mirrored for someone looking at it.</summary>
        internal static GameObject Quad(Transform parent, Vector3 pos, Vector3 normal, float size, Material? mat)
        {
            var go = new GameObject("GO_VanLogo");
            go.transform.SetParent(parent, false);
            var n = normal.normalized;
            var right = Vector3.Cross(n, Vector3.up).normalized * (size / 2f);
            var up = Vector3.up * (size / 2f);
            var mesh = new Mesh { name = "GO_VanLogo" };
            mesh.vertices = new[] { pos - right - up, pos + right - up, pos + right + up, pos - right + up };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.normals = new[] { n, n, n, n };
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>The provider's business logo as a material (the game's square sign picture of the business).</summary>
        internal static Material? Logo()
        {
            var s = Core.S;
            string name = s != null && City.BusinessName(s.HeadendKey, out var n) ? n : "";
            if (name == _logoFor && _logo != null && Time.unscaledTime >= _logoCheck)
            {   // GO-LOGO-2 (user 2026-11-11, the device table: "kein Logo" - a white square): Going Public Server empties the
                // game's logo cache for a business when its logo files arrive again (GameStatePatcher.InvalidateLogoTexture-
                // CacheForBusiness); the material kept the picture of then, white once it is gone. Once a second the game's
                // current picture goes onto the material - every device table, van and label with it updates.
                _logoCheck = Time.unscaledTime + 1f;
                Texture2D? now = null;
                try { now = LogoHelper.GetBusinessLogoTexture(name, LogoSize.SquareSign, true); } catch { }
                if (now != null && now.width >= 16 && _logo.HasProperty("_BaseColorMap") && _logo.GetTexture("_BaseColorMap") != now)
                    _logo.SetTexture("_BaseColorMap", now);
            }
            if (name == _logoFor) return _logo;
            _logoFor = name;
            _logo = null;
            if (name.Length == 0) return null;
            Texture2D? tex = null;
            try { tex = LogoHelper.GetBusinessLogoTexture(name, LogoSize.SquareSign, true); } catch { }
            var sh = Models.LitShader;
            if (tex == null || tex.width < 16 || sh == null) { _logoFor = ""; return null; }   // not made yet: try again later
            _logo = new Material(sh) { name = "GO_VanLogo", hideFlags = HideFlags.DontUnloadUnusedAsset };
            if (_logo.HasProperty("_BaseColorMap")) _logo.SetTexture("_BaseColorMap", tex);
            if (_logo.HasProperty("_BaseColor")) _logo.SetColor("_BaseColor", Color.white);
            if (_logo.HasProperty("_Smoothness")) _logo.SetFloat("_Smoothness", 0.45f);
            return _logo;
        }
    }
}
