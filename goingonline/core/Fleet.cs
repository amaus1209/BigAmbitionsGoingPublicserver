using System.Collections.Generic;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>The provider's vehicles and workplaces. Vehicles are the player's own, bought at the game's dealers and parked
    /// at the headend (within 80 m of its door); the one being driven does not count, nor do other players' cars on a server
    /// (ids "BAMP_..."). A vehicle with a cargo capacity from 14 on is a van (the dealers, read 2026-11-06: UMC Desert 8,000 $,
    /// Vord V150 44,000 $, Mersaidi Dash 48,500 $, UMC Nunavut 65,000 $, Vord Courier D500 72,500 $) - for technicians and switching staff; network masters take any car. Workplaces (desk, PC, headset) are
    /// set up in the app. Both serve one person per shift. User 2026-11-06: "Netzmeister Arbeitsplatz und Auto, Techniker
    /// braucht Auto, Schalter Auto, Kundenmitarbeiter Arbeitsplatz, technische Entstörung Arbeitsplatz".</summary>
    internal static class Fleet
    {
        public sealed class Car { public string Id = "", Name = ""; public bool Van; public int Cargo; }

        static readonly List<Car> Cars = new List<Car>();     // the player's own, looked at every few seconds
        static readonly List<Car> All = new List<Car>();      // ... and the leased ones
        static float _at = -100f;
        static string _for = "";
        public static int TestVans, TestCars;   // the self-test has no real vehicles

        /// <summary>The vehicles at the headend now: the player's own (looked at every few seconds) and the leased ones
        /// (user 2026-11-06: the start with little money - a leased van instead of buying one at a dealer).</summary>
        public static List<Car> AtHeadend(IspState s)
        {
            All.Clear();
            All.AddRange(Own(s));
            // GO-VAN-3: leased vans are real service vans at the office (in Own); only those not standing there yet count here
            int pending = Sim.Test ? s.LeaseVans : Mathf.Max(0, s.LeaseVans - s.LeaseIds.Count);
            for (int i = 0; i < pending; i++) All.Add(new Car { Id = "lease-van-" + i, Name = Go.T("go_lease_van_name", "leased van"), Van = true, Cargo = 24 });
            for (int i = 0; i < s.LeaseCars; i++) All.Add(new Car { Id = "lease-car-" + i, Name = Go.T("go_lease_car_name", "leased car"), Van = false, Cargo = 8 });
            return All;
        }

        static List<Car> Own(IspState s)
        {
            if (Sim.Test)
            {
                Cars.Clear();
                for (int i = 0; i < TestVans; i++) Cars.Add(new Car { Id = "test-van-" + i, Name = "Mersaidi Dash", Van = true, Cargo = 24 });
                for (int i = 0; i < TestCars; i++) Cars.Add(new Car { Id = "test-car-" + i, Name = "Honza Mimic", Van = false, Cargo = 8 });
                return Cars;
            }
            if (Time.unscaledTime - _at < 5f && _for == s.HeadendKey) return Cars;
            _at = Time.unscaledTime; _for = s.HeadendKey;
            Cars.Clear();
            try
            {
                var g = SaveGameManager.Current;
                if (g == null || g.VehicleInstances == null) return Cars;
                // GO-HUBS-1: vehicles parked at any office of the provider - the headend and the hubs
                var offices = new List<Vector3>();
                foreach (var key in Places.AllOffices(s)) { var p = City.PosOf(key); if (p != Vector3.zero) offices.Add(p); }
                if (offices.Count == 0) return Cars;
                string active = g.ActiveVehicleId ?? "";
                foreach (var v in g.VehicleInstances)
                {
                    if (v == null || string.IsNullOrEmpty(v.id) || v.id.StartsWith("BAMP_") || v.id == active) continue;
                    string type = v.vehicleTypeName ?? "";
                    if (type.Length == 0 || type.Contains("scooter") || type.Contains("handtruck") || type.Contains("flatbed") || type.Contains("deliveryjob")) continue;
                    Vector3 pos = v.position;
                    try
                    {
                        var ctl = Helpers.VehicleHelper.GetVehicleController(v);
                        if (ctl != null) pos = ctl.transform.position;   // where it stands now, not at the last save
                    }
                    catch { }
                    bool near = s.VanAway.ContainsKey(v.id) || s.LeaseIds.Contains(v.id);   // GO-VAN-4: out at a customer, still the office's
                    if (!near) foreach (var o in offices) { var d = pos - o; d.y = 0f; if (d.magnitude <= Cat.FleetRadius) { near = true; break; } }
                    if (!near) continue;
                    int cargo = 0;
                    try { cargo = Vehicles.VehicleTypes.VehicleTypeHelper.GetVehicleType(type)?.maxCargoCapacity ?? 0; } catch { }
                    Cars.Add(new Car { Id = v.id, Name = Go.Game(type), Van = cargo >= Cat.VanCargo, Cargo = cargo });
                }
            }
            catch (System.Exception ex) { Go.Warn("fleet: " + ex.Message); }
            return Cars;
        }

        public static int Vans(IspState s) { int n = 0; foreach (var c in AtHeadend(s)) if (c.Van) n++; return n; }
        public static int Others(IspState s) { int n = 0; foreach (var c in AtHeadend(s)) if (!c.Van) n++; return n; }

        /// <summary>How many people a building has room for, by its kind and size (user 2026-11-06: "Kundencenter für die Leute,
        /// Anzahl der Personen vom Gebäudetyp"): one workplace per 10 m² in an office, per 15 m² in a shop, per 25 m² in a
        /// warehouse; 2 to 400.</summary>
        public static int Capacity(string key)
        {
            if (!City.ByKey.TryGetValue(key, out var b)) return 0;
            int per = b.Kind == Cat.KindOffice ? 10 : b.Kind == Cat.KindRetail ? 15 : b.Kind == Cat.KindWarehouse ? 25 : 0;
            return per == 0 ? 0 : Mathf.Clamp(b.Sqm / per, 2, 400);
        }

        /// <summary>Room for workplaces at the headend and in the customer center - one room when both are the same office
        /// (GO-OFFICE-1).</summary>
        public static int MaxDesks(IspState s) => Capacity(s.HeadendKey) - (s.CenterKey == s.HeadendKey ? s.CenterDesks : 0);
        public static int MaxCenterDesks(IspState s) => s.CenterKey.Length == 0 ? 0 : Capacity(s.CenterKey) - (s.CenterKey == s.HeadendKey ? s.Desks : 0);
        /// <summary>The customer center is there and still the player's.</summary>
        public static bool CenterOk(IspState s) => s.CenterKey.Length > 0 && (Sim.Test || City.Mine(s.CenterKey));

        /// <summary>Offices the player rents or owns: where a customer center may be (GO-OFFICE-1: offices only).</summary>
        public static IEnumerable<House> CenterChoices()
        {
            foreach (var b in City.All)
                if (b.Kind == Cat.KindOffice && City.Mine(b.Key)) yield return b;
        }

        /// <summary>On duty this hour: the game's employees by their shift in BizMan (GAME-TYPES-1), staff hired in the app
        /// before 0.6 by their own shift.</summary>
        public static bool OnDuty(IspState s, Tech t, int h) => t.Emp.Length > 0 ? Crew.OnDuty(s, t, h)
            : t.Role == Cat.RoleMaster ? h >= 8 && h < 16 : Sim.OnShift(t, h);

        /// <summary>This hour's vehicles and workplaces for everyone on duty: vans to technicians, then to switching staff;
        /// network masters a workplace (at the headend first) and a car (a van if no car is left); then the customer service and
        /// the technical support their workplaces (in the customer center first). Returns the vehicles in use.</summary>
        public static int Assign(IspState s, int h)
        {
            var cars = AtHeadend(s);
            // GO-FLEET-1 (user 2026-11-07: "kann die Techniker nicht zuweisen ... genauso müsste drin stehen 2 Leasingfahrzeuge"):
            // a person with a vehicle of their own (BizMan's fleet page) takes it first when on duty; the rest goes round
            var pinned = new Dictionary<int, Car>();
            var taken = new HashSet<string>();
            foreach (var t in s.Techs)
            {
                if (t.Pin.Length == 0 || taken.Contains(t.Pin)) continue;
                var c = cars.Find(x => x.Id == t.Pin);
                if (c == null || !CanDrive(t, c) || !OnDuty(s, t, h)) continue;
                pinned[t.Id] = c; taken.Add(c.Id);
            }
            var vans = new Queue<Car>(); var others = new Queue<Car>();
            foreach (var c in cars) if (!taken.Contains(c.Id)) (c.Van ? vans : others).Enqueue(c);
            int head = s.HeadendDown ? 0 : s.Desks, center = CenterOk(s) ? s.CenterDesks : 0, used = 0;
            foreach (byte role in new[] { Cat.RoleTech, Cat.RoleSwitch, Cat.RoleMaster, Cat.RoleSales, Cat.RoleService, Cat.RoleSupport })
                foreach (var t in s.Techs)
                {
                    if (t.Role != role) continue;
                    t.Car = ""; t.CarId = ""; t.Desk = false;
                    byte why = t.Emp.Length > 0 ? Crew.Duty(s, t, h) : (OnDuty(s, t, h) ? (byte)0 : (byte)3);   // GO-WORK-2: 4/5 workplace or office not ready
                    if (why != 0) { t.Missing = why; continue; }
                    int took = 0;   // 1 = a headend workplace, 2 = one in the customer center
                    if (t.Emp.Length > 0) t.Desk = true;   // the game's employee works at the workplace of the shift
                    else if (Cat.NeedsDesk(role))
                    {
                        bool headFirst = role == Cat.RoleMaster;
                        if (headFirst ? head > 0 : center <= 0 && head > 0) { head--; took = 1; }
                        else if (center > 0) { center--; took = 2; }
                        else if (head > 0) { head--; took = 1; }
                        if (took == 0) { t.Missing = 2; continue; }
                        t.Desk = true;
                    }
                    if (Cat.NeedsCar(role))
                    {
                        Car? c = pinned.TryGetValue(t.Id, out var own) ? own
                               : !Cat.NeedsVan(role) ? (others.Count > 0 ? others.Dequeue() : vans.Count > 0 ? vans.Dequeue() : null)
                                                     : (vans.Count > 0 ? vans.Dequeue() : null);
                        if (c == null) { t.Missing = 1; if (took == 1) head++; else if (took == 2) center++; t.Desk = false; continue; }
                        t.Car = c.Name; t.CarId = c.Id; used++;
                    }
                    t.Missing = 0;
                }
            return used;
        }

        /// <summary>GO-FLEET-1: technicians and switching staff need a van, network masters and field sales any vehicle.</summary>
        public static bool CanDrive(Tech t, Car c) => Cat.NeedsCar(t.Role) && (c.Van || !Cat.NeedsVan(t.Role));

        /// <summary>GO-FLEET-1: gives a vehicle to one person of its own (techId 0: to nobody - it goes round again). A
        /// vehicle has one such person at most, a person one vehicle.</summary>
        public static void Pin(IspState s, string carId, int techId)
        {
            foreach (var t in s.Techs) if (t.Pin == carId) t.Pin = "";
            var who = techId == 0 ? null : s.Techs.Find(x => x.Id == techId);
            if (who != null) who.Pin = carId;
        }
    }
}
