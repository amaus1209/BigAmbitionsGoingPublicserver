using System.Collections.Generic;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>The provider's clockwork: every game hour the network's load, faults and the staff's work, every game day the
    /// bill, new customers and cancellations (KONZEPT.md has the numbers and their sources).
    ///
    /// A new customer's way (user 2026-11-06): the customer service takes the order -> the old phone number is ported (or a
    /// new one handed out) -> the switching staff connect the tap in the building (coax) -> self-install by post, or an
    /// appointment with a technician (fiber always) -> the customer is live and pays. A coax customer who cancels keeps the
    /// signal until the switching staff disconnect the tap.</summary>
    internal static class Sim
    {
        static readonly System.Random Rng = new System.Random(System.Environment.TickCount);

        /// <summary>The self-test (SelfTest.cs): no money moves, no notification shows, the headend counts as the player's.</summary>
        internal static bool Test;

        internal static float Rand() => (float)Rng.NextDouble();
        static int Round(float x) { if (x <= 0f) return 0; int n = (int)x; return n + (Rand() < x - n ? 1 : 0); }
        static int Sample(float expected) => expected < 1f ? (Rand() < expected ? 1 : 0) : Round(expected);
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        // ── counts ───────────────────────────────────────────────────────────────────────────────────────────

        public static Tariff? TariffOf(IspState s, int id)
        {
            foreach (var t in s.Tariffs) if (t.Id == id) return t;
            return null;
        }

        public static int Customers(IspState s)
        {
            int n = 0;
            foreach (var site in s.Sites.Values) foreach (var c in site.C) n += c.N;
            return n;
        }

        public static int CustomersOf(Site site, bool? fiber = null, bool? biz = null)
        {
            int n = 0;
            foreach (var c in site.C)
                if ((fiber == null || c.Fib == fiber) && (biz == null || c.Biz == biz)) n += c.N;
            return n;
        }

        static int Count(IspState s, System.Func<Tariff, bool> f)
        {
            int n = 0;
            foreach (var site in s.Sites.Values)
                foreach (var c in site.C) { var t = TariffOf(s, c.T); if (t != null && f(t)) n += c.N; }
            return n;
        }

        // ── GO-CAP-1: what the devices serve. GO-HUBS-1 (0.8): CMTS and OLT per group - the headend's neighbourhood (with
        // every neighbourhood that has no hub of its own) or a hub's; telephony, IPTV and the CA system at the headend ──

        /// <summary>Devices of a kind in the headend's group (a piece bought in the app before 0.6 counts one).</summary>
        public static int Units(IspState s, string item, string flag) => Places.Units(s, Places.HeadHood(s), item, flag);

        const int NoLimit = 1000000;
        public static int CapCmts(IspState s, string g) => Test ? NoLimit : Places.Units(s, g, GameTypes.ICmts30, "cmts30") * Cat.CapCmts30 + Places.Units(s, g, GameTypes.ICmts31, "cmts31") * Cat.CapCmts31
                                                                          + Places.Units(s, g, GameTypes.IVcmts, "vcmts") * Cat.CapVcmts;   // GO-RPHY-1
        /// <summary>GO-PORTS-1: the CMTS ports (8 per CMTS) and the nodes on them. More nodes than ports share ports, as real
        /// cable networks combine nodes on one port - then they share its capacity, and the evenings get slow.</summary>
        public static int Ports(IspState s, string g) => Test ? NoLimit : (Places.Units(s, g, GameTypes.ICmts30, "cmts30") + Places.Units(s, g, GameTypes.ICmts31, "cmts31")) * Cat.CmtsPorts;
        /// <summary>GO-RPHY-1: the vCMTS servers' ports - for the remote PHY nodes only.</summary>
        public static int RphyPorts(IspState s, string g) => Test ? NoLimit : Places.Units(s, g, GameTypes.IVcmts, "vcmts") * Cat.VcmtsPorts;
        /// <summary>The nodes of a group on the CMTS ports (rphy false) or on the vCMTS (rphy true).</summary>
        public static int NodesIn(IspState s, string g, bool building = true, bool rphy = false)
        {
            int n = 0;
            foreach (var h in s.Hoods.Values) if (h.Rphy == rphy && Places.Group(s, h.Id) == g) n += h.Nodes + (building ? h.NodesBuilding : 0);
            return n;
        }
        public static int FreePorts(IspState s, string g) => Mathf.Max(0, Ports(s, g) - NodesIn(s, g));
        public static int FreeRphyPorts(IspState s, string g) => Mathf.Max(0, RphyPorts(s, g) - NodesIn(s, g, true, true));
        public static int FreePortsFor(IspState s, string hood)
        {
            string g = Places.Group(s, hood);
            return s.Hoods.TryGetValue(hood, out var h) && h.Rphy ? FreeRphyPorts(s, g) : FreePorts(s, g);
        }
        public static float PortShare(IspState s, string g)
        {
            int ports = Ports(s, g), nodes = NodesIn(s, g, false);
            return nodes <= ports ? 1f : ports <= 0 ? 0f : (float)ports / nodes;
        }
        /// <summary>The share of a node's capacity it gets on its pool of ports (CMTS or vCMTS).</summary>
        public static float PortShareFor(IspState s, Hood hood)
        {
            string g = Places.Group(s, hood.Id);
            if (!hood.Rphy) return PortShare(s, g);
            int ports = RphyPorts(s, g), nodes = NodesIn(s, g, false, true);
            return nodes <= ports ? 1f : ports <= 0 ? 0f : (float)ports / nodes;
        }
        /// <summary>GO-DEEP-1: units one node serves in this neighbourhood.</summary>
        public static int PerNode(Hood? h) => h != null && h.Deep ? Cat.DeepPerNode : Cat.HomesPerNode;
        /// <summary>GO-UP-1: a tariff's upload - fiber symmetric, coax its own or a tenth of the download (5 to 50).</summary>
        public static int UpOf(Tariff t) => t.Mbit <= 0 ? 0 : t.Fiber ? t.Mbit : t.Up > 0 ? t.Up : Mathf.Clamp(t.Mbit / 10, 5, Cat.UpLowMax);

        public static int CapOlt(IspState s, string g) => Test ? NoLimit : Places.Units(s, g, GameTypes.IOlt, "olt") * Cat.CapOlt;
        /// <summary>DOCSIS 3.1 and fibre are there for a neighbourhood when its group's office has the CMTS 3.1 or the OLT.</summary>
        public static bool Cmts31At(IspState s, string hood) => Test ? s.Cmts31 : Places.Units(s, Places.Group(s, hood), GameTypes.ICmts31, "cmts31") + Places.Units(s, Places.Group(s, hood), GameTypes.IVcmts, "vcmts") > 0;
        public static bool OltAt(IspState s, string hood) => Test ? s.Olt : Places.Units(s, Places.Group(s, hood), GameTypes.IOlt, "olt") > 0;
        /// <summary>An OLT stands in any of the provider's offices (the fibre pages show then).</summary>
        public static bool AnyOlt(IspState s)
        {
            if (Test || s.Olt) return s.Olt;
            foreach (var g in Places.Groups(s)) if (Places.Units(s, g, GameTypes.IOlt, "olt") > 0) return true;
            return false;
        }
        public static int CapVoice(IspState s) => Test ? NoLimit : Units(s, GameTypes.IVoice, "voice") * Cat.CapVoice;
        public static int CapIptv(IspState s) => Test ? NoLimit : Units(s, GameTypes.IIptv, "iptv") * Cat.CapIptv;
        public static int CapCas(IspState s) => Test ? NoLimit : Units(s, GameTypes.ICas, "cas") * Cat.CapCas;

        static int CountG(IspState s, string g, System.Func<Tariff, Cohort, bool> f)
        {
            int n = 0;
            foreach (var site in s.Sites.Values)
            {
                if (Places.Group(s, site.Hood) != g) continue;
                foreach (var c in site.C) { var t = TariffOf(s, c.T); if (t != null && f(t, c)) n += c.N; }
            }
            return n;
        }
        public static int UseCmts(IspState s, string g) => CountG(s, g, (t, c) => t.Mbit > 0 && !c.Fib);
        public static int UseOlt(IspState s, string g) => CountG(s, g, (t, c) => t.Mbit > 0 && c.Fib);
        public static int UsePayTv(IspState s) { int n = 0; foreach (var p in s.PayTv) n += p.Subs; return n; }

        // free places for new customers, counted once at the start of an hour's joining and used up while it signs: modems and
        // OLT places per group, the people of a group (GO-STAFF-1), telephony and IPTV at the headend
        static readonly Dictionary<string, int> _freeCmts = new Dictionary<string, int>(), _freeOlt = new Dictionary<string, int>(), _freeStaff = new Dictionary<string, int>();
        static readonly Dictionary<string, byte> _shortRole = new Dictionary<string, byte>();
        static int _freeVoice, _freeIptv;
        static bool _capsLive;

        static void RefreshCaps(IspState s)
        {
            Places.Refresh(s, true);
            _freeCmts.Clear(); _freeOlt.Clear(); _freeStaff.Clear(); _shortRole.Clear();
            foreach (var g in Places.Groups(s))
            {
                _freeCmts[g] = Mathf.Max(0, CapCmts(s, g) - UseCmts(s, g) - Deals.WsInGroup(s, g));   // GO-WHOLESALE-1: their modems too
                _freeOlt[g] = Mathf.Max(0, CapOlt(s, g) - UseOlt(s, g));
                var (free, role) = Places.StaffFree(s, g);
                if (!Places.RulesOn(s)) { free = NoLimit; role = 255; }   // a provider from before 0.8: not before its grace days are over
                _freeStaff[g] = free; _shortRole[g] = role;
            }
            _freeVoice = Mathf.Max(0, CapVoice(s) - PhoneCustomers(s));
            _freeIptv = Mathf.Max(0, CapIptv(s) - IptvCustomers(s));
            _capsLive = true;
        }

        static int Free(Dictionary<string, int> d, string g) => d.TryGetValue(g, out int n) ? n : 0;

        /// <summary>How many more customers of this tariff a neighbourhood takes now: the devices of its group (and the
        /// headend's), its people.</summary>
        static int FreeFor(IspState s, Tariff t, string hood)
        {
            if (!_capsLive) RefreshCaps(s);
            string g = Places.Group(s, hood);
            int free = Test ? NoLimit : Free(_freeStaff, g);
            if (t.Mbit > 0) free = Mathf.Min(free, t.Fiber ? Free(_freeOlt, g) : Free(_freeCmts, g));
            if (t.Phone && t.Mbit > 0) free = Mathf.Min(free, _freeVoice);
            if (t.Ip && t.Tv && t.Mbit > 0) free = Mathf.Min(free, _freeIptv);
            return free;
        }

        static void UseFor(IspState s, Tariff t, int n, string hood)
        {
            string g = Places.Group(s, hood);
            if (_freeStaff.ContainsKey(g)) _freeStaff[g] -= n;
            if (t.Mbit > 0)
            {
                var d = t.Fiber ? _freeOlt : _freeCmts;
                if (d.ContainsKey(g)) d[g] -= n;
            }
            if (t.Phone && t.Mbit > 0) _freeVoice -= n;
            if (t.Ip && t.Tv && t.Mbit > 0) _freeIptv -= n;
        }

        /// <summary>A device kind or the people are full: once a day an entry in the event log, so the player knows what to set
        /// up or whom to hire.</summary>
        static void CapNote(IspState s, Tariff t, string hood)
        {
            string g = Places.Group(s, hood);
            string what = !Test && Free(_freeStaff, g) <= 0 ? "staff"
                        : t.Mbit <= 0 ? "" : t.Fiber && Free(_freeOlt, g) <= 0 ? "olt" : !t.Fiber && Free(_freeCmts, g) <= 0 ? "cmts"
                        : t.Phone && _freeVoice <= 0 ? "voice" : t.Ip && t.Tv && _freeIptv <= 0 ? "iptv" : "";
            if (what.Length == 0) return;
            string flag = "capfull:" + what + ":" + g + ":" + Day();
            if (Has(s, flag)) return;
            s.Flags.RemoveAll(x => x.StartsWith("capfull:" + what + ":" + g + ":"));
            s.Flags.Add(flag);
            string where = City.HoodName(g);
            if (what == "staff")
            {
                byte role = _shortRole.TryGetValue(g, out var r) ? r : Cat.RoleTech;
                Event(s, Go.T("go_event_stafffull", "Too few people for {0} ({1}) - new customers there wait until you hire more: one person serves {2} customers.", where, Act.RoleName(role), Places.PerPerson(role)));
                return;
            }
            string name = what == "cmts" ? "CMTS" : what == "olt" ? "OLT" : what == "voice" ? Go.T("go_net_voice", "Voice platform") : Go.T("go_net_iptv", "IPTV platform");
            if (what == "cmts" || what == "olt")
                Event(s, Go.T("go_event_capfull_at", "{0} for {1} is full - new customers there wait until another one stands in its office.", name, where));
            else
                Event(s, Go.T("go_event_capfull", "{0} is full - new customers wait until another one stands in the headend.", name));
        }

        public static int TvCustomers(IspState s) => Count(s, t => t.Tv || t.Mbit == 0);
        public static int InternetCustomers(IspState s) => Count(s, t => t.Mbit > 0);
        public static int PhoneCustomers(IspState s) => Count(s, t => t.Phone && t.Mbit > 0);
        public static int IptvCustomers(IspState s) => Count(s, t => t.Ip && t.Tv && t.Mbit > 0);

        public static int FreeRiders(IspState s) { int n = 0; foreach (var site in s.Sites.Values) n += site.FreeRiders; return n; }
        public static int FreeNumbers(IspState s) => Mathf.Max(0, s.NumberBlocks * 1000 - s.NumbersUsed);

        /// <summary>Customers on their way in: in the tickets of one kind (orders, porting, switching on, installations).</summary>
        public static int InPipeline(IspState s, byte type, bool? move = null)
        {
            int n = 0;
            foreach (var t in s.Tickets) if (t.Type == type && (move == null || t.Move == move)) n += t.N;
            return n;
        }

        /// <summary>Homes passed in a neighbourhood's coax (the units of its coax buildings, ready or being built).</summary>
        public static int HomesPassed(IspState s, string hood)
        {
            int n = 0;
            foreach (var site in s.Sites.Values)
                if (site.Hood == hood && !site.CoaxOff && (site.Coax || site.CoaxHours > 0)) n += site.Homes + site.Biz;
            return n;
        }

        public static int HoodCoaxCustomers(IspState s, string hood)
        {
            int n = 0;
            foreach (var site in s.Sites.Values) if (site.Hood == hood) n += CustomersOf(site, fiber: false);
            return n;
        }

        public static int HoodFiberCustomers(IspState s, string hood)
        {
            int n = 0;
            foreach (var site in s.Sites.Values) if (site.Hood == hood) n += CustomersOf(site, fiber: true);
            return n;
        }

        public static int Devices(IspState s, byte dev, byte own)
        {
            int n = 0;
            foreach (var site in s.Sites.Values) foreach (var c in site.C) if (c.Dev == dev && c.Own == own) n += c.N;
            return n;
        }

        /// <summary>GO-TV-2 (user 2026-11-08: "ein Sat-Gerät, ein Transponder"; before, the transmodulators were bought in the
        /// app, two transponders each): every Sat device that stands in the headend puts one transponder on the cable. The
        /// transponders set in the app stay; on air are as many as there are devices (a provider from before 0.6 that bought
        /// its satellite dishes in the app counts one device). The self-test has no rooms and keeps the old way.</summary>
        public static int SatDevices(IspState s)
        {
            if (Test) return Cat.MaxTransponders;
            int n = 0;
            try { n = Units(s, GameTypes.ISat, "sat"); } catch { }
            if (n == 0 && s.AppGear.Contains("sat")) n = 1;
            return n;
        }

        /// <summary>The transponders on the cable now: those set, at most one per Sat device.</summary>
        public static int OnAir(IspState s) => Mathf.Min(s.Transponders, SatDevices(s));

        public static bool TvReady(IspState s) => s.Sat && OnAir(s) > 0 && !s.HeadendDown;
        public static bool IptvReady(IspState s) => s.Iptv && s.Sat && !s.HeadendDown;
        public static bool PhoneReady(IspState s) => s.Voice && !s.HeadendDown;
        public static int Channels(IspState s) => OnAir(s) * Cat.ChannelsPerTransponder;
        /// <summary>How much an offer of so many channels draws (enough from about 120 channels).</summary>
        public static float TvDraw(IspState s) => Mathf.Clamp(Channels(s) / 120f, 0.3f, 1f);

        // ── capacity ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>DOCSIS 3.0 slots and the OFDM blocks left for 3.1 on one node, after the TV transponders.</summary>
        public static (int d30, int ofdm) Spectrum(IspState s, bool d31)
        {
            int free = Mathf.Max(0, Cat.Slots - OnAir(s));
            int d30 = Mathf.Min(Cat.D30Slots, free);
            int ofdm = d31 ? (free - d30) / Cat.OfdmSlots : 0;
            return (d30, ofdm);
        }

        public static float NodeMbit(IspState s, bool d31)
        {
            var (d30, ofdm) = Spectrum(s, d31);
            return d30 * Cat.SlotMbit + ofdm * Cat.OfdmMbit;
        }

        public static int PonCustomers(IspState s) => s.Split64 ? 64 : 32;

        public static int Unlock31(IspState s) => Mathf.Max(0, Cat.Unlock31 * Pct("unlock31", 100) / 100);
        public static int UnlockFiber(IspState s) => Mathf.Max(0, Cat.UnlockFiber * Pct("unlockfiber", 100) / 100);
        static int Pct(string id, int def) { try { return Go.Host?.Percent(id, def) ?? def; } catch { return def; } }
        internal static int Option(string id, int def) => Pct(id, def);

        public static bool Has(IspState s, string flag) => s.Flags.Contains(flag);
        public static void Event(IspState s, string text)
        {
            s.Events.Insert(0, Go.T("go_event_day", "Day {0}", Day()) + ": " + text);
            if (s.Events.Count > 40) s.Events.RemoveRange(40, s.Events.Count - 40);
        }

        public static int Day() { try { return SaveGameManager.Current?.Day ?? 0; } catch { return 0; } }

        /// <summary>How old a connection is, in game years (built -1 = at the founding).</summary>
        public static float AgeYears(IspState s, int built, int today)
        {
            if (built < 0) built = (int)(s.FoundedHour / 24);
            return Mathf.Max(0f, (today - built) / (float)DaysPerYear());
        }

        /// <summary>The coax age of a neighbourhood: its buildings' ages weighted by their units.</summary>
        public static float HoodCoaxAge(IspState s, string hood, int today)
        {
            float sum = 0f; int units = 0;
            foreach (var site in s.Sites.Values)
                if (site.Hood == hood && site.Coax && !site.CoaxOff) { int u = site.Homes + site.Biz; sum += u * AgeYears(s, site.BuiltDay, today); units += u; }
            return units > 0 ? sum / units : 0f;
        }

        static float HoodFiberAge(IspState s, string hood, int today)
        {
            float sum = 0f; int units = 0;
            foreach (var site in s.Sites.Values)
                if (site.Hood == hood && site.Fiber) { int u = site.Homes + site.Biz; sum += u * AgeYears(s, site.FiberDay, today); units += u; }
            return units > 0 ? sum / units : 0f;
        }

        /// <summary>A purchase price after the volume discount the provider's size brings.</summary>
        public static float Buy(IspState s, float list) => list * (1f - Cat.Volume(Customers(s)));

        /// <summary>Today in the provider's own clock: the game's day, or the simulated day in the self-test.</summary>
        public static int Today(IspState s) => Test ? (int)(s.LastHour / 24) : Day();
        public static long NowHour() { try { var g = SaveGameManager.Current; return g == null ? -1 : (long)g.Day * 24 + g.Hour; } catch { return -1; } }

        /// <summary>A network master works in this neighbourhood (with a workplace and a car at the last hour on duty).</summary>
        public static bool HasMaster(IspState s, string hood)
        {
            foreach (var t in s.Techs) if (t.Role == Cat.RoleMaster && t.Hood == hood && t.Equipped) return true;
            return false;
        }

        /// <summary>Building in a neighbourhood without a network master takes twice as long.</summary>
        public static int BuildFactor(IspState s, string hood) => HasMaster(s, hood) ? 1 : 2;

        static bool OnDutyWith(IspState s, byte role)
        {
            foreach (var t in s.Techs) if (t.Role == role && t.Missing == 0) return true;
            return false;
        }

        /// <summary>Hours of work waiting for a role now.</summary>
        public static float Backlog(IspState s, byte role)
        {
            float h = 0f;
            bool support = s.Techs.Exists(t => t.Role == Cat.RoleSupport);
            foreach (var t in s.Tickets)
            {
                float left = Cat.PerUnit(t.Type) ? t.N * Cat.Fault(t.Type).work : Mathf.Max(0f, t.Work - t.Done);
                switch (role)
                {
                    case Cat.RoleTech:
                        if (t.Type == Cat.TtInstall || t.Type == Cat.TtHouseAmp || (Cat.IsFault(t.Type) && (t.Type != Cat.TtWifi || !support))) h += left;
                        break;
                    case Cat.RoleSwitch: if (t.Type == Cat.TtOn || t.Type == Cat.TtOff) h += left; break;
                    case Cat.RoleService: if (t.Type == Cat.TtOrder || t.Type == Cat.TtComplaint) h += left; break;
                    case Cat.RoleSupport: if (t.Type == Cat.TtWifi) h += left; else if (!t.Diag && Cat.IsFault(t.Type)) h += Cat.DiagWork; break;
                }
            }
            if (role == Cat.RoleService) h += s.Calls * Cat.CallWork;
            return h;
        }

        /// <summary>How many people a role needs for yesterday's work and half of what waits now (7.5 hours each, more with
        /// overtime; the founder's 6 hours a day count for orders, calls and switching); technicians: one at least, two from
        /// 400 customers, one per shift from 1,000 (faults come round the clock); network masters one per neighbourhood.
        /// -1 = no fixed need (field sales).</summary>
        public static int Need(IspState s, byte role)
        {
            if (role == Cat.RoleSales) return -1;
            if (role == Cat.RoleMaster)
            {   // one per neighbourhood with a network, and GO-STAFF-1 one per Cat.MasterSpan people in the field of each group
                int n = 0;
                foreach (var h in s.Hoods.Values) if (h.Nodes + h.NodesBuilding > 0 || h.FiberIn) n++;
                int span = 0;
                foreach (var g in Places.Groups(s)) span += Mathf.CeilToInt(Places.Span(s, g).field / (float)Cat.MasterSpan);
                return Mathf.Max(n, span);
            }
            var y = s.Books.Count > 0 ? s.Books[0] : s.Today;
            float work = role == Cat.RoleTech ? y.WorkTech : role == Cat.RoleSwitch ? y.WorkSwitch : role == Cat.RoleService ? y.WorkService : y.WorkSupport;
            float per = 7.5f * (s.Overtime ? Cat.OvertimeHours : 1f);
            float hours = work + 0.5f * Backlog(s, role);
            // the founder lends a hand 8-20 at half time: orders and calls first, the rest for switching
            float founder = Cat.FounderService * 12f;
            float office = y.WorkService + 0.5f * Backlog(s, Cat.RoleService) + y.CallsIn * Cat.CallWork;
            if (role == Cat.RoleService) hours -= founder;
            else if (role == Cat.RoleSwitch) hours -= Mathf.Max(0f, founder - office);
            int need = Mathf.CeilToInt(Mathf.Max(0f, hours) / per);
            if (role == Cat.RoleTech)
            {   // faults come round the clock - but a small start makes do with one person by day
                int n = Customers(s);
                need = Mathf.Max(need, n >= 1000 ? 3 : n >= 400 ? 2 : 1);
            }
            return need;
        }

        // ── the hour ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Runs every game hour from the last one done up to now. A gap of more than two days (a player back on a
        /// server after a while) is not caught up - the provider rested with the player's other businesses.</summary>
        public static void Advance(IspState s)
        {
            long now = NowHour();
            if (now < 0) return;
            if (s.LastHour < 0 || now < s.LastHour) { s.LastHour = now; return; }
            if (now - s.LastHour > 48)
            {
                Go.Log($"{now - s.LastHour} hours passed without the provider - not caught up.");
                s.LastHour = now;
                return;
            }
            while (s.LastHour < now)
            {
                long h = s.LastHour + 1;
                if (h % 24 == 0) EndOfDay(s, (int)(h / 24) - 1);
                Hour(s, h);
                s.LastHour = h;
            }
        }

        internal static void Hour(IspState s, long abs)
        {
            int h = (int)(abs % 24);
            Build(s);
            CheckHeadend(s);
            Load(s, h, abs);
            Faults(s, abs);
            Staff(s, abs, h);
            Ports(s, abs);
            Grid.Hour(s, abs);   // GO-POWER-1: power cuts begin and end
            Events.Hour(s, abs);   // GO-EVENTS-1: storms, matches, excavators, the press, test wins, subsidies
            Outages(s, abs);
            Appointments(s, abs);
        }

        static void Build(IspState s)
        {
            foreach (var site in s.Sites.Values)
            {
                if (site.CoaxHours > 0 && --site.CoaxHours == 0)
                {
                    site.Coax = true; site.CoaxHours = -1;
                    Event(s, Go.T("go_event_site_ready", "{0} is connected.", City.Name(site.Key)));
                }
                if (site.FiberHours > 0 && --site.FiberHours == 0)
                {
                    site.Fiber = true; site.FiberHours = -1;
                    Event(s, Go.T("go_event_fiber_ready", "{0} has fiber now.", City.Name(site.Key)));
                }
            }
            foreach (var hood in s.Hoods.Values)
            {
                if (hood.BuildHours > 0 && --hood.BuildHours == 0)
                {
                    hood.Nodes += hood.NodesBuilding; hood.NodesBuilding = 0;
                    Event(s, Go.T("go_event_nodes_ready", "New nodes in {0} are running.", City.HoodName(hood.Id)));
                }
                if (hood.D31Hours > 0 && --hood.D31Hours == 0)
                {
                    hood.D31 = true;
                    Event(s, Go.T("go_event_31_ready", "{0} runs DOCSIS 3.1 now.", City.HoodName(hood.Id)));
                }
                if (hood.LinkHours > 0 && --hood.LinkHours == 0)
                {   // GO-HUBS-1: the hub is on the fibre - its CMTS and OLT take over the neighbourhood
                    hood.Link = true;
                    Places.Changed();
                    Event(s, Go.T("go_event_link_ready", "The feeder fibre to {0} is ready - the hub there serves the neighbourhood now.", City.HoodName(hood.Id)));
                }
                if (hood.HsHours > 0 && --hood.HsHours == 0) hood.HsHouses = true;   // GO-UP-1: the nodes are rebuilt
                if (hood.HsHouses && OpenCount(s, Cat.TtHouseAmp, hood.Id) == 0)
                {
                    hood.HsHouses = false; hood.HighSplit = true;
                    Event(s, Go.T("go_event_hs_ready", "{0} runs high split now - uploads up to {1} per node.", City.HoodName(hood.Id), Go.Bw((int)Cat.UpHigh)));
                }
                if (hood.DeepHours > 0 && --hood.DeepHours == 0)
                {   // GO-DEEP-1: small nodes without amplifiers
                    hood.Deep = true;
                    hood.Nodes = Mathf.Max(hood.Nodes, hood.DeepNodes);
                    Event(s, Go.T("go_event_deep_ready", "Fiber deep in {0}: {1} small nodes without amplifiers.", City.HoodName(hood.Id), hood.Nodes));
                }
                if (hood.RphyHours > 0 && --hood.RphyHours == 0)
                {
                    hood.Rphy = true;
                    Event(s, Go.T("go_event_rphy_ready", "{0}'s nodes run remote PHY on the vCMTS now.", City.HoodName(hood.Id)));
                }
                if (hood.RingHours > 0 && --hood.RingHours == 0)
                {
                    hood.Ring = true;
                    Event(s, Go.T("go_event_ring_ready", "The ring to {0} is closed - a cut fibre no longer takes the neighbourhood down.", City.HoodName(hood.Id)));
                }
            }
        }

        static void CheckHeadend(IspState s)
        {
            if (Test) return;
            Gear.Refresh(s);   // GAME-TYPES-1: the equipment that stands in the headend
            Crew.Sync(s);      // GAME-TYPES-1: the staff the game's recruitment agencies brought
            bool mine = City.Mine(s.HeadendKey);
            if (!mine && !s.HeadendDown)
            {
                s.HeadendDown = true;
                Event(s, Go.T("go_event_headend_lost", "The headend's building is no longer yours - the whole network is down."));
                Notify.Bad(Go.T("go_note_headend_lost", "{0}: the headend's building is no longer yours. The network is down until it moves.", s.Company), 1);
            }
            else if (mine && s.NoCmts && !s.HeadendDown)
            {   // nothing to run the network on: no CMTS in the headend
                s.HeadendDown = true;
                if (Customers(s) > 0) Notify.Bad(Go.T("go_note_nocmts", "{0}: no CMTS stands in the headend - the network is down. Buy one at an office furniture store and set it up there.", s.Company), 6);
            }
            else if (mine && !s.NoCmts && s.HeadendDown)
            {
                s.HeadendDown = false;
                Event(s, Go.T("go_event_headend_back", "The headend runs again."));
            }
        }

        /// <summary>Days in a game year (the game's setting, 60 by default).</summary>
        public static int DaysPerYear()
        {
            int perYear = 60;
            try { perYear = Mathf.Max(1, SaveGameManager.Current?.gameVariables?.daysPerYear ?? 60); } catch { }
            return perYear;
        }

        /// <summary>How long a permission contract with an owner runs: two game years (Cat.PermitDays at 60 days a year).</summary>
        public static int PermitDays() => Mathf.Max(20, Cat.PermitDays * DaysPerYear() / 60);

        /// <summary>Game years since the founding, as traffic growth (Cat.Growth).</summary>
        public static float TrafficGrowth(IspState s)
        {
            long now = Test ? s.LastHour : NowHour();
            return Cat.Growth(Mathf.Max(0f, (now - s.FoundedHour) / 24f / DaysPerYear()));
        }

        /// <summary>IPTV of so many customers in one place at this hour: one multicast stream per channel watched (at most 80)
        /// plus replay as single streams.</summary>
        static float IptvLoad(int customers, int h)
        {
            if (customers <= 0) return 0f;
            float viewers = customers * Cat.HomeDay[h] * Cat.IptvWatch;
            return Mathf.Min(viewers, Cat.IptvMulticastMax) * Cat.IptvChannelMbit + viewers * Cat.IptvReplay * Cat.IptvChannelMbit;
        }

        /// <summary>The load of every neighbourhood this hour, the slowed-down customers, and the upstream need.</summary>
        static void Load(IspState s, int h, long abs = -1)
        {
            float ev = Events.LoadFactor(s, abs);   // GO-EVENTS-1: a big match in the evening
            float up = 0f, grow = TrafficGrowth(s) * ev;
            int ingress;
            foreach (var hood in s.Hoods.Values) { hood.Util[h] = 0f; hood.FiberUtil[h] = 0f; if (hood.UpUtil == null || hood.UpUtil.Length != 24) hood.UpUtil = new float[24]; hood.UpUtil[h] = 0f; }
            var perHood = new Dictionary<string, (float l30, float l31, float lf, int coax, int fib, int ipc, int ipf, float up)>();
            foreach (var site in s.Sites.Values)
            {
                perHood.TryGetValue(site.Hood, out var v);
                foreach (var c in site.C)
                {
                    var t = TariffOf(s, c.T);
                    if (t == null || t.Mbit <= 0) { if (c.Fib) v.fib += c.N; else v.coax += c.N; continue; }
                    float l = Cat.Tavg(t.Mbit) * grow * (c.Biz ? Cat.BizDay[h] : Cat.HomeDay[h]) * c.N;
                    bool ip = t.Ip && t.Tv;
                    if (c.Fib) { v.lf += l; v.fib += c.N; if (ip) v.ipf += c.N; }
                    else
                    {
                        if (c.Dev == Cat.Dev31) v.l31 += l; else v.l30 += l; v.coax += c.N; if (ip) v.ipc += c.N;
                        // GO-UP-1: the upload - video calls, cloud backups, home office by day
                        v.up += (0.12f * Cat.Tavg(t.Mbit) + 0.004f * UpOf(t)) * grow * (c.Biz ? Cat.BizDay[h] : 0.2f + 0.8f * Cat.HomeDay[h]) * c.N;
                    }
                }
                // ex-customers not switched off yet: about half of them still surf on the old modem
                if (site.FreeRiders > 0) v.l30 += 0.5f * site.FreeRiders * Cat.Tavg(100) * grow * Cat.HomeDay[h];
                perHood[site.Hood] = v;
            }
            foreach (var kv in perHood)
            {
                if (!s.Hoods.TryGetValue(kv.Key, out var hood)) continue;
                var v = kv.Value;
                v.l30 += IptvLoad(v.ipc, h) * ev;
                v.lf += IptvLoad(v.ipf, h) * ev;
                float wsl = Deals.WsLoad(s, hood.Id, h, grow);   // GO-WHOLESALE-1: the renter's lines share the nodes
                wsl += Peers.Load(s, hood.Id, h, grow);           // GO-WHOLESALE-2: and other players' lines
                v.l30 += wsl;
                v.up += Deals.WsUp(s, hood.Id, h, grow);
                ingress = 0;
                foreach (var t in s.Tickets) if (t.Type == Cat.TtIngress && t.Hood == hood.Id) ingress++;
                float noise = hood.Nodes > 0 ? Mathf.Min(1f, ingress / (float)hood.Nodes) : 0f;
                float factor = hood.Nodes > 0 ? 1f - 0.5f * noise : 0f;
                var (d30, ofdm) = Spectrum(s, hood.D31 && Cmts31At(s, hood.Id));
                float share = PortShareFor(s, hood);   // GO-PORTS-1: nodes sharing ports share their capacity (per hub, CMTS or vCMTS)
                factor *= share * (hood.Rphy ? Cat.RphyGain : 1f);   // GO-RPHY-1: a cleaner signal carries more
                // GO-UP-1: the return path - noise hurts it most, high split widens it
                float upNode = hood.HighSplit ? Cat.UpHigh : hood.D31 ? Cat.UpLow31 : Cat.UpLow30;
                float upCap = hood.Nodes * upNode * share * (1f - 0.7f * noise) * (hood.Rphy ? Cat.RphyGain : 1f);
                float uu = v.up > 0f ? v.up / Mathf.Max(1f, upCap) : 0f;
                hood.UpUtil[h] = uu;
                hood.CongHours += v.coax * 0.5f * Clamp01((uu - 1f) * 2f);   // a full upload is felt, if less than a full download
                float cap30 = hood.Nodes * d30 * Cat.SlotMbit * factor, capOfdm = hood.Nodes * ofdm * Cat.OfdmMbit * factor;
                float util = 0f;
                if (v.l30 + v.l31 > 0f)
                {
                    float u30 = cap30 > 0f ? (v.l30 + Mathf.Max(0f, v.l31 - capOfdm)) / cap30 : 9f;
                    float u31 = v.l31 > 0f ? v.l31 / Mathf.Max(1f, capOfdm + Mathf.Max(0f, cap30 - v.l30)) : 0f;
                    util = Mathf.Max(u30, u31);
                }
                hood.Util[h] = util;
                float slow = Clamp01((util - 1f) * 2f);
                hood.CongHours += v.coax * slow;
                hood.CustHours += v.coax;
                float capF = hood.PonPorts * Cat.PonMbit;
                float uf = v.lf > 0f ? v.lf / Mathf.Max(1f, capF) : 0f;
                hood.FiberUtil[h] = uf;
                float slowF = Clamp01((uf - 1f) * 2f);
                hood.CongHours += v.fib * slowF;
                hood.CustHours += v.fib;
                up += v.l30 - wsl + v.l31 + v.lf;   // their traffic leaves through their own transit
            }
            float off = Mathf.Min(up * Cat.PeeringShare, s.PeeringPorts * Cat.PeeringMbit);
            off += Deals.CacheOff(s, up - off, h);   // GO-CACHE-1: the streaming caches serve the evening from inside
            float need = Mathf.Max(0f, up - off);
            s.UpLoad[h] = need;
            // the transit port is full beyond twice the booked amount: everybody slows down
            float commit = Cat.TransitMbit[Mathf.Clamp(s.TransitTier, 0, Cat.TransitMbit.Length - 1)];
            float slowAll = Clamp01((need / (2f * commit) - 1f) * 2f);
            if (slowAll > 0f)
                foreach (var kv in perHood)
                    if (s.Hoods.TryGetValue(kv.Key, out var hood)) hood.CongHours += (kv.Value.coax + kv.Value.fib) * slowAll;
        }

        // ── faults ───────────────────────────────────────────────────────────────────────────────────────────

        static float FaultMult => Pct("faults", 100) / 100f;

        /// <summary>Upkeep money per day lowers the coax faults by up to a half (full effect at $40 per node and day).</summary>
        static float UpkeepFactor(IspState s)
        {
            int nodes = 0;
            foreach (var h in s.Hoods.Values) nodes += h.Nodes;
            return nodes <= 0 ? 1f : 1f - 0.5f * Mathf.Min(1f, s.Upkeep / (nodes * 40f));
        }

        static bool OpenFor(IspState s, byte type, string hood, string site)
        {
            foreach (var t in s.Tickets) if (t.Type == type && t.Hood == hood && t.Site == site) return true;
            return false;
        }

        public static int OpenTickets(IspState s, byte type, string hood) => OpenCount(s, type, hood);

        static int OpenCount(IspState s, byte type, string hood)
        {
            int n = 0;
            foreach (var t in s.Tickets) if (t.Type == type && t.Hood == hood) n++;
            return n;
        }

        static void Faults(IspState s, long abs)
        {
            if (s.HeadendDown) return;
            float m = FaultMult / 24f, up = UpkeepFactor(s);
            if (Grid.Pnm(s)) up *= Cat.PnmFaults;   // GO-PNM-1: worn amplifiers, noise and house faults are caught before they fail
            int coaxAll = 0, today = (int)(abs / 24);
            foreach (var hood in s.Hoods.Values)
            {
                int coax = HoodCoaxCustomers(s, hood.Id), fib = HoodFiberCustomers(s, hood.Id);
                coaxAll += coax;
                // old cable fails more often (user 2026-11-06: the newer the connection, the less maintenance)
                float master = (HasMaster(s, hood.Id) ? Cat.MasterFaults : 1f) * (1f + Cat.AgeFaultsCoax * HoodCoaxAge(s, hood.Id, today))
                             * Events.FaultFactor(s, hood.Id, abs);   // GO-EVENTS-1: a storm over the neighbourhood
                if (hood.Nodes > 0 && coax > 0)
                {
                    int perNode = Mathf.Max(1, coax / hood.Nodes);
                    // GO-DEEP-1: no amplifiers, few actives, small service groups; GO-RPHY-1: digital optics
                    float ampK = hood.Deep ? 0f : 1f, noiseK = (hood.Deep ? 0.5f : 1f) * (hood.Rphy ? 0.7f : 1f), powerK = hood.Deep ? 0.6f : 1f;
                    if (OpenCount(s, Cat.TtIngress, hood.Id) < hood.Nodes && Rand() < noiseK * Cat.RateIngress * hood.Nodes * m * up * master)
                        Open(s, abs, Cat.TtIngress, hood.Id, "", perNode);
                    if (OpenCount(s, Cat.TtAmp, hood.Id) < hood.Nodes && Rand() < ampK * Cat.RateAmp * hood.Nodes * m * up * master)
                        Open(s, abs, Cat.TtAmp, hood.Id, "", Mathf.Max(1, perNode / 4));
                    if (OpenCount(s, Cat.TtPower, hood.Id) < hood.Nodes && Rand() < powerK * Cat.RatePower * hood.Nodes * m * up * master)
                        Open(s, abs, Cat.TtPower, hood.Id, "", perNode);
                }
                if (fib > 0 && !OpenFor(s, Cat.TtFiberCut, hood.Id, "") && Rand() < Cat.RateFiberCut * m * (1f + Cat.AgeFaultsFiber * HoodFiberAge(s, hood.Id, today)) * Events.FaultFactor(s, hood.Id, abs, true))
                    Open(s, abs, Cat.TtFiberCut, hood.Id, "", fib);
            }
            foreach (var site in s.Sites.Values)
            {
                int coax = CustomersOf(site, fiber: false), all = CustomersOf(site);
                if (all == 0) continue;
                float master = (HasMaster(s, site.Hood) ? Cat.MasterFaults : 1f) * (1f + Cat.AgeFaultsCoax * AgeYears(s, site.BuiltDay, today))
                             * Events.FaultFactor(s, site.Hood, abs);   // GO-EVENTS-1
                if (coax > 0 && !OpenFor(s, Cat.TtHouse, site.Hood, site.Key) && Rand() < Cat.RateHouse * m * up * master)
                    Open(s, abs, Cat.TtHouse, site.Hood, site.Key, coax);
                int devs = 0, onts = 0;
                foreach (var c in site.C)
                {
                    if (c.Own == Cat.OwnSelf || c.Dev == Cat.DevNone) continue;
                    if (c.Dev == Cat.DevOnt) onts += c.N; else devs += c.N;
                }
                int d = Sample(devs * Cat.RateDevice * m) + Sample(onts * Cat.RateOnt * m);
                for (int i = 0; i < d && i < 3; i++) Open(s, abs, Cat.TtDevice, site.Hood, site.Key, 1);
                // customers' own faults: an own modem dead, the TV set wrong, the phone broken, the power off, the cable in the
                // flat broken - reported like any fault, found out on the phone or at the door
                int own = Sample(all * Cat.CustomerFaults * m);
                for (int i = 0; i < own && i < 2; i++) Open(s, abs, Cat.TtDevice, site.Hood, site.Key, 1).Cause = OwnCause(s, site);
                int w = Sample(all * Cat.RateWifi * m);
                for (int i = 0; i < w && i < 3; i++) Open(s, abs, Cat.TtWifi, site.Hood, site.Key, 1);
            }
            // GO-HUBS-1: a CMTS crash takes down the coax of its group (the headend's or a hub's); an excavator may cut a hub's
            // feeder - without the ring the whole neighbourhood is off, with it nothing is (but the ring is open until repaired)
            foreach (var g in Places.Groups(s))
            {
                int coax = 0;
                foreach (var site in s.Sites.Values) if (Places.Group(s, site.Hood) == g) coax += CustomersOf(site, fiber: false);
                if (coax > 0 && !OpenFor(s, Cat.TtCmts, Test ? "" : g, "") && Rand() < Cat.RateCmts * m)
                    Open(s, abs, Cat.TtCmts, Test ? "" : g, "", coax);
            }
            if (!Test)
                foreach (var hood in s.Hoods.Values)
                {
                    if (!hood.Link || hood.Id == Places.HeadHood(s) || OpenFor(s, Cat.TtLinkCut, hood.Id, "") || Rand() >= Cat.RateLinkCut * m) continue;
                    int all = 0;
                    foreach (var site in s.Sites.Values) if (site.Hood == hood.Id) all += CustomersOf(site);
                    Open(s, abs, Cat.TtLinkCut, hood.Id, "", hood.Ring ? 0 : all);
                    if (hood.Ring) Event(s, Go.T("go_event_ring_held", "An excavator cut the feeder fibre to {0} - the ring holds, nobody is off. Repair it before a second cut.", City.HoodName(hood.Id)));
                }
        }

        static bool NetworkFault(byte type) =>
            type == Cat.TtHouse || type == Cat.TtAmp || type == Cat.TtIngress || type == Cat.TtPower || type == Cat.TtFiberCut || type == Cat.TtLinkCut;

        public static Ticket Open(IspState s, long abs, byte type, string hood, string site, int affected)
        {
            var (work, need, outage) = Cat.Fault(type);
            if (NetworkFault(type) && hood.Length > 0 && HasMaster(s, hood)) work *= Cat.MasterWork;   // the master knows the network
            var t = new Ticket { Id = s.NextId++, Type = type, Hood = hood, Site = site, Affected = affected, Opened = abs, Work = work, Need = need };
            bool pnm = (NetworkFault(type) || type == Cat.TtCmts) && Grid.Pnm(s);   // GO-PNM-1: the monitoring saw it - diagnosed already
            if (pnm) { t.Diag = true; t.Work = work * Cat.DiagFactor; }
            s.Tickets.Add(t);
            if (Cat.IsFault(type))
            {
                if (type == Cat.TtWifi) s.Today.WorkSupport += work; else { s.Today.WorkTech += work + 0.6f; s.Today.WorkSupport += Cat.DiagWork; }
            }
            if (Cat.IsFault(type))
            {
                s.Today.Faults++;
                // customers call: a share of everybody without service, the one customer of a device or Wi-Fi fault
                float calls = outage && affected > 1 ? affected * Cat.CallShare * (pnm ? Cat.PnmCalls : 1f) : 1f;   // GO-PNM-1: the customers were told
                s.Calls += calls; s.CallsToday += calls;
                s.Today.WorkService += calls * Cat.CallWork;
            }
            if (outage && affected >= 25) Events.NoteOutage(s, abs);   // GO-EVENTS-1: two within three days bring the press
            if (affected >= 25 || type == Cat.TtCmts || type == Cat.TtFiberCut || type == Cat.TtLinkCut)
            {
                string where = site.Length > 0 ? City.Name(site) : hood.Length > 0 ? City.HoodName(hood) : Go.T("go_headend", "headend");
                Notify.Bad(Go.T("go_note_fault", "{0}: {1} at {2} - {3} customers affected.", s.Company, FaultName(type), where, Go.Num(affected)), 6);
            }
            return t;
        }

        public static string FaultName(byte type)
        {
            switch (type)
            {
                case Cat.TtInstall: return Go.T("go_tt_install", "Installation");
                case Cat.TtDevice: return Go.T("go_tt_device", "Device broken");
                case Cat.TtWifi: return Go.T("go_tt_wifi", "Wi-Fi problem");
                case Cat.TtHouse: return Go.T("go_tt_house", "House connection (water, house amplifier)");
                case Cat.TtAmp: return Go.T("go_tt_amp", "Amplifier failure");
                case Cat.TtIngress: return Go.T("go_tt_ingress", "Return-path interference");
                case Cat.TtPower: return Go.T("go_tt_power", "Power cut at a node");
                case Cat.TtFiberCut: return Go.T("go_tt_fibercut", "Fiber cut (digger)");
                case Cat.TtCmts: return Go.T("go_tt_cmts", "CMTS crash");
                case Cat.TtLinkCut: return Go.T("go_tt_linkcut", "feeder fibre cut");
                case Cat.TtHouseAmp: return Go.T("go_tt_houseamp", "swap the house amplifier");
                case Cat.TtOrder: return Go.T("go_tt_order", "New orders");
                case Cat.TtPort: return Go.T("go_tt_port", "Number porting");
                case Cat.TtOn: return Go.T("go_tt_on", "Switch on");
                case Cat.TtOff: return Go.T("go_tt_off", "Switch off");
                case Cat.TtComplaint: return Go.T("go_tt_complaints", "Complaints");
                default: return "?";
            }
        }

        /// <summary>What a customer's own fault turned out to be.</summary>
        public static string CauseName(byte cause)
        {
            switch (cause)
            {
                case Cat.CauseOwnModem: return Go.T("go_cause_modem", "own modem broken");
                case Cat.CauseTv: return Go.T("go_cause_tv", "TV set wrong");
                case Cat.CausePhone: return Go.T("go_cause_phone", "phone broken");
                case Cat.CausePower: return Go.T("go_cause_power", "power off");
                case Cat.CauseCable: return Go.T("go_cause_cable", "connection cable broken");
                default: return "?";
            }
        }

        /// <summary>Why customers complain.</summary>
        public static string ComplaintName(byte why)
        {
            switch (why)
            {
                case Cat.WhyRude: return Go.T("go_why_rude", "technician unfriendly");
                case Cat.WhyFee: return Go.T("go_why_fee", "fault service charge");
                case Cat.WhyMissed: return Go.T("go_why_missed", "appointment not kept");
                case Cat.WhyHotline: return Go.T("go_why_hotline", "hotline not reachable");
                case Cat.WhySlow: return Go.T("go_why_slow", "internet slow in the evening");
                case Cat.WhyMove: return Go.T("go_why_move", "move takes too long");
                case Cat.WhyBill: return Go.T("go_why_bill", "bill not clear");
                case Cat.WhyTv: return Go.T("go_why_tv", "TV channel missing or picture faulty");
                case Cat.WhyPrice: return Go.T("go_why_price", "price too high");
                case Cat.WhyOutage: return Go.T("go_why_outage", "outage");
                default: return "?";
            }
        }

        /// <summary>A customer complains: one ticket per reason, technician and day for the customer service.</summary>
        public static void Complain(IspState s, long abs, byte why, string about, string site, Tech? tech = null)
        {
            long day = abs / 24;
            var t = s.Tickets.Find(x => x.Type == Cat.TtComplaint && x.Cause == why && x.About == about && x.Opened / 24 == day);
            if (t == null)
            {
                string hood = s.Sites.TryGetValue(site, out var st) ? st.Hood : "";
                t = Open(s, abs, Cat.TtComplaint, hood, site, 0);
                t.Cause = why; t.About = about; t.Work = 0f;
            }
            t.N++; t.Work += Cat.ComplaintWork;
            s.Today.Complaints++; s.Today.WorkService += Cat.ComplaintWork;
            if (tech != null) tech.ComplaintDays.Add((int)day);
        }

        /// <summary>How likely a charged customer complains about the fault service charge (more with a dearer one).</summary>
        static float FeeComplaint(IspState s) => 0.04f + s.ServiceFee / 2000f;

        public static bool IsOutage(byte type) => Cat.Fault(type).outage;

        // ── the staff ────────────────────────────────────────────────────────────────────────────────────────

        public static bool OnShift(Tech t, int h) => t.Shift == 0 ? h >= 6 && h < 14 : t.Shift == 1 ? h >= 14 && h < 22 : h >= 22 || h < 6;

        static Ticket? TicketById(IspState s, int id)
        {
            foreach (var t in s.Tickets) if (t.Id == id) return t;
            return null;
        }

        static bool ForTech(Ticket k, bool switchers, bool support) =>
            k.Type == Cat.TtInstall || k.Type == Cat.TtHouseAmp || (Cat.IsFault(k.Type) && (k.Type != Cat.TtWifi || !support))
            || ((k.Type == Cat.TtOn || k.Type == Cat.TtOff) && !switchers);
        static bool ForSwitch(Ticket k) => k.Type == Cat.TtOn || k.Type == Cat.TtOff;
        static bool ForService(Ticket k) => k.Type == Cat.TtOrder || k.Type == Cat.TtComplaint;

        /// <summary>Everyone on duty works this hour (with a vehicle and a workplace where the role needs them, Fleet.Assign):
        /// the technical support diagnoses and fixes remotely, the customer service takes orders and calls, switching staff
        /// connect and disconnect taps, technicians take faults and appointments (and switching jobs or Wi-Fi visits when
        /// nobody else does them). The founder answers a few orders and calls between 8 and 20 o'clock.</summary>
        static void Staff(IspState s, long abs, int h)
        {
            Deals.SlaRefresh(s);   // GO-SLA-1: which buildings have customers with a repair time
            int cars = Fleet.Assign(s, h);
            s.Today.Fleet += cars * Cat.FuelDay / 8f;
            foreach (var t in s.Techs)
            {
                if (t.Missing == 3) continue;   // off duty: the last state stands
                t.Equipped = t.Missing == 0;
            }
            bool switchers = OnDutyWith(s, Cat.RoleSwitch), support = OnDutyWith(s, Cat.RoleSupport);
            foreach (var tech in s.Techs)
            {
                var cur = tech.Ticket != 0 ? TicketById(s, tech.Ticket) : null;
                if (cur == null) { tech.Ticket = 0; tech.Travel = 0f; }
                if (tech.Missing != 0)
                {
                    if (cur != null) { cur.Techs.Remove(tech.Id); tech.Ticket = 0; tech.Travel = 0f; }   // someone else takes it
                    if (tech.Missing == 3) tech.At = "";   // the next shift starts at the headend
                }
            }
            float hours = s.Overtime ? Cat.OvertimeHours : 1f;
            foreach (var tech in s.Techs.FindAll(x => x.Missing == 0 && x.Role == Cat.RoleSupport)) Support(s, abs, tech, hours);
            foreach (var tech in s.Techs.FindAll(x => x.Missing == 0 && x.Role == Cat.RoleService)) Office(s, abs, tech.Name, 1f / Cat.SkillTime(tech.Skill), hours);
            if (h >= 8 && h < 20)
            {   // the founder lends a hand: orders and calls first, then switching jobs in the buildings
                float left = Office(s, abs, Go.T("go_you", "you"), 1f, Cat.FounderService);
                if (left > 0.001f) FounderField(s, abs, left);
            }
            var unled = Places.Unled(s);   // GO-STAFF-1: beyond what the network masters lead, people in the field work slower
            float field = hours * Events.FieldBoost(s, abs);   // GO-EVENTS-1: standby during a storm
            foreach (var tech in s.Techs.FindAll(x => x.Missing == 0 && x.Role == Cat.RoleSwitch)) Field(s, abs, tech, ForSwitch, field * (unled.Contains(tech.Id) ? Cat.Unled : 1f));
            foreach (var tech in s.Techs.FindAll(x => x.Missing == 0 && x.Role == Cat.RoleTech))
                Field(s, abs, tech, k => ForTech(k, switchers, support) && (k.Type == Cat.TtInstall || k.Due <= abs), field * (unled.Contains(tech.Id) ? Cat.Unled : 1f));
            foreach (var tech in s.Techs.FindAll(x => x.Missing == 0 && x.Role == Cat.RoleSales)) Canvass(s, tech, hours);
            Contractors(s, abs);
        }

        /// <summary>Field sales go from door to door: so many units an hour in the connected buildings with the most still to win
        /// - in a visited building more people sign that day (Join).</summary>
        static void Canvass(IspState s, Tech tech, float hours)
        {
            float units = Cat.SalesUnitsPerHour * hours / Cat.SkillTime(tech.Skill);
            Site? best = null; float bestOpen = 0f;
            foreach (var site in s.Sites.Values)
            {
                if (site.Lost || (!site.Coax && !site.Fiber)) continue;
                float open = Cat.MaxShare * (site.Homes + site.Biz) - CustomersOf(site) - site.Canvassed;
                if (open > bestOpen) { bestOpen = open; best = site; }
            }
            if (best == null) return;
            best.Canvassed += Mathf.Min(units, bestOpen);
            tech.At = best.Key;
        }

        /// <summary>Contractors (user 2026-11-06: a way out when there are too few people): with the switch on, jobs waiting for
        /// more than 8 hours go to an outside company - per job, up to a cap a day. Technicians: faults and appointments;
        /// switching: taps; customer service: orders and the calls nobody took.</summary>
        static void Contractors(IspState s, long abs)
        {
            var b = s.Today;
            for (int guard = 0; guard < 400; guard++)
            {
                bool did = false;
                foreach (var t in s.Tickets.ToArray())
                {
                    if (abs - t.Opened < Cat.SubWait || t.Techs.Count > 0 || s.PlayerTicket == t.Id) continue;
                    bool tech = s.SubTech && b.SubJobsTech < Cat.SubTechCap && ((t.Type == Cat.TtInstall && !t.Move) || t.Type == Cat.TtHouseAmp || (Cat.IsFault(t.Type) && t.Type != Cat.TtCmts));
                    bool sw = s.SubSwitch && b.SubJobsSwitch < Cat.SubSwitchCap && (t.Type == Cat.TtOn || t.Type == Cat.TtOff);
                    bool sv = s.SubService && b.SubJobsService < Cat.SubServiceCap && (t.Type == Cat.TtOrder || t.Type == Cat.TtComplaint);
                    if (!tech && !sw && !sv) continue;
                    string by = Go.T("go_contractor", "a contractor");
                    if (Cat.PerUnit(t.Type))
                    {
                        int cap = tech ? Cat.SubTechCap - b.SubJobsTech : sw ? Cat.SubSwitchCap - b.SubJobsSwitch : Cat.SubServiceCap - b.SubJobsService;
                        int n = Mathf.Min(t.N, cap);
                        if (n <= 0) continue;
                        float price = tech ? Cat.SubTechJob : sw ? Cat.SubSwitchJob : Cat.SubOrderJob;
                        if (tech) b.SubJobsTech += n; else if (sw) b.SubJobsSwitch += n; else b.SubJobsService += n;
                        b.Contractors += n * price;
                        Progress(s, t, n * Cat.Fault(t.Type).work - t.Done + 0.001f, by);
                    }
                    else
                    {
                        b.SubJobsTech++;
                        b.Contractors += Cat.SubTechJob;
                        Resolve(s, t, by);
                    }
                    did = true;
                    break;
                }
                if (!did) break;
            }
            if (s.SubService && s.Calls > 0f && b.SubJobsService < Cat.SubServiceCap)
            {
                float n = Mathf.Min(s.Calls, (Cat.SubServiceCap - b.SubJobsService) * 2f);
                s.Calls -= n; s.CallsAnswered += n;
                b.Contractors += n * Cat.SubCall;
            }
        }

        /// <summary>An hour in the field: drive (from where one is), work, take the next most urgent job.</summary>
        static void Field(IspState s, long abs, Tech tech, System.Func<Ticket, bool> fits, float hours)
        {
            float budget = hours;
            for (int guard = 0; guard < 8 && budget > 0.001f; guard++)
            {
                var t = tech.Ticket != 0 ? TicketById(s, tech.Ticket) : null;
                if (t == null)
                {
                    // GO-HUBS-1: the jobs of one's own neighbourhood group first, then anywhere
                    string g = Places.GroupOf(s, tech);
                    t = Test ? Pick(s, abs, fits) : Pick(s, abs, k => fits(k) && Places.GroupOfTicket(s, k) == g) ?? Pick(s, abs, fits);
                    if (t == null) break;
                    tech.Ticket = t.Id; t.Techs.Add(tech.Id);
                    tech.Travel = TravelHours(s, tech, t);
                }
                if (tech.Travel > 0f)
                {
                    float d = Mathf.Min(budget, tech.Travel);
                    tech.Travel -= d; budget -= d;
                    if (tech.Travel > 0f) break;
                }
                if (!t.Visited && AtTheDoor(t))
                {   // at the customer's door: sometimes nobody is home
                    t.Visited = true;
                    if (Rand() < Cat.NoShow) { NoShow(s, t, tech, abs); continue; }
                }
                float rate = 1f / Cat.SkillTime(tech.Skill);
                if (t.Type == Cat.TtFiberCut) rate *= Mathf.Min(1f, t.Techs.Count / (float)t.Need);   // a splice needs two
                float left = Cat.PerUnit(t.Type) ? Cat.Fault(t.Type).work - t.Done : t.Work - t.Done;
                float use = Mathf.Min(budget, Mathf.Max(0.001f, left) / rate);
                budget -= use;
                int before = t.N;
                tech.At = PlaceKey(s, t);
                Progress(s, t, use * rate, tech.Name);
                bool gone = !s.Tickets.Contains(t);
                if ((t.Type == Cat.TtInstall && t.N < before) || (gone && AtTheDoor(t)))
                {   // a visit at a customer's: how the technician treated them shows
                    s.Today.Visits++;
                    if (Rand() < Cat.RudeChance(tech.Manner)) Complain(s, abs, Cat.WhyRude, tech.Name, t.Site, tech);
                    t.Visited = false;   // the next customer is another door
                }
                if (t.Type == Cat.TtInstall && t.N < before && !gone)
                {   // one customer done: the most urgent work is chosen again (often the next one here, without driving)
                    t.Techs.Remove(tech.Id); tech.Ticket = 0;
                }
            }
        }

        /// <summary>What a customer's own fault is, by what the building's customers have.</summary>
        static byte OwnCause(IspState s, Site site)
        {
            bool own = false, tv = false, phone = false;
            foreach (var c in site.C)
            {
                if (c.N <= 0) continue;
                if (c.Own == Cat.OwnSelf && c.Dev != Cat.DevNone) own = true;
                var t = TariffOf(s, c.T);
                if (t != null && (t.Tv || t.Mbit <= 0)) tv = true;
                if (t != null && t.Phone) phone = true;
            }
            float wOwn = own ? 15f : 0f, wTv = tv ? 25f : 0f, wPhone = phone ? 10f : 0f, wPower = 25f, wCable = 25f;
            float r = Rand() * (wOwn + wTv + wPhone + wPower + wCable);
            if ((r -= wOwn) < 0f) return Cat.CauseOwnModem;
            if ((r -= wTv) < 0f) return Cat.CauseTv;
            if ((r -= wPhone) < 0f) return Cat.CausePhone;
            if ((r -= wPower) < 0f) return Cat.CausePower;
            return Cat.CauseCable;
        }

        /// <summary>A job at a customer's flat (installation, device, Wi-Fi visit): somebody has to be at home.</summary>
        static bool AtTheDoor(Ticket t) => t.Type == Cat.TtInstall || t.Type == Cat.TtDevice || t.Type == Cat.TtWifi;

        /// <summary>Nobody was home (user 2026-11-06: "Kunde nicht anwesend"): the trip is charged as the fault service
        /// charge, the appointment moves a day, the technician takes the next job.</summary>
        static void NoShow(IspState s, Ticket t, Tech tech, long abs)
        {
            s.Today.NoShows++;
            s.Today.ServiceFees += s.ServiceFee;
            if (Rand() < 0.5f * FeeComplaint(s)) Complain(s, abs, Cat.WhyFee, "", t.Site);
            t.Techs.Remove(tech.Id); tech.Ticket = 0; tech.Travel = 0f;
            t.Visited = false;
            if (t.Type == Cat.TtInstall && t.N > 1)
            {   // one customer of the group was not home: their appointment alone moves a day
                var k = Open(s, abs, Cat.TtInstall, t.Hood, t.Site, 0);
                k.T = t.T; k.Dev = t.Dev; k.Own = t.Own; k.Biz = t.Biz; k.Fib = t.Fib; k.Move = t.Move; k.Relocation = t.Relocation;
                k.N = 1; k.Work = Cat.Fault(Cat.TtInstall).work; k.Need = 1; k.Due = Mathf.Max((int)t.Due, (int)abs) + 24;
                t.N--; t.Work = Mathf.Max(0f, t.Work - Cat.Fault(Cat.TtInstall).work); t.Need = Mathf.Clamp(t.N, 1, 4);
            }
            else t.Due = System.Math.Max(t.Due, abs) + 24;
        }

        /// <summary>An hour at a desk: orders first, then calls. rate = work per hour, hours = the time there is.</summary>
        /// <summary>The founder lends a hand (user 2026-11-06: "am Anfang wenig Gewinn"): what is left of the founder's time
        /// after orders and calls goes into switching jobs in the buildings, without a vehicle, so a young company needs no
        /// switching staff yet.</summary>
        static void FounderField(IspState s, long abs, float hours)
        {
            float budget = hours;
            for (int guard = 0; guard < 40 && budget > 0.001f; guard++)
            {
                var t = Pick(s, abs, ForSwitch);
                if (t == null) break;
                float left = t.N * Cat.Fault(t.Type).work - t.Done;
                float use = Mathf.Min(budget, Mathf.Max(0.001f, left));
                budget -= use;
                Progress(s, t, use, Go.T("go_you", "you"));
            }
        }

        static float Office(IspState s, long abs, string by, float rate, float hours)
        {
            float budget = hours;
            for (int guard = 0; guard < 400 && budget > 0.001f; guard++)
            {
                var t = Pick(s, abs, ForService);
                if (t == null) break;
                float left = t.N * Cat.Fault(t.Type).work - t.Done;
                float use = Mathf.Min(budget, Mathf.Max(0.001f, left) / rate);
                budget -= use;
                Progress(s, t, use * rate, by);
            }
            if (budget > 0.001f && s.Calls > 0f)
            {
                float n = Mathf.Min(s.Calls, budget * rate / Cat.CallWork);
                s.Calls -= n; s.CallsAnswered += n;
                budget -= n * Cat.CallWork / rate;
            }
            return Mathf.Max(0f, budget);
        }

        /// <summary>An hour of the technical support: diagnose faults from the desk (a broken device is often fixed right away,
        /// everything else needs less work on site), then Wi-Fi problems by remote access.</summary>
        static void Support(IspState s, long abs, Tech tech, float hours)
        {
            float budget = hours, rate = 1f / Cat.SkillTime(tech.Skill);
            for (int guard = 0; guard < 40 && budget > 0.001f; guard++)
            {
                var d = Pick(s, abs, k => !k.Diag && (k.Type == Cat.TtDevice || NetworkFault(k.Type)), true);
                if (d != null)
                {
                    budget -= Cat.DiagWork / rate;
                    d.Diag = true;
                    if (d.Type == Cat.TtDevice && d.Cause == 0 && Rand() < Cat.RemoteFix + (Grid.Pnm(s) ? Cat.PnmRemote : 0f)) { Resolve(s, d, tech.Name, true); continue; }
                    if (d.Type == Cat.TtDevice && (d.Cause == Cat.CauseTv || d.Cause == Cat.CausePower) && Rand() < Cat.RemoteHelp) { Resolve(s, d, tech.Name, true); continue; }
                    d.Work = Mathf.Max(d.Done + 0.05f, d.Work * Cat.DiagFactor);
                    continue;
                }
                var w = Pick(s, abs, k => k.Type == Cat.TtWifi);
                if (w == null) break;
                float use = Mathf.Min(budget, (w.Work - w.Done) / rate);
                budget -= use;
                Progress(s, w, use * rate, tech.Name);
            }
        }

        /// <summary>The most urgent ticket that fits and can still take someone: outages by customers and age, appointments that
        /// are due soon, orders and switching jobs, then the rest. diagnosis = for the support's look (ignores who is on it).</summary>
        static Ticket? Pick(IspState s, long abs, System.Func<Ticket, bool> fits, bool diagnosis = false)
        {
            Ticket? best = null; float bestScore = 0f;
            foreach (var t in s.Tickets)
            {
                if (t.Type == Cat.TtPort || !fits(t)) continue;
                if (!diagnosis && (t.Techs.Count >= t.Need || (!Cat.PerUnit(t.Type) && t.Done >= t.Work) || (Cat.PerUnit(t.Type) && t.N <= 0))) continue;
                float age = abs - t.Opened;
                float score;
                // faults come first (20+), appointments due soon next; waiting raises the urgency only so far, so an old
                // switching job never pushes a fault aside
                switch (t.Type)
                {
                    case Cat.TtInstall: score = t.Move ? 2f + Mathf.Min(age, 96f) / 8f : (t.Due - abs < 6 ? 16f : 8f) + Mathf.Min(age, 48f) / 4f; break;
                    case Cat.TtOrder: score = 6f + Mathf.Min(age, 72f) / 4f; break;
                    case Cat.TtComplaint: score = 7f + Mathf.Min(age, 48f) / 3f; break;
                    case Cat.TtOn: score = 6f + Mathf.Min(age, 72f) / 6f; break;
                    case Cat.TtOff: score = 4f + Mathf.Min(age, 72f) / 8f; break;
                    case Cat.TtWifi: score = 3f + Mathf.Min(age, 48f) / 4f; break;
                    case Cat.TtHouseAmp: score = 3f + Mathf.Min(age, 96f) / 12f; break;   // GO-UP-1: a planned job, after the faults
                    case Cat.TtIngress: score = 14f + 2f * Mathf.Log(1f + t.Affected, 2f) + Mathf.Min(age, 48f) / 2f; break;
                    default: score = 20f + 2f * Mathf.Log(1f + t.Affected, 2f) + Mathf.Min(age, 48f) / 2f; break;
                }
                if (Cat.IsFault(t.Type) && Deals.SlaTicket(t)) score += Cat.SlaPick;   // GO-SLA-1
                if (score > bestScore || (score == bestScore && best != null && t.Id < best.Id)) { best = t; bestScore = score; }
            }
            return best;
        }

        /// <summary>Hours of driving from where the worker is (the headend at the start of a shift): 0.2 h plus 0.6 h per km,
        /// at most 2.5 h; nothing for the same place, a remote fix, or the headend's own fault seen from there.</summary>
        public static float TravelHours(IspState s, Tech tech, Ticket t)
        {
            if (t.Type == Cat.TtOrder) return 0f;
            string fromKey = tech.At.Length > 0 ? tech.At : tech.Base.Length > 0 ? tech.Base : s.HeadendKey;   // GO-HUBS-1: a shift starts at the person's office
            string toKey = PlaceKey(s, t);
            if (toKey.Length > 0 && toKey == fromKey) return 0f;
            var from = City.PosOf(fromKey);
            var to = PlaceOf(s, t);
            if (from == Vector3.zero || to == Vector3.zero) return 0.5f;
            float km = Vector3.Distance(from, to) / 1000f;
            return Mathf.Min(2.5f, 0.2f + km * 0.6f);
        }

        /// <summary>Work on a ticket: orders, switching jobs and installations finish customer by customer, anything else when all
        /// its hours are done.</summary>
        public static void Progress(IspState s, Ticket t, float hours, string by)
        {
            t.Done += hours;
            if (Cat.PerUnit(t.Type))
            {
                float unit = Cat.Fault(t.Type).work;
                while (t.Done >= unit - 0.001f && t.N > 0)
                {
                    t.Done -= unit; t.N--; t.Work = Mathf.Max(0f, t.Work - unit);
                    if (s.Sites.TryGetValue(t.Site, out var site)) UnitDone(s, site, t);
                }
                if (t.N <= 0) Resolve(s, t, by);
                else if (t.Type == Cat.TtInstall || t.Type == Cat.TtOn || t.Type == Cat.TtOff) t.Need = Mathf.Clamp(t.N, 1, 4);
                return;
            }
            if (t.Done >= t.Work - 0.001f) Resolve(s, t, by);
        }

        /// <summary>One customer of a ticket is through its step.</summary>
        static void UnitDone(IspState s, Site site, Ticket t)
        {
            long now = Test ? s.LastHour : NowHour();
            switch (t.Type)
            {
                case Cat.TtInstall:
                    if (t.Move) MoveToFiber(s, site, 1, t.Dev);
                    else if (t.Relocation) { Add(site, t.T, t.Dev, t.Own, t.Biz, t.Fib, 1); s.Today.InstalledCust++; }   // moved in: no new customer
                    else { Add(site, t.T, t.Dev, t.Own, t.Biz, t.Fib, 1); s.Today.Joined++; s.Today.InstalledCust++; s.Today.Fees += s.Fee; }
                    break;
                case Cat.TtOff:
                    if (site.FreeRiders > 0) site.FreeRiders--;
                    break;
                case Cat.TtComplaint:
                    s.Today.ComplaintsDone++;
                    break;
                case Cat.TtOn:
                    AfterSwitchOn(s, site, t, 1, now);
                    break;
                case Cat.TtOrder:
                    AfterOrder(s, site, t, now);
                    break;
            }
        }

        // the next step of a customer group: same tariff, device, ownership, business, fiber
        static Ticket Merge(IspState s, byte type, Site site, Ticket from, int n, long abs, long due)
        {
            var t = s.Tickets.Find(x => x.Type == type && !x.Move && !x.NoNumber && x.Site == site.Key && x.T == from.T && x.Dev == from.Dev
                                        && x.Own == from.Own && x.Biz == from.Biz && x.Fib == from.Fib && x.Done <= 0f && (type != Cat.TtPort || x.Due == due));
            if (t == null)
            {
                t = Open(s, abs, type, site.Hood, site.Key, 0);
                t.T = from.T; t.Dev = from.Dev; t.Own = from.Own; t.Biz = from.Biz; t.Fib = from.Fib; t.Due = due; t.Work = 0f;
            }
            t.N += n;
            if (Cat.PerUnit(type)) t.Work += n * Cat.Fault(type).work;
            if (type == Cat.TtInstall || type == Cat.TtOn || type == Cat.TtOff) t.Need = Mathf.Clamp(t.N, 1, 4);
            CountWork(s, type, n);
            return t;
        }

        /// <summary>Hours of work a role gets with n more customers on a ticket (driving included as a rough share).</summary>
        static void CountWork(IspState s, byte type, int n)
        {
            switch (type)
            {
                case Cat.TtInstall: s.Today.WorkTech += n * (Cat.Fault(type).work + 0.3f); break;
                case Cat.TtOn: case Cat.TtOff: s.Today.WorkSwitch += n * (Cat.Fault(type).work + 0.1f); break;
                case Cat.TtOrder: s.Today.WorkService += n * Cat.Fault(type).work; break;
            }
        }

        /// <summary>An order is taken: a phone customer brings the old number (porting, a fee to the old provider, two days) or
        /// gets a new one from the provider's blocks (waits when there is none); then switching on or the fiber appointment.</summary>
        static void AfterOrder(IspState s, Site site, Ticket order, long now)
        {
            var tr = TariffOf(s, order.T);
            if (tr != null && tr.Phone && tr.Mbit > 0)
            {
                if (order.Ported > 0)
                {
                    order.Ported--;
                    s.Today.Phone += Cat.PortFee;
                    Merge(s, Cat.TtPort, site, order, 1, now, (now / 24 + Cat.PortDays) * 24 + 8);   // the switch-over: 8 o'clock, two days on
                    return;
                }
                if (FreeNumbers(s) <= 0)
                {
                    var wait = s.Tickets.Find(x => x.Type == Cat.TtPort && x.NoNumber && x.Site == site.Key && x.T == order.T && x.Dev == order.Dev && x.Own == order.Own && x.Biz == order.Biz && x.Fib == order.Fib);
                    if (wait == null)
                    {
                        wait = Open(s, now, Cat.TtPort, site.Hood, site.Key, 0);
                        wait.T = order.T; wait.Dev = order.Dev; wait.Own = order.Own; wait.Biz = order.Biz; wait.Fib = order.Fib; wait.NoNumber = true; wait.Work = 0f;
                        if (!Has(s, "nonumbers" + Day()))
                        {
                            s.Flags.Add("nonumbers" + Day());
                            Notify.Warn(Go.T("go_note_nonumbers", "{0}: no free phone numbers - buy a number block in the Network tab.", s.Company), 6);
                        }
                    }
                    wait.N++;
                    return;
                }
                s.NumbersUsed++;
            }
            Next(s, site, order, 1, now);
        }

        /// <summary>After the order (and the porting): coax customers need their tap switched on, fiber customers an appointment.</summary>
        static void Next(IspState s, Site site, Ticket from, int n, long now)
        {
            if (n <= 0) return;
            if (!from.Fib) Merge(s, Cat.TtOn, site, from, n, now, 0);
            else Merge(s, Cat.TtInstall, site, from, n, now, now + 24);
        }

        /// <summary>The tap is on: most coax customers install by post (the device is shipped), the rest get an appointment.</summary>
        static void AfterSwitchOn(IspState s, Site site, Ticket from, int n, long now)
        {
            if (from.Relocation)
            {   // a customer who moved: switched on at the new address, the modem came along
                Add(site, from.T, from.Dev, from.Own, from.Biz, from.Fib, n);
                return;
            }
            var tr = TariffOf(s, from.T);
            bool tvOnly = tr != null && tr.Mbit <= 0;
            int self = tvOnly ? n : Round(n * Cat.SelfInstallShare);
            if (self > 0)
            {
                Add(site, from.T, from.Dev, from.Own, from.Biz, from.Fib, self);
                // GO-CENTERS-1: where a customer center stands, people pick their device up there - no parcel to send
                bool pickup = !Test && Places.HasCenter(s, site.Hood);
                if (!tvOnly && !pickup) s.Today.Ship += self * Cat.Ship;
                s.Today.Joined += self;
                s.Today.Fees += self * s.Fee;
            }
            if (n - self > 0) Merge(s, Cat.TtInstall, site, from, n - self, now, now + 24);
        }

        /// <summary>Porting: on the switch-over day the customers go on; waiting for numbers ends when there are free ones.</summary>
        static void Ports(IspState s, long abs)
        {
            for (int i = s.Tickets.Count - 1; i >= 0; i--)
            {
                var t = s.Tickets[i];
                if (t.Type != Cat.TtPort || !s.Sites.TryGetValue(t.Site, out var site)) continue;
                int go;
                if (t.NoNumber) { go = Mathf.Min(t.N, FreeNumbers(s)); s.NumbersUsed += go; }
                else go = abs >= t.Due ? t.N : 0;
                if (go <= 0) continue;
                t.N -= go;
                Next(s, site, t, go, abs);
                if (t.N <= 0) s.Tickets.Remove(t);
            }
        }

        /// <summary>A ticket is done: everyone on it goes back to work; a broken device is swapped.</summary>
        public static void Resolve(IspState s, Ticket t, string by, bool remote = false)
        {
            s.Tickets.Remove(t);
            foreach (var tech in s.Techs) if (tech.Ticket == t.Id) { tech.Ticket = 0; tech.Travel = 0f; }
            if (s.PlayerTicket == t.Id) s.PlayerTicket = 0;
            if (Cat.PerUnit(t.Type) || t.Type == Cat.TtPort) return;   // their customers went on one by one
            if (t.Type != Cat.TtHouseAmp) s.Today.Fixed++;   // a planned job, no fault
            s.Today.Repair += Cat.RepairMaterial(t.Type);   // the old coax is patched for little, a fiber cut is dear
            if (t.Type == Cat.TtDevice && t.Cause > 0)
            {   // the customer's own fault (user 2026-11-06: "Entstörungspauschale wegen Kundenfehlverhalten")
                s.Today.OwnFault++;
                if (!remote)
                {
                    s.Today.ServiceFees += s.ServiceFee;
                    if (Rand() < FeeComplaint(s)) Complain(s, s.LastHour, Cat.WhyFee, "", t.Site);
                }
                if (t.Cause == Cat.CauseOwnModem && Rand() < 0.5f && s.Sites.TryGetValue(t.Site, out var os))
                {   // their own modem is dead: half of them buy one from the technician
                    byte dev = os.C.Exists(x => x.Fib) ? Cat.DevOnt : s.Cmts31 ? Cat.Dev31 : Cat.Dev30;
                    if (TakeStock(s, dev, 1)) s.Today.Sold += BuyPrice(s, dev);
                }
            }
            else if (t.Type == Cat.TtDevice && s.Sites.TryGetValue(t.Site, out var ds))
            {
                // the swap: a rented device comes from the stock (bought at once if there is none), a bought one is
                // bought again by most customers
                var c = ds.C.Find(x => x.Own != Cat.OwnSelf && x.Dev != Cat.DevNone && x.N > 0);
                if (c != null)
                {
                    if (c.Own == Cat.OwnRent)
                    {
                        if (!TakeStock(s, c.Dev, 1)) s.Today.Ship += DeviceCost(c.Dev) * 1.5f;   // bought in a hurry
                    }
                    else if (Rand() < 0.7f) s.Today.Sold += BuyPrice(s, c.Dev);
                }
            }
            if (by.Length > 0 && (t.Affected >= 25 || t.Type == Cat.TtCmts || t.Type == Cat.TtFiberCut))
                Event(s, Go.T("go_event_fixed", "{0} fixed by {1}.", FaultName(t.Type), by));
        }

        /// <summary>The player's own work on site: each game minute near the place is a minute of work (as a worker with
        /// three stars).</summary>
        public static void PlayerWork(IspState s, float gameHours)
        {
            if (s.PlayerTicket == 0 || gameHours <= 0f) return;
            var t = TicketById(s, s.PlayerTicket);
            if (t == null) { s.PlayerTicket = 0; return; }
            if (!PlayerNear(s, t)) return;
            Progress(s, t, gameHours / Cat.SkillTime(3), Go.T("go_you", "you"));
            if (!s.Tickets.Contains(t))
                Notify.Good(Go.T("go_note_player_fixed", "{0}: you fixed it - {1}.", s.Company, FaultName(t.Type)), 5);
        }

        public static Vector3 PlaceOf(IspState s, Ticket t) =>
            t.Type == Cat.TtCmts ? City.PosOf(CmtsPlace(s, t)) : t.Site.Length > 0 ? City.PosOf(t.Site) : City.HoodCenter(s, t.Hood);

        public static string PlaceKey(IspState s, Ticket t) =>
            t.Type == Cat.TtCmts ? CmtsPlace(s, t) : t.Site.Length > 0 ? t.Site : City.HoodSite(s, t.Hood);

        /// <summary>GO-HUBS-1: the office whose CMTS crashed (the headend for one of before 0.8).</summary>
        static string CmtsPlace(IspState s, Ticket t)
        {
            if (Test || t.Hood.Length == 0 || t.Hood == Places.HeadHood(s)) return s.HeadendKey;
            string hub = Places.Hub(s, t.Hood);
            return hub.Length > 0 ? hub : s.HeadendKey;
        }

        public static bool PlayerNear(IspState s, Ticket t)
        {
            try
            {
                var p = Helpers.PlayerHelper.GetPosition();
                var at = PlaceOf(s, t);
                if (at == Vector3.zero) return false;
                float limit = t.Site.Length > 0 || t.Type == Cat.TtCmts ? 45f : 160f;   // a node: anywhere near the middle
                var d = p - at; d.y = 0f;
                return d.magnitude <= limit;
            }
            catch { return false; }
        }

        static void Outages(IspState s, long abs)
        {
            foreach (var site in s.Sites.Values) site.CustHours += CustomersOf(site);
            if (s.HeadendDown)
            {
                foreach (var site in s.Sites.Values) site.OutHours += CustomersOf(site);
                return;
            }
            if (s.Cuts.Count > 0)   // GO-POWER-1: nodes or offices without power
                foreach (var site in s.Sites.Values)
                {
                    float share = Grid.OutShare(s, site, abs);
                    if (share > 0f) site.OutHours += CustomersOf(site) * share;
                }
            foreach (var t in s.Tickets)
            {
                if (!IsOutage(t.Type) || t.Affected <= 0 || t.Cause > 0) continue;   // a customer's own fault is not the network's
                if (t.Site.Length > 0)
                {
                    if (s.Sites.TryGetValue(t.Site, out var site)) site.OutHours += t.Affected;
                    continue;
                }
                // a node, the fiber of a neighbourhood, a hub's feeder or a CMTS: spread over the buildings it serves
                bool? fiber = t.Type == Cat.TtFiberCut ? true : t.Type == Cat.TtLinkCut ? (bool?)null : false;
                bool Hits(Site site) => t.Hood.Length == 0 || (t.Type == Cat.TtCmts ? Places.Group(s, site.Hood) == t.Hood : site.Hood == t.Hood);
                int total = 0;
                foreach (var site in s.Sites.Values)
                    if (Hits(site)) total += CustomersOf(site, fiber: fiber);
                if (total <= 0) continue;
                foreach (var site in s.Sites.Values)
                    if (Hits(site))
                        site.OutHours += t.Affected * CustomersOf(site, fiber: fiber) / (float)total;
            }
        }

        /// <summary>A missed appointment (§ 58 TKG): $10 or 20 % of the monthly price per customer, and 30 % of them call it off.
        /// A planned move to fiber waits without compensation (the customers keep their coax).</summary>
        static void Appointments(IspState s, long abs)
        {
            for (int i = s.Tickets.Count - 1; i >= 0; i--)
            {
                var t = s.Tickets[i];
                if (t.Type != Cat.TtInstall || t.Move || abs < t.Due || t.N <= 0) continue;
                var tr = TariffOf(s, t.T);
                float price = tr?.Price ?? 1.5f;
                s.Today.Comp += t.N * Cat.MissedAppointment(price);
                s.Today.MissedCust += t.N;
                for (int c = Sample(t.N * 0.25f); c > 0; c--) Complain(s, abs, Cat.WhyMissed, "", t.Site);
                int off = Round(t.N * 0.3f);
                if (off > 0) Drop(s, t, off);
                t.Due = abs + 24;
                t.Work = t.N * Cat.Fault(Cat.TtInstall).work;
                t.Need = Mathf.Clamp(t.N, 1, 4);
                if (!Has(s, "missed" + Day()))
                {
                    s.Flags.Add("missed" + Day());
                    Notify.Bad(Go.T("go_note_missed", "{0}: installation appointments were missed - customers get compensation, some cancel.", s.Company), 6);
                }
                if (t.N <= 0) s.Tickets.RemoveAt(i);
            }
        }

        /// <summary>Customers of a ticket on their way in give up: their devices come back, their numbers too.</summary>
        static void Drop(IspState s, Ticket t, int n)
        {
            n = Mathf.Min(n, t.N);
            if (n <= 0) return;
            GiveBack(s, t.Dev, t.Own, n);
            var tr = TariffOf(s, t.T);
            if (tr != null && tr.Ip && tr.Tv) PutStb(s, n);
            if (t.Type == Cat.TtOrder && t.Ported > 0) t.Ported = Mathf.Max(0, t.Ported - n);
            t.N -= n;
            if (Cat.PerUnit(t.Type)) t.Work = Mathf.Max(0f, t.Work - n * Cat.Fault(t.Type).work);
            s.Today.Left += n;
        }

        // ── devices ──────────────────────────────────────────────────────────────────────────────────────────

        public static float DeviceCost(byte dev) => dev == Cat.Dev31 ? Cat.BuyM31 : dev == Cat.DevOnt ? Cat.BuyOnt : Cat.BuyM30;
        public static float BuyPrice(IspState s, byte dev) => dev == Cat.Dev31 ? s.Prices.Buy31 : dev == Cat.DevOnt ? s.Prices.BuyOnt : s.Prices.Buy30;
        public static float RentPrice(IspState s, byte dev) => dev == Cat.Dev31 ? s.Prices.Rent31 : dev == Cat.DevOnt ? s.Prices.RentOnt : s.Prices.Rent30;

        /// <summary>Devices at hand: the refurbished ones given back (IspState.Stock) and, in the real game, the new ones in the
        /// headend's storage (GAME-TYPES-1: goods from a wholesaler or an importer).</summary>
        public static int StockOf(IspState s, byte dev) => Spare(s, dev) + Gear.InStorage(s, Gear.DeviceItem(dev));
        static int Spare(IspState s, byte dev) => dev == Cat.Dev31 ? s.Stock.M31 : dev == Cat.DevOnt ? s.Stock.Ont : s.Stock.M30;

        public static bool TakeStock(IspState s, byte dev, int n)
        {
            int spare = Mathf.Min(Spare(s, dev), n);
            if (spare < n && !Gear.TakeFromStorage(s, Gear.DeviceItem(dev), n - spare)) return false;
            if (dev == Cat.Dev31) s.Stock.M31 -= spare; else if (dev == Cat.DevOnt) s.Stock.Ont -= spare; else s.Stock.M30 -= spare;
            return true;
        }

        public static int StbStock(IspState s) => s.Stock.Stb + Gear.InStorage(s, GameTypes.IStb);
        public static int CardStock(IspState s) => s.Stock.Cards + Gear.InStorage(s, GameTypes.ICard);

        static bool TakeStb(IspState s, int n)
        {
            int spare = Mathf.Min(s.Stock.Stb, n);
            if (spare < n && !Gear.TakeFromStorage(s, GameTypes.IStb, n - spare)) return false;
            s.Stock.Stb -= spare;
            return true;
        }

        static bool TakeCards(IspState s, int n)
        {
            int spare = Mathf.Min(s.Stock.Cards, n);
            if (spare < n && !Gear.TakeFromStorage(s, GameTypes.ICard, n - spare)) return false;
            s.Stock.Cards -= spare;
            return true;
        }

        public static void PutStock(IspState s, byte dev, int n)
        {
            if (n <= 0) return;
            if (dev == Cat.Dev31) s.Stock.M31 += n; else if (dev == Cat.DevOnt) s.Stock.Ont += n; else if (dev == Cat.Dev30) s.Stock.M30 += n;
        }

        /// <summary>Set-top boxes come back (one in ten is broken).</summary>
        static void PutStb(IspState s, int n) { if (n > 0) s.Stock.Stb += n - Round(n * 0.1f); }

        /// <summary>Devices of customers who leave: rented ones come back to the stock (one in ten is broken).</summary>
        static void GiveBack(IspState s, byte dev, byte own, int n)
        {
            if (own == Cat.OwnRent && dev != Cat.DevNone) PutStock(s, dev, n - Round(n * 0.1f));
        }

        public static void Add(Site site, int tariff, byte dev, byte own, bool biz, bool fib, int n)
        {
            if (n <= 0) return;
            var c = site.C.Find(x => x.T == tariff && x.Dev == dev && x.Own == own && x.Biz == biz && x.Fib == fib);
            if (c == null) site.C.Add(c = new Cohort { T = tariff, Dev = dev, Own = own, Biz = biz, Fib = fib });
            c.N += n;
        }

        static void MoveToFiber(IspState s, Site site, int count, byte reserved)
        {
            int left = count, moved = 0;
            foreach (var c in site.C.ToArray())
            {
                if (left <= 0) break;
                if (c.Fib || c.N <= 0) continue;
                int n = Mathf.Min(left, c.N);
                var from = TariffOf(s, c.T);
                var to = FiberTariffFor(s, from);
                if (to == null) continue;
                c.N -= n; left -= n; moved += n;
                GiveBack(s, c.Dev, c.Own, n);
                bool tvOnly = to.Mbit <= 0;
                Add(site, to.Id, tvOnly ? Cat.DevNone : Cat.DevOnt, tvOnly ? Cat.OwnSelf : c.Own == Cat.OwnBought ? Cat.OwnRent : c.Own, c.Biz, true, n);
            }
            site.C.RemoveAll(x => x.N <= 0);
            if (left > 0 && reserved == Cat.DevOnt) PutStock(s, Cat.DevOnt, left);   // the ONT reserved for a customer who had gone
            if (moved > 0 && CustomersOf(site, fiber: false) == 0)
                Event(s, Go.T("go_event_moved", "All customers in {0} are on fiber now.", City.Name(site.Key)));
        }

        /// <summary>The fiber tariff a coax customer moves to: at least as fast, with TV and phone if the old one had them, the
        /// cheapest of those.</summary>
        public static Tariff? FiberTariffFor(IspState s, Tariff? from)
        {
            Tariff? best = null;
            foreach (var t in s.Tariffs)
            {
                if (!t.Fiber || !t.Active) continue;
                if (from != null && from.Mbit <= 0) { if (t.Mbit > 0) continue; }
                else if (t.Mbit < (from?.Mbit ?? 0) || (from != null && from.Tv && !t.Tv) || (from != null && from.Phone && !t.Phone)) continue;
                if (best == null || t.Price < best.Price) best = t;
            }
            if (best == null && from != null && from.Mbit > 0)
                foreach (var t in s.Tariffs)
                    if (t.Fiber && t.Active && t.Mbit > 0 && (best == null || t.Mbit > best.Mbit)) best = t;
            return best;
        }

        // ── the day ──────────────────────────────────────────────────────────────────────────────────────────

        internal static void EndOfDay(IspState s, int day)
        {
            var b = s.Today;
            b.Day = day;
            long dayEnd = (long)(day + 1) * 24;

            // income
            foreach (var site in s.Sites.Values)
                foreach (var c in site.C)
                {
                    var t = TariffOf(s, c.T);
                    if (t == null) continue;
                    b.Subs += t.Price * c.N;
                    if (c.Own == Cat.OwnRent && c.Dev != Cat.DevNone) b.Rent += RentPrice(s, c.Dev) * c.N;
                    if (t.Ip && t.Tv && t.Mbit > 0) b.Rent += s.Prices.RentStb * c.N;   // the set-top box
                }
            foreach (var p in s.PayTv) b.PayTvIn += p.Price * p.Subs;

            // running costs
            int tier = Mathf.Clamp(s.TransitTier, 0, Cat.TransitMbit.Length - 1);
            b.Transit = Cat.TransitDay(tier) + s.PeeringPorts * Cat.PeeringDay;
            var sorted = (float[])s.UpLoad.Clone();
            System.Array.Sort(sorted);
            float p95 = sorted[sorted.Length - 2];   // the busiest hour does not count (95th percentile of the day)
            if (p95 > Cat.TransitMbit[tier]) b.Transit += (p95 - Cat.TransitMbit[tier]) * Cat.TransitRate[tier] * Cat.Overage / 30f;
            foreach (var p in s.PayTv)
            {
                var o = Cat.Offer(p.Id);
                if (o != null) b.PayTvOut += Mathf.Max(o.Minimum, p.Subs * o.Cost);
            }
            foreach (var tech in s.Techs)
                if (tech.Emp.Length == 0)   // the game pays its own employees (GAME-TYPES-1)
                    b.Wages += 8f * tech.Wage * (tech.Shift == 2 && tech.Role != Cat.RoleMaster ? Cat.NightBonus : 1f) * (s.Overtime ? Cat.OvertimeWage : 1f);
            b.Upkeep += s.Upkeep + s.Sales;
            b.Phone += PhoneCustomers(s) * Cat.Interconnect * (1f - Cat.Volume(Customers(s)));
            b.Fleet += (s.Desks + s.CenterDesks) * Cat.DeskDay;
            b.Lease += s.LeaseVans * Cat.VanLease + s.LeaseCars * Cat.CarLease;
            b.Power += Gear.Power(s) + Grid.BatteryDay(s);   // GAME-TYPES-1: the equipment in the offices; GO-POWER-1: the nodes' batteries
            if (s.Pnm) b.Monitor += Grid.PnmDay(s);   // GO-PNM-1: the licence
            int today = day + 1;
            foreach (var site in s.Sites.Values)
            {   // maintenance by the age of each connection: almost nothing when new, more with every game year
                int units = site.Homes + site.Biz;
                if (site.Coax && !site.CoaxOff) b.Maint += units * (Cat.MaintCoax + Cat.MaintCoaxYear * AgeYears(s, site.BuiltDay, today));
                if (site.Fiber) b.Maint += units * (Cat.MaintFiber + Cat.MaintFiberYear * AgeYears(s, site.FiberDay, today));
            }
            float volume = 1f - Cat.Volume(Customers(s));   // size pays: cheaper wholesale and interconnection
            b.PayTvOut *= volume;

            // compensation for outages older than two days (§ 58 TKG)
            float avg = AvgPrice(s);
            foreach (var t in s.Tickets)
            {
                if (!IsOutage(t.Type) || t.Affected <= 0 || t.Cause > 0) continue;   // a customer's own fault is not the network's
                int k = (int)((dayEnd - t.Opened) / 24) + 1;
                if (k >= 3 && t.CompDays < k)
                {
                    b.Comp += t.Affected * Cat.Comp(avg, k);
                    t.CompDays = k;
                }
            }
            if (s.HeadendDown) b.Comp += Customers(s) * Cat.Comp(avg, 3) * 0.5f;
            Deals.Day(s, b, day + 1);   // GO-WHOLESALE-1, GO-SLA-1, GO-BULK-1
            Peers.Day(s, b, day + 1);   // GO-WHOLESALE-2: lines on other players' networks

            // the levy: the game's tax rate on a day with a profit
            int pct = 10;
            try { pct = SaveGameManager.Current?.gameVariables?.taxPercentage ?? 10; } catch { }
            b.Tax = Mathf.Max(0f, b.Operating) * pct / 100f;

            Money.Day(s, b);

            // yesterday's service quality drives today's customers
            float custAll = 0f, outAll = 0f, congAll = 0f, hoodCust = 0f;
            foreach (var site in s.Sites.Values)
            {
                site.OutYesterday = site.CustHours > 0f ? Clamp01(site.OutHours / site.CustHours) : 0f;
                custAll += site.CustHours; outAll += site.OutHours;
                site.OutHours = 0f; site.CustHours = 0f;
            }
            foreach (var hood in s.Hoods.Values)
            {
                hood.CongYesterday = hood.CustHours > 0f ? Clamp01(hood.CongHours / hood.CustHours) : 0f;
                congAll += hood.CongHours; hoodCust += hood.CustHours;
                hood.CongHours = 0f; hood.CustHours = 0f;
            }
            float outShare = custAll > 0f ? outAll / custAll : 0f, congShare = hoodCust > 0f ? congAll / hoodCust : 0f;
            // complaints: calls nobody answered, slow evenings, moves that take too long
            for (int c = Sample(Mathf.Max(0f, s.CallsToday - s.CallsAnswered) * 0.03f); c > 0; c--) Complain(s, dayEnd - 1, Cat.WhyHotline, "", "");
            for (int c = Sample(Customers(s) * congShare * 0.002f); c > 0; c--) Complain(s, dayEnd - 1, Cat.WhySlow, "", "");
            // GO-FAULTS-2 (user 2026-11-08: "zu wenig Störungen, zu wenig Beschwerden"): customers complain without a mistake of
            // the provider too - a bill they do not understand, a missing TV channel, a price above the market - and after an
            // outage
            int custNow = Customers(s);
            for (int c = Sample(custNow * 0.0012f); c > 0; c--) Complain(s, dayEnd - 1, Cat.WhyBill, "", "");
            for (int c = Sample(TvCustomers(s) * 0.0008f); c > 0; c--) Complain(s, dayEnd - 1, Cat.WhyTv, "", "");
            for (int c = Sample(custNow * outShare * 0.06f); c > 0; c--) Complain(s, dayEnd - 1, Cat.WhyOutage, "", "");
            int dear = 0;
            foreach (var tar in s.Tariffs)
            {
                if (!tar.Active) continue;
                float worth = Worth(s, tar, false, Cat.KindHome);
                if (worth > 0f && tar.Price > worth * 1.2f) dear += Act.Users(s, tar);
            }
            for (int c = Sample(dear * 0.002f); c > 0; c--) Complain(s, dayEnd - 1, Cat.WhyPrice, "", "");
            foreach (var t in s.Tickets.ToArray())
                if (t.Relocation && !t.Late && (t.Type == Cat.TtOn || t.Type == Cat.TtInstall) && dayEnd - t.Opened > 72) { t.Late = true; Complain(s, dayEnd - 1, Cat.WhyMove, "", t.Site); }
            foreach (var tech in s.Techs) tech.ComplaintDays.RemoveAll(x => x < day - 30);
            Satisfaction(s, b, congShare, outShare, dayEnd);
            s.Reputation = Mathf.Clamp(s.Reputation + (s.Satisfaction - s.Reputation) * 0.2f, 0f, 100f);
            b.CallsIn = s.CallsToday; b.CallsDone = s.CallsAnswered;
            s.Calls = 0f; s.CallsToday = 0f; s.CallsAnswered = 0f;   // who did not get through has given up

            Places.DayNote(s, day + 1);   // GO-HUBS-1: the grace days of an old provider, then what the neighbourhoods lack
            Permits(s, day + 1, dayEnd);
            Rival.Day(s, day + 1);
            Churn(s, dayEnd);
            Moves(s, dayEnd);
            Impatience(s);
            Join(s, dayEnd);
            foreach (var site in s.Sites.Values) site.Canvassed = 0f;
            Waiting(s, dayEnd);
            PayTvDay(s);
            Candidates(s, day);
            Unlocks(s);

            b.Customers = Customers(s);
            s.Books.Insert(0, b);
            if (s.Books.Count > 30) s.Books.RemoveRange(30, s.Books.Count - 30);
            s.Today = new DayBook { Day = day + 1 };
            for (int i = 0; i < 24; i++) s.UpLoad[i] = 0f;
            s.Flags.RemoveAll(f => (f.StartsWith("missed") || f.StartsWith("nostock") || f.StartsWith("nocards") || f.StartsWith("nonumbers"))
                                   && !f.EndsWith((day + 1).ToString()));
        }

        /// <summary>Customer satisfaction (0..100) from five parts of the day (user 2026-11-06: too few technicians, switching
        /// staff or customer service make customers unhappy): the network (slow evenings, outages), how long faults stay open,
        /// appointments kept, calls answered, how long orders and switching wait. The reputation follows it day by day.</summary>
        static void Satisfaction(IspState s, DayBook b, float congShare, float outShare, long now)
        {
            s.SatNet = Clamp01(1f - 1.5f * congShare - 2f * outShare);
            float age = 0f, weight = 0f, wait = 0f, waiting = 0f;
            foreach (var t in s.Tickets)
            {
                float a = now - t.Opened;
                if (Cat.IsFault(t.Type)) { float w = Mathf.Max(1, t.Affected); age += a * w; weight += w; }
                else if (t.Type == Cat.TtOrder || t.Type == Cat.TtOn) { wait += a * t.N; waiting += t.N; }
            }
            s.SatFaults = weight > 0f ? Clamp01(1f - Mathf.Max(0f, age / weight - 12f) / 48f) : 1f;
            s.SatAppoint = b.InstalledCust + b.MissedCust > 0 ? Clamp01(1f - b.MissedCust / (float)(b.InstalledCust + b.MissedCust)) : 1f;
            s.SatCalls = s.CallsToday > 10f ? Clamp01(s.CallsAnswered / s.CallsToday) : 1f;
            s.SatOrders = waiting > 0f ? Clamp01(1f - Mathf.Max(0f, wait / waiting - 24f) / 72f) : 1f;
            // complaints: half a complaint per 1,000 customers a day is normal; more, or complaints a day old, cost
            int cust = Customers(s), open = 0;
            foreach (var t in s.Tickets) if (t.Type == Cat.TtComplaint && now - t.Opened > 24) open += t.N;
            float perThousand = 1000f * b.Complaints / Mathf.Max(1, cust);
            s.SatComplaints = Clamp01(1f - Mathf.Max(0f, perThousand - 0.5f) * 0.25f - open / Mathf.Max(5f, cust * 0.002f) * 0.5f);
            s.Satisfaction = 100f * (0.27f * s.SatNet + 0.22f * s.SatFaults + 0.14f * s.SatAppoint + 0.14f * s.SatCalls + 0.13f * s.SatOrders + 0.10f * s.SatComplaints);
        }

        /// <summary>Permission contracts (user 2026-11-06: they run out, then they are made new - or another provider takes the
        /// building): a note ten days before; renewed by itself (when switched on) in the last five days; at the end a tender
        /// the provider wins the better its reputation (field sales help). Lost: the building's customers go to the other
        /// provider, and it is closed to the provider for a contract term.</summary>
        static void Permits(IspState s, int today, long abs)
        {
            bool sales = s.Techs.Exists(t => t.Role == Cat.RoleSales);
            foreach (var site in s.Sites.Values)
            {
                if (site.Lost)
                {
                    if (today >= site.LostUntil) { site.Lost = false; site.PermitUntil = -1; }
                    continue;
                }
                if (site.PermitUntil < 0) { site.PermitUntil = today + PermitDays(); continue; }
                int left = site.PermitUntil - today;
                if (left == Cat.RenewNoticeDays)
                    Event(s, Go.T("go_event_permit_soon", "The permission contract for {0} ends on day {1}.", City.Name(site.Key), site.PermitUntil));
                if (left <= 5 && left >= 0 && s.AutoRenew && Act.Renew(s, site.Key)) continue;
                if (left > 0) continue;
                // the tender
                float win = Rival.TenderChance(s, sales);
                if (Rand() < win && Act.Renew(s, site.Key, true))
                {
                    Event(s, Go.T("go_event_tender_won", "{0}: the owner renewed the contract after a tender.", City.Name(site.Key)));
                    continue;
                }
                Lose(s, site, today);
            }
        }

        static void Lose(IspState s, Site site, int today)
        {
            int n = CustomersOf(site);
            foreach (var c in site.C)
            {
                GiveBack(s, c.Dev, c.Own, c.N);
                var t = TariffOf(s, c.T);
                if (t != null && t.Ip && t.Tv) PutStb(s, c.N);
            }
            site.C.Clear();
            site.FreeRiders = 0; site.Waiting = 0;
            site.Coax = false; site.Fiber = false; site.CoaxHours = -1; site.FiberHours = -1;
            s.Tickets.RemoveAll(t => t.Site == site.Key);
            foreach (var tech in s.Techs) if (tech.Ticket != 0 && !s.Tickets.Exists(t => t.Id == tech.Ticket)) { tech.Ticket = 0; tech.Travel = 0f; }
            s.Today.Left += n;
            site.Lost = true; site.LostUntil = today + PermitDays(); site.PermitUntil = -1;
            Event(s, Go.T("go_event_permit_lost", "{0} went to {2} - {1} customers are gone.", City.Name(site.Key), Go.Num(n), Rival.Name));
            Notify.Bad(Go.T("go_note_permit_lost", "{0}: the permission contract for {1} ended - {3} took the building and {2} customers.", s.Company, City.Name(site.Key), Go.Num(n), Rival.Name), 8);
        }

        /// <summary>Orders not taken after two days, and taps not switched on after three: a fifth (a tenth) give up.</summary>
        static void Impatience(IspState s)
        {
            long now = Test ? s.LastHour : NowHour();
            for (int i = s.Tickets.Count - 1; i >= 0; i--)
            {
                var t = s.Tickets[i];
                if (t.N <= 0) continue;
                float age = now - t.Opened;
                if (t.Type == Cat.TtOrder && age > 48) Drop(s, t, Round(t.N * 0.2f));
                else if (t.Type == Cat.TtOn && age > 72) Drop(s, t, Round(t.N * 0.1f));
                else continue;
                if (t.N <= 0) { s.Tickets.RemoveAt(i); foreach (var tech in s.Techs) if (tech.Ticket == t.Id) tech.Ticket = 0; }
            }
        }

        public static float AvgPrice(IspState s)
        {
            float sum = 0f; int n = 0;
            foreach (var site in s.Sites.Values)
                foreach (var c in site.C) { var t = TariffOf(s, c.T); if (t != null) { sum += t.Price * c.N; n += c.N; } }
            return n > 0 ? sum / n : 1.5f;
        }

        /// <summary>What a customer group expects to pay for a tariff: the market price with TV and phone, for its wealth, at
        /// the competitor's price level in the neighbourhood ("" = its average).</summary>
        public static float Worth(IspState s, Tariff t, bool biz, byte kind, string hood = "")
        {
            float v = Cat.RefPrice(t.Mbit);
            if (t.Tv || t.Mbit <= 0) v += t.Ip && t.Mbit > 0 ? Cat.RefTv + Cat.IptvExtra : Cat.RefTv * TvDraw(s);
            if (t.Phone && t.Mbit > 0) v += Cat.PhoneWorth;
            if (t.Mbit > 0) v += 0.004f * Mathf.Max(0, UpOf(t) - Mathf.Clamp(t.Mbit / 10, 5, Cat.UpLowMax));   // GO-UP-1: more upload than usual
            if (t.Sla && biz) v *= Cat.SlaWorth;   // GO-SLA-1
            return v * Cat.Wealth(biz, kind) * Rival.PriceLevel(s, hood);
        }

        /// <summary>How much a price draws: 1 at the market price, up to 1.8 when much cheaper, little when dear; a
        /// minimum term puts a few people off.</summary>
        public static float PriceDraw(IspState s, Tariff t, bool biz, byte kind, string hood = "")
        {
            if (t.Price <= 0.01f) return 1.8f;
            float f = Mathf.Pow(Worth(s, t, biz, kind, hood) / t.Price, 1.5f);
            return Mathf.Clamp(f, 0.05f, 1.8f) * (1f - 0.0025f * t.Term) * Cat.FeeDraw(s.Fee);
        }

        /// <summary>Daily cancellations. A coax customer who goes keeps the signal until the switching staff disconnect the
        /// tap; fiber is switched off from the OLT at once.</summary>
        static void Churn(IspState s, long abs)
        {
            float rep = s.Reputation;
            int today = (int)(abs / 24);
            int oldComplaints = 0, all = Customers(s);
            foreach (var t in s.Tickets) if (t.Type == Cat.TtComplaint && abs - t.Opened > 24) oldComplaints += t.N;
            float complaints = all > 0 ? oldComplaints * Cat.ComplaintChurn / all : 0f;   // nobody answered them for a day
            foreach (var site in s.Sites.Values)
            {
                s.Hoods.TryGetValue(site.Hood, out var hood);
                float cong = hood?.CongYesterday ?? 0f;
                int offTaps = 0;
                foreach (var c in site.C)
                {
                    if (c.N <= 0) continue;
                    var t = TariffOf(s, c.T);
                    int lost;
                    if (t == null) lost = c.N;
                    else
                    {
                        float dear = Mathf.Max(0f, t.Price / Mathf.Max(0.01f, Worth(s, t, c.Biz, site.Kind, site.Hood)) - 1.3f) * 0.05f;
                        float rate = Cat.BaseChurn + 0.03f * cong + 0.06f * site.OutYesterday * (c.Biz ? 2f : 1f) + dear
                                   + (rep < 60f ? (60f - rep) / 2000f : 0f) + Rival.ChurnExtra(s, site.Hood, today) + complaints;
                        if (!t.Active) rate += 0.01f;   // a tariff no longer sold: a few move on
                        if (!Places.Covered(s, site.Hood)) rate += Cat.UncoveredChurn;   // GO-HUBS-1: nobody looks after them there
                        rate *= t.Term >= 30 ? 0.5f : t.Term >= 10 ? 0.75f : 1f;
                        if (s.HeadendDown) rate += 0.05f;
                        lost = Mathf.Min(c.N, Round(c.N * rate));
                    }
                    if (lost <= 0) continue;
                    c.N -= lost;
                    s.Today.Left += lost;
                    GiveBack(s, c.Dev, c.Own, lost);
                    if (t != null && t.Ip && t.Tv) PutStb(s, lost);
                    if (t != null && t.Phone) s.NumbersUsed = Mathf.Max(0, s.NumbersUsed - Round(lost * (1f - Cat.PortShare)));
                    if (!c.Fib) offTaps += lost;
                }
                site.C.RemoveAll(x => x.N <= 0);
                if (offTaps > 0) OffTicket(s, site, abs, offTaps, false);
            }
        }

        /// <summary>Taps to disconnect in a building (the customers keep the signal until then); a move gets its own ticket.</summary>
        static void OffTicket(IspState s, Site site, long abs, int n, bool relocation)
        {
            site.FreeRiders += n;
            var off = s.Tickets.Find(x => x.Type == Cat.TtOff && x.Site == site.Key && x.Done <= 0f && x.Relocation == relocation);
            if (off == null) { off = Open(s, abs, Cat.TtOff, site.Hood, site.Key, 0); off.Work = 0f; off.Relocation = relocation; }
            off.N += n;
            off.Work += n * Cat.Fault(Cat.TtOff).work;
            CountWork(s, Cat.TtOff, n);
            off.Need = Mathf.Clamp(off.N, 1, 4);
        }

        /// <summary>Customers move (user 2026-11-06: "Umzugsgebühr, die den Schalter auslöst zum Abklemmen alte Adresse und
        /// Aufschalten neue Adresse"): half within their neighbourhood, the others anywhere in the city. Into one of the
        /// provider's buildings they take the contract along for the move fee (a dear fee makes some cancel instead): the
        /// switching staff disconnect the old tap and connect the new one (fiber: an appointment). Anywhere else they cancel.</summary>
        static void Moves(IspState s, long abs)
        {
            int movers = Sample(Customers(s) * Cat.MoveRate);
            for (int i = 0; i < movers; i++)
            {
                var (from, c) = RandomCustomer(s);
                if (from == null || c == null) break;
                var tr = TariffOf(s, c.T);
                var dest = Destination(from.Hood);
                Site? to = null;
                if (dest != null && dest.Key != from.Key && s.Sites.TryGetValue(dest.Key, out var ds) && !ds.Lost && (c.Fib ? ds.Fiber : ds.Coax && !ds.CoaxOff)
                    && CustomersOf(ds) + Pending(s, ds.Key, c.Biz) + 1 <= Cat.MaxShare * (ds.Homes + ds.Biz)) to = ds;
                c.N--;
                from.C.RemoveAll(x => x.N <= 0);
                if (!c.Fib) OffTicket(s, from, abs, 1, true);   // coax: the old tap stays on until the switching staff disconnect it
                if (to != null && Rand() < Cat.MoveStay(s.MoveFee))
                {
                    s.Today.Moves++;
                    s.Today.MoveFees += s.MoveFee;
                    var k = Open(s, abs, c.Fib ? Cat.TtInstall : Cat.TtOn, to.Hood, to.Key, 0);
                    k.T = c.T; k.Dev = c.Dev; k.Own = c.Own; k.Biz = c.Biz; k.Fib = c.Fib; k.Relocation = true;
                    k.N = 1; k.Work = Cat.Fault(k.Type).work; k.Need = 1;
                    if (c.Fib) k.Due = abs + 48;
                    CountWork(s, k.Type, 1);
                }
                else
                {   // moved where the provider is not (or the fee was too dear): the contract ends
                    s.Today.MovesLost++; s.Today.Left++;
                    GiveBack(s, c.Dev, c.Own, 1);
                    if (tr != null && tr.Ip && tr.Tv) PutStb(s, 1);
                    if (tr != null && tr.Phone && Rand() >= Cat.PortShare) s.NumbersUsed = Mathf.Max(0, s.NumbersUsed - 1);
                }
            }
        }

        /// <summary>One customer group, chosen by chance (each customer counts once).</summary>
        static (Site?, Cohort?) RandomCustomer(IspState s)
        {
            int all = Customers(s);
            if (all <= 0) return (null, null);
            int r = Rng.Next(all);
            foreach (var site in s.Sites.Values)
                foreach (var c in site.C)
                {
                    if (r < c.N) return (site, c);
                    r -= c.N;
                }
            return (null, null);
        }

        /// <summary>Where a household moves: half stay in the neighbourhood; weighted by the buildings' units.</summary>
        static House? Destination(string hood)
        {
            bool near = Rand() < Cat.MoveSameHood;
            int sum = 0;
            foreach (var b in City.All) if (b.Connectable && (!near || b.Hood == hood)) sum += b.Units;
            if (sum <= 0) return null;
            int r = Rng.Next(sum);
            foreach (var b in City.All)
            {
                if (!b.Connectable || (near && b.Hood != hood)) continue;
                if (r < b.Units) return b;
                r -= b.Units;
            }
            return null;
        }

        /// <summary>The tariffs a building's new customers can choose from.</summary>
        public static List<Tariff> Offerable(IspState s, Site site, bool biz)
        {
            var list = new List<Tariff>();
            if (site.Lost || !Places.Covered(s, site.Hood)) return list;   // GO-HUBS-1: no office or customer center there
            bool fiber = site.Fiber && OltAt(s, site.Hood);
            bool coax = site.Coax && !site.CoaxOff;
            if (!fiber && !coax) return list;
            s.Hoods.TryGetValue(site.Hood, out var hood);
            bool d31 = hood != null && hood.D31 && Cmts31At(s, site.Hood);
            foreach (var t in s.Tariffs)
            {
                if (!t.Active || t.Fiber != fiber) continue;
                if (t.Sla && !biz) continue;   // GO-SLA-1: a repair time is for firms
                if (t.Mbit <= 0) { if (!TvReady(s) || biz || site.Bulk > 0) continue; }   // GO-BULK-1: the owner pays their TV
                else if (t.Tv && (t.Ip ? !IptvReady(s) : !TvReady(s))) continue;
                if (t.Phone && t.Mbit > 0 && !PhoneReady(s)) continue;
                if (!fiber && t.Mbit > 500 && !d31) continue;
                if (!fiber && UpOf(t) > Cat.UpLowMax && !(hood != null && hood.HighSplit)) continue;   // GO-UP-1
                if (FreeFor(s, t, site.Hood) <= 0) { CapNote(s, t, site.Hood); continue; }   // GO-CAP-1: the devices are full; GO-STAFF-1: the people
                list.Add(t);
            }
            return list;
        }

        /// <summary>How much a customer group likes a tariff: segment 0 the price, 1 the value for the money, 2 the speed (a
        /// fair price still counts), 3 TV only by value.</summary>
        static float Score(IspState s, Tariff t, int segment, bool biz, byte kind, string hood = "")
        {
            if (segment == 0) return -t.Price + 0.0001f * t.Mbit;
            if (segment == 2) return Mathf.Log(Mathf.Max(1, t.Mbit), 2f) + 0.5f * Mathf.Min(1.8f, PriceDraw(s, t, biz, kind, hood));
            return Worth(s, t, biz, kind, hood) / Mathf.Max(0.01f, t.Price);
        }

        /// <summary>How sharply a group picks: the price-minded hardly look at a dearer tariff, others spread more.</summary>
        static float Spread(int segment) => segment == 0 ? 6f : segment == 2 ? 1.5f : 8f;

        static Tariff? Choose(IspState s, List<Tariff> list, int segment, bool biz, byte kind, string hood = "")
        {
            // segment 0 cheap, 1 value, 2 power, 3 TV only
            Tariff? best = null; float bestScore = float.MinValue;
            foreach (var t in list)
            {
                if (segment == 3 ? t.Mbit > 0 : t.Mbit <= 0) continue;
                float score = Score(s, t, segment, biz, kind, hood);
                if (score > bestScore) { bestScore = score; best = t; }
            }
            return best;
        }

        static void Join(IspState s, long abs)
        {
            if (s.HeadendDown) return;
            RefreshCaps(s);   // GO-CAP-1
            float growth = Pct("growth", 100) / 100f;
            float rep = 0.4f + s.Reputation / 100f;
            float sales = 1f + 0.5f * Mathf.Min(1f, s.Sales / 500f);
            int today = (int)(abs / 24);
            foreach (var site in s.Sites.Values)
            {
                s.Hoods.TryGetValue(site.Hood, out var hood);
                float quality = (1f - 0.6f * (hood?.CongYesterday ?? 0f)) * (1f - 0.8f * site.OutYesterday) * Rival.JoinFactor(s, site.Hood, today);
                for (int pass = 0; pass < 2; pass++)
                {
                    bool biz = pass == 1;
                    int units = biz ? site.Biz : site.Homes;
                    if (units <= 0) continue;
                    var list = Offerable(s, site, biz);
                    if (list.Count == 0) continue;
                    // ex-customers not switched off yet still have the signal - they take a unit, and do not sign
                    int ours = CustomersOf(site, biz: biz) + (biz ? 0 : site.Waiting + site.FreeRiders) + Pending(s, site.Key, biz);
                    float open = Cat.MaxShare * units - ours;
                    if (open < 0.5f) continue;
                    var segments = biz ? new[] { (1, 0.6f), (2, 0.4f) }
                                       : new[] { (3, Cat.TvOnlyShare), (0, 0.4f * (1f - Cat.TvOnlyShare)), (1, 0.45f * (1f - Cat.TvOnlyShare)), (2, 0.15f * (1f - Cat.TvOnlyShare)) };
                    foreach (var (seg, share) in segments)
                    {
                        var t = Choose(s, list, seg, biz, site.Kind, site.Hood);
                        if (t == null) continue;
                        float visited = Mathf.Min(1f, site.Canvassed / Mathf.Max(1, site.Homes + site.Biz));
                        float draw = PriceDraw(s, t, biz, site.Kind, site.Hood) * quality * rep * sales * growth * (1f + Cat.SalesBoost * visited)
                                   * (Deals.WsIn(s, site.Hood) > 0 ? Cat.WsCannibal : 1f) * (!biz && site.Bulk > 0 ? Cat.BulkJoin : 1f)   // GO-WHOLESALE-1, GO-BULK-1
                                   * (seg == 2 ? Rival.SpeedFactor(s, site.Hood, t.Mbit) : 1f)   // power users compare with the competitor's fastest
                                   * Events.JoinFactor(s, t, abs);   // GO-EVENTS-1: a sports promotion, the press, a test win
                        int n = Round(open * share * Cat.JoinRate * draw);
                        if (n <= 0) continue;
                        // people of one group do not all take the same: similar tariffs get a share by how close they come
                        var weights = new List<(Tariff t, float w)>();
                        float best = Score(s, t, seg, biz, site.Kind, site.Hood), sum = 0f;
                        foreach (var x in list)
                        {
                            if (seg == 3 ? x.Mbit > 0 : x.Mbit <= 0) continue;
                            float w = Mathf.Exp((Score(s, x, seg, biz, site.Kind, site.Hood) - best) * Spread(seg));
                            if (w < 0.03f) continue;
                            weights.Add((x, w)); sum += w;
                        }
                        int left = n;
                        for (int i = 0; i < weights.Count && left > 0; i++)
                        {
                            int k = i == weights.Count - 1 ? left : Mathf.Min(left, Round(n * weights[i].w / sum));
                            if (k > 0) { Sign(s, site, weights[i].t, biz, k, abs); left -= k; }
                        }
                    }
                }
            }
        }

        /// <summary>Customers of a building on their way in (orders, porting, switching on, appointments).</summary>
        static int Pending(IspState s, string key, bool biz)
        {
            int n = 0;
            foreach (var t in s.Tickets)
                if (t.Site == key && t.Biz == biz && !t.Move && (t.Type == Cat.TtInstall || t.Type == Cat.TtOrder || t.Type == Cat.TtPort || t.Type == Cat.TtOn)) n += t.N;
            return n;
        }

        /// <summary>New customers sign: the device (their own, rented or bought - whatever is cheaper over 120 days) and, for
        /// IPTV, a rented set-top box come from the stock - without them they wait. Then the order goes to the customer service.</summary>
        static void Sign(IspState s, Site site, Tariff t, bool biz, int n, long abs)
        {
            if (t.Mbit <= 0) { Order(s, site, t, Cat.DevNone, Cat.OwnSelf, biz, n, abs); return; }
            int free = FreeFor(s, t, site.Hood);   // GO-CAP-1: no more than the devices take; the rest waits
            if (free < n)
            {
                if (!biz) site.Waiting += n - Mathf.Max(0, free);
                CapNote(s, t, site.Hood);
                n = Mathf.Max(0, free);
                if (n <= 0) return;
            }
            UseFor(s, t, n, site.Hood);
            bool ip = t.Ip && t.Tv;
            if (ip)
            {
                int boxes = Mathf.Min(n, StbStock(s));
                if (boxes < n)
                {
                    if (!biz) site.Waiting += n - boxes;
                    StockNote(s);
                    n = boxes;
                    if (n <= 0) return;
                }
                if (!TakeStb(s, n)) return;
            }
            s.Hoods.TryGetValue(site.Hood, out var hood);
            bool d31 = hood != null && hood.D31 && Cmts31At(s, site.Hood);
            byte dev = t.Fiber ? Cat.DevOnt : t.Mbit > 500 ? Cat.Dev31 : d31 && (s.Stock.M31 > 0 && Rand() < 0.35f) ? Cat.Dev31 : Cat.Dev30;
            int own = Round(n * Cat.OwnDeviceShare);
            int rest = n - own;
            float rentCost = RentPrice(s, dev) * 120f, buy = BuyPrice(s, dev);
            float rentShare = Mathf.Clamp(buy / Mathf.Max(1f, buy + rentCost), 0.05f, 0.95f);
            int rent = Round(rest * rentShare), bought = rest - rent;
            int need = rent + bought, have = Mathf.Min(need, StockOf(s, dev));
            if (have < need)
            {
                // customers without a device wait (and a 3.0 modem can stand in for 3.1 below 500 Mbit/s)
                int missing = need - have;
                if (dev == Cat.Dev31 && t.Mbit <= 500)
                {
                    int alt = Mathf.Min(missing, s.Stock.M30);
                    if (alt > 0) { s.Stock.M30 -= alt; Order(s, site, t, Cat.Dev30, Cat.OwnRent, biz, alt, abs); missing -= alt; }
                }
                if (missing > 0)
                {
                    if (!biz) site.Waiting += missing;
                    if (ip) PutStb(s, missing);
                    StockNote(s);
                }
                float k = need > 0 ? have / (float)need : 0f;
                rent = Mathf.RoundToInt(rent * k); bought = have - rent;
            }
            TakeStock(s, dev, rent + bought);
            s.Today.Sold += bought * buy;
            Order(s, site, t, dev, Cat.OwnRent, biz, rent, abs);
            Order(s, site, t, dev, Cat.OwnBought, biz, bought, abs);
            Order(s, site, t, dev, Cat.OwnSelf, biz, own, abs);
        }

        static void StockNote(IspState s)
        {
            if (Has(s, "nostock" + Day())) return;
            s.Flags.Add("nostock" + Day());
            Notify.Warn(Go.T("go_note_nostock", "{0}: devices are out of stock - new customers are waiting.", s.Company), 6);
        }

        /// <summary>The order for the customer service (one per building and customer kind; a share brings its phone number).</summary>
        static void Order(IspState s, Site site, Tariff t, byte dev, byte own, bool biz, int n, long abs)
        {
            if (n <= 0) return;
            var probe = new Ticket { T = t.Id, Dev = dev, Own = own, Biz = biz, Fib = t.Fiber };
            var order = Merge(s, Cat.TtOrder, site, probe, n, abs, abs + 48);
            if (t.Phone && t.Mbit > 0) order.Ported += Round(n * Cat.PortShare);
        }

        /// <summary>Waiting customers get their device (and set-top box) once the stock has one; after three days they give up.</summary>
        static void Waiting(IspState s, long abs)
        {
            foreach (var site in s.Sites.Values)
            {
                if (site.Waiting <= 0) { site.WaitDays = 0; continue; }
                var list = Offerable(s, site, false);
                var t = Choose(s, list, 1, false, site.Kind, site.Hood);
                if (t == null) { site.Waiting = 0; site.WaitDays = 0; continue; }
                byte dev = t.Mbit <= 0 ? Cat.DevNone : t.Fiber ? Cat.DevOnt : t.Mbit > 500 ? Cat.Dev31 : Cat.Dev30;
                int n = dev == Cat.DevNone ? site.Waiting : Mathf.Min(site.Waiting, StockOf(s, dev));
                if (t.Ip && t.Tv && t.Mbit > 0) n = Mathf.Min(n, StbStock(s));
                if (n > 0)
                {
                    if (dev != Cat.DevNone && !TakeStock(s, dev, n)) continue;
                    if (t.Ip && t.Tv && t.Mbit > 0) TakeStb(s, n);
                    Order(s, site, t, dev, dev == Cat.DevNone ? Cat.OwnSelf : Cat.OwnRent, false, n, abs);
                    site.Waiting -= n;
                }
                if (site.Waiting > 0 && ++site.WaitDays > 3) { site.Waiting = 0; site.WaitDays = 0; }   // they gave up
            }
        }

        static void PayTvDay(IspState s)
        {
            int tv = TvCustomers(s);
            float rep = 0.4f + s.Reputation / 100f;
            foreach (var p in s.PayTv)
            {
                var o = Cat.Offer(p.Id);
                if (o == null) continue;
                float draw = p.Price <= 0.01f ? 2f : Mathf.Clamp(Mathf.Pow(o.Recommended / p.Price, 2f), 0f, 2f);
                int target = s.Cas && (TvReady(s) || IptvReady(s)) ? Mathf.Min(tv, Mathf.RoundToInt(tv * o.Demand * draw * rep)) : 0;
                if (p.Subs < target)
                {
                    int want = Mathf.CeilToInt((target - p.Subs) * 0.15f);
                    int casFree = Mathf.Max(0, CapCas(s) - UsePayTv(s));   // GO-CAP-1: the CA system's subscriptions
                    if (want > casFree)
                    {
                        want = casFree;
                        if (!Has(s, "capfull:cas:" + Day()))
                        {
                            s.Flags.RemoveAll(x => x.StartsWith("capfull:cas:"));
                            s.Flags.Add("capfull:cas:" + Day());
                            Event(s, Go.T("go_event_capfull", "{0} is full - new customers wait until another one stands in the headend.", Go.T("go_tv_cas", "CA system")));
                        }
                    }
                    int add = Mathf.Min(want, CardStock(s));
                    if (add > 0 && TakeCards(s, add)) p.Subs += add; else add = 0;
                    if (add < want && !Has(s, "nocards" + Day()))
                    {
                        s.Flags.Add("nocards" + Day());
                        Notify.Warn(Go.T("go_note_nocards", "{0}: smartcards are out of stock - Pay-TV cannot grow.", s.Company), 6);
                    }
                }
                else if (p.Subs > target)
                {
                    int off = Mathf.CeilToInt((p.Subs - target) * 0.15f);
                    p.Subs -= off; s.Stock.Cards += off - Round(off * 0.1f);
                }
            }
        }

        /// <summary>Every day one or two new applications per role; at most three wait per role.</summary>
        static void Candidates(IspState s, int day)
        {
            if (!Test) { s.Candidates.Clear(); return; }   // GAME-TYPES-1: the game's recruitment agencies bring the staff
            if (s.CandidatesDay == day) return;
            s.CandidatesDay = day;
            foreach (byte role in Cat.Roles)
            {
                var mine = s.Candidates.FindAll(c => c.Role == role);
                for (int i = 0; i < mine.Count - 2; i++) s.Candidates.Remove(mine[i]);
                int fresh = 1 + Rng.Next(2);
                for (int i = 0; i < fresh; i++)
                {
                    int skill = 1 + Rng.Next(5);
                    if (Rng.Next(3) == 0) skill = Mathf.Max(1, skill - 1);
                    float wage = Mathf.Round(Cat.Wage(role, skill) * (0.9f + 0.2f * Rand()) * 2f) / 2f;
                    int manner = 1 + Rng.Next(5);
                    if (Rng.Next(2) == 0) manner = (manner + 3) / 2;   // most are somewhere in the middle
                    s.Candidates.Add(new Candidate { Role = role, Name = Cat.First[Rng.Next(Cat.First.Length)] + " " + Cat.Last[Rng.Next(Cat.Last.Length)], Skill = skill, Wage = wage, Manner = manner });
                }
            }
        }

        public static void FirstCandidates(IspState s) => Candidates(s, Day());

        static void Unlocks(IspState s)
        {
            int n = Customers(s);
            if (n >= Unlock31(s) && !Has(s, "u31"))
            {
                s.Flags.Add("u31");
                Event(s, Go.T("go_event_u31", "DOCSIS 3.1 is available now."));
                Notify.Good(Go.T("go_note_u31", "{0}: {1} customers - DOCSIS 3.1 is available in the Network tab.", s.Company, Go.Num(n)), 8);
            }
            if (n >= UnlockFiber(s) && s.Cmts31 && !Has(s, "ufiber"))
            {
                s.Flags.Add("ufiber");
                Event(s, Go.T("go_event_ufiber", "FTTH with XGS-PON is available now."));
                Notify.Good(Go.T("go_note_ufiber", "{0}: {1} customers - fiber to the home (XGS-PON) is available in the Network tab.", s.Company, Go.Num(n)), 8);
            }
        }
    }
}
