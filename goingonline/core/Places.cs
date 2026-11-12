using System.Collections.Generic;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GO-HUBS-1, GO-CENTERS-1, GO-STAFF-1 (Going Online 0.8). The user, 2026-11-08: "sonst kann ein kleines Büro die
    /// ganze Stadt verwalten - es sollen ja mehrere Büros eröffnet werden ... in jedem Viertel muss mindestens ein Kundencenter
    /// und Büro sein"; the answers: office + customer center in the neighbourhood, the offices hold the staff and the headend,
    /// the customer centers are shops with walk-in customers and the devices, customers where both are missing cancel little
    /// by little; then "Hubs + Glasfaserring" and "festlegen, wie viel ein Personal (Technik, Kundendienst, Schalter) bedient
    /// und wie viel max. ein Meister an Personal unter sich hat".
    ///
    /// Built as real cable networks are (patents US7068712 and EP1394989, the cable franchise appendices of Washington D.C.):
    /// one main headend - satellite TV, the internet transit, telephony, Pay-TV - and hubs, each with the CMTS and OLT for the
    /// nodes of its area, joined by fibre.
    /// - An office of ours (the "Cable provider" business) in a neighbourhood is its office. The headend's neighbourhood has
    ///   the headend; any other neighbourhood's office serves it as a hub once the feeder fibre from the headend is built
    ///   (Hood.Link) and a CMTS stands there. Equipment and staff of the offices of a neighbourhood count for it (a second
    ///   office in the same neighbourhood is more room).
    /// - A neighbourhood without its own hub hangs on the headend, as the whole network did before 0.8 (its "group" is the
    ///   headend's neighbourhood).
    /// - A customer center (the "Cable customer center" business, a shop) looks after the customers of its neighbourhood.
    /// - New customers and new buildings only where both stand (Covered); elsewhere customers cancel little by little
    ///   (Cat.UncoveredChurn). A provider from before 0.8 keeps the old rules for Cat.V08Grace days.
    /// - Each person serves so many customers of their group (Cat.PerTech, PerSwitch, PerService; the founder some in the
    ///   headend's), a network master leads up to Cat.MasterSpan people in the field - those beyond work slower.
    /// The developer's dry run has no buildings: there every neighbourhood is covered and hangs on the headend.</summary>
    internal static class Places
    {
        const int NoLimit = 1000000;
        static readonly Dictionary<string, List<string>> _offices = new Dictionary<string, List<string>>();   // neighbourhood -> offices (the headend first)
        static readonly Dictionary<string, List<string>> _centers = new Dictionary<string, List<string>>();   // neighbourhood -> customer centers
        static readonly Dictionary<string, string> _group = new Dictionary<string, string>();                 // neighbourhood -> its group (cached)
        static readonly Dictionary<string, bool> _hubCmts = new Dictionary<string, bool>();
        static float _at = -100f;
        static IspState? _for;
        static string _head = "";

        /// <summary>Reads which buildings of ours stand where: at most every two seconds, or at once (force).</summary>
        public static void Refresh(IspState s, bool force = false)
        {
            if (!force && ReferenceEquals(s, _for) && _head == s.HeadendKey && Time.unscaledTime - _at < 2f) return;
            _for = s; _head = s.HeadendKey; _at = Time.unscaledTime;
            _offices.Clear(); _centers.Clear(); _group.Clear(); _hubCmts.Clear();
            if (Sim.Test) return;
            try
            {
                var regs = SaveGameManager.Current?.BuildingRegistrations;
                if (regs != null)
                    foreach (var reg in regs)
                    {
                        if (reg == null || !GameTypes.IsOurs(reg.businessTypeName) || !(reg.RentedByPlayer || reg.BuildingOwnedByPlayer)) continue;
                        string key = City.KeyOf(reg.Address);
                        if (!City.ByKey.TryGetValue(key, out var b) || b.Hood.Length == 0) continue;
                        string type = GameTypes.BuildingTypeOf(reg);
                        var map = type == "ba:buildingtype_office" ? _offices : type == "ba:buildingtype_retail" ? _centers : null;
                        if (map == null) continue;
                        if (!map.TryGetValue(b.Hood, out var l)) map[b.Hood] = l = new List<string>();
                        if (key == s.HeadendKey) l.Insert(0, key); else l.Add(key);
                    }
            }
            catch (System.Exception ex) { Go.Warn("places: " + ex.Message); }
            // the headend belongs to its neighbourhood in any case (a headend of 0.6 in a warehouse too)
            string hh = HeadHood(s);
            if (hh.Length > 0)
            {
                if (!_offices.TryGetValue(hh, out var l)) _offices[hh] = l = new List<string>();
                if (!l.Contains(s.HeadendKey)) l.Insert(0, s.HeadendKey);
            }
        }

        /// <summary>A provider of before 0.8 gets Cat.V08Grace days with the old rules; a new one starts with the new ones.</summary>
        public static void Migrate(IspState s)
        {
            if (s.Format >= 8) return;
            bool old = s.FoundedHour > 0 && (s.Sites.Count > 0 || s.Techs.Count > 0);
            s.Format = 8;
            s.V08Day = old ? Sim.Day() : Sim.Day() - Cat.V08Grace;
            if (old) Sim.Event(s, Go.T("go_event_v08", "Going Online 0.8: every neighbourhood needs an office of yours and a customer center (a shop) - from day {0} on there are new customers only where both stand, and elsewhere customers cancel little by little. Until then the old rules apply.", s.V08Day + Cat.V08Grace));
        }

        public static string HeadHood(IspState s) => City.ByKey.TryGetValue(s.HeadendKey, out var b) ? b.Hood : "";

        /// <summary>0.8's rules apply: a provider founded with 0.8, or the grace days are over.</summary>
        public static bool RulesOn(IspState s) => !Sim.Test && s.V08Day >= 0 && Sim.Today(s) >= s.V08Day + Cat.V08Grace;
        public static int GraceLeft(IspState s) => Sim.Test || s.V08Day < 0 ? 0 : Mathf.Max(0, s.V08Day + Cat.V08Grace - Sim.Today(s));

        public static List<string> OfficesIn(IspState s, string hood) { Refresh(s); return _offices.TryGetValue(hood, out var l) ? l : Empty; }
        public static List<string> CentersIn(IspState s, string hood) { Refresh(s); return _centers.TryGetValue(hood, out var l) ? l : Empty; }
        static readonly List<string> Empty = new List<string>();

        public static bool HasOffice(IspState s, string hood) => Sim.Test || OfficesIn(s, hood).Count > 0;
        public static bool HasCenter(IspState s, string hood) => Sim.Test || CentersIn(s, hood).Count > 0;

        /// <summary>The first office of a neighbourhood: its hub ("" = none).</summary>
        public static string Hub(IspState s, string hood) { var l = OfficesIn(s, hood); return l.Count > 0 ? l[0] : ""; }

        /// <summary>A CMTS stands in one of the neighbourhood's offices.</summary>
        static bool HubCmts(IspState s, string hood)
        {
            var offices = OfficesIn(s, hood);
            if (_hubCmts.TryGetValue(hood, out bool have)) return have;
            have = false;
            foreach (var key in offices) if (Gear.PlacedIn(key, GameTypes.ICmts30) + Gear.PlacedIn(key, GameTypes.ICmts31) + Gear.PlacedIn(key, GameTypes.IVcmts) > 0) { have = true; break; }
            _hubCmts[hood] = have;
            return have;
        }

        /// <summary>The cached groups are stale: a feeder was built, a CMTS set up.</summary>
        public static void Changed() { _group.Clear(); _hubCmts.Clear(); }

        /// <summary>The neighbourhood is served from its own office: the headend's, or a hub with its feeder fibre and a CMTS.</summary>
        public static bool Served(IspState s, string hood)
        {
            if (Sim.Test) return true;
            if (hood == HeadHood(s)) return true;
            return s.Hoods.TryGetValue(hood, out var h) && h.Link && HasOffice(s, hood) && HubCmts(s, hood);
        }

        /// <summary>The neighbourhood whose offices serve this one's network ("" in the dry run: all one).</summary>
        public static string Group(IspState s, string hood)
        {
            if (Sim.Test) return "";
            Refresh(s);
            if (_group.TryGetValue(hood, out var g)) return g;
            string hh = HeadHood(s);
            g = hood.Length == 0 || hood == hh || !Served(s, hood) ? hh : hood;
            _group[hood] = g;
            return g;
        }

        /// <summary>Every group with something to serve: the headend's neighbourhood and each served hub.</summary>
        public static List<string> Groups(IspState s)
        {
            var l = new List<string>();
            if (Sim.Test) { l.Add(""); return l; }
            l.Add(HeadHood(s));
            foreach (var h in s.Hoods.Keys) if (h != l[0] && Served(s, h)) l.Add(h);
            return l;
        }

        /// <summary>New customers and new buildings may come in this neighbourhood.</summary>
        public static bool Covered(IspState s, string hood) => !RulesOn(s) || (Served(s, hood) && HasCenter(s, hood));

        /// <summary>What a neighbourhood lacks for new customers: "" nothing, else "office", "link", "cmts" or "center".</summary>
        public static string Lacks(IspState s, string hood)
        {
            if (Sim.Test) return "";
            if (hood != HeadHood(s))
            {
                if (!HasOffice(s, hood)) return "office";
                if (!(s.Hoods.TryGetValue(hood, out var h) && h.Link)) return "link";
                if (!HubCmts(s, hood)) return "cmts";
            }
            return HasCenter(s, hood) ? "" : "center";
        }

        public static string LacksText(IspState s, string hood) => Lacks(s, hood) switch
        {
            "office" => Go.T("go_lack_office", "no office of yours in the neighbourhood"),
            "link" => Go.T("go_lack_link", "the feeder fibre from the headend is not built yet"),
            "cmts" => Go.T("go_lack_cmts", "no CMTS stands in its office"),
            "center" => Go.T("go_lack_center", "no customer center (a shop of yours) in the neighbourhood"),
            _ => "",
        };

        /// <summary>The offices of a group (the headend first in its own).</summary>
        public static List<string> GroupOffices(IspState s, string group) => OfficesIn(s, group);

        /// <summary>Every building of ours: the offices and the customer centers.</summary>
        public static List<string> AllKeys(IspState s)
        {
            Refresh(s);
            var l = new List<string>();
            foreach (var v in _offices.Values) foreach (var k in v) if (!l.Contains(k)) l.Add(k);
            foreach (var v in _centers.Values) foreach (var k in v) if (!l.Contains(k)) l.Add(k);
            return l;
        }

        public static List<string> AllOffices(IspState s)
        {
            Refresh(s);
            var l = new List<string>();
            foreach (var v in _offices.Values) foreach (var k in v) if (!l.Contains(k)) l.Add(k);
            return l;
        }

        public static bool IsOffice(IspState s, string key) { foreach (var v in _offices.Values) if (v.Contains(key)) return true; return false; }

        /// <summary>Equipment of a kind in a group's offices (a piece bought in the app before 0.6 counts one at the headend).</summary>
        public static int Units(IspState s, string group, string item, string flag)
        {
            int n = 0;
            if (!Sim.Test) foreach (var key in GroupOffices(s, group)) n += Gear.PlacedIn(key, item);
            if ((Sim.Test || group == HeadHood(s)) && s.AppGear.Contains(flag)) n++;
            return n;
        }

        // ── GO-STAFF-1: who serves how many ────────────────────────────────────────────────────────────────────────

        /// <summary>The group a person works for: the neighbourhood of the building they are assigned to; a network master
        /// outside our buildings (at the headquarters) the one the player gave them.</summary>
        public static string GroupOf(IspState s, Tech t)
        {
            if (Sim.Test) return "";
            if (t.Role == Cat.RoleMaster && t.Hood.Length > 0) return Group(s, t.Hood);   // a network master leads the people of the neighbourhood they look after
            string key = t.Base.Length > 0 ? t.Base : s.HeadendKey;
            return City.ByKey.TryGetValue(key, out var b) ? Group(s, b.Hood) : HeadHood(s);
        }

        /// <summary>A network master based in a hub looks after the hub's neighbourhood (Crew.Sync sets it).</summary>
        public static bool HubMaster(IspState s, Tech t) =>
            !Sim.Test && t.Role == Cat.RoleMaster && t.Base.Length > 0 && IsOffice(s, t.Base) && City.ByKey.TryGetValue(t.Base, out var b) && b.Hood != HeadHood(s);

        /// <summary>The group a ticket belongs to (a CMTS crash names its group, everything else its neighbourhood).</summary>
        public static string GroupOfTicket(IspState s, Ticket k) =>
            Sim.Test ? "" : k.Hood.Length == 0 ? HeadHood(s) : k.Type == Cat.TtCmts ? k.Hood : Group(s, k.Hood);

        public static int PerPerson(byte role) => role == Cat.RoleTech ? Cat.PerTech : role == Cat.RoleSwitch ? Cat.PerSwitch : Cat.PerService;

        public static int People(IspState s, string group, byte role)
        {
            int n = 0;
            foreach (var t in s.Techs) if (t.Role == role && GroupOf(s, t) == group) n++;
            return n;
        }

        /// <summary>How many customers a role of a group serves: the people times their share, the founder some in the
        /// headend's group, a contractor some in each.</summary>
        public static int Serves(IspState s, string group, byte role)
        {
            if (Sim.Test) return NoLimit;
            int n = People(s, group, role) * PerPerson(role);
            if (group == HeadHood(s)) n += Cat.FounderServes;
            if (role == Cat.RoleTech ? s.SubTech : role == Cat.RoleSwitch ? s.SubSwitch : s.SubService) n += Cat.SubServes;
            return n;
        }

        /// <summary>A group's customers, with those on their way in; coaxOnly for the switching staff (fibre is switched in
        /// the OLT).</summary>
        public static int CustomersOf(IspState s, string group, bool coaxOnly)
        {
            int n = 0;
            foreach (var site in s.Sites.Values)
            {
                if (Group(s, site.Hood) != group) continue;
                foreach (var c in site.C) if (!coaxOnly || !c.Fib) n += c.N;
            }
            foreach (var t in s.Tickets)
            {
                if (t.Move || t.Relocation || !(t.Type == Cat.TtOrder || t.Type == Cat.TtPort || t.Type == Cat.TtOn || t.Type == Cat.TtInstall)) continue;
                if (coaxOnly && t.Fib) continue;
                if (Group(s, t.Hood) == group) n += t.N;
            }
            return n;
        }

        /// <summary>How many more customers a group's people take, and which role is short ("" none).</summary>
        public static (int free, byte role) StaffFree(IspState s, string group)
        {
            if (Sim.Test) return (NoLimit, 255);
            int all = CustomersOf(s, group, false), coax = CustomersOf(s, group, true);
            int tech = Serves(s, group, Cat.RoleTech) - all, sw = Serves(s, group, Cat.RoleSwitch) - coax, sv = Serves(s, group, Cat.RoleService) - all;
            int free = Mathf.Min(tech, Mathf.Min(sw, sv));
            byte role = free == tech ? Cat.RoleTech : free == sv ? Cat.RoleService : Cat.RoleSwitch;
            return (Mathf.Max(0, free), free > 0 ? (byte)255 : role);
        }

        /// <summary>People in the field (technicians, switching staff) beyond what their group's network masters lead -
        /// Cat.MasterSpan each; they work slower (Cat.Unled). Earliest hired are led first.</summary>
        public static HashSet<int> Unled(IspState s)
        {
            var r = new HashSet<int>();
            if (Sim.Test || !RulesOn(s)) return r;
            var masters = new Dictionary<string, int>();
            foreach (var t in s.Techs) if (t.Role == Cat.RoleMaster) { string g = GroupOf(s, t); masters[g] = (masters.TryGetValue(g, out int m) ? m : 0) + 1; }
            var used = new Dictionary<string, int>();
            var field = s.Techs.FindAll(t => t.Role == Cat.RoleTech || t.Role == Cat.RoleSwitch);
            field.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (var t in field)
            {
                string g = GroupOf(s, t);
                int led = (masters.TryGetValue(g, out int m) ? m : 0) * Cat.MasterSpan;
                int u = used.TryGetValue(g, out int k) ? k : 0;
                if (u < led) used[g] = u + 1; else r.Add(t.Id);
            }
            return r;
        }

        /// <summary>Field staff of a group and how many its network masters lead.</summary>
        public static (int field, int led) Span(IspState s, string group)
        {
            int field = People(s, group, Cat.RoleTech) + People(s, group, Cat.RoleSwitch);
            return (field, People(s, group, Cat.RoleMaster) * Cat.MasterSpan);
        }

        /// <summary>Once a day: the grace days of a provider from before 0.8, then the neighbourhoods with customers that lack
        /// an office, its feeder or a customer center.</summary>
        public static void DayNote(IspState s, int today)
        {
            if (Sim.Test || s.V08Day < 0) return;
            var lacking = new List<string>();
            foreach (var h in s.Hoods.Values)
            {
                bool any = false;
                foreach (var site in s.Sites.Values) if (site.Hood == h.Id && site.C.Count > 0) { any = true; break; }
                if (any && Lacks(s, h.Id).Length > 0) lacking.Add(City.HoodName(h.Id) + " (" + LacksText(s, h.Id) + ")");
            }
            int left = s.V08Day + Cat.V08Grace - today;
            if (left > 0)
            {
                if (lacking.Count == 0) return;
                Sim.Event(s, Go.T("go_event_v08_left", "In {0} days the rules of 0.8 apply - new customers only where an office and a customer center stand. Still missing: {1}.", left, string.Join(", ", lacking)));
                if (left == 5 || left == 1) Notify.Warn(Go.T("go_note_v08_left", "{0}: in {1} days new customers come only to neighbourhoods with an office and a customer center of yours - see the Network tab.", s.Company, left), 10);
                return;
            }
            if (left == 0) Notify.Bad(Go.T("go_note_v08_on", "{0}: from today new customers come only to neighbourhoods with an office and a customer center of yours; elsewhere customers cancel little by little.", s.Company), 12);
            if (lacking.Count > 0) Sim.Event(s, Go.T("go_event_lacking", "No new customers, and customers leave little by little, in: {0}.", string.Join(", ", lacking)));
        }

        // ── GO-HUBS-1: feeder fibre and ring ──────────────────────────────────────────────────────────────────────

        /// <summary>Metres between two buildings (along the ground), 0 if one is unknown.</summary>
        public static float Metres(string a, string b)
        {
            var pa = City.PosOf(a); var pb = City.PosOf(b);
            if (pa == Vector3.zero || pb == Vector3.zero) return 0f;
            var d = pa - pb; d.y = 0f;
            return d.magnitude;
        }

        /// <summary>The nearest other place on the fibre (the headend or another linked hub) - the ring's second way.</summary>
        public static string RingPartner(IspState s, string hood)
        {
            string hub = Hub(s, hood), best = "";
            float bestD = float.MaxValue;
            foreach (var h in s.Hoods.Values)
            {
                if (h.Id == hood || h.Id == HeadHood(s) || !h.Link) continue;
                string other = Hub(s, h.Id);
                if (other.Length == 0) continue;
                float d = Metres(hub, other);
                if (d > 0f && d < bestD) { bestD = d; best = other; }
            }
            return best.Length > 0 ? best : s.HeadendKey;   // no other hub yet: a second, separate way to the headend
        }
    }
}
