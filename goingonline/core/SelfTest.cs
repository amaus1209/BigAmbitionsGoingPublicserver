using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>A dry run of the provider for the developer: a made-up provider in the real city, played by simple rules for
    /// some game days in a few milliseconds, every day written to the log. Only in memory - no money of the game moves
    /// (Sim.Test), no notification shows, nothing goes into the save. Going Public Server runs it on a dedicated server when
    /// the mod's folder holds "goingonline-selftest.txt".
    /// The test player starts with a given amount of money (user 2026-11-06: "mit Startkapital anfangen"; the founder loan
    /// went out again: "warum Kredit - raus damit") and may only spend what it has (Money.TestBudget). The competitor plays
    /// along. So the test shows whether the start works.</summary>
    internal static class SelfTest
    {
        static float _peak;
        static int _firstProfit;
        const float Reserve = 6000f;

        static bool Afford(float cost) => Money.TestCash >= cost + Reserve;

        public static void Run(int days, float money = -1f)
        {
            Sim.Test = true;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (!City.Ensure()) { Go.Warn("selftest: the city is not there."); return; }
                Go.Log("selftest game types: " + GameTypes.Check() + ".");
                var kinds = new int[5];
                int homes = 0, biz = 0;
                foreach (var b in City.All) { if (b.Connectable) kinds[b.Kind]++; homes += b.Homes; biz += b.Biz; }
                Go.Log($"selftest city: {City.All.Count} buildings; connectable: residential {kinds[0]}, shops {kinds[1]}, offices {kinds[2]}, warehouses {kinds[3]}, villas {kinds[4]}; {homes} households, {biz} businesses.");
                House? he = null;
                foreach (var b in City.All) if (b.Type == "ba:buildingtype_office" && b.Hood.Length > 0 && (he == null || b.Sqm > he.Sqm)) he = b;   // GO-OFFICE-1
                if (he == null) { Go.Warn("selftest: no office for the headend."); return; }

                long start = Sim.NowHour();
                if (start < 0) start = 240;
                var s = new IspState { Company = "Selbsttest", HeadendKey = he.Key, CenterKey = he.Key, FoundedHour = start, LastHour = start, TransitTier = 1 };
                // the money a player brings along (default 55,000 $: a new game's 4,200 $ and what a first business earned)
                int serverMoney = -1;
                try { serverMoney = SaveGameManager.Current?.gameVariables?.startingMoney ?? -1; } catch { }
                float startMoney = money >= 0f ? money : 55000f;
                Money.TestBudget = true; Money.TestCash = startMoney; Money.TestLowestDay = -1;
                Money.TestLowest = Money.TestCash;
                Go.Log($"selftest money: start money {startMoney:0}; competitor strength {Rival.Strength():0.00}; this server's start money setting {serverMoney}.");
                Go.Log("selftest vans at the dealers: " + VanPrices());
                if (!Money.Invest(s, Cat.Cmts30)) { Go.Warn("selftest: the start capital does not pay the CMTS."); return; }
                Act.Defaults(s);
                _firstProfit = -1;
                // first: one technician with a leased van, the biggest buildings near the headend as far as the money goes
                Want(s, Cat.RoleTech, 1);
                Vehicles(s);
                // a small start: the biggest buildings as far as the money goes, 8,000 $ stay for devices and the first days
                int sites = 0;
                foreach (var b in City.All.Where(x => x.Connectable && x.Hood == he.Hood && x.Kind != Cat.KindWarehouse).OrderByDescending(x => x.Units))
                {
                    if (Money.TestCash - Act.SiteCost(s, b, out _) < 8000f) break;
                    if (Act.Connect(s, b.Key)) sites++;
                }
                Go.Log($"selftest: headend {City.Name(he.Key)} in {City.HoodName(he.Hood)}; {sites} buildings ordered ({Sim.HomesPassed(s, he.Hood)} units); invested {Go.Money(s.Today.Build)}; cash left {Money.TestCash:0}.");
                s.AutoRenew = false;   // three contracts end soon, to see the tenders; from day 20 they are renewed by themselves
                int early = 0;
                foreach (var site in s.Sites.Values) if (early++ < 3) site.PermitUntil = (int)(start / 24) + 18;

                int day0 = (int)(start / 24) + 1;
                for (int d = 0; d < days; d++)
                    for (int h = 0; h < 24; h++)
                    {
                        long abs = (long)(day0 + d) * 24 + h;
                        if (h == 0 && d > 0) { Sim.EndOfDay(s, day0 + d - 1); Report(s); }
                        Sim.Hour(s, abs);
                        s.LastHour = abs;
                        if (h == 23) Operate(s, d, he.Hood);
                    }
                Summary(s);
                RivalSummary(s);
                float week = 0f; int k = 0;
                foreach (var b in s.Books) { if (k++ >= 7) break; week += b.Operating - b.Tax; }
                Go.Log($"selftest money end: cash {Money.TestCash:0}, lowest {Money.TestLowest:0} on day {Money.TestLowestDay}, first day with a profit {_firstProfit}, result of the last 7 days {week:0}.");
                var (chars, back) = SaveCarrier.RoundTrip(s);
                Go.Log($"selftest end: {Sim.Customers(s)} customers in {s.Sites.Count} buildings, {s.Hoods.Count} neighbourhoods, {s.Tickets.Count} open tickets; save entry {chars} characters, {back} customers after reading it back; {watch.ElapsedMilliseconds} ms.");
            }
            catch (System.Exception ex) { Go.Warn("selftest failed: " + ex); }
            finally { Sim.Test = false; Money.TestBudget = false; }
        }

        /// <summary>The vans the game's dealers sell, with their prices (to set the leasing price against).</summary>
        static string VanPrices()
        {
            var l = new List<string>();
            try
            {
                foreach (var name in global::Vehicles.VehicleTypes.VehicleTypeHelper.GetVehicleTypeNames())
                {
                    var t = global::Vehicles.VehicleTypes.VehicleTypeHelper.GetVehicleType(name);
                    if (t != null && t.IsMotorVehicle && t.maxCargoCapacity >= Cat.VanCargo) l.Add($"{Go.Game(name)} {t.price:0} (cargo {t.maxCargoCapacity})");
                }
            }
            catch (System.Exception ex) { return "? " + ex.Message; }
            return l.Count == 0 ? "none" : string.Join("; ", l);
        }

        /// <summary>The state at the end in a few lines: tariffs, roles, neighbourhoods, devices.</summary>
        static void Summary(IspState s)
        {
            var tar = new List<string>();
            foreach (var t in s.Tariffs)
                tar.Add($"{t.Name} [{(t.Fiber ? "fiber" : "coax")} {t.Mbit} tv {(t.Tv ? (t.Ip ? "iptv" : "cable") : "-")} phone {(t.Phone ? "y" : "-")} price {t.Price:0.00} term {t.Term} active {t.Active}] {Act.Users(s, t)}");
            Go.Log("selftest summary tariffs: " + string.Join("; ", tar));
            var roles = new List<string>();
            foreach (byte r in Cat.Roles)
                roles.Add($"{Act.RoleName(r)} need {Sim.Need(s, r)} have {s.Techs.Count(t => t.Role == r)} backlog {Sim.Backlog(s, r):0.0}h");
            Go.Log("selftest summary roles: " + string.Join("; ", roles) + $"; desks {s.Desks}, center {s.CenterDesks}; wages/day {s.Techs.Sum(t => t.Wage) * 8f:0}");
            var hoods = new List<string>();
            foreach (var h in s.Hoods.Values)
            {
                int cust = 0; foreach (var site in s.Sites.Values) if (site.Hood == h.Id) cust += Sim.CustomersOf(site);
                hoods.Add($"{City.HoodName(h.Id)} nodes {h.Nodes} units {Sim.HomesPassed(s, h.Id)} customers {cust} d31 {h.D31} fiber {h.FiberIn} peak {h.Util.Max():0.00} util " + string.Join(",", h.Util.Select(u => u.ToString("0.00"))));
            }
            Go.Log("selftest summary hoods: " + string.Join("; ", hoods));
            int[] dev = new int[4], own = new int[3];
            foreach (var site in s.Sites.Values) foreach (var c in site.C) { if (c.Dev < 4) dev[c.Dev] += c.N; if (c.Own < 3) own[c.Own] += c.N; }
            Go.Log($"selftest summary devices: none {dev[0]} d30 {dev[1]} d31 {dev[2]} ont {dev[3]}; own-kind {own[0]}/{own[1]}/{own[2]}; stock m30 {s.Stock.M30} m31 {s.Stock.M31} ont {s.Stock.Ont} cards {s.Stock.Cards} stb {s.Stock.Stb}; "
                + $"numbers {s.NumberBlocks} blocks, {Sim.FreeNumbers(s)} free; phone {Sim.PhoneCustomers(s)}, IPTV {Sim.IptvCustomers(s)}; transponders {s.Transponders}; transit tier {s.TransitTier}; "
                + $"pay-tv " + string.Join(", ", s.PayTv.Select(p => $"{p.Id} {p.Subs} at {p.Price:0.00}")) + $"; reputation {s.Reputation:0}; sites lost {s.Sites.Values.Count(x => x.Lost)}");
        }


        static void RivalSummary(IspState s)
        {
            var r = s.Rival;
            var hoods = new List<string>();
            foreach (var kv in r.Hoods)
                hoods.Add($"{City.HoodName(kv.Key)} tech {kv.Value.Tech} price {kv.Value.Price:0.00} cut until {kv.Value.PriceUntil} promo until {kv.Value.PromoUntil} share {Rival.Share(s, kv.Key):0.00}");
            Go.Log($"selftest rival: strength {Rival.Strength():0.00}, reputation {r.Reputation:0}; " + string.Join("; ", hoods) + $"; exclusive contracts {r.Locked.Count}; moves {r.Log.Count}: " + string.Join(" / ", r.Log.Take(6)));
        }

        /// <summary>The team the test plays with: what the app shows as the need, but only as far as the money goes - a
        /// young company with little money makes do with one technician, the founder takes the orders.</summary>
        static void Staff(IspState s)
        {
            var y = s.Books.Count > 0 ? s.Books[0] : null;
            // more people only when the customers carry them (device sales are one-off and do not count) or money is plenty
            float carried = y == null ? 0f : y.Subs + y.Rent + y.PayTvIn - y.Running;   // device sales are one-off and do not count
            bool ok = carried > 250f || Money.TestCash > 60000f;
            int customers = Sim.Customers(s);
            Want(s, Cat.RoleTech, Mathf.Clamp(Sim.Need(s, Cat.RoleTech), 1, ok ? 40 : 1));
            // the founder switches on by day; switching staff only when that is not enough any more
            Want(s, Cat.RoleSwitch, ok && Sim.InPipeline(s, Cat.TtOn) > 25 ? Mathf.Clamp(Sim.Need(s, Cat.RoleSwitch), 1, 30) : s.Techs.Count(t => t.Role == Cat.RoleSwitch));
            Want(s, Cat.RoleService, ok ? Mathf.Clamp(Sim.Need(s, Cat.RoleService), 0, 30) : (Sim.InPipeline(s, Cat.TtOrder) > 60 ? 1 : 0));
            Want(s, Cat.RoleSupport, ok && customers > 1500 ? Mathf.Clamp(Sim.Need(s, Cat.RoleSupport), 0, 10) : 0);
            Want(s, Cat.RoleSales, ok && customers > 800 ? 1 : 0);
            if (carried > 600f || Money.TestCash > 60000f)
                foreach (var h in s.Hoods.Keys.ToList())
                    if (!s.Techs.Exists(t => t.Role == Cat.RoleMaster && t.Hood == h) && s.Sites.Values.Where(x => x.Hood == h).Sum(x => Sim.CustomersOf(x)) >= 1000)
                        s.Techs.Add(new Tech { Id = s.NextId++, Role = Cat.RoleMaster, Hood = h, Name = "Meister " + h, Skill = 3, Wage = Cat.Wage(Cat.RoleMaster, 3) });
            int desks = s.Techs.Count(t => Cat.NeedsDesk(t.Role));
            while (s.Desks < desks && Afford(Cat.Desk) && Act.BuyDesks(s, 1)) { }
            Vehicles(s);
        }

        /// <summary>No vans of its own: it leases as many as the busiest shift needs, and cars for the masters and sales.</summary>
        static void Vehicles(IspState s)
        {
            Fleet.TestVans = 0; Fleet.TestCars = 0;
            int vans = 0;
            for (byte sh = 0; sh < 3; sh++) vans = Mathf.Max(vans, s.Techs.Count(t => Cat.NeedsVan(t.Role) && t.Shift == sh));
            s.LeaseVans = vans;
            s.LeaseCars = s.Techs.Count(t => t.Role == Cat.RoleMaster) + s.Techs.Count(t => t.Role == Cat.RoleSales);
        }

        static void Want(IspState s, byte role, int count)
        {
            var mine = s.Techs.FindAll(t => t.Role == role && t.Ticket == 0);
            if (s.Techs.Count(t => t.Role == role) > count + 2 && mine.Count > 0) Act.Fire(s, mine[mine.Count - 1]);   // one a day
            while (s.Techs.Count(t => t.Role == role) < count)
            {
                int i = s.Techs.Count(t => t.Role == role);
                s.Techs.Add(new Tech { Id = s.NextId++, Role = role, Name = Act.RoleName(role) + " " + (i + 1), Skill = 3, Wage = Cat.Wage(role, 3), Shift = (byte)(i % 3), Manner = 1 + (int)(Sim.Rand() * 5) });
            }
        }

        /// <summary>Connects the biggest free buildings of a neighbourhood as far as the money goes (a reserve stays).</summary>
        static int Grow(IspState s, string hood, int most, int maxUnits = int.MaxValue)
        {
            var list = City.All.FindAll(b => b.Connectable && b.Hood == hood && b.Kind != Cat.KindWarehouse && !s.Sites.ContainsKey(b.Key)
                                             && Rival.LockedUntil(s, b.Key, Sim.Today(s)) < 0);
            list.Sort((a, b) => b.Units.CompareTo(a.Units));
            int n = 0, units = 0;
            foreach (var b in list)
            {
                if (n >= most || units >= maxUnits) break;
                if (!Afford(Act.SiteCost(s, b, out _))) break;
                if (Act.Connect(s, b.Key)) { n++; units += b.Units; }
            }
            return n;
        }

        /// <summary>More buildings while the money lasts down to a smaller reserve (the best investment: a building pays back
        /// in a couple of weeks).</summary>
        static void GrowWith(IspState s, string hood, int most, float reserve)
        {
            var list = City.All.FindAll(b => b.Connectable && b.Hood == hood && b.Kind != Cat.KindWarehouse && !s.Sites.ContainsKey(b.Key)
                                             && Rival.LockedUntil(s, b.Key, Sim.Today(s)) < 0);
            list.Sort((a, b) => b.Units.CompareTo(a.Units));
            int n = 0;
            foreach (var b in list)
            {
                if (n >= most) break;
                float cost = Act.SiteCost(s, b, out _);
                if (Money.TestCash < cost + reserve) break;
                if (Act.Connect(s, b.Key)) n++;
            }
        }

        static float Max(float[] v) { float m = 0f; foreach (var x in v) if (x > m) m = x; return m; }

        /// <summary>A sensible player with a budget: bandwidth after the need, nodes where it is full, devices on stock, more
        /// buildings while the money lasts; phone, TV, Pay-TV, IPTV, DOCSIS 3.1, fiber and a second neighbourhood only when
        /// there is money for them.</summary>
        static void Operate(IspState s, int d, string home)
        {
            var y = s.Books.Count > 0 ? s.Books[0] : null;
            if (_firstProfit < 0 && y != null && y.Operating - y.Tax > 0f) _firstProfit = y.Day;
            _peak = Max(s.UpLoad);
            int tier = 0;
            while (tier < Cat.TransitMbit.Length - 1 && Cat.TransitMbit[tier] < _peak * 1.15f) tier++;
            s.TransitTier = tier;
            foreach (var h in s.Hoods.Values)
                if (h.Nodes > 0 && h.NodesBuilding == 0 && Max(h.Util) > 0.95f && Afford(Cat.MoreNode)) Act.AddNode(s, h.Id);
            // devices first: without them nobody can sign (bought down to the last dollar, in small lots when money is short)
            int lot = Money.TestCash > 20000f ? 100 : 25;
            if (s.Stock.M30 < 30 && Money.TestCash >= lot * 45f) Act.BuyDevices(s, Cat.Dev30, lot);
            if (s.Cmts31 && s.Stock.M31 < 30 && Money.TestCash >= lot * 95f) Act.BuyDevices(s, Cat.Dev31, lot);
            if (s.Olt && s.Stock.Ont < 30 && Money.TestCash >= lot * 70f) Act.BuyDevices(s, Cat.DevOnt, lot);
            Staff(s);
            bool rich = Money.TestCash > 15000f;
            s.AutoRenew = d >= 20;
            s.SubTech = rich && Sim.Backlog(s, Cat.RoleTech) > 16f;
            s.SubSwitch = rich && Sim.Backlog(s, Cat.RoleSwitch) > 16f;
            s.SubService = rich && Sim.Backlog(s, Cat.RoleService) > 8f;
            s.Overtime = Money.TestCash > 5000f && Sim.Backlog(s, Cat.RoleTech) > 24f;
            int nodes = s.Hoods.Values.Sum(h => h.Nodes);
            s.Upkeep = (Money.TestCash > 0f ? 40f : 10f) * nodes;
            s.Sales = rich ? 200f : 0f;
            // more buildings when the ones there are filling up (or money is plenty)
            float filled = Sim.HomesPassed(s, home) > 0 ? s.Sites.Values.Where(x => x.Hood == home).Sum(x => Sim.CustomersOf(x)) / (float)Sim.HomesPassed(s, home) : 1f;
            if (filled > 0.45f || Money.TestCash > 40000f) GrowWith(s, home, 5, 3000f);
            float earns = y == null ? 0f : y.Subs + y.Rent + y.PayTvIn - y.Running;
            bool extras = earns > 300f;

            if (!s.Voice && extras && Afford(Cat.Voice + 2 * Cat.NumberBlock + Reserve))
            {
                Act.BuyVoice(s);
                Act.BuyNumbers(s); Act.BuyNumbers(s);
                foreach (var t in s.Tariffs) if (t.Mbit >= 250) t.Phone = true;
                Go.Log($"selftest day {d}: phone on (2 number blocks).");
            }
            if (!s.Sat && extras && Afford(Cat.Sat + 8 * Cat.Transmod + Reserve))
            {
                Act.BuySat(s);
                for (int i = 0; i < 16; i++) Act.Transponders(s, 1);
                var tv = Act.AddTariff(s, false, true);
                var komfort = s.Tariffs.Find(t => t.Mbit == 250);
                if (komfort != null) { komfort.Tv = true; komfort.Price += 0.3f; }
                Go.Log($"selftest day {d}: TV on - {s.Transponders} transponders, tariff '{tv.Name}'.");
            }
            if (s.Sat && !s.Cas && extras && Afford(Cat.Cas + 200 * 12f + Reserve))
            {
                Act.BuyCas(s);
                Act.BuyCards(s, 200);
                Act.Contract(s, "sport", true);
                Act.Contract(s, "film", true);
                Go.Log($"selftest day {d}: Pay-TV on.");
            }
            if (s.Sat && !s.Iptv && extras && Afford(Cat.Iptv + 300 * Cat.BuyStb + Reserve))
            {
                Act.BuyIptv(s);
                Act.BuyStb(s, 300);
                s.Tariffs.Add(new Tariff { Id = s.NextTariff++, Name = "IPTV 250", Mbit = 250, Tv = true, Ip = true, Phone = s.Voice, Price = 2.89f });
                Go.Log($"selftest day {d}: IPTV on.");
            }
            if (s.Iptv && s.Stock.Stb < 60 && Afford(200 * Cat.BuyStb)) Act.BuyStb(s, 200);
            if (s.Voice && Sim.FreeNumbers(s) < 300 && Afford(Cat.NumberBlock)) Act.BuyNumbers(s);
            if (s.Stock.Cards < 50 && s.Cas && Afford(200 * 12f)) Act.BuyCards(s, 200);
            if (!s.Cmts31 && Sim.Customers(s) >= Sim.Unlock31(s) && Afford(Cat.Cmts31 + nodes * Cat.Node31 + Reserve))
            {
                Act.Buy31(s);
                foreach (var h in s.Hoods.Keys.ToList()) Act.Upgrade31(s, h);
                s.Tariffs.Add(new Tariff { Id = s.NextTariff++, Name = "Giga 1000", Mbit = 1000, Price = 2.79f });
                Go.Log($"selftest day {d}: DOCSIS 3.1 bought.");
            }
            else if (s.Cmts31) foreach (var h in s.Hoods.Values) if (!h.D31 && h.D31Hours <= 0 && Afford(h.Nodes * Cat.Node31)) Act.Upgrade31(s, h.Id);
            if (s.Cmts31) foreach (var h in s.Hoods.Keys.ToList()) if (s.Hoods[h].D31) Act.SwapModems(s, h);
            if (!s.Olt && s.Cmts31 && Sim.Customers(s) >= Sim.UnlockFiber(s) && Afford(Cat.Olt + 300 * 70f + 5 * 20000f))
            {
                Act.BuyOlt(s);
                Act.BuyDevices(s, Cat.DevOnt, 300);
                s.Tariffs.Add(new Tariff { Id = s.NextTariff++, Name = "Glasfaser 2000", Fiber = true, Mbit = 2000, Price = 3.09f });
                var big = s.Sites.Values.OrderByDescending(x => x.Homes + x.Biz).Take(5).Select(x => x.Key).ToList();
                foreach (var key in big) Act.ConnectFiber(s, key);
                Go.Log($"selftest day {d}: fiber - OLT bought, {big.Count} buildings get fiber.");
            }
            if (s.Olt) foreach (var site in s.Sites.Values.Where(x => x.Fiber).ToList()) Act.MoveToFiber(s, site.Key);
            // a rude technician goes to the customer-care training when the money is there
            foreach (var t in s.Techs) if (t.Role == Cat.RoleTech && t.Manner <= 2 && Act.CanTrain(s, t) && Afford(Cat.Training + 2000f)) Act.Train(s, t);
            // old coax is refurbished (one building a day) when the money is there - it counts as new again
            var oldest = s.Sites.Values.Where(x => x.Coax && !x.CoaxOff && !x.Lost && Sim.AgeYears(s, x.BuiltDay, Sim.Today(s)) >= 2f)
                                       .OrderByDescending(x => x.Homes + x.Biz).FirstOrDefault();
            if (oldest != null && Afford(Act.RefurbishCost(oldest) + 10000f)) Act.Refurbish(s, oldest.Key);
            if (s.Hoods.Count < 2 && extras && Afford(40000f))
            {
                var other = City.Hoods.FirstOrDefault(h => !s.Hoods.ContainsKey(h) && !h.EndsWith("thehamptons"));
                if (other != null) Go.Log($"selftest day {d}: second neighbourhood {City.HoodName(other)} - {Grow(s, other, 999)} buildings ordered.");
            }
            else if (s.Hoods.Count >= 2 && Money.TestCash > 30000f)
                foreach (var h in s.Hoods.Keys.ToList()) if (h != home) Grow(s, h, 5);
        }

        static void Report(IspState s)
        {
            var b = s.Books[0];
            int open = s.Tickets.Count(t => t.Type != Cat.TtInstall);
            int inst = s.Tickets.Where(t => t.Type == Cat.TtInstall).Sum(t => t.N);
            var busiest = s.Hoods.Values.OrderByDescending(h => Max(h.Util)).FirstOrDefault();
            float util = busiest == null ? 0f : Max(busiest.Util);
            string busy = busiest == null ? "-" : $"{City.HoodName(busiest.Id)} {util:0.00} ({busiest.Nodes} nodes, 3.1 {busiest.D31})";
            float arpu = b.Customers > 0 ? (b.Subs + b.Rent + b.PayTvIn) / b.Customers : 0f;
            int waiting = s.Sites.Values.Sum(x => x.Waiting);
            int today = b.Day + 1;
            int fights = s.Rival.Hoods.Values.Count(h => h.PromoUntil >= today || h.PriceUntil >= today);
            Go.Log($"selftest staff day {b.Day}: satisfaction {s.Satisfaction:0} (net {s.SatNet:0.00} faults {s.SatFaults:0.00} appoint {s.SatAppoint:0.00} calls {s.SatCalls:0.00} orders {s.SatOrders:0.00} complaints {s.SatComplaints:0.00}); "
                 + $"team tech {s.Techs.Count(t => t.Role == Cat.RoleTech)} switch {s.Techs.Count(t => t.Role == Cat.RoleSwitch)} service {s.Techs.Count(t => t.Role == Cat.RoleService)} support {s.Techs.Count(t => t.Role == Cat.RoleSupport)} masters {s.Techs.Count(t => t.Role == Cat.RoleMaster)} sales {s.Techs.Count(t => t.Role == Cat.RoleSales)}; leased vans {s.LeaseVans} cars {s.LeaseCars}, desks {s.Desks}; "
                 + $"pipeline orders {Sim.InPipeline(s, Cat.TtOrder)} port {Sim.InPipeline(s, Cat.TtPort)} on {Sim.InPipeline(s, Cat.TtOn)} off {Sim.InPipeline(s, Cat.TtOff)} installs {Sim.InPipeline(s, Cat.TtInstall)}; free riders {Sim.FreeRiders(s)}; calls {b.CallsIn:0}/{b.CallsDone:0}; "
                 + $"phone {Sim.PhoneCustomers(s)}; IPTV {Sim.IptvCustomers(s)}; lost buildings {s.Sites.Values.Count(x => x.Lost)}; contractors {b.Contractors:0}; lease {b.Lease:0}, fleet {b.Fleet:0}, maintenance {b.Maint:0} (coax age {s.Hoods.Keys.Select(h => Sim.HoodCoaxAge(s, h, b.Day + 1)).DefaultIfEmpty(0f).Max():0.0} y.), repair material {b.Repair:0}, volume discount {Cat.Volume(b.Customers):0.00}; "
                 + $"rival rep {s.Rival.Reputation:0}, price level {Rival.PriceLevel(s, ""):0.00}, fighting in {fights}, exclusive {s.Rival.Locked.Count}; "
                 + $"moves {b.Moves} along, {b.MovesLost} cancelled, fees {b.MoveFees:0}; visits {b.Visits}, own fault {b.OwnFault}, nobody home {b.NoShows}, charges {b.ServiceFees:0}; "
                 + $"complaints {b.Complaints}, answered {b.ComplaintsDone}, open {s.Tickets.Where(t => t.Type == Cat.TtComplaint).Sum(t => t.N)}, satisfaction part {s.SatComplaints:0.00}; manners {string.Join("", s.Techs.Where(t => t.Role == Cat.RoleTech).Select(t => t.Manner))}.");
            Go.Log($"selftest day {b.Day}: customers {b.Customers} (+{b.Joined} -{b.Left}, waiting {waiting}), internet {Sim.InternetCustomers(s)}, TV {Sim.TvCustomers(s)}, Pay-TV {s.PayTv.Sum(p => p.Subs)}; "
                 + $"income {b.Income:0} (subs {b.Subs:0}, rent {b.Rent:0}, sold {b.Sold:0}, paytv {b.PayTvIn:0}), running {b.Running:0} (transit {b.Transit:0}, wages {b.Wages:0}, comp {b.Comp:0}, paytv {b.PayTvOut:0}, upkeep {b.Upkeep:0}, ship {b.Ship:0}), "
                 + $"levy {b.Tax:0}, result {b.Operating - b.Tax:0}, invested {b.Build + b.Gear:0}; cash {Money.TestCash:0}; faults {b.Faults} fixed {b.Fixed} open {open}, installs waiting {inst}; "
                 + $"reputation {s.Reputation:0}; revenue per customer {arpu:0.00}; team {s.Techs.Count}; busiest {busy}; upstream peak {_peak:0} Mbit/s on tier {Cat.TransitMbit[s.TransitTier]}; nodes {s.Hoods.Values.Sum(h => h.Nodes)}; stock {s.Stock.M30}/{s.Stock.M31}/{s.Stock.Ont}/{s.Stock.Cards}.");
        }
    }
}
