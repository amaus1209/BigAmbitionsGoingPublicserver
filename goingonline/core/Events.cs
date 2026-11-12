using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GO-EVENTS-1 (user 2026-11-11, "Spielinhalts-Verbesserungen", chose "Ereignisse"): now and then something
    /// happens in the city that the provider has to decide on. A notification announces it; the overview shows it with two
    /// buttons until its deadline - no answer is the second choice, which costs nothing. At most one new event a day (rolled
    /// at 9:00), each kind with a pause of its own, never more than two undecided at a time; none in the developer's dry run.
    /// Costs and payments go into the day's bill (DayBook.Events / EventsIn, the finance app's "special measures" and
    /// "refunds and subsidies").
    /// - Storm: announced 8-14 h ahead over one to three of our neighbourhoods, for 6-10 h - network faults there four
    ///   times as often, fibre cuts three times. "Standby" pays the field staff overtime: they get 1.5 times as much done
    ///   during the storm and the day after.
    /// - Big match: a final in one or two days, 20-23 h - 35 % more traffic in those hours; a sports promotion brings 60 %
    ///   more sign-ups for tariffs with TV until the match day's bill. Afterwards a congested neighbourhood costs 3 points
    ///   of reputation, a smooth evening brings 1.
    /// - Excavator: a building company cuts a cable of ours (a fibre cut, or a coax trunk: one node's customers). Claiming
    ///   damages costs a lawyer; after 5-8 days the company's insurance pays in three of four cases.
    /// - Press: two big outages (25+ customers) within three days, or satisfaction under 50 - the city paper writes about
    ///   it: reputation -6 and a week of fewer sign-ups (x0.8). A credit for every customer (1 $ each) wins back 4 points
    ///   and softens the week (x0.92).
    /// - Test winner: satisfaction 85+, reputation 70+, 300+ customers - reputation +5, ten days of more sign-ups (x1.1,
    ///   x1.4 with an advertising campaign).
    /// - Fibre subsidy: the city pays for each building of a neighbourhood that gets fibre within 20 days (ordered from the
    ///   offer on) - joining promises at least two or three, else a penalty.</summary>
    internal static class Events
    {
        public const byte KStorm = 1, KMatch = 2, KDig = 3, KPress = 4, KAward = 5, KFund = 6;

        const float StormFaults = 4f, StormFiber = 3f, StandbyHours = 1.5f, StandbyPerHead = 90f;
        const float MatchLoad = 1.35f, MatchPromo = 1.6f;
        const float DigFee = 250f, DigBase = 1500f, DigPerCustomer = 15f, DigMax = 12000f, DigPays = 0.75f;
        const float PressHit = 6f, PressBack = 4f, PressJoin = 0.8f, PressJoinSoft = 0.92f, PressPerCustomer = 1f;
        const float AwardRep = 5f, AwardJoin = 1.1f, AwardJoinAd = 1.4f, AwardAd = 1500f;
        const float FundPerBuilding = 1500f, FundPenalty = 2500f;
        const int FundDays = 20;

        static string KeyOf(byte k) => k switch { KStorm => "storm", KMatch => "match", KDig => "dig", KPress => "press", KAward => "award", _ => "fund" };

        /// <summary>Days before the same kind may come again.</summary>
        static int Pause(byte k) => k switch { KStorm => 14, KMatch => 18, KDig => 12, KPress => 20, KAward => 45, _ => 30 };

        static bool Has(string list, string hood)
        {
            foreach (var p in list.Split(',')) if (p == hood) return true;
            return false;
        }

        static string Hoods(string list)
        {
            var names = new List<string>();
            foreach (var p in list.Split(',')) if (p.Length > 0) names.Add(City.HoodName(p));
            return string.Join(", ", names);
        }

        static string Clock(long abs) => Go.T("go_ev_clock", "day {0}, {1}:00", abs / 24, abs % 24);

        // ── the hour ────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Every game hour (Sim.Hour): deadlines, what the events do now and when they end, the daily roll.</summary>
        public static void Hour(IspState s, long abs)
        {
            if (Sim.Test || s.Company.Length == 0) return;
            foreach (var e in s.Happen)
                if (e.Choice < 0 && e.Due >= 0 && abs >= e.Due) Decide(s, e, false, true);
            for (int i = s.Happen.Count - 1; i >= 0; i--)
                if (Step(s, s.Happen[i], abs)) s.Happen.RemoveAt(i);
            s.BigOuts.RemoveAll(x => abs - x > 72);
            int today = (int)(abs / 24);
            if (abs % 24 == 9 && s.HappenDay != today && !s.HeadendDown) Roll(s, abs, today);
        }

        /// <summary>One event's hour; true when it is over.</summary>
        static bool Step(IspState s, Happening e, long abs)
        {
            switch (e.Kind)
            {
                case KStorm:
                    if (abs == e.Start) Sim.Event(s, Go.T("go_ev_storm_start", "The storm has reached {0}.", Hoods(e.Hoods)));
                    if (abs == e.End) Sim.Event(s, Go.T("go_ev_storm_end", "The storm over {0} is over.", Hoods(e.Hoods)));
                    return abs >= e.End + (e.Choice == 1 ? 24 : 0);
                case KMatch:
                    if (abs < e.End) return false;
                    if (abs == e.End) Judge(s, e);
                    return abs > e.End;   // kept to the day's bill: the promotion counts for the match day's sign-ups too
                case KDig:
                    if (e.Choice == 0) return true;
                    if (e.Choice < 0 || abs < e.End) return false;
                    if (Sim.Rand() < DigPays)
                    {
                        s.Today.EventsIn += e.Gain;
                        string paid = Go.T("go_ev_dig_paid", "The building company's insurance pays {0} damages for the cut cable in {1}.", Go.Money(e.Gain), Hoods(e.Hoods));
                        Sim.Event(s, paid);
                        Notify.Good(s.Company + ": " + paid, 8);
                    }
                    else
                    {
                        string no = Go.T("go_ev_dig_refused", "The building company rejects the claim for the cable in {0} - no damages.", Hoods(e.Hoods));
                        Sim.Event(s, no);
                        Notify.Warn(s.Company + ": " + no, 8);
                    }
                    return true;
                case KPress:
                case KAward:
                    if (abs < e.End) return false;
                    if (e.Kind == KPress) Sim.Event(s, Go.T("go_ev_press_end", "The press report is forgotten."));
                    return true;
                case KFund:
                    if (e.Choice == 0) return true;
                    if (e.Choice < 0 || abs < e.End) return false;
                    int n = FundCount(s, e);
                    float pay = Mathf.Min(n, e.Goal * 3) * e.Gain;
                    s.Today.EventsIn += pay;
                    string text;
                    if (n >= e.Goal)
                    {
                        text = Go.T("go_ev_fund_paid", "Fibre subsidy for {0}: the city pays {1} for {2} buildings.", Hoods(e.Hoods), Go.Money(pay), n);
                        Notify.Good(s.Company + ": " + text, 8);
                    }
                    else
                    {
                        s.Today.Events += e.Cost;
                        text = Go.T("go_ev_fund_missed", "Fibre subsidy for {0}: only {1} of {2} buildings - the city pays {3} and charges the penalty of {4}.",
                                    Hoods(e.Hoods), n, e.Goal, Go.Money(pay), Go.Money(e.Cost));
                        Notify.Warn(s.Company + ": " + text, 8);
                    }
                    Sim.Event(s, text);
                    return true;
            }
            return true;
        }

        /// <summary>After the match: did the network hold in its hours (20-22)?</summary>
        static void Judge(IspState s, Happening e)
        {
            var bad = new List<string>();
            foreach (var h in s.Hoods.Values)
            {
                if (Sim.HoodCoaxCustomers(s, h.Id) + Sim.HoodFiberCustomers(s, h.Id) <= 0) continue;
                for (int i = 20; i <= 22; i++)
                    if (h.Util[i] > 1f || h.FiberUtil[i] > 1f) { bad.Add(City.HoodName(h.Id)); break; }
            }
            string name = MatchName(e);
            if (bad.Count > 0)
            {
                s.Reputation = Mathf.Max(0f, s.Reputation - 3f);
                string text = Go.T("go_ev_match_bad", "During the {0} the picture stuttered in {1} - your reputation suffers.", name, string.Join(", ", bad));
                Sim.Event(s, text);
                Notify.Warn(s.Company + ": " + text, 8);
            }
            else
            {
                s.Reputation = Mathf.Min(100f, s.Reputation + 1f);
                Sim.Event(s, Go.T("go_ev_match_good", "The {0} ran without a stutter - the network held.", name));
            }
        }

        // ── new events ──────────────────────────────────────────────────────────────────────────────────────────────

        static void Roll(IspState s, long abs, int today)
        {
            int open = 0;
            foreach (var e in s.Happen) if (e.Choice < 0 && e.Due >= 0) open++;
            if (open >= 2 || Sim.Customers(s) < 30) return;
            var kinds = new List<byte> { KStorm, KMatch, KDig, KPress, KAward, KFund };
            for (int i = kinds.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (kinds[i], kinds[j]) = (kinds[j], kinds[i]); }
            foreach (var k in kinds)
            {
                if (Running(s, k)) continue;
                if (s.HappenLast.TryGetValue(KeyOf(k), out int last) && today - last < Pause(k)) continue;
                if (Sim.Rand() >= Chance(s, k)) continue;
                if (!Begin(s, k, abs, today)) continue;
                s.HappenLast[KeyOf(k)] = today;
                s.HappenDay = today;
                return;
            }
        }

        static bool Running(IspState s, byte k)
        {
            foreach (var e in s.Happen) if (e.Kind == k) return true;
            return false;
        }

        static float Chance(IspState s, byte k)
        {
            switch (k)
            {
                case KStorm: return OurHoods(s).Count > 0 ? 0.035f : 0f;
                case KMatch: return Sim.TvCustomers(s) >= 50 || Sim.InternetCustomers(s) >= 200 ? 0.03f : 0f;
                case KDig: return OurHoods(s).Count > 0 ? 0.03f : 0f;
                case KPress: return (s.BigOuts.Count >= 2 || s.Satisfaction < 50f) && Sim.Customers(s) >= 100 ? 0.3f : 0f;
                case KAward: return s.Satisfaction >= 85f && s.Reputation >= 70f && Sim.Customers(s) >= 300 ? 0.04f : 0f;
                default: return FundHood(s, out _) != null ? 0.025f : 0f;
            }
        }

        /// <summary>Neighbourhoods with our network and customers in them.</summary>
        static List<string> OurHoods(IspState s)
        {
            var list = new List<string>();
            foreach (var h in s.Hoods.Values)
                if ((h.Nodes > 0 || h.FiberIn) && Sim.HoodCoaxCustomers(s, h.Id) + Sim.HoodFiberCustomers(s, h.Id) > 0) list.Add(h.Id);
            return list;
        }

        static int FieldStaff(IspState s)
        {
            int n = 0;
            foreach (var t in s.Techs) if (t.Role == Cat.RoleTech || t.Role == Cat.RoleSwitch || t.Role == Cat.RoleMaster) n++;
            return n;
        }

        static bool Begin(IspState s, byte k, long abs, int today)
        {
            var e = new Happening { Id = s.NextId++, Kind = k, Start = abs };
            string text;
            switch (k)
            {
                case KStorm:
                {
                    var hoods = OurHoods(s);
                    for (int i = hoods.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (hoods[i], hoods[j]) = (hoods[j], hoods[i]); }
                    int n = UnityEngine.Random.Range(1, Mathf.Min(3, hoods.Count) + 1);
                    e.Hoods = string.Join(",", hoods.GetRange(0, n));
                    e.Start = abs + UnityEngine.Random.Range(8, 15);
                    e.End = e.Start + UnityEngine.Random.Range(6, 11);
                    int field = FieldStaff(s);
                    e.Cost = field * StandbyPerHead;
                    if (field > 0) e.Due = e.Start; else e.Choice = 0;
                    text = Title(s, e);
                    break;
                }
                case KMatch:
                    e.Start = (today + UnityEngine.Random.Range(1, 3)) * 24L + 20;
                    e.End = e.Start + 3;
                    e.Name = new[] { "final", "derby", "int" }[UnityEngine.Random.Range(0, 3)];
                    e.Cost = Mathf.Round(Mathf.Min(3000f, 800f + 0.3f * Sim.TvCustomers(s)));
                    e.Due = e.Start;
                    text = Title(s, e);
                    break;
                case KDig:
                {
                    var hoods = OurHoods(s);
                    string hood = hoods[UnityEngine.Random.Range(0, hoods.Count)];
                    s.Hoods.TryGetValue(hood, out var h);
                    int fib = Sim.HoodFiberCustomers(s, hood), coax = Sim.HoodCoaxCustomers(s, hood);
                    byte type; int affected;
                    if (fib > 0 && (coax <= 0 || h == null || h.Nodes <= 0 || Sim.Rand() < 0.5f)) { type = Cat.TtFiberCut; affected = fib; }
                    else if (h != null && h.Nodes > 0 && coax > 0) { type = Cat.TtAmp; affected = Mathf.Max(1, coax / h.Nodes); }
                    else return false;
                    if (Sim.OpenTickets(s, type, hood) > 0) return false;
                    Sim.Open(s, abs, type, hood, "", affected);
                    e.Hoods = hood;
                    e.Affected = affected;
                    e.Cost = DigFee;
                    e.Gain = Mathf.Round(Mathf.Min(DigMax, DigBase + DigPerCustomer * affected));
                    e.Due = abs + 48;
                    text = Title(s, e);
                    break;
                }
                case KPress:
                    s.Reputation = Mathf.Max(0f, s.Reputation - PressHit);
                    e.End = abs + 7 * 24;
                    e.Cost = Mathf.Round(Mathf.Clamp(Sim.Customers(s) * PressPerCustomer, 100f, 20000f));
                    e.Due = abs + 24;
                    s.BigOuts.Clear();
                    text = Title(s, e);
                    break;
                case KAward:
                    s.Reputation = Mathf.Min(100f, s.Reputation + AwardRep);
                    e.End = abs + 10 * 24;
                    e.Cost = AwardAd;
                    e.Due = abs + 24;
                    text = Title(s, e);
                    break;
                default:
                {
                    string? hood = FundHood(s, out int open);
                    if (hood == null) return false;
                    e.Hoods = hood;
                    e.Goal = open >= 12 ? 3 : 2;
                    e.Gain = FundPerBuilding;
                    e.Cost = FundPenalty;
                    e.StartDay = today;
                    e.End = abs + FundDays * 24;
                    e.Due = abs + 48;
                    text = Title(s, e);
                    break;
                }
            }
            s.Happen.Add(e);
            Sim.Event(s, text);
            string note = s.Company + ": " + text + (e.Due >= 0 && e.Choice < 0 ? " " + Go.T("go_ev_decide", "Decide in the app (Overview).") : "");
            if (k == KAward) Notify.Good(note, 10); else Notify.Warn(note, 10);
            Go.Log($"event {KeyOf(k)} (GO-EVENTS-1).");
            return true;
        }

        /// <summary>The neighbourhood for a fibre subsidy: fibre possible there, the most buildings still without it.</summary>
        static string? FundHood(IspState s, out int open)
        {
            string? best = null; open = 0;
            foreach (var h in s.Hoods.Values)
            {
                if (!Sim.OltAt(s, h.Id) || !Places.Covered(s, h.Id)) continue;
                int n = 0;
                foreach (var b in City.All)
                {
                    if (b.Hood != h.Id || !b.Connectable) continue;
                    if (s.Sites.TryGetValue(b.Key, out var site) && (site.Fiber || site.FiberHours > 0 || site.Lost)) continue;
                    n++;
                }
                if (n >= 6 && n > open) { open = n; best = h.Id; }
            }
            return best;
        }

        /// <summary>Buildings of the subsidy's neighbourhood with fibre ordered since the offer (built or being built).</summary>
        static int FundCount(IspState s, Happening e)
        {
            int n = 0;
            foreach (var site in s.Sites.Values)
                if (site.Hood == e.Hoods && site.FiberDay >= e.StartDay && (site.Fiber || site.FiberHours > 0)) n++;
            return n;
        }

        // ── decisions ───────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The overview's buttons.</summary>
        public static void Decide(IspState s, int id, bool yes)
        {
            foreach (var e in s.Happen) if (e.Id == id) { Decide(s, e, yes, false); return; }
        }

        static void Decide(IspState s, Happening e, bool yes, bool auto)
        {
            if (e.Choice >= 0) return;
            e.Choice = yes ? 1 : 0;
            long now = Sim.NowHour();
            if (yes)
            {
                switch (e.Kind)
                {
                    case KStorm:
                        s.Today.Events += e.Cost;
                        Sim.Event(s, Go.T("go_ev_storm_ordered", "Standby ordered: the field staff work overtime during the storm and the day after ({0}).", Go.Money(e.Cost)));
                        break;
                    case KMatch:
                        s.Today.Events += e.Cost;
                        Sim.Event(s, Go.T("go_ev_match_ordered", "Sports promotion booked ({0}).", Go.Money(e.Cost)));
                        break;
                    case KDig:
                        s.Today.Events += e.Cost;
                        e.End = (now >= 0 ? now : e.Start) + UnityEngine.Random.Range(5, 9) * 24;
                        Sim.Event(s, Go.T("go_ev_dig_claimed", "Damages claimed from the building company ({0} for the lawyer).", Go.Money(e.Cost)));
                        break;
                    case KPress:
                        s.Today.Events += e.Cost;
                        s.Reputation = Mathf.Min(100f, s.Reputation + PressBack);
                        Sim.Event(s, Go.T("go_ev_press_paid", "Every customer gets a credit ({0}) - part of the trust comes back.", Go.Money(e.Cost)));
                        break;
                    case KAward:
                        s.Today.Events += e.Cost;
                        Sim.Event(s, Go.T("go_ev_award_ordered", "Campaign with the test win booked ({0}).", Go.Money(e.Cost)));
                        break;
                    case KFund:
                        Sim.Event(s, Go.T("go_ev_fund_joined", "Joined the fibre subsidy in {0}: at least {1} buildings with fibre by {2}.", Hoods(e.Hoods), e.Goal, Clock(e.End)));
                        break;
                }
            }
            else
                Sim.Event(s, auto ? Go.T("go_ev_auto", "No decision in time - \"{0}\".", NoLabel(s, e)) : Go.T("go_ev_declined", "Decided: \"{0}\".", NoLabel(s, e)));
        }

        // ── what the simulation asks ────────────────────────────────────────────────────────────────────────────────

        /// <summary>Faults in a neighbourhood (Sim.Faults): a storm over it makes them more frequent.</summary>
        public static float FaultFactor(IspState s, string hood, long abs, bool fiber = false)
        {
            foreach (var e in s.Happen)
                if (e.Kind == KStorm && abs >= e.Start && abs < e.End && Has(e.Hoods, hood)) return fiber ? StormFiber : StormFaults;
            return 1f;
        }

        /// <summary>The field staff's hours (Sim.Staff): standby during a storm and the day after.</summary>
        public static float FieldBoost(IspState s, long abs)
        {
            foreach (var e in s.Happen)
                if (e.Kind == KStorm && e.Choice == 1 && abs >= e.Start && abs < e.End + 24) return StandbyHours;
            return 1f;
        }

        /// <summary>The traffic (Sim.Load): the big match's hours.</summary>
        public static float LoadFactor(IspState s, long abs)
        {
            foreach (var e in s.Happen)
                if (e.Kind == KMatch && abs >= e.Start && abs < e.End) return MatchLoad;
            return 1f;
        }

        /// <summary>Sign-ups for a tariff (Sim.Join, at the day's bill).</summary>
        public static float JoinFactor(IspState s, Tariff t, long abs)
        {
            float f = 1f;
            foreach (var e in s.Happen)
                switch (e.Kind)
                {
                    case KMatch: if (e.Choice == 1 && abs <= e.End + 1 && (t.Tv || t.Mbit <= 0)) f *= MatchPromo; break;
                    case KPress: if (abs <= e.End) f *= e.Choice == 1 ? PressJoinSoft : PressJoin; break;
                    case KAward: if (abs <= e.End) f *= e.Choice == 1 ? AwardJoinAd : AwardJoin; break;
                }
            return f;
        }

        /// <summary>Sim.Open: an outage of 25 or more customers (two within three days bring the press).</summary>
        public static void NoteOutage(IspState s, long abs)
        {
            if (Sim.Test) return;
            s.BigOuts.Add(abs);
            if (s.BigOuts.Count > 10) s.BigOuts.RemoveAt(0);
        }

        // ── the overview ────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>What the overview shows: undecided ones first.</summary>
        public static List<Happening> Shown(IspState s)
        {
            var list = new List<Happening>();
            foreach (var e in s.Happen) if (Open(e)) list.Add(e);
            foreach (var e in s.Happen) if (!Open(e) && (e.Kind != KDig || e.Choice == 1) && (e.Kind != KFund || e.Choice == 1)) list.Add(e);
            return list;
        }

        public static bool Open(Happening e) => e.Choice < 0 && e.Due >= 0;

        /// <summary>Tabs.Signature: the overview is built again when an event comes, is decided or ends.</summary>
        public static string Sig(IspState s)
        {
            var sb = new StringBuilder();
            foreach (var e in s.Happen) sb.Append(e.Id).Append(':').Append(e.Choice).Append(',');
            return sb.ToString();
        }

        /// <summary>GO-LOBBY-2: the customer center's spot - a test win now, the match the sports promotion is for.</summary>
        internal static bool AwardNow(IspState s)
        {
            long now = Sim.NowHour();
            foreach (var e in s.Happen) if (e.Kind == KAward && now < e.End) return true;
            return false;
        }

        internal static string? PromoMatch(IspState s)
        {
            long now = Sim.NowHour();
            foreach (var e in s.Happen) if (e.Kind == KMatch && e.Choice == 1 && now < e.End) return MatchName(e);
            return null;
        }

        static string MatchName(Happening e) => e.Name == "derby" ? Go.T("go_ev_match_derby", "city derby")
                                              : e.Name == "int" ? Go.T("go_ev_match_int", "international match")
                                              : Go.T("go_ev_match_final", "cup final");

        /// <summary>The event as it came (notification, events list, the overview while undecided).</summary>
        static string Title(IspState s, Happening e)
        {
            switch (e.Kind)
            {
                case KStorm:
                    return Go.T("go_ev_storm", "Storm warning: from {0} a storm crosses {1} for about {2} h - network faults there four times as often.",
                                Clock(e.Start), Hoods(e.Hoods), e.End - e.Start)
                         + (e.Due >= 0 ? " " + Go.T("go_ev_storm_ask", "Field staff on standby (overtime) get half as much again done during the storm and the day after.")
                                       : " " + Go.T("go_ev_storm_nostaff", "No field staff in the team - no standby possible."));
                case KMatch:
                    return Go.T("go_ev_match", "On day {0} at 20:00 the {1} is on - half the city watches: 35 % more traffic in the evening (streaming and IPTV). A sports promotion brings 60 % more sign-ups for tariffs with TV until then.",
                                e.Start / 24, MatchName(e));
                case KDig:
                    return Go.T("go_ev_dig", "A building company's excavator cut a cable of yours in {0} - {1} connections without service, the repair is under way. Claim damages? A lawyer costs {2}; the company's insurance usually pays (about {3}).",
                                Hoods(e.Hoods), Go.Num(e.Affected), Go.Money(e.Cost), Go.Money(e.Gain));
                case KPress:
                    return Go.T("go_ev_press", "The city paper writes about the outages at {0}: reputation -6, and for a week fewer people sign. A credit for all your customers ({1} in all) wins back trust.",
                                s.Company, Go.Money(e.Cost));
                case KAward:
                    return Go.T("go_ev_award", "A consumer magazine names {0} the best cable provider in the city: reputation +5, ten days of more sign-ups. Advertise the test win?",
                                s.Company);
                default:
                    return Go.T("go_ev_fund", "The city subsidises fibre in {0}: {1} for every building that gets fibre by {2}. Joining means at least {3} buildings - else a penalty of {4}.",
                                Hoods(e.Hoods), Go.Money(e.Gain), Clock(e.End), e.Goal, Go.Money(e.Cost));
            }
        }

        /// <summary>The overview's line for an event: the offer while undecided, else what goes on now.</summary>
        public static string Text(IspState s, Happening e)
        {
            if (Open(e)) return Title(s, e);
            long now = Sim.NowHour();
            switch (e.Kind)
            {
                case KStorm:
                    string standby = e.Choice == 1 ? Go.T("go_ev_standby_until", " | field staff on standby until {0}", Clock(e.End + 24)) : "";
                    return now < e.Start ? Go.T("go_ev_storm_soon", "Storm over {0} from {1}", Hoods(e.Hoods), Clock(e.Start)) + standby
                         : now < e.End ? Go.T("go_ev_storm_now", "Storm over {0} until {1}", Hoods(e.Hoods), Clock(e.End)) + standby
                         : Go.T("go_ev_storm_after", "After the storm over {0}", Hoods(e.Hoods)) + standby;
                case KMatch:
                    if (now >= e.End) return Go.T("go_ev_match_over", "The {0} is over.", MatchName(e));
                    if (now >= e.Start) return Go.T("go_ev_match_now", "The {0} is on - 35 % more traffic until 23:00.", MatchName(e));
                    return e.Choice == 1 ? Go.T("go_ev_match_promo", "Sports promotion until the {0} on day {1}: 60 % more sign-ups for tariffs with TV", MatchName(e), e.Start / 24)
                                         : Go.T("go_ev_match_soon", "The {0} on day {1} at 20:00: 35 % more traffic in its hours", MatchName(e), e.Start / 24);
                case KDig:
                    return Go.T("go_ev_dig_wait", "Claim against the building company ({0}): the answer comes by day {1}.", Hoods(e.Hoods), e.End / 24);
                case KPress:
                    return Go.T("go_ev_press_now", "Press report: fewer sign-ups until day {0}{1}", e.End / 24,
                                e.Choice == 1 ? Go.T("go_ev_press_soft", " (softened by the credit)") : "");
                case KAward:
                    return Go.T("go_ev_award_now", "Test winner: {0} more sign-ups until day {1}", Go.Pct((e.Choice == 1 ? AwardJoinAd : AwardJoin) - 1f), e.End / 24);
                default:
                    return Go.T("go_ev_fund_now", "Fibre subsidy in {0}: {1} of {2} buildings by {3}", Hoods(e.Hoods), FundCount(s, e), e.Goal, Clock(e.End));
            }
        }

        public static string YesLabel(IspState s, Happening e)
        {
            switch (e.Kind)
            {
                case KStorm: return Go.T("go_ev_storm_yes", "Order standby ({0})", Go.Money(e.Cost));
                case KMatch: return Go.T("go_ev_match_yes", "Sports promotion ({0})", Go.Money(e.Cost));
                case KDig: return Go.T("go_ev_dig_yes", "Claim damages ({0})", Go.Money(e.Cost));
                case KPress: return Go.T("go_ev_press_yes", "Credit for all ({0})", Go.Money(e.Cost));
                case KAward: return Go.T("go_ev_award_yes", "Advertise it ({0})", Go.Money(e.Cost));
                default: return Go.T("go_ev_fund_yes", "Join");
            }
        }

        public static string NoLabel(IspState s, Happening e)
        {
            switch (e.Kind)
            {
                case KStorm: return Go.T("go_ev_storm_no", "Wait");
                case KMatch: return Go.T("go_ev_match_no", "No promotion");
                case KDig: return Go.T("go_ev_dig_no", "Let it go");
                case KPress: return Go.T("go_ev_press_no", "Only a statement");
                case KAward: return Go.T("go_ev_award_no", "No campaign");
                default: return Go.T("go_ev_fund_no", "Decline");
            }
        }

        public static string DueText(Happening e) => e.Due >= 0 ? Go.T("go_ev_due", "decide by {0}", Clock(e.Due)) : "";
    }
}
