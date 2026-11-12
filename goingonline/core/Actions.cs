using System.Collections.Generic;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>What the player does in the app. Every action that costs money pays first (Money.Invest refuses without
    /// enough money) and returns false then.</summary>
    internal static class Act
    {
        public static readonly int[] CoaxSpeeds = { 16, 50, 100, 250, 500, 1000 };
        public static readonly int[] FiberSpeeds = { 100, 300, 500, 1000, 2000, 5000, 8000 };
        public const float FiberFeed = 20000f;   // feeder fiber into a neighbourhood for the PON trees

        static long Now => Sim.NowHour();

        // ── founding and the headend ─────────────────────────────────────────────────────────────────────────

        /// <summary>GO-DISSOLVE-1 (user 2026-11-08: "Going Online wurde gelöscht, die Lagerhalle und das Kundenbüro,
        /// trotzdem ist es noch freigeschaltet beim Menüpunkt Going Online"). Without its building the provider waits on
        /// purpose ("the network is down until it moves"), so a new office can take over. The user chose "Nur ein Knopf in
        /// der App" over dissolving by itself: the company ends only on this button. The button shows only when no building
        /// of ours is left, because GameFound would found it again at once from one. Ending it removes the provider from
        /// the game and from the save; the leased vehicles, contracts and customers are part of it and end with it. The
        /// game's own things stay the game's: the staff, the devices on the shelves and the money. The app shows
        /// "Found a company" again.</summary>
        public static bool Dissolve(IspState s)
        {
            if (!ReferenceEquals(Core.S, s)) return false;
            if (City.Mine(s.HeadendKey) || GameTypes.AnyOurs())
            {
                Notify.Warn(Go.T("go_dissolve_busy", "{0} still has an office of yours - close the business in BizMan first.", s.Company), 6);
                return false;
            }
            string name = s.Company;
            int customers = Sim.Customers(s);
            Core.Dissolve();
            Go.Log($"provider '{name}' dissolved by the player ({customers} customers, no building of it left) (GO-DISSOLVE-1).");
            Notify.Info(Go.T("go_dissolve_done", "{0} is dissolved. You can found a new cable provider in an office at any time.", name), 6);
            return true;
        }

        /// <summary>Founds the provider with the headend in an office the player rents or owns (GO-OFFICE-1: headend and
        /// customer center in one). In the real game (GAME-TYPES-1) that is BizMan's "Cable provider" business (GameFound); the
        /// CMTS is a piece of furniture from an office furniture store then. The developer's dry run pays the CMTS here.</summary>
        public static bool Found(string name, string headendKey)
        {
            if (Core.S != null || headendKey.Length == 0 || !City.Mine(headendKey)) return false;
            if (Sim.Test && Money.Cash < Cat.Cmts30) return false;
            var s = new IspState
            {
                Company = string.IsNullOrWhiteSpace(name) ? "Going Online" : name.Trim(),
                HeadendKey = headendKey,
                CenterKey = City.ByKey.TryGetValue(headendKey, out var hb) && hb.Kind == Cat.KindOffice ? headendKey : "",   // GO-OFFICE-1: one roof
                FoundedHour = Now,
                LastHour = Now,
                Seed = System.Environment.TickCount,
                TransitTier = 1,
            };
            if (Sim.Test && !Money.Invest(s, Cat.Cmts30)) return false;
            s.Format = 8;   // 0.6: the equipment stands in the headend, the staff are the game's employees; 0.8: offices and customer centers per neighbourhood
            s.V08Day = Sim.Day() - Cat.V08Grace;   // GO-HUBS-1: a new provider starts with 0.8's rules
            Defaults(s);
            Sim.Event(s, Go.T("go_event_founded", "{0} founded. The headend stands in {1}.", s.Company, City.Name(headendKey)));
            Core.Founded(s);
            Notify.Good(Sim.Test ? Go.T("go_note_founded", "{0} is founded - connect your first buildings in the Buildings tab.", s.Company)
                                 : Go.T("go_note_founded_game", "{0} is founded. Buy a CMTS at an office furniture store and set it up in the headend - then connect your first buildings in the Buildings tab.", s.Company), 10);
            return true;
        }

        /// <summary>A new provider's start: 25 modems and three tariffs (the CMTS is paid by the caller).</summary>
        internal static void Defaults(IspState s)
        {
            s.Stock.M30 = 25;
            s.Tariffs.Add(new Tariff { Id = s.NextTariff++, Name = "Start 50", Mbit = 50, Price = 1.89f });
            s.Tariffs.Add(new Tariff { Id = s.NextTariff++, Name = "Komfort 250", Mbit = 250, Price = 2.29f });
            s.Tariffs.Add(new Tariff { Id = s.NextTariff++, Name = "Turbo 500", Mbit = 500, Price = 2.59f, Term = 10 });
            s.Today = new DayBook { Day = Sim.Day() };
            Sim.FirstCandidates(s);
        }

        public static bool MoveHeadend(IspState s, string key)
        {
            if (key == s.HeadendKey || !City.Mine(key)) return false;
            if (!Money.Invest(s, Cat.MoveHeadend)) return false;
            s.HeadendKey = key; s.HeadendDown = false;
            Sim.Event(s, Go.T("go_event_headend_moved", "The headend moved to {0}.", City.Name(key)));
            return true;
        }

        public static bool Buy31(IspState s)
        {
            if (!Sim.Test) return false;   // GAME-TYPES-1: sold in the game's stores
            if (s.Cmts31 || Sim.Customers(s) < Sim.Unlock31(s) || !Money.Invest(s, Cat.Cmts31)) return false;
            s.Cmts31 = true;
            Sim.Event(s, Go.T("go_event_cmts31", "The CMTS runs DOCSIS 3.1 - now upgrade the nodes neighbourhood by neighbourhood."));
            return true;
        }

        public static bool BuyOlt(IspState s)
        {
            if (!Sim.Test) return false;   // GAME-TYPES-1: sold in the game's stores
            if (s.Olt || !s.Cmts31 || Sim.Customers(s) < Sim.UnlockFiber(s) || !Money.Invest(s, Cat.Olt)) return false;
            s.Olt = true;
            Sim.Event(s, Go.T("go_event_olt", "The OLT for XGS-PON is installed - buildings can get fiber now."));
            return true;
        }

        public static bool BuySat(IspState s)
        {
            if (!Sim.Test) return false;   // GAME-TYPES-1: sold in the game's stores
            if (s.Sat || !Money.Invest(s, Cat.Sat)) return false;
            s.Sat = true;
            Sim.Event(s, Go.T("go_event_sat", "The satellite dishes on the headend are up."));
            return true;
        }

        public static bool BuyCas(IspState s)
        {
            if (!Sim.Test) return false;   // GAME-TYPES-1: sold in the game's stores
            if (s.Cas || !s.Sat || !Money.Invest(s, Cat.Cas)) return false;
            s.Cas = true;
            Sim.Event(s, Go.T("go_event_cas", "The CA system encrypts Pay-TV now."));
            return true;
        }

        public static bool BuyVoice(IspState s)
        {
            if (!Sim.Test) return false;   // GAME-TYPES-1: sold in the game's stores
            if (s.Voice || !Money.Invest(s, Cat.Voice)) return false;
            s.Voice = true;
            Sim.Event(s, Go.T("go_event_voice", "The voice platform runs - tariffs can have a phone line now. Buy number blocks for new numbers."));
            return true;
        }

        public static bool BuyIptv(IspState s)
        {
            if (!Sim.Test) return false;   // GAME-TYPES-1: sold in the game's stores
            if (s.Iptv || !s.Sat || !Money.Invest(s, Cat.Iptv)) return false;
            s.Iptv = true;
            Sim.Event(s, Go.T("go_event_iptv", "The IPTV platform runs - TV tariffs can come as IPTV now (set-top boxes in the Devices tab)."));
            return true;
        }

        public static bool BuyNumbers(IspState s)
        {
            if (!s.Voice || !Money.Invest(s, Cat.NumberBlock)) return false;
            s.NumberBlocks++;
            return true;
        }

        /// <summary>Workplaces at the headend (center = false) or in the customer center.</summary>
        public static bool BuyDesks(IspState s, int n, bool center = false)
        {
            if (!Sim.Test) return false;   // GAME-TYPES-1: sold in the game's stores
            int have = center ? s.CenterDesks : s.Desks, max = center ? Fleet.MaxCenterDesks(s) : Fleet.MaxDesks(s);
            n = Mathf.Min(n, max - have);
            if (n <= 0 || (center && !Fleet.CenterOk(s)) || !Money.Invest(s, n * Cat.Desk)) return false;
            if (center) s.CenterDesks += n; else s.Desks += n;
            return true;
        }

        public static void SellDesk(IspState s, bool center = false)
        {
            if (center) { if (s.CenterDesks > 0) s.CenterDesks--; }
            else if (s.Desks > 0) s.Desks--;
        }

        /// <summary>The customer center moves into another building: the workplaces move along as far as there is room
        /// ($200 per workplace for the move).</summary>
        public static bool SetCenter(IspState s, string key)
        {
            if (key == s.CenterKey || !City.Mine(key) || Fleet.Capacity(key) <= 0) return false;
            int keep = Mathf.Min(s.CenterDesks, Fleet.Capacity(key));
            if (!Money.Invest(s, keep * 200f)) return false;
            s.CenterKey = key; s.CenterDesks = keep;
            Sim.Event(s, Go.T("go_event_center", "The customer center is now in {0} (room for {1} people).", City.Name(key), Fleet.Capacity(key)));
            return true;
        }

        public static bool BuyStb(IspState s, int n)
        {
            if (!Sim.Test) return false;   // GAME-TYPES-1: sold in the game's stores
            if (!s.Iptv || n <= 0 || !Money.Invest(s, n * Sim.Buy(s, Cat.BuyStb), true)) return false;
            s.Stock.Stb += n;
            return true;
        }

        /// <summary>A tariff's TV: none, cable (DVB-C) or IPTV (with the IPTV platform).</summary>
        public static void NextTv(IspState s, Tariff t)
        {
            if (t.Mbit <= 0) return;
            if (!t.Tv) { t.Tv = true; t.Ip = false; }
            else if (!t.Ip && s.Iptv) t.Ip = true;
            else { t.Tv = false; t.Ip = false; }
        }

        /// <summary>More or fewer transponders in the cable - GO-TV-2: one per Sat device in the headend (Sim.SatDevices); more
        /// than set up can only go down.</summary>
        public static bool Transponders(IspState s, int delta)
        {
            int cap = Mathf.Min(Cat.MaxTransponders, Sim.SatDevices(s));
            int n = Mathf.Clamp(s.Transponders + delta, 0, Cat.MaxTransponders);
            if (delta > 0 && n > cap) return false;
            if (n == s.Transponders || !s.Sat) return false;
            s.Transponders = n;
            return true;
        }

        public static void Transit(IspState s, int delta) => s.TransitTier = Mathf.Clamp(s.TransitTier + delta, 0, Cat.TransitMbit.Length - 1);

        public static bool Peering(IspState s, int delta)
        {
            if (delta > 0)
            {
                if (!Money.Invest(s, Cat.PeeringPort)) return false;
                s.PeeringPorts++;
                return true;
            }
            if (s.PeeringPorts <= 0) return false;
            s.PeeringPorts--;
            return true;
        }

        public static void Upkeep(IspState s, float delta) => s.Upkeep = Mathf.Clamp(s.Upkeep + delta, 0f, 50000f);
        public static void Sales(IspState s, float delta) => s.Sales = Mathf.Clamp(s.Sales + delta, 0f, 50000f);

        // ── the coax network ─────────────────────────────────────────────────────────────────────────────────

        public static Hood HoodOf(IspState s, string id)
        {
            if (!s.Hoods.TryGetValue(id, out var h)) s.Hoods[id] = h = new Hood { Id = id };
            return h;
        }

        /// <summary>Nodes a neighbourhood still needs for so many more units (500 each).</summary>
        public static int NodesNeeded(IspState s, string hood, int moreUnits)
        {
            s.Hoods.TryGetValue(hood, out var h);
            int have = (h?.Nodes ?? 0) + (h?.NodesBuilding ?? 0);
            int passed = Sim.HomesPassed(s, hood) + moreUnits;
            int need = Mathf.Max(1, Mathf.CeilToInt(passed / (float)Sim.PerNode(h)));   // GO-DEEP-1: small nodes there
            return Mathf.Max(0, need - have);
        }

        static float NodesCost(IspState s, string hood, int nodes)
        {
            if (nodes <= 0) return 0f;
            s.Hoods.TryGetValue(hood, out var h);
            bool first = (h?.Nodes ?? 0) + (h?.NodesBuilding ?? 0) == 0;
            bool home = City.ByKey.TryGetValue(s.HeadendKey, out var he) && he.Hood == hood;   // next to the headend the fiber is short
            return (first ? (home ? Cat.FirstNodeHome : Cat.FirstNode) + (nodes - 1) * Cat.MoreNode : nodes * Cat.MoreNode) + (h != null && h.D31 ? nodes * Cat.Node31 : 0f)
                 + (h != null && h.Batteries ? nodes * Cat.BatteryPerNode : 0f);   // GO-POWER-1: a new node gets its batteries too
        }

        public static float SiteCost(IspState s, House b, out int nodes)
        {
            nodes = NodesNeeded(s, b.Hood, b.Units);
            return Cat.SiteBase + Cat.SitePerUnit * b.Units + NodesCost(s, b.Hood, nodes);
        }

        public static bool Connect(IspState s, string key)
        {
            if (!City.ByKey.TryGetValue(key, out var b) || !b.Connectable || b.Hood.Length == 0) return false;
            if (!Places.Covered(s, b.Hood)) return false;   // GO-HUBS-1: an office and a customer center there first
            if (s.Sites.TryGetValue(key, out var have) && (have.Coax || have.CoaxHours > 0 || have.Lost)) return false;
            if (have == null && Rival.LockedUntil(s, key, Sim.Today(s)) >= 0) return false;   // the competitor's exclusive contract
            float cost = SiteCost(s, b, out int nodes);
            if (nodes > Sim.FreePortsFor(s, b.Hood)) { PortsFull(s, b.Hood); return false; }   // GO-PORTS-1: a new node needs a free CMTS port
            if (!Money.Invest(s, cost)) return false;
            var hood = HoodOf(s, b.Hood);
            int f = Sim.BuildFactor(s, b.Hood);
            if (nodes > 0)
            {
                hood.NodesBuilding += nodes;
                hood.BuildHours = Mathf.Max(hood.BuildHours, Cat.NodeHours * f);
            }
            var site = have ?? new Site { Key = key, Hood = b.Hood, Homes = b.Homes, Biz = b.Biz, Kind = b.Kind };
            site.CoaxOff = false;
            site.CoaxHours = Mathf.Max((b.Units > 60 ? 48 : 24) * f, hood.BuildHours);
            site.BuiltDay = Sim.Today(s);   // a new connection: maintenance starts low
            if (site.PermitUntil < Sim.Today(s)) site.PermitUntil = Sim.Today(s) + Sim.PermitDays();   // the permission contract with the owner
            s.Sites[key] = site;
            return true;
        }

        /// <summary>Refurbishing a building's coax (new amplifier, taps, cable in the house): what it costs - the transfer point
        /// again, a little cheaper than at first.</summary>
        public static float RefurbishCost(Site site) => (Cat.SiteBase + Cat.SitePerUnit * (site.Homes + site.Biz)) * Cat.Refurbish;

        /// <summary>Refurbishes a building's coax: it counts as new again (low maintenance, few faults).</summary>
        public static bool Refurbish(IspState s, string key)
        {
            if (!s.Sites.TryGetValue(key, out var site) || site.Lost || !site.Coax || site.CoaxOff) return false;
            if (!Money.Invest(s, RefurbishCost(site))) return false;
            site.BuiltDay = Sim.Today(s);
            Sim.Event(s, Go.T("go_event_refurbished", "The coax in {0} is refurbished - it counts as new again.", City.Name(key)));
            return true;
        }

        /// <summary>What renewing a permission contract costs: the permission again (cheaper with field sales, dearer after a tender).</summary>
        public static float RenewCost(IspState s, Site site, bool tender = false) =>
            (Cat.SiteBase + Cat.SitePerUnit * (site.Homes + site.Biz)) * (s.Techs.Exists(t => t.Role == Cat.RoleSales) ? Cat.SalesRenew : 1f) * (tender ? 1.3f : 1f);

        /// <summary>The permission contract runs two more game years from its end (or from today if it has ended).</summary>
        /// <summary>The renewal is billed with the day's running costs (into the red like rent), so a building is never lost only
        /// because the money is short that day.</summary>
        public static bool Renew(IspState s, string key, bool tender = false)
        {
            if (!s.Sites.TryGetValue(key, out var site) || site.Lost) return false;
            s.Today.Permits += RenewCost(s, site, tender);
            site.PermitUntil = Mathf.Max(site.PermitUntil, Sim.Today(s)) + Sim.PermitDays();
            return true;
        }

        /// <summary>GO-PORTS-1: the CMTS ports of a group are all taken - say so (once a day).</summary>
        static void PortsFull(IspState s, string hood)
        {
            string g = Places.Group(s, hood);
            string flag = "portsfull:" + g + ":" + Sim.Day();
            if (Sim.Has(s, flag)) return;
            s.Flags.RemoveAll(x => x.StartsWith("portsfull:" + g + ":"));
            s.Flags.Add(flag);
            Sim.Event(s, Go.T("go_event_portsfull2", "All CMTS ports for {0} are taken ({1} per CMTS) - a new node needs another CMTS in its office.", City.HoodName(g), Cat.CmtsPorts));
        }

        public static bool AddNode(IspState s, string hood)
        {
            if (Sim.FreePortsFor(s, hood) <= 0) { PortsFull(s, hood); return false; }   // GO-PORTS-1
            float cost = NodesCost(s, hood, 1);
            if (!Money.Invest(s, cost)) return false;
            var h = HoodOf(s, hood);
            h.NodesBuilding++;
            h.BuildHours = Mathf.Max(h.BuildHours, Cat.NodeHours * Sim.BuildFactor(s, hood));
            return true;
        }

        /// <summary>GO-NODES-1 (user 2026-11-08: "ich müsste auch Knoten entfernen können, um Geld zu sparen"): one node less - one
        /// under construction is stopped first; a running one only while the others still cover the connected units (500 each).
        /// Fewer nodes mean fewer faults and less upkeep, but less room in the evening. Nothing is refunded.</summary>
        public static bool RemoveNode(IspState s, string hood)
        {
            if (!s.Hoods.TryGetValue(hood, out var h)) return false;
            if (h.NodesBuilding > 0) { h.NodesBuilding--; if (h.NodesBuilding == 0) h.BuildHours = 0; return true; }
            if (!CanRemoveNode(s, h)) return false;
            h.Nodes--;
            return true;
        }

        public static bool CanRemoveNode(IspState s, Hood h) =>
            h.NodesBuilding > 0 || (h.Nodes > 1 && (h.Nodes - 1) * Sim.PerNode(h) >= Sim.HomesPassed(s, h.Id));

        /// <summary>GO-HUBS-1: the feeder fibre from the headend to a neighbourhood's hub - by the distance.</summary>
        public static float LinkCost(IspState s, string hood) => Cat.LinkBase + Cat.LinkPerM * Places.Metres(s.HeadendKey, Places.Hub(s, hood));

        public static bool CanLink(IspState s, string hood) =>
            !Sim.Test && hood != Places.HeadHood(s) && Places.HasOffice(s, hood) && !(s.Hoods.TryGetValue(hood, out var h) && (h.Link || h.LinkHours > 0));

        public static bool BuildLink(IspState s, string hood)
        {
            if (!CanLink(s, hood) || !Money.Invest(s, LinkCost(s, hood))) return false;
            var h = HoodOf(s, hood);
            h.LinkHours = Cat.LinkHours * Sim.BuildFactor(s, hood);
            Sim.Event(s, Go.T("go_event_link_build", "The feeder fibre to the hub in {0} is being built ({1} h).", City.HoodName(hood), h.LinkHours));
            return true;
        }

        /// <summary>GO-HUBS-1: the ring - a second way for a hub's fibre: to the nearest other hub, or a separate way to the
        /// headend (longer).</summary>
        public static float RingCost(IspState s, string hood)
        {
            string partner = Places.RingPartner(s, hood);
            float m = Places.Metres(Places.Hub(s, hood), partner) * (partner == s.HeadendKey ? Cat.RingDetour : 1f);
            return Cat.RingBase + Cat.RingPerM * m;
        }

        public static bool CanRing(IspState s, string hood) =>
            s.Hoods.TryGetValue(hood, out var h) && h.Link && !h.Ring && h.RingHours <= 0;

        public static bool BuildRing(IspState s, string hood)
        {
            if (!CanRing(s, hood) || !Money.Invest(s, RingCost(s, hood))) return false;
            var h = HoodOf(s, hood);
            h.RingHours = Cat.RingHours * Sim.BuildFactor(s, hood);
            Sim.Event(s, Go.T("go_event_ring_build", "The ring for {0} is being closed via {1} ({2} h).", City.HoodName(hood), City.Name(Places.RingPartner(s, hood)), h.RingHours));
            return true;
        }

        // ── GO-UP-1, GO-DEEP-1, GO-RPHY-1: the neighbourhood's network rebuilt ─────────────────────────────────────────

        /// <summary>High split: new amplifiers and couplers on every node (7,000 $ each, two days), then a technician swaps each
        /// connected building's house amplifier - when the last one is done the return path reaches 204 MHz.</summary>
        public static float HighSplitCost(IspState s, string hood) => s.Hoods.TryGetValue(hood, out var h) ? Mathf.Max(1, h.Nodes + h.NodesBuilding) * Cat.HighSplitNode : 0f;

        public static bool CanHighSplit(IspState s, Hood h) => h.D31 && !h.HighSplit && !h.HsHouses && h.HsHours <= 0 && h.Nodes > 0;

        public static bool HighSplit(IspState s, string hood)
        {
            if (!s.Hoods.TryGetValue(hood, out var h) || !CanHighSplit(s, h) || !Money.Invest(s, HighSplitCost(s, hood))) return false;
            h.HsHours = Cat.HighSplitHours * Sim.BuildFactor(s, hood);
            int houses = 0;
            foreach (var site in s.Sites.Values)
            {
                if (site.Hood != hood || !site.Coax || site.CoaxOff || site.Lost) continue;
                var t = Sim.Open(s, Sim.NowHour(), Cat.TtHouseAmp, hood, site.Key, 0);
                t.Due = 0;
                s.Today.WorkTech += Cat.Fault(Cat.TtHouseAmp).work + 0.3f;
                houses++;
            }
            Sim.Event(s, Go.T("go_event_hs_build", "High split in {0}: the nodes are rebuilt ({1} h), then the house amplifiers of {2} buildings are swapped.", City.HoodName(hood), h.HsHours, houses));
            return true;
        }

        /// <summary>Fiber deep: fibre to small nodes for 128 units, the amplifiers go - every node (old and new) 5,500 $; the
        /// new nodes need free ports.</summary>
        public static int DeepNodesFor(IspState s, Hood h) => Mathf.Max(h.Nodes + h.NodesBuilding, Mathf.CeilToInt(Sim.HomesPassed(s, h.Id) / (float)Cat.DeepPerNode));
        public static float DeepCost(IspState s, Hood h) => DeepNodesFor(s, h) * Cat.DeepNode;

        public static bool CanDeep(IspState s, Hood h) =>
            h.D31 && !h.Deep && h.DeepHours <= 0 && h.Nodes > 0 && Sim.FreePortsFor(s, h.Id) >= DeepNodesFor(s, h) - h.Nodes - h.NodesBuilding;

        public static bool FiberDeep(IspState s, string hood)
        {
            if (!s.Hoods.TryGetValue(hood, out var h) || !CanDeep(s, h) || !Money.Invest(s, DeepCost(s, h))) return false;
            h.DeepNodes = DeepNodesFor(s, h);
            h.DeepHours = Cat.DeepHours * Sim.BuildFactor(s, hood);
            Sim.Event(s, Go.T("go_event_deep_build", "Fiber deep in {0}: {1} small nodes are built ({2} h).", City.HoodName(hood), h.DeepNodes, h.DeepHours));
            return true;
        }

        /// <summary>Remote PHY: a remote PHY device in every node (3,000 $ each) - the nodes then run on a vCMTS server of their
        /// group, which needs free ports for all of them.</summary>
        public static float RphyCost(IspState s, Hood h) => Mathf.Max(1, h.Nodes + h.NodesBuilding) * Cat.RphyNode;

        public static bool CanRphy(IspState s, Hood h) =>
            !h.Rphy && h.RphyHours <= 0 && h.Nodes > 0 && Sim.FreeRphyPorts(s, Places.Group(s, h.Id)) >= h.Nodes + h.NodesBuilding;

        public static bool Rphy(IspState s, string hood)
        {
            if (!s.Hoods.TryGetValue(hood, out var h) || !CanRphy(s, h) || !Money.Invest(s, RphyCost(s, h))) return false;
            h.RphyHours = Cat.RphyHours * Sim.BuildFactor(s, hood);
            Sim.Event(s, Go.T("go_event_rphy_build", "Remote PHY in {0}: the nodes get their remote PHY devices ({1} h).", City.HoodName(hood), h.RphyHours));
            return true;
        }

        public static float Upgrade31Cost(IspState s, string hood)
        {
            s.Hoods.TryGetValue(hood, out var h);
            return h == null ? 0f : (h.Nodes + h.NodesBuilding) * Cat.Node31;
        }

        public static bool Upgrade31(IspState s, string hood)
        {
            if (!Sim.Cmts31At(s, hood) || !s.Hoods.TryGetValue(hood, out var h) || h.D31 || h.D31Hours > 0) return false;
            if (!Money.Invest(s, Upgrade31Cost(s, hood))) return false;
            h.D31Hours = Cat.Upgrade31Hours * Sim.BuildFactor(s, hood);
            return true;
        }

        public static int RentedOld(IspState s, string hood)
        {
            int n = 0;
            foreach (var site in s.Sites.Values)
                if (site.Hood == hood) foreach (var c in site.C) if (!c.Fib && c.Dev == Cat.Dev30 && c.Own == Cat.OwnRent) n += c.N;
            return n;
        }

        /// <summary>Rented 3.0 modems of a DOCSIS 3.1 neighbourhood swapped for 3.1 ones by post ($6 each).</summary>
        public static int SwapModems(IspState s, string hood)
        {
            if (!s.Hoods.TryGetValue(hood, out var h) || !h.D31) return 0;
            int left = Mathf.Min(RentedOld(s, hood), s.Stock.M31), done = 0;
            foreach (var site in s.Sites.Values)
            {
                if (site.Hood != hood || left <= 0) continue;
                foreach (var c in site.C.ToArray())
                {
                    if (left <= 0) break;
                    if (c.Fib || c.Dev != Cat.Dev30 || c.Own != Cat.OwnRent || c.N <= 0) continue;
                    int n = Mathf.Min(left, c.N);
                    c.N -= n; left -= n; done += n;
                    Sim.Add(site, c.T, Cat.Dev31, Cat.OwnRent, c.Biz, false, n);
                }
                site.C.RemoveAll(x => x.N <= 0);
            }
            s.Stock.M31 -= done; s.Stock.M30 += done;
            s.Today.Ship += done * Cat.Ship;
            return done;
        }

        // ── fiber ────────────────────────────────────────────────────────────────────────────────────────────

        public static float FiberCost(IspState s, House b, out int ports)
        {
            ports = Mathf.CeilToInt(b.Units * Cat.MaxShare / Sim.PonCustomers(s));
            s.Hoods.TryGetValue(b.Hood, out var h);
            return Cat.FiberSiteBase + Cat.FiberPerUnit * b.Units + ports * Cat.PonPort + (h != null && h.FiberIn ? 0f : FiberFeed);
        }

        public static bool ConnectFiber(IspState s, string key)
        {
            if (!City.ByKey.TryGetValue(key, out var b) || !b.Connectable || b.Hood.Length == 0 || !Sim.OltAt(s, b.Hood)) return false;
            if (!Places.Covered(s, b.Hood)) return false;   // GO-HUBS-1
            if (s.Sites.TryGetValue(key, out var have) && (have.Fiber || have.FiberHours > 0 || have.Lost)) return false;
            if (have == null && Rival.LockedUntil(s, key, Sim.Today(s)) >= 0) return false;   // the competitor's exclusive contract
            float cost = FiberCost(s, b, out int ports);
            if (!Money.Invest(s, cost)) return false;
            var hood = HoodOf(s, b.Hood);
            hood.FiberIn = true;
            hood.PonPorts += ports;
            var site = have ?? new Site { Key = key, Hood = b.Hood, Homes = b.Homes, Biz = b.Biz, Kind = b.Kind };
            site.FiberHours = 24 * Mathf.Clamp(Mathf.CeilToInt(b.Units / 40f), 1, 4) * Sim.BuildFactor(s, b.Hood);
            site.FiberDay = Sim.Today(s);
            if (site.PermitUntil < Sim.Today(s)) site.PermitUntil = Sim.Today(s) + Sim.PermitDays();
            s.Sites[key] = site;
            return true;
        }

        /// <summary>The coax customers of a fiber building get an appointment to move (an ONT each from the stock).</summary>
        public static int MoveToFiber(IspState s, string key)
        {
            if (!s.Sites.TryGetValue(key, out var site) || !site.Fiber) return 0;
            int coax = Sim.CustomersOf(site, fiber: false), planned = 0;
            foreach (var t in s.Tickets) if (t.Type == Cat.TtInstall && t.Move && t.Site == key) planned += t.N;
            int n = Mathf.Min(coax - planned, s.Stock.Ont);
            if (n <= 0) return 0;
            s.Stock.Ont -= n;
            var ticket = Sim.Open(s, Now, Cat.TtInstall, site.Hood, key, 0);
            ticket.Move = true; ticket.Dev = Cat.DevOnt; ticket.Fib = true; ticket.N = n;
            ticket.Work = n * Cat.Fault(Cat.TtInstall).work; ticket.Need = Mathf.Clamp(n, 1, 4);
            ticket.Due = Now + 48;
            return n;
        }

        public static bool CoaxOff(IspState s, string key)
        {
            if (!s.Sites.TryGetValue(key, out var site) || !site.Fiber || site.CoaxOff || Sim.CustomersOf(site, fiber: false) > 0) return false;
            site.CoaxOff = true; site.Coax = false; site.CoaxHours = -1;
            return true;
        }

        // ── tariffs, devices, TV ─────────────────────────────────────────────────────────────────────────────

        public static Tariff AddTariff(IspState s, bool fiber, bool tvOnly)
        {
            var t = new Tariff { Id = s.NextTariff++, Fiber = fiber };
            if (tvOnly) { t.Mbit = 0; t.Tv = true; t.Price = 0.59f; t.Name = Go.T("go_tariff_tvonly", "Cable TV"); }
            else if (fiber) { t.Mbit = 1000; t.Price = 2.99f; t.Name = "Glasfaser 1000"; }
            else { t.Mbit = 100; t.Price = 2.09f; t.Name = Go.T("go_tariff_new", "New tariff"); }
            s.Tariffs.Add(t);
            return t;
        }

        public static bool DeleteTariff(IspState s, Tariff t)
        {
            foreach (var site in s.Sites.Values) foreach (var c in site.C) if (c.T == t.Id && c.N > 0) return false;
            foreach (var k in s.Tickets) if (k.Type == Cat.TtInstall && k.T == t.Id) return false;
            s.Tariffs.Remove(t);
            return true;
        }

        public static int Users(IspState s, Tariff t)
        {
            int n = 0;
            foreach (var site in s.Sites.Values) foreach (var c in site.C) if (c.T == t.Id) n += c.N;
            return n;
        }

        /// <summary>GO-UP-1: the next upload of a coax tariff (automatic, then 10 ... 200 Mbit/s, never above the download).</summary>
        public static int NextUp(Tariff t)
        {
            var list = Cat.CoaxUploads;
            int i = System.Array.IndexOf(list, t.Up);
            for (int k = 1; k <= list.Length; k++)
            {
                int v = list[(i + k + list.Length) % list.Length];
                if (v <= t.Mbit) return v;
            }
            return 0;
        }

        public static int NextSpeed(Tariff t, int dir)
        {
            var list = t.Fiber ? FiberSpeeds : CoaxSpeeds;
            int i = System.Array.IndexOf(list, t.Mbit);
            if (i < 0) i = 0;
            return list[Mathf.Clamp(i + dir, 0, list.Length - 1)];
        }

        public static bool BuyDevices(IspState s, byte dev, int n)
        {
            if (!Sim.Test) return false;   // GAME-TYPES-1: sold in the game's stores
            if (n <= 0) return false;
            if (dev == Cat.Dev31 && !s.Cmts31) return false;
            if (dev == Cat.DevOnt && !s.Olt) return false;
            if (!Money.Invest(s, n * Sim.Buy(s, Sim.DeviceCost(dev)), true)) return false;
            Sim.PutStock(s, dev, n);
            return true;
        }

        public static bool BuyCards(IspState s, int n)
        {
            if (!Sim.Test) return false;   // GAME-TYPES-1: sold in the game's stores
            if (!s.Cas || n <= 0 || !Money.Invest(s, n * Sim.Buy(s, Cat.BuyCard), true)) return false;
            s.Stock.Cards += n;
            return true;
        }

        public static void Contract(IspState s, string id, bool on)
        {
            var have = s.PayTv.Find(p => p.Id == id);
            if (on && have == null)
            {
                var o = Cat.Offer(id);
                if (o == null) return;
                s.PayTv.Add(new PayTv { Id = id, Price = o.Recommended });
                Sim.Event(s, Go.T("go_event_paytv_on", "Contract with {0} signed.", o.Name));
            }
            else if (!on && have != null)
            {
                s.Stock.Cards += have.Subs;   // the cards come back
                s.PayTv.Remove(have);
                var o = Cat.Offer(id);
                Sim.Event(s, Go.T("go_event_paytv_off", "Contract with {0} ended.", o?.Name ?? id));
            }
        }

        // ── the team ─────────────────────────────────────────────────────────────────────────────────────────

        public static void Hire(IspState s, Candidate c)
        {
            if (!Sim.Test) return;   // GAME-TYPES-1: the game's recruitment agencies
            if (!s.Candidates.Remove(c)) return;
            byte shift = 0;
            int early = s.Techs.FindAll(t => t.Role == c.Role && t.Shift == 0).Count, late = s.Techs.FindAll(t => t.Role == c.Role && t.Shift == 1).Count;
            if (late < early) shift = 1;
            var tech = new Tech { Id = s.NextId++, Role = c.Role, Name = c.Name, Skill = c.Skill, Wage = c.Wage, Shift = shift, Manner = c.Manner };
            if (c.Role == Cat.RoleMaster)
            {
                // the first neighbourhood of the network without a master
                foreach (var h in s.Hoods.Keys)
                    if (!s.Techs.Exists(x => x.Role == Cat.RoleMaster && x.Hood == h)) { tech.Hood = h; break; }
                if (tech.Hood.Length == 0) foreach (var h in s.Hoods.Keys) { tech.Hood = h; break; }
            }
            s.Techs.Add(tech);
            Sim.Event(s, Go.T("go_event_hired", "{0} joins the team.", c.Name) + " (" + RoleName(c.Role) + ")");
        }

        /// <summary>A network master's neighbourhood: the next one of the network.</summary>
        public static void NextHood(IspState s, Tech t)
        {
            var hoods = new List<string>(s.Hoods.Keys);
            hoods.Sort();
            if (hoods.Count == 0) return;
            int i = hoods.IndexOf(t.Hood);
            t.Hood = hoods[(i + 1) % hoods.Count];
        }

        public static string RoleName(byte role) =>
            role == Cat.RoleMaster ? Go.T("go_role_master", "Network master") : role == Cat.RoleSwitch ? Go.T("go_role_switch", "Switching staff")
            : role == Cat.RoleService ? Go.T("go_role_service", "Customer service") : role == Cat.RoleSupport ? Go.T("go_role_support", "Technical support")
            : role == Cat.RoleSales ? Go.T("go_role_sales", "Field sales") : Go.T("go_role_tech", "Technician");

        public static void Fire(IspState s, Tech t)
        {
            foreach (var k in s.Tickets) k.Techs.Remove(t.Id);
            s.Techs.Remove(t);
            Sim.Event(s, Go.T("go_event_fired", "{0} left the team.", t.Name));
        }

        /// <summary>Leased vans (vans = true) or cars: one more or one less, paid per day.</summary>
        public static void Lease(IspState s, bool vans, int delta)
        {
            if (vans) s.LeaseVans = Mathf.Clamp(s.LeaseVans + delta, 0, 40);
            else s.LeaseCars = Mathf.Clamp(s.LeaseCars + delta, 0, 20);
        }

        /// <summary>A customer-care training: the person treats customers one step better (at most once in 30 days).</summary>
        public static bool Train(IspState s, Tech t)
        {
            int today = Sim.Today(s);
            if (t.Manner >= 5 || today - t.TrainedDay < Cat.TrainingDays) return false;
            if (!Money.Invest(s, Cat.Training)) return false;
            t.Manner++;
            t.TrainedDay = today;
            Sim.Event(s, Go.T("go_event_trained", "{0} took a customer-care training.", t.Name));
            return true;
        }

        public static bool CanTrain(IspState s, Tech t) => t.Manner < 5 && Sim.Today(s) - t.TrainedDay >= Cat.TrainingDays;

        public static void NextShift(IspState s, Tech t)
        {
            t.Shift = (byte)((t.Shift + 1) % 3);
            foreach (var k in s.Tickets) k.Techs.Remove(t.Id);   // the job goes back to the others
            t.Ticket = 0; t.Travel = 0f; t.At = "";
        }

        public static string ShiftName(byte shift) =>
            shift == 0 ? Go.T("go_shift_early", "Early 6-14") : shift == 1 ? Go.T("go_shift_late", "Late 14-22") : Go.T("go_shift_night", "Night 22-6");

        // ── the player on site ───────────────────────────────────────────────────────────────────────────────

        public static bool Navigate(IspState s, Ticket t)
        {
            string key = Sim.PlaceKey(s, t);
            return key.Length > 0 && City.Navigate(key);
        }

        public static void Repair(IspState s, Ticket t)
        {
            s.PlayerTicket = s.PlayerTicket == t.Id ? 0 : t.Id;
            if (s.PlayerTicket != 0) Navigate(s, t);
        }
    }
}
