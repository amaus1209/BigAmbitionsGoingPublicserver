using System.Collections.Generic;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>The competitor (user 2026-11-06: "Baue eine Konkurrenzfirma mit ein"): Skyline Cable, the city's old cable
    /// company. At first it has every household. Where the provider takes its customers it fights back:
    /// - price cuts in that neighbourhood, which creep back afterwards;
    /// - win-back campaigns with switching bonuses (fewer sign with the provider, a few more leave);
    /// - network upgrades, DOCSIS 3.1 and later fiber (power users only switch for a tariff that keeps up);
    /// - exclusive contracts with the owners of buildings the provider has not connected yet;
    /// - a bid in every tender for an ended permission contract.
    /// Now and then its own network fails in a neighbourhood - that day more people switch. How hard it fights: the game's
    /// rivals difficulty times the mod option "competition" (0 % = it only sits there).</summary>
    internal static class Rival
    {
        public const string Name = "Skyline Cable";
        public static readonly int[] TopMbit = { 500, 1000, 2000 };

        /// <summary>How hard the competitor fights (1 = normal).</summary>
        public static float Strength()
        {
            float game = 1f;
            try { game = SaveGameManager.Current?.gameVariables?.rivalsDifficultyMultiplier ?? 1f; } catch { }
            return Mathf.Max(0f, game) * Sim.Option("rival", 100) / 100f;
        }

        public static RivalHood Hood(IspState s, string id)
        {
            if (!s.Rival.Hoods.TryGetValue(id, out var h)) { h = new RivalHood(); s.Rival.Hoods[id] = h; }
            return h;
        }

        static RivalHood? Find(IspState s, string id) => id.Length > 0 && s.Rival.Hoods.TryGetValue(id, out var h) ? h : null;

        /// <summary>The competitor's prices in a neighbourhood against the market price (1 = the market price, its campaign
        /// for new customers included). "" = the average over the provider's neighbourhoods.</summary>
        public static float PriceLevel(IspState s, string hood)
        {
            int today = Sim.Today(s);
            if (hood.Length > 0) return Level(Find(s, hood), today);
            float sum = 0f; int n = 0;
            foreach (var id in s.Hoods.Keys) { sum += Level(Find(s, id), today); n++; }
            return n > 0 ? sum / n : 1f;
        }

        static float Level(RivalHood? h, int today) => h == null ? 1f : h.Price * (h.PromoUntil >= today ? Cat.RivalPromoPrice : 1f);

        /// <summary>The fastest tariff the competitor sells in a neighbourhood.</summary>
        public static int Top(IspState s, string hood) => TopMbit[Mathf.Clamp(Find(s, hood)?.Tech ?? 0, 0, TopMbit.Length - 1)];

        /// <summary>How readily people of a neighbourhood sign with the provider today: fewer during its win-back campaign,
        /// many more on a day its own network fails; its reputation counts too.</summary>
        public static float JoinFactor(IspState s, string hood, int today)
        {
            float f = Mathf.Clamp(1f + (60f - s.Rival.Reputation) / 100f, 0.8f, 1.4f);
            var h = Find(s, hood);
            if (h == null) return f;
            if (h.OutageUntil >= today) f *= Cat.RivalOutageJoin;
            if (h.PromoUntil >= today) f *= Cat.RivalPromoJoin;
            return f;
        }

        /// <summary>Extra cancellations per day while the competitor tries to win its customers back.</summary>
        public static float ChurnExtra(IspState s, string hood, int today)
        {
            var h = Find(s, hood);
            return h != null && h.PromoUntil >= today ? Cat.RivalPromoChurn * Mathf.Min(2f, Strength()) : 0f;
        }

        /// <summary>Power users switch only for a tariff that keeps up with the competitor's fastest one.</summary>
        public static float SpeedFactor(IspState s, string hood, int mbit)
        {
            int top = Top(s, hood);
            return mbit >= top ? 1f : Mathf.Clamp(Mathf.Sqrt(mbit / (float)top), 0.4f, 1f);
        }

        /// <summary>The share of the units the provider reaches in a neighbourhood that are already its customers.</summary>
        public static float Share(IspState s, string hood)
        {
            int passed = Sim.HomesPassed(s, hood);
            if (passed <= 0) return 0f;
            int ours = 0;
            foreach (var site in s.Sites.Values) if (site.Hood == hood) ours += Sim.CustomersOf(site);
            return Mathf.Clamp01(ours / (float)passed);
        }

        /// <summary>The competitor's exclusive contract on a building the provider has not connected: until this day, or -1.</summary>
        public static int LockedUntil(IspState s, string key, int today) =>
            s.Rival.Locked.TryGetValue(key, out int until) && until >= today ? until : -1;

        /// <summary>The competitor's day: contracts and campaigns run out, upgrades finish, and where the provider has taken
        /// customers it decides what to do.</summary>
        public static void Day(IspState s, int today)
        {
            var r = s.Rival;
            float k = Strength();
            var ended = new List<string>();
            foreach (var kv in r.Locked) if (kv.Value < today) ended.Add(kv.Key);
            foreach (var key in ended) r.Locked.Remove(key);
            r.Reputation = Mathf.MoveTowards(r.Reputation, 60f, 0.5f);
            int age = today - (int)(s.FoundedHour / 24);
            foreach (var id in new List<string>(s.Hoods.Keys))
            {
                var h = Hood(s, id);
                string hood = City.HoodName(id);
                if (h.TechDay >= 0 && today >= h.TechDay)
                {
                    h.Tech = (byte)Mathf.Min(h.Tech + 1, TopMbit.Length - 1);
                    h.TechDay = -1;
                    Say(s, today, Go.T("go_rival_tech_done", "{0} now sells up to {1} in {2}.", Name, Go.Bw(TopMbit[h.Tech]), hood), true);
                }
                if (h.PriceUntil < today && h.Price < 1f) h.Price = Mathf.Min(1f, h.Price + 0.01f);
                if (k <= 0f) continue;
                float share = Share(s, id);
                int ours = 0;
                foreach (var site in s.Sites.Values) if (site.Hood == id) ours += Sim.CustomersOf(site);
                float weight = Mathf.Clamp01((ours - 150f) / 450f);   // a newcomer with a few hundred customers is not worth a fight yet
                if (share > 0.15f && h.PriceUntil < today && Sim.Rand() < 0.06f * k * weight * Mathf.Min(2f, share / 0.3f))
                {
                    float cut = 0.06f + 0.04f * Sim.Rand();
                    h.Price = Mathf.Max(Cat.RivalMinPrice, h.Price - cut);
                    h.PriceUntil = today + 12 + (int)(Sim.Rand() * 10);
                    Say(s, today, Go.T("go_rival_price", "{0} cuts its prices in {1} by {2} (until day {3}). Your customers there compare.", Name, hood, Go.Pct(cut), h.PriceUntil), true);
                }
                if (share > 0.08f && h.PromoUntil < today && Sim.Rand() < 0.04f * k * weight)
                {
                    h.PromoUntil = today + 6;
                    Say(s, today, Go.T("go_rival_promo", "{0} tries to win customers back in {1} with switching bonuses (until day {2}): fewer sign with you, a few more leave.", Name, hood, h.PromoUntil), true);
                }
                if (h.Tech < TopMbit.Length - 1 && h.TechDay < 0 && age > (h.Tech == 0 ? 25f : 60f) / Mathf.Max(0.3f, k) && Sim.Rand() < 0.04f * k)
                {
                    h.TechDay = today + 10;
                    Say(s, today, Go.T("go_rival_tech_plan", "{0} upgrades its network in {1}: from day {2} it sells up to {3}.", Name, hood, h.TechDay, Go.Bw(TopMbit[h.Tech + 1])), false);
                }
                if (h.OutageUntil < today && Sim.Rand() < Cat.RivalOutage)
                {
                    h.OutageUntil = today;
                    r.Reputation = Mathf.Max(20f, r.Reputation - 3f);
                    Say(s, today, Go.T("go_rival_outage", "Big outage at {0} in {1} - today many of its customers look for another provider.", Name, hood), false);
                }
            }
            if (k > 0f && s.Hoods.Count > 0 && Sim.Rand() < 0.08f * k) Lock(s, today);
        }

        /// <summary>An exclusive contract with the owner of one of the biggest buildings the provider has not connected, in a
        /// neighbourhood where it is: one game year.</summary>
        static void Lock(IspState s, int today)
        {
            var free = new List<House>();
            foreach (var b in City.All)
                if (b.Units >= 10 && s.Hoods.ContainsKey(b.Hood) && !s.Sites.ContainsKey(b.Key) && LockedUntil(s, b.Key, today) < 0) free.Add(b);
            if (free.Count == 0) return;
            free.Sort((a, b) => b.Units.CompareTo(a.Units));
            var pick = free[Mathf.Min(free.Count - 1, (int)(Sim.Rand() * Mathf.Min(6, free.Count)))];
            int until = today + Sim.PermitDays() / 2;
            s.Rival.Locked[pick.Key] = until;
            Say(s, today, Go.T("go_rival_lock", "{0} signed an exclusive contract with the owner of {1} ({2} units) until day {3}.", Name, City.Name(pick.Key), Go.Num(pick.Units), until), false);
        }

        static void Say(IspState s, int day, string text, bool note)
        {
            s.Rival.Log.Insert(0, Go.T("go_event_day", "Day {0}", day) + ": " + text);
            if (s.Rival.Log.Count > 20) s.Rival.Log.RemoveRange(20, s.Rival.Log.Count - 20);
            Sim.Event(s, text);
            if (note) Notify.Warn(text, 8);
        }

        /// <summary>The chance to win a tender for an ended permission contract: the provider's reputation against the
        /// competitor's, field sales help, a hard competitor bids higher.</summary>
        public static float TenderChance(IspState s, bool sales) =>
            Mathf.Clamp(0.2f + 0.6f * s.Reputation / 100f + (sales ? 0.2f : 0f) - 0.15f * (Strength() - 1f) - (s.Rival.Reputation - 60f) / 200f, 0.1f, 0.95f);
    }
}
