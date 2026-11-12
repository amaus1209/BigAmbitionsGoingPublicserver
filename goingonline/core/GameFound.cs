using BigAmbitions.DayNightCycle;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GAME-TYPES-1 (replaces 0.5's BIZMAN-FOUND-1 card, user 2026-11-06: "darüber soll Kabelanbieter gründbar sein"):
    /// founding through the game. A "Cable provider" business founded in BizMan ("Start new business" in an office the
    /// player rents or owns) founds the provider with that office as its headend and customer center in one (GO-OFFICE-1)
    /// and the business's name as the company's. It is opened around the clock - the dialog leaves a new business
    /// "temporarily closed" with 8-16 opening hours, and faults come at night too; the shifts the player planned stay as
    /// they are. A provider of 0.6 with its headend in a warehouse moves into the office by itself.</summary>
    internal static class GameFound
    {
        static float _next;
        static readonly HashSet<string> _prepared = new HashSet<string>();
        static readonly HashSet<string> _centerFirst = new HashSet<string>();

        /// <summary>Main thread, while in a city (every 2 seconds).</summary>
        public static void Tick()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 2f;
            try
            {
                var game = SaveGameManager.Current;
                if (game?.BuildingRegistrations == null) return;
                foreach (var reg in game.BuildingRegistrations)
                {
                    if (reg == null || !GameTypes.IsOurs(reg.businessTypeName) || !(reg.RentedByPlayer || reg.BuildingOwnedByPlayer)) continue;
                    string key = City.KeyOf(reg.Address);
                    var s = Core.S;
                    // GO-CENTERS-1: until 0.7 the customer center type stood in offices - such an office is one of the provider's
                    // offices now (a hub in its neighbourhood), the customer center is a shop
                    if (reg.businessTypeName == GameTypes.Center && GameTypes.InOffice(reg))
                    {
                        reg.businessTypeName = GameTypes.Cable;
                        _prepared.Remove(key);
                        Changed(reg);
                        if (s != null) Sim.Event(s, Go.T("go_event_center_office", "{0} was a customer center office - it is one of your offices now. Customer centers are shops since 0.8.", City.Name(key)));
                        Go.Log($"the customer center office {key} is a cable office now (GO-CENTERS-1).");
                    }
                    if (s == null)
                    {
                        if (!GameTypes.InOffice(reg) && reg.businessTypeName == GameTypes.Center)
                        {   // a customer center alone founds nothing - the provider begins with its office
                            if (_centerFirst.Add(key)) Notify.Warn(Go.T("go_note_center_first", "A cable customer center needs its provider: found the \"Cable provider\" in an office first (BizMan, \"Start new business\")."), 10);
                            continue;
                        }
                        Prepare(reg);
                        string name = string.IsNullOrWhiteSpace(reg.BusinessName) ? "Going Online" : reg.BusinessName.Trim();
                        if (Act.Found(name, key))
                        {
                            if (Core.S != null && (GameTypes.InOffice(reg) || reg.businessTypeName == GameTypes.Center)) Core.S.CenterKey = key;   // GO-OFFICE-1: one roof
                            Go.Log($"provider '{name}' founded from the game's business in {key} (GAME-TYPES-1, GO-OFFICE-1).");
                        }
                        return;
                    }
                    if (key != s.HeadendKey && GameTypes.InOffice(reg) && MoveInto(s, reg, key)) return;
                    // GO-HUBS-1, GO-CENTERS-1: every office and customer center of ours is opened, its neighbourhood listed
                    if (City.ByKey.TryGetValue(key, out var b) && b.Hood.Length > 0 && !s.Hoods.ContainsKey(b.Hood))
                    {
                        Act.HoodOf(s, b.Hood);
                        Sim.Event(s, GameTypes.InOffice(reg)
                            ? Go.T("go_event_office_new", "A new office of yours in {0}: build the feeder fibre from the headend (Network tab) and set up a CMTS there - then it serves the neighbourhood as a hub.", City.HoodName(b.Hood))
                            : Go.T("go_event_center_new2", "A customer center of yours in {0}: set up customer desks (Going Online's customer center workplace) and assign customer service; waiting places, the device table and a television for your spot bring it to life.", City.HoodName(b.Hood)));
                    }
                    Prepare(reg);
                }
            }
            catch (System.Exception ex) { Go.Warn("founding from the game: " + ex.Message); }
        }

        /// <summary>GO-OFFICE-1: the headend is not where it belongs since 0.7 - in a warehouse (0.6) or a building that is no
        /// longer the player's.</summary>
        internal static bool HeadendMisplaced(IspState s)
        {
            if (Sim.Test || s.HeadendKey.Length == 0) return false;
            var head = City.Reg(s.HeadendKey);
            return head == null || !City.Mine(s.HeadendKey) || !GameTypes.InOffice(head);
        }

        /// <summary>GO-OFFICE-1 (user 2026-11-07: "die Kopfstelle kann man ja im Kundenbüro errichten, damit würde das Lager
        /// entfallen"): an office of ours - a new "Cable provider" or the customer center of 0.6 - takes over as headend and
        /// customer center: when the old headend is a warehouse and a CMTS already stands in the office (until then the
        /// warehouse keeps the network running), or at once when the old one is no longer the player's. A customer center
        /// becomes a "Cable provider" business then (its name, staff and shifts stay); the old warehouse becomes the game's
        /// warehouse again - devices there still count as stock.</summary>
        static bool MoveInto(IspState s, BuildingRegistration reg, string key)
        {
            if (!HeadendMisplaced(s)) return false;
            bool lost = s.HeadendDown && !City.Mine(s.HeadendKey);   // lost since the hourly check at least, not for a moment
            if (!lost && !Gear.Ready(reg)) return false;
            string old = s.HeadendKey;
            if (reg.businessTypeName == GameTypes.Center)
            {
                reg.businessTypeName = GameTypes.Cable;
                _prepared.Remove(key);
                Changed(reg);
            }
            Prepare(reg);
            s.HeadendKey = key;
            if (s.CenterKey.Length == 0 || s.CenterKey == old || !Fleet.CenterOk(s)) s.CenterKey = key;
            Gear.Refresh(s);
            s.HeadendDown = s.NoCmts;
            var was = City.Reg(old);
            bool back = false;
            if (!lost && was != null && was.businessTypeName == GameTypes.Cable && GameTypes.BuildingTypeOf(was) == "ba:buildingtype_warehouse")
            {
                was.businessTypeName = "ba:businesstype_warehouse";
                _prepared.Remove(old);
                Changed(was);
                back = true;
            }
            Sim.Event(s, Go.T("go_event_headend_office", "The headend and the customer center are now in the office {0}. Assign your staff to it (MyEmployees).", City.Name(key)));
            if (back) Sim.Event(s, Go.T("go_event_old_warehouse", "{0} is an ordinary warehouse again - devices there still count as stock.", City.Name(old)));
            Notify.Good(Go.T("go_note_headend_office", "{0}: the headend is now in the office {1}.", s.Company, City.Name(key)), 8);
            Go.Log($"headend moved from {old} into the office {key}{(back ? "; the old warehouse runs as the game's warehouse again" : "")} (GO-OFFICE-1).");
            return true;
        }

        /// <summary>The map's point and the game's pages learn of a business's new type.</summary>
        static void Changed(BuildingRegistration reg)
        {
            try { InstanceBehavior<CityManager>.Instance.FindCityBuildingController(reg.Address)?.UpdatePoi(); } catch { }
            try { GlobalEvents.onBuildingRegistrationChange?.Invoke(reg.Address); } catch { }
        }

        /// <summary>Open and open around the clock (once per building and session; the shifts stay).</summary>
        internal static void Prepare(BuildingRegistration reg)
        {
            string key = City.KeyOf(reg.Address);
            if (!_prepared.Add(key)) return;
            EnsureLogo(reg);
            if (!GameTypes.InOffice(reg) && reg.businessTypeName == GameTypes.Center)
            {   // GO-CENTERS-1: a shop keeps the opening hours the player sets in BizMan
                try { reg.TemporarilyClose(false); } catch (System.Exception ex) { Go.Warn("open " + key + ": " + ex.Message); }
                return;
            }
            try
            {
                reg.TemporarilyClose(false);
                if (reg.scheduleDays == null) reg.scheduleDays = new List<ScheduleDay>();
                foreach (DayOfWeekOrdered d in System.Enum.GetValues(typeof(DayOfWeekOrdered)))
                {
                    var day = reg.scheduleDays.Find(x => x != null && x.day == d);
                    if (day == null) { day = new ScheduleDay { day = d }; reg.scheduleDays.Add(day); }
                    day.isOpen = true;
                    day.openingHourSlots = new List<OpeningHourSlot> { new OpeningHourSlot(0, 24) };
                }
            }
            catch (System.Exception ex) { Go.Warn("open " + key + ": " + ex.Message); }
        }

        /// <summary>LOGO-GO-1 (user 2026-11-07: "Firmenlogo nicht auswählbar"): founded in a warehouse, a business gets the game's
        /// warehouse logo - the type's icon in black on white; ours is a full square, so the whole logo came out black (1.2 KB),
        /// and the game keeps a warehouse's logo out of BizMan's editor. A cable business without a logo shape gets a proper one
        /// once: a shape of its type (GameTypes.MakeType gives it electronics and web agency shapes), the game's colours, the
        /// game's own generator. Its own pictures (shop signs, a file over 4 KB) stay; afterwards BizMan's logo editor (open for
        /// our types in the multiplayer mod) and the shop signs window take over.</summary>
        static void EnsureLogo(BuildingRegistration reg)
        {
            try
            {
                if (reg.logoSettings != null && !string.IsNullOrEmpty(reg.logoSettings.logoShape)) return;
                if (string.IsNullOrWhiteSpace(reg.BusinessName)) return;
                string dir = LogoHelper.GetPlayerBusinessLogoPath(reg.BusinessName);
                string square = System.IO.Path.Combine(dir, LogoSize.SquareSign.ToString() + ".jpg");
                if (System.IO.File.Exists(square) && new System.IO.FileInfo(square).Length >= 4096) return;
                var t = Helpers.BusinessTypeHelper.GetData(reg.businessTypeName);
                if (t?.logoShapes == null || t.logoShapes.Count == 0) return;
                reg.logoSettings = LogoHelper.GenerateLogoSetting(reg.businessTypeName);
                var r = reg;
                BusinessLogoGenerator.Create(reg.BusinessName, reg.logoSettings, dir, true, () =>
                {
                    try { GlobalEvents.onBuildingRegistrationChange?.Invoke(r.Address); } catch { }
                });
                Go.Log($"logo for '{reg.BusinessName}': only the black warehouse logo was there - a logo of its own made with shape '{reg.logoSettings.logoShape}' (LOGO-GO-1).");
            }
            catch (System.Exception ex) { Go.Warn("logo: " + ex.Message); }
        }
    }
}
