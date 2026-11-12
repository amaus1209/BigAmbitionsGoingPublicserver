using System.Reflection;
using BigAmbitions.Characters.Skills;
using BigAmbitions.Items;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GAME-TYPES-1 (user 2026-11-06, after three Workshop examples - IT Business Expansion, Skill Kit and Spotless -
    /// "alles in einem Rutsch"): the cable provider lives in the game's own catalogues, through the ways the game has for
    /// mods (no Harmony, no game file changed, nothing taken from those mods):
    /// - the business type "Cable provider" (BAModAPI.ModdingAPI.RegisterModBusinessType) in an office: headend and customer
    ///   center in one (GO-OFFICE-1); BizMan offers it under "Start new business", the map, BizMan's pages and reports show it
    ///   like any business. "Cable customer center" stays registered for saves from 0.6 but is no longer offered;
    /// - the headend's equipment as furniture (CMTS, OLT, satellite head-end, CAS, telephony and IPTV platforms) and two
    ///   workplaces for the staff, sold by the city's office furniture stores (Services.ContractItemsForSaleService); the
    ///   devices (modems, ONTs, set-top boxes, smartcards) as goods at the electronics wholesalers and importers
    ///   (ItemsGetter.RegisterModItem); the models sit in Helpers.PrefabHelper's cache, as the game looks up a mod item's
    ///   model: copies of the game's cabinet and phone wearing our own meshes (GO-MODELS-1, Models.cs), the workplaces
    ///   the game's workstation re-coloured; the icons are renders of our models;
    /// - five jobs of the game (SkillHelper's catalogue) that the city's recruitment agencies offer (City Workforce Inc.,
    ///   Anderson Recruitment Corp.), plus the game's own customer service;
    /// - a chapter in the game's help (F1).
    /// Registered once the game's catalogues are loaded (from the main menu on), so a save with these types loads.
    /// Everything is also undone on request: Uninstall turns them back into the game's own (Finances: "Remove the mod
    /// safely").</summary>
    internal static class GameTypes
    {
        public const string Cable = "goingonline:businesstype_cableprovider";
        public const string Center = "goingonline:businesstype_cablecenter";

        public const string SkTech = "goingonline:skill_cabletech", SkSwitch = "goingonline:skill_cableswitch",
                            SkMaster = "goingonline:skill_networkmaster", SkSales = "goingonline:skill_fieldsales",
                            SkSupport = "goingonline:skill_techsupport", SkService = "ba:skill_customerservice";

        public const string ICmts30 = "goingonline:itemname_gocmts30", ICmts31 = "goingonline:itemname_gocmts31",
                            IOlt = "goingonline:itemname_goolt", ISat = "goingonline:itemname_gosat", ICas = "goingonline:itemname_gocas",
                            IVoice = "goingonline:itemname_govoice", IIptv = "goingonline:itemname_goiptv",
                            IDesk = "goingonline:itemname_godesk", ITechDesk = "goingonline:itemname_gotechdesk",
                            IControlDesk = "goingonline:itemname_gocontroldesk", IDeviceTable = "goingonline:itemname_godevicetable",
                            IUps = "goingonline:itemname_goups", IGenerator = "goingonline:itemname_gogenerator",
                            IVcmts = "goingonline:itemname_govcmts", ICache = "goingonline:itemname_gocache",
                            IModem30 = "goingonline:itemname_gomodem30", IModem31 = "goingonline:itemname_gomodem31",
                            IOnt = "goingonline:itemname_goont", IStb = "goingonline:itemname_gostb", ICard = "goingonline:itemname_gocard",
                            IInternet = "goingonline:itemname_gointernet", ITv = "goingonline:itemname_gotv", IRent = "goingonline:itemname_gorent";

        /// <summary>0 headend equipment, 1 a workplace, 2 a device (goods), 3 a service line for BizMan's reports, 4 furniture of
        /// the customer center (GO-LOBBY-1).</summary>
        internal sealed class Piece
        {
            public string Name = "", Source = "", IconFile = "";
            public byte Kind;
            public float Price, Power;
            public Color Tint = Color.white;
            public string[]? Skills;
            public int Box = 10;
        }

        static readonly Color Graphite = new Color(0.32f, 0.35f, 0.40f);

        /// <summary>The template of the workplaces: the game's own employee workstation of the office jobs (found by its kind -
        /// "Computer Workstation" in the stores is a choice of desk and computer, not one item).</summary>
        const string AutoStation = "auto:station";
        public static readonly Piece[] Pieces =
        {
            new Piece { Name = ICmts30, Source = "ba:itemname_bigcabinet", Kind = 0, Price = Cat.Cmts30, Power = 25f, Tint = Graphite, IconFile = "GO_Item_Cmts30.png" },
            new Piece { Name = ICmts31, Source = "ba:itemname_bigcabinet", Kind = 0, Price = Cat.Cmts31, Power = 60f, Tint = new Color(0.20f, 0.42f, 0.48f), IconFile = "GO_Item_Cmts31.png" },
            new Piece { Name = IOlt, Source = "ba:itemname_bigcabinet", Kind = 0, Price = Cat.Olt, Power = 90f, Tint = new Color(0.22f, 0.45f, 0.30f), IconFile = "GO_Item_Olt.png" },
            // GO-RPHY-1: the vCMTS server for remote PHY nodes
            new Piece { Name = IVcmts, Source = "ba:itemname_bigcabinet", Kind = 0, Price = Cat.Vcmts, Power = 30f, Tint = new Color(0.30f, 0.55f, 0.62f), IconFile = "GO_Item_Vcmts.png" },
            // GO-CACHE-1: a streaming service's cache server (the service gives it, the provider pays its installation)
            new Piece { Name = ICache, Source = "ba:itemname_bigcabinet", Kind = 0, Price = Cat.Cache, Power = 8f, Tint = new Color(0.62f, 0.32f, 0.55f), IconFile = "GO_Item_Cache.png" },
            new Piece { Name = ISat, Source = "ba:itemname_bigcabinet", Kind = 0, Price = Cat.Sat, Power = 20f, Tint = new Color(0.52f, 0.28f, 0.24f), IconFile = "GO_Item_Sat.png" },
            new Piece { Name = ICas, Source = "ba:itemname_bigcabinet", Kind = 0, Price = Cat.Cas, Power = 30f, Tint = new Color(0.40f, 0.30f, 0.52f), IconFile = "GO_Item_Cas.png" },
            new Piece { Name = IVoice, Source = "ba:itemname_bigcabinet", Kind = 0, Price = Cat.Voice, Power = 30f, Tint = new Color(0.52f, 0.44f, 0.24f), IconFile = "GO_Item_Voice.png" },
            new Piece { Name = IIptv, Source = "ba:itemname_bigcabinet", Kind = 0, Price = Cat.Iptv, Power = 50f, Tint = new Color(0.28f, 0.36f, 0.72f), IconFile = "GO_Item_Iptv.png" },
            // GO-POWER-1: the UPS (a battery cabinet, an hour) and the generator (it runs as long as the cut lasts) of an office
            new Piece { Name = IUps, Source = "ba:itemname_bigcabinet", Kind = 0, Price = Cat.Ups, Power = 5f, Tint = new Color(0.62f, 0.60f, 0.30f), IconFile = "GO_Item_Ups.png" },
            new Piece { Name = IGenerator, Source = "ba:itemname_bigcabinet", Kind = 0, Price = Cat.Generator, Power = 10f, Tint = new Color(0.55f, 0.22f, 0.18f), IconFile = "GO_Item_Generator.png" },
            new Piece { Name = IDesk, Source = AutoStation, Kind = 1, Price = Cat.Desk, Power = 0f, Skills = new[] { SkService, SkSupport, SkSales, SkMaster } },
            new Piece { Name = ITechDesk, Source = AutoStation, Kind = 1, Price = Cat.Desk, Power = 0f, Tint = new Color(0.80f, 0.86f, 0.92f), Skills = new[] { SkTech, SkSwitch, SkMaster } },
            // GO-WORK-3 (user 2026-11-08: "ein Arbeitsplatz für Meister und Schaltdienst fehlt noch"): the control room's own
            // workplace - switching staff and network masters; the technical workplace keeps them too
            new Piece { Name = IControlDesk, Source = AutoStation, Kind = 1, Price = Cat.Desk, Power = 0f, Tint = new Color(0.80f, 0.92f, 0.84f), Skills = new[] { SkSwitch, SkMaster } },
            // GO-LOBBY-1 (user 2026-11-08: "im Kundencenter ein Tisch mit den Geräten für die Kunden"): the device table, a
            // counter the size of the game's console table with the provider's devices on it (model godevicetable)
            new Piece { Name = IDeviceTable, Source = "ba:itemname_consoletablerattan", Kind = 4, Price = 1800f, Power = 0f, Tint = new Color(0.10f, 0.62f, 0.75f), IconFile = "GO_Item_DeviceTable.png" },
            new Piece { Name = IModem30, Source = "ba:itemname_smartphone1", Kind = 2, Price = Cat.BuyM30, Tint = Graphite, Box = 20, IconFile = "GO_Item_Modem30.png" },
            new Piece { Name = IModem31, Source = "ba:itemname_smartphone1", Kind = 2, Price = Cat.BuyM31, Tint = new Color(0.20f, 0.42f, 0.48f), Box = 20, IconFile = "GO_Item_Modem31.png" },
            new Piece { Name = IOnt, Source = "ba:itemname_smartphone1", Kind = 2, Price = Cat.BuyOnt, Tint = new Color(0.22f, 0.45f, 0.30f), Box = 20, IconFile = "GO_Item_Ont.png" },
            new Piece { Name = IStb, Source = "ba:itemname_smartphone1", Kind = 2, Price = Cat.BuyStb, Tint = new Color(0.28f, 0.36f, 0.72f), Box = 20, IconFile = "GO_Item_Stb.png" },
            new Piece { Name = ICard, Source = "ba:itemname_smartphone1", Kind = 2, Price = Cat.BuyCard, Tint = new Color(0.52f, 0.44f, 0.24f), Box = 50, IconFile = "GO_Item_Card.png" },
            new Piece { Name = IInternet, Source = "ba:itemname_hourlyprogrammerfee", Kind = 3, Price = 2f, IconFile = "GO_Item_Internet.png" },
            new Piece { Name = ITv, Source = "ba:itemname_hourlyprogrammerfee", Kind = 3, Price = 1f, IconFile = "GO_Item_Tv.png" },
            new Piece { Name = IRent, Source = "ba:itemname_hourlyprogrammerfee", Kind = 3, Price = 0.5f, IconFile = "GO_Item_Modem30.png" },
        };

        /// <summary>The five jobs: id, the game's job it is modelled on (icon, colours), wage per hour, its second skill.</summary>
        static readonly (string id, string source, float wage, string second)[] Jobs =
        {
            (SkTech, "ba:skill_deliverydriver", 22f, "ba:skill_customerservice"),
            (SkSwitch, "ba:skill_deliverydriver", 18f, "ba:skill_customerservice"),
            (SkMaster, "ba:skill_logisticsmanager", 32f, "ba:skill_negotiation"),
            (SkSales, "ba:skill_purchasingagent", 20f, "ba:skill_negotiation"),
            (SkSupport, "ba:skill_programmer", 26f, "ba:skill_customerservice"),
        };

        public static byte RoleOf(string skill) => skill switch
        {
            SkTech => Cat.RoleTech, SkSwitch => Cat.RoleSwitch, SkMaster => Cat.RoleMaster,
            SkSales => Cat.RoleSales, SkSupport => Cat.RoleSupport, SkService => Cat.RoleService, _ => 255,
        };

        public static string SkillOf(byte role) => role switch
        {
            Cat.RoleTech => SkTech, Cat.RoleSwitch => SkSwitch, Cat.RoleMaster => SkMaster,
            Cat.RoleSales => SkSales, Cat.RoleSupport => SkSupport, _ => SkService,
        };

        public static bool IsOurs(string? businessType) => businessType == Cable || businessType == Center;

        /// <summary>GO-DISSOLVE-1: the player still rents or owns a building with a cable business. As long as one is there,
        /// GameFound would found the provider again from it at once, so it cannot be dissolved.</summary>
        public static bool AnyOurs()
        {
            try
            {
                var regs = SaveGameManager.Current?.BuildingRegistrations;
                if (regs == null) return false;
                foreach (var reg in regs)
                    if (reg != null && IsOurs(reg.businessTypeName) && (reg.RentedByPlayer || reg.BuildingOwnedByPlayer)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>The kind of building a registration is in ("" if the game does not know it).</summary>
        public static string BuildingTypeOf(BuildingRegistration? reg)
        {
            try { return reg?.GetBuildingType() ?? ""; } catch { return ""; }
        }

        /// <summary>GO-OFFICE-1: a business of ours in an office - the place of the provider since 0.7.</summary>
        public static bool InOffice(BuildingRegistration? reg) => BuildingTypeOf(reg) == "ba:buildingtype_office";
        public static Piece? Find(string name) { foreach (var p in Pieces) if (p.Name == name) return p; return null; }

        const BindingFlags SF = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        const BindingFlags IF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static readonly Dictionary<string, Item> _items = new Dictionary<string, Item>();
        static readonly Dictionary<string, SkillData> _skills = new Dictionary<string, SkillData>();
        static readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();
        static BusinessType? _cable, _center;
        static GameObject? _holder;
        static float _next;
        static bool _logged;
        static object? _city;
        static readonly List<string> _furnitureStores = new List<string>(), _deviceShops = new List<string>();
        static readonly List<string> _wholesaleShops = new List<string>(), _importShops = new List<string>();
        static readonly List<KeyValuePair<Vector3, string>> _wholesaleAt = new List<KeyValuePair<Vector3, string>>();

        /// <summary>Where the equipment and the workplaces are sold, and where the devices (for the app's pages).</summary>
        public static IReadOnlyList<string> FurnitureStores => _furnitureStores;
        public static IReadOnlyList<string> DeviceShops => _deviceShops;
        /// <summary>GO-SHOPS-2: every wholesaler of the city (by name), and the importers that bring electronics.</summary>
        public static IReadOnlyList<string> WholesaleShops => _wholesaleShops;
        public static IReadOnlyList<string> ImportShops => _importShops;

        /// <summary>GO-SHOPS-2: the wholesaler nearest to a place ("" = none known).</summary>
        public static string NearestWholesale(Vector3 at)
        {
            string best = ""; float bd = float.MaxValue;
            foreach (var w in _wholesaleAt)
            {
                Vector3 d = w.Key - at; d.y = 0f;
                if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = w.Value; }
            }
            return best;
        }

        /// <summary>Main thread, every frame from the start (cheap once done): registers what is missing.</summary>
        public static void Ensure()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 2f;
            try { if (Helpers.BusinessTypeHelper.GetData("ba:businesstype_warehouse") == null) return; }   // the catalogues are not loaded yet
            catch { return; }
            BlueprintsInstall.Run();   // GO-BLUEPRINT-2: the templates into the game's Blueprints folder, once
            bool did = false;
            did |= Step("jobs", EnsureSkills);
            did |= Step("items", EnsureItems);
            did |= Step("business types", EnsureTypes);
            if (did && !_logged && _skills.Count == Jobs.Length && _cable != null && _center != null)
            {
                _logged = true;
                Go.Log($"game types: business types {(_cable != null ? "cable provider" : "-")} / {(_center != null ? "customer center" : "-")}, {_items.Count} items, {_skills.Count} jobs (GAME-TYPES-1).");
            }
            Step("headquarters", HqJobs);
            Step("workplace jobs", () => UpdateJobs());
            Step("city", () => { CityHooks(); return false; });
            Step("service van", () => { Van.Ensure(); return false; });   // GO-VAN-2: the vehicle type and its model
            Step("service vans", () => { ServiceVans.Tick(); return false; });
            Step("map filter", () => { GameUi.MapFilters(); return false; });
            Step("help", () => { Help(); return false; });
        }

        public const string Hq = "ba:businesstype_headquarters";
        static float _hqNext;
        static bool _hqLogged;

        /// <summary>GO-HQ-1 (user 2026-11-07: "der Netzmeister fehlt auch, der müsste unter Hauptsitz eingestellt werden"):
        /// network masters can also be hired for the player's headquarters - the game's headquarters type names the job,
        /// and the computers its own jobs work at (desktop computer, laptop ... - found by those jobs) take network masters
        /// too. Crew counts them from there. Checked once a minute (a reload of the catalogues brings the game's own back).</summary>
        static bool HqJobs()
        {
            if (Time.unscaledTime < _hqNext) return false;
            _hqNext = Time.unscaledTime + 60f;
            bool did = false;
            var hq = Helpers.BusinessTypeHelper.GetData(Hq);
            if (hq != null && System.Array.IndexOf(hq.employeePrimarySkills ?? new string[0], SkMaster) < 0)
            {
                var l = new List<string>(hq.employeePrimarySkills ?? new string[0]) { SkMaster };
                hq.employeePrimarySkills = l.ToArray();
                did = true;
            }
            int stations = 0;
            foreach (var it in ItemsGetter.AllItems)
            {
                if (it == null || it.suitableSkills == null || !it.type.HasFlag(ItemType.EmployeeWorkstation)) continue;
                if (System.Array.IndexOf(it.suitableSkills, "ba:skill_logisticsmanager") < 0 || ItemsGetter.IsModItem(it.itemName)) continue;
                stations++;
                if (System.Array.IndexOf(it.suitableSkills, SkMaster) >= 0) continue;
                var l = new List<string>(it.suitableSkills) { SkMaster };
                it.suitableSkills = l.ToArray();
                did = true;
            }
            if (did && !_hqLogged)
            {
                _hqLogged = true;
                Go.Log($"game types: network masters can be hired for the headquarters and work at its {stations} kinds of computers (GO-HQ-1).");
            }
            return did;
        }

        /// <summary>GO-HQ-1: the headquarters' entry names the network master (the recruitment dialog, at the moment of the
        /// pick).</summary>
        internal static void HqJobsNow() { _hqNext = 0f; HqJobs(); }

        static readonly HashSet<string> _failed = new HashSet<string>();

        /// <summary>GO-HOOKS-3: one step that fails is reported once and tried again next round - before, the first failure
        /// anywhere stopped all of them for the session (the recruitment agencies' jobs among them).</summary>
        static bool Step(string what, System.Func<bool> f)
        {
            try { bool r = f(); _failed.Remove(what); return r; }
            catch (System.Exception ex) { if (_failed.Add(what)) Go.Warn("game types (" + what + "): " + ex); return false; }
        }

        /// <summary>For the developer's self-test log: what of it the game knows right now.</summary>
        public static string Check()
        {
            try
            {
                int types = 0, items = 0, models = 0, own = 0, icons = 0, jobs = 0, agencies = 0, stores = 0;
                if (Helpers.BusinessTypeHelper.GetData(Cable) != null) types++;
                if (Helpers.BusinessTypeHelper.GetData(Center) != null) types++;
                var cache = typeof(Helpers.PrefabHelper).GetField("PrefabCache", SF)?.GetValue(null) as Dictionary<string, UnityEngine.Object>;
                var iconCache = typeof(ItemHelper).GetField("ItemIconsCache", SF)?.GetValue(null) as Dictionary<string, Sprite>;
                foreach (var p in Pieces)
                {
                    if (ItemsGetter.GetByName(p.Name, true) != null) items++;
                    string id = p.Name.Substring(p.Name.IndexOf('_') + 1);
                    if ((p.Kind == 0 || p.Kind == 1 || p.Kind == 4 || p.Kind == 2 && Models.Has(id)) && cache != null && cache.TryGetValue(ModelPath(p, id), out var m) && m != null)
                    {
                        models++;
                        if (m is GameObject mg && mg.GetComponent<ModelLights>() != null) own++;
                    }
                    if (iconCache != null && iconCache.TryGetValue(p.Name, out var ic) && ic != null) icons++;
                }
                foreach (var (id, _, _, _) in Jobs) if (SkillHelper.GetData(id) != null) jobs++;
                var city = InstanceBehavior<CityManager>.Instance;
                if (city?.cityBuildingControllers != null)
                    foreach (var cbc in city.cityBuildingControllers)
                    {
                        var st = cbc?.building?.SpecialService?.settings;
                        if (st is Buildings.RecruitmentAgencySettings ra && ra.availableEmployeeSkills != null && System.Array.IndexOf(ra.availableEmployeeSkills, SkTech) + System.Array.IndexOf(ra.availableEmployeeSkills, SkSupport) > -2) agencies++;
                        var reg = cbc?.buildingRegistration;
                        if (reg != null && Services.ContractItemsForSaleService.TryGetItemsForAddress(reg.Address, out var l) && l != null && (l.Contains(ICmts30) || l.Contains(IModem30))) stores++;
                    }
                return $"business types {types}/2, items {items}/{Pieces.Length}, models {models}/{Pieces.Count(x => x.Kind == 0 || x.Kind == 1 || x.Kind == 4 || x.Kind == 2 && Models.Has(x.Name.Substring(x.Name.IndexOf('_') + 1)))} (own {own}), icons {icons}/{Pieces.Length}, jobs {jobs}/{Jobs.Length}, agencies with the jobs {agencies}, stores with the items {stores}";
            }
            catch (System.Exception ex) { return "check failed: " + ex.Message; }
        }

        // ── jobs ──────────────────────────────────────────────────────────────────────────────────────────────────────

        static bool EnsureSkills()
        {
            var dict = typeof(SkillHelper).GetField("Skills", SF)?.GetValue(null) as Dictionary<string, SkillData>;
            if (dict == null) return false;
            bool did = false;
            foreach (var (id, source, wage, second) in Jobs)
            {
                if (dict.ContainsKey(id)) continue;
                if (!_skills.TryGetValue(id, out var k) || k == null)
                {
                    var src = SkillHelper.GetData(source) ?? SkillHelper.GetData("ba:skill_deliverydriver");
                    if (src == null) continue;
                    k = UnityEngine.Object.Instantiate(src);
                    k.name = id; k.skillName = id; k.baseHourlyWage = wage;
                    if (SkillHelper.GetData(second) != null) k.secondarySkill = second;
                    k.hideFlags = HideFlags.HideAndDontSave;
                    _skills[id] = k;
                }
                dict[id] = k;   // after a reload of the game's catalogue (it clears the dictionary) it comes back
                did = true;
            }
            return did;
        }

        // ── business types ────────────────────────────────────────────────────────────────────────────────────────────

        static bool EnsureTypes()
        {
            bool did = false;
            var names = new HashSet<string>(Helpers.BusinessTypeHelper.BusinessTypeNames);
            if (!names.Contains(Cable))
            {
                if (_cable == null)
                {
                    _cable = MakeType(Cable, "ba:businesstype_headquarters", "ba:buildingtype_office");
                    if (_cable != null)
                    {
                        Requirements(_cable);
                        // GO-OFFICE-2 (user 2026-11-07, "beides ja"): like every office business of the game, founding it
                        // needs the Manhattan Business School's course "Office Businesses" (BizMan's start dialog checks it)
                        _cable.courseRequired = DiplomaName.OfficeBusinesses;
                    }
                }
                if (_cable != null && BAModAPI.ModdingAPI.RegisterModBusinessType(_cable)) did = true;
            }
            if (!names.Contains(Center))
            {
                // GO-CENTERS-1 (0.8, user 2026-11-08: "Einzelhandel kann z. B. Kundencenter sein", "Laden mit Kundschaft"): the
                // customer center is a shop - founded in BizMan in a retail building, where the customer service sits and the
                // devices lie (until 0.7 the type stood in offices; such old ones become offices of the provider, GameFound)
                _center ??= MakeType(Center, "ba:businesstype_headquarters", "ba:buildingtype_retail");
                if (_center != null && BAModAPI.ModdingAPI.RegisterModBusinessType(_center)) did = true;
            }
            if (Helpers.BusinessTypeHelper.GetData(Center) != null) RetireCenter();
            return did;
        }

        static bool _centerRetired;

        /// <summary>GO-OFFICE-1 (user 2026-11-07: "die Kopfstelle kann man ja im Kundenbüro errichten, damit würde das Lager
        /// entfallen, und Kundenbüro mit Kopfstelle nur in Büroräume"): the cable provider is one business in an office - the
        /// headend's equipment, the workplaces and the customer service under one roof. The separate customer center of 0.6
        /// stays a registered type, so a save with one loads and keeps it, but BizMan's start dialog no longer offers it (the
        /// game's own list per building type, kept over a reload of the building types).</summary>
        static void RetireCenter()
        {
            if (_centerRetired) return;
            _centerRetired = true;
            var m = typeof(Buildings.BuildingTypeHelper).GetMethod("UnregisterModBusinessTypeAvailability", SF);
            if (m == null) { Go.Warn("game types: the game's list of business types per building type has changed - the old customer center is still offered in offices (GO-OFFICE-1)."); return; }
            bool ok = m.Invoke(null, new object[] { "ba:buildingtype_office", Center }) is bool b && b;
            Go.Log($"game types: the cable provider is founded in offices (the headend and the hubs), the customer center in shops{(ok ? "; it is no longer offered in offices" : "")} (GO-OFFICE-1, GO-CENTERS-1).");
        }

        /// <summary>GO-OFFICE-2 (user 2026-11-07: "simuliere die Miete eines Büros sowie die Einrichtung für z. B. Webdesign,
        /// welche Funktionsweise nötig ist, bis es funktional läuft - das nimmst du dann her um Going Online zu erstellen"): the
        /// cable office is set up like every office business of the game - the web development agency's requirements (work
        /// computers, toilet stalls one per 100 m², sinks one per 200 m², up to four each) and the headend's CMTS. BizMan lists
        /// them under the business's requirements and makes to-do tasks of them at the founding; Going Online's staff in the
        /// office work only when they are met (Crew.Duty), as the game's office businesses do.</summary>
        static void Requirements(BusinessType t)
        {
            var list = new List<Buildings.BuildingTypes.Shared.BusinessRequirement.BusinessRequirement>();
            var cmts = CmtsRequirement();
            if (cmts != null) list.Add(cmts);
            var web = Helpers.BusinessTypeHelper.GetData("ba:businesstype_webdevelopmentagency");
            if (web?.businessRequirements != null) foreach (var r in web.businessRequirements) if (r != null && !list.Contains(r)) list.Add(r);
            t.businessRequirements = list;
            Go.Log($"game types: the cable office needs {string.Join(", ", list.Select(r => r.businessRequirementName))} (GO-OFFICE-2).");
        }

        /// <summary>GO-OFFICE-2: what the office still lacks of its requirements, in the game's own words (empty: all set up,
        /// or not an office of ours).</summary>
        internal static List<string> MissingSetup(BuildingRegistration? reg)
        {
            var r = new List<string>();
            if (reg == null || !IsOurs(reg.businessTypeName) || !InOffice(reg)) return r;
            try
            {
                var t = Helpers.BusinessTypeHelper.GetData(reg.businessTypeName);
                if (t?.businessRequirements == null) return r;
                foreach (var q in t.businessRequirements)
                    if (q != null && !Helpers.BusinessHelper.IsRequirementMet(reg, q)) r.Add(Go.Game(q.GetLocalizeKey()));
            }
            catch { }
            return r;
        }

        static Buildings.BuildingTypes.Shared.BusinessRequirement.BusinessRequirement? _cmtsReq;

        /// <summary>A copy of one of the game's "this item must stand here" requirements, for the CMTS (3.0 or 3.1); it shows
        /// under the CMTS's own name and links to the help page on the headend's equipment.</summary>
        static Buildings.BuildingTypes.Shared.BusinessRequirement.BusinessRequirement? CmtsRequirement()
        {
            if (_cmtsReq != null) return _cmtsReq;
            Buildings.BuildingTypes.Shared.BusinessRequirement.SpecificItemsInBuilding? src = null;
            foreach (var n in Helpers.BusinessTypeHelper.BusinessTypeNames)
            {
                var bt = Helpers.BusinessTypeHelper.GetData(n);
                if (bt?.businessRequirements == null) continue;
                foreach (var r in bt.businessRequirements)
                    if (r is Buildings.BuildingTypes.Shared.BusinessRequirement.SpecificItemsInBuilding sib && !ReferenceEquals(sib, _cmtsReq)) { src = sib; break; }
                if (src != null) break;
            }
            if (src == null) { Go.Warn("game types: no requirement of the game to copy - the cable office lists no CMTS requirement (GO-OFFICE-2)."); return null; }
            var c = UnityEngine.Object.Instantiate(src);
            c.name = "GoingOnlineCmtsRequirement";
            c.items = new[] { ICmts30, ICmts31, IVcmts };   // GO-RPHY-1: a vCMTS server serves as the office's CMTS too
            c.additionalItemsNeededForTutorial = new string[0];
            c.businessRequirementName = "goingonline:businessrequirement_cmts";
            var bt0 = typeof(Buildings.BuildingTypes.Shared.BusinessRequirement.BusinessRequirement);
            bt0.GetField("helpLink", IF)?.SetValue(c, "goingonline-equipment");
            bt0.GetField("todoTaskItemName", IF)?.SetValue(c, ICmts30);   // its name in the list: the CMTS's own
            bt0.GetField("localizeKey", IF)?.SetValue(c, ICmts30);
            c.hideFlags = HideFlags.HideAndDontSave;
            return _cmtsReq = c;
        }

        static BusinessType? MakeType(string name, string source, string buildingType)
        {
            var src = Helpers.BusinessTypeHelper.GetData(source);
            if (src == null) return null;
            var t = UnityEngine.Object.Instantiate(src);
            t.name = name;
            t.businessTypeName = name;
            t.suitableBuildingType = buildingType;
            t.employeePrimarySkills = (string[])JobsOf(name).Clone();   // GO-ASSIGN-2: all of its jobs (UpdateJobs); hiring goes by the workplaces (JobsFor)
            t.courseRequired = default;   // GO-OFFICE-2: not the headquarters' course that came along (the cable type sets its own)
            t.businessRequirements = new List<Buildings.BuildingTypes.Shared.BusinessRequirement.BusinessRequirement>();
            t.spawnCustomers = false;
            t.cityMapFilterColor = new Color(0.10f, 0.62f, 0.75f, 1f);
            var icon = Icon.Load("GO_AppIcon.png");
            if (icon != null) t.icon = icon;
            // LOGO-SHAPES-1: the multiplayer mod puts black-and-white shapes of a cable company into the game's shape list
            // (gps_cable1 ...); with them a new provider's logo is one of those. Without them (the stand-alone mod) LOGO-GO-1:
            // logo shapes of their own - the warehouse has none (the game gives a warehouse only its plain logo), so the cable
            // provider's logo editor and the game's logo generator had nothing to offer; electronics and the web agency's
            // shapes fit a cable company, the customer center keeps the headquarters' and gets those too
            var shapes = new List<string>();
            try
            {
                var avail = LogoHelper.AvailableIcons;
                if (avail != null) foreach (var n in avail) if (n != null && n.StartsWith("gps_cable", System.StringComparison.Ordinal)) shapes.Add(n);
            }
            catch { }
            if (shapes.Count == 0)
            {
                shapes.AddRange(t.logoShapes ?? new List<string>());
                foreach (var from in new[] { "ba:businesstype_electronicsstore", "ba:businesstype_webdevelopmentagency" })
                {
                    var lt = Helpers.BusinessTypeHelper.GetData(from);
                    if (lt?.logoShapes != null) foreach (var sh in lt.logoShapes) if (!string.IsNullOrEmpty(sh) && !shapes.Contains(sh)) shapes.Add(sh);
                }
            }
            if (shapes.Count > 0) t.logoShapes = shapes;
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        // ── goods and furniture ───────────────────────────────────────────────────────────────────────────────────────

        static readonly Dictionary<string, float> _retry = new Dictionary<string, float>();

        static bool EnsureItems()
        {
            bool did = false;
            foreach (var p in Pieces)
            {
                if (ItemsGetter.IsModItem(p.Name) && ItemsGetter.GetByName(p.Name, true) != null) { Visuals(p); continue; }
                if (_retry.TryGetValue(p.Name, out var at) && Time.unscaledTime < at) continue;
                if (p.Source == AutoStation) p.Source = FindStation() ?? AutoStation;
                var src = p.Source == AutoStation ? null : ItemsGetter.GetByName(p.Source, true);
                if (src == null)
                {
                    if (!_retry.ContainsKey(p.Name)) Go.Warn($"game types: the template {p.Source} for {p.Name} is missing - tried again every minute.");
                    _retry[p.Name] = Time.unscaledTime + 60f;
                    continue;
                }
                if (!_items.TryGetValue(p.Name, out var it) || it == null)
                {
                    it = UnityEngine.Object.Instantiate(src);
                    it.name = p.Name; it.itemName = p.Name;
                    typeof(Item).GetField("defaultMarketPrice", IF)?.SetValue(it, p.Price);
                    if (p.Kind == 2) { it.wholesalePrice = p.Price; it.isADemandedProduct = false; it.boxSize = p.Box; it.limitDemandToNeighbourhoods = new string[0]; }
                    if (p.Kind == 3) { it.isADemandedProduct = false; it.wholesalePrice = 0f; }
                    if (p.Skills != null) it.suitableSkills = p.Skills;
                    var icon = Sprite(p, src);
                    if (icon != null) it.icon = icon;
                    it.hideFlags = HideFlags.HideAndDontSave;
                    _items[p.Name] = it;
                }
                ItemsGetter.RegisterModItem(it);
                Visuals(p);
                did = true;
            }
            Models.Report();
            return did;
        }

        /// <summary>The game's workstation for its office jobs (a programmer works there), a desk first.</summary>
        static string? FindStation()
        {
            string? any = null;
            try
            {
                foreach (var it in ItemsGetter.AllItems)
                {
                    if (it == null || !it.type.HasFlag(ItemType.EmployeeWorkstation) || it.suitableSkills == null) continue;
                    if (System.Array.IndexOf(it.suitableSkills, "ba:skill_programmer") < 0) continue;
                    if (ItemsGetter.IsModItem(it.itemName)) continue;
                    if (it.itemName.Contains("desk") || it.itemName.Contains("workstation")) { Go.Log("game types: workplaces made from " + it.itemName + "."); return it.itemName; }
                    any ??= it.itemName;
                }
            }
            catch { }
            if (any != null) Go.Log("game types: workplaces made from " + any + ".");
            return any;
        }

        static Sprite? Sprite(Piece p, Item src)
        {
            if (_icons.TryGetValue(p.Name, out var have) && have != null) return have;
            Sprite? sp = p.IconFile.Length > 0 ? Icon.Load(p.IconFile) : null;
            if (sp == null) { try { sp = src.icon != null ? src.icon : ItemHelper.GetIcon(p.Source); } catch { } }
            if (sp != null) _icons[p.Name] = sp;
            return sp;
        }

        /// <summary>Where the game looks for a piece's model. Furniture is placed: "Prefabs/&lt;id&gt;.prefab" (PrefabHelper.LoadPrefab).
        /// GO-DEVICE-VISUALS-1 (the user's server log 2026-11-08: "game types: no model for ba:itemname_smartphone1", 2.5 times a
        /// second - 92,275 lines on server 3): a device is goods like a smartphone, and the game shows goods by their display
        /// model "Prefabs/Visuals/&lt;id&gt;.prefab" (PrefabHelper.CreateVisualForItemNamePooled, the cardboard box when there is
        /// none). The smartphone has no "Prefabs/smartphone1.prefab", so the devices' template was never found.</summary>
        static string ModelPath(Piece p, string id) => p.Kind == 2 ? "Prefabs/Visuals/" + id + ".prefab" : "Prefabs/" + id + ".prefab";

        /// <summary>A template whose model was not found: warned once, looked for again once a minute (the lookup walks the
        /// game's bundles, and this runs every couple of seconds).</summary>
        static readonly Dictionary<string, float> _noModel = new Dictionary<string, float>();

        /// <summary>The model the game finds under the item's name (<see cref="ModelPath"/>), and its icon (ItemHelper's icon
        /// cache; the address table must know the icon path, else the game shows a cardboard box).</summary>
        static void Visuals(Piece p)
        {
            string id = p.Name.Substring(p.Name.IndexOf('_') + 1), srcId = p.Source.Substring(p.Source.IndexOf('_') + 1);
            // the icon
            try
            {
                if (_icons.TryGetValue(p.Name, out var sp) && sp != null)
                {
                    if (typeof(ItemHelper).GetField("ItemIconsCache", SF)?.GetValue(null) is Dictionary<string, Sprite> icons && !icons.ContainsKey(p.Name))
                        icons[p.Name] = sp;
                    if (typeof(BigAmbitions.SaveSystem.AddressableResolver).GetField("CachedAddressLookups", SF)?.GetValue(null) is Dictionary<string, string> lookups)
                    {
                        string key = ("ItemIcons/" + id + ".png").ToLowerInvariant();
                        if (!lookups.ContainsKey(key) && lookups.TryGetValue(("ItemIcons/" + srcId + ".png").ToLowerInvariant(), out var real)) lookups[key] = real;
                    }
                }
            }
            catch { }
            if (p.Kind == 3) return;                       // a service line has no model
            if (p.Kind == 2 && !Models.Has(id)) return;    // a device without a model of its own lies in its box
            // the model
            try
            {
                if (!(typeof(Helpers.PrefabHelper).GetField("PrefabCache", SF)?.GetValue(null) is Dictionary<string, UnityEngine.Object> cache)) return;
                string path = ModelPath(p, id);
                if (cache.TryGetValue(path, out var have) && have != null) return;
                if (_noModel.TryGetValue(p.Name, out var again) && Time.unscaledTime < again) return;
                // LoadPrefabAssetByName(name) loads "Prefabs/<name>.prefab", so "Visuals/<id>" reaches a display model
                var prefab = Helpers.PrefabHelper.LoadPrefabAssetByName(p.Kind == 2 ? "Visuals/" + srcId : srcId);
                if (prefab == null)
                {
                    if (!_noModel.ContainsKey(p.Name)) Go.Warn($"game types: no model for {p.Source} ({p.Name}) - looked for again every minute (GO-DEVICE-VISUALS-1).");
                    _noModel[p.Name] = Time.unscaledTime + 60f;
                    return;
                }
                if (_noModel.Remove(p.Name)) Go.Log($"game types: model for {p.Name} found after all (GO-DEVICE-VISUALS-1).");
                if (_holder == null)
                {
                    _holder = new GameObject("GoingOnline_Models");
                    _holder.SetActive(false);   // the copies inside stay active themselves, so the game's copies of them are
                    UnityEngine.Object.DontDestroyOnLoad(_holder);
                }
                var go = UnityEngine.Object.Instantiate(prefab, _holder.transform);
                go.name = id;
                var ic = go.GetComponent<ItemController>();
                if (ic != null) ic.itemName = p.Name;
                bool own = p.Kind != 1 && Models.Apply(go, id, p.Tint);        // GO-MODELS-1: our model on the template's renderer
                if (!own && p.Tint != Color.white) Tint(go, p.Tint);
                if (own) { if (go.GetComponent<ModelLights>() == null) go.AddComponent<ModelLights>(); }
                else if (p.Kind == 0 && go.GetComponent<RackLights>() == null) go.AddComponent<RackLights>();   // the equipment's LEDs blink
                cache[path] = go;
            }
            catch (System.Exception ex) { Go.Warn($"game types: model for {p.Name}: {ex.Message}"); }
        }

        /// <summary>Own copies of the materials, darkened or coloured (the game's own furniture keeps its look).</summary>
        static void Tint(GameObject go, Color tint)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var m = new Material(mats[i]);
                    foreach (var prop in new[] { "_BaseColor", "_Color" })
                        if (m.HasProperty(prop)) { var c = m.GetColor(prop); m.SetColor(prop, new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a)); }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        // ── the city: recruitment agencies, stores, importers ──────────────────────────────────────────────────────────

        static float _hooksNext;
        static string _hooksKey = "";

        /// <summary>GO-HOOKS-2 (user 2026-11-07: "finde keine Techniker oder Meister usw. bei der Personalagentur" - all three
        /// agencies answered "no jobs" for the cable provider): the hooks ran once per city, the moment its buildings existed.
        /// After a reconnect the game had fresh copies of the agencies' settings whose services were not ready yet ("0
        /// recruitment agencies" in the Player.log, and the business pack took the furniture store for an appliance store),
        /// and nothing tried again. Now they run every 20 s; each step only adds what is missing, so a repeat changes nothing.</summary>
        static void CityHooks()
        {
            CityManager? city = null;
            try { city = InstanceBehavior<CityManager>.Instance; } catch { }
            if (city == null || SaveGameManager.Current == null) { _city = null; return; }
            bool first = !ReferenceEquals(city, _city);
            if (!first && Time.unscaledTime < _hooksNext) return;
            var cbcs = city.cityBuildingControllers;
            if (cbcs == null || cbcs.Length == 0) return;
            _city = city;
            _hooksNext = Time.unscaledTime + 20f;
            _furnitureStores.Clear(); _deviceShops.Clear(); _wholesaleShops.Clear(); _importShops.Clear(); _wholesaleAt.Clear();
            var furniture = Pieces.Where(p => p.Kind == 0 || p.Kind == 1 || p.Kind == 4).Select(p => p.Name).ToList();
            var devices = Pieces.Where(p => p.Kind == 2).Select(p => p.Name).ToList();
            var electronics = new[] { "ba:itemname_smartphone1", "ba:itemname_smartphone2", "ba:itemname_laptop", "ba:itemname_headphones01" };
            int agencies = 0, offering = 0;
            var hiring = new List<string>();
            var furnStores = new List<BuildingRegistration>(); var anyFurniture = new List<BuildingRegistration>();
            var wholesale = new List<CityBuildingController>(); var importers = new List<BuildingRegistration>();
            foreach (var cbc in cbcs)
            {
                var settings = cbc?.building?.SpecialService?.settings;
                if (settings == null) continue;
                if (settings is Buildings.RecruitmentAgencySettings ra)
                {
                    if (OfferJobs(ra)) agencies++;
                    var offered = JobsAt(ra);
                    if (offered.Contains(SkTech) || offered.Contains(SkMaster)) offering++;
                    if (offered.Count > 0) hiring.Add($"{cbc!.buildingRegistration?.BusinessName ?? "?"} {offered.Count}");
                    continue;
                }
                var reg = cbc!.buildingRegistration;
                if (reg == null) continue;
                if (settings is Buildings.ImportExportSettings ie && ie.itemsAvailable != null)
                {
                    // an importer with electronics brings the devices too - imported through a purchasing agent at a
                    // headquarters into a warehouse, as the game has it for every import
                    if (ie.itemsAvailable.Any(x => electronics.Contains(x)))
                    {
                        foreach (var d in devices) Add(ie.itemsAvailable, d);
                        importers.Add(reg);
                    }
                    continue;
                }
                if (settings is FurnitureStoreSettings) { anyFurniture.Add(reg); if (Sells(reg, "ba:itemname_computerworkstation")) furnStores.Add(reg); continue; }
                // GO-SHOPS-2 (user 2026-11-06: "brauche für den Mod Großhändler, die Modems anbieten usw., und Importe später, wenn
                // man Lager hat und Hauptquartier"): the city's wholesalers all sell the same goods and no electronics, so the
                // old pick (wholesalers with electronics, else the first two in load order) took two at random - other ones on
                // every start and on every machine. Now every wholesaler sells the devices.
                if (settings is Buildings.WholesaleStoreSettings) wholesale.Add(cbc);
            }
            if (furnStores.Count == 0) furnStores = anyFurniture.OrderBy(r => r.BusinessName ?? "").ThenBy(r => City.KeyOf(r.Address)).Take(2).ToList();
            foreach (var reg in furnStores) { Offer(reg, furniture); Shop(_furnitureStores, reg); }
            int shelves = 0;
            foreach (var cbc in wholesale.OrderBy(c => c.buildingRegistration.BusinessName ?? "").ThenBy(c => City.KeyOf(c.buildingRegistration.Address)))
            {
                var reg = cbc.buildingRegistration;
                shelves += StockShelves(reg, devices);
                Offer(reg, devices);
                Shop(_wholesaleShops, reg);
                try { _wholesaleAt.Add(new KeyValuePair<Vector3, string>(cbc.transform.position, _wholesaleShops[_wholesaleShops.Count - 1])); } catch { }
            }
            foreach (var reg in importers.OrderBy(r => r.BusinessName ?? "").ThenBy(r => City.KeyOf(r.Address))) Shop(_importShops, reg);
            _deviceShops.AddRange(_wholesaleShops); _deviceShops.AddRange(_importShops);
            string key = offering + "|" + string.Join(",", hiring) + "|" + shelves + "|" + string.Join(",", _furnitureStores) + "|" + string.Join(",", _deviceShops);
            if (!first && agencies == 0 && key == _hooksKey) return;   // a repeat that found everything in place
            _hooksKey = key;
            Go.Log($"game types in the city: {offering} recruitment agencies offer the jobs ({agencies} added now); jobs for the cable provider per agency: {(hiring.Count > 0 ? string.Join(", ", hiring) : "none")} (type {TypeNote(Cable)}); equipment at {string.Join(", ", _furnitureStores)}; devices on {shelves} shelves at the wholesalers ({string.Join(", ", _wholesaleShops)}) and by import ({string.Join(", ", _importShops)}) (GO-SHOPS-2, GO-HOOKS-2, GO-HIRE-1).");
        }

        static void Add(List<string> list, string x) { if (!list.Contains(x)) list.Add(x); }

        /// <summary>All of the provider's jobs, in the order the dialogs list them.</summary>
        static readonly string[] AllJobs = { SkTech, SkSwitch, SkMaster, SkSales, SkSupport, SkService };
        /// <summary>GO-CENTERS-1 (user 2026-11-08: "im Kundenbüro Kundendienstler und Reinigungskraft, im Büro der Netzmeister,
        /// Reinigungskraft, Schalter und Techniker"): the offices' jobs and the customer centers' (cleaning is the game's own job
        /// of the building, BizMan offers it by itself). Technical support and field sales stay with the offices.</summary>
        static readonly string[] OfficeJobs = { SkTech, SkSwitch, SkMaster, SkSales, SkSupport };
        static readonly string[] CenterJobs = { SkService };
        static string[] JobsOf(string type) => type == Center ? CenterJobs : OfficeJobs;

        /// <summary>GO-BIZMAN-3: the customer devices (modems, ONT, set-top box, smartcard) - goods on the storage shelves.</summary>
        internal static IEnumerable<string> DeviceItems => Pieces.Where(p => p.Kind == 2).Select(p => p.Name);
        internal static bool IsDevice(string? item) => !string.IsNullOrEmpty(item) && Pieces.Any(p => p.Kind == 2 && p.Name == item);

        /// <summary>GO-HIRE-1: the city's recruitment agencies offer our jobs - next to the delivery drivers the cable
        /// technicians and switching staff, next to the office jobs the network masters, field sales and the technical
        /// support. True when something was added.</summary>
        internal static bool OfferJobs(Buildings.RecruitmentAgencySettings ra)
        {
            var have = new List<string>(ra.availableEmployeeSkills ?? new string[0]);
            int before = have.Count;
            if (have.Contains("ba:skill_deliverydriver")) { Add(have, SkTech); Add(have, SkSwitch); }
            if (have.Contains("ba:skill_programmer") || have.Contains("ba:skill_logisticsmanager")) { Add(have, SkMaster); Add(have, SkSales); Add(have, SkSupport); }
            if (have.Count == before) return false;
            ra.availableEmployeeSkills = have.ToArray();
            return true;
        }

        /// <summary>GO-HIRE-1: which of the provider's jobs this agency has (with the workplaces for them, the recruitment
        /// dialog offers those).</summary>
        internal static List<string> JobsAt(Buildings.RecruitmentAgencySettings ra)
        {
            var have = ra.availableEmployeeSkills ?? new string[0];
            var r = new List<string>();
            foreach (var k in AllJobs) if (System.Array.IndexOf(have, k) >= 0) r.Add(k);
            return r;
        }

        // ── GO-WORK-1: the jobs follow the workplaces ─────────────────────────────────────────────────────────────────

        static float _jobsNext;

        /// <summary>GO-WORK-1: the provider's jobs whose workplace stands in the building - an item BizMan's schedule gives
        /// shifts at: Going Online's technical workplace (cable technicians, switching staff, network masters) or customer
        /// center workplace (customer service, technical support, field sales, network masters).</summary>
        internal static List<string> WorkplaceJobs(BuildingRegistration? reg)
        {
            var r = new List<string>();
            if (reg?.itemInstances == null) return r;
            var seen = new HashSet<string>();
            foreach (var inst in reg.itemInstances.Values)
            {
                if (inst == null || string.IsNullOrEmpty(inst.itemName) || !seen.Add(inst.itemName)) continue;
                var it = ItemsGetter.GetByName(inst.itemName, true);
                if (it == null || !it.assignable || it.suitableSkills == null) continue;
                foreach (var k in it.suitableSkills) if (RoleOf(k) != 255) Add(r, k);
            }
            r.Sort((a, b) => System.Array.IndexOf(AllJobs, a).CompareTo(System.Array.IndexOf(AllJobs, b)));
            return r;
        }

        /// <summary>GO-WORK-1 (user 2026-11-07: "für Techniker und Kundendienst muss ein Arbeitsplatz errichtet werden, anhand
        /// der Arbeitsplätze kann man die Leute einstellen"): the recruitment dialog offers a business of ours only the jobs whose
        /// workplaces stand in it - JobsFor sets them the moment the business is picked there (GO-HIRE-1), NoJobsNote says
        /// what is missing; in the multiplayer mod the job board goes by them too.
        /// GO-ASSIGN-2 (user 2026-11-08, twice: "Kann Mitarbeiter der Firma nicht zuweisen Kabel Guru"): the type itself names
        /// ALL of the provider's jobs. The employees app (one person, the mass actions, the candidates) offers a business
        /// only to people whose job its type names, and the type's list followed the workplaces: with none it was empty, and
        /// the office's work computers count as network masters' workplaces (GO-HQ-1), so an office with computers named
        /// only that job - the cable technicians and customer service people could not be assigned. BizMan's schedule finds
        /// the workstations through this list as well. Every two seconds; true when a list changed.</summary>
        internal static bool UpdateJobs(bool now = false)
        {
            if (!now && Time.unscaledTime < _jobsNext) return false;
            _jobsNext = Time.unscaledTime + 2f;
            bool did = false;
            foreach (var type in new[] { Cable, Center })
            {
                var jobs = new List<string>(JobsOf(type));
                did |= SetJobs(Helpers.BusinessTypeHelper.GetData(type), jobs);
                did |= SetJobs(type == Cable ? _cable : _center, jobs);
            }
            return did;
        }

        static bool SetJobs(BusinessType? t, List<string> jobs)
        {
            if (t == null) return false;
            var have = t.employeePrimarySkills;
            if (have != null && have.SequenceEqual(jobs)) return false;
            t.employeePrimarySkills = jobs.ToArray();
            return true;
        }

        /// <summary>GO-WORK-1: the recruitment dialog, the moment a business of ours is picked - its type names exactly this
        /// building's jobs (the next round of UpdateJobs names all jobs again). Returns those jobs.</summary>
        internal static List<string> JobsFor(BuildingRegistration reg)
        {
            var jobs = WorkplaceJobs(reg);
            var allowed = JobsOf(reg.businessTypeName);   // GO-CENTERS-1: customer service in the customer centers, the rest in the offices
            jobs.RemoveAll(k => System.Array.IndexOf(allowed, k) < 0);
            if (Helpers.BusinessTypeHelper.GetData(reg.businessTypeName) == null) EnsureTypes();
            SetJobs(Helpers.BusinessTypeHelper.GetData(reg.businessTypeName), jobs);
            return jobs;
        }

        /// <summary>GO-WORK-1: why an agency has nobody for this business of ours ("" = it has someone): no workplace there
        /// yet, or none for a job this agency has.</summary>
        internal static string NoJobsNote(BuildingRegistration reg, string[]? agency)
        {
            var jobs = WorkplaceJobs(reg);
            var allowed = JobsOf(reg.businessTypeName);
            jobs.RemoveAll(k => System.Array.IndexOf(allowed, k) < 0);
            string name = string.IsNullOrWhiteSpace(reg.BusinessName) ? City.Name(City.KeyOf(reg.Address)) : reg.BusinessName.Trim();
            if (jobs.Count == 0 && reg.businessTypeName == Center)
                return Go.T("go_hire_nocenterdesk", "{0} has no customer workplace yet. In a customer center the agencies offer customer service: set up Going Online's customer center workplace there (office furniture store) - a computer on a desk, with a chair.", name);
            if (jobs.Count == 0)
                return Go.T("go_hire_noworkplace3", "{0} has no workplace yet. The agencies offer the jobs whose workplaces stand there: set up Going Online's technical workplace (cable technicians, switching staff, network masters), control room workplace (switching staff, network masters) or customer center workplace (technical support, field sales, network masters) from an office furniture store - a computer on a desk, with a chair. Customer service works in your customer centers (shops).", name);
            foreach (var k in jobs) if (agency != null && System.Array.IndexOf(agency, k) >= 0) return "";
            var names = new List<string>();
            foreach (var k in jobs) names.Add(Go.Game(k));
            return Go.T("go_hire_otheragency", "This agency has nobody for the workplaces in {0} ({1}). Cable technicians, switching staff and customer service are at Anderson Recruitment Corp., network masters, field sales and technical support at City Workforce Inc.", name, string.Join(", ", names));
        }

        /// <summary>For the log: the type's jobs as the game sees them.</summary>
        static string TypeNote(string typeName)
        {
            try
            {
                var t = Helpers.BusinessTypeHelper.GetData(typeName);
                if (t == null) return "not registered";
                return (ReferenceEquals(t, typeName == Cable ? _cable : _center) ? "" : "another object, ") + (t.employeePrimarySkills?.Length ?? 0) + " jobs from the workplaces";
            }
            catch (System.Exception ex) { return ex.Message; }
        }

        /// <summary>GO-SHOPS-2: a wholesaler sells in the store only what stands on its pallet shelves (each shelf's
        /// playerItemPurchaserSettings names one good) - a store list alone left the devices on no shelf, so nobody could buy
        /// them there (user 2026-11-06 at Hudson Wholesale). In the game's own cached copy of the store's layout (the one
        /// BuildingManager builds the store from when someone walks in) a few shelves change to the devices: a shelf whose
        /// sign names it alone first (shelf and sign change), then the outer shelf of three under one sign, then one of two.
        /// Every good keeps at least one shelf, and the same layout file gives the same shelves on every machine.</summary>
        static int StockShelves(BuildingRegistration reg, List<string> devices)
        {
            global::BusinessLayoutSet? set = null;
            try { set = BusinessLayoutSets.BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(reg.businessTypeName, new Blueprints.BuildingSizeInfo(reg), reg.Layout, false); }
            catch (System.Exception ex) { Go.Warn("game types: the layout of " + City.KeyOf(reg.Address) + ": " + ex.Message); }
            if (set?.Items == null) return 0;
            bool OnShelf(BusinessLayoutSets.Item? i, string kind) => i != null && i.itemName == kind && i.playerItemPurchaserSettings != null && !string.IsNullOrEmpty(i.playerItemPurchaserSettings.itemName);
            var shelves = set.Items.Where(i => OnShelf(i, "ba:itemname_palletshelf")).ToList();
            int done = shelves.Count(i => devices.Contains(i.playerItemPurchaserSettings.itemName));
            if (done > 0) return done;   // this cached layout has them already (another store with the same layout, or a second city load)
            var signs = set.Items.Where(i => OnShelf(i, "ba:itemname_hangingsignhigh")).ToList();
            float Dist(BusinessLayoutSets.Item a, BusinessLayoutSets.Item b) { float dx = a.position.x - b.position.x, dz = a.position.z - b.position.z; return Mathf.Sqrt(dx * dx + dz * dz); }
            float Key(BusinessLayoutSets.Item a) => a.position.x * 10000f + a.position.z;
            var under = new Dictionary<BusinessLayoutSets.Item, List<BusinessLayoutSets.Item>>();
            foreach (var sh in shelves)
            {
                string good = sh.playerItemPurchaserSettings.itemName;
                BusinessLayoutSets.Item? sign = null; float best = float.MaxValue;
                foreach (var sg in signs)
                {
                    if (sg.playerItemPurchaserSettings.itemName != good) continue;
                    float d = Dist(sh, sg);
                    if (d < best) { best = d; sign = sg; }
                }
                if (sign == null) continue;
                if (!under.TryGetValue(sign, out var list)) under[sign] = list = new List<BusinessLayoutSets.Item>();
                list.Add(sh);
            }
            var perGood = shelves.GroupBy(s => s.playerItemPurchaserSettings.itemName).ToDictionary(g => g.Key, g => g.Count());
            var picks = new List<(int rank, string good, float key, BusinessLayoutSets.Item shelf, BusinessLayoutSets.Item? sign)>();
            foreach (var kv in under)
            {
                string good = kv.Key.playerItemPurchaserSettings.itemName;
                if (perGood[good] < 2) continue;   // a good's only shelf stays
                var group = kv.Value;
                if (group.Count == 1) picks.Add((0, good, Key(group[0]), group[0], kv.Key));
                else
                {
                    var edge = group.OrderByDescending(s => Dist(s, kv.Key)).ThenBy(s => Key(s)).First();
                    picks.Add((group.Count >= 3 ? 1 : 2, good, Key(edge), edge, null));
                }
            }
            int n = 0;
            var used = new HashSet<string>();   // one shelf per good
            foreach (var pk in picks.OrderBy(x => x.rank).ThenBy(x => x.good, System.StringComparer.Ordinal).ThenBy(x => x.key))
            {
                if (n >= devices.Count) break;
                if (!used.Add(pk.good)) continue;
                pk.shelf.playerItemPurchaserSettings.itemName = devices[n];
                pk.shelf.playerItemPurchaserSettings.enabled = true;
                if (pk.sign != null) pk.sign.playerItemPurchaserSettings.itemName = devices[n];
                n++;
            }
            return n;
        }

        static bool Sells(BuildingRegistration reg, string item)
        {
            try { var l = reg.GetListOfItemsForSale(); return l != null && l.Contains(item); } catch { return false; }
        }

        /// <summary>The store sells its own goods and ours (in the store and by phone: the game reads the same list).</summary>
        static void Offer(BuildingRegistration reg, List<string> ours)
        {
            try
            {
                var list = new List<string>(reg.GetListOfItemsForSale() ?? new List<string>());
                foreach (var x in ours) Add(list, x);
                Services.ContractItemsForSaleService.SetItemsForAddress(reg.Address, list);
            }
            catch (System.Exception ex) { Go.Warn("game types: store " + City.KeyOf(reg.Address) + ": " + ex.Message); }
        }

        static void Shop(List<string> into, BuildingRegistration reg)
        {
            string where;
            try { where = Streets.AddressHelper.ToFormattedString(reg.Address); } catch { where = City.Name(City.KeyOf(reg.Address)); }
            string n = !string.IsNullOrWhiteSpace(reg.BusinessName) ? reg.BusinessName + " (" + where + ")" : where;
            if (!into.Contains(n)) into.Add(n);
        }

        // ── help (F1) ─────────────────────────────────────────────────────────────────────────────────────────────────

        const string HelpCategory = "goingonline_help_category";
        static readonly string[] HelpPages = { "start", "offices", "equipment", "devices", "staff", "customers", "rival", "remove" };

        /// <summary>The help builds its chapters from helpstructure.json when it is first opened (and again on a language
        /// change); ours is added to its list then and the chapters are built once more.</summary>
        static void Help()
        {
            try
            {
                var hs = InstanceBehavior<HelpSystem>.Instance;
                if (hs == null) return;
                if (!(typeof(HelpSystem).GetField("_isInitialized", IF)?.GetValue(hs) is bool init) || !init) return;
                if (!(typeof(HelpSystem).GetField("_categories", IF)?.GetValue(hs) is List<UnityEngine.UI.Extensions.HelpSystem.HelpStructureGroupEntry> cats)) return;
                if (cats.Exists(c => c != null && c.CategoryLocalizorKey == HelpCategory)) return;
                var entry = new UnityEngine.UI.Extensions.HelpSystem.HelpStructureGroupEntry { CategoryLocalizorKey = HelpCategory, Pages = new List<UnityEngine.UI.Extensions.HelpSystem.HelpStructurePageEntry>() };
                foreach (var page in HelpPages)
                    entry.Pages.Add(new UnityEngine.UI.Extensions.HelpSystem.HelpStructurePageEntry { Slug = "goingonline-" + page, PageLocalizorKeyPrefix = "goingonline_help_" + page });
                cats.Add(entry);
                typeof(HelpSystem).GetMethod("LoadCategories", IF)?.Invoke(hs, null);
            }
            catch (System.Exception ex) { Go.Warn("help: " + ex.Message); }
        }

        // ── removing the mod safely ───────────────────────────────────────────────────────────────────────────────────

        /// <summary>Turns everything of the mod in this save back into the game's own, so the save loads without the mod:
        /// the cable provider becomes a warehouse, a customer center an empty building; the equipment and the workplaces
        /// become the game's furniture they were made from; the devices in storage go; the staff keep their jobs as the
        /// game's jobs they were modelled on. Returns what was changed (for the app's message).</summary>
        public static (int businesses, int furniture, int devices, int staff) Uninstall()
        {
            int b = 0, f = 0, d = 0, e = 0;
            var game = SaveGameManager.Current;
            if (game == null) return (0, 0, 0, 0);
            foreach (var reg in game.BuildingRegistrations)
            {
                if (reg == null) continue;
                if (reg.businessTypeName == Cable && BuildingTypeOf(reg) == "ba:buildingtype_warehouse") { reg.businessTypeName = "ba:businesstype_warehouse"; b++; }   // a headend from 0.6
                else if (IsOurs(reg.businessTypeName)) { reg.businessTypeName = "ba:businesstype_empty"; reg.TemporarilyClose(true); b++; }
                if (reg.itemInstances == null) continue;
                foreach (var inst in reg.itemInstances.Values)
                {
                    if (inst == null) continue;
                    var p = Find(inst.itemName);
                    if (p != null && (p.Kind == 0 || p.Kind == 1 || p.Kind == 4)) { inst.itemName = p.Source; f++; }
                    if (inst.cargoInstances != null) d += inst.cargoInstances.RemoveAll(c => c != null && Find(c.itemName)?.Kind == 2);
                }
            }
            foreach (var emp in Helpers.EmployeeHelper.EmployeeInstancesDictionary.Values)
            {
                var skills = emp?.characterData?.skills;
                if (skills == null) continue;
                foreach (var sk in skills)
                    foreach (var (id, source, _, _) in Jobs)
                        if (sk != null && sk.name == id) { sk.name = source; e++; }
            }
            return (b, f, d, e);
        }
    }
}
