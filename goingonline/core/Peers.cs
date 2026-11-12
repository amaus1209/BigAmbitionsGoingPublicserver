using System.Collections.Generic;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GO-WHOLESALE-2: one neighbourhood of an offer - its homes passed and yesterday's quality.</summary>
    public sealed class PeerHood
    {
        public string Id = "", Name = "";
        public int Homes;
        public float Out, Cong;           // shares of yesterday's customer-hours without service / slowed down
    }

    /// <summary>GO-WHOLESALE-2: another player's network on offer (Going Public Server sends them round).</summary>
    public sealed class PeerOffer
    {
        public string Owner = "", Company = "";
        public float Price;               // per line and day
        public List<PeerHood> Hoods = new List<PeerHood>();
    }

    /// <summary>GO-WHOLESALE-2: a renter's day on an owner's network - what the renter paid for which lines.</summary>
    public sealed class PeerBill
    {
        public string Owner = "", Renter = "", Company = "";
        public int Day;
        public Dictionary<string, int> Lines = new Dictionary<string, int>();
        public float Amount;
    }

    /// <summary>GO-WHOLESALE-2 (user 2026-11-11, "Spielinhalts-Verbesserungen" - chose "Leitungen an Mitspieler"): lines
    /// between players in Going Public Server, as GO-WHOLESALE-1 does with the computer's Funkwelle Mobil.
    /// - The owner offers the neighbourhoods its network serves (looked after there: an office and a customer center) at a
    ///   price per line and day; the offer rides on the provider report to the server, which sends all offers round.
    /// - A renter takes an offer: in its neighbourhoods where the renter has no network of its own, its lines grow day by
    ///   day towards 5 % of the homes passed (more with a good reputation, less where the owner's network fails), and the
    ///   people on them pay the renter's average tariff price. Each day's end the renter pays the owner's price for its
    ///   lines (its finances: "Leitungen von Mitspielern") and sends the bill through the server.
    /// - The owner books the bill when it arrives (the server keeps it until the owner is online; each day once) as
    ///   "Leitungen an Mitspieler"; the renter's lines load its nodes like its own customers' (Sim.Load).
    /// - An offer that is gone (withdrawn, or the owner's provider is gone) pauses the contract; after 7 days it ends.
    /// Nothing of this in the stand-alone mod (no other players) or the developer's dry run.</summary>
    internal static class Peers
    {
        /// <summary>Going Public Server: the other players' offers (the server's latest list).</summary>
        public static System.Func<List<PeerOffer>>? Offers;
        /// <summary>Going Public Server: a day's bill to the server, for the network's owner.</summary>
        public static System.Action<PeerBill>? SendBill;
        /// <summary>Going Public Server: this player's id (the one the offers carry as their owner).</summary>
        public static System.Func<string>? Me;

        public static bool Available
        {
            get
            {
                if (Offers == null || SendBill == null || Me == null) return false;
                try { return !string.IsNullOrEmpty(Me()); } catch { return false; }
            }
        }

        const float Share = 0.05f, Grow = 0.08f, Shrink = 0.15f, OutLoss = 0.3f;
        const int PauseDays = 7;

        public static List<PeerOffer> Others()
        {
            var list = new List<PeerOffer>();
            if (!Available) return list;
            string me = "";
            try { me = Me!(); } catch { }
            try { foreach (var o in Offers!() ?? new List<PeerOffer>()) if (o != null && o.Owner != me && o.Hoods.Count > 0) list.Add(o); } catch { }
            return list;
        }

        static PeerOffer? OfferOf(string owner)
        {
            foreach (var o in Others()) if (o.Owner == owner) return o;
            return null;
        }

        /// <summary>The owner's report: the neighbourhoods on offer (served and looked after), with homes passed and
        /// yesterday's quality.</summary>
        public static List<PeerHood> MyHoods(IspState s)
        {
            var list = new List<PeerHood>();
            if (!s.OfferLines || s.HeadendDown) return list;
            foreach (var h in s.Hoods.Values)
            {
                if ((h.Nodes <= 0 && !h.FiberIn) || !Places.Covered(s, h.Id)) continue;
                int homes = Sim.HomesPassed(s, h.Id);
                if (homes <= 0) continue;
                float o = 0f, c = 0f;
                foreach (var site in s.Sites.Values)
                    if (site.Hood == h.Id) { int n = Sim.CustomersOf(site); o += site.OutYesterday * n; c += n; }
                list.Add(new PeerHood { Id = h.Id, Name = City.HoodName(h.Id), Homes = homes, Out = c > 0f ? o / c : 0f, Cong = h.CongYesterday });
            }
            return list;
        }

        // ── the renter ──────────────────────────────────────────────────────────────────────────────────────────────

        public static PeerRent? RentOf(IspState s, string owner)
        {
            foreach (var r in s.Rents) if (r.Owner == owner) return r;
            return null;
        }

        public static void Rent(IspState s, PeerOffer o)
        {
            if (RentOf(s, o.Owner) != null) return;
            s.Rents.Add(new PeerRent { Owner = o.Owner, Company = o.Company, Price = o.Price, Since = Sim.Day() });
            Sim.Event(s, Go.T("go_event_peer_rent", "Lines rented on {0}'s network for {1} each a day - your customers there grow from tomorrow.", o.Company, Go.Money(o.Price)));
        }

        public static void Stop(IspState s, string owner)
        {
            var r = RentOf(s, owner);
            if (r == null) return;
            s.Rents.Remove(r);
            Sim.Event(s, Go.T("go_event_peer_stop", "The lines on {0}'s network are cancelled ({1} customers there leave).", r.Company, Go.Num(Lines(r))));
        }

        public static int Lines(PeerRent r) { int n = 0; foreach (var v in r.Lines.Values) n += v; return n; }
        public static int RentedLines(IspState s) { int n = 0; foreach (var r in s.Rents) n += Lines(r); return n; }

        /// <summary>Sim.EndOfDay: the lines on other players' networks grow or shrink, their customers pay the renter, the
        /// renter pays the owners - and each owner gets the bill.</summary>
        public static void Day(IspState s, DayBook b, int day)
        {
            if (Sim.Test || !Available || s.Rents.Count == 0) return;
            string me = "";
            try { me = Me!(); } catch { }
            float retail = Sim.AvgPrice(s);
            float rep = 0.4f + s.Reputation / 100f;
            for (int i = s.Rents.Count - 1; i >= 0; i--)
            {
                var r = s.Rents[i];
                var o = OfferOf(r.Owner);
                if (o == null)
                {   // withdrawn or gone: the lines wait, no money moves; after a week the contract ends
                    if (++r.Missing >= PauseDays)
                    {
                        s.Rents.RemoveAt(i);
                        Sim.Event(s, Go.T("go_event_peer_gone", "{0} no longer offers its network - the lines there have ended.", r.Company));
                    }
                    continue;
                }
                r.Missing = 0;
                r.Company = o.Company;
                r.Price = o.Price;
                var seen = new HashSet<string>();
                foreach (var h in o.Hoods)
                {
                    seen.Add(h.Id);
                    r.Lines.TryGetValue(h.Id, out int have);
                    bool own = s.Hoods.TryGetValue(h.Id, out var mine) && (mine.Nodes > 0 || mine.FiberIn);
                    float target = own ? 0f : h.Homes * Share * Mathf.Clamp(rep * (1f - 2f * h.Out) * (1f - 0.5f * h.Cong), 0f, 1.4f);
                    float diff = target - have;
                    int step = diff >= 0f ? Mathf.CeilToInt(diff * Grow) : -Mathf.CeilToInt(-diff * Shrink);
                    int loss = Mathf.RoundToInt(have * h.Out * OutLoss);
                    int now = Mathf.Max(0, have + step - loss);
                    if (now > 0) r.Lines[h.Id] = now; else r.Lines.Remove(h.Id);
                }
                foreach (var k in new List<string>(r.Lines.Keys)) if (!seen.Contains(k)) r.Lines.Remove(k);   // no longer on offer
                int lines = Lines(r);
                if (lines <= 0) continue;
                float cost = lines * r.Price;
                b.PeerSubs += lines * retail;
                b.PeerRent += cost;
                try
                {
                    SendBill!(new PeerBill { Owner = r.Owner, Renter = me, Company = s.Company, Day = day, Lines = new Dictionary<string, int>(r.Lines), Amount = cost });
                }
                catch (System.Exception ex) { Go.Warn("lines bill: " + ex.Message + " (GO-WHOLESALE-2)."); }
            }
        }

        // ── the owner ───────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>A renter's bill arrived (main thread): once per renter and day. True when it was booked or known.</summary>
        public static bool Receive(IspState? s, PeerBill bill)
        {
            if (s == null || bill == null || string.IsNullOrEmpty(bill.Renter)) return false;
            if (!s.Tenants.TryGetValue(bill.Renter, out var t)) s.Tenants[bill.Renter] = t = new PeerTenant();
            if (bill.Day <= t.LastDay) return true;
            bool first = t.LastDay < 0;
            t.LastDay = bill.Day;
            t.Company = bill.Company ?? "";
            t.Lines = bill.Lines ?? new Dictionary<string, int>();
            float amount = Mathf.Clamp(bill.Amount, 0f, 1_000_000f);
            t.LastAmount = amount;
            s.Today.PeerIn += amount;
            if (first)
            {
                string text = Go.T("go_event_peer_tenant", "{0} rents lines on your network: {1} lines, {2} a day.", t.Company, Go.Num(TenantLines(t)), Go.Money(amount));
                Sim.Event(s, text);
                Notify.Good(s.Company + ": " + text, 8);
            }
            return true;
        }

        public static int TenantLines(PeerTenant t) { int n = 0; foreach (var v in t.Lines.Values) n += v; return n; }

        /// <summary>Sim.Load: the renters' lines in a neighbourhood use its nodes like the owner's own customers.</summary>
        public static float Load(IspState s, string hood, int h, float grow)
        {
            if (s.Tenants.Count == 0) return 0f;
            int n = 0, today = Sim.Day();
            foreach (var t in s.Tenants.Values)
                if (today - t.LastDay <= 3 && t.Lines.TryGetValue(hood, out int k)) n += k;   // a renter who stopped paying stops counting
            return n * Cat.Tavg(250) * grow * Cat.HomeDay[h];
        }

        /// <summary>The app's Network tab: who rents here (lines, yesterday's bill).</summary>
        public static string TenantsText(IspState s)
        {
            var parts = new List<string>();
            int today = Sim.Day();
            foreach (var t in s.Tenants.Values)
                if (today - t.LastDay <= 3 && TenantLines(t) > 0)
                    parts.Add(Go.T("go_peer_tenant_line", "{0}: {1} lines, {2} a day", t.Company, Go.Num(TenantLines(t)), Go.Money(t.LastAmount)));
            return parts.Count == 0 ? Go.T("go_peer_no_tenants", "Nobody rents lines on your network yet.") : string.Join("\n", parts);
        }

        /// <summary>Tabs.Signature: the Network tab is built again when offers or contracts change.</summary>
        public static string Sig(IspState s)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(Available).Append(s.OfferLines).Append('|');
            foreach (var o in Others()) sb.Append(o.Owner).Append(':').Append(o.Hoods.Count).Append(',');
            sb.Append('|');
            foreach (var r in s.Rents) sb.Append(r.Owner).Append(',');
            return sb.ToString();
        }
    }
}
