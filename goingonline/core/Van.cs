using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Items;
using Extensions;
using UnityEngine;
using Vehicles.VehicleTypes;
using Vehicles.Components;   // GO-VAN-15: VehicleBlinker
using BigAmbitions.InputSystem;   // GO-VAN-15: the game's blinker actions

namespace GoingOnline
{
    /// <summary>GO-VAN-2..5 (user 2026-11-09: "Der Betriebswagen von Going Online war noch nicht fertig"; asked what it should
    /// do, the user chose all four: a real vehicle, standing at the office, driving to the customers, more in the traffic).
    ///
    /// GO-VAN-2, the vehicle: "Servicewagen", our own vehicle type (BAModAPI.ModdingAPI.RegisterModVehicleType - the game keeps
    /// it across catalogue reloads), a copy of the compact "Honza Mimic" with the van's body (the model GO-VAN-1 made for that
    /// very car: same wheelbase, the body at CarHolder/Body/CheapSaloon), the business logo on both sides and the back. The
    /// game loads a player vehicle's model as "Prefabs/Vehicles/PlayerVehicles/&lt;id&gt;.prefab"; our copy waits in that cache.
    /// 14+ boxes, so Going Online counts it as a van. The car dealers that sell vans offer it (GamePatches: their contract
    /// list), in the shop and by phone; the game buys and delivers it as any of its own.
    /// GO-VAN-3, at the office: the vans leased in the app are real ones now - parked in a street slot by the office (the slots
    /// the game hands out to player cars, ParkingLaneGenerator), back to the lessor when the lease ends; they sell for nothing.
    /// GO-VAN-4, to the customers: a technician at a customer's building (a ticket, the drive done) has his vehicle parked
    /// in a street slot there, legally, until the work is done; then it goes back to where it stood at the office. A vehicle
    /// the player drives, or one the player stands next to, is never moved.
    /// GO-VAN-5, the beacon: on a van at work (in the traffic, at a customer) the roof beacon flashes - per renderer slot, as
    /// the cars' brake lights (the game may hand a car its own material copies).
    /// Main thread. Nothing of this on a dedicated server or in the self-test.</summary>
    internal static class Van
    {
        public const string TypeName = "goingonline:vehicletype_govan", Id = "govan";
        const string Template = "ba:vehicletype_honzamimic", TemplatePrefab = "Vehicles/PlayerVehicles/honzamimic";
        const string PrefabKey = "Prefabs/Vehicles/PlayerVehicles/" + Id + ".prefab";
        const BindingFlags SF = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        const BindingFlags IF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        public const float Price = 26500f;
        static readonly Color Teal = new Color(0.20f, 0.42f, 0.48f);
        internal static Color Accent => Teal;   // GO-VAN-16: the leased van models' accent parts

        static GameObject? _holder;
        static bool _typeLogged, _prefabLogged, _prefabWarned;
        static float _prefabRetry;
        static Material? _logo;
        static Texture? _logoTex;

        // ── GO-VAN-2: the vehicle type and its model ────────────────────────────────────────────────────────────────

        /// <summary>From the main menu on, every couple of seconds (GameTypes.Ensure). True once both are in place.</summary>
        static int _mainThread = -1;
        static bool OnMain => _mainThread == System.Threading.Thread.CurrentThread.ManagedThreadId;

        public static bool Ensure()
        {
            _mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            bool type = EnsureType();
            bool model = type && EnsurePrefab();   // GO-VAN-8: on a dedicated server too (the car without its looks)
            if (type) { try { EnsureShowcase(); } catch (System.Exception ex) { Once("showcase|" + ex.Message, "service van: showroom piece: " + ex.Message + " (GO-LIVERY-1)."); } }
            return type && model;
        }

        static bool EnsureType()
        {
            if (VehicleTypeHelper.GetVehicleType(TypeName) != null) return true;
            var tpl = VehicleTypeHelper.GetVehicleType(Template);
            if (tpl == null) return false;   // the catalogue is not loaded yet
            var vt = UnityEngine.Object.Instantiate(tpl);
            vt.name = TypeName;
            vt.hideFlags = HideFlags.HideAndDontSave;
            vt.vehicleTypeName = TypeName;
            vt.price = Price;
            vt.maxCargoCapacity = Mathf.Max(Cat.VanCargo + 2, 16);
            vt.maxFuel = 62f;
            vt.maxSpeed = Mathf.Max(vt.maxSpeed, 70);
            vt.taxDeductible = true;
            vt.isLuxuryCar = false;
            vt.autoParkSupported = true;
            vt.enclosed = true;
            vt.destinationsThatCanDeliver = Mathf.Max(vt.destinationsThatCanDeliver, 1);
            bool ok = false;
            try { ok = BAModAPI.ModdingAPI.RegisterModVehicleType(vt); } catch (System.Exception ex) { Go.Warn("service van: " + ex.Message + " (GO-VAN-2)."); }
            if (!_typeLogged)
            {
                _typeLogged = true;
                Go.Log(ok ? $"service van: vehicle type {TypeName} registered ({Go.Money(Price)}, {vt.maxCargoCapacity} boxes, {vt.maxSpeed} km/h) (GO-VAN-2)."
                          : $"service van: the game did not take the vehicle type {TypeName} (GO-VAN-2).");
            }
            return ok;
        }

        static bool EnsurePrefab()
        {
            if (!(typeof(Helpers.PrefabHelper).GetField("PrefabCache", SF)?.GetValue(null) is Dictionary<string, UnityEngine.Object> cache)) return false;
            if (cache.TryGetValue(PrefabKey, out var have) && have != null) return true;
            if (Time.unscaledTime < _prefabRetry) return false;
            _prefabRetry = Time.unscaledTime + 30f;
            // GO-VAN-8 (the user's server 1 log 2026-11-10: "CreateAndSpawnVehicle failed: Prefab wasn't found:
            // Vehicles/PlayerVehicles/govan"): the dedicated server spawns the players' vehicles itself (parked ones, those of
            // players who are away), so it needs the van in the cache as well - there the template car alone: nothing is
            // drawn on a server, and the colliders are the template's on every machine anyway.
            bool server = Application.isBatchMode;
            var model = server ? null : Models.Get(Id);
            var tpl = Helpers.PrefabHelper.LoadPrefabAssetByName(TemplatePrefab);
            if (tpl == null || (model == null && !server))
            {
                if (!_prefabWarned) { _prefabWarned = true; Go.Warn($"service van: {(tpl == null ? "no template car" : "no model file")} yet - tried again every 30 s (GO-VAN-2)."); }
                return false;
            }
            if (_holder == null)
            {
                _holder = new GameObject("GoingOnline_Vehicles");
                _holder.SetActive(false);   // the copy inside stays active itself, so the game's copies of it are
                UnityEngine.Object.DontDestroyOnLoad(_holder);
            }
            var go = UnityEngine.Object.Instantiate(tpl, _holder.transform);
            go.name = Id;
            // GO-VAN-11 (the user's picture 2026-11-10: the van's hover info said "Honza Mimic, Lager: 0/8" - user: "nicht
            // Honza Mimic sondern ..."): the copy kept the template car's own VehicleType in its controller, which the hover
            // info, the petrol station and the towing read - now ours (its name, 16 boxes, its fuel)
            try
            {
                var vc = go.GetComponent<VehicleController>();
                var own = VehicleTypeHelper.GetVehicleType(TypeName);
                if (vc != null && own != null) vc.vehicleType = own;
            }
            catch (System.Exception ex) { Once("vtype|" + ex.Message, "service van: its vehicle type on the car: " + ex.Message + " (GO-VAN-11)."); }
            if (!server)
            {
                if (!Dress(go.transform, model!))
                {
                    UnityEngine.Object.Destroy(go);
                    if (!_prefabWarned) { _prefabWarned = true; Go.Warn("service van: the template car has no body at CarHolder/Body/CheapSaloon (GO-VAN-2)."); }
                    return false;
                }
                if (go.GetComponent<VanSkin>() == null) go.AddComponent<VanSkin>();   // GO-LIVERY-1: every copy paints its livery
            }
            cache[PrefabKey] = go;
            if (!_prefabLogged)
            {
                _prefabLogged = true;
                int tris = 0;   // GO-VAN-10: which van model a player has, in the log (the model can change without the code)
                if (!server) for (int i = 0; i < model!.Mesh.subMeshCount; i++) tris += (int)(model.Mesh.GetIndexCount(i) / 3);
                Go.Log(server ? $"service van: the template car ready as {PrefabKey} for the server's own spawns (GO-VAN-8)."
                              : $"service van: model ready as {PrefabKey} ({tris} triangles, {model!.Parts.Length} parts) (GO-VAN-2/10).");
            }
            return true;
        }

        /// <summary>The van's body and logos on a compact car (the player's copy or a traffic car).</summary>
        /// <summary>The van's body, wheels and logos on a compact car (the player's copy or a traffic car). GO-VAN-6 (user
        /// 2026-11-09: "nicht so eckig ... auch mit Radkappen"): the body is the rounded second shape, and the car's wheels
        /// (meshes named "...Wheel") become ours - tyre, steel rim and a full hubcap, one for each side (the car's own wheel
        /// transforms keep turning and steering them). <paramref name="wheels"/> collects what was there, for a way back.</summary>
        internal static bool Dress(Transform car, Models.Model model, List<GameObject>? logos = null,
                                   List<(MeshFilter mf, Mesh mesh, MeshRenderer mr, Material[] mats)>? wheels = null)
        {
            var body = car.Find("CarHolder/Body/CheapSaloon");
            var mf = body != null ? body.GetComponent<MeshFilter>() : null;
            var mr = body != null ? body.GetComponent<MeshRenderer>() : null;
            if (mf == null || mr == null) return false;
            Livery.NoteGameMaterial(mr.sharedMaterial);   // GO-LIVERY-3: the game car's own paint material, before ours replaces it
            mf.sharedMesh = model.Mesh;
            mr.sharedMaterials = Models.MaterialsFor(model, Id, Teal, mr.sharedMaterial);
            VanLights.Attach(mr, model);   // GO-VAN-7: headlights, tail and brake lights, blinkers as the game switches them
            Headlights(car);               // GO-VAN-13: the light cones from the van's headlights
            Models.Model? wl = Models.Get("govanwheel_l"), wr = Models.Get("govanwheel_r");
            if (wl != null && wr != null)
                foreach (var f in car.GetComponentsInChildren<MeshFilter>(true))
                {
                    var was = f.sharedMesh;
                    if (was == null || was.name.IndexOf("Wheel", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var r = f.GetComponent<MeshRenderer>();
                    if (r == null) continue;
                    bool left = car.InverseTransformPoint(f.transform.position).x < 0f;   // the game's left looks to -x
                    var w = left ? wl : wr;
                    wheels?.Add((f, was, r, r.sharedMaterials));
                    f.sharedMesh = w.Mesh;
                    r.sharedMaterials = Models.MaterialsFor(w, left ? "govanwheel_l" : "govanwheel_r", Teal, r.sharedMaterial);
                }
            foreach (var kv in model.Markers)
                if (kv.Key.StartsWith("logo"))
                {
                    var q = ServiceVans.Quad(mr.transform, kv.Value.pos, kv.Value.normal, kv.Value.size, LogoMaterial());
                    logos?.Add(q);
                }
            return true;
        }

        internal const string RightSpot = "GO_SpotlightRight";

        /// <summary>GO-VAN-13 (user 2026-11-10: "die Lichtkegel der Scheinwerfer passen noch nicht"): the template car has one
        /// spot for both headlights ("Spotlights", 1.78 m up over its bonnet, 23 deg down, 4 m, 115 deg) - over our higher
        /// bonnet it lit the van itself and the road from the wrong place. It moves to the van's left headlight and a copy of
        /// it to the right one (VanLights switches the copy with it), lower (0.87 m), flatter (11 deg), a little longer and
        /// narrower, each with two thirds of the light.</summary>
        static void Headlights(Transform car)
        {
            var spot = car.Find("Spotlights");
            var light = spot != null ? spot.GetComponent<Light>() : null;
            if (spot == null || light == null) return;
            bool first = Mathf.Abs(spot.localPosition.y - 0.87f) > 0.01f;
            spot.localPosition = new Vector3(-0.60f, 0.87f, 2.43f);
            spot.localRotation = Quaternion.Euler(11f, 0f, 0f);
            if (first) { light.range = 9f; light.spotAngle = 100f; light.intensity *= 0.66f; }
            if (car.Find(RightSpot) == null)
            {
                var copy = UnityEngine.Object.Instantiate(spot.gameObject, spot.parent);
                copy.name = RightSpot;
                copy.transform.localPosition = new Vector3(0.60f, 0.87f, 2.43f);
                copy.transform.localRotation = spot.localRotation;
            }
        }

        /// <summary>One logo material for every van of ours (the player's provider's square sign picture; Going Online's own
        /// sign until there is one) - its picture changes in place, so vans already in the world change with it.</summary>
        internal static Material? LogoMaterial()
        {
            if (_logo != null) return _logo;
            var sh = Models.LitShader;
            if (sh == null) return null;
            _logo = new Material(sh) { name = "GO_VanLogoShared", hideFlags = HideFlags.DontUnloadUnusedAsset };
            if (_logo.HasProperty("_BaseColor")) _logo.SetColor("_BaseColor", Color.white);
            if (_logo.HasProperty("_Smoothness")) _logo.SetFloat("_Smoothness", 0.45f);
            UpdateLogo();
            return _logo;
        }

        static void UpdateLogo()
        {
            if (_logo == null) return;
            Texture? tex = null;
            try { var m = ServiceVans.Logo(); if (m != null && m.HasProperty("_BaseColorMap")) tex = m.GetTexture("_BaseColorMap"); } catch { }
            if (tex == null) { try { tex = Icon.Load("GO_AppIcon.png")?.texture; } catch { } }
            if (tex == _logoTex) return;
            _logoTex = tex;
            if (_logo.HasProperty("_BaseColorMap")) _logo.SetTexture("_BaseColorMap", tex);
        }

        /// <summary>GamePatches: a dealer's contract list - where it sells vans, ours is on it too.</summary>
        internal static void OfferAt(object settings)
        {
            if (VehicleTypeHelper.GetVehicleType(TypeName) == null) return;
            var t = settings.GetType();
            if (!(t.GetField("_vehicles", IF)?.GetValue(settings) is System.Collections.IList list)) return;
            bool vans = false;
            foreach (var v in list)
            {
                string name = v?.GetType().GetProperty("VehicleName")?.GetValue(v) as string ?? "";
                if (name == TypeName) return;
                var vt = name.Length > 0 ? VehicleTypeHelper.GetVehicleType(name) : null;
                if (vt != null && vt.maxCargoCapacity >= Cat.VanCargo) vans = true;
            }
            if (!vans) return;
            var add = t.GetMethod("TryAddVehicleByVehicleType", IF);
            if (add == null) return;
            var ps = add.GetParameters();
            add.Invoke(settings, ps.Length >= 2 ? new object?[] { TypeName, null } : new object?[] { TypeName });
            if (_offered++ < 3) Go.Log($"service van: offered at a car dealer ({list.Count} vehicles on its list) (GO-VAN-2).");
        }
        static int _offered;

        // ── GO-VAN-3/4: leased vans at the office, vans at the customers ──────────────────────────────────────────────

        static float _next;
        static readonly HashSet<string> _warnedOnce = new HashSet<string>();

        static void Once(string key, string msg) { if (_warnedOnce.Add(key)) Go.Log(msg); }

        /// <summary>Main thread, every frame in the city (Core.Tick): the beacons; every 2 s the leases and the trips.</summary>
        public static void Tick(IspState? s)
        {
            if (Application.isBatchMode || Sim.Test) return;
            try { BeaconSwitch(s); } catch (System.Exception ex) { Once("beaconkey|" + ex.Message, "service van: beacon switch: " + ex.Message + " (GO-VAN-15)."); }
            Beacons();
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 2f;
            UpdateLogo();
            if (s == null || string.IsNullOrEmpty(s.HeadendKey) || SaveGameManager.Current == null) return;
            try { Leases(s); } catch (System.Exception ex) { Once("lease|" + ex.Message, "service van: leasing: " + ex.Message + " (GO-VAN-3)."); }
            try { Trips(s); } catch (System.Exception ex) { Once("trip|" + ex.Message, "service van: trips: " + ex.Message + " (GO-VAN-4)."); }
        }

        static VehicleInstance? Find(string id)
        {
            var list = SaveGameManager.Current?.VehicleInstances;
            if (list == null || string.IsNullOrEmpty(id)) return null;
            foreach (var v in list) if (v != null && v.id == id) return v;
            return null;
        }

        static VehicleController? Controller(VehicleInstance v)
        {
            try { return Helpers.VehicleHelper.GetVehicleController(v); } catch { return null; }
        }

        /// <summary>Where the player could see a vehicle move from - far away while inside a building (no street in sight).</summary>
        static Vector3 PlayerPos()
        {
            try { if (BuildingManager.IsInsideBuilding) return new Vector3(1e6f, 0f, 1e6f); } catch { }
            try { var pc = Helpers.PlayerHelper.PlayerController; if (pc != null) return pc.transform.position; } catch { }
            var cam = Camera.main;
            return cam != null ? cam.transform.position : new Vector3(1e6f, 0f, 1e6f);
        }

        static bool Driven(VehicleInstance v) => v.id == (SaveGameManager.Current?.ActiveVehicleId ?? "");

        /// <summary>A vehicle's body renderer (the one our van model sits on), null when it is not in the city now.</summary>
        static Renderer? BodyOf(string id)
        {
            var v = Find(id);
            var ctl = v != null ? Controller(v) : null;
            var body = ctl != null ? ctl.transform.Find("CarHolder/Body/CheapSaloon") : null;
            return body != null ? body.GetComponent<Renderer>() : null;
        }

        // ── GO-VAN-15: the roof beacon by hand ──────────────────────────────────────────────────────────────────────────

        static float _lAt = -9f, _rAt = -9f, _comboAt = -9f;
        static bool _stableL, _stableR, _keyLogged;
        static FieldInfo? _fLeft, _fRight;
        static VehicleBlinker? _bl;
        static string _blFor = "";

        /// <summary>GO-VAN-15 (user 2026-11-10: "mit den Tasten Q und E gleichzeitig aktivieren und deaktivieren"): in a service
        /// van the player drives, both blinker keys at once (the game's own actions, so a changed key binding counts) switch
        /// the roof beacon on or off. The two presses also toggled the blinkers - they go back to how they were before.</summary>
        static void BeaconSwitch(IspState? s)
        {
            var g = SaveGameManager.Current;
            if (g == null || s == null) return;
            string id = g.ActiveVehicleId ?? "";
            if (id.Length == 0) return;
            var v = Find(id);
            if (v == null || v.vehicleTypeName != TypeName) return;
            bool lp = PlayerAction.VehicleLeftBlinker.Pressed(), rp = PlayerAction.VehicleRightBlinker.Pressed();
            float t = Time.unscaledTime;
            if (_blFor != id || _bl == null)
            {   // the van's blinker component, looked up once per van (every frame otherwise)
                var ctl = Controller(v);
                var cc = ctl != null ? ctl.GetComponentInParent<CarController>() : null;
                _bl = cc != null ? cc.GetComponentInChildren<VehicleBlinker>(true) : ctl != null ? ctl.GetComponentInChildren<VehicleBlinker>(true) : null;
                _blFor = _bl != null ? id : "";
            }
            var bl = _bl;
            if (!lp && !rp)
            {   // what the blinkers were while no key was near - the state to go back to
                if (t - Mathf.Max(_lAt, _rAt) > 0.4f && bl != null) ReadBlinkers(bl, out _stableL, out _stableR);
                return;
            }
            if (lp) _lAt = t;
            if (rp) _rAt = t;
            if (Mathf.Abs(_lAt - _rAt) > 0.25f || t - _comboAt < 0.6f) return;
            _comboAt = t;
            bool on = !s.BeaconOn.Contains(id);
            if (on) s.BeaconOn.Add(id); else s.BeaconOn.Remove(id);
            _scanNext = 0f;   // the beacon follows at once
            if (bl != null) RestoreBlinkers(bl, _stableL, _stableR);
            if (!_keyLogged) { _keyLogged = true; Go.Log($"service van: roof beacon switched {(on ? "on" : "off")} by the blinker keys together (GO-VAN-15)."); }
        }

        static void ReadBlinkers(VehicleBlinker bl, out bool left, out bool right)
        {
            _fLeft ??= typeof(VehicleBlinker).GetField("_isLeftBlinkerOn", IF);
            _fRight ??= typeof(VehicleBlinker).GetField("_isRightBlinkerOn", IF);
            left = _fLeft != null && (bool)_fLeft.GetValue(bl);
            right = _fRight != null && (bool)_fRight.GetValue(bl);
        }

        static void RestoreBlinkers(VehicleBlinker bl, bool left, bool right)
        {
            if (_fLeft == null || _fRight == null) return;
            for (int i = 0; i < 3; i++)
            {
                ReadBlinkers(bl, out bool l, out bool r);
                if (l == left && r == right) return;
                if (l != left) bl.ToggleLeftBlinker(); else bl.ToggleRightBlinker();
            }
        }

        /// <summary>GO-VAN-12 (user 2026-11-10: "die geleasten Service Wagen sollen nicht fahrbar sein, nur die gekauften"):
        /// leased vans are no vehicles of the game any more - they drive on the city map only (VanPins), and the fleet counts
        /// them as before (Fleet.AtHeadend: "lease-van-N"). The real ones GO-VAN-3 made go back to the lessor - not the one
        /// the player sits in, nor one next to the player (it goes once the player is away).</summary>
        static void Leases(IspState s)
        {
            if (s.LeaseIds.Count == 0) return;
            Vector3 me = PlayerPos();
            for (int i = s.LeaseIds.Count - 1; i >= 0; i--)
            {
                var v = Find(s.LeaseIds[i]);
                if (v == null) { s.LeaseIds.RemoveAt(i); continue; }
                if (Driven(v)) continue;
                var ctl = Controller(v);
                Vector3 at = ctl != null ? ctl.transform.position : (Vector3)v.position;
                if ((at - me).sqrMagnitude < 25f * 25f) continue;
                Helpers.VehicleHelper.Delete(v, ctl);
                s.LeaseIds.RemoveAt(i);
                s.VanHome.Remove(v.id);
                s.VanAway.Remove(v.id);
                Go.Log($"service van: a leased van went back to the lessor - leased vans drive on the city map only now ({s.LeaseIds.Count} left) (GO-VAN-12).");
                return;
            }
        }

        /// <summary>GO-VAN-4: each vehicle of a technician at a customer stands at that customer; the others at the office.</summary>
        static void Trips(IspState s)
        {
            var want = new Dictionary<string, string>();   // vehicle id -> the building it should stand at
            foreach (var t in s.Techs)
            {
                if (t == null || string.IsNullOrEmpty(t.CarId) || t.CarId.StartsWith("lease-") || t.CarId.StartsWith("test-")) continue;
                if (t.Ticket != 0 && t.Travel <= 0f && t.At.Length > 0 && t.At != s.HeadendKey) want[t.CarId] = t.At;
            }
            Vector3 me = PlayerPos();
            // out to a customer (one per round, so nothing jumps at once)
            foreach (var kv in want)
            {
                if (s.VanAway.TryGetValue(kv.Key, out var there) && there == kv.Value) continue;
                var v = Find(kv.Key);
                if (v == null || Driven(v)) continue;
                var ctl = Controller(v);
                if (ctl == null) continue;
                Vector3 from = ctl.transform.position;
                if ((from - me).sqrMagnitude < 30f * 30f) continue;   // the player stands next to it: it waits
                var door = City.PosOf(kv.Value);
                if (door == Vector3.zero) continue;
                if (!Parking.TryFind(door, 5.2f, ctl.gameObject, out var pos, out var rot)) continue;
                if (!s.VanAway.ContainsKey(v.id)) s.VanHome[v.id] = new[] { from.x, from.y, from.z, ctl.transform.eulerAngles.y };
                Place(v, ctl, pos, rot);
                s.VanAway[v.id] = kv.Value;
                Once("trip-first", $"service van: a technician's vehicle drove to a customer (GO-VAN-4).");
                return;
            }
            // back to the office once the work there is done (or the player took the vehicle)
            foreach (var kv in new List<KeyValuePair<string, string>>(s.VanAway))
            {
                if (want.TryGetValue(kv.Key, out var still) && still == kv.Value) continue;
                var v = Find(kv.Key);
                if (v == null || Driven(v)) { s.VanAway.Remove(kv.Key); s.VanHome.Remove(kv.Key); continue; }
                if (want.ContainsKey(kv.Key)) continue;   // straight on to the next customer (above, next round)
                var ctl = Controller(v);
                if (ctl == null) continue;
                if ((ctl.transform.position - me).sqrMagnitude < 30f * 30f) continue;
                Vector3 pos; Quaternion rot;
                if (s.VanHome.TryGetValue(v.id, out var h) && h != null && h.Length >= 4 && Parking.Free(new Vector3(h[0], h[1], h[2]), Quaternion.Euler(0f, h[3], 0f), ctl.gameObject))
                { pos = new Vector3(h[0], h[1], h[2]); rot = Quaternion.Euler(0f, h[3], 0f); }
                else
                {
                    var door = City.PosOf(s.HeadendKey);
                    if (door == Vector3.zero || !Parking.TryFind(door, 5.2f, ctl.gameObject, out pos, out rot)) continue;
                }
                Place(v, ctl, pos, rot);
                s.VanAway.Remove(kv.Key);
                s.VanHome.Remove(kv.Key);
                return;
            }
        }

        static void Place(VehicleInstance v, VehicleController ctl, Vector3 pos, Quaternion rot)
        {
            Helpers.VehicleHelper.TeleportVehicleToGround(ctl, pos, rot);
            v.position = ctl.transform.position;
            v.rotation = ctl.transform.rotation;
            v.parkingState = Helpers.ParkingState.Legal;   // a service vehicle at work: no ticket
            // GO-VAN-16b: the slot search leaves the game's parked cars out (as the game's own) - those in the van's place go,
            // as the game does after its own placing of player vehicles
            try { if (ctl.vehicleType != null) Helpers.VehicleHelper.DestroyBlockingVehicles(ctl.gameObject, ctl.vehicleType, onlyParkedVehicles: true); } catch { }
        }

        // ── GO-LIVERY-1: the livery on a van ───────────────────────────────────────────────────────────────────────────

        /// <summary>VanSkin, now and then: the livery the van should show (its owner's, in the colour the game gave the
        /// vehicle), on the paint's material slot. Nothing while the car does not wear the van's body.</summary>
        internal static void Skin(VanSkin k)
        {
            var body = k.transform.Find("CarHolder/Body/CheapSaloon");
            var mf = body != null ? body.GetComponent<MeshFilter>() : null;
            var mr = body != null ? body.GetComponent<MeshRenderer>() : null;
            var model = Models.Get(Id);
            if (mf == null || mr == null || model == null || mf.sharedMesh != model.Mesh) return;
            try { Plates.Apply(k, mr, model); } catch (System.Exception ex) { Once("plate|" + ex.Message, "number plates: " + ex.Message + " (GO-PLATE-1)."); }
            int slot = model.PartIndex("van_white");
            var mats = mr.sharedMaterials;
            if (slot < 0 || slot >= mats.Length) return;
            int paint = k.FixedPaint >= 0 ? k.FixedPaint : PaintOf(k);
            if (k.FixedPaint >= 0) Livery.Tint(mr, paint);   // GO-LIVERY-3: the game's paint takes the tint - the traffic's vans stay white
            var spec = Livery.SpecFor(k.gameObject, k.Showroom, out var logo, out var logoKey);
            string key = Livery.KeyOf(spec, paint, logoKey);
            if (key == k.Key && mats[slot] != null) { Livery.Touch(key); return; }
            var template = Models.MaterialsFor(model, Id, Teal, null)[slot];
            var mat = Livery.MaterialFor(spec, paint, logo, logoKey, template);
            if (mat == null) return;                       // one is painted per frame: the next round
            mats[slot] = mat;
            mr.sharedMaterials = mats;
            k.Key = key;
            // the small logo signs of the first look stay for "logo only" on the player's own vans
            bool own = string.IsNullOrEmpty(SafeOwner(k.gameObject));
            bool signs = own && Livery.Plain(spec);
            foreach (Transform ch in mr.transform) if (ch.name == "GO_VanLogo" && ch.gameObject.activeSelf != signs) ch.gameObject.SetActive(signs);
        }

        static string? SafeOwner(GameObject go) { try { return Livery.OwnerOf?.Invoke(go); } catch { return null; } }

        /// <summary>The van's own paint material (the one each livery copies), null without the model.</summary>
        internal static Material? PaintTemplate()
        {
            var model = Models.Get(Id);
            if (model == null) return null;
            int slot = model.PartIndex("van_white");
            return slot < 0 ? null : Models.MaterialsFor(model, Id, Teal, null)[slot];
        }

        /// <summary>The colour the game gave the vehicle (the dealer's colour choice; CarFeatures.SetColor), else white.</summary>
        static int PaintOf(VanSkin k)
        {
            var cf = k.GetComponent<CarFeatures>();
            if (cf == null) cf = k.GetComponentInChildren<CarFeatures>();
            var vc = cf != null ? cf.VehicleColor : null;
            if (vc == null) return Livery.White;
            Color32 t = vc.tint;
            return (t.r << 16) | (t.g << 8) | t.b;
        }

        /// <summary>The game's whitest vehicle colour (its name), "" when the list is not there.</summary>
        internal static string WhiteColor()
        {
            try
            {
                var list = InstanceBehavior<GlobalReferences>.Instance?.vehicleColors;
                if (list == null) return "";
                string best = ""; float lum = -1f;
                foreach (var c in list)
                {
                    if (c == null) continue;
                    float l = 0.2126f * c.tint.r + 0.7152f * c.tint.g + 0.0722f * c.tint.b - 0.6f * (Mathf.Max(c.tint.r, Mathf.Max(c.tint.g, c.tint.b)) - Mathf.Min(c.tint.r, Mathf.Min(c.tint.g, c.tint.b)));
                    if (l > lum) { lum = l; best = c.name; }
                }
                return best;
            }
            catch { return ""; }
        }

        // ── GO-LIVERY-1: the van in the dealers' showrooms ─────────────────────────────────────────────────────────────

        public const string ShowcaseItem = "goingonline:itemname_govanshowcase";
        const string ShowcaseTemplate = "ba:itemname_honzamimicshowcase", ShowcasePrefab = "honzamimicshowcase";
        const string ShowcaseKey = "Prefabs/govanshowcase.prefab";
        static BigAmbitions.Items.Item? _showItem;
        static bool _showLogged, _showWarned;
        static float _showRetry;

        /// <summary>The showroom piece: a copy of the Honza Mimic's (the van's own chassis) that shows and sells our van -
        /// the item, and (with graphics) its model in the game's prefab cache.</summary>
        static bool EnsureShowcase()
        {
            if (ItemsGetter.GetByName(ShowcaseItem, true) == null)
            {
                var src = ItemsGetter.GetByName(ShowcaseTemplate, true);
                if (src == null) return false;
                if (_showItem == null)
                {
                    _showItem = UnityEngine.Object.Instantiate(src);
                    _showItem.name = ShowcaseItem;
                    _showItem.itemName = ShowcaseItem;
                    _showItem.vehicleType = TypeName;
                    _showItem.hideFlags = HideFlags.HideAndDontSave;
                }
                ItemsGetter.RegisterModItem(_showItem);
                if (ItemsGetter.GetByName(ShowcaseItem, true) == null) return false;
            }
            if (Application.isBatchMode) return true;
            if (!(typeof(Helpers.PrefabHelper).GetField("PrefabCache", SF)?.GetValue(null) is Dictionary<string, UnityEngine.Object> cache)) return false;
            if (cache.TryGetValue(ShowcaseKey, out var have) && have != null) return true;
            if (Time.unscaledTime < _showRetry) return false;
            _showRetry = Time.unscaledTime + 30f;
            var model = Models.Get(Id);
            var tpl = Helpers.PrefabHelper.LoadPrefabAssetByName(ShowcasePrefab);
            if (model == null || tpl == null)
            {
                if (!_showWarned) { _showWarned = true; Go.Warn($"service van: no {(model == null ? "model" : "showroom car of the game")} for the showroom piece yet - tried again every 30 s (GO-LIVERY-1)."); }
                return false;
            }
            if (_holder == null)
            {
                _holder = new GameObject("GoingOnline_Vehicles");
                _holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_holder);
            }
            var go = UnityEngine.Object.Instantiate(tpl, _holder.transform);
            go.name = "govanshowcase";
            var show = go.GetComponent<Controllers.ShowcaseVehicleController>();
            if (show == null || !Dress(go.transform, model))
            {
                UnityEngine.Object.Destroy(go);
                if (!_showWarned) { _showWarned = true; Go.Warn("service van: the game's showroom car is not built as it was - no showroom piece (GO-LIVERY-1)."); }
                return false;
            }
            show.vehicleName = TypeName;
            show.itemName = ShowcaseItem;
            var sign = go.transform.Find("SM_ShopSign");                 // the price sign stood on the low car's bonnet
            if (sign != null) sign.localPosition += new Vector3(0f, 0.24f, -0.02f);
            var box = go.GetComponent<BoxCollider>();                     // the van is higher than the car: click it anywhere
            if (box != null && box.size.y < 1.95f)
            {
                float bottom = box.center.y - box.size.y * 0.5f;
                box.size = new Vector3(box.size.x, 1.95f - bottom, box.size.z);
                box.center = new Vector3(box.center.x, bottom + box.size.y * 0.5f, box.center.z);
            }
            var skin = go.GetComponent<VanSkin>() ?? go.AddComponent<VanSkin>();
            skin.Showroom = true;
            cache[ShowcaseKey] = go;
            if (!_showLogged) { _showLogged = true; Go.Log($"service van: showroom piece ready ({ShowcaseItem}) (GO-LIVERY-1)."); }
            return true;
        }

        /// <summary>Before a dealer's list or showroom is built: the piece must be there now (else the game's code meets a
        /// missing item).</summary>
        internal static bool EnsureShowcaseNow()
        {
            _showRetry = 0f;
            return EnsureType() && EnsureShowcase();
        }

        /// <summary>The dealers that sell vans and where the piece stands (the game's layouts, measured 2026-11-10): the
        /// cheap dealer shows the Honza Mimic twice - its second one becomes ours; the two truck dealers get one more
        /// next to their vans, on floor that is free in their layout.</summary>
        static readonly (string layout, float x, float z, float yaw, bool replace)[] Spots =
        {
            ("GarmentDistrictCarDealershipCheap", 1004.5f, -97.0f, 0f, true),
            ("GarmentDistrictCarDealershipTrucks", 1034.5f, -165.0f, 90f, false),
            ("IndustryCityCarDealershipTrucks", 1043.5f, -157.4f, 90f, false),
        };

        /// <summary>GamePatches: a layout was loaded (the game keeps it): the piece goes in when the dealer is one of ours.</summary>
        internal static void Showroom(global::BusinessLayoutSet? set)
        {
            if (set?.Items == null || string.IsNullOrEmpty(set.LayoutName) || !OnMain) return;   // the game's next call on the main thread does it
            foreach (var sp in Spots)
            {
                if (set.LayoutName != sp.layout) continue;
                foreach (var it in set.Items) if (it != null && it.itemName == ShowcaseItem) return;   // done before
                if (!EnsureShowcaseNow()) return;
                string white = WhiteColor();
                if (sp.replace)
                {
                    BusinessLayoutSets.Item? best = null; float d = 2f;
                    foreach (var it in set.Items)
                    {
                        if (it == null || it.itemName != ShowcaseTemplate) continue;
                        float dd = Mathf.Abs(it.position.x - sp.x) + Mathf.Abs(it.position.z - sp.z);
                        if (dd < d) { d = dd; best = it; }
                    }
                    if (best == null) { Once("showroom-miss|" + sp.layout, $"service van: {sp.layout} has no Honza Mimic where it was - no showroom piece there (GO-LIVERY-1)."); return; }
                    best.itemName = ShowcaseItem;
                    best.playerItemPurchaserSettings ??= new BigAmbitions.Items.PlayerItemPurchaserSettings { enabled = true, itemQuantity = 1 };
                    best.playerItemPurchaserSettings.itemName = ShowcaseItem;
                    best.playerItemPurchaserSettings.enabled = true;
                    if (white.Length > 0) best.customValue = white;
                }
                else
                {
                    set.Items.Add(new BusinessLayoutSets.Item
                    {
                        id = "goingonline-govanshowcase-" + sp.layout,
                        itemName = ShowcaseItem,
                        position = new Vector3(sp.x, 0f, sp.z),
                        rotation = Quaternion.Euler(0f, sp.yaw, 0f),
                        playerItemPurchaserSettings = new BigAmbitions.Items.PlayerItemPurchaserSettings { enabled = true, itemName = ShowcaseItem, itemQuantity = 1 },
                        customValue = white.Length > 0 ? white : null,
                    });
                }
                Once("showroom|" + sp.layout, $"service van: in the showroom of {sp.layout} ({(sp.replace ? "instead of the second Honza Mimic" : "next to the vans")}) (GO-LIVERY-1).");
                return;
            }
        }

        /// <summary>GamePatches: an interior is about to be built from a layout. Holds the piece? Then its item and model must
        /// be there now - if they cannot be, the layout gets the game's own car back (an interior never breaks over it).</summary>
        internal static void BeforeInterior(global::BusinessLayoutSet? set)
        {
            if (set?.Items == null || !OnMain) return;
            bool has = false;
            foreach (var it in set.Items) if (it != null && it.itemName == ShowcaseItem) { has = true; break; }
            if (!has || EnsureShowcaseNow()) return;
            for (int i = set.Items.Count - 1; i >= 0; i--)
            {
                var it = set.Items[i];
                if (it == null || it.itemName != ShowcaseItem) continue;
                if (it.id != null && it.id.StartsWith("goingonline-")) { set.Items.RemoveAt(i); continue; }
                it.itemName = ShowcaseTemplate;
                if (it.playerItemPurchaserSettings != null) it.playerItemPurchaserSettings.itemName = ShowcaseTemplate;
            }
            Once("showroom-back|" + set.LayoutName, $"service van: the showroom piece was not ready - {set.LayoutName} shows the game's cars this time (GO-LIVERY-1).");
        }

        // ── GO-VAN-5: the roof beacon ──────────────────────────────────────────────────────────────────────────────────

        static readonly List<(Renderer r, int slot, bool right)> _beacons = new List<(Renderer, int, bool)>();
        static readonly HashSet<Renderer> _beaconScanned = new HashSet<Renderer>();
        static MaterialPropertyBlock? _mpb;
        static readonly int EmissiveId = Shader.PropertyToID("_EmissiveColor"), ExposureId = Shader.PropertyToID("_EmissiveExposureWeight");
        static float _beaconNext, _scanNext;
        static readonly HashSet<Renderer> _working = new HashSet<Renderer>();

        /// <summary>A van body's renderer flashes while the van works (a traffic van; a van parked at a customer).</summary>
        internal static void Working(Renderer r, bool on)
        {
            if (r == null) return;
            if (on) { _working.Add(r); Adopt(r); } else _working.Remove(r);
        }

        /// <summary>A traffic car that is a car again: its beacon slots go (the slot numbers now mean the car's own materials).</summary>
        internal static void Forget(Renderer r)
        {
            if (r == null) return;
            _working.Remove(r);
            _beaconScanned.Remove(r);
            _mpb ??= new MaterialPropertyBlock();
            for (int i = _beacons.Count - 1; i >= 0; i--)
            {
                if (_beacons[i].r != r) continue;
                try { r.GetPropertyBlock(_mpb, _beacons[i].slot); _mpb.Clear(); r.SetPropertyBlock(_mpb, _beacons[i].slot); } catch { }
                _beacons.RemoveAt(i);
            }
        }

        static readonly HashSet<Renderer> _awayLit = new HashSet<Renderer>();

        static void Adopt(Renderer r)
        {
            if (!_beaconScanned.Add(r)) return;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string n = mats[i] != null ? mats[i].name : "";
                if (n.StartsWith("GO_beacon")) _beacons.Add((r, i, n.StartsWith("GO_beacon_r")));
            }
        }

        static void Beacons()
        {
            float t = Time.unscaledTime;
            if (t >= _scanNext)
            {
                _scanNext = t + 3f;
                _working.RemoveWhere(x => x == null);
                // the player's own vans parked at a customer flash; back at the office they stop. GO-VAN-15: the one the
                // player drives only by hand (Q+E), and those switched on by hand always
                var now = new HashSet<Renderer>();
                var s = Core.S;
                string driven = SaveGameManager.Current?.ActiveVehicleId ?? "";
                if (s != null)
                {
                    foreach (var id in s.VanAway.Keys)
                        if (id != driven && BodyOf(id) is Renderer r) { now.Add(r); Working(r, true); }
                    s.BeaconOn.RemoveAll(id => Find(id) == null);   // sold, or gone
                    foreach (var id in s.BeaconOn)
                        if (BodyOf(id) is Renderer r) { now.Add(r); Working(r, true); }
                }
                foreach (var r in _awayLit) if (r != null && !now.Contains(r)) Working(r, false);
                _awayLit.Clear();
                foreach (var r in now) _awayLit.Add(r);
            }
            if (t < _beaconNext || _beacons.Count == 0) return;
            _beaconNext = t + 0.04f;
            // GO-VAN-15: the left lamp a double flash, then the right one - 0.8 s for both (a model without the split:
            // its one lamp flashes as the left one)
            float ph = t % 0.8f;
            bool litL = ph < 0.07f || (ph >= 0.13f && ph < 0.20f);
            bool litR = (ph >= 0.40f && ph < 0.47f) || (ph >= 0.53f && ph < 0.60f);
            var on = new Vector4(1f, 0.42f, 0.02f, 1f) * 6f;
            var off = new Vector4(0.02f, 0.008f, 0f, 1f);
            _mpb ??= new MaterialPropertyBlock();
            for (int i = _beacons.Count - 1; i >= 0; i--)
            {
                var (r, slot, right) = _beacons[i];
                if (r == null) { _beacons.RemoveAt(i); continue; }
                if (!r.isVisible) continue;
                r.GetPropertyBlock(_mpb, slot);
                _mpb.SetVector(EmissiveId, (right ? litR : litL) && _working.Contains(r) ? on : off);
                _mpb.SetFloat(ExposureId, 0f);
                r.SetPropertyBlock(_mpb, slot);
            }
        }

        // ── street parking slots (as Going Public Server's ParkingSpots, by reflection: the stand-alone mod has no publicizer) ──

        internal static class Parking
        {
            static readonly float[] Radii = { 25f, 60f, 120f };
            static readonly Collider[] _lanes = new Collider[96];
            static readonly Collider[] _hits = new Collider[16];
            static FieldInfo? _dist, _side, _col, _segs;
            static PropertyInfo? _span, _curved;
            static MethodInfo? _pose, _sync;
            static bool _looked;
            static int _laneMask = -1, _freeMask = -1, _groundMask = -1;

            static bool Look()
            {
                if (_looked) return _pose != null;
                _looked = true;
                var t = typeof(ParkingLaneGenerator);
                _dist = t.GetField("parkingSpotDistance", IF); _side = t.GetField("sideBySideParkingSpots", IF);
                _col = t.GetField("_collider", IF); _segs = t.GetField("_segments", IF);
                _span = t.GetProperty("SlotSpan", IF); _curved = t.GetProperty("IsCurved", IF);
                _pose = t.GetMethod("GetPlayerSpotPose", IF); _sync = t.GetMethod("SyncCurveSegments", IF);
                if (_pose == null || _dist == null || _col == null || _span == null) Go.Warn("service van: the street parking lanes are not where they were - no parking (GO-VAN-3).");
                try
                {
                    _laneMask = Helpers.LayerHelper.parkingAreaLayerMask;
                    _freeMask = Helpers.LayerHelper.freeParkingSpotDetectionMask;
                    _groundMask = Helpers.LayerHelper.groundLayerMask;
                }
                catch { _laneMask = LayerMask.GetMask("ParkingArea"); _freeMask = LayerMask.GetMask("PlayerVehicles", "Vehicles", "Human"); _groundMask = LayerMask.GetMask("Ground"); }
                return _pose != null;
            }

            /// <summary>The nearest free player slot of a street parking lane around <paramref name="near"/> (no handicapped or
            /// delivery lanes); its pose on the ground.</summary>
            internal static bool TryFind(Vector3 near, float length, GameObject? self, out Vector3 pos, out Quaternion rot, float maxRadius = 120f)
            {
                pos = default; rot = Quaternion.identity;
                if (!Look()) return false;
                Physics.SyncTransforms();
                foreach (float radius in Radii)
                {
                    if (radius > maxRadius) break;
                    int n = Physics.OverlapSphereNonAlloc(near, radius, _lanes, _laneMask, QueryTriggerInteraction.Collide);
                    var lanes = new HashSet<ParkingLaneGenerator>();
                    for (int i = 0; i < n; i++)
                    {
                        var lane = _lanes[i] != null ? _lanes[i].GetComponentInParent<ParkingLaneGenerator>() : null;
                        if (lane != null) lanes.Add(lane);
                    }
                    float best = float.MaxValue;
                    bool found = false;
                    foreach (var lane in lanes)
                    {
                        try
                        {
                            if (lane.isHandicapParking || lane.ContainsDeliveryVehicle()) continue;
                            var col = _col!.GetValue(lane) as BoxCollider;
                            if (col == null) continue;
                            float dist = (float)_dist!.GetValue(lane);
                            bool side = _side != null && (bool)_side.GetValue(lane);
                            if (side && length > col.size.z + 0.5f) continue;
                            if (dist <= 0.1f) continue;
                            if (_curved != null && (bool)_curved.GetValue(lane) && _segs?.GetValue(lane) is System.Collections.ICollection sg && sg.Count == 0) _sync?.Invoke(lane, null);
                            int slots = Mathf.FloorToInt((float)_span!.GetValue(lane) / dist);
                            for (int k = 0; k < slots; k++)
                            {
                                var args = new object?[] { k, null, null };
                                _pose!.Invoke(lane, args);
                                var c = (Vector3)args[1]!; var r = (Quaternion)args[2]!;
                                float d = (c - near).sqrMagnitude;
                                if (d >= best || Mathf.Abs(c.y - near.y) > 6f) continue;
                                var half = side ? new Vector3(dist * 0.5f, 0.5f, col.size.z * 0.5f)
                                                : new Vector3(col.size.x * 0.5f, 0.5f, Mathf.Max(dist * 0.5f, length * 0.5f + 0.3f));
                                if (Occupied(c, half, r, self)) continue;
                                best = d; pos = c; rot = r; found = true;
                            }
                        }
                        catch { }
                    }
                    if (found) { Ground(ref pos, ref rot); return true; }
                }
                return false;
            }

            /// <summary>Nothing stands where a vehicle of ours would go (its own body aside).</summary>
            internal static bool Free(Vector3 pos, Quaternion rot, GameObject? self)
            {
                if (!Look()) return false;
                Physics.SyncTransforms();
                return !Occupied(pos + Vector3.up * 0.6f, new Vector3(1.0f, 0.5f, 2.6f), rot, self);
            }

            static bool Occupied(Vector3 center, Vector3 half, Quaternion rot, GameObject? self)
            {
                int n = Physics.OverlapBoxNonAlloc(center, half, _hits, rot, _freeMask, QueryTriggerInteraction.Ignore);
                if (n >= _hits.Length) return true;
                for (int i = 0; i < n; i++)
                {
                    var col = _hits[i];
                    if (col == null || (self != null && col.transform.IsChildOf(self.transform))) continue;
                    return true;
                }
                return false;
            }

            static void Ground(ref Vector3 pos, ref Quaternion rot)
            {
                if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var hit, 4f, _groundMask, QueryTriggerInteraction.Ignore))
                {
                    pos = hit.point;
                    rot = Quaternion.FromToRotation(Vector3.up, hit.normal) * rot;
                }
            }
        }
    }
}
