using System.Collections.Generic;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>Going Online 0.8, step 5 (the user chose all four from the research, 2026-11-08, then "beides fertigstellen"):
    ///
    /// GO-CACHE-1 - streaming caches: streaming services put cache servers into providers' networks free of charge; filled at
    /// night, they serve the evening from inside the network (Analysys Mason 2022). The server is a piece of furniture for the
    /// headend (the provider pays its installation and power); it works from Cat.CacheMin internet customers and takes a
    /// fifth of the traffic off the transit (two of them at most), at the price of some filling traffic at night.
    ///
    /// GO-WHOLESALE-1 - renting the network: a provider without a cable network of its own (Cat.WsName, invented - as O2 sells
    /// internet over Vodafone's cable network since 2019) offers to rent lines once the provider has Cat.WsMinCustomers
    /// customers. Accepted, its lines grow towards Cat.WsShare of the homes passed where the network is looked after - fewer
    /// at a dear price or on slow evenings; they pay per line and day, load the nodes and the CMTS's places like customers of
    /// ours (their traffic leaves through their own transit), a little of our own growth goes to them, and an outage costs a
    /// penalty. A player as the renter comes later (the multiplayer protocol).
    ///
    /// GO-SLA-1 - business tariffs with a repair time: only firms take them, they are worth more to them, their faults go first,
    /// and more than Cat.SlaHours off in a day costs half a day's price per such customer.
    ///
    /// GO-BULK-1 - bulk contracts: the owner of a residential building with Cat.BulkMinHomes homes or more pays for TV in every
    /// home (two game years); the owner answers an offer the next day, by the provider's reputation; its homes need no TV
    /// tariff any more, and more of them take internet.</summary>
    internal static class Deals
    {
        static int Round(float x) { if (x <= 0f) return 0; int n = (int)x; return n + (Sim.Rand() < x - n ? 1 : 0); }

        // ── GO-CACHE-1 ────────────────────────────────────────────────────────────────────────────────────────────────

        public static int CacheUnits(IspState s) => Sim.Test ? 0 : Mathf.Min(Cat.CacheMax, Places.Units(s, Places.HeadHood(s), GameTypes.ICache, "cache"));

        public static bool CacheActive(IspState s) => CacheUnits(s) > 0 && Sim.InternetCustomers(s) >= Cat.CacheMin;

        /// <summary>What the caches take off the transit this hour (negative at night: they fill).</summary>
        public static float CacheOff(IspState s, float up, int h)
        {
            if (!CacheActive(s)) return 0f;
            int n = CacheUnits(s);
            return h >= 3 && h <= 5 ? -n * Cat.CacheFill : up * Cat.CacheShare * n;
        }

        // ── GO-WHOLESALE-1 ────────────────────────────────────────────────────────────────────────────────────────────

        public static int WsTotal(IspState s) { int n = 0; foreach (var v in s.WsLines.Values) n += v; return n; }
        public static int WsIn(IspState s, string hood) => s.WsLines.TryGetValue(hood, out var n) ? n : 0;

        public static int WsInGroup(IspState s, string g)
        {
            int n = 0;
            foreach (var kv in s.WsLines) if (Places.Group(s, kv.Key) == g) n += kv.Value;
            return n;
        }

        /// <summary>The renter's lines in a neighbourhood this hour: they surf like a 250 Mbit/s tariff of ours.</summary>
        public static float WsLoad(IspState s, string hood, int h, float grow) => WsIn(s, hood) * Cat.Tavg(250) * grow * Cat.HomeDay[h];
        public static float WsUp(IspState s, string hood, int h, float grow) => WsIn(s, hood) * (0.12f * Cat.Tavg(250) + 0.004f * 25f) * grow * (0.2f + 0.8f * Cat.HomeDay[h]);

        public static void Accept(IspState s)
        {
            if (s.Wholesale || s.WsOffered < 0) return;
            s.Wholesale = true;
            Sim.Event(s, Go.T("go_event_ws_on", "Contract with {0}: it rents lines on your network for {1} each a day.", Cat.WsName, Go.Money(s.WsPrice)));
        }

        public static void Stop(IspState s)
        {
            if (!s.Wholesale) return;
            s.Wholesale = false;
            int lines = WsTotal(s);
            s.WsLines.Clear();
            Sim.Event(s, Go.T("go_event_ws_off", "The contract with {0} has ended - its {1} lines are switched off.", Cat.WsName, Go.Num(lines)));
        }

        public static void Price(IspState s, float delta) => s.WsPrice = Mathf.Clamp(Mathf.Round((s.WsPrice + delta) * 100f) / 100f, 0.3f, 3f);

        // ── GO-SLA-1 ──────────────────────────────────────────────────────────────────────────────────────────────────

        static readonly HashSet<string> _slaSites = new HashSet<string>(), _slaHoods = new HashSet<string>();

        /// <summary>Once an hour (before the staff work): which buildings and neighbourhoods have customers with a repair time.</summary>
        public static void SlaRefresh(IspState s)
        {
            _slaSites.Clear(); _slaHoods.Clear();
            foreach (var site in s.Sites.Values)
                foreach (var c in site.C)
                    if (c.N > 0 && c.Biz && Sim.TariffOf(s, c.T)?.Sla == true) { _slaSites.Add(site.Key); _slaHoods.Add(site.Hood); break; }
        }

        /// <summary>A ticket that hits customers with a repair time (their building, or the network of their neighbourhood).</summary>
        public static bool SlaTicket(Ticket t) =>
            _slaSites.Count > 0 && (t.Site.Length > 0 ? _slaSites.Contains(t.Site) : t.Hood.Length == 0 || _slaHoods.Contains(t.Hood));

        static int SlaCustomers(IspState s, Site site, out float price)
        {
            int n = 0; price = 0f;
            foreach (var c in site.C)
            {
                var t = Sim.TariffOf(s, c.T);
                if (c.N <= 0 || !c.Biz || t == null || !t.Sla) continue;
                n += c.N; price += t.Price * c.N;
            }
            if (n > 0) price /= n;
            return n;
        }

        // ── GO-BULK-1 ─────────────────────────────────────────────────────────────────────────────────────────────────

        public static bool CanOfferBulk(IspState s, Site site) =>
            !Sim.Test && site.Homes >= Cat.BulkMinHomes && site.Coax && !site.CoaxOff && !site.Lost && site.Bulk == 0 && site.BulkAsked < 0
            && Sim.Today(s) - site.BulkNo >= 10 && Sim.TvReady(s) && Places.Covered(s, site.Hood);

        public static void OfferBulk(IspState s, string key)
        {
            if (!s.Sites.TryGetValue(key, out var site) || !CanOfferBulk(s, site)) return;
            site.BulkAsked = Sim.Today(s);
            site.BulkPrice = Cat.BulkPriceRef;
            Sim.Event(s, Go.T("go_event_bulk_offer", "Bulk contract offered to the owner of {0}: TV for its {1} homes at {2} each a day - the answer comes tomorrow.", City.Name(key), site.Homes, Go.Money(site.BulkPrice)));
        }

        // ── once a day ────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>End of a day, before the day's outages are reset: the renter's offer, lines, money and penalty; the repair
        /// time penalties; the owners' answers and the bulk contracts' money.</summary>
        public static void Day(IspState s, DayBook b, int today)
        {
            if (Sim.Test) return;
            // the outage of the day per neighbourhood (customer-hours without service / customer-hours)
            var outH = new Dictionary<string, (float o, float c)>();
            foreach (var site in s.Sites.Values)
            {
                outH.TryGetValue(site.Hood, out var v);
                outH[site.Hood] = (v.o + site.OutHours, v.c + site.CustHours);
            }

            // GO-WHOLESALE-1
            if (!s.Wholesale)
            {
                if (s.WsOffered < 0 && Sim.Customers(s) >= Cat.WsMinCustomers && s.Reputation >= 50f)
                {
                    s.WsOffered = today;
                    Sim.Event(s, Go.T("go_event_ws_offer", "{0}, a provider without a cable network of its own, wants to rent lines on your network: {1} per line and day. Network tab, \"Rent the network\".", Cat.WsName, Go.Money(s.WsPrice)));
                    Notify.Info(Go.T("go_note_ws_offer", "{0}: {1} wants to rent lines on your network - see the Network tab.", s.Company, Cat.WsName), 10);
                }
            }
            else
            {
                float priceK = Mathf.Clamp(Cat.WsPriceRef / Mathf.Max(0.1f, s.WsPrice), 0.3f, 1.5f);
                foreach (var h in s.Hoods.Values)
                {
                    int have = WsIn(s, h.Id), passed = Sim.HomesPassed(s, h.Id);
                    if (passed <= 0 || !Places.Covered(s, h.Id)) { if (have > 0) s.WsLines[h.Id] = have - Mathf.Max(1, Round(have * 0.02f)); continue; }
                    float target = passed * Cat.WsShare * priceK * (1f - h.CongYesterday);
                    int delta = Round((target - have) * Cat.WsGrow);
                    if (delta == 0 && target > have + 0.5f) delta = 1;
                    s.WsLines[h.Id] = Mathf.Max(0, have + delta);
                }
                var empty = new List<string>();
                foreach (var kv in s.WsLines) if (kv.Value <= 0) empty.Add(kv.Key);
                foreach (var k in empty) s.WsLines.Remove(k);
                b.Wholesale += WsTotal(s) * s.WsPrice;
                // their lines were off: a penalty by how long (their contract with us)
                foreach (var kv in s.WsLines)
                {
                    if (!outH.TryGetValue(kv.Key, out var v) || v.c <= 0f) continue;
                    float share = v.o / v.c;
                    if (share > 0.02f) b.Comp += kv.Value * s.WsPrice * Cat.WsPenalty * Mathf.Min(1f, share * 10f);
                }
            }

            // GO-SLA-1: more than the repair time off - half a day's price per such customer
            foreach (var site in s.Sites.Values)
            {
                int n = SlaCustomers(s, site, out float price);
                if (n <= 0 || site.CustHours <= 0f) continue;
                float hoursOff = 24f * site.OutHours / site.CustHours;
                if (hoursOff > Cat.SlaHours) b.Comp += n * price * Cat.SlaPenalty;
            }

            // GO-BULK-1: the owners' answers, the contracts' money and their end
            foreach (var site in s.Sites.Values)
            {
                if (site.BulkAsked >= 0 && today > site.BulkAsked)
                {
                    float p = Mathf.Clamp(0.3f + 0.6f * s.Reputation / 100f, 0.2f, 0.85f);
                    site.BulkAsked = -1;
                    if (Sim.Rand() < p && !site.Lost)
                    {
                        site.Bulk = site.Homes;
                        site.BulkUntil = today + Sim.PermitDays();
                        Sim.Event(s, Go.T("go_event_bulk_yes", "The owner of {0} signed the bulk contract: TV for {1} homes until day {2}.", City.Name(site.Key), site.Bulk, site.BulkUntil));
                    }
                    else
                    {
                        site.BulkNo = today;
                        Sim.Event(s, Go.T("go_event_bulk_no", "The owner of {0} does not want a bulk contract for now.", City.Name(site.Key)));
                    }
                }
                if (site.Bulk <= 0) continue;
                if (site.Lost || site.CoaxOff || today > site.BulkUntil)
                {
                    bool renew = !site.Lost && !site.CoaxOff && Sim.Rand() < 0.7f;
                    if (renew) { site.BulkUntil = today + Sim.PermitDays(); Sim.Event(s, Go.T("go_event_bulk_renew", "The bulk contract of {0} was renewed until day {1}.", City.Name(site.Key), site.BulkUntil)); }
                    else { site.Bulk = 0; site.BulkUntil = -1; Sim.Event(s, Go.T("go_event_bulk_end", "The bulk contract of {0} has ended.", City.Name(site.Key))); continue; }
                }
                b.Bulk += site.Bulk * site.BulkPrice;
            }
        }

        public static int BulkHomes(IspState s) { int n = 0; foreach (var site in s.Sites.Values) n += site.Bulk; return n; }
    }
}
