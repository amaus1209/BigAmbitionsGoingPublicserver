using System.Collections.Generic;
using BigAmbitions.Items;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GAME-TYPES-1: the headend's equipment and the devices are things of the game (GameTypes). In the real game
    /// - the network runs on what STANDS in the headend: a CMTS (DOCSIS 3.0 or 3.1) for the internet, an OLT for fiber, the
    ///   satellite head-end for TV, the CA system for Pay-TV, the telephony and the IPTV platform - each with its daily
    ///   power and maintenance;
    /// - modems, ONTs, set-top boxes and smartcards come from the headend's storage (goods bought at a wholesaler or
    ///   delivered by an importer); devices customers give back are refurbished and kept apart (IspState.Stock) and go out
    ///   first.
    /// A provider founded before 0.6 keeps what it bought in the app (IspState.AppGear). The developer's dry run (Sim.Test)
    /// keeps everything in IspState as before.</summary>
    internal static class Gear
    {
        static readonly (string item, string flag)[] Equipment =
        {
            (GameTypes.ICmts30, "cmts30"), (GameTypes.ICmts31, "cmts31"), (GameTypes.IOlt, "olt"), (GameTypes.ISat, "sat"),
            (GameTypes.ICas, "cas"), (GameTypes.IVoice, "voice"), (GameTypes.IIptv, "iptv"),
            (GameTypes.IUps, "ups"), (GameTypes.IGenerator, "generator"),   // GO-POWER-1
            (GameTypes.IVcmts, "vcmts"),                                      // GO-RPHY-1
            (GameTypes.ICache, "cache"),                                      // GO-CACHE-1
        };

        /// <summary>A provider from before 0.6: what it had bought in the app counts as standing in the headend.</summary>
        public static void Migrate(IspState s)
        {
            if (s.Format >= 6) return;
            s.Format = 6;
            if (s.FoundedHour <= 0 && s.Sites.Count == 0) return;
            if (!s.AppGear.Contains("cmts30")) s.AppGear.Add("cmts30");   // paid at the founding
            if (s.Cmts31) s.AppGear.Add("cmts31");
            if (s.Olt) s.AppGear.Add("olt");
            if (s.Sat) s.AppGear.Add("sat");
            if (s.Cas) s.AppGear.Add("cas");
            if (s.Voice) s.AppGear.Add("voice");
            if (s.Iptv) s.AppGear.Add("iptv");
        }

        static int PlacedAt(BuildingRegistration? reg, string item)
        {
            if (reg?.itemInstances == null) return 0;
            int n = 0;
            foreach (var i in reg.itemInstances.Values) if (i != null && i.itemName == item) n++;
            return n;
        }

        /// <summary>GO-OFFICE-1: a CMTS stands in this building - the network can run from it.</summary>
        public static bool Ready(BuildingRegistration? reg) => PlacedAt(reg, GameTypes.ICmts30) + PlacedAt(reg, GameTypes.ICmts31) + PlacedAt(reg, GameTypes.IVcmts) > 0;

        /// <summary>How many of a piece stand in one building.</summary>
        public static int PlacedIn(string key, string item) => key.Length == 0 ? 0 : PlacedAt(City.Reg(key), item);

        /// <summary>How many of a piece stand in the provider's buildings: equipment in its offices (GO-HUBS-1: the headend and
        /// the hubs), workplaces and goods in all of them (the customer centers too).</summary>
        public static int Placed(IspState s, string item)
        {
            if (Sim.Test) return 0;
            bool equipment = GameTypes.Find(item)?.Kind == 0;
            var keys = equipment ? Places.AllOffices(s) : Places.AllKeys(s);
            int n = 0;
            foreach (var key in keys) n += PlacedIn(key, item);
            if (!equipment && s.CenterKey.Length > 0 && !keys.Contains(s.CenterKey)) n += PlacedIn(s.CenterKey, item);   // a customer center office of 0.6
            return n;
        }

        /// <summary>Every game hour in the real game: the equipment's flags from what stands in the headend (or was bought in
        /// the app before 0.6). Returns true when something changed (for the event log).</summary>
        public static void Refresh(IspState s)
        {
            if (Sim.Test) return;
            var head = City.Reg(s.HeadendKey);
            bool Has(string item, string flag) => s.AppGear.Contains(flag) || PlacedAt(head, item) > 0;
            bool c30 = Has(GameTypes.ICmts30, "cmts30"), c31 = Has(GameTypes.ICmts31, "cmts31") || Has(GameTypes.IVcmts, "vcmts");   // GO-RPHY-1: a vCMTS speaks 3.1
            bool was = s.NoCmts;
            s.NoCmts = !c30 && !c31;
            if (s.NoCmts != was && s.Sites.Count > 0)
                Sim.Event(s, s.NoCmts ? Go.T("go_event_nocmts", "No CMTS stands in the headend any more - the internet is down until one is set up.")
                                      : Go.T("go_event_cmts_back", "A CMTS stands in the headend again - the internet runs."));
            s.Cmts31 = c31;
            s.Olt = Has(GameTypes.IOlt, "olt");
            s.Sat = Has(GameTypes.ISat, "sat");
            s.Cas = Has(GameTypes.ICas, "cas");
            s.Voice = Has(GameTypes.IVoice, "voice");
            s.Iptv = Has(GameTypes.IIptv, "iptv");
        }

        /// <summary>A piece of equipment stands in the headend (or was bought in the app before 0.6).</summary>
        public static bool Has(IspState s, string item)
        {
            foreach (var (it, flag) in Equipment) if (it == item && s.AppGear.Contains(flag)) return true;
            return PlacedAt(City.Reg(s.HeadendKey), item) > 0;
        }

        /// <summary>The equipment's power and maintenance per day.</summary>
        public static float Power(IspState s)
        {
            float sum = 0f;
            if (Sim.Test)
            {   // the dry run: what it has
                sum += GameTypes.Find(GameTypes.ICmts30)!.Power;
                if (s.Cmts31) sum += GameTypes.Find(GameTypes.ICmts31)!.Power;
                if (s.Olt) sum += GameTypes.Find(GameTypes.IOlt)!.Power;
                if (s.Sat) sum += GameTypes.Find(GameTypes.ISat)!.Power;
                if (s.Cas) sum += GameTypes.Find(GameTypes.ICas)!.Power;
                if (s.Voice) sum += GameTypes.Find(GameTypes.IVoice)!.Power;
                if (s.Iptv) sum += GameTypes.Find(GameTypes.IIptv)!.Power;
                return sum;
            }
            var offices = Places.AllOffices(s);   // GO-HUBS-1: the hubs' equipment draws power too
            foreach (var (item, flag) in Equipment)
            {
                var p = GameTypes.Find(item);
                if (p == null) continue;
                int n = 0;
                foreach (var key in offices) n += PlacedIn(key, item);
                if (n == 0 && s.AppGear.Contains(flag)) n = 1;
                sum += n * p.Power;
            }
            return sum;
        }

        // ── devices ───────────────────────────────────────────────────────────────────────────────────────────────────

        public static string DeviceItem(byte dev) => dev == Cat.Dev31 ? GameTypes.IModem31 : dev == Cat.DevOnt ? GameTypes.IOnt : GameTypes.IModem30;

        /// <summary>GO-SHOPS-2 (user 2026-11-06: "Importe später, wenn man Lager hat und Hauptquartier"): where devices count as
        /// stock - the headend first, then the player's own warehouses, where the game delivers an import plan.</summary>
        static readonly List<BuildingRegistration> _stockRegs = new List<BuildingRegistration>();
        static float _stockRegsAt = -100f;
        static string _stockRegsFor = "";

        static List<BuildingRegistration> StockRegs(IspState s)
        {
            float now = Time.unscaledTime;
            if (now - _stockRegsAt < 10f && _stockRegsFor == s.HeadendKey) return _stockRegs;
            _stockRegsAt = now; _stockRegsFor = s.HeadendKey;
            _stockRegs.Clear();
            // GO-CENTERS-1 (user 2026-11-08: "die Kundenbüros haben die Geräte"): the customer centers first, then the offices
            var keys = Places.AllKeys(s);
            keys.Sort((a, b) => Places.IsOffice(s, a).CompareTo(Places.IsOffice(s, b)));
            foreach (var key in keys)
            {
                var reg = City.Reg(key);
                if (reg != null && !_stockRegs.Contains(reg)) _stockRegs.Add(reg);
            }
            var head = City.Reg(s.HeadendKey);
            if (head != null && !_stockRegs.Contains(head)) _stockRegs.Add(head);
            try
            {
                foreach (var b in City.All)
                {
                    if (b.Type != "ba:buildingtype_warehouse" || b.Key == s.HeadendKey || !City.Mine(b.Key)) continue;
                    var reg = City.Reg(b.Key);
                    if (reg != null && reg.businessTypeName == "ba:businesstype_warehouse") _stockRegs.Add(reg);
                }
            }
            catch { }
            return _stockRegs;
        }

        /// <summary>Units of a device in the headend's storage and the player's warehouses (shelves, pallets, boxes, vehicles
        /// parked there).</summary>
        public static int InStorage(IspState s, string item)
        {
            if (Sim.Test) return 0;
            int n = 0;
            foreach (var reg in StockRegs(s))
            {
                try { n += Helpers.BuildingHelper.CountTotalResourcesInStock(reg.Address, item); } catch { }
            }
            return n;
        }

        /// <summary>Takes units out of the headend's storage, then the player's warehouses (the buildings' shelves and boxes);
        /// false if all of them have fewer.</summary>
        public static bool TakeFromStorage(IspState s, string item, int n)
        {
            if (Sim.Test || n <= 0) return n <= 0;
            var regs = StockRegs(s);
            int have = 0;
            foreach (var reg in regs) have += Cargo(reg, item);
            if (have < n) return false;
            foreach (var reg in regs)
            {
                if (n <= 0) break;
                int here = Cargo(reg, item);
                if (here <= 0) continue;
                int take = Mathf.Min(here, n);
                TakeFrom(reg, item, take);
                n -= take;
            }
            return true;
        }

        static int Cargo(BuildingRegistration reg, string item)
        {
            int have = 0;
            if (reg?.itemInstances == null) return 0;
            foreach (var inst in reg.itemInstances.Values)
                if (inst?.cargoInstances != null)
                    foreach (var c in inst.cargoInstances) if (c != null && c.itemName == item) have += c.amount;
            return have;
        }

        static void TakeFrom(BuildingRegistration reg, string item, int n)
        {
            if (reg?.itemInstances == null) return;
            foreach (var inst in reg.itemInstances.Values)
            {
                if (inst?.cargoInstances == null || n <= 0) continue;
                bool changed = false;
                for (int i = 0; i < inst.cargoInstances.Count && n > 0; i++)
                {
                    var c = inst.cargoInstances[i];
                    if (c == null || c.itemName != item) continue;
                    int take = Mathf.Min(c.amount, n);
                    c.amount -= take; n -= take; changed = true;
                    if (c.amount <= 0) { inst.cargoInstances.RemoveAt(i); i--; }
                }
                if (changed) { try { inst.OnItemsInCargoUpdated()?.Invoke(); } catch { } }
            }
        }
    }
}
