using UnityEngine;

namespace GoingOnline
{
    /// <summary>Going Online 0.8, step 3 (the user chose it from the research, 2026-11-08): network monitoring and the power supply.
    ///
    /// GO-PNM-1 - network monitoring (proactive network maintenance): the CMTS and the modems measure the line all the time;
    /// software in the control room reads it and finds plant faults before customers call (vendors name 10-40 % fewer truck
    /// rolls; ZCorum, VIAVI, Broadband Library). Switched on in the app, a licence per day and modem; it works while a network
    /// master is on duty and a control room workplace stands in one of the offices. Then network faults are known at once
    /// (diagnosed, less work on site), half as many customers call about them, worn amplifiers and noise are caught before
    /// they fail (a quarter fewer such faults), and the technical support fixes more devices remotely.
    ///
    /// GO-POWER-1 - power cuts: the city's power fails in a neighbourhood now and then, for one to five hours. Half of its
    /// homes are dark themselves (nobody misses the internet there); the others need the network:
    /// - the nodes and amplifiers are fed from power supplies along the cable - with batteries in them (bought per
    ///   neighbourhood) they bridge two hours, without them they go off at once;
    /// - an office of ours in the neighbourhood goes dark: with a UPS for an hour, with a generator for as long as it lasts
    ///   (fuel per hour) - without them its whole group is off, and the headend dark takes TV, the internet line and the
    ///   phones of the whole city with it.
    /// After the power comes back some power supplies are broken (faults). In the developer's dry run there are no cuts.</summary>
    internal static class Grid
    {
        // ── GO-PNM-1 ──────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The monitoring works this hour.</summary>
        public static bool Pnm(IspState s)
        {
            if (!s.Pnm) return false;
            if (Sim.Test) return true;
            if (Gear.Placed(s, GameTypes.IControlDesk) <= 0) return false;
            foreach (var t in s.Techs) if (t.Role == Cat.RoleMaster && t.Missing == 0) return true;
            return false;
        }

        /// <summary>Why it does not work now ("" = it works or is off).</summary>
        public static string PnmWhy(IspState s)
        {
            if (!s.Pnm || Sim.Test) return "";
            if (Gear.Placed(s, GameTypes.IControlDesk) <= 0) return Go.T("go_pnm_nodesk", "no control room workplace in your offices");
            foreach (var t in s.Techs) if (t.Role == Cat.RoleMaster && t.Missing == 0) return "";
            return Go.T("go_pnm_nomaster", "no network master on duty now");
        }

        public static float PnmDay(IspState s) => Cat.PnmBase + Cat.PnmPerModem * Sim.InternetCustomers(s);

        // ── GO-POWER-1 ────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>An office of ours in this neighbourhood is dark so many hours into a cut: not with a generator, not in the
        /// UPS's first hour.</summary>
        public static bool OfficeDark(IspState s, string hood, int into)
        {
            if (Places.OfficesIn(s, hood).Count == 0) return false;
            if (Places.Units(s, hood, GameTypes.IGenerator, "generator") > 0) return false;
            if (Places.Units(s, hood, GameTypes.IUps, "ups") > 0 && into < Cat.UpsHours) return false;
            return true;
        }

        static PowerCut? CutIn(IspState s, string hood, long abs)
        {
            foreach (var c in s.Cuts) if (c.Hood == hood && abs >= c.Start && abs < c.End) return c;
            return null;
        }

        /// <summary>The share of a building's customers without service this hour because of a power cut (those whose homes
        /// are dark themselves do not count).</summary>
        public static float OutShare(IspState s, Site site, long abs)
        {
            if (Sim.Test || s.Cuts.Count == 0) return 0f;
            float share = 0f, here = 1f - Cat.DarkShare;
            var own = CutIn(s, site.Hood, abs);
            if (own != null)
            {
                int into = (int)(abs - own.Start);
                bool battery = s.Hoods.TryGetValue(site.Hood, out var h) && h.Batteries && into < Cat.BatteryHours;
                if (!battery) share = here;   // the nodes and amplifiers have no power
            }
            // the office of its group, and the headend (TV, the internet line, the phones of everybody)
            foreach (var g in new[] { Places.Group(s, site.Hood), Places.HeadHood(s) })
            {
                var cut = CutIn(s, g, abs);
                if (cut == null || !OfficeDark(s, g, (int)(abs - cut.Start))) continue;
                share = Mathf.Max(share, site.Hood == g ? here : 1f);
            }
            return share;
        }

        /// <summary>Every game hour: cuts end (some power supplies broke), the generators burn fuel, new cuts begin.</summary>
        public static void Hour(IspState s, long abs)
        {
            if (Sim.Test) return;
            for (int i = s.Cuts.Count - 1; i >= 0; i--)
            {
                var c = s.Cuts[i];
                if (abs < c.End) continue;
                s.Cuts.RemoveAt(i);
                int broken = 0;
                if (s.Hoods.TryGetValue(c.Hood, out var h))
                    for (int n = 0; n < h.Nodes; n++)
                        if (Sim.Rand() < Cat.AfterCutFaults) { Sim.Open(s, abs, Cat.TtPower, c.Hood, "", Mathf.Max(1, Sim.HoodCoaxCustomers(s, c.Hood) / Mathf.Max(1, h.Nodes))); broken++; }
                Sim.Event(s, broken > 0 ? Go.T("go_event_power_back_broken", "The power is back in {0} - {1} power supplies of the network did not survive it.", City.HoodName(c.Hood), broken)
                                        : Go.T("go_event_power_back", "The power is back in {0}.", City.HoodName(c.Hood)));
            }
            foreach (var c in s.Cuts)
            {   // a generator runs while its office has no power from the city
                if (abs < c.Start) continue;
                int gens = Places.Units(s, c.Hood, GameTypes.IGenerator, "generator");
                if (gens > 0) { s.Today.Power += gens * Cat.GeneratorFuel; c.Fuel += 1f; }
            }
            foreach (var h in s.Hoods.Values)
            {
                if (CutIn(s, h.Id, abs) != null) continue;
                bool ours = h.Nodes > 0 || h.FiberIn || Places.OfficesIn(s, h.Id).Count > 0;
                if (!ours || Sim.Rand() >= Cat.RatePowerCut / 24f) continue;
                int hours = UnityEngine.Random.Range(Cat.CutMin, Cat.CutMax + 1);
                s.Cuts.Add(new PowerCut { Hood = h.Id, Start = abs, End = abs + hours });
                Started(s, h, hours);
            }
        }

        /// <summary>A new cut: what it does to the network, in one entry and a notification.</summary>
        static void Started(IspState s, Hood h, int hours)
        {
            var what = new System.Collections.Generic.List<string>();
            if (h.Nodes > 0)
                what.Add(h.Batteries ? Go.T("go_power_batteries", "the batteries hold the nodes for {0} h", Cat.BatteryHours)
                                     : Go.T("go_power_nobatteries", "the nodes are off at once (no batteries)"));
            if (Places.OfficesIn(s, h.Id).Count > 0)
            {
                bool gen = Places.Units(s, h.Id, GameTypes.IGenerator, "generator") > 0, ups = Places.Units(s, h.Id, GameTypes.IUps, "ups") > 0;
                string office = h.Id == Places.HeadHood(s) ? Go.T("go_headend", "headend") : Go.T("go_power_hub", "the hub");
                what.Add(gen ? Go.T("go_power_gen", "{0} runs on its generator", office)
                       : ups ? Go.T("go_power_ups", "{0} runs an hour on its UPS", office)
                       : Go.T("go_power_dark", "{0} is dark", office));
            }
            string text = Go.T("go_event_power_cut", "Power cut in {0} for about {1} h: {2}.", City.HoodName(h.Id), hours, what.Count > 0 ? string.Join(", ", what) : Go.T("go_power_nothing", "nothing of yours is hit"));
            Sim.Event(s, text);
            if (what.Count > 0) Notify.Warn(s.Company + ": " + text, 8);
        }

        /// <summary>Batteries for every node of a neighbourhood (and the ones built later come with them).</summary>
        public static float BatteryCost(IspState s, string hood) => s.Hoods.TryGetValue(hood, out var h) ? Mathf.Max(1, h.Nodes + h.NodesBuilding) * Cat.BatteryPerNode : 0f;

        public static bool BuyBatteries(IspState s, string hood)
        {
            if (!s.Hoods.TryGetValue(hood, out var h) || h.Batteries || h.Nodes + h.NodesBuilding == 0) return false;
            if (!Money.Invest(s, BatteryCost(s, hood))) return false;
            h.Batteries = true;
            Sim.Event(s, Go.T("go_event_batteries", "The nodes' power supplies in {0} have batteries now - they bridge {1} hours without power.", City.HoodName(hood), Cat.BatteryHours));
            return true;
        }

        /// <summary>The batteries' upkeep per day (they age and are replaced).</summary>
        public static float BatteryDay(IspState s)
        {
            float sum = 0f;
            foreach (var h in s.Hoods.Values) if (h.Batteries) sum += h.Nodes * Cat.BatteryDay;
            return sum;
        }

        /// <summary>A cut going on now, for the app: the neighbourhood and the hours left.</summary>
        public static string CutsNow(IspState s)
        {
            long now = Sim.NowHour();
            var parts = new System.Collections.Generic.List<string>();
            foreach (var c in s.Cuts) if (now >= c.Start && now < c.End) parts.Add(Go.T("go_power_now", "{0} (another {1} h)", City.HoodName(c.Hood), c.End - now));
            return string.Join(", ", parts);
        }
    }
}
