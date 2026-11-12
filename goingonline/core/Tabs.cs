using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoingOnline
{
    /// <summary>The page's content: the founding form, then the nine tabs. <see cref="Signature"/> says when a tab's
    /// layout changed (it is rebuilt then); all numbers in it update live.</summary>
    internal static class Tabs
    {
        static string _foundName = "", _foundKey = "";
        static int _hoodFilter = -1, _kindFilter, _stateFilter, _page;
        static string _search = "";
        const int PerPage = 25;

        static string Y(bool b) => b ? Go.T("go_yes", "yes") : Go.T("go_no", "no");
        static string Gc(string s) => $"<color={Ui.Hex(Ui.Good)}>{s}</color>";
        static string Wc(string s) => $"<color={Ui.Hex(Ui.Warn)}>{s}</color>";
        static string Bc(string s) => $"<color={Ui.Hex(Ui.Bad)}>{s}</color>";
        static string Sc(string s) => $"<color={Ui.Hex(Ui.Sub)}>{s}</color>";
        static int Hour { get { try { return SaveGameManager.Current?.Hour ?? 0; } catch { return 0; } } }
        static string PerDay(float v) => Go.T("go_per_day", "{0}/day", Go.Money(v));

        public static string Signature(IspState? s, int tab)
        {
            if (s == null) return _foundKey + "|" + string.Join(",", ChoiceKeys());
            var sb = new StringBuilder();
            switch (tab)
            {
                case 0: sb.Append(s.HeadendDown).Append(_dissolveAsk).Append(s.HeadendDown && City.Mine(s.HeadendKey)).Append(s.HeadendDown && GameTypes.AnyOurs())   // GO-DISSOLVE-1
                          .Append('|').Append(Events.Sig(s)); break;   // GO-EVENTS-1
                case 1:
                    sb.Append(s.Cmts31).Append(s.Olt).Append(s.HeadendDown).Append(s.Split64).Append(Sim.Has(s, "u31")).Append(Sim.Has(s, "ufiber")).Append(s.Voice).Append(s.Iptv).Append(s.Sat);
                    foreach (var h in s.Hoods.Values) sb.Append('|').Append(h.Id).Append(h.Nodes > 0).Append(h.D31).Append(h.D31Hours > 0).Append(h.FiberIn)
                                                        .Append(Places.Group(s, h.Id) == h.Id);
                    sb.Append(Places.RulesOn(s));
                    foreach (var hood in City.Hoods) sb.Append('#').Append(Places.OfficesIn(s, hood).Count).Append(Places.CentersIn(s, hood).Count);
                    if (s.HeadendDown) sb.Append(string.Join(",", ChoiceKeys()));
                    sb.Append('|').Append(Peers.Sig(s));   // GO-WHOLESALE-2: offers and contracts with other players
                    break;
                case 2:
                    sb.Append(_hoodFilter).Append(_kindFilter).Append(_stateFilter).Append(_page).Append(_search).Append(s.Olt);
                    foreach (var site in s.Sites.Values) sb.Append('|').Append(site.Key).Append(site.Coax).Append(site.Fiber).Append(site.CoaxHours > 0).Append(site.FiberHours > 0).Append(site.CoaxOff).Append(site.Lost);
                    break;
                case 3:
                    sb.Append(s.Olt).Append(s.Sat).Append(s.Voice).Append(s.Iptv);
                    foreach (var t in s.Tariffs) sb.Append('|').Append(t.Id);
                    break;
                case 4: sb.Append(s.Cmts31).Append(s.Olt).Append(s.Cas).Append(s.Iptv); break;
                case 5:
                    sb.Append(s.Sat).Append(s.Cas);
                    foreach (var p in s.PayTv) sb.Append('|').Append(p.Id);
                    break;
                case 6:
                    sb.Append(s.PlayerTicket).Append(s.AutoRenew);
                    foreach (var site in s.Sites.Values) if (!site.Lost && site.PermitUntil >= 0 && site.PermitUntil - Sim.Day() <= 15) sb.Append('~').Append(site.Key).Append(site.PermitUntil);
                    foreach (var t in s.Tickets) sb.Append('|').Append(t.Id);
                    break;
                case 7:
                    sb.Append(s.CenterKey).Append(s.Hoods.Count);
                    foreach (var t in s.Techs) sb.Append('|').Append(t.Id);
                    foreach (var c in s.Candidates) sb.Append('#').Append(c.Name);
                    foreach (var b in Fleet.CenterChoices()) sb.Append('~').Append(b.Key);
                    break;
                case 8: sb.Append(s.Books.Count).Append(s.Books.Count > 0 ? s.Books[0].Day : -1); break;
                case 9: sb.Append(s.Hoods.Count).Append(s.Rival.Log.Count > 0 ? s.Rival.Log[0] : "").Append(s.Rival.Locked.Count); break;
                case 10: sb.Append(s.HeadendKey).Append('|').Append(_vanEpoch); break;
            }
            return sb.ToString();
        }

        static List<string> ChoiceKeys()
        {
            var l = new List<string>();
            foreach (var b in City.HeadendChoices()) l.Add(b.Key);
            return l;
        }

        static string KindName(byte kind) =>
            kind == Cat.KindHome ? Go.T("go_kind_home", "Residential") : kind == Cat.KindRetail ? Go.T("go_kind_retail", "Shops + flats")
            : kind == Cat.KindOffice ? Go.T("go_kind_office", "Offices") : kind == Cat.KindWarehouse ? Go.T("go_kind_warehouse", "Warehouse")
            : Go.T("go_kind_villa", "Villa");

        static string Units(int homes, int biz)
        {
            var parts = new List<string>();
            if (homes > 0) parts.Add(Go.T("go_units_homes", "{0} households", Go.Num(homes)));
            if (biz > 0) parts.Add(Go.T("go_units_biz", "{0} businesses", Go.Num(biz)));
            return string.Join(" + ", parts);
        }

        // ── founding ─────────────────────────────────────────────────────────────────────────────────────────

        public static void Founding(Transform list)
        {
            if (!Sim.Test) { FoundingGame(list); return; }
            if (_foundName.Length == 0) _foundName = "Going Online";
            Ui.Head(list, Go.T("go_found_title", "Become a cable network operator"),
                Go.T("go_found_text", "Buy bandwidth, connect the city's buildings and sell internet, TV and phone lines - against Skyline Cable, the city's old cable company. You start small with DOCSIS 3.0; DOCSIS 3.1 and fiber to the home (XGS-PON) come later."));
            var card = Ui.Column(list, 10f, true);
            var r = Ui.Row(card);
            Ui.Label(r, Go.T("go_found_name", "Company name"), 300f, 22f, Ui.Sub);
            Ui.Field(r, _foundName, 520f, v => _foundName = v.Trim(), 32);

            Ui.Head(list, Go.T("go_found_headend", "Headend"),
                Go.T("go_found_headend_text2", "The headend needs an office that you rent or own - headend and customer center under one roof: satellite dishes for TV, the CMTS for the internet, later the OLT for fiber, the workplaces of your staff. Your technicians drive out from there."));
            var choices = new List<House>(City.HeadendChoices());
            var card2 = Ui.Column(list, 8f, true);
            if (choices.Count == 0)
                Ui.Para(card2, Wc(Go.T("go_found_none2", "You do not rent or own an office yet. Rent one (the city map shows them) and come back.")), 22f);
            foreach (var b in choices)
            {
                var row = Ui.Row(card2, 52f);
                string key = b.Key;
                Ui.Btn(row, City.Name(key) + "  " + Sc("(" + Go.T("go_kind_office", "Offices") + ", " + City.HoodName(b.Hood) + ")"),
                       -1f, () =>
                       {
                           _foundKey = key;
                           // a building with a business of the player's: its name
                           if ((_foundName.Length == 0 || _foundName == "Going Online") && City.BusinessName(key, out var biz)) _foundName = biz;
                       }, null, null, () => _foundKey == key).GetComponent<LayoutElement>().flexibleWidth = 1f;
            }
            // BIZMAN-FOUND-1: the other way in
            Ui.Para(card2, Sc(Go.T("go_found_bizman2", "Or in BizMan: for an empty office of yours, \"Cable provider\" stands next to the other businesses - the business is founded under the name you type, and the provider with it.")), 20f);
            // what the start costs (the founder loan went out again: "warum Kredit - raus damit")
            var cap = Ui.Column(list, 8f, true);
            Ui.LivePara(cap, () => Go.T("go_found_cost", "The start: the CMTS {0}, the first neighbourhood's node {1}, then each building {2} plus {3} per unit. Your money now: {4}.",
                Go.Money(Cat.Cmts30), Go.Money(Cat.FirstNode), Go.Money(Cat.SiteBase), Go.Money(Cat.SitePerUnit), Go.Money(Money.Cash)), 20f, Ui.Sub);
            Ui.Gap(list, 6f);
            var go = Ui.Row(list, 60f);
            Ui.Btn(go, "", 520f, () =>
            {
                if (Act.Found(_foundName, _foundKey)) { App.SetTab(0); _foundKey = ""; }
            }, () => _foundKey.Length > 0 && City.Mine(_foundKey) && Money.Cash >= Cat.Cmts30,
            () => Go.T("go_found_button", "Found the company - {0}", Go.Money(Cat.Cmts30)), null, 23f);
            Ui.LiveLabel(go, () => Go.T("go_found_cash", "Your money: {0}", Go.Money(Money.Cash)), -1f, 21f, Ui.Sub);
            Ui.Para(list, Go.T("go_found_package", "The start: a CMTS for DOCSIS 3.0 at the headend, 25 cable modems and three tariffs to begin with (you can change them). Prices are per day, like rent and wages in the game. The first technician's van can be leased in the Staff tab."), 20f);
            if (SaveCarrier.HasUnreadable)
                Ui.Para(list, Bc(Go.T("go_found_unreadable", "This save has a provider that could not be read. It stays in the save untouched; founding a new one replaces it.")), 20f);
        }

        // ── the tabs ─────────────────────────────────────────────────────────────────────────────────────────

        public static void Build(IspState s, int tab, Transform list)
        {
            switch (tab)
            {
                case 0: Overview(s, list); break;
                case 1: Network(s, list); break;
                case 2: Buildings(s, list); break;
                case 3: Tariffs(s, list); break;
                case 4: Devices(s, list); break;
                case 5: Tv(s, list); break;
                case 6: Faults(s, list); break;
                case 7: Team(s, list); break;
                case 8: Finance(s, list); break;
                case 10: VanTab(s, list); break;
                default: RivalTab(s, list); break;
            }
        }

        static DayBook? Yesterday(IspState s) => s.Books.Count > 0 ? s.Books[0] : null;

        /// <summary>GO-EVENTS-1: the events - the undecided ones with their two buttons and deadline, then what goes on now.</summary>
        static void Happenings(IspState s, Transform list)
        {
            var shown = Events.Shown(s);
            if (shown.Count == 0) return;
            var box = Ui.Column(list, 8f, true);
            Ui.Para(box, Go.T("go_ev_title", "Events"), 24f, Color.white, FontStyles.Bold);
            foreach (var e in shown)
            {
                var ev = e;
                if (Events.Open(ev))
                {
                    Ui.LivePara(box, () => Wc(Events.Text(s, ev)), 21f, Color.white);
                    var r = Ui.Row(box, 46f);
                    Ui.Btn(r, Events.YesLabel(s, ev), 420f, () => Events.Decide(s, ev.Id, true));
                    Ui.Btn(r, Events.NoLabel(s, ev), 300f, () => Events.Decide(s, ev.Id, false));
                    Ui.LiveLabel(r, () => Events.DueText(ev), -1f, 20f, Ui.Sub);
                }
                else Ui.LivePara(box, () => Events.Text(s, ev), 21f);
            }
        }

        static void Overview(IspState s, Transform list)
        {
            Ui.Head(list, s.Company, Go.T("go_ov_sub", "Headend: {0}", City.Name(s.HeadendKey)));
            if (s.HeadendDown)
                Ui.Para(list, Bc(s.NoCmts && City.Mine(s.HeadendKey)
                    ? Go.T("go_ov_nocmts", "No CMTS stands in the headend - the network is down. Buy one at {0} and set it up there.", Stores(GameTypes.FurnitureStores))
                    : Go.T("go_ov_down", "The headend's building is no longer yours - the whole network is down. Move the headend in the Network tab.")), 22f);
            if (s.HeadendDown && !Sim.Test && !City.Mine(s.HeadendKey) && !GameTypes.AnyOurs()) Dissolving(s, list);   // GO-DISSOLVE-1
            if (!Sim.Test)   // GO-OFFICE-2: what the office still needs before the staff can work there
                Ui.LivePara(list, () =>
                {
                    var miss = GameTypes.MissingSetup(City.Reg(s.HeadendKey));
                    return miss.Count == 0 ? "" : Wc(Go.T("go_ov_setup", "The office is not set up yet - your staff cannot work there until it has: {0}. BizMan (Insight) shows how many.", string.Join(", ", miss)));
                }, 21f);
            if (!Sim.Test)   // GO-HUBS-1, GO-CENTERS-1: neighbourhoods with customers that nobody looks after
                Ui.LivePara(list, () =>
                {
                    var lacking = new List<string>();
                    foreach (var h in s.Hoods.Values)
                    {
                        bool any = false;
                        foreach (var site in s.Sites.Values) if (site.Hood == h.Id && site.C.Count > 0) { any = true; break; }
                        if (any && Places.Lacks(s, h.Id).Length > 0) lacking.Add(City.HoodName(h.Id) + " (" + Places.LacksText(s, h.Id) + ")");
                    }
                    if (lacking.Count == 0) return "";
                    int left = Places.GraceLeft(s);
                    return left > 0 ? Wc(Go.T("go_ov_lacking_soon", "In {0} days new customers come only where an office and a customer center of yours stand - missing in: {1}. See the Network tab.", left, string.Join(", ", lacking)))
                                    : Bc(Go.T("go_ov_lacking", "No new customers, and customers leave little by little, in: {0}. See the Network tab.", string.Join(", ", lacking)));
                }, 21f);
            if (!Sim.Test && City.Mine(s.HeadendKey) && GameFound.HeadendMisplaced(s))   // GO-OFFICE-1: a headend of 0.6 in a warehouse
                Ui.Para(list, Wc(Go.T("go_ov_warehouse2", "The headend still stands in a warehouse. Since Going Online 0.7 the cable provider is one business in an office - headend and customer center under one roof: in BizMan, \"Start new business\" in an office, \"Cable provider\" (it needs the course \"Office Businesses\"; or take the office of your customer center), and set up a CMTS there. The headend then moves by itself (assign your staff to the office), and the warehouse stays your store for devices.")), 21f);
            if (!Sim.Test) Happenings(s, list);   // GO-EVENTS-1
            var c = Ui.Column(list, 4f, true);
            Ui.Pair(c, Go.T("go_ov_customers", "Customers"), () =>
                Go.T("go_ov_customers_v", "{0} in all | internet {1} | TV {2} | Pay-TV {3}", Go.Num(Sim.Customers(s)), Go.Num(Sim.InternetCustomers(s)),
                     Go.Num(Sim.TvCustomers(s)), Go.Num(PayTvSubs(s))));
            if (!Sim.Test)
                Ui.Pair(c, Go.T("go_ov_deals", "Rented and bulk"), () =>
                {
                    string ws = s.Wholesale ? Go.T("go_ov_ws", "{0} lines rented to {1}", Go.Num(Deals.WsTotal(s)), Cat.WsName) : s.WsOffered >= 0 ? Wc(Go.T("go_ov_ws_offer", "{0} wants to rent lines (Network tab)", Cat.WsName)) : "-";
                    int bulk = Deals.BulkHomes(s);
                    return ws + (bulk > 0 ? " | " + Go.T("go_ov_bulk", "{0} homes with bulk TV", Go.Num(bulk)) : "") + (Deals.CacheActive(s) ? " | " + Go.T("go_ov_cache", "streaming cache on") : "");
                });
            Ui.Pair(c, Go.T("go_ov_yesterday", "Yesterday"), () =>
            {
                var b = Yesterday(s);
                if (b == null) return Sc(Go.T("go_ov_noday", "the first bill comes at midnight"));
                float res = b.Operating - b.Tax;
                return Go.T("go_ov_yesterday_v", "income {0} | running costs {1} | levy {2} | result {3}", Go.Money(b.Income), Go.Money(b.Running), Go.Money(b.Tax),
                            res >= 0 ? Gc(Go.Money(res)) : Bc(Go.Money(res)));
            });
            Ui.Pair(c, Go.T("go_ov_week", "Last 7 days"), () =>
            {
                float res = 0f, inv = 0f; int n = 0;
                foreach (var b in s.Books) { if (n++ >= 7) break; res += b.Operating - b.Tax; inv += b.Build + b.Gear; }
                return Go.T("go_ov_week_v", "result {0} | invested {1}", res >= 0 ? Gc(Go.Money(res)) : Bc(Go.Money(res)), Go.Money(inv));
            });
            Ui.Pair(c, Go.T("go_ov_today", "Today so far"), () =>
                Go.T("go_ov_today_v", "{0} new customers | {1} cancelled | {2} faults | {3} fixed", s.Today.Joined, s.Today.Left, s.Today.Faults, s.Today.Fixed));
            Ui.Pair(c, Go.T("go_ov_rival", "Competition"), () =>
            {
                int units = 0, ours = Sim.Customers(s);
                foreach (var b in City.All) if (b.Connectable && s.Hoods.ContainsKey(b.Hood)) units += b.Units;
                string txt = Go.T("go_ov_rival_v", "{0} | in your neighbourhoods you have {1}, it has {2}", Rival.Name,
                    Go.Pct(units > 0 ? Mathf.Clamp01(ours / (float)units) : 0f), Go.Pct(units > 0 ? Mathf.Clamp01(1f - ours / (float)units) : 1f));
                int today = Sim.Day();
                foreach (var kv in s.Rival.Hoods)
                    if (s.Hoods.ContainsKey(kv.Key) && (kv.Value.PromoUntil >= today || kv.Value.PriceUntil >= today)) { txt += " | " + Wc(Go.T("go_ov_rival_fight", "fights back in {0}", City.HoodName(kv.Key))); break; }
                return txt;
            });
            FirstSteps(s, list);
            var sat = Ui.Row(c, 40f);
            Ui.Label(sat, Go.T("go_ov_satisfaction", "Customer satisfaction"), 420f, 21f, Ui.Sub);
            Ui.Bar(sat, 360f, 22f, () => s.Satisfaction / 100f, () => s.Satisfaction >= 70f ? Ui.Good : s.Satisfaction >= 50f ? Ui.Warn : Ui.Bad);
            Ui.LiveLabel(sat, () => Go.Num(s.Satisfaction) + " / 100", -1f, 21f);
            var rep = Ui.Row(c, 40f);
            Ui.Label(rep, Go.T("go_ov_reputation", "Reputation"), 420f, 21f, Ui.Sub);
            Ui.Bar(rep, 360f, 22f, () => s.Reputation / 100f, () => s.Reputation >= 60f ? Ui.Good : s.Reputation >= 40f ? Ui.Warn : Ui.Bad);
            Ui.LiveLabel(rep, () => Go.Num(s.Reputation) + " / 100", -1f, 21f);
            Ui.Pair(c, Go.T("go_ov_load", "Busiest neighbourhood today"), () =>
            {
                string name = "-"; float max = 0f;
                foreach (var h in s.Hoods.Values) { float m = Max(h.Util); if (m > max) { max = m; name = City.HoodName(h.Id); } }
                return max <= 0f ? "-" : Load(max) + "  " + Sc(name);
            });
            Ui.Pair(c, Go.T("go_ov_upstream", "Upstream"), () =>
                Go.T("go_ov_upstream_v", "now {0} | booked {1}", Go.Bw(s.UpLoad[Hour]), Go.Bw(Cat.TransitMbit[s.TransitTier])));
            if (!Sim.Test)
                Ui.Pair(c, Go.T("go_ov_power", "Power cuts"), () => { string now = Grid.CutsNow(s); return now.Length == 0 ? Sc("-") : Wc(now); });
            Ui.Pair(c, Go.T("go_ov_faults", "Open faults"), () =>
            {
                int n = 0, off = 0, inst = 0;
                foreach (var t in s.Tickets)
                {
                    if (t.Type == Cat.TtInstall) { inst += t.N; continue; }
                    n++; if (Sim.IsOutage(t.Type)) off += t.Affected;
                }
                return Go.T("go_ov_faults_v", "{0} faults | {1} customers without service | {2} installations waiting", n, Go.Num(off), Go.Num(inst));
            });
            Ui.Pair(c, Go.T("go_ov_pipeline", "On their way in"), () =>
                Go.T("go_ov_pipeline_v", "orders {0} | porting {1} | switch on {2} | appointments {3}",
                     Go.Num(Sim.InPipeline(s, Cat.TtOrder)), Go.Num(Sim.InPipeline(s, Cat.TtPort)), Go.Num(Sim.InPipeline(s, Cat.TtOn)), Go.Num(Sim.InPipeline(s, Cat.TtInstall, false))));
            Ui.Pair(c, Go.T("go_ov_calls", "Calls today"), () =>
                s.CallsToday <= 0f ? "-" : Go.T("go_ov_calls_v", "{0} calls, {1} answered | {2} still on the signal without a contract",
                    Go.Num(s.CallsToday), Go.Pct(Mathf.Clamp01(s.CallsAnswered / s.CallsToday)), Go.Num(Sim.FreeRiders(s))));
            Ui.Pair(c, Go.T("go_ov_team", "Team"), () =>
            {
                int on = 0;
                foreach (var t in s.Techs) if (t.Missing == 0) on++;
                return Go.T("go_ov_team_v", "{0} people, {1} at work now", s.Techs.Count, on);
            });

            Ui.Head(list, Go.T("go_sat_title", "Why customers are happy or not"),
                Go.T("go_sat_text", "Yesterday's satisfaction in six parts. Too few people show up here first: faults stay open (technicians), taps wait (switching staff), calls go unanswered and orders pile up (customer service). Ways out: hire (Staff tab shows the need per role), contractors for the jobs that wait, overtime for a few days."));
            var sc = Ui.Column(list, 4f, true);
            SatLine(s, sc, Go.T("go_sat_net", "Network (evenings, outages)"), () => s.SatNet, Go.T("go_sat_net_fix", "more nodes, DOCSIS 3.1, more bandwidth, network masters"));
            SatLine(s, sc, Go.T("go_sat_faults", "Faults fixed in time"), () => s.SatFaults, Go.T("go_sat_faults_fix", "more technicians or technical support, contractor for technicians"));
            SatLine(s, sc, Go.T("go_sat_appoint", "Appointments kept"), () => s.SatAppoint, Go.T("go_sat_appoint_fix", "more technicians, contractor, overtime"));
            SatLine(s, sc, Go.T("go_sat_calls", "Calls answered"), () => s.SatCalls, Go.T("go_sat_calls_fix", "more customer service (customer center), contractor for the customer service"));
            SatLine(s, sc, Go.T("go_sat_orders", "Orders and switching on time"), () => s.SatOrders, Go.T("go_sat_orders_fix", "more customer service and switching staff, contractors"));
            SatLine(s, sc, Go.T("go_sat_complaints", "Few complaints, answered in time"), () => s.SatComplaints, Go.T("go_sat_complaints_fix", "customer service for the complaints, friendly technicians (training), fair charges"));

            Ui.Head(list, Go.T("go_ov_budgets", "Daily budgets"),
                Go.T("go_ov_budgets_text", "Network maintenance lowers the coax faults (up to half at $40 per node and day). Sales and advertising bring new customers faster (full effect at $500 a day)."));
            var bc = Ui.Column(list, 6f, true);
            var r1 = Ui.Row(bc);
            Ui.Label(r1, Go.T("go_ov_upkeep", "Network maintenance"), 420f, 21f, Ui.Sub);
            Ui.Btn(r1, "-", 60f, () => Act.Upkeep(s, -25f));
            Ui.LiveLabel(r1, () => PerDay(s.Upkeep), 200f, 22f, null, FontStyles.Normal, TextAlignmentOptions.Center);
            Ui.Btn(r1, "+", 60f, () => Act.Upkeep(s, 25f));
            var r2 = Ui.Row(bc);
            Ui.Label(r2, Go.T("go_ov_sales", "Sales and advertising"), 420f, 21f, Ui.Sub);
            Ui.Btn(r2, "-", 60f, () => Act.Sales(s, -50f));
            Ui.LiveLabel(r2, () => PerDay(s.Sales), 200f, 22f, null, FontStyles.Normal, TextAlignmentOptions.Center);
            Ui.Btn(r2, "+", 60f, () => Act.Sales(s, 50f));

            Ui.Head(list, Go.T("go_ov_unlocks", "Technology"));
            var uc = Ui.Column(list, 4f, true);
            Ui.Pair(uc, "DOCSIS 3.1", () => s.Cmts31 ? Gc(Go.T("go_running", "running")) :
                Sim.Customers(s) >= Sim.Unlock31(s) ? Wc(Go.T("go_available", "available - Network tab")) :
                Go.T("go_unlock_at", "from {0} customers ({1} now)", Go.Num(Sim.Unlock31(s)), Go.Num(Sim.Customers(s))));
            Ui.Pair(uc, Go.T("go_fiber_name", "FTTH with XGS-PON"), () => s.Olt ? Gc(Go.T("go_running", "running")) :
                Sim.Customers(s) >= Sim.UnlockFiber(s) && s.Cmts31 ? Wc(Go.T("go_available", "available - Network tab")) :
                Go.T("go_unlock_fiber_at", "from {0} customers and with DOCSIS 3.1 ({1} now)", Go.Num(Sim.UnlockFiber(s)), Go.Num(Sim.Customers(s))));

            Ui.Head(list, Go.T("go_ov_events", "What happened"));
            var ec = Ui.Column(list, 4f, true);
            Ui.LivePara(ec, () => s.Events.Count == 0 ? Sc("-") : string.Join("\n", s.Events.GetRange(0, Mathf.Min(12, s.Events.Count))), 20f);
        }

        static int PayTvSubs(IspState s) { int n = 0; foreach (var p in s.PayTv) n += p.Subs; return n; }

        static void SatLine(IspState s, Transform parent, string name, System.Func<float> value, string fix)
        {
            var r = Ui.Row(parent, 38f);
            Ui.Label(r, name, 420f, 20f, Ui.Sub);
            Ui.Bar(r, 260f, 18f, value, () => value() >= 0.8f ? Ui.Good : value() >= 0.5f ? Ui.Warn : Ui.Bad);
            Ui.LiveLabel(r, () => Go.Pct(value()), 100f, 20f, null, FontStyles.Normal, TextAlignmentOptions.Center);
            Ui.LiveLabel(r, () => value() >= 0.8f ? "" : Wc(Go.T("go_sat_fix", "helps: {0}", fix)), -1f, 19f);
        }
        static float Max(float[] v) { float m = 0f; foreach (var x in v) if (x > m) m = x; return m; }
        static string Load(float util) { string p = Go.Pct(util); return util >= 1f ? Bc(p) : util >= 0.8f ? Wc(p) : Gc(p); }

        static void Network(IspState s, Transform list)
        {
            Ui.Head(list, Go.T("go_net_headend", "Headend"), City.Name(s.HeadendKey));
            var hc = Ui.Column(list, 6f, true);
            if (s.HeadendDown && !City.Mine(s.HeadendKey) && !Sim.Test)   // GO-OFFICE-1: a new office takes over by itself
                Ui.Para(hc, Bc(Go.T("go_net_down_game", "The building is no longer yours - the network is down. In BizMan, \"Start new business\" in another office of yours, \"Cable provider\", and set up a CMTS there - the headend moves there by itself.")), 21f);
            else if (s.HeadendDown && !City.Mine(s.HeadendKey))
            {
                Ui.Para(hc, Bc(Go.T("go_net_down2", "The building is no longer yours - the network is down. Move the headend to an office you rent or own ({0}).", Go.Money(Cat.MoveHeadend))), 21f);
                foreach (var b in City.HeadendChoices())
                {
                    string key = b.Key;
                    var r = Ui.Row(hc);
                    Ui.Label(r, City.Name(key), -1f);
                    Ui.Btn(r, Go.T("go_net_move", "Move here"), 260f, () => Act.MoveHeadend(s, key), () => Money.CanPay(Cat.MoveHeadend));
                }
            }
            if (!Sim.Test) Ui.LivePara(hc, () => Sc(Go.T("go_gear_where", "The equipment is furniture: buy it at {0} (in the store or by phone) and set it up in the headend, {1}. It runs as long as it stands there and costs power and maintenance every day.", Stores(GameTypes.FurnitureStores), City.Name(s.HeadendKey))), 19f);
            Equip(hc, "CMTS DOCSIS 3.0", Go.T("go_net_cmts30_text", "talks to all cable modems, up to 500 Mbit/s per tariff"), () => Sim.Test || Gear.Has(s, GameTypes.ICmts30), null, Cat.Cmts30, null);
            Equip(hc, "CMTS DOCSIS 3.1", Go.T("go_net_cmts31_text", "OFDM in the free channels, tariffs up to 1000 Mbit/s; then every neighbourhood's nodes"),
                  () => s.Cmts31, () => Act.Buy31(s), Cat.Cmts31,
                  () => Sim.Customers(s) >= Sim.Unlock31(s) ? "" : Go.T("go_unlock_at", "from {0} customers ({1} now)", Go.Num(Sim.Unlock31(s)), Go.Num(Sim.Customers(s))));
            Equip(hc, Go.T("go_net_vcmts", "vCMTS server"), Go.T("go_net_vcmts_text", "the CMTS as software for remote PHY nodes: {0} nodes and {1} modems per server, speaks DOCSIS 3.1", Cat.VcmtsPorts, Go.Num(Cat.CapVcmts)),
                  () => Gear.Has(s, GameTypes.IVcmts), null, Cat.Vcmts, null);
            Equip(hc, Go.T("go_net_olt", "OLT for XGS-PON"), Go.T("go_net_olt_text", "fiber to the home, 10 Gbit/s both ways per PON tree, TV over an own wavelength (RF overlay)"),
                  () => s.Olt, () => Act.BuyOlt(s), Cat.Olt,
                  () => !s.Cmts31 ? Go.T("go_net_needs31", "needs DOCSIS 3.1 first") : Sim.Customers(s) >= Sim.UnlockFiber(s) ? "" :
                        Go.T("go_unlock_at", "from {0} customers ({1} now)", Go.Num(Sim.UnlockFiber(s)), Go.Num(Sim.Customers(s))));
            Equip(hc, Go.T("go_net_voice", "Voice platform"), Go.T("go_net_voice_text", "softswitch and session border controller for the phone lines (VoIP over the cable modem or the ONT), interconnection with the other networks"),
                  () => s.Voice, () => Act.BuyVoice(s), Cat.Voice, null);
            if (s.Voice)
            {
                var r = Ui.Row(hc);
                Ui.Label(r, Go.T("go_net_numbers", "Phone numbers"), 420f, 21f, Ui.Sub);
                Ui.LiveLabel(r, () => Go.T("go_net_numbers_v", "{0} blocks of 1,000 | {1} free", s.NumberBlocks, Go.Num(Sim.FreeNumbers(s))), 360f, 21f);
                Ui.Btn(r, Go.T("go_net_numbers_buy", "+ block {0}", Go.Money(Cat.NumberBlock)), 260f, () => Act.BuyNumbers(s), () => Money.CanPay(Cat.NumberBlock));
                Ui.Label(r, Sc(Go.T("go_net_numbers_text", "{0} of new phone customers bring their number (porting: {1} to the old provider, two days)", Go.Pct(Cat.PortShare), Go.Money(Cat.PortFee))), -1f, 19f);
            }
            Equip(hc, Go.T("go_net_iptv", "IPTV platform"), Go.T("go_net_iptv_text", "encoders for the satellite channels, middleware and multicast: TV over the internet line, about 120 channels and replay, no cable channels needed - but it takes bandwidth on the nodes"),
                  () => s.Iptv, () => Act.BuyIptv(s), Cat.Iptv, () => s.Sat ? "" : Go.T("go_tv_needsat", "needs the satellite dishes"));
            if (!Sim.Test)
            {   // GO-CAP-1: how full the devices are - GO-HUBS-1: the headend's own (its neighbourhood and those without a hub)
                string hg = Places.HeadHood(s);
                CapRow(hc, "CMTS", () => Sim.UseCmts(s, hg), () => Sim.CapCmts(s, hg), Go.T("go_cap_cmts", "cable modems: {0} per CMTS 3.0, {1} per CMTS 3.1", Cat.CapCmts30, Cat.CapCmts31));
                CapRow(hc, Go.T("go_ports", "CMTS ports"), () => Sim.NodesIn(s, hg), () => Sim.Ports(s, hg), Go.T("go_ports_per", "nodes: {0} ports per CMTS - more nodes than ports share them and get slower", Cat.CmtsPorts));
                if (Sim.RphyPorts(s, hg) > 0) CapRow(hc, Go.T("go_rphy_ports", "vCMTS ports"), () => Sim.NodesIn(s, hg, true, true), () => Sim.RphyPorts(s, hg), Go.T("go_rphy_ports_per", "remote PHY nodes: {0} per vCMTS server", Cat.VcmtsPorts));
                if (s.Olt) CapRow(hc, "OLT", () => Sim.UseOlt(s, hg), () => Sim.CapOlt(s, hg), Go.T("go_cap_per", "{0} per device", Cat.CapOlt));
                if (s.Voice) CapRow(hc, Go.T("go_net_voice", "Voice platform"), () => Sim.PhoneCustomers(s), () => Sim.CapVoice(s), Go.T("go_cap_per", "{0} per device", Cat.CapVoice));
                if (s.Iptv) CapRow(hc, Go.T("go_net_iptv", "IPTV platform"), () => Sim.IptvCustomers(s), () => Sim.CapIptv(s), Go.T("go_cap_per", "{0} per device", Cat.CapIptv));
                if (s.Cas) CapRow(hc, Go.T("go_tv_cas", "CA system"), () => Sim.UsePayTv(s), () => Sim.CapCas(s), Go.T("go_cap_per", "{0} per device", Cat.CapCas));
            }
            if (s.Olt)
            {
                var r = Ui.Row(hc);
                Ui.Label(r, Go.T("go_net_splitter", "Splitter for new fiber buildings"), 420f, 21f, Ui.Sub);
                Ui.Btn(r, "1:32", 140f, () => s.Split64 = false, null, null, () => !s.Split64);
                Ui.Btn(r, "1:64", 140f, () => s.Split64 = true, null, null, () => s.Split64);
                Ui.Label(r, Sc(Go.T("go_net_splitter_text", "1:32 more speed per customer, 1:64 half the PON ports")), -1f, 19f);
            }

            // GO-POWER-1: what keeps an office running in a power cut
            Equip(hc, Go.T("go_net_ups", "UPS"), Go.T("go_net_ups_text", "battery cabinet: the office keeps running for an hour when the city's power fails"), () => Gear.Has(s, GameTypes.IUps), null, Cat.Ups, null);
            Equip(hc, Go.T("go_net_generator", "Generator"), Go.T("go_net_generator_text", "diesel generator: the office runs as long as a power cut lasts ({0} fuel per hour)", Go.Money(Cat.GeneratorFuel)), () => Gear.Has(s, GameTypes.IGenerator), null, Cat.Generator, null);

            // GO-CACHE-1
            Equip(hc, Go.T("go_net_cache", "Streaming cache"), Go.T("go_net_cache_text", "a streaming service's server in the headend: filled at night, it serves the evening from inside - a fifth of the traffic off the transit (two at most); from {0} internet customers", Go.Num(Cat.CacheMin)),
                  () => Deals.CacheActive(s), null, Cat.Cache, null);
            if (!Sim.Test) MonitorSection(s, list);   // GO-PNM-1, GO-POWER-1
            if (!Sim.Test) RentSection(s, list);      // GO-WHOLESALE-1
            if (!Sim.Test && Peers.Available) PeerSection(s, list);   // GO-WHOLESALE-2

            Ui.Head(list, Go.T("go_net_transit", "Bandwidth (IP transit)"),
                Go.T("go_net_transit_text", "You buy a fixed amount from a carrier. The bigger, the cheaper per Mbit/s. Billing as with the 95th percentile: the busiest hour of the day does not count; above the booked amount it costs 1.5 times as much. Beyond twice the amount the line is full and everybody slows down."));
            var tc = Ui.Column(list, 6f, true);
            var tr = Ui.Row(tc);
            Ui.Label(tr, Go.T("go_net_booked", "Booked"), 300f, 21f, Ui.Sub);
            Ui.Btn(tr, "-", 60f, () => Act.Transit(s, -1), () => s.TransitTier > 0);
            Ui.LiveLabel(tr, () => Go.Bw(Cat.TransitMbit[s.TransitTier]) + "  " + Sc(PerDay(Cat.TransitDay(s.TransitTier))), 420f, 22f, null, FontStyles.Normal, TextAlignmentOptions.Center);
            Ui.Btn(tr, "+", 60f, () => Act.Transit(s, 1), () => s.TransitTier < Cat.TransitMbit.Length - 1);
            Ui.Pair(tc, Go.T("go_net_need", "Need"), () =>
            {
                float max = Max(s.UpLoad), now = s.UpLoad[Hour], commit = Cat.TransitMbit[s.TransitTier];
                string peak = max > commit * 2f ? Bc(Go.Bw(max)) : max > commit ? Wc(Go.Bw(max)) : Gc(Go.Bw(max));
                return Go.T("go_net_need_v", "now {0} | peak today {1}", Go.Bw(now), peak);
            }, 300f);
            var tb = Ui.Row(tc, 36f);
            Ui.Label(tb, "", 300f);
            Ui.Bar(tb, 600f, 20f, () => Max(s.UpLoad) / Mathf.Max(1f, Cat.TransitMbit[s.TransitTier]));
            var pr = Ui.Row(tc);
            Ui.Label(pr, Go.T("go_net_peering", "Peering ports"), 300f, 21f, Ui.Sub);
            Ui.LiveLabel(pr, () => s.PeeringPorts.ToString(), 80f, 22f, null, FontStyles.Normal, TextAlignmentOptions.Center);
            Ui.Btn(pr, Go.T("go_net_peering_add", "+ port {0}", Go.Money(Cat.PeeringPort)), 260f, () => Act.Peering(s, 1), () => Money.CanPay(Cat.PeeringPort));
            Ui.Btn(pr, "-", 60f, () => Act.Peering(s, -1), () => s.PeeringPorts > 0);
            Ui.Label(pr, Sc(Go.T("go_net_peering_text", "at an internet exchange: takes 35 % of the traffic, up to 10 Gbit/s per port, {0}", PerDay(Cat.PeeringDay))), -1f, 19f);

            Ui.Head(list, Go.T("go_net_spectrum", "Spectrum of a node"));
            Ui.LivePara(list, () =>
            {
                var (d30, ofdm) = Sim.Spectrum(s, true);
                return Go.T("go_net_spectrum_v", "96 channels of 8 MHz: TV {0} | DOCSIS 3.0 {1} ({2}) | DOCSIS 3.1 {3} OFDM blocks ({4}, only for 3.1 modems). More TV transponders leave less room for the internet.",
                            s.Transponders, d30, Go.Bw(d30 * Cat.SlotMbit), ofdm, Go.Bw(ofdm * Cat.OfdmMbit));
            }, 20f);

            if (!Sim.Test) PlacesSection(s, list);   // GO-HUBS-1, GO-CENTERS-1
            Ui.Head(list, Go.T("go_net_hoods", "Neighbourhoods"),
                Go.T("go_net_hoods_text", "A node serves up to 500 units; all customers on it share its capacity. More nodes (a node split) and DOCSIS 3.1 make room. The bars show today's load per hour."));
            var hoods = new List<Hood>(s.Hoods.Values);
            hoods.Sort((a, b) => string.Compare(City.HoodName(a.Id), City.HoodName(b.Id), System.StringComparison.CurrentCulture));
            if (hoods.Count == 0) Ui.Para(list, Sc(Go.T("go_net_nohoods", "No neighbourhood connected yet - start in the Buildings tab.")), 21f);
            foreach (var h in hoods) HoodCard(s, list, h);
        }

        /// <summary>GO-HUBS-1, GO-CENTERS-1, GO-STAFF-1 (0.8): per neighbourhood its office (the headend, a hub on the feeder fibre
        /// and the ring) and its customer centers, whether new customers may come, and who serves how many there.</summary>
        static void PlacesSection(IspState s, Transform list)
        {
            int left = Places.GraceLeft(s);
            Ui.Head(list, Go.T("go_places_title", "Offices and customer centers"),
                Go.T("go_places_text", "Every neighbourhood needs an office of yours and a customer center: the office (\"Cable provider\" in BizMan) holds the staff and the technology - the headend, or a hub with its own CMTS on a feeder fibre from the headend; the customer center (\"Cable customer center\" in a shop) the customer service and the devices. New customers and new buildings only where both stand; elsewhere customers cancel little by little. A ring - a second way for the fibre - keeps a hub running when an excavator cuts its feeder."));
            if (left > 0) Ui.Para(list, Wc(Go.T("go_places_grace", "Your provider is from before 0.8: these rules apply in {0} days.", left)), 21f);
            var hoods = new List<string>(City.Hoods);
            hoods.Sort((a, b) => string.Compare(City.HoodName(a), City.HoodName(b), System.StringComparison.CurrentCulture));
            string hh = Places.HeadHood(s);
            foreach (var id in hoods)
            {
                string hood = id;
                var card = Ui.Column(list, 4f, true);
                var r = Ui.Row(card, 40f);
                Ui.Label(r, City.HoodName(hood) + (hood == hh ? "  " + Sc("(" + Go.T("go_headend", "headend") + ")") : ""), 420f, 22f, Color.white, FontStyles.Bold);
                Ui.LiveLabel(r, () =>
                {
                    string why = Places.LacksText(s, hood);
                    if (why.Length == 0) return Gc(Go.T("go_places_ok", "looked after - new customers can come"));
                    return Places.RulesOn(s) ? Bc(why) : Wc(why);
                }, -1f, 20f);
                var r2 = Ui.Row(card, 34f);
                Ui.LiveLabel(r2, () =>
                {
                    var offices = Places.OfficesIn(s, hood);
                    var centers = Places.CentersIn(s, hood);
                    string o = offices.Count == 0 ? Wc(Go.T("go_places_nooffice", "none - rent an office, found \"Cable provider\" in BizMan")) : string.Join(", ", offices.ConvertAll(City.Name));
                    string c = centers.Count == 0 ? Wc(Go.T("go_places_nocenter", "none - rent a shop, found \"Cable customer center\" in BizMan")) : string.Join(", ", centers.ConvertAll(City.Name));
                    string power = offices.Count == 0 ? "" : " | " + (Places.Units(s, hood, GameTypes.IGenerator, "generator") > 0 ? Gc(Go.T("go_places_gen", "generator"))
                                 : Places.Units(s, hood, GameTypes.IUps, "ups") > 0 ? Wc(Go.T("go_places_ups", "UPS only")) : Sc(Go.T("go_places_nopower", "no backup power")));
                    return Go.T("go_places_where", "Office: {0} | Customer center: {1}", o, c) + power;
                }, -1f, 19f);
                if (hood != hh)
                {   // the feeder fibre and the ring
                    var r3 = Ui.Row(card, 46f);
                    Ui.Btn(r3, "", 420f, () => Act.BuildLink(s, hood), () => Act.CanLink(s, hood) && Money.CanPay(Act.LinkCost(s, hood)), () =>
                    {
                        s.Hoods.TryGetValue(hood, out var h);
                        if (h != null && h.Link) return Go.T("go_places_link_ok", "Feeder fibre: built");
                        if (h != null && h.LinkHours > 0) return Go.T("go_places_link_in", "Feeder fibre in {0} h", h.LinkHours);
                        if (!Places.HasOffice(s, hood)) return Go.T("go_places_link_nooffice", "Feeder fibre: an office first");
                        return Go.T("go_places_link_build", "Build feeder fibre {0} ({1} m)", Go.Money(Act.LinkCost(s, hood)), Go.Num(Places.Metres(s.HeadendKey, Places.Hub(s, hood))));
                    }, () => s.Hoods.TryGetValue(hood, out var h) && h.Link, 18f);
                    Ui.Btn(r3, "", 420f, () => Act.BuildRing(s, hood), () => Act.CanRing(s, hood) && Money.CanPay(Act.RingCost(s, hood)), () =>
                    {
                        s.Hoods.TryGetValue(hood, out var h);
                        if (h != null && h.Ring) return Go.T("go_places_ring_ok", "Ring: closed");
                        if (h != null && h.RingHours > 0) return Go.T("go_places_ring_in", "Ring in {0} h", h.RingHours);
                        if (h == null || !h.Link) return Go.T("go_places_ring_nolink", "Ring: the feeder first");
                        return Go.T("go_places_ring_build", "Close the ring {0} (via {1})", Go.Money(Act.RingCost(s, hood)), City.Name(Places.RingPartner(s, hood)));
                    }, () => s.Hoods.TryGetValue(hood, out var h) && h.Ring, 18f);
                    Ui.Label(r3, Sc(Go.T("go_places_fibre_text", "{0} plus {1} per metre", Go.Money(Cat.LinkBase), Go.Money(Cat.LinkPerM))), -1f, 18f);
                }
                if (hood == hh || Places.Group(s, hood) == hood)
                {   // GO-STAFF-1: who serves how many in this group
                    var r4 = Ui.Row(card, 34f);
                    Ui.LiveLabel(r4, () => StaffLine(s, Places.Group(s, hood)), -1f, 19f);
                }
                else if (s.Hoods.ContainsKey(hood))
                    Ui.Para(card, Sc(Go.T("go_places_onhead", "Its network hangs on the headend (no hub of its own) - the headend's staff and devices serve it.")), 18f);
            }
        }

        /// <summary>GO-PNM-1 and GO-POWER-1: the network monitoring (switch, state, licence) and the power cuts.</summary>
        static void MonitorSection(IspState s, Transform list)
        {
            Ui.Head(list, Go.T("go_pnm_title", "Network monitoring"),
                Go.T("go_pnm_text", "Software in the control room reads the measurements of the CMTS and the modems all the time and finds faults in the network before customers call - vendors name 10 to 40 % fewer trips for the technicians. It works while a network master is on duty and a control room workplace stands in one of your offices: network faults are known and diagnosed at once, half as many customers call about them, worn amplifiers and noise are replaced before they fail, and the technical support fixes more devices remotely."));
            var pc = Ui.Column(list, 6f, true);
            var pr = Ui.Row(pc);
            Ui.Btn(pr, "", 360f, () => s.Pnm = !s.Pnm, null, () => Go.T("go_pnm_switch", "Monitoring: {0}", s.Pnm ? Go.T("go_on", "on") : Go.T("go_off", "off")), () => s.Pnm, 19f);
            Ui.LiveLabel(pr, () =>
            {
                string cost = Go.T("go_pnm_cost", "{0} a day ({1} plus {2} per modem)", Go.Money(Grid.PnmDay(s)), Go.Money(Cat.PnmBase), Go.Money(Cat.PnmPerModem));
                if (!s.Pnm) return Sc(cost);
                string why = Grid.PnmWhy(s);
                return (why.Length == 0 ? Gc(Go.T("go_pnm_works", "works")) : Wc(Go.T("go_pnm_idle", "idle: {0}", why))) + "  " + Sc(cost);
            }, -1f, 19f);

            Ui.Head(list, Go.T("go_power_title", "Power cuts"),
                Go.T("go_power_text", "Now and then the city's power fails in a neighbourhood, for one to five hours. Half of its homes are dark themselves; the others need your network. Batteries in the nodes' power supplies bridge two hours (per neighbourhood, Network tab). An office goes dark without backup power: a UPS keeps it running for an hour, a generator as long as the cut lasts. A dark hub takes its neighbourhood down, the dark headend TV, the internet line and the phones of the whole city. Afterwards some power supplies are broken."));
            Ui.LivePara(list, () => { string now = Grid.CutsNow(s); return now.Length == 0 ? Sc(Go.T("go_power_none", "No power cut at the moment.")) : Wc(Go.T("go_power_now_text", "Power cut now: {0}", now)); }, 20f);
        }

        /// <summary>GO-WHOLESALE-1: renting lines to another provider - the offer, the price, the lines and the money.</summary>
        static void RentSection(IspState s, Transform list)
        {
            Ui.Head(list, Go.T("go_ws_title", "Rent the network"),
                Go.T("go_ws_text", "{0} sells internet without a cable network of its own and rents lines on yours - as big providers do with each other. Its lines grow where your network is looked after (towards 8 % of the homes passed), fewer at a dear price or on slow evenings. They pay per line and day, load your nodes and the CMTS like customers of yours, take a little of your own growth, and an outage costs a penalty.", Cat.WsName));
            var c = Ui.Column(list, 6f, true);
            var r = Ui.Row(c);
            Ui.Btn(r, "", 360f, () => { if (s.Wholesale) Deals.Stop(s); else Deals.Accept(s); }, () => s.Wholesale || s.WsOffered >= 0,
                   () => s.Wholesale ? Go.T("go_ws_stop", "End the contract") : s.WsOffered >= 0 ? Go.T("go_ws_accept", "Accept the offer") : Go.T("go_ws_wait", "No offer yet (from {0} customers)", Go.Num(Cat.WsMinCustomers)), () => s.Wholesale, 19f);
            Ui.Btn(r, "-", 60f, () => Deals.Price(s, -0.05f));
            Ui.LiveLabel(r, () => Go.T("go_ws_price", "{0} per line and day", Go.Money(s.WsPrice)), 300f, 21f, null, FontStyles.Normal, TextAlignmentOptions.Center);
            Ui.Btn(r, "+", 60f, () => Deals.Price(s, 0.05f));
            Ui.LivePara(c, () =>
            {
                if (!s.Wholesale) return "";
                var parts = new List<string>();
                foreach (var kv in s.WsLines) parts.Add(City.HoodName(kv.Key) + " " + Go.Num(kv.Value));
                return Go.T("go_ws_now", "{0} lines - {1} a day | {2}", Go.Num(Deals.WsTotal(s)), Go.Money(Deals.WsTotal(s) * s.WsPrice), parts.Count == 0 ? "-" : string.Join(", ", parts));
            }, 20f);
        }

        /// <summary>GO-WHOLESALE-2: lines between players - your network on offer to them, and theirs to rent.</summary>
        static void PeerSection(IspState s, Transform list)
        {
            Ui.Head(list, Go.T("go_peer_title", "Lines for other players"),
                Go.T("go_peer_text", "Other players with a cable provider of their own rent lines in your neighbourhoods (those your network serves and looks after) and pay you per line and day; their lines load your nodes like your customers. The other way round you win customers where another player has a network and you have none: you pay their price per line and day, your customers there pay your tariffs. Their lines grow towards 5 % of the homes passed - more with a good reputation, fewer where the network fails."));
            var c = Ui.Column(list, 6f, true);
            var r = Ui.Row(c);
            Ui.Btn(r, "", 360f, () => s.OfferLines = !s.OfferLines, null,
                   () => s.OfferLines ? Go.T("go_peer_offer_off", "Withdraw the offer") : Go.T("go_peer_offer_on", "Offer my network"), () => s.OfferLines, 19f);
            Ui.Btn(r, "-", 60f, () => s.OfferPrice = Mathf.Max(0.2f, Mathf.Round((s.OfferPrice - 0.05f) * 100f) / 100f));
            Ui.LiveLabel(r, () => Go.T("go_ws_price", "{0} per line and day", Go.Money(s.OfferPrice)), 300f, 21f, null, FontStyles.Normal, TextAlignmentOptions.Center);
            Ui.Btn(r, "+", 60f, () => s.OfferPrice = Mathf.Min(5f, Mathf.Round((s.OfferPrice + 0.05f) * 100f) / 100f));
            Ui.LivePara(c, () =>
            {
                if (!s.OfferLines) return Sc(Go.T("go_peer_not_offered", "Not on offer."));
                var hoods = Peers.MyHoods(s);
                string where = hoods.Count == 0 ? Wc(Go.T("go_peer_no_hoods", "no neighbourhood yet (a network with an office and a customer center there)"))
                                                : string.Join(", ", hoods.ConvertAll(h => h.Name));
                return Go.T("go_peer_offered", "On offer in: {0}", where) + "\n" + Peers.TenantsText(s);
            }, 20f);
            var offers = Peers.Others();
            Ui.Para(c, Go.T("go_peer_others", "Other players' networks"), 22f, Color.white, FontStyles.Bold);
            if (offers.Count == 0) Ui.Para(c, Sc(Go.T("go_peer_none", "No other player offers lines at the moment.")), 20f);
            foreach (var o in offers)
            {
                var offer = o;
                var row = Ui.Row(c, 48f);
                Ui.LiveLabel(row, () =>
                {
                    float o2 = 0f; int homes = 0;
                    foreach (var h in offer.Hoods) { o2 += h.Out * h.Homes; homes += h.Homes; }
                    string q = Go.T("go_peer_quality", "outages {0}", Go.Pct(homes > 0 ? o2 / homes : 0f));
                    var rent = Peers.RentOf(s, offer.Owner);
                    string mine = rent == null ? "" : " | " + Gc(Go.T("go_peer_rented", "{0} lines of yours, {1} a day", Go.Num(Peers.Lines(rent)), Go.Money(Peers.Lines(rent) * rent.Price)));
                    return Go.T("go_peer_offer_line", "{0}: {1} | {2} per line and day | {3}", offer.Company, string.Join(", ", offer.Hoods.ConvertAll(h => h.Name)), Go.Money(offer.Price), q) + mine;
                }, -1f, 20f);
                Ui.Btn(row, "", 220f, () => { if (Peers.RentOf(s, offer.Owner) != null) Peers.Stop(s, offer.Owner); else Peers.Rent(s, offer); }, null,
                       () => Peers.RentOf(s, offer.Owner) != null ? Go.T("go_peer_cancel", "Cancel") : Go.T("go_peer_rent", "Rent lines"), () => Peers.RentOf(s, offer.Owner) != null, 19f);
            }
            foreach (var rent in s.Rents)
                if (offers.TrueForAll(o => o.Owner != rent.Owner))
                {
                    var rr = rent;
                    var row = Ui.Row(c, 48f);
                    Ui.LiveLabel(row, () => Wc(Go.T("go_peer_paused", "{0}: the offer is gone - your {1} lines wait (the contract ends after 7 days)", rr.Company, Go.Num(Peers.Lines(rr)))), -1f, 20f);
                    Ui.Btn(row, Go.T("go_peer_cancel", "Cancel"), 220f, () => Peers.Stop(s, rr.Owner));
                }
        }

        /// <summary>GO-STAFF-1: one group's people against its customers, and its network masters' span.</summary>
        static string StaffLine(IspState s, string g)
        {
            int all = Places.CustomersOf(s, g, false), coax = Places.CustomersOf(s, g, true);
            string Part(byte role, int need)
            {
                int cap = Places.Serves(s, g, role);
                string v = Act.RoleName(role) + " " + Places.People(s, g, role) + " (" + Go.T("go_staff_serves", "for {0}", Go.Num(cap)) + ")";
                return need >= cap ? Bc(v) : need >= cap * 0.9f ? Wc(v) : v;
            }
            var (field, led) = Places.Span(s, g);
            string master = Act.RoleName(Cat.RoleMaster) + " " + Places.People(s, g, Cat.RoleMaster) + " (" + Go.T("go_staff_leads", "lead {0} of {1} in the field", Mathf.Min(field, led), field) + ")";
            if (field > led) master = Wc(master);
            return Go.T("go_staff_line", "Customers {0} | {1} | {2} | {3} | {4}", Go.Num(all), Part(Cat.RoleTech, all), Part(Cat.RoleSwitch, coax), Part(Cat.RoleService, all), master);
        }

        /// <summary>The stores the game types were put into (GameTypes), for a sentence.</summary>
        static string Stores(IReadOnlyList<string> list) => list.Count == 0 ? Go.T("go_gear_nostore", "a store of the city") : string.Join(", ", list.Take(3));

        /// <summary>GO-CAP-1: "used of capacity" for one kind of device, red when full.</summary>
        static void CapRow(Transform parent, string name, System.Func<int> use, System.Func<int> cap, string per)
        {
            var r = Ui.Row(parent);
            Ui.Label(r, Go.T("go_cap_load", "Load {0}", name), 420f, 21f, Ui.Sub);
            Ui.LiveLabel(r, () =>
            {
                int u = use(), c = cap();
                string v = Go.T("go_cap_v", "{0} of {1}", Go.Num(u), Go.Num(c));
                return c <= 0 || u >= c ? Bc(v) : u >= c * 0.9f ? Wc(v) : v;
            }, 260f, 21f);
            Ui.Label(r, Sc(per), -1f, 19f);
        }

        static void Equip(Transform parent, string name, string text, System.Func<bool> have, UnityEngine.Events.UnityAction? buy, float cost, System.Func<string>? locked)
        {
            var r = Ui.Row(parent, 56f);
            Ui.Label(r, name, 300f, 22f, Color.white, FontStyles.Bold);
            Ui.Label(r, Sc(text), -1f, 19f);
            if (!Sim.Test)
            {   // GAME-TYPES-1: furniture from an office furniture store, set up in the headend
                Ui.LiveLabel(r, () => have() ? Gc(Go.T("go_gear_stands", "stands in the headend")) : Wc(Go.T("go_gear_buy", "buy it ({0}), set it up in the headend", Go.Money(cost))), 360f, 19f, null, FontStyles.Bold, TextAlignmentOptions.Center);
                return;
            }
            if (buy == null) { Ui.Label(r, Gc(Go.T("go_running", "running")), 300f, 21f, null, FontStyles.Bold, TextAlignmentOptions.Center); return; }
            Ui.Btn(r, "", 300f, buy, () => !have() && (locked == null || locked().Length == 0) && Money.CanPay(cost),
                   () => have() ? Go.T("go_running", "running") : locked != null && locked().Length > 0 ? locked() : Go.T("go_buy_for", "Buy - {0}", Go.Money(cost)),
                   () => have(), 19f);
        }

        static void HoodCard(IspState s, Transform list, Hood h)
        {
            var card = Ui.Column(list, 6f, true);
            var r = Ui.Row(card, 44f);
            Ui.Label(r, City.HoodName(h.Id), 300f, 24f, Color.white, FontStyles.Bold);
            Ui.LiveLabel(r, () =>
            {
                string nodes = Go.T("go_net_nodes", "{0} nodes", h.Nodes) + (h.NodesBuilding > 0 ? " " + Wc(Go.T("go_net_building", "(+{0} in {1} h)", h.NodesBuilding, h.BuildHours)) : "");
                return nodes + " | " + Go.T("go_net_units", "{0} of {1} units", Go.Num(Sim.HomesPassed(s, h.Id)), Go.Num((h.Nodes + h.NodesBuilding) * Sim.PerNode(h)))
                     + " | " + Go.T("go_net_cust", "{0} coax customers", Go.Num(Sim.HoodCoaxCustomers(s, h.Id)))
                     + (h.D31 ? " | " + Gc("DOCSIS 3.1") : h.D31Hours > 0 ? " | " + Wc(Go.T("go_net_31_in", "3.1 in {0} h", h.D31Hours)) : "");
            }, -1f, 20f);
            if (!Sim.Test && h.Id != Places.HeadHood(s) && Places.Group(s, h.Id) == h.Id)
            {   // GO-HUBS-1: the hub's own CMTS and ports
                var hr = Ui.Row(card, 34f);
                Ui.LiveLabel(hr, () => Sc(Go.T("go_net_hubload", "Hub {0}: CMTS {1} of {2} modems | ports {3} of {4}", City.Name(Places.Hub(s, h.Id)),
                    Go.Num(Sim.UseCmts(s, h.Id)), Go.Num(Sim.CapCmts(s, h.Id)), Sim.NodesIn(s, h.Id), Sim.Ports(s, h.Id))), -1f, 19f);
            }
            var r2 = Ui.Row(card, 64f);
            Ui.Chart(r2, 360f, 56f, () => h.Util, () => Hour);
            Ui.LiveLabel(r2, () => Go.T("go_net_peak", "peak today {0}", Load(Max(h.Util))) + "\n" + Sc(Go.T("go_net_cap", "{0} per node", Go.Bw(Sim.NodeMbit(s, h.D31 && Sim.Cmts31At(s, h.Id)) * Sim.PortShare(s, Places.Group(s, h.Id))))), 260f, 19f);
            Ui.Btn(r2, "", 240f, () => Act.AddNode(s, h.Id), () => h.Nodes + h.NodesBuilding > 0 && Sim.FreePortsFor(s, h.Id) > 0,
                   () => Go.T("go_net_addnode", "+ node {0}", Go.Money(Cat.MoreNode + (h.D31 ? Cat.Node31 : 0f))), null, 19f);
            Ui.Btn(r2, "", 160f, () => Act.RemoveNode(s, h.Id), () => Act.CanRemoveNode(s, h),
                   () => Go.T("go_net_removenode", "- node"), null, 19f);   // GO-NODES-1
            Ui.Btn(r2, "", 240f, () => Act.Upgrade31(s, h.Id), () => Sim.Cmts31At(s, h.Id) && !h.D31 && h.D31Hours <= 0 && Money.CanPay(Act.Upgrade31Cost(s, h.Id)),
                   () => h.D31 ? "DOCSIS 3.1 " + Go.T("go_running", "running") : h.D31Hours > 0 ? Go.T("go_net_31_in", "3.1 in {0} h", h.D31Hours) : "DOCSIS 3.1 " + Go.Money(Act.Upgrade31Cost(s, h.Id)), () => h.D31, 19f);
            Ui.Btn(r2, "", 280f, () =>
            {
                int n = Act.SwapModems(s, h.Id);
                if (n > 0) Sim.Event(s, Go.T("go_event_swapped", "{0} modems in {1} swapped for DOCSIS 3.1.", n, City.HoodName(h.Id)));
            }, () => h.D31 && Act.RentedOld(s, h.Id) > 0 && s.Stock.M31 > 0,
                   () => Go.T("go_net_swap", "Swap 3.0 modems ({0})", Go.Num(Act.RentedOld(s, h.Id))), null, 19f);
            if (h.Nodes + h.NodesBuilding > 0)
            {   // GO-UP-1, GO-DEEP-1, GO-RPHY-1: the upload and the rebuilds of the neighbourhood's network
                var ru = Ui.Row(card, 46f);
                Ui.LiveLabel(ru, () => Go.T("go_net_up", "Upload: peak today {0} | {1} per node", Load(Max(h.UpUtil ?? new float[24])),
                    Go.Bw((int)(h.HighSplit ? Cat.UpHigh : h.D31 ? Cat.UpLow31 : Cat.UpLow30))), 360f, 18f);
                Ui.Btn(ru, "", 300f, () => Act.HighSplit(s, h.Id), () => Act.CanHighSplit(s, h) && Money.CanPay(Act.HighSplitCost(s, h.Id)), () =>
                    h.HighSplit ? Go.T("go_net_hs_on", "High split running")
                    : h.HsHouses ? Go.T("go_net_hs_houses", "House amplifiers: {0} left", Sim.OpenTickets(s, Cat.TtHouseAmp, h.Id))
                    : h.HsHours > 0 ? Go.T("go_net_hs_in", "High split in {0} h", h.HsHours)
                    : !h.D31 ? Go.T("go_net_hs_need31", "High split: DOCSIS 3.1 first")
                    : Go.T("go_net_hs_buy", "High split {0}", Go.Money(Act.HighSplitCost(s, h.Id))), () => h.HighSplit, 17f);
                Ui.Btn(ru, "", 280f, () => Act.FiberDeep(s, h.Id), () => Act.CanDeep(s, h) && Money.CanPay(Act.DeepCost(s, h)), () =>
                    h.Deep ? Go.T("go_net_deep_on", "Fiber deep running")
                    : h.DeepHours > 0 ? Go.T("go_net_deep_in", "Fiber deep in {0} h", h.DeepHours)
                    : !h.D31 ? Go.T("go_net_deep_need31", "Fiber deep: DOCSIS 3.1 first")
                    : Sim.FreePortsFor(s, h.Id) < Act.DeepNodesFor(s, h) - h.Nodes - h.NodesBuilding ? Go.T("go_net_deep_ports", "Fiber deep: {0} ports short", Act.DeepNodesFor(s, h) - h.Nodes - h.NodesBuilding - Sim.FreePortsFor(s, h.Id))
                    : Go.T("go_net_deep_buy", "Fiber deep {0} ({1} nodes)", Go.Money(Act.DeepCost(s, h)), Act.DeepNodesFor(s, h)), () => h.Deep, 17f);
                Ui.Btn(ru, "", 280f, () => Act.Rphy(s, h.Id), () => Act.CanRphy(s, h) && Money.CanPay(Act.RphyCost(s, h)), () =>
                    h.Rphy ? Go.T("go_net_rphy_on", "Remote PHY running")
                    : h.RphyHours > 0 ? Go.T("go_net_rphy_in", "Remote PHY in {0} h", h.RphyHours)
                    : Sim.FreeRphyPorts(s, Places.Group(s, h.Id)) < h.Nodes + h.NodesBuilding ? Go.T("go_net_rphy_novcmts", "Remote PHY: a vCMTS server first")
                    : Go.T("go_net_rphy_buy", "Remote PHY {0}", Go.Money(Act.RphyCost(s, h))), () => h.Rphy, 17f);
            }
            if (!Sim.Test && h.Nodes + h.NodesBuilding > 0)
            {   // GO-POWER-1
                var rb = Ui.Row(card, 42f);
                Ui.Btn(rb, "", 420f, () => Grid.BuyBatteries(s, h.Id), () => !h.Batteries && Money.CanPay(Grid.BatteryCost(s, h.Id)),
                       () => h.Batteries ? Go.T("go_net_batteries_ok", "Batteries in the power supplies: yes") : Go.T("go_net_batteries_buy", "Batteries for the nodes {0}", Go.Money(Grid.BatteryCost(s, h.Id))), () => h.Batteries, 18f);
                Ui.Label(rb, Sc(Go.T("go_net_batteries_text", "they bridge {0} hours of a power cut ({1} per node and day)", Cat.BatteryHours, Go.Money(Cat.BatteryDay))), -1f, 18f);
            }
            if (h.FiberIn)
            {
                var r3 = Ui.Row(card, 64f);
                Ui.Chart(r3, 360f, 56f, () => h.FiberUtil, () => Hour);
                Ui.LiveLabel(r3, () => Go.T("go_net_fiber", "Fiber: {0} PON ports | {1} customers | peak today {2}", h.PonPorts, Go.Num(Sim.HoodFiberCustomers(s, h.Id)), Load(Max(h.FiberUtil))), -1f, 19f);
            }
        }

        // ── buildings ────────────────────────────────────────────────────────────────────────────────────────

        static bool KindMatch(House b) =>
            _kindFilter == 0 || (_kindFilter == 1 && (b.Kind == Cat.KindHome || b.Kind == Cat.KindVilla)) || (_kindFilter == 2 && b.Kind == Cat.KindRetail)
            || (_kindFilter == 3 && b.Kind == Cat.KindOffice) || (_kindFilter == 4 && b.Kind == Cat.KindWarehouse);

        static void Buildings(IspState s, Transform list)
        {
            Ui.Head(list, Go.T("go_bld_title", "Buildings"),
                Go.T("go_bld_text", "Connecting a building takes the owner's permission and a house transfer point - the house network is there already. The more units, the more customers. Missing nodes are built with it."));
            var f = Ui.Row(list, 52f);
            string hoodName = _hoodFilter < 0 || _hoodFilter >= City.Hoods.Count ? Go.T("go_bld_allhoods", "All neighbourhoods") : City.HoodName(City.Hoods[_hoodFilter]);
            Ui.Btn(f, "<", 52f, () => { _hoodFilter = _hoodFilter <= -1 ? City.Hoods.Count - 1 : _hoodFilter - 1; _page = 0; });
            Ui.Label(f, hoodName, 300f, 21f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Ui.Btn(f, ">", 52f, () => { _hoodFilter = _hoodFilter >= City.Hoods.Count - 1 ? -1 : _hoodFilter + 1; _page = 0; });
            string[] kinds = { Go.T("go_bld_allkinds", "All kinds"), Go.T("go_kind_home", "Residential"), Go.T("go_kind_retail", "Shops + flats"), Go.T("go_kind_office", "Offices"), Go.T("go_kind_warehouse", "Warehouse") };
            Ui.Btn(f, kinds[_kindFilter], 240f, () => { _kindFilter = (_kindFilter + 1) % kinds.Length; _page = 0; });
            string[] states = { Go.T("go_bld_allstates", "Connected or not"), Go.T("go_bld_connected", "Connected"), Go.T("go_bld_open", "Not connected") };
            Ui.Btn(f, states[_stateFilter], 260f, () => { _stateFilter = (_stateFilter + 1) % states.Length; _page = 0; });
            Ui.Field(f, _search, -1f, v => { _search = v.Trim(); _page = 0; }, 40, Go.T("go_bld_search", "Search a street ..."));

            var all = new List<House>();
            foreach (var b in City.All)
            {
                if (!b.Connectable || b.Hood.Length == 0) continue;
                if (_hoodFilter >= 0 && _hoodFilter < City.Hoods.Count && b.Hood != City.Hoods[_hoodFilter]) continue;
                if (!KindMatch(b)) continue;
                bool conn = s.Sites.ContainsKey(b.Key);
                if (_stateFilter == 1 && !conn || _stateFilter == 2 && conn) continue;
                if (_search.Length > 0 && City.Name(b.Key).IndexOf(_search, System.StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                all.Add(b);
            }
            all.Sort((a, b) =>
            {
                bool ca = s.Sites.ContainsKey(a.Key), cb = s.Sites.ContainsKey(b.Key);
                if (ca != cb) return ca ? -1 : 1;
                return b.Units.CompareTo(a.Units);
            });
            int pages = Mathf.Max(1, Mathf.CeilToInt(all.Count / (float)PerPage));
            _page = Mathf.Clamp(_page, 0, pages - 1);
            var pr = Ui.Row(list, 48f);
            Ui.Btn(pr, "<", 52f, () => _page--, () => _page > 0);
            Ui.Label(pr, Go.T("go_bld_page", "Page {0} of {1} | {2} buildings", _page + 1, pages, Go.Num(all.Count)), 420f, 21f, Ui.Sub, FontStyles.Normal, TextAlignmentOptions.Center);
            Ui.Btn(pr, ">", 52f, () => _page++, () => _page < pages - 1);

            // GO-LAYOUT-3 (user 2026-11-10: "Text in Rot abgeschnitten"): the kind and the state wider - "Glasfaser | Koax |
            // Ausfälle" and "Skyline Cable bis Tag 126" were cut at 210
            var head = Ui.Row(list, 36f, 10f);
            Ui.Label(head, Go.T("go_bld_address", "Address"), BldAddr, 19f, Ui.Sub);
            Ui.Label(head, Go.T("go_bld_kind", "Kind"), BldKind, 19f, Ui.Sub);
            Ui.Label(head, Go.T("go_bld_units", "Units"), BldUnits, 19f, Ui.Sub);
            Ui.Label(head, Go.T("go_bld_state", "State"), BldState, 19f, Ui.Sub);
            Ui.Label(head, Go.T("go_bld_customers", "Customers"), BldCust, 19f, Ui.Sub);
            for (int i = _page * PerPage; i < Mathf.Min(all.Count, (_page + 1) * PerPage); i++) BuildingRow(s, list, all[i]);
        }

        const float BldAddr = 330f, BldKind = 300f, BldUnits = 270f, BldState = 400f, BldCust = 140f;

        static void BuildingRow(IspState s, Transform list, House b)
        {
            string key = b.Key;
            var r = Ui.Row(list, 50f, 10f);
            // GO-LAYOUT-1/3: address to customers take 1480, the buttons beside them only when the tab has the room, else they
            // stand on a second line
            const float InfoWidth = BldAddr + BldKind + BldUnits + BldState + BldCust + 4 * 10f;
            bool olt = Sim.AnyOlt(s);
            bool bulk = !Sim.Test && b.Homes >= Cat.BulkMinHomes;
            float buttons = 250f + 230f + 130f + 2 * 10f + (olt ? 270f + 190f + 2 * 10f : 0f) + (bulk ? 270f + 10f : 0f);
            bool twoLines = InfoWidth + 10f + buttons > App.ListWidth;
            string name = City.Name(key);
            if (City.BusinessName(key, out var biz)) name += "  " + Sc(biz);
            Ui.Label(r, name, BldAddr, 20f);
            Ui.Label(r, KindName(b.Kind) + "  " + Sc(City.HoodName(b.Hood)), BldKind, 18f);
            Ui.Label(r, Units(b.Homes, b.Biz), BldUnits, 19f);
            Ui.LiveLabel(r, () =>
            {
                if (!s.Sites.TryGetValue(key, out var site))
                {
                    int locked = Rival.LockedUntil(s, key, Sim.Day());
                    return locked >= 0 ? Bc(Go.T("go_bld_locked", "{0} until day {1}", Rival.Name, locked)) : Sc(Go.T("go_bld_none", "not connected"));
                }
                if (site.Lost) return Bc(Go.T("go_bld_lost", "{1} until day {0}", site.LostUntil, Rival.Name));
                var parts = new List<string>();
                if (site.Fiber) parts.Add(Gc(Go.T("go_bld_fiber", "fiber")));
                else if (site.FiberHours > 0) parts.Add(Wc(Go.T("go_bld_fiber_in", "fiber in {0} h", site.FiberHours)));
                if (site.Coax && !site.CoaxOff)
                {
                    float age = Sim.AgeYears(s, site.BuiltDay, Sim.Day());
                    string coax = Go.T("go_bld_coax", "coax");
                    if (age >= 1f) coax += " " + Go.T("go_bld_age", "{0} y.", Go.Num(age, 1));
                    parts.Add(age >= 3f ? Wc(coax) : Gc(coax));
                }
                else if (site.CoaxHours > 0) parts.Add(Wc(Go.T("go_bld_coax_in", "coax in {0} h", site.CoaxHours)));
                if (site.OutYesterday > 0.05f) parts.Add(Bc(Go.T("go_bld_out", "outages")));
                return parts.Count == 0 ? "-" : string.Join(" | ", parts);
            }, BldState, 19f);
            Ui.LiveLabel(r, () =>
            {
                if (!s.Sites.TryGetValue(key, out var site)) return "";
                int n = Sim.CustomersOf(site);
                return Go.Num(n) + " / " + Go.Num(b.Units) + (site.Waiting > 0 ? " " + Wc("+" + site.Waiting) : "");
            }, BldCust, 19f);
            if (twoLines)
            {
                r = Ui.Row(list, 44f, 10f);
                Ui.Label(r, "", 200f, 18f);   // under the address
            }
            Ui.Btn(r, "", 250f, () =>
            {
                if (s.Sites.TryGetValue(key, out var old) && old.Coax && !old.CoaxOff) Act.Refurbish(s, key);
                else Act.Connect(s, key);
            }, () =>
            {
                if (s.Sites.TryGetValue(key, out var old) && old.Coax && !old.CoaxOff)
                    return Sim.AgeYears(s, old.BuiltDay, Sim.Day()) >= 1f && Money.CanPay(Act.RefurbishCost(old));
                if (s.Sites.TryGetValue(key, out var site) && (site.Coax || site.CoaxHours > 0 || site.Fiber)) return false;
                if (site == null && Rival.LockedUntil(s, key, Sim.Day()) >= 0) return false;
                if (!Places.Covered(s, b.Hood)) return false;   // GO-HUBS-1
                return Money.CanPay(Act.SiteCost(s, b, out _));
            }, () =>
            {
                if (!s.Sites.ContainsKey(key) && Rival.LockedUntil(s, key, Sim.Day()) >= 0) return Go.T("go_bld_exclusive", "exclusive contract");
                if (s.Sites.TryGetValue(key, out var old) && old.Coax && !old.CoaxOff && Sim.AgeYears(s, old.BuiltDay, Sim.Day()) >= 1f)
                    return Go.T("go_bld_refurbish", "Refurbish {0}", Go.Money(Act.RefurbishCost(old)));
                if (s.Sites.TryGetValue(key, out var site) && (site.Coax || site.CoaxHours > 0)) return Go.T("go_bld_coax_ok", "coax connected");
                if (s.Sites.TryGetValue(key, out var fs) && fs.Fiber) return Go.T("go_bld_fiberonly", "fiber only");
                if (!Places.Covered(s, b.Hood)) return Go.T("go_bld_uncovered", "no office/center there");
                float cost = Act.SiteCost(s, b, out int nodes);
                return nodes > 0 ? Go.T("go_bld_connect_nodes", "Connect {0} (+{1} node)", Go.Money(cost), nodes) : Go.T("go_bld_connect", "Connect {0}", Go.Money(cost));
            }, null, 18f);
            if (olt)
            {
                Ui.Btn(r, "", 270f, () => Act.ConnectFiber(s, key), () =>
                {
                    if (s.Sites.TryGetValue(key, out var site) && (site.Fiber || site.FiberHours > 0)) return false;
                    if (!Sim.OltAt(s, b.Hood) || !Places.Covered(s, b.Hood)) return false;   // GO-HUBS-1: the OLT of its group
                    return Money.CanPay(Act.FiberCost(s, b, out _));
                }, () =>
                {
                    if (s.Sites.TryGetValue(key, out var site) && (site.Fiber || site.FiberHours > 0)) return Go.T("go_bld_fiber_ok", "fiber connected");
                    if (!Sim.OltAt(s, b.Hood)) return Go.T("go_bld_nolt", "no OLT for it");
                    return Go.T("go_bld_fiber_build", "Fiber {0}", Go.Money(Act.FiberCost(s, b, out _)));
                }, null, 18f);
                Ui.Btn(r, "", 190f, () =>
                {
                    if (!s.Sites.TryGetValue(key, out var site) || !site.Fiber) return;
                    if (Sim.CustomersOf(site, fiber: false) == 0) Act.CoaxOff(s, key); else Act.MoveToFiber(s, key);
                }, () =>
                {
                    if (!s.Sites.TryGetValue(key, out var site) || !site.Fiber) return false;
                    int coax = Sim.CustomersOf(site, fiber: false);
                    return coax == 0 ? !site.CoaxOff && (site.Coax || site.CoaxHours > 0) : coax > Planned(s, key) && s.Stock.Ont > 0;
                }, () =>
                {
                    if (!s.Sites.TryGetValue(key, out var site) || !site.Fiber) return Go.T("go_bld_move", "Move");
                    int coax = Sim.CustomersOf(site, fiber: false);
                    if (coax == 0) return site.CoaxOff ? Go.T("go_bld_coax_isoff", "coax off") : Go.T("go_bld_coaxoff", "Switch coax off");
                    return Go.T("go_bld_move_n", "Move {0}", coax - Planned(s, key));
                }, null, 18f);
            }
            Ui.Btn(r, "", 230f, () => Act.Renew(s, key), () => s.Sites.TryGetValue(key, out var ps) && !ps.Lost && ps.PermitUntil >= 0 && ps.PermitUntil - Sim.Day() <= 30,
                   () =>
                   {
                       if (!s.Sites.TryGetValue(key, out var ps) || ps.Lost || ps.PermitUntil < 0) return Go.T("go_bld_permit_none", "no contract");
                       int left = ps.PermitUntil - Sim.Day();
                       return left <= 30 ? Go.T("go_bld_renew", "Renew {0} ({1} d)", Go.Money(Act.RenewCost(s, ps)), left) : Go.T("go_bld_permit", "contract to day {0}", ps.PermitUntil);
                   }, null, 17f);
            if (bulk)   // GO-BULK-1
                Ui.Btn(r, "", 270f, () => Deals.OfferBulk(s, key), () => s.Sites.TryGetValue(key, out var bs) && Deals.CanOfferBulk(s, bs), () =>
                {
                    if (!s.Sites.TryGetValue(key, out var bs)) return Go.T("go_bld_bulk_none", "Bulk: connect first");
                    if (bs.Bulk > 0) return Go.T("go_bld_bulk_on", "Bulk to day {0}", bs.BulkUntil);
                    if (bs.BulkAsked >= 0) return Go.T("go_bld_bulk_asked", "Bulk: answer tomorrow");
                    return Go.T("go_bld_bulk_offer", "Offer bulk TV ({0})", Go.Money(Cat.BulkPriceRef * b.Homes));
                }, () => s.Sites.TryGetValue(key, out var bs2) && bs2.Bulk > 0, 16f);
            Ui.Btn(r, Go.T("go_bld_drive", "Route"), 130f, () => City.Navigate(key), null, null, null, 18f);
        }

        static int Planned(IspState s, string key)
        {
            int n = 0;
            foreach (var t in s.Tickets) if (t.Type == Cat.TtInstall && t.Move && t.Site == key) n += t.N;
            return n;
        }

        // ── tariffs ──────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>A fee with - and +: 10 $ steps from 0 to max, what it means beside it.</summary>
        static void FeeRow(Transform list, string name, System.Func<float> get, System.Action<float> set, float max, string text)
        {
            var r = Ui.Row(list, 50f);
            Ui.Label(r, name, 300f, 21f, Ui.Sub);
            Ui.Btn(r, "-", 60f, () => set(Mathf.Max(0f, get() - 10f)), () => get() > 0f, null, null, 22f);
            Ui.LiveLabel(r, () => Go.Money(get()), 110f, 22f).alignment = TextAlignmentOptions.Center;
            Ui.Btn(r, "+", 60f, () => set(Mathf.Min(max, get() + 10f)), () => get() < max, null, null, 22f);
            Ui.Label(r, Sc(text), -1f, 19f);
        }

        static void Tariffs(IspState s, Transform list)
        {
            Ui.Head(list, Go.T("go_tar_title", "Tariffs"),
                Go.T("go_tar_text", "Prices are per day. The market price is what Skyline Cable asks for a comparable offer where you are - what customers compare with (businesses pay up to 1.6 times, villas up to twice). Cheaper draws more customers, much dearer makes them leave. A minimum term lowers cancellations but puts a few people off. Over 500 Mbit/s only with DOCSIS 3.1."));
            Ui.Para(list, Sc(Go.T("go_tar_up_text", "Upload: fiber is symmetric; a coax tariff has a tenth of its download (5 to 50 Mbit/s) or the upload you choose - more than 50 Mbit/s (marked *) only where high split runs. More upload is worth a little more to customers and loads the return path of the nodes.")), 19f);
            FeeRow(list, Go.T("go_tar_fee", "Connection fee"), () => s.Fee, v => s.Fee = v, Cat.FeeMax,
                   Go.T("go_tar_fee_text", "once per new connection - it pays the devices; customers accept 40 $, more puts some off, 0 $ draws a few more"));
            FeeRow(list, Go.T("go_tar_movefee", "Move fee"), () => s.MoveFee, v => s.MoveFee = v, Cat.MoveFeeMax,
                   Go.T("go_tar_movefee_text", "when a customer moves into another of your buildings: the switching staff disconnect the old address and connect the new one; 30 $ is accepted, more makes some cancel instead"));
            FeeRow(list, Go.T("go_tar_servicefee", "Fault service charge"), () => s.ServiceFee, v => s.ServiceFee = v, Cat.ServiceFeeMax,
                   Go.T("go_tar_servicefee_text", "when the technician finds the customer's own fault (own modem, TV set wrong, phone, power off, cable in the flat) or nobody is home; the dearer, the more complaints"));
            // GO-LAYOUT-2 (user 2026-11-10: "passe an, dass es in einer Zeile steht ... Name Netz Tempo Upload TV usw. -
            // Tempo ist Download"): the headings over the columns of TariffRow, in its widths (TariffCols)
            var head = Ui.Row(list, 34f, TariffGap);
            Ui.Label(head, Go.T("go_tar_name", "Name"), ColName, 19f, Ui.Sub);
            Ui.Label(head, Go.T("go_tar_tech", "Network"), ColNet, 19f, Ui.Sub);
            Ui.Label(head, Go.T("go_tar_speed", "Download"), ColDown, 19f, Ui.Sub, FontStyles.Normal, TextAlignmentOptions.Center);
            Ui.Label(head, Go.T("go_tar_upload", "Upload"), ColUp, 19f, Ui.Sub, FontStyles.Normal, TextAlignmentOptions.Center);
            Ui.Label(head, "TV", ColTv, 19f, Ui.Sub, FontStyles.Normal, TextAlignmentOptions.Center);
            Ui.Label(head, Go.T("go_tar_phone", "Phone"), ColPhone, 19f, Ui.Sub, FontStyles.Normal, TextAlignmentOptions.Center);
            Ui.Label(head, Go.T("go_tar_price", "Price/day"), ColPrice, 19f, Ui.Sub);
            Ui.Label(head, Go.T("go_tar_market", "Market"), ColMarket, 19f, Ui.Sub);
            if (!TariffTwoLines)   // on two lines the second one names them itself
            {
                Ui.Label(head, Go.T("go_tar_term", "Term"), ColTerm, 19f, Ui.Sub, FontStyles.Normal, TextAlignmentOptions.Center);
                Ui.Label(head, "SLA", ColSla, 19f, Ui.Sub, FontStyles.Normal, TextAlignmentOptions.Center);
                Ui.Label(head, Go.T("go_tar_users", "Customers"), ColUsers, 19f, Ui.Sub, FontStyles.Normal, TextAlignmentOptions.Center);
            }
            foreach (var t in s.Tariffs.ToArray()) TariffRow(s, list, t);
            var add = Ui.Row(list, 56f);
            Ui.Btn(add, Go.T("go_tar_add", "+ Coax tariff"), 280f, () => Act.AddTariff(s, false, false), () => s.Tariffs.Count < 12);
            if (s.Olt) Ui.Btn(add, Go.T("go_tar_add_fiber", "+ Fiber tariff"), 280f, () => Act.AddTariff(s, true, false), () => s.Tariffs.Count < 12);
            if (s.Sat)
            {
                Ui.Btn(add, Go.T("go_tar_add_tv", "+ TV only (coax)"), 280f, () => Act.AddTariff(s, false, true), () => s.Tariffs.Count < 12);
                if (s.Olt) Ui.Btn(add, Go.T("go_tar_add_tv_fiber", "+ TV only (fiber)"), 280f, () => Act.AddTariff(s, true, true), () => s.Tariffs.Count < 12);
            }
            if (!s.Sat) Ui.Para(list, Sc(Go.T("go_tar_notv", "TV in a tariff needs the satellite dishes and transponders (TV tab).")), 19f);
            Ui.Para(list, Sc(Go.T("go_tar_options", "TV: cable (DVB-C, needs transponders) or IPTV (needs the IPTV platform and a set-top box per customer, takes bandwidth). Phone: a flat-rate line, customers value it about {0} a day (needs the voice platform).", Go.Money(Cat.PhoneWorth))), 19f);
        }

        // GO-LAYOUT-2: the columns - narrower than GO-LAYOUT-1's, so that a tariff fits on one line from about 1700 units of
        // list width (a 1920 or wider screen); the buttons' texts a little smaller, so that none is cut ("im Verk...")
        const float TariffGap = 8f, ColName = 200f, ColNet = 100f, ColPm = 40f, ColBw = 130f, ColDown = ColPm + ColBw + ColPm + 2f * TariffGap,
                    ColUp = 160f, ColTv = 100f, ColPhone = 80f, ColPrice = 100f, ColMarket = 90f,
                    ColTerm = 120f, ColSla = 100f, ColUsers = 110f, ColSale = 120f, ColDel = 100f;
        const float TariffOneLine = ColName + ColNet + ColDown + ColUp + ColTv + ColPhone + ColPrice + ColMarket
                                  + ColTerm + ColSla + ColUsers + ColSale + ColDel + 12f * TariffGap;

        /// <summary>GO-LAYOUT-1/2: a tariff on one line when the tab has the room, else on two (narrow screens).</summary>
        static bool TariffTwoLines => TariffOneLine + 10f > App.ListWidth;

        static void TariffRow(IspState s, Transform list, Tariff t)
        {
            var r = Ui.Row(list, 52f, TariffGap);
            Ui.Field(r, t.Name, ColName, v => { if (v.Trim().Length > 0) t.Name = v.Trim(); }, 24);
            Ui.Label(r, t.Fiber ? Go.T("go_fiber", "Fiber") : Go.T("go_coax", "Coax"), ColNet, 19f);
            if (t.Mbit > 0)
            {
                Ui.Btn(r, "-", ColPm, () => t.Mbit = Act.NextSpeed(t, -1));
                Ui.LiveLabel(r, () => Go.Bw(t.Mbit), ColBw, 20f, null, FontStyles.Normal, TextAlignmentOptions.Center);
                Ui.Btn(r, "+", ColPm, () => t.Mbit = Act.NextSpeed(t, 1));
            }
            else Ui.Label(r, Go.T("go_tar_tvonly", "TV only"), ColDown, 20f, Ui.Sub, FontStyles.Normal, TextAlignmentOptions.Center);
            // GO-UP-1: the upload - fiber symmetric, coax automatic (a tenth) or chosen; above 50 Mbit/s only where high split runs
            if (t.Mbit > 0 && t.Fiber) Ui.LiveLabel(r, () => Go.Bw(Sim.UpOf(t)), ColUp, 19f, null, FontStyles.Normal, TextAlignmentOptions.Center);
            else if (t.Mbit > 0)
                Ui.Btn(r, "", ColUp, () => t.Up = Act.NextUp(t), null,
                       () => (t.Up == 0 ? Go.T("go_tar_up_auto", "auto") + " " : "") + Go.Bw(Sim.UpOf(t)) + (Sim.UpOf(t) > Cat.UpLowMax ? " *" : ""), null, 16f);
            else Ui.Label(r, "-", ColUp, 20f, null, FontStyles.Normal, TextAlignmentOptions.Center);
            if (t.Mbit > 0)
            {
                Ui.Btn(r, "", ColTv, () => Act.NextTv(s, t), () => s.Sat,
                       () => !t.Tv ? Go.T("go_no", "no") : t.Ip ? "IPTV" : Go.T("go_tar_cable", "cable"), () => t.Tv, 19f);
                Ui.Btn(r, "", ColPhone, () => t.Phone = !t.Phone, () => s.Voice, () => Y(t.Phone), () => t.Phone, 19f);
            }
            else
            {
                Ui.Label(r, Go.T("go_tar_cable", "cable"), ColTv, 20f, null, FontStyles.Normal, TextAlignmentOptions.Center);
                Ui.Label(r, "-", ColPhone, 20f, null, FontStyles.Normal, TextAlignmentOptions.Center);
            }
            Ui.Field(r, t.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), ColPrice, v =>
            {
                if (Go.ParseNum(v, out float p) && p >= 0f && p < 1000f) t.Price = Mathf.Round(p * 100f) / 100f;
            }, 8);
            Ui.LiveLabel(r, () =>
            {
                float w = Sim.Worth(s, t, false, Cat.KindHome);
                string m = Go.Money(w);
                return t.Price > w * 1.3f ? Bc(m) : t.Price > w ? Wc(m) : Gc(m);
            }, ColMarket, 20f);
            bool two = TariffTwoLines;
            if (two)
            {   // narrow screens: the rest on a second line, each with its name - there is room for the longer texts
                r = Ui.Row(list, 46f, TariffGap);
                Ui.Label(r, "", ColName, 18f);   // under the name
            }
            Ui.Btn(r, "", two ? 210f : ColTerm, () => t.Term = t.Term == 0 ? 10 : t.Term == 10 ? 30 : 0, null,
                   () => (two ? Go.T("go_tar_term", "Term") + ": " : "") + (t.Term == 0 ? Go.T("go_tar_term0", "monthly") : Go.T("go_tar_termdays", "{0} days", t.Term)), null, 17f);
            if (t.Mbit > 0)   // GO-SLA-1
                Ui.Btn(r, "", ColSla, () => t.Sla = !t.Sla, null, () => t.Sla ? Go.T("go_tar_sla_on", "SLA 8 h") : Go.T("go_tar_sla_off", "no SLA"), () => t.Sla, 16f);
            else Ui.Label(r, "", ColSla, 18f);
            Ui.LiveLabel(r, () => (two ? Go.T("go_tar_users", "Customers") + ": " : "") + Go.Num(Act.Users(s, t)), two ? 190f : ColUsers, 20f, null, FontStyles.Normal,
                         two ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);
            Ui.Btn(r, "", ColSale, () => t.Active = !t.Active, null, () => t.Active ? Go.T("go_tar_on", "on sale") : Go.T("go_tar_off", "stopped"), () => t.Active, 17f);
            Ui.Btn(r, Go.T("go_tar_delete", "Delete"), ColDel, () => Act.DeleteTariff(s, t), () => Act.Users(s, t) == 0, null, null, 18f);
        }

        // ── devices ──────────────────────────────────────────────────────────────────────────────────────────

        static void Devices(IspState s, Transform list)
        {
            Ui.Head(list, Go.T("go_dev_title", "Devices"),
                Go.T("go_dev_text", "New customers choose: their own device (about 15 %, router freedom), or renting or buying - whatever is cheaper for them over 120 days. Rented devices stay yours: when one breaks you replace it, when a customer leaves it comes back. Market prices: rent about {0} a day, buy about $120-$180.", Go.Money(Cat.RefRent)));
            if (!Sim.Test) Ui.LivePara(list, () =>   // GO-SHOPS-2: every wholesaler (the nearest named), the importers later
            {
                string near = "";
                try { var at = City.PosOf(s.HeadendKey); if (at != Vector3.zero) near = GameTypes.NearestWholesale(at); } catch { }
                if (near.Length == 0) near = Stores(GameTypes.WholesaleShops);
                return Sc(Go.T("go_dev_where3", "New devices are goods. Every wholesaler of the city has them on its shelves - nearest to the headend: {0}. Carry the boxes into the office {1} and put them on a storage shelf there (e.g. from Square Appliances or Ika Bohag) - what lies there is the stock. Later, with a headquarters, a purchasing agent and a warehouse, an importer delivers them too ({2}); devices in your warehouses count as stock as well. Devices given back are refurbished and go out first.",
                    near, City.Name(s.HeadendKey), Stores(GameTypes.ImportShops)))
                     + (GamePatches.Active ? " " + Sc(Go.T("go_dev_delivery2", "Without carrying: call the wholesaler (Contacts) and start a delivery contract for the cable provider - the devices come at 8 o'clock onto the storage shelves in the office (pallet shelves count too), the amounts are set in BizMan (Deliveries).")) : "");
            }, 20f);
            DeviceCard(s, list, Cat.Dev30, Go.T("go_dev_30", "Cable modem DOCSIS 3.0"), () => s.Prices.Rent30, v => s.Prices.Rent30 = v, () => s.Prices.Buy30, v => s.Prices.Buy30 = v);
            if (s.Cmts31) DeviceCard(s, list, Cat.Dev31, Go.T("go_dev_31", "Cable modem DOCSIS 3.1"), () => s.Prices.Rent31, v => s.Prices.Rent31 = v, () => s.Prices.Buy31, v => s.Prices.Buy31 = v);
            if (s.Olt) DeviceCard(s, list, Cat.DevOnt, Go.T("go_dev_ont", "ONT for fiber"), () => s.Prices.RentOnt, v => s.Prices.RentOnt = v, () => s.Prices.BuyOnt, v => s.Prices.BuyOnt = v);
            if (s.Iptv)
            {
                var c = Ui.Column(list, 6f, true);
                var r = Ui.Row(c);
                Ui.Label(r, Go.T("go_dev_stb", "IPTV set-top boxes"), 420f, 22f, Color.white, FontStyles.Bold);
                Ui.LiveLabel(r, () => Go.T("go_dev_stock", "in stock: {0}", Go.Num(Sim.StbStock(s))), 260f, 21f);
                foreach (int n in Sim.Test ? new[] { 50, 200 } : new int[0])
                {
                    int k = n;
                    Ui.Btn(r, "", 220f, () => Act.BuyStb(s, k), () => Money.CanPay(k * Sim.Buy(s, Cat.BuyStb)), () => "+" + k + "  " + Go.Money(k * Sim.Buy(s, Cat.BuyStb)));
                }
                var p = Ui.Row(c);
                Ui.Label(p, Go.T("go_dev_rent", "Rent per day"), 220f, 21f, Ui.Sub);
                Ui.Field(p, s.Prices.RentStb.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), 130f, v => { if (Go.ParseNum(v, out float x) && x >= 0f && x < 100f) s.Prices.RentStb = Mathf.Round(x * 100f) / 100f; }, 8);
                Ui.LiveLabel(p, () => Sc(Go.T("go_dev_stb_text", "every IPTV customer rents one | in use {0}", Go.Num(Sim.IptvCustomers(s)))), -1f, 19f);
            }
            if (s.Cas)
            {
                var c = Ui.Column(list, 6f, true);
                var r = Ui.Row(c);
                Ui.Label(r, Go.T("go_dev_cards", "Smartcards for Pay-TV"), 420f, 22f, Color.white, FontStyles.Bold);
                Ui.LiveLabel(r, () => Go.T("go_dev_stock", "in stock: {0}", Go.Num(Sim.CardStock(s))), 260f, 21f);
                foreach (int n in Sim.Test ? new[] { 50, 200 } : new int[0])
                {
                    int k = n;
                    Ui.Btn(r, "", 220f, () => Act.BuyCards(s, k), () => Money.CanPay(k * Sim.Buy(s, Cat.BuyCard)), () => "+" + k + "  " + Go.Money(k * Sim.Buy(s, Cat.BuyCard)));
                }
            }
            Ui.LivePara(list, () =>
            {
                int w = 0;
                foreach (var site in s.Sites.Values) w += site.Waiting;
                return w > 0 ? Wc(Go.T("go_dev_waiting", "{0} new customers are waiting for a device - they give up after three days.", Go.Num(w))) : "";
            }, 21f);
        }

        static void DeviceCard(IspState s, Transform list, byte dev, string name, System.Func<float> rent, System.Action<float> setRent, System.Func<float> buy, System.Action<float> setBuy)
        {
            var c = Ui.Column(list, 6f, true);
            var r = Ui.Row(c);
            Ui.Label(r, name, 420f, 22f, Color.white, FontStyles.Bold);
            Ui.LiveLabel(r, () => Go.T("go_dev_stock", "in stock: {0}", Go.Num(Sim.StockOf(s, dev))), 260f, 21f);
            foreach (int n in Sim.Test ? new[] { 10, 50, 200 } : new int[0])
            {
                int k = n;
                Ui.Btn(r, "", 220f, () => Act.BuyDevices(s, dev, k), () => Money.CanPay(k * Sim.Buy(s, Sim.DeviceCost(dev))),
                    () => "+" + k + "  " + Go.Money(k * Sim.Buy(s, Sim.DeviceCost(dev))));
            }
            Ui.Pair(c, Go.T("go_dev_inuse", "In use"), () => Go.T("go_dev_inuse_v", "rented {0} | sold {1} | own devices {2}",
                Go.Num(Sim.Devices(s, dev, Cat.OwnRent)), Go.Num(Sim.Devices(s, dev, Cat.OwnBought)), Go.Num(Sim.Devices(s, dev, Cat.OwnSelf))));
            var p = Ui.Row(c);
            Ui.Label(p, Go.T("go_dev_rent", "Rent per day"), 220f, 21f, Ui.Sub);
            Ui.Field(p, rent().ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), 130f, v => { if (Go.ParseNum(v, out float x) && x >= 0f && x < 100f) setRent(Mathf.Round(x * 100f) / 100f); }, 8);
            Ui.Label(p, Go.T("go_dev_buy", "Sale price"), 200f, 21f, Ui.Sub, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            Ui.Field(p, buy().ToString("0", System.Globalization.CultureInfo.InvariantCulture), 130f, v => { if (Go.ParseNum(v, out float x) && x >= 0f && x < 5000f) setBuy(Mathf.Round(x)); }, 8);
            Ui.LiveLabel(p, () => Sc(Go.T("go_dev_cost", "you pay {0} each", Go.Money(Sim.Buy(s, Sim.DeviceCost(dev))))), -1f, 19f);
        }

        // ── TV ───────────────────────────────────────────────────────────────────────────────────────────────

        static void Tv(IspState s, Transform list)
        {
            Ui.Head(list, Go.T("go_tv_title", "Television"),
                Go.T("go_tv_text2", "The headend receives TV by satellite. Every Sat device in the office puts one satellite transponder (DVB-S2) on the cable (DVB-C); a transponder carries about 8 channels. More channels need more Sat devices (from the furniture store). Free TV costs no licence per customer. Every transponder takes a channel in the cable that the internet then lacks."));
            var c = Ui.Column(list, 6f, true);
            Equip(c, Go.T("go_tv_sat", "Satellite dishes"), Go.T("go_tv_sat_text", "dishes, LNBs and the rack at the headend"), () => s.Sat, () => Act.BuySat(s), Cat.Sat, null);
            if (s.Sat)
            {
                var r = Ui.Row(c);
                Ui.Label(r, Go.T("go_tv_transponders", "Transponders"), 300f, 21f, Ui.Sub);
                Ui.Btn(r, "-", 60f, () => Act.Transponders(s, -1), () => s.Transponders > 0);
                Ui.LiveLabel(r, () => s.Transponders + "  " + Sc(Go.T("go_tv_channels", "about {0} channels", Sim.Channels(s))), 300f, 22f, null, FontStyles.Normal, TextAlignmentOptions.Center);
                Ui.Btn(r, "+", 60f, () => Act.Transponders(s, 1), () => s.Transponders < Mathf.Min(Cat.MaxTransponders, Sim.SatDevices(s)));
                Ui.LiveLabel(r, () =>
                {   // GO-TV-2: one transponder per Sat device
                    int dev = Sim.SatDevices(s);
                    if (s.Transponders > dev)
                        return Wc(Go.T("go_tv_onair", "only {0} on air - set up {1} more Sat devices", Sim.OnAir(s), s.Transponders - dev));
                    return Sc(Go.T("go_tv_sats", "{0} Sat devices - one transponder each", dev));
                }, -1f, 19f);
                Ui.Pair(c, Go.T("go_tv_customers", "TV customers"), () => Go.Num(Sim.TvCustomers(s)) + "  " + Sc(Go.T("go_tv_draw", "the offer draws {0}", Go.Pct(Sim.TvDraw(s)))));
            }
            Ui.Head(list, Go.T("go_tv_paytv", "Pay-TV as a reseller"),
                Go.T("go_tv_paytv_text", "Wholesale contracts with Pay-TV providers: they charge you per subscriber and day (some a minimum per day), you set the price and bill your customers. Pay-TV needs the CA system (encryption) and a smartcard per subscription (Devices tab)."));
            var cc = Ui.Column(list, 6f, true);
            Equip(cc, Go.T("go_tv_cas", "CA system"), Go.T("go_tv_cas_text", "encrypts the Pay-TV channels"), () => s.Cas, () => Act.BuyCas(s), Cat.Cas,
                  () => s.Sat ? "" : Go.T("go_tv_needsat", "needs the satellite dishes"));
            foreach (var o in Cat.PayTvOffers) PayTvCard(s, list, o);
        }

        static void PayTvCard(IspState s, Transform list, Cat.PayTvOffer o)
        {
            var c = Ui.Column(list, 6f, true);
            var r = Ui.Row(c, 44f);
            Ui.Label(r, o.Name, 300f, 23f, Color.white, FontStyles.Bold);
            Ui.Label(r, Sc(Go.T(o.Key, o.Id)), 300f, 20f);
            Ui.Label(r, Go.T("go_tv_terms", "you pay {0} per subscriber and day | minimum {1} a day | suggested price {2}", Go.Money(o.Cost), Go.Money(o.Minimum), Go.Money(o.Recommended)), -1f, 19f);
            var p = s.PayTv.Find(x => x.Id == o.Id);
            var r2 = Ui.Row(c);
            if (p == null)
            {
                Ui.Btn(r2, Go.T("go_tv_sign", "Sign the contract"), 320f, () => Act.Contract(s, o.Id, true), () => s.Cas);
                return;
            }
            Ui.Label(r2, Go.T("go_tv_price", "Your price per day"), 260f, 21f, Ui.Sub);
            Ui.Field(r2, p.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), 130f, v => { if (Go.ParseNum(v, out float x) && x >= 0f && x < 100f) p.Price = Mathf.Round(x * 100f) / 100f; }, 8);
            Ui.LiveLabel(r2, () =>
            {
                float margin = p.Subs * p.Price - Mathf.Max(o.Minimum, p.Subs * o.Cost);
                return Go.T("go_tv_subs", "{0} subscribers | per day {1}", Go.Num(p.Subs), margin >= 0 ? Gc(Go.Money(margin)) : Bc(Go.Money(margin)));
            }, -1f, 21f);
            Ui.Btn(r2, Go.T("go_tv_end", "End the contract"), 260f, () => Act.Contract(s, o.Id, false));
        }

        // ── orders and faults ────────────────────────────────────────────────────────────────────────────────

        static void Faults(IspState s, Transform list)
        {
            Ui.Head(list, Go.T("go_ord_title", "Orders and faults"),
                Go.T("go_ord_text", "A new customer's way: the customer service takes the order, a phone number is ported or handed out, the switching staff connect the tap in the building (coax), then self-install by post or an appointment with a technician (fiber always). Coax customers who cancel keep the signal until their tap is disconnected."));
            if (s.PlayerTicket != 0)
            {
                var pc = Ui.Column(list, 6f, true);
                Ui.LivePara(pc, () =>
                {
                    var t = s.Tickets.Find(x => x.Id == s.PlayerTicket);
                    if (t == null) return "";
                    bool near = Sim.PlayerNear(s, t);
                    float done = Cat.PerUnit(t.Type) ? t.Done / Cat.Fault(t.Type).work : t.Done / Mathf.Max(0.01f, t.Work);
                    return Go.T("go_flt_you", "You repair: {0} - {1} done.", Sim.FaultName(t.Type), Go.Pct(Mathf.Clamp01(done))) + " "
                         + (near ? Gc(Go.T("go_flt_near", "You are there, the work goes on.")) : Wc(Go.T("go_flt_far", "Drive there - the route is set.")));
                }, 22f);
            }
            var sum = Ui.Column(list, 4f, true);
            Ui.Pair(sum, Go.T("go_ord_service", "Customer service"), () =>
            {
                int orders = Sim.InPipeline(s, Cat.TtOrder);
                string calls = s.CallsToday > 0f ? Go.T("go_ord_calls", "calls today {0}, answered {1}", Go.Num(s.CallsToday), Go.Pct(Mathf.Clamp01(s.CallsAnswered / s.CallsToday))) : Go.T("go_ord_nocalls", "no calls today");
                return Go.T("go_ord_orders", "{0} orders waiting", Go.Num(orders)) + " | " + calls;
            }, 380f);
            Ui.Pair(sum, Go.T("go_ord_porting", "Porting"), () =>
            {
                int port = 0, nonum = 0;
                foreach (var t in s.Tickets) if (t.Type == Cat.TtPort) { if (t.NoNumber) nonum += t.N; else port += t.N; }
                string n = Go.T("go_ord_porting_v", "{0} numbers move over (2 days) | {1} free numbers", Go.Num(port), Go.Num(Sim.FreeNumbers(s)));
                return nonum > 0 ? n + " | " + Bc(Go.T("go_ord_nonumber", "{0} wait for a number", Go.Num(nonum))) : n;
            }, 380f);
            Ui.Pair(sum, Go.T("go_ord_switching", "Switching orders"), () =>
                Go.T("go_ord_switching_v", "switch on {0} | switch off {1} | still on the signal without a contract {2}",
                     Go.Num(Sim.InPipeline(s, Cat.TtOn)), Go.Num(Sim.InPipeline(s, Cat.TtOff)), Go.Num(Sim.FreeRiders(s))), 380f);
            Ui.Pair(sum, Go.T("go_ord_installs", "Appointments"), () =>
                Go.T("go_ord_installs_v", "{0} installations | {1} moves to fiber", Go.Num(Sim.InPipeline(s, Cat.TtInstall, false)), Go.Num(Sim.InPipeline(s, Cat.TtInstall, true))), 380f);
            Ui.Pair(sum, Go.T("go_ord_moves", "Moves"), () =>
            {
                var y = Yesterday(s);
                int on = 0, off = 0;
                foreach (var t in s.Tickets) if (t.Relocation) { if (t.Type == Cat.TtOff) off += t.N; else on += t.N; }
                return Go.T("go_ord_moves_v", "yesterday {0} moved along ({1}), {2} cancelled | waiting: disconnect {3}, connect {4}",
                    y?.Moves ?? 0, Go.Money(y?.MoveFees ?? 0f), y?.MovesLost ?? 0, Go.Num(off), Go.Num(on));
            }, 380f);
            Ui.Pair(sum, Go.T("go_ord_visits", "At customers'"), () =>
            {
                var y = Yesterday(s);
                return y == null ? "-" : Go.T("go_ord_visits_v", "yesterday {0} visits | {1} the customer's own fault | {2} nobody home | {3} charged",
                    y.Visits, y.OwnFault, y.NoShows, Go.Money(y.ServiceFees));
            }, 380f);
            Ui.Pair(sum, Go.T("go_ord_complaints", "Complaints"), () =>
            {
                var y = Yesterday(s);
                int open = 0;
                foreach (var t in s.Tickets) if (t.Type == Cat.TtComplaint) open += t.N;
                string v = Go.T("go_ord_complaints_v", "today {0}, answered {1} | open {2}", s.Today.Complaints, s.Today.ComplaintsDone, open);
                return y != null && y.Complaints > 0 ? v + " | " + Sc(Go.T("go_ord_complaints_y", "yesterday {0}", y.Complaints)) : v;
            }, 380f);

            Ui.Head(list, Go.T("go_permit_title", "Permission contracts"),
                Go.T("go_permit_text", "Every building has a contract with its owner for two game years. Renew it before it ends - or there is a tender, and with a poor reputation Skyline Cable takes the building and its customers. Field sales make renewing cheaper and tenders easier to win."));
            var pr = Ui.Row(list);
            Ui.Btn(pr, "", 380f, () => s.AutoRenew = !s.AutoRenew, null,
                   () => Go.T("go_permit_auto", "Renew by itself: {0}", s.AutoRenew ? Go.T("go_on", "on") : Go.T("go_off", "off")), () => s.AutoRenew);
            Ui.Label(pr, Sc(Go.T("go_permit_auto_text", "in the last five days, when the money is there")), -1f, 19f);
            var ending = new List<Site>();
            foreach (var site in s.Sites.Values) if (!site.Lost && site.PermitUntil >= 0 && site.PermitUntil - Sim.Day() <= 15) ending.Add(site);
            ending.Sort((a, b) => a.PermitUntil.CompareTo(b.PermitUntil));
            if (ending.Count == 0) Ui.Para(list, Gc(Go.T("go_permit_none", "No contract ends in the next 15 days.")), 21f);
            foreach (var site in ending)
            {
                string key = site.Key;
                var r = Ui.Row(list, 48f, 10f);
                Ui.Label(r, City.Name(key), 400f, 20f);
                Ui.LiveLabel(r, () => s.Sites.TryGetValue(key, out var x) ? Go.T("go_permit_ends", "ends on day {0} ({1} days)", x.PermitUntil, x.PermitUntil - Sim.Day()) : "", 320f, 20f);
                Ui.LiveLabel(r, () => Go.T("go_flt_n", "{0} customers", Sim.CustomersOf(site)), 200f, 20f);
                Ui.Btn(r, "", 300f, () => Act.Renew(s, key), null, () => Go.T("go_permit_renew", "Renew - {0}", Go.Money(Act.RenewCost(s, site))));
            }

            var faults = s.Tickets.FindAll(t => Cat.IsFault(t.Type));
            faults.Sort((a, b) => b.Affected.CompareTo(a.Affected) != 0 ? b.Affected.CompareTo(a.Affected) : a.Opened.CompareTo(b.Opened));
            Ui.Head(list, Go.T("go_flt_title", "Faults"),
                Go.T("go_flt_text", "Free technicians take the most urgent fault. A fault that is not fixed within two days costs compensation from the third day ($5 or 10 % of the monthly price per customer and day, from the 5th day $10 or 20 %). You can drive there yourself and repair it on site."));
            if (faults.Count == 0) Ui.Para(list, Gc(Go.T("go_flt_none", "No faults right now.")), 22f);
            int shown = 0;
            foreach (var t in faults) { if (shown++ >= 50) break; TicketRow(s, list, t); }

            var jobs = s.Tickets.FindAll(t => t.Type == Cat.TtInstall || t.Type == Cat.TtOn || t.Type == Cat.TtOff || t.Type == Cat.TtOrder || t.Type == Cat.TtPort);
            Ui.Head(list, Go.T("go_ord_jobs", "Orders, switching jobs and appointments"),
                Go.T("go_flt_installs_text", "Fiber always needs a technician at the customer's, coax a quarter of the time. An appointment missed by more than 24 hours costs compensation, and some customers cancel."));
            if (jobs.Count == 0) Ui.Para(list, Gc(Go.T("go_flt_noinstalls", "No appointments waiting.")), 21f);
            jobs.Sort((a, b) => a.Type != b.Type ? a.Type.CompareTo(b.Type) : a.Opened.CompareTo(b.Opened));
            shown = 0;
            foreach (var t in jobs) { if (shown++ >= 40) break; TicketRow(s, list, t); }

            var complaints = s.Tickets.FindAll(t => t.Type == Cat.TtComplaint);
            complaints.Sort((a, b) => a.Opened.CompareTo(b.Opened));
            Ui.Head(list, Go.T("go_cmp_title", "Complaints"),
                Go.T("go_cmp_text", "Customers complain about an unfriendly technician, the fault service charge, an appointment not kept, a hotline nobody answers, slow evenings or a move that takes too long. The customer service answers them; a complaint nobody answered for a day makes a few customers leave. Unfriendly technicians can take a customer-care training (tab Staff)."));
            if (complaints.Count == 0) Ui.Para(list, Gc(Go.T("go_cmp_none", "No complaints open.")), 21f);
            shown = 0;
            foreach (var t in complaints) { if (shown++ >= 30) break; TicketRow(s, list, t); }
        }

        static void TicketRow(IspState s, Transform list, Ticket t)
        {
            var r = Ui.Row(list, 50f, 10f);
            string what = t.Type == Cat.TtInstall ? (t.Move ? Go.T("go_flt_move", "Move to fiber") : t.Relocation ? Go.T("go_tt_move_install", "Move: installation") : Go.T("go_tt_install", "Installation"))
                        : t.Type == Cat.TtPort && t.NoNumber ? Go.T("go_tt_nonumber", "Waiting for a number")
                        : t.Type == Cat.TtOn && t.Relocation ? Go.T("go_tt_move_on", "Move: connect the new address")
                        : t.Type == Cat.TtOff && t.Relocation ? Go.T("go_tt_move_off", "Move: disconnect the old address")
                        : t.Type == Cat.TtDevice && t.Cause > 0 ? (t.Diag ? Go.T("go_tt_own", "Customer's own: {0}", Sim.CauseName(t.Cause)) : Go.T("go_tt_report", "Customer reports a fault (cause unclear)"))
                        : t.Type == Cat.TtComplaint ? Go.T("go_tt_complaint", "Complaint: {0}", Sim.ComplaintName(t.Cause)) + (t.About.Length > 0 ? " (" + t.About + ")" : "")
                        : Sim.FaultName(t.Type);
            Ui.Label(r, what, 330f, 20f, Sim.IsOutage(t.Type) ? Ui.Bad : Color.white);
            string where = t.Type == Cat.TtCmts ? (t.Hood.Length == 0 || t.Hood == Places.HeadHood(s) || Places.Hub(s, t.Hood).Length == 0 ? Go.T("go_headend", "headend") : City.Name(Places.Hub(s, t.Hood)))
                         : t.Site.Length > 0 ? City.Name(t.Site) : City.HoodName(t.Hood);
            Ui.Label(r, where, 300f, 19f);
            Ui.LiveLabel(r, () => Cat.PerUnit(t.Type) || t.Type == Cat.TtPort ? Go.T("go_flt_n", "{0} customers", t.N) : Go.T("go_flt_affected", "{0} affected", Go.Num(t.Affected)), 170f, 19f);
            Ui.LiveLabel(r, () =>
            {
                long now = Sim.NowHour();
                if ((t.Type == Cat.TtDevice || t.Type == Cat.TtWifi) && t.Due > now) return Wc(Go.T("go_flt_again", "new visit in {0} h", t.Due - now));
                if (t.Type == Cat.TtInstall || (t.Type == Cat.TtPort && !t.NoNumber))
                {
                    long left = t.Due - now;
                    return left >= 0 ? Go.T("go_flt_due", "due in {0} h", left) : Bc(Go.T("go_flt_late", "{0} h late", -left));
                }
                long age = now - t.Opened;
                return age >= 48 ? Bc(Go.T("go_flt_age", "{0} h", age)) : age >= 24 ? Wc(Go.T("go_flt_age", "{0} h", age)) : Go.T("go_flt_age", "{0} h", age);
            }, 130f, 19f);
            Ui.LiveLabel(r, () =>
            {
                if (s.PlayerTicket == t.Id) return Gc(Go.T("go_flt_byyou", "you"));
                if (t.Type == Cat.TtPort) return Sc(t.NoNumber ? Go.T("go_flt_buyblock", "buy a number block") : Go.T("go_flt_oldprovider", "at the old provider"));
                if (t.Type == Cat.TtOrder || t.Type == Cat.TtComplaint) return Sc(Go.T("go_flt_service", "customer service"));
                if (t.Techs.Count == 0)
                    return Wc(t.Type == Cat.TtOn || t.Type == Cat.TtOff ? Go.T("go_flt_waiting_switch", "waiting for switching staff") : Go.T("go_flt_waiting", "waiting for a technician"));
                var names = new List<string>();
                bool driving = false;
                foreach (var tech in s.Techs) if (t.Techs.Contains(tech.Id)) { names.Add(tech.Name); if (tech.Travel > 0f) driving = true; }
                float done = Cat.PerUnit(t.Type) ? 0f : t.Done / Mathf.Max(0.01f, t.Work);
                return string.Join(", ", names) + " " + Sc(driving ? Go.T("go_flt_driving", "driving") : Cat.PerUnit(t.Type) ? Go.T("go_flt_installing", "at work") : Go.T("go_flt_working", "working {0}", Go.Pct(done)));
            }, 330f, 19f);
            if (t.Type != Cat.TtWifi && t.Type != Cat.TtOrder && t.Type != Cat.TtPort && t.Type != Cat.TtComplaint)
            {
                Ui.Btn(r, Go.T("go_bld_drive", "Route"), 120f, () => Act.Navigate(s, t), null, null, null, 18f);
                Ui.Btn(r, "", 190f, () => Act.Repair(s, t), null,
                       () => s.PlayerTicket == t.Id ? Go.T("go_flt_stop", "Stop") : Go.T("go_flt_repair", "Do it myself"), () => s.PlayerTicket == t.Id, 18f);
            }
        }

        // ── the staff ────────────────────────────────────────────────────────────────────────────────────────

        static string RoleText(byte role) =>
            role == Cat.RoleMaster ? Go.T("go_role_master_text", "One per neighbourhood: plans and looks after the network there - 40 % fewer coax faults, network faults need less work, and building and upgrades take the normal time (twice as long without a network master). Needs a workplace and a car, works 8-16.")
            : role == Cat.RoleSwitch ? Go.T("go_role_switch_text", "Switches the taps of coax customers on and off (DOCSIS). Without switching staff the technicians do it besides their work. Needs a van.")
            : role == Cat.RoleService ? Go.T("go_role_service_text", "Takes new orders (porting included) and answers the calls - during outages many customers call. Unanswered calls cost reputation. Needs a workplace.")
            : role == Cat.RoleSupport ? Go.T("go_role_support_text", "Diagnoses faults from the desk: a broken device is often fixed remotely, the rest needs less work on site; Wi-Fi problems are solved by remote access (without support a technician has to drive there). Needs a workplace.")
            : role == Cat.RoleSales ? Go.T("go_role_sales_text", "Goes from door to door in your buildings: where they were, more people sign that day. Negotiates the permission contracts with the owners - renewing costs 30 % less, and tenders are won more often. Needs a car.")
            : Go.T("go_role_tech_text", "Fixes faults and keeps the appointments at the customers'. Needs a van.");

        /// <summary>How a person treats customers, coloured: 1-2 rude, 3 normal, 4-5 friendly.</summary>
        static string Manner(int m)
        {
            string v = Go.T("go_staff_manner", "manner {0}/5", Mathf.Clamp(m, 1, 5));
            return m <= 2 ? Bc(v) : m >= 4 ? Gc(v) : v;
        }

        static string Missing(Tech t)
        {
            switch (t.Missing)
            {
                case 1: return Bc(Cat.NeedsVan(t.Role) ? Go.T("go_staff_novan2", "no van at your offices") : Go.T("go_staff_nocar2", "no car at your offices"));
                case 2: return Bc(Go.T("go_staff_nodesk", "no workplace free"));
                case 3: return Sc(Go.T("go_team_off", "off duty"));
                case 4: return Bc(Go.T("go_staff_station_incomplete", "workplace incomplete - the computer needs a desk and a chair"));
                case 5: return Bc(Go.T("go_staff_office_unready", "the office is not set up yet - see BizMan, requirements"));
                default: return "";
            }
        }

        /// <summary>Workplaces of one place: how many, the room there, buy and give back.</summary>
        static void DeskRow(IspState s, Transform parent, bool center)
        {
            if (!Sim.Test)
            {   // GAME-TYPES-1: Going Online's workplaces are furniture from an office furniture store
                if (center) return;   // one row for all buildings (Gear.Placed counts the old customer center too)
                var gr = Ui.Row(parent);
                Ui.Label(gr, Go.T("go_staff_desks_office", "Workplaces in the office"), 380f, 21f, Ui.Sub);
                Ui.LiveLabel(gr, () => Go.T("go_staff_desks_game3", "{0} technical, {1} control room, {2} customer center workplaces", Gear.Placed(s, GameTypes.ITechDesk), Gear.Placed(s, GameTypes.IControlDesk), Gear.Placed(s, GameTypes.IDesk)), 760f, 21f);
                Ui.Label(gr, Sc(Go.T("go_staff_desks_where", "from {0}", Stores(GameTypes.FurnitureStores))), -1f, 19f);
                // GO-WORK-1: what the recruitment agencies offer for it now
                var jr = Ui.Row(parent);
                Ui.Label(jr, Go.T("go_staff_jobs", "The agencies offer"), 380f, 21f, Ui.Sub);
                Ui.LiveLabel(jr, () =>
                {
                    var jobs = GameTypes.WorkplaceJobs(City.Reg(s.HeadendKey));
                    if (s.CenterKey.Length > 0 && s.CenterKey != s.HeadendKey) foreach (var k in GameTypes.WorkplaceJobs(City.Reg(s.CenterKey))) if (!jobs.Contains(k)) jobs.Add(k);
                    if (jobs.Count == 0) return Wc(Go.T("go_staff_jobs_none", "nobody yet - set up a workplace first"));
                    var names = new List<string>();
                    foreach (var k in jobs) names.Add(Act.RoleName(GameTypes.RoleOf(k)));
                    return string.Join(", ", names);
                }, -1f, 21f);
                return;
            }
            var dr = Ui.Row(parent);
            Ui.Label(dr, center ? Go.T("go_center_desks", "Workplaces in the customer center") : Go.T("go_staff_desks", "Workplaces at the headend"), 380f, 21f, Ui.Sub);
            Ui.LiveLabel(dr, () => center ? Go.T("go_staff_desks_v", "{0} of at most {1}", s.CenterDesks, Fleet.MaxCenterDesks(s))
                                          : Go.T("go_staff_desks_v", "{0} of at most {1}", s.Desks, Fleet.MaxDesks(s)), 220f, 22f);
            foreach (int n in new[] { 1, 5, 20 })
            {
                int k = n;
                Ui.Btn(dr, "+" + k + "  " + Go.Money(k * Cat.Desk), 200f, () => Act.BuyDesks(s, k, center),
                       () => (center ? s.CenterDesks + k <= Fleet.MaxCenterDesks(s) && Fleet.CenterOk(s) : s.Desks + k <= Fleet.MaxDesks(s)) && Money.CanPay(k * Cat.Desk));
            }
            Ui.Btn(dr, "-", 60f, () => Act.SellDesk(s, center), () => center ? s.CenterDesks > 0 : s.Desks > 0);
            Ui.Label(dr, Sc(Go.T("go_staff_desk_text", "desk, PC and headset, {0} a day each", Go.Money(Cat.DeskDay))), -1f, 19f);
        }

        static void Team(IspState s, Transform list)
        {
            if (!Sim.Test)
                Ui.Head(list, Go.T("go_staff_title", "Staff"),
                    Go.T("go_staff_text_game3", "Your staff are the game's employees, and the jobs come with the workplaces: set up Going Online's technical workplace in the office (from an office furniture store; a computer - on a desk, with a chair) and the recruitment agencies offer cable technicians, switching staff and network masters for it; the control room workplace brings switching staff and network masters, the customer center workplace customer service, technical support, field sales and network masters. Cable technicians, switching staff and customer service are at Anderson Recruitment Corp., the others at City Workforce Inc. (network masters also for your headquarters, at its computers). Everyone gets a shift at a workplace in BizMan (Schedule) - one person per workplace and shift; the game pays the wages. Vehicles: park vans at the office (within 80 m) or lease them here; each shift gets one by itself - in BizMan (Fleet) a vehicle can also be assigned to one person."));
            else
            Ui.Head(list, Go.T("go_staff_title", "Staff"),
                Go.T("go_staff_text", "Everyone works an 8-hour shift (night shift 25 % extra); more skill is faster. Vehicles: buy them at a dealer of the game and park them at the headend (within 80 m) - vans (UMC Desert, Vord V150, Mersaidi Dash, UMC Nunavut, Vord Courier D500) for technicians and switching staff, any car for network masters and field sales - or lease them here. Workplaces you set up here. A vehicle or a workplace serves one person per shift."));
            var res = Ui.Column(list, 6f, true);
            Ui.Pair(res, Go.T("go_staff_fleet2", "Vehicles at your offices"), () =>
            {
                var cars = Fleet.AtHeadend(s);
                if (cars.Count == 0) return Wc(Go.T("go_staff_nofleet", "none - park vans at the headend"));
                // GO-FLEET-2 (user 2026-11-08: "Namen bündeln Leasing-Transporter"): the same name once, with how many -
                // "5 × Leasing van" instead of the name five times; in the order the vehicles come
                var order = new List<string>();
                var count = new Dictionary<string, int>();
                foreach (var c in cars)
                {
                    string n = c.Name + (c.Van ? "" : " " + Sc("(" + Go.T("go_staff_car", "car") + ")"));
                    if (count.TryGetValue(n, out int k)) count[n] = k + 1; else { count[n] = 1; order.Add(n); }
                }
                var names = new List<string>();
                foreach (var n in order) names.Add(count[n] > 1 ? count[n] + " × " + n : n);
                return Go.T("go_staff_fleet_v", "{0} vans, {1} cars: {2}", Fleet.Vans(s), Fleet.Others(s), string.Join(", ", names));
            }, 380f);
            LeaseRow(s, res, true);
            LeaseRow(s, res, false);
            DeskRow(s, res, false);
            if (!Sim.Test)
            {   // GO-STAFF-1 (user 2026-11-08: "festlegen, wie viel ein Personal ... bedient und wie viel max. ein Meister an Personal
                // unter sich hat")
                Ui.Para(res, Sc(Go.T("go_staff_rule", "One person serves: technicians {0} customers, switching staff {1} coax customers, customer service {2} customers - each in the neighbourhood of their office or customer center (you yourself {3} at the headend, a contractor {4} more). A network master leads up to {5} people in the field; beyond that they work slower.", Cat.PerTech, Cat.PerSwitch, Cat.PerService, Cat.FounderServes, Cat.SubServes, Cat.MasterSpan)), 19f);
                foreach (var g in Places.Groups(s))
                {
                    string grp = g;
                    var gr = Ui.Row(res, 34f);
                    Ui.Label(gr, City.HoodName(grp), 300f, 20f, Color.white, FontStyles.Bold);
                    Ui.LiveLabel(gr, () => StaffLine(s, grp), -1f, 19f);
                }
            }
            var nav = Ui.Row(res);
            Ui.Btn(nav, Go.T("go_staff_route", "Route to the headend"), 340f, () => City.Navigate(s.HeadendKey), null, null, null, 19f);

            // the customer center: where the customer service and the technical support sit - GO-OFFICE-1: in the real game the
            // provider's office itself; a separate one only from 0.6
            if (!Sim.Test && (s.CenterKey.Length == 0 || s.CenterKey == s.HeadendKey)) { Members(s, list); return; }
            Ui.Head(list, Go.T("go_center_title", "Customer center"),
                Go.T("go_center_text2", "The customer service and the technical support work in the customer center, an office you rent or own; the network masters sit at the headend first. How many people fit depends on the building: one workplace per 10 m²."));
            var cc = Ui.Column(list, 6f, true);
            if (s.CenterKey.Length > 0)
            {
                Ui.Pair(cc, Go.T("go_center_where", "Customer center"), () =>
                {
                    string k = City.ByKey.TryGetValue(s.CenterKey, out var b) ? KindName(b.Kind) + ", " + Go.Num(b.Sqm) + " m²" : "";
                    return City.Name(s.CenterKey) + "  " + Sc("(" + k + ")") + (Fleet.CenterOk(s) ? "" : "  " + Bc(Go.T("go_center_lost", "no longer yours - its workplaces are unused")));
                }, 380f);
                DeskRow(s, cc, true);
                var cr = Ui.Row(cc);
                Ui.Btn(cr, Go.T("go_center_route", "Route to the customer center"), 380f, () => City.Navigate(s.CenterKey), null, null, null, 19f);
            }
            if (!Sim.Test)
                Ui.Para(cc, Sc(Go.T("go_center_old", "A customer center of its own is from Going Online 0.6 - it keeps working. New workplaces belong in the provider's office: headend and customer center under one roof.")), 20f);
            var choices = Sim.Test ? new List<House>(Fleet.CenterChoices()) : new List<House>();
            choices.RemoveAll(b => b.Key == s.CenterKey);
            if (choices.Count == 0 && s.CenterKey.Length == 0)
                Ui.Para(cc, Wc(Go.T("go_center_none2", "Rent an office for the customer center (the city map shows them) and come back.")), 21f);
            int shownC = 0;
            foreach (var b in choices)
            {
                if (shownC++ >= 8) break;
                string key = b.Key;
                var r = Ui.Row(cc, 48f);
                Ui.Label(r, City.Name(key) + "  " + Sc("(" + KindName(b.Kind) + ", " + Go.Num(b.Sqm) + " m²)"), -1f, 20f);
                Ui.Label(r, Go.T("go_center_room", "room for {0}", Fleet.Capacity(key)), 200f, 20f);
                Ui.Btn(r, s.CenterKey.Length > 0 ? Go.T("go_center_move", "Move here") : Go.T("go_center_set", "Customer center here"), 300f,
                       () => Act.SetCenter(s, key), () => Money.CanPay(Mathf.Min(s.CenterDesks, Fleet.Capacity(key)) * 200f), null, null, 19f);
            }
            Members(s, list);
        }

        /// <summary>The staff per job, the candidates (dry run), overtime and what it all costs.</summary>
        static void Members(IspState s, Transform list)
        {
            foreach (byte role in Cat.Roles)
            {
                byte rl = role;
                var mine = s.Techs.FindAll(x => x.Role == rl);
                Ui.Head(list, Act.RoleName(rl) + "  " + Sc("(" + mine.Count + ")"), RoleText(rl));
                var nr = Ui.Row(list, 44f, 10f);
                Ui.LiveLabel(nr, () =>
                {
                    int need = Sim.Need(s, rl), have = s.Techs.FindAll(x => x.Role == rl).Count;
                    if (need < 0) return Sc(Go.T("go_staff_need_free", "as many as you like"));
                    string t = Go.T("go_staff_need", "need about {0}, in the team {1}", need, have);
                    return have >= need ? Gc(t) : have >= need - 1 ? Wc(t) : Bc(t + " - " + Go.T("go_staff_short", "{0} missing", need - have));
                }, 520f, 20f);
                if (rl == Cat.RoleTech || rl == Cat.RoleSwitch || rl == Cat.RoleService)
                {
                    float price = rl == Cat.RoleTech ? Cat.SubTechJob : rl == Cat.RoleSwitch ? Cat.SubSwitchJob : Cat.SubOrderJob;
                    int cap = rl == Cat.RoleTech ? Cat.SubTechCap : rl == Cat.RoleSwitch ? Cat.SubSwitchCap : Cat.SubServiceCap;
                    Ui.Btn(nr, "", 380f, () =>
                    {
                        if (rl == Cat.RoleTech) s.SubTech = !s.SubTech; else if (rl == Cat.RoleSwitch) s.SubSwitch = !s.SubSwitch; else s.SubService = !s.SubService;
                    }, null, () => Go.T("go_staff_sub", "Contractor: {0}", (rl == Cat.RoleTech ? s.SubTech : rl == Cat.RoleSwitch ? s.SubSwitch : s.SubService) ? Go.T("go_on", "on") : Go.T("go_off", "off")),
                    () => rl == Cat.RoleTech ? s.SubTech : rl == Cat.RoleSwitch ? s.SubSwitch : s.SubService, 19f);
                    Ui.Label(nr, Sc(Go.T("go_staff_sub_text", "takes jobs waiting over 8 h: {0} each, up to {1} a day", Go.Money(price), cap)), -1f, 19f);
                }
                foreach (var tech in mine)
                {
                    var t = tech;
                    var r = Ui.Row(list, 50f, 10f);
                    // GO-RENAME-1: hired staff get a name box - the new name is the game's own (BizMan, MyEmployees too)
                    if (t.Emp.Length > 0) Ui.Field(r, t.Name, 260f, v => Crew.Rename(t, v), Crew.NameLimit);
                    else Ui.Label(r, t.Name, 260f, 21f, Color.white, FontStyles.Bold);
                    Ui.Label(r, Ui.Stars(t.Skill), 140f, 20f);
                    Ui.Label(r, Go.T("go_team_wage", "{0}/h", Go.Money(t.Wage)), 110f, 20f);
                    if (rl == Cat.RoleTech)
                    {
                        Ui.LiveLabel(r, () => Manner(t.Manner) + (t.ComplaintDays.Count > 0 ? " " + Bc(Go.T("go_staff_complaints", "{0} complaints", t.ComplaintDays.Count)) : ""), 260f, 19f);
                        Ui.Btn(r, "", 230f, () => Act.Train(s, t), () => Act.CanTrain(s, t) && Money.CanPay(Cat.Training),
                               () => t.Manner >= 5 ? Go.T("go_staff_trained", "very friendly") : Go.T("go_staff_train", "Training {0}", Go.Money(Cat.Training)), null, 18f);
                    }
                    if (rl == Cat.RoleMaster)
                        Ui.Btn(r, "", 280f, () => Act.NextHood(s, t), () => s.Hoods.Count > 1 && !Places.HubMaster(s, t), () => t.Hood.Length > 0 ? City.HoodName(t.Hood) : Go.T("go_staff_nohood", "no neighbourhood"));
                    else if (t.Emp.Length > 0) Ui.Label(r, Sc(Go.T("go_staff_shift_game", "shift in BizMan")), 220f, 19f);
                    else Ui.Btn(r, "", 220f, () => Act.NextShift(s, t), null, () => Act.ShiftName(t.Shift));
                    Ui.LiveLabel(r, () =>
                    {
                        string miss = Missing(t);
                        if (miss.Length > 0) return miss;
                        if (rl == Cat.RoleMaster) return Gc(Go.T("go_staff_master_on", "on duty")) + (t.Car.Length > 0 ? " " + Sc("(" + t.Car + ")") : "");
                        if (rl == Cat.RoleService || rl == Cat.RoleSupport) return Gc(Go.T("go_staff_desk_on", "at the desk"));
                        var k = t.Ticket != 0 ? s.Tickets.Find(x => x.Id == t.Ticket) : null;
                        if (k == null) return Gc(Go.T("go_team_free", "free")) + (t.Car.Length > 0 ? " " + Sc("(" + t.Car + ")") : "");
                        string where = k.Site.Length > 0 ? City.Name(k.Site) : City.HoodName(k.Hood);
                        return (t.Travel > 0f ? Go.T("go_team_driving", "driving to") : Go.T("go_team_at", "at")) + " " + where + " " + Sc("(" + Sim.FaultName(k.Type) + ")");
                    }, -1f, 19f);
                    if (t.Emp.Length == 0) Ui.Btn(r, Go.T("go_team_fire", "Dismiss"), 150f, () => Act.Fire(s, t));
                }
                foreach (var cand in s.Candidates.FindAll(c => c.Role == rl))
                {
                    var c = cand;
                    var r = Ui.Row(list, 46f, 10f);
                    Ui.Label(r, Sc(Go.T("go_staff_applies", "applies:")) + " " + c.Name, 360f, 20f);
                    Ui.Label(r, Ui.Stars(c.Skill), 140f, 20f);
                    if (rl == Cat.RoleTech) Ui.Label(r, Manner(c.Manner), 200f, 19f);
                    Ui.Label(r, Go.T("go_team_wage", "{0}/h", Go.Money(c.Wage)), 110f, 20f);
                    Ui.Label(r, Sc(Go.T("go_team_perday", "about {0} a day", Go.Money(8f * c.Wage))), 220f, 19f);
                    Ui.Btn(r, Go.T("go_team_hire", "Hire"), 160f, () => Act.Hire(s, c), () => s.Techs.Count < 120);
                }
            }
            Ui.Head(list, Go.T("go_staff_overtime", "Overtime"), Go.T("go_staff_overtime_text", "Everyone works 10 instead of 8 hours - a quarter more work done, the two extra hours at time and a half. Good for a few days, dear for long."));
            var ot = Ui.Row(list);
            Ui.Btn(ot, "", 320f, () => s.Overtime = !s.Overtime, null, () => Go.T("go_staff_overtime_btn", "Overtime: {0}", s.Overtime ? Go.T("go_on", "on") : Go.T("go_off", "off")), () => s.Overtime);
            Ui.LivePara(list, () =>
            {
                float sum = 0f;
                foreach (var t in s.Techs) if (t.Emp.Length == 0) sum += 8f * t.Wage * (t.Shift == 2 && t.Role != Cat.RoleMaster ? Cat.NightBonus : 1f) * (s.Overtime ? Cat.OvertimeWage : 1f);
                return Go.T("go_staff_cost", "The staff costs {0} a day in wages, workplaces {1}, fuel about {2} per vehicle and shift.", Go.Money(sum), Go.Money((s.Desks + s.CenterDesks) * Cat.DeskDay), Go.Money(Cat.FuelDay));
            }, 21f);
        }

        // ── finance ──────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Leased vans or cars: how many, one more or one less, the price per day.</summary>
        static void LeaseRow(IspState s, Transform list, bool vans)
        {
            var r = Ui.Row(list, 50f);
            Ui.Label(r, vans ? Go.T("go_lease_vans", "Leased vans") : Go.T("go_lease_cars", "Leased cars"), 380f, 21f, Ui.Sub);
            Ui.Btn(r, "-", 60f, () => Act.Lease(s, vans, -1), () => (vans ? s.LeaseVans : s.LeaseCars) > 0, null, null, 22f);
            Ui.LiveLabel(r, () => Go.Num(vans ? s.LeaseVans : s.LeaseCars), 70f, 22f).alignment = TextAlignmentOptions.Center;
            Ui.Btn(r, "+", 60f, () => Act.Lease(s, vans, 1), null, null, null, 22f);
            Ui.Label(r, Sc(vans ? Go.T("go_lease_price_van2", "{0} a day each, no purchase - they drive on the city map only (the row \"Service vans (Going Online)\"); the service vans you buy you can drive yourself", Go.Money(Cat.VanLease))
                                : Go.T("go_lease_price", "{0} a day each, no purchase - they always stand at the headend", Go.Money(Cat.CarLease))), -1f, 19f);
            if (vans)   // GO-VAN-2/4
                Ui.Para(list, Sc(Go.T("go_van_text2", "Going Online's service van (in your livery - tab \"Service van\") is also for sale at the car dealers that sell vans, {0}. When someone from the technical staff works at a customer's, their vehicle stands there until the work is done; the roof beacon flashes.", Go.Money(Van.Price))), 19f);
        }

        // ── GO-LIVERY-1: the service vans' livery ───────────────────────────────────────────────────────────────

        /// <summary>GO-LIVERY-1 (user 2026-11-10: "Ja" to Entwurf 1 in the game, adjustable in the app): the picture of the
        /// livery (the left side and the back as the game paints them), the motif, the panel's colour, the logo, the two signs
        /// on the doors, the slogan and the contact line. The offers follow the equipment in the headend.</summary>
        static void VanTab(IspState s, Transform list)
        {
            var st = s.Style ??= new VanStyle();
            Ui.Head(list, Go.T("go_van_title", "Your service vans"),
                Go.T("go_van_head", "This is how your service vans look: the ones bought at the car dealers that sell vans ({0}) and those of your technical staff in the city's traffic. Leased vans drive as points on the city map. Other players see your vans like this too.", Go.Money(Van.Price)));
            // GO-LIVERY-2 (user 2026-11-10: "die Fahrzeug-Umrandungen fehlen?"): the van itself, drawn from its model, over
            // the livery's field - without the model the field alone, as before
            var dl = Livery.DrawingPlan(0);
            var db = Livery.DrawingPlan(1);
            bool drawn = dl != null && db != null;
            RawImage side, back;
            RawImage? sideDraw = null, backDraw = null;
            if (drawn)
            {
                const float ph = 360f;
                var pic = Ui.Row(list, ph, 16f);
                var sideBox = Ui.Picture(pic, ph * dl!.Aspect, ph);
                var backBox = Ui.Picture(pic, ph * db!.Aspect, ph);
                sideBox.color = backBox.color = PictureGround;   // the drawing's own background, until it is painted
                side = Layer(sideBox, dl.Field());
                back = Layer(backBox, db.Field());
                sideDraw = Layer(sideBox, (0f, 0f, 1f, 1f));
                backDraw = Layer(backBox, (0f, 0f, 1f, 1f));
            }
            else
            {
                var pic = Ui.Row(list, 300f, 16f);
                side = Ui.Picture(pic, 300f * 2048f / 706f, 300f);
                back = Ui.Picture(pic, 300f * 722f / 616f, 300f);
            }
            side.color = back.color = new Color(1f, 1f, 1f, 0f);
            Ui.Live.Add(() =>
            {
                if (side == null || back == null) return;
                if (sideDraw != null && sideDraw.texture == null)
                {
                    var t0 = Livery.DrawingTexture(0);
                    if (t0 != null) { sideDraw.texture = t0; sideDraw.color = Color.white; }
                }
                if (backDraw != null && backDraw.texture == null)
                {
                    var t1 = Livery.DrawingTexture(1);
                    if (t1 != null) { backDraw.texture = t1; backDraw.color = Color.white; }
                }
                var tpl = Van.PaintTemplate();
                if (tpl == null) return;
                var tex = Livery.Preview(tpl, out var rs, out var rb, out bool none);
                if (none)
                {   // no livery picture (no provider yet, "logo only"): with the drawing a plain white van, else nothing
                    if (drawn) { side.texture = null; back.texture = null; side.color = back.color = PlainPaint; }
                    else side.color = back.color = new Color(1f, 1f, 1f, 0f);
                    return;
                }
                if (tex == null) return;   // painted in a later frame
                if (side.texture != tex) { side.texture = tex; back.texture = tex; }
                side.uvRect = drawn && dl!.Mirror ? Mirrored(rs) : rs;
                back.uvRect = drawn && db!.Mirror ? Mirrored(rb) : rb;
                side.color = back.color = Color.white;
            });
            // the motif
            var mr = Ui.Row(list, 50f, 10f);
            Ui.Label(mr, Go.T("go_van_motif", "Picture"), 220f, 21f, Ui.Sub);
            string[] motifs = { Go.T("go_van_motif_0", "Fibre light"), Go.T("go_van_motif_1", "Technicians at work"), Go.T("go_van_motif_2", "Lettering only"), Go.T("go_van_motif_3", "Logo only") };
            for (int i = 0; i < motifs.Length; i++)
            {
                int m = i;
                Ui.Btn(mr, motifs[i], 250f, () => st.Motif = m, null, null, () => st.Motif == m, 20f);
            }
            // GO-LIVERY-4 (user 2026-11-10: "von den Logos passt die Größe nicht - mach unter Gestaltung einen Schieberegler
            // für die Schriftgröße und Logogröße"): the lettering's and the logo's size
            var zr = Ui.Row(list, 50f, 10f);
            Ui.Label(zr, Go.T("go_van_textsize", "Text size"), 220f, 21f, Ui.Sub);
            Ui.Slider(zr, 320f, 50, 200, 5, () => st.TextPct, v => st.TextPct = v);
            Ui.LiveLabel(zr, () => st.TextPct + " %", 90f, 21f);
            Ui.Label(zr, Go.T("go_van_logosize", "Logo size"), 200f, 21f, Ui.Sub);
            Ui.Slider(zr, 320f, 50, 200, 5, () => st.LogoPct, v => st.LogoPct = v);
            Ui.LiveLabel(zr, () => st.LogoPct + " %", 90f, 21f);
            // the colour of the panel and the lettering
            var cr = Ui.Row(list, 50f, 6f);
            Ui.Label(cr, Go.T("go_van_panel", "Colour of the panel"), 220f, 21f, Ui.Sub);
            foreach (int rgb in Livery.Palette)
            {
                int c = rgb;
                Ui.Swatch(cr, new Color(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f, 1f), 46f, () => st.Panel = c, () => st.Panel == c);
            }
            // the switches
            var tr = Ui.Row(list, 50f, 10f);
            Ui.Btn(tr, "", 340f, () => st.Logo = !st.Logo, null, () => Go.T("go_van_logo", "Business logo on the panel: {0}", st.Logo ? Go.T("go_on", "on") : Go.T("go_off", "off")), () => st.Logo, 20f);
            Ui.Btn(tr, "", 420f, () => st.Tech = !st.Tech, null, () => Go.T("go_van_tech", "\"Technicians at work\" on the front doors: {0}", st.Tech ? Go.T("go_on", "on") : Go.T("go_off", "off")), () => st.Tech, 20f);
            Ui.Btn(tr, "", 340f, () => st.Cable = !st.Cable, null, () => Go.T("go_van_cable", "Cable on the sliding doors: {0}", st.Cable ? Go.T("go_on", "on") : Go.T("go_off", "off")), () => st.Cable, 20f);
            // the texts
            var sr = Ui.Row(list, 50f, 10f);
            Ui.Label(sr, Go.T("go_van_slogan", "Slogan"), 220f, 21f, Ui.Sub);
            Ui.Field(sr, st.Slogan, 520f, v => st.Slogan = (v ?? "").Trim(), 32, LiveryPaint.DefaultSlogan(Livery.CompanyName(s)));
            var kr = Ui.Row(list, 50f, 10f);
            Ui.Label(kr, Go.T("go_van_contact", "Contact line"), 220f, 21f, Ui.Sub);
            Ui.Field(kr, st.Contact, 820f, v => st.Contact = (v ?? "").Trim(), 56, Livery.DefaultContact());
            // the offers and where the colour comes from
            Ui.LivePara(list, () =>
            {
                string names = Livery.OfferNames(Livery.ServicesOf(s));
                return Go.T("go_van_offers", "Offers on the van (from the equipment in your headend): {0}", names.Length > 0 ? names
                    : Go.T("go_van_offers_none", "none yet - they come with the equipment: CMTS internet, OLT fibre, satellite headend TV, IPTV platform IPTV, voice platform telephone, CA system pay TV"));
            }, 20f);
            Ui.Para(list, Sc(Go.T("go_van_paint_note", "The van's colour is chosen when buying it at the car dealer (the game's colour choice). The car dealers with vans show a service van in your livery - a click on it opens the purchase.")), 19f);
            if (GameUi.InMultiplayerMod)   // CAR-LIVERY-1: Going Public Server - each van its own livery, each side its look
                Ui.Para(list, Sc(Go.T("go_van_each_note", "Each van can have a livery of its own, each side its look and colour, and a new paint: sit in the van and choose \"Livery\" beside \"Park Vehicle\". This page's look stays for the vans without one.")), 19f);
            var rr = Ui.Row(list, 50f, 10f);
            Ui.Btn(rr, Go.T("go_van_reset", "Default look"), 260f, () =>
            {   // the same object (the page's buttons hold it); the page is built anew for the text boxes
                var d = new VanStyle();
                st.Motif = d.Motif; st.Panel = d.Panel; st.Logo = d.Logo; st.Tech = d.Tech; st.Cable = d.Cable;
                st.Slogan = d.Slogan; st.Contact = d.Contact;
                st.TextPct = d.TextPct; st.LogoPct = d.LogoPct;   // GO-LIVERY-4
                _vanEpoch++;
            });
        }

        static int _vanEpoch;

        // GO-LIVERY-2: the drawing's background (VanPicture's) and the van's plain paint (Livery.White)
        static readonly Color PictureGround = new Color(228f / 255f, 232f / 255f, 236f / 255f, 1f);
        static readonly Color PlainPaint = new Color(0.949f, 0.949f, 0.949f, 1f);

        /// <summary>GO-LIVERY-2: a picture over a part of another one (x, y, width, height from its bottom left, 0..1).</summary>
        static RawImage Layer(RawImage box, (float x, float y, float w, float h) at)
        {
            var go = new GameObject("Layer", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(box.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(at.x, at.y);
            rt.anchorMax = new Vector2(at.x + at.w, at.y + at.h);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var raw = go.GetComponent<RawImage>();
            raw.raycastTarget = false;
            raw.color = new Color(1f, 1f, 1f, 0f);
            return raw;
        }

        static Rect Mirrored(Rect r) => new Rect(r.x + r.width, r.y, -r.width, r.height);

        /// <summary>The first steps of a young company, ticked off as they are done.</summary>
        /// <summary>GAME-TYPES-1: the cable provider is a business of the game - founded in BizMan.</summary>
        static void FoundingGame(Transform list)
        {
            Ui.Head(list, Go.T("go_found_title", "Become a cable network operator"),
                Go.T("go_found_text", "Buy bandwidth, connect the city's buildings and sell internet, TV and phone lines - against Skyline Cable, the city's old cable company. You start small with DOCSIS 3.0; DOCSIS 3.1 and fiber to the home (XGS-PON) come later."));
            var c = Ui.Column(list, 6f, true);
            // GO-OFFICE-1: one office - the headend, the customer center and the workplaces under one roof
            Ui.Para(c, Go.T("go_found_office_1", "1. Rent or buy an office - it becomes the cable provider's home: the headend and the customer center under one roof."), 21f);
            Ui.Para(c, Go.T("go_found_office_2", "2. Open it in BizMan: \"Start new business\" - \"Cable provider\", type the company's name, \"Start business\". Like every office business it needs the course \"Office Businesses\" of the Manhattan Business School. Going Online founds the provider with it."), 21f);
            Ui.Para(c, Go.T("go_found_office_3b", "3. Set the office up like every office business: a CMTS and Going Online's technical workplace (from {0}) - the workplace is a computer, put it on a desk with a chair - and toilets and sinks as the building's size asks (BizMan, Insight: requirements). The recruitment agencies offer the jobs whose workplaces stand there - hire a cable technician at Anderson Recruitment Corp. and give them a shift at the workplace in BizMan.", Stores(GameTypes.FurnitureStores)), 21f);
            Ui.Para(c, Go.T("go_found_office_4", "4. Open a customer center: rent a shop in the same neighbourhood and found \"Cable customer center\" there in BizMan - customer service works there, the devices lie there. Every neighbourhood needs an office of yours and a customer center before customers sign there; an office in another neighbourhood becomes a hub with its own CMTS once you build its feeder fibre from the headend."), 21f);
            Ui.Para(c, Sc(Go.T("go_found_office_more2", "Vans park at your offices (within 80 m) or are leased in the app; the devices for new customers lie on storage shelves in your customer centers or offices.")), 20f);
            if (SaveCarrier.HasUnreadable)
                Ui.Para(list, Bc(Go.T("go_found_unreadable", "This save has a provider that could not be read. It stays in the save untouched; founding a new one replaces it.")), 20f);
        }

        static void FirstSteps(IspState s, Transform list)
        {
            int age = Sim.Day() - (int)(s.FoundedHour / 24);
            bool sites = s.Sites.Count > 0, tech = s.Techs.Exists(t => t.Role == Cat.RoleTech || t.Role == Cat.RoleSwitch), van = Fleet.Vans(s) > 0, customers = Sim.Customers(s) > 0;
            bool cmts = Sim.Test || !s.NoCmts;
            bool center = Sim.Test || Places.HasCenter(s, Places.HeadHood(s));   // GO-CENTERS-1
            if (age > 15 && sites && tech && van && customers && cmts && center) return;
            Ui.Head(list, Go.T("go_steps_title", "First steps"), Go.T("go_steps_text", "A small start is enough: one neighbourhood, a few big buildings, one person in the field. The rest grows with the income."));
            var c = Ui.Column(list, 4f, true);
            void Step(bool done, string text) => Ui.Para(c, (done ? Gc(Go.T("go_steps_done", "done")) : Wc(Go.T("go_steps_open", "to do"))) + "  " + text, 20f);
            if (!Sim.Test) Step(cmts, Go.T("go_steps_cmts", "Set up a CMTS in the headend - from {0}; without it the network does not run.", Stores(GameTypes.FurnitureStores)));
            if (!Sim.Test) Step(center, Go.T("go_steps_center", "Open a customer center in the headend's neighbourhood: rent a shop, found \"Cable customer center\" in BizMan and set up a customer workplace there - without it no customers sign."));
            Step(sites, Go.T("go_steps_build", "Connect a few big buildings near the headend (tab Buildings) - each neighbourhood first needs a node."));
            Step(tech, Sim.Test ? Go.T("go_steps_hire", "Hire a technician or switching staff (tab Staff) - they switch on the new customers in the buildings.")
                                : Go.T("go_steps_hire_office", "Set up a technical workplace in the office (office furniture store) - then Anderson Recruitment Corp. offers cable technicians and switching staff; hire one and give them a shift in BizMan."));
            Step(van, Go.T("go_steps_van", "A van at the headend: lease one (tab Staff) or buy one at a dealer and park it there."));
            Step(customers, Go.T("go_steps_customers", "The first customers sign: until you have staff for it, you take the orders yourself between 8 and 20 o'clock and switch the connections on."));
        }

        /// <summary>The competitor: its offers, how it stands in each of the provider's neighbourhoods, what it did.</summary>
        static void RivalTab(IspState s, Transform list)
        {
            var r = s.Rival;
            Ui.Head(list, Rival.Name, Go.T("go_rival_text", "The city's old cable company. At first it has every household, and it fights back where you take its customers: it cuts its prices, tries to win customers back with switching bonuses, upgrades its network, signs exclusive contracts with the owners of buildings you have not connected yet and bids in every tender. On a day its own network fails, many of its customers look for someone else. How hard it fights follows the game's setting for rivals and the mod option."));
            var c = Ui.Column(list, 6f, true);
            var rr = Ui.Row(c, 40f);
            Ui.Label(rr, Go.T("go_rival_rep", "Its reputation"), 420f, 21f, Ui.Sub);
            Ui.Bar(rr, 360f, 22f, () => r.Reputation / 100f, () => r.Reputation >= 60f ? Ui.Bad : r.Reputation >= 40f ? Ui.Warn : Ui.Good);
            Ui.LiveLabel(rr, () => Go.T("go_rival_rep_v", "{0} / 100 (yours {1})", Go.Num(r.Reputation), Go.Num(s.Reputation)), -1f, 21f);
            Ui.Pair(c, Go.T("go_rival_strength", "How hard it fights"), () => Go.Pct(Rival.Strength()));
            Ui.Pair(c, Go.T("go_rival_offers", "Its prices a day (average)"), () =>
            {
                var parts = new List<string>();
                float level = Rival.PriceLevel(s, "");
                int top = 500;
                foreach (var h in s.Hoods.Keys) top = Mathf.Max(top, Rival.Top(s, h));
                foreach (int m in new[] { 50, 250, 500, 1000, 2000 })
                    if (m <= top) parts.Add(Go.Bw(m) + " " + Go.Money(Cat.RefPrice(m) * level));
                parts.Add(Go.T("go_rival_tv", "TV {0}", Go.Money(Cat.RefTv * level)));
                return string.Join(" | ", parts);
            });

            Ui.Head(list, Go.T("go_rival_hoods", "In your neighbourhoods"), Go.T("go_rival_hoods_text", "Your share of all units in the neighbourhood, its technology, its prices against the market price, and what it is doing right now."));
            int today = Sim.Day();
            foreach (var id in s.Hoods.Keys)
            {
                string hid = id;
                var card = Ui.Column(list, 4f, true);
                Ui.Para(card, City.HoodName(hid), 22f, Color.white, FontStyles.Bold);
                Ui.Pair(card, Go.T("go_rival_market", "Market"), () =>
                {
                    int units = 0, ours = 0;
                    foreach (var b in City.All) if (b.Connectable && b.Hood == hid) units += b.Units;
                    foreach (var site in s.Sites.Values) if (site.Hood == hid) ours += Sim.CustomersOf(site);
                    float share = units > 0 ? Mathf.Clamp01(ours / (float)units) : 0f;
                    return Go.T("go_rival_market_v", "you {0} of {1} units ({2}) | {3} {4}", Go.Num(ours), Go.Num(units), Go.Pct(share), Rival.Name, Go.Pct(1f - share));
                });
                Ui.Pair(card, Go.T("go_rival_tech", "Its network"), () =>
                {
                    var h = Rival.Hood(s, hid);
                    string t = Go.T("go_rival_tech_v", "up to {0}", Go.Bw(Rival.TopMbit[h.Tech]));
                    if (h.TechDay >= 0) t += " | " + Wc(Go.T("go_rival_tech_soon", "{0} from day {1}", Go.Bw(Rival.TopMbit[Mathf.Min(h.Tech + 1, Rival.TopMbit.Length - 1)]), h.TechDay));
                    return t;
                });
                Ui.Pair(card, Go.T("go_rival_prices", "Its prices"), () =>
                {
                    var h = Rival.Hood(s, hid);
                    var parts = new List<string> { Go.T("go_rival_level", "{0} of the market price", Go.Pct(h.Price)) };
                    if (h.PriceUntil >= Sim.Day()) parts.Add(Wc(Go.T("go_rival_cut_until", "price cut until day {0}", h.PriceUntil)));
                    if (h.PromoUntil >= Sim.Day()) parts.Add(Wc(Go.T("go_rival_promo_until", "win-back campaign until day {0}", h.PromoUntil)));
                    if (h.OutageUntil >= Sim.Day()) parts.Add(Gc(Go.T("go_rival_outage_now", "its network is down today")));
                    return string.Join(" | ", parts);
                });
                Ui.Pair(card, Go.T("go_rival_locked", "Exclusive contracts"), () =>
                {
                    int n = 0;
                    foreach (var kv in r.Locked) if (kv.Value >= Sim.Day() && City.ByKey.TryGetValue(kv.Key, out var b) && b.Hood == hid) n++;
                    return n == 0 ? "-" : Go.T("go_rival_locked_v", "{0} buildings (tab Buildings shows until when)", n);
                });
            }
            if (s.Hoods.Count == 0) Ui.Para(list, Sc(Go.T("go_rival_none", "Connect your first building - then you meet it.")), 21f);

            Ui.Head(list, Go.T("go_rival_log", "What it did"));
            var log = Ui.Column(list, 2f, true);
            if (r.Log.Count == 0) Ui.Para(log, Sc(Go.T("go_rival_quiet", "nothing yet - it reacts when you take its customers")), 20f);
            foreach (var line in r.Log) Ui.Para(log, line, 19f);
        }

        static void Finance(IspState s, Transform list)
        {
            Ui.Head(list, Go.T("go_fin_title", "Finance"),
                Go.T("go_fin_text", "The bill runs at midnight. Every item is also in the game's finance app (EconoView). The levy is the game's tax rate on a day with a profit; investments are paid at once."));
            var y = Yesterday(s);
            if (y != null)
            {
                var c = Ui.Column(list, 2f, true);
                Ui.Para(c, Go.T("go_fin_day", "Day {0}", y.Day), 23f, Color.white, FontStyles.Bold);
                Line(c, Go.T("go_fin_subs", "Subscriptions"), y.Subs);
                Line(c, Go.T("go_fin_rent", "Device rent"), y.Rent);
                Line(c, Go.T("go_fin_sold", "Devices sold"), y.Sold);
                Line(c, Go.T("go_fin_paytv", "Pay-TV"), y.PayTvIn);
                Line(c, Go.T("go_fin_fees", "Connection fees"), y.Fees);
                Line(c, Go.T("go_fin_movefees", "Move fees"), y.MoveFees);
                Line(c, Go.T("go_fin_servicefees", "Fault service charges"), y.ServiceFees);
                Line(c, Go.T("go_fin_transit", "Bandwidth"), -y.Transit);
                Line(c, Go.T("go_fin_paytvbuy", "Pay-TV wholesale"), -y.PayTvOut);
                Line(c, Go.T("go_fin_wages", "Staff"), -y.Wages);
                Line(c, Go.T("go_fin_comp", "Compensation"), -y.Comp);
                Line(c, Go.T("go_fin_upkeep", "Maintenance and sales"), -y.Upkeep);
                Line(c, Go.T("go_fin_ship", "Shipping and replacements"), -y.Ship);
                Line(c, Go.T("go_fin_phone", "Interconnection and porting"), -y.Phone);
                Line(c, Go.T("go_fin_fleet", "Fuel and workplaces"), -y.Fleet);
                Line(c, Go.T("go_fin_contractors", "Contractors"), -y.Contractors);
                Line(c, Go.T("go_fin_lease", "Leasing"), -y.Lease);
                Line(c, Go.T("go_fin_maint", "Maintenance (by the connections' age)"), -y.Maint);
                Line(c, Go.T("go_fin_repair", "Repair material"), -y.Repair);
                Line(c, Go.T("go_fin_permits", "Permission contracts"), -y.Permits);
                if (y.Power > 0.005f) Line(c, Go.T("go_fin_power", "Power and maintenance of the equipment"), -y.Power);
                if (y.Events > 0.005f) Line(c, Go.T("go_fin_events", "Events (special measures)"), -y.Events);   // GO-EVENTS-1
                if (y.EventsIn > 0.005f) Line(c, Go.T("go_fin_eventsin", "Refunds and subsidies"), y.EventsIn);
                if (y.PeerSubs > 0.005f) Line(c, Go.T("go_fin_peersubs", "Subscriptions on other players' networks"), y.PeerSubs);   // GO-WHOLESALE-2
                if (y.PeerRent > 0.005f) Line(c, Go.T("go_fin_peerrent", "Lines rented from players"), -y.PeerRent);
                if (y.PeerIn > 0.005f) Line(c, Go.T("go_fin_peerin", "Lines rented to players"), y.PeerIn);
                Line(c, Go.T("go_fin_tax", "Levy"), -y.Tax);
                Line(c, Go.T("go_fin_result", "Result"), y.Operating - y.Tax, true);
                Line(c, Go.T("go_fin_invest", "Invested (network, devices)"), -(y.Build + y.Gear));
            }
            else Ui.Para(list, Sc(Go.T("go_ov_noday", "the first bill comes at midnight")), 21f);
            Ui.LivePara(list, () =>
            {
                int n = Sim.Customers(s);
                float v = Cat.Volume(n);
                return v > 0f ? Go.T("go_fin_volume", "Volume discount {0}: with {1} customers devices, Pay-TV wholesale and interconnection are cheaper (up to 20 % at 5,000).", Go.Pct(v), Go.Num(n))
                              : Sc(Go.T("go_fin_volume_none", "From 500 customers on there is a volume discount on devices, Pay-TV wholesale and interconnection (up to 20 % at 5,000)."));
            }, 20f);
            var head = Ui.Row(list, 34f, 10f);
            foreach (var (k, w) in new[] { (Go.T("go_fin_col_day", "Day"), 100f), (Go.T("go_fin_col_in", "Income"), 180f), (Go.T("go_fin_col_run", "Running costs"), 200f),
                                            (Go.T("go_fin_col_tax", "Levy"), 140f), (Go.T("go_fin_col_res", "Result"), 180f), (Go.T("go_fin_col_inv", "Invested"), 180f),
                                            (Go.T("go_fin_col_cust", "Customers"), 150f), (Go.T("go_fin_col_new", "New"), 110f), (Go.T("go_fin_col_left", "Left"), 110f) })
                Ui.Label(head, k, w, 19f, Ui.Sub);
            int n = 0;
            foreach (var b in s.Books)
            {
                if (n++ >= 14) break;
                var r = Ui.Row(list, 40f, 10f);
                float res = b.Operating - b.Tax;
                Ui.Label(r, b.Day.ToString(), 100f, 20f);
                Ui.Label(r, Go.Money(b.Income), 180f, 20f);
                Ui.Label(r, Go.Money(b.Running), 200f, 20f);
                Ui.Label(r, Go.Money(b.Tax), 140f, 20f);
                Ui.Label(r, res >= 0 ? Gc(Go.Money(res)) : Bc(Go.Money(res)), 180f, 20f);
                Ui.Label(r, Go.Money(b.Build + b.Gear), 180f, 20f);
                Ui.Label(r, Go.Num(b.Customers), 150f, 20f);
                Ui.Label(r, b.Joined.ToString(), 110f, 20f);
                Ui.Label(r, b.Left.ToString(), 110f, 20f);
            }
            if (!Sim.Test) Removal(list);
        }

        static bool _removeAsk, _dissolveAsk;

        /// <summary>GO-DISSOLVE-1 (the user chose "Nur ein Knopf in der App"): no building of the provider is left - found a
        /// cable provider in another office (the headend moves there) or end the company. Two steps, as the mod's removal.</summary>
        static void Dissolving(IspState s, Transform list)
        {
            Ui.Para(list, Go.T("go_dissolve_text", "No office of {0} is left. Found a \"Cable provider\" in another office in BizMan and the headend moves there - or dissolve the company: the network, the customers, the contracts and the leased vehicles end, and the app starts anew. Your staff, the devices on the shelves and your money stay.", s.Company), 21f);
            var r = Ui.Row(list, 52f);
            if (!_dissolveAsk)
                Ui.Btn(r, Go.T("go_dissolve_btn", "Dissolve the company"), 420f, () => { _dissolveAsk = true; App.Dirty(); }, null, null, null, 20f);
            else
            {
                Ui.Label(r, Wc(Go.T("go_dissolve_sure", "Really? This cannot be undone.")), 420f, 21f);
                Ui.Btn(r, Go.T("go_dissolve_yes", "Yes, dissolve"), 300f, () => { _dissolveAsk = false; Act.Dissolve(s); App.Dirty(); }, null, null, null, 20f);
                Ui.Btn(r, Go.T("go_remove_no", "Cancel"), 200f, () => { _dissolveAsk = false; App.Dirty(); }, null, null, null, 20f);
            }
        }

        /// <summary>GAME-TYPES-1 (Spotless showed the need: a save with a mod's business type needs the mod): before taking the
        /// mod out, everything of it in this save becomes the game's own - then save, then remove the mod.</summary>
        static void Removal(Transform list)
        {
            Ui.Head(list, Go.T("go_remove_title", "Remove the mod safely"),
                Go.T("go_remove_text2", "A save with a cable provider needs this mod. Before you remove it: press the button, save the game, then remove the mod. The cable provider becomes an empty office (a headend of 0.6 a warehouse), a customer center an empty building; the equipment and the workplaces become the game's furniture they were made from; devices in storage are gone; your staff keep working with the game's jobs. The provider itself stays in the save and is found again if the mod comes back."));
            var r = Ui.Row(list, 52f);
            if (!_removeAsk)
                Ui.Btn(r, Go.T("go_remove_btn", "Prepare the removal"), 420f, () => { _removeAsk = true; App.Dirty(); }, null, null, null, 20f);
            else
            {
                Ui.Label(r, Wc(Go.T("go_remove_sure", "Really? This cannot be undone.")), 420f, 21f);
                Ui.Btn(r, Go.T("go_remove_yes", "Yes, prepare it"), 300f, () =>
                {
                    _removeAsk = false;
                    var (b, f, d, e) = GameTypes.Uninstall();
                    Notify.Good(Go.T("go_remove_done", "Ready: {0} businesses, {1} pieces of furniture, {2} device lots and {3} jobs are the game's own now. Save the game, then remove the mod.", b, f, d, e), 12);
                    App.Dirty();
                }, null, null, null, 20f);
                Ui.Btn(r, Go.T("go_remove_no", "Cancel"), 200f, () => { _removeAsk = false; App.Dirty(); }, null, null, null, 20f);
            }
        }

        static void Line(Transform parent, string key, float v, bool bold = false)
        {
            if (!bold && v > -0.005f && v < 0.005f) return;
            var r = Ui.Row(parent, 34f);
            Ui.Label(r, key, 420f, 21f, bold ? Color.white : Ui.Sub, bold ? FontStyles.Bold : FontStyles.Normal);
            Ui.Label(r, v >= 0 ? Gc(Go.Money(v)) : Bc(Go.Money(v)), 220f, 21f, null, bold ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.MidlineRight);
        }
    }
}
