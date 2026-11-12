using System;
using System.Collections.Generic;
using System.Linq;
using BigAmbitions.Characters.Skills;
using BigAmbitions.Items;
using Buildings.BuildingTypes.Shared.Dirtiness;
using Helpers;
using Helpers.BusinessSimulation;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>BUSINESS-PACK-1 (user 2026-11-06, after the Workshop mods IT Business Expansion and Skill Kit by Cow a Bunga and
    /// Spotless by We1tall: "Ja, nachbauen" - as modules of this server, which runs no other mods): five new businesses in the
    /// game's own catalogues, our own code. The game runs them like any business - BizMan founds them, the player hires the
    /// staff at the recruitment agencies, gives them workplaces and shifts and sets the prices of the business's services in
    /// BizMan, and the game pays the sales at midnight. What is new about each runs in its own simulator, which the game calls
    /// every hour for an open business (BusinessType.simulator):
    /// - IT firm (office): IT Support, Software Development, Cloud Services - billable hours of the staff at IT workplaces,
    ///   with servers (blade, rack, mainframe) that must stand in the office and cost power every day;
    /// - Cleaning company (office): the crews first clean the player's own other businesses, then clients of the city pay
    ///   by the hour; floor scrubbers, carpet extractors and window kits make them faster;
    /// - Laundromat (shop): washers and dryers, people of the neighbourhood wash, dry and leave their laundry for wash and
    ///   fold; water, power and detergent cost a little per load.
    /// The registration runs from the main menu on, on a dedicated server too (a save with these businesses must load); the
    /// AMP switches (ModuleItFirm, ModuleCleaning, ModuleLaundromat) decide whether the simulators work.</summary>
    internal static class BizPack
    {
        // ── business types ───────────────────────────────────────────────────────────────────────────────────────────
        public const string ItSupport = "gps:businesstype_itsupport", SoftDev = "gps:businesstype_softwaredev",
                            Cloud = "gps:businesstype_cloudservices", Cleaning = "gps:businesstype_cleaningcompany",
                            Laundromat = "gps:businesstype_laundromat";

        // ── jobs (the cleaners and attendants are the game's own) ───────────────────────────────────────────────────
        public const string SkItSupport = "gps:skill_itsupport", SkSoftDev = "gps:skill_softwaredeveloper",
                            SkCloud = "gps:skill_cloudengineer", SkCleanLead = "gps:skill_cleaninglead",
                            SkCleaning = "ba:skill_cleaning", SkService = "ba:skill_customerservice";

        // ── items (an id has exactly one "_": the game finds the model under the part after it) ─────────────────────
        public const string IItDesk = "gps:itemname_gpsitdesk", IBlade = "gps:itemname_gpsbladeserver", IRack = "gps:itemname_gpsrackserver",
                            IMainframe = "gps:itemname_gpsmainframe",
                            ICart = "gps:itemname_gpscleancart", IScrubber = "gps:itemname_gpsscrubber", IExtractor = "gps:itemname_gpscarpetextractor",
                            IWindowKit = "gps:itemname_gpswindowkit",
                            IWasher8 = "gps:itemname_gpswasher8", IWasher12 = "gps:itemname_gpswasher12", IWasher27 = "gps:itemname_gpswasher27",
                            IDryer15 = "gps:itemname_gpsdryer15", IDryer30 = "gps:itemname_gpsdryer30", IFoldTable = "gps:itemname_gpsfoldtable",
                            IChanger = "gps:itemname_gpschanger",
                            SItSupport = "gps:itemname_gpsitsupporthour", SSoftDev = "gps:itemname_gpssoftwarehour", SCloud = "gps:itemname_gpscloudhour",
                            SClean = "gps:itemname_gpscleaninghour", SWash = "gps:itemname_gpswash", SDry = "gps:itemname_gpsdry", SFold = "gps:itemname_gpswashfold";

        internal sealed class ItemDef
        {
            public string Name = "", Source = "", IconFile = "";
            public byte Kind;            // 0 equipment (furniture), 1 a workplace, 3 a service line the business sells
            public float Price, PerDay;  // the store price, power or water per day (equipment)
            public Color Tint = Color.white;
            public string[]? Skills;
            public bool Lights;          // blinking LEDs (servers)
            public byte Shop;            // 0 the office furniture store, 1 an appliance store (washers, dryers)
        }

        internal sealed class TypeDef
        {
            public string Name = "", Source = "", Building = "", Product = "", Module = "", IconFile = "";
            public string[] Skills = new string[0];
            public Func<BusinessSimulator> Simulator = () => ScriptableObject.CreateInstance<ItSim>();
            public Color Colour = new Color(0.3f, 0.5f, 0.9f, 1f);
        }

        static readonly Color Graphite = new Color(0.30f, 0.33f, 0.38f), Steel = new Color(0.82f, 0.85f, 0.88f), White = new Color(0.96f, 0.97f, 0.98f);
        const string AutoStation = "auto:station";

        public static readonly ItemDef[] Items =
        {
            // IT firm
            new ItemDef { Name = IItDesk, Source = AutoStation, Kind = 1, Price = 2500f, Tint = new Color(0.86f, 0.90f, 1f), Skills = new[] { SkItSupport, SkSoftDev, SkCloud } },
            new ItemDef { Name = IBlade, Source = "ba:itemname_smallcabinet", Kind = 0, Price = 2500f, PerDay = 40f, Tint = Graphite, Lights = true, IconFile = "BP_Blade.png" },
            new ItemDef { Name = IRack, Source = "ba:itemname_bigcabinet", Kind = 0, Price = 5000f, PerDay = 80f, Tint = Graphite, Lights = true, IconFile = "BP_Rack.png" },
            new ItemDef { Name = IMainframe, Source = "ba:itemname_bigcabinet", Kind = 0, Price = 35000f, PerDay = 160f, Tint = new Color(0.20f, 0.24f, 0.30f), Lights = true, IconFile = "BP_Mainframe.png" },
            // cleaning company
            new ItemDef { Name = ICart, Source = "ba:itemname_cleaningstation", Kind = 1, Price = 900f, Tint = new Color(0.55f, 0.75f, 0.95f), Skills = new[] { SkCleaning, SkCleanLead } },
            new ItemDef { Name = IScrubber, Source = "ba:itemname_cleaningstation", Kind = 0, Price = 4000f, PerDay = 6f, Tint = new Color(0.95f, 0.75f, 0.30f), IconFile = "BP_Scrubber.png" },
            new ItemDef { Name = IExtractor, Source = "ba:itemname_cleaningstation", Kind = 0, Price = 2500f, PerDay = 4f, Tint = new Color(0.55f, 0.85f, 0.55f), IconFile = "BP_Extractor.png" },
            new ItemDef { Name = IWindowKit, Source = "ba:itemname_smallcabinet", Kind = 0, Price = 600f, PerDay = 1f, Tint = new Color(0.70f, 0.85f, 1f), IconFile = "BP_Window.png" },
            // laundromat
            new ItemDef { Name = IWasher8, Source = "ba:itemname_counterdishwashermodern", Kind = 0, Price = 1200f, Tint = White, Shop = 1, IconFile = "BP_Washer.png" },
            new ItemDef { Name = IWasher12, Source = "ba:itemname_counterdishwashermodern", Kind = 0, Price = 2400f, Tint = Steel, Shop = 1, IconFile = "BP_Washer.png" },
            new ItemDef { Name = IWasher27, Source = "ba:itemname_counterdishwashermodern", Kind = 0, Price = 6500f, Tint = new Color(0.62f, 0.66f, 0.72f), Shop = 1, IconFile = "BP_WasherBig.png" },
            new ItemDef { Name = IDryer15, Source = "ba:itemname_counterdishwashermodern", Kind = 0, Price = 1800f, Tint = new Color(0.90f, 0.92f, 0.96f), Shop = 1, IconFile = "BP_Dryer.png" },
            new ItemDef { Name = IDryer30, Source = "ba:itemname_counterdishwashermodern", Kind = 0, Price = 4200f, Tint = new Color(0.70f, 0.74f, 0.80f), Shop = 1, IconFile = "BP_Dryer.png" },
            new ItemDef { Name = IFoldTable, Source = "ba:itemname_consoletable", Kind = 0, Price = 400f, Tint = new Color(0.92f, 0.92f, 0.90f), Shop = 1, IconFile = "BP_Fold.png" },
            new ItemDef { Name = IChanger, Source = "ba:itemname_smallcabinet", Kind = 0, Price = 900f, Tint = new Color(0.40f, 0.62f, 0.45f), Shop = 1, IconFile = "BP_Changer.png" },
            // the services the businesses sell (BizMan's prices and reports)
            new ItemDef { Name = SItSupport, Source = "ba:itemname_hourlyprogrammerfee", Kind = 3, Price = 55f, IconFile = "BP_ItSupport.png" },
            new ItemDef { Name = SSoftDev, Source = "ba:itemname_hourlyprogrammerfee", Kind = 3, Price = 85f, IconFile = "BP_Software.png" },
            new ItemDef { Name = SCloud, Source = "ba:itemname_hourlyprogrammerfee", Kind = 3, Price = 110f, IconFile = "BP_Cloud.png" },
            new ItemDef { Name = SClean, Source = "ba:itemname_hourlyprogrammerfee", Kind = 3, Price = 35f, IconFile = "BP_Clean.png" },
            new ItemDef { Name = SWash, Source = "ba:itemname_hourlyprogrammerfee", Kind = 3, Price = 4f, IconFile = "BP_Washer.png" },
            new ItemDef { Name = SDry, Source = "ba:itemname_hourlyprogrammerfee", Kind = 3, Price = 3f, IconFile = "BP_Dryer.png" },
            new ItemDef { Name = SFold, Source = "ba:itemname_hourlyprogrammerfee", Kind = 3, Price = 18f, IconFile = "BP_Fold.png" },
        };

        public static readonly TypeDef[] Types =
        {
            new TypeDef { Name = ItSupport, Source = "ba:businesstype_webdevelopmentagency", Building = "ba:buildingtype_office", Product = SItSupport, Module = "ModuleItFirm",
                          Skills = new[] { SkItSupport }, Simulator = () => ScriptableObject.CreateInstance<ItSim>(), Colour = new Color(0.25f, 0.55f, 0.95f), IconFile = "BP_ItSupport.png" },
            new TypeDef { Name = SoftDev, Source = "ba:businesstype_webdevelopmentagency", Building = "ba:buildingtype_office", Product = SSoftDev, Module = "ModuleItFirm",
                          Skills = new[] { SkSoftDev }, Simulator = () => ScriptableObject.CreateInstance<ItSim>(), Colour = new Color(0.35f, 0.40f, 0.90f), IconFile = "BP_Software.png" },
            new TypeDef { Name = Cloud, Source = "ba:businesstype_webdevelopmentagency", Building = "ba:buildingtype_office", Product = SCloud, Module = "ModuleItFirm",
                          Skills = new[] { SkCloud }, Simulator = () => ScriptableObject.CreateInstance<ItSim>(), Colour = new Color(0.20f, 0.70f, 0.90f), IconFile = "BP_Cloud.png" },
            new TypeDef { Name = Cleaning, Source = "ba:businesstype_webdevelopmentagency", Building = "ba:buildingtype_office", Product = SClean, Module = "ModuleCleaning",
                          Skills = new[] { SkCleaning, SkCleanLead }, Simulator = () => ScriptableObject.CreateInstance<CleaningSim>(), Colour = new Color(0.30f, 0.75f, 0.85f), IconFile = "BP_Clean.png" },
            new TypeDef { Name = Laundromat, Source = "ba:businesstype_hairdresser", Building = "ba:buildingtype_retail", Product = SWash, Module = "ModuleLaundromat",
                          Skills = new[] { SkService }, Simulator = () => ScriptableObject.CreateInstance<LaundromatSim>(), Colour = new Color(0.45f, 0.70f, 0.95f), IconFile = "BP_Washer.png" },
        };

        /// <summary>The jobs: id, the game's job they are modelled on (icon, colours), its wage times this, the second skill.</summary>
        static readonly (string id, string source, float wageFactor, string second)[] Jobs =
        {
            (SkItSupport, "ba:skill_programmer", 0.90f, "ba:skill_customerservice"),
            (SkSoftDev, "ba:skill_programmer", 1.00f, "ba:skill_negotiation"),
            (SkCloud, "ba:skill_programmer", 1.15f, "ba:skill_negotiation"),
            (SkCleanLead, "ba:skill_logisticsmanager", 0.80f, "ba:skill_cleaning"),
        };

        public static TypeDef? TypeOf(string? businessType) { foreach (var t in Types) if (t.Name == businessType) return t; return null; }
        public static ItemDef? ItemOf(string? name) { foreach (var i in Items) if (i.Name == name) return i; return null; }

        static readonly Dictionary<string, Item> _items = new Dictionary<string, Item>();
        static readonly Dictionary<string, SkillData> _skills = new Dictionary<string, SkillData>();
        static readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, BusinessType> _types = new Dictionary<string, BusinessType>();
        static readonly Dictionary<string, float> _retry = new Dictionary<string, float>();
        static GameObject? _holder;
        static float _next;
        static bool _logged, _broken;
        static object? _city;

        /// <summary>Main thread, every frame from the start (MPCanvasUI.Update); cheap once done.</summary>
        internal static void Ensure()
        {
            if (_broken || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 2f;
            try
            {
                if (BusinessTypeHelper.GetData("ba:businesstype_webdevelopmentagency") == null) return;   // the catalogues are not loaded yet
                EnsureSkills();
                EnsureItems();
                EnsureTypes();
                if (!_logged && _skills.Count == Jobs.Length && _types.Count == Types.Length)
                {
                    _logged = true;
                    int models = 0;
                    foreach (var d in Items)
                        if (d.Kind != 3 && PrefabHelper.PrefabCache.TryGetValue("Prefabs/" + d.Name.Substring(d.Name.IndexOf('_') + 1) + ".prefab", out var m) && m != null) models++;
                    Plugin.Logger.LogInfo($"[BizPack] {_types.Count} business types, {_items.Count} items ({models} models, {_icons.Count(k => !k.Key.StartsWith("file:"))} icons), {_skills.Count} jobs (BUSINESS-PACK-1).");
                }
                CityHooks();
                Help();
            }
            catch (Exception ex) { _broken = true; Plugin.Logger.LogWarning($"[BizPack] {ex}"); }
        }

        static void EnsureSkills()
        {
            var dict = SkillHelper.Skills;
            if (dict == null) return;
            foreach (var (id, source, factor, second) in Jobs)
            {
                if (dict.ContainsKey(id)) continue;
                if (!_skills.TryGetValue(id, out var k) || k == null)
                {
                    var src = SkillHelper.GetData(source);
                    if (src == null) continue;
                    k = UnityEngine.Object.Instantiate(src);
                    k.name = id; k.skillName = id; k.baseHourlyWage = src.baseHourlyWage * factor;
                    if (SkillHelper.GetData(second) != null) k.secondarySkill = second;
                    k.hideFlags = HideFlags.HideAndDontSave;
                    _skills[id] = k;
                }
                dict[id] = k;
            }
        }

        static void EnsureTypes()
        {
            var names = new HashSet<string>(BusinessTypeHelper.BusinessTypeNames);
            foreach (var d in Types)
            {
                if (names.Contains(d.Name)) continue;
                if (!_types.TryGetValue(d.Name, out var t) || t == null)
                {
                    var src = BusinessTypeHelper.GetData(d.Source);
                    if (src == null) continue;
                    t = UnityEngine.Object.Instantiate(src);
                    t.name = d.Name; t.businessTypeName = d.Name; t.suitableBuildingType = d.Building;
                    t.employeePrimarySkills = d.Skills;
                    t.businessProducts = new[] { new BusinessProduct { itemName = d.Product, impact = 1f } };
                    t.productSources = new string[0];
                    t.businessRequirements = new List<Buildings.BuildingTypes.Shared.BusinessRequirement.BusinessRequirement>();
                    t.spawnCustomers = false;   // the simulator serves the customers, every hour
                    t.simulator = d.Simulator();
                    t.simulator.hideFlags = HideFlags.HideAndDontSave;
                    t.cityMapFilterColor = d.Colour;
                    var icon = Icon(d.IconFile);
                    if (icon != null) t.icon = icon;
                    var shapes = LogoShapesExtra.ForType(d.Name);   // LOGO-SHAPES-1: its own black-and-white logo shapes
                    if (shapes.Count > 0) t.logoShapes = shapes;
                    t.hideFlags = HideFlags.HideAndDontSave;
                    _types[d.Name] = t;
                }
                BAModAPI.ModdingAPI.RegisterModBusinessType(t);
            }
        }

        static void EnsureItems()
        {
            foreach (var d in Items)
            {
                if (ItemsGetter.IsModItem(d.Name) && ItemsGetter.GetByName(d.Name, true) != null) { Visuals(d); continue; }
                if (_retry.TryGetValue(d.Name, out var at) && Time.unscaledTime < at) continue;
                if (d.Source == AutoStation) d.Source = FindStation() ?? AutoStation;
                var src = d.Source == AutoStation ? null : ItemsGetter.GetByName(d.Source, true);
                if (src == null)
                {
                    if (!_retry.ContainsKey(d.Name)) Plugin.Logger.LogWarning($"[BizPack] the template {d.Source} for {d.Name} is missing - tried again every minute.");
                    _retry[d.Name] = Time.unscaledTime + 60f;
                    continue;
                }
                if (!_items.TryGetValue(d.Name, out var it) || it == null)
                {
                    it = UnityEngine.Object.Instantiate(src);
                    it.name = d.Name; it.itemName = d.Name;
                    it.defaultMarketPrice = d.Price;
                    if (d.Kind == 3) { it.isADemandedProduct = false; it.wholesalePrice = 0f; }
                    if (d.Skills != null) { it.suitableSkills = d.Skills; it.assignable = true; }
                    var icon = Icon(d.IconFile);
                    if (icon == null) { try { icon = src.icon != null ? src.icon : ItemHelper.GetIcon(d.Source); } catch { } }
                    if (icon != null) { it.icon = icon; _icons[d.Name] = icon; }
                    it.hideFlags = HideFlags.HideAndDontSave;
                    _items[d.Name] = it;
                }
                ItemsGetter.RegisterModItem(it);
                Visuals(d);
            }
        }

        static string? FindStation()
        {
            foreach (var it in ItemsGetter.AllItems)
                if (it != null && it.type.HasFlag(ItemType.EmployeeWorkstation) && it.suitableSkills != null
                    && Array.IndexOf(it.suitableSkills, "ba:skill_programmer") >= 0 && !ItemsGetter.IsModItem(it.itemName)) return it.itemName;
            return null;
        }

        static Sprite? Icon(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            if (_icons.TryGetValue("file:" + file, out var have) && have != null) return have;
            try
            {
                string path = System.IO.Path.Combine(MPConfig.ModRootPath, "assets", file);
                if (!System.IO.File.Exists(path)) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                if (!tex.LoadImage(System.IO.File.ReadAllBytes(path), false)) return null;
                var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                sp.hideFlags = HideFlags.HideAndDontSave;
                _icons["file:" + file] = sp;
                return sp;
            }
            catch { return null; }
        }

        /// <summary>The model under the item's name in the game's model cache, and the icon in its icon cache.</summary>
        static void Visuals(ItemDef d)
        {
            string id = d.Name.Substring(d.Name.IndexOf('_') + 1), srcId = d.Source.Substring(d.Source.IndexOf('_') + 1);
            try
            {
                if (_icons.TryGetValue(d.Name, out var sp) && sp != null)
                {
                    if (!ItemHelper.ItemIconsCache.ContainsKey(d.Name)) ItemHelper.ItemIconsCache[d.Name] = sp;
                    var lookups = BigAmbitions.SaveSystem.AddressableResolver.CachedAddressLookups;
                    string key = ("ItemIcons/" + id + ".png").ToLowerInvariant();
                    if (!lookups.ContainsKey(key) && lookups.TryGetValue(("ItemIcons/" + srcId + ".png").ToLowerInvariant(), out var real)) lookups[key] = real;
                }
            }
            catch { }
            if (d.Kind == 3) return;
            try
            {
                var cache = PrefabHelper.PrefabCache;
                string path = "Prefabs/" + id + ".prefab";
                if (cache.TryGetValue(path, out var have) && have != null) return;
                var prefab = PrefabHelper.LoadPrefabAssetByName(srcId);
                if (prefab == null) return;
                if (_holder == null) { _holder = new GameObject("BizPack_Models"); _holder.SetActive(false); UnityEngine.Object.DontDestroyOnLoad(_holder); }
                var go = UnityEngine.Object.Instantiate(prefab, _holder.transform);
                go.name = id;
                var ic = go.GetComponent<ItemController>();
                if (ic != null) ic.itemName = d.Name;
                if (d.Tint != Color.white)
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] == null) continue;
                            var m = new Material(mats[i]);
                            foreach (var prop in new[] { "_BaseColor", "_Color" })
                                if (m.HasProperty(prop)) { var c = m.GetColor(prop); m.SetColor(prop, new Color(c.r * d.Tint.r, c.g * d.Tint.g, c.b * d.Tint.b, c.a)); }
                            mats[i] = m;
                        }
                        r.sharedMaterials = mats;
                    }
                if (d.Lights && go.GetComponent<GoingOnline.RackLights>() == null) go.AddComponent<GoingOnline.RackLights>();
                cache[path] = go;
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[BizPack] model for {d.Name}: {ex.Message}"); }
        }

        // ── the city: agencies and stores ───────────────────────────────────────────────────────────────────────────

        static float _hooksNext;
        static string _hooksKey = "";
        static int _noApplianceRuns;

        /// <summary>GO-HOOKS-2 (as Going Online's GameTypes.CityHooks): every 20 s, adding only what is missing - after a
        /// reconnect the agencies' settings were fresh copies the single first run had missed, and the appliance stores were
        /// not known yet (the furniture store got the washers). The appliance stores are all of them now, by name.</summary>
        static void CityHooks()
        {
            var city = InstanceBehavior<CityManager>.Instance;
            if (city == null || SaveGameManager.Current == null) { _city = null; return; }
            bool first = !ReferenceEquals(city, _city);
            if (first) _noApplianceRuns = 0;
            if (!first && Time.unscaledTime < _hooksNext) return;
            _hooksNext = Time.unscaledTime + 20f;
            var cbcs = city.cityBuildingControllers;
            if (cbcs == null || cbcs.Length == 0) return;
            _city = city;
            var office = Items.Where(i => (i.Kind == 0 || i.Kind == 1) && i.Shop == 0).Select(i => i.Name).ToList();
            var appliances = Items.Where(i => i.Kind == 0 && i.Shop == 1).Select(i => i.Name).ToList();
            var furniture = new List<BuildingRegistration>(); var applianceStores = new List<BuildingRegistration>();
            int agencies = 0, offering = 0;
            foreach (var cbc in cbcs)
            {
                // the city's appliance stores are ordinary shops of the game (no special service)
                if (cbc?.buildingRegistration != null && cbc.buildingRegistration.businessTypeName == "ba:businesstype_appliancestore" && !cbc.buildingRegistration.RentedByPlayer)
                    applianceStores.Add(cbc.buildingRegistration);
                var settings = cbc?.building?.SpecialService?.settings;
                if (settings == null) continue;
                if (settings is Buildings.RecruitmentAgencySettings ra)
                {
                    if (OfferJobs(ra)) agencies++;
                    if (System.Array.IndexOf(ra.availableEmployeeSkills ?? new string[0], SkItSupport) >= 0) offering++;
                    continue;
                }
                var reg = cbc!.buildingRegistration;
                if (reg == null) continue;
                if (settings is FurnitureStoreSettings) furniture.Add(reg);
            }
            // the office furniture store with the computers (Going Online's equipment is sold there too)
            var officeStores = furniture.Where(r => Sells(r, GoingOnline.GameTypes.ICmts30) || Sells(r, "ba:itemname_desktopcomputer")).ToList();
            if (officeStores.Count == 0) officeStores = furniture.Take(1).ToList();
            applianceStores = applianceStores.Distinct().OrderBy(r => r.BusinessName ?? "").ThenBy(r => GoingOnline.City.KeyOf(r.Address)).ToList();
            if (applianceStores.Count == 0 && ++_noApplianceRuns >= 3)   // none known after a minute: a city without them
            {
                applianceStores = furniture.Where(r => !officeStores.Contains(r)).Take(1).ToList();
                if (applianceStores.Count == 0) applianceStores = officeStores;
            }
            foreach (var r in officeStores) Offer(r, office);
            foreach (var r in applianceStores) Offer(r, appliances);
            string key = offering + "|" + Names(officeStores) + "|" + Names(applianceStores);
            if (!first && agencies == 0 && key == _hooksKey) return;
            _hooksKey = key;
            Plugin.Logger.LogInfo($"[BizPack] in the city: {offering} recruitment agencies offer the jobs ({agencies} added now); servers and cleaning gear at {Names(officeStores)}; washers and dryers at {Names(applianceStores)} (GO-HOOKS-2).");
        }

        static void Add(List<string> list, string x) { if (!list.Contains(x)) list.Add(x); }

        /// <summary>GO-HIRE-1: the agencies with the office jobs (programmers) offer the pack's jobs too. True when something
        /// was added. Also called by the recruitment dialog the moment a business of the pack is picked.</summary>
        internal static bool OfferJobs(Buildings.RecruitmentAgencySettings ra)
        {
            var have = new List<string>(ra.availableEmployeeSkills ?? new string[0]);
            int before = have.Count;
            if (have.Contains("ba:skill_programmer")) { Add(have, SkItSupport); Add(have, SkSoftDev); Add(have, SkCloud); Add(have, SkCleanLead); }
            if (have.Count == before) return false;
            ra.availableEmployeeSkills = have.ToArray();
            return true;
        }

        internal static bool IsOurs(string? businessType) => TypeOf(businessType) != null;
        static bool Sells(BuildingRegistration reg, string item) { try { var l = reg.GetListOfItemsForSale(); return l != null && l.Contains(item); } catch { return false; } }

        static void Offer(BuildingRegistration reg, List<string> ours)
        {
            try
            {
                var list = new List<string>(reg.GetListOfItemsForSale() ?? new List<string>());
                foreach (var x in ours) Add(list, x);
                Services.ContractItemsForSaleService.SetItemsForAddress(reg.Address, list);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[BizPack] store: {ex.Message}"); }
        }

        static string Names(List<BuildingRegistration> regs) => string.Join(", ", regs.Select(r =>
        {
            string a = ""; try { a = Streets.AddressHelper.ToFormattedString(r.Address); } catch { }
            return (string.IsNullOrWhiteSpace(r.BusinessName) ? "" : r.BusinessName + " ") + "(" + a + ")";
        }));

        // ── help (F1) ───────────────────────────────────────────────────────────────────────────────────────────────

        const string HelpCategory = "gps_help_bizpack";
        static readonly string[] HelpPages = { "it", "cleaning", "laundromat" };

        static void Help()
        {
            try
            {
                var hs = InstanceBehavior<HelpSystem>.Instance;
                if (hs == null || !hs._isInitialized || hs._categories == null) return;
                if (hs._categories.Exists(c => c != null && c.CategoryLocalizorKey == HelpCategory)) return;
                var entry = new UnityEngine.UI.Extensions.HelpSystem.HelpStructureGroupEntry { CategoryLocalizorKey = HelpCategory, Pages = new List<UnityEngine.UI.Extensions.HelpSystem.HelpStructurePageEntry>() };
                foreach (var p in HelpPages)
                    entry.Pages.Add(new UnityEngine.UI.Extensions.HelpSystem.HelpStructurePageEntry { Slug = "gps-" + p, PageLocalizorKeyPrefix = "gps_help_" + p });
                hs._categories.Add(entry);
                hs.LoadCategories();
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[BizPack] help: {ex.Message}"); }
        }

        // ── shared by the simulators ────────────────────────────────────────────────────────────────────────────────

        /// <summary>The employees working this hour at a workplace of the business with one of the given jobs, with the
        /// skill value of that job and their satisfaction.</summary>
        internal static List<(Entities.EmployeeInstance emp, float skill)> OnShift(BuildingRegistration reg, string[] jobs, int hour)
        {
            var list = new List<(Entities.EmployeeInstance, float)>();
            if (reg.scheduleDays == null) return list;
            var today = TimeHelper.GetDayOfWeek();
            var seen = new HashSet<string>();
            foreach (var day in reg.scheduleDays)
            {
                if (day == null || day.day != today || day.workShifts == null) continue;
                foreach (var ws in day.workShifts)
                {
                    if (ws == null || string.IsNullOrEmpty(ws.employeeId) || ws.startingHour > hour || ws.endingHour <= hour || !seen.Add(ws.employeeId)) continue;
                    var emp = EmployeeHelper.GetEmployeeById(ws.employeeId, false);
                    if (emp == null || emp.isAbsent || emp.characterData?.skills == null) continue;
                    foreach (var sk in emp.characterData.skills)
                        if (sk != null && Array.IndexOf(jobs, sk.name) >= 0) { list.Add((emp, sk.value)); break; }
                }
            }
            return list;
        }

        internal static int Placed(BuildingRegistration reg, string item)
        {
            int n = 0;
            if (reg.itemInstances != null) foreach (var i in reg.itemInstances.Values) if (i != null && i.itemName == item) n++;
            return n;
        }

        /// <summary>A completed sale of the business, as the game's own simulators record one (paid at midnight).</summary>
        internal static void Sell(BuildingRegistration reg, string item, float price, float serviceSkill, int count = 1)
        {
            if (count <= 0 || reg.unprocessedCompletedOrders == null) return;
            float clean = 100f;
            try { clean = reg.GetCleanliness(); } catch { }
            for (int i = 0; i < count; i++)
            {
                var o = new Order { completed = true, customerServiceSkill = serviceSkill, cleanliness = clean };
                o.entries.Add(new Entities.OrderEntry { itemName = item, price = price, available = true, priceAccceptable = true, paid = true });
                try { o.customerDemandScore = OrderHelper.CalculateOrderDemandScore(o, reg); } catch { }
                reg.unprocessedCompletedOrders.Add(o);
            }
        }

        /// <summary>How many of the people who would come accept the business's price (the default price draws all).</summary>
        internal static float PriceDraw(BuildingRegistration reg, string item)
        {
            float price = ItemHelper.GetPrice(item, reg), def = ItemOf(item)?.Price ?? price;
            if (price <= 0.01f || def <= 0.01f) return 1f;
            return Mathf.Clamp(Mathf.Pow(def / price, 1.6f), 0f, 1.4f);
        }

        /// <summary>A daily cost of the business (power for servers, water and detergent ...), booked once a day at 23 o'clock
        /// with its address, so BizMan's income statement shows it.</summary>
        internal static void DailyCost(BuildingRegistration reg, int hour, float amount, string transaction)
        {
            if (hour != 23 || amount < 0.005f) return;
            try
            {
                var info = new TransactionInfo(transaction, new Dictionary<string, string> { { "businessName", reg.BusinessName ?? "" } });
                GameManager.ChangeMoneySafe(-Mathf.Round(amount * 100f) / 100f, info, null, reg.Address, true, false);
            }
            catch { }
        }

        /// <summary>A whole number from a fraction, rounded at random (2.3 is 2 seven times in ten, else 3).</summary>
        internal static int Draw(float x) => Mathf.FloorToInt(x) + (UnityEngine.Random.value < x - Mathf.Floor(x) ? 1 : 0);

        /// <summary>Hour of the day as a share of the busiest time (office work, a shop's people).</summary>
        internal static float DayCurve(int hour, bool office) => office
            ? (hour >= 9 && hour < 17 ? 1f : hour >= 7 && hour < 20 ? 0.5f : 0.1f)
            : (hour >= 17 && hour < 21 ? 1f : hour >= 9 && hour < 17 ? 0.7f : hour >= 7 && hour < 23 ? 0.35f : 0.05f);

        internal static bool On(string module) => ServerRules.Module(module);

        public const string TServers = "gps:transaction_serverpower", TSupplies = "gps:transaction_cleaningsupplies", TLaundry = "gps:transaction_laundrysupplies";

        /// <summary>A business's running total of the day (the simulator is one per business type, shared by all of them).</summary>
        static readonly Dictionary<string, float> _day = new Dictionary<string, float>();
        internal static float AddToDay(BuildingRegistration reg, float amount)
        {
            string k = GameStateReader.AddressKey(reg);
            _day.TryGetValue(k, out var v);
            v += amount;
            _day[k] = v;
            return v;
        }
        internal static void ClearDay(BuildingRegistration reg) => _day.Remove(GameStateReader.AddressKey(reg));

        /// <summary>The business's own service among its products, so BizMan lists it with its price.</summary>
        internal static void EnsureProduct(BuildingRegistration reg, string item)
        {
            try
            {
                if (reg.cachedAvailableProducts == null) reg.cachedAvailableProducts = new List<string>();
                if (!reg.cachedAvailableProducts.Contains(item)) reg.cachedAvailableProducts.Add(item);
            }
            catch { }
        }
    }

    /// <summary>IT firm: every hour each IT employee on shift at an IT workplace bills an hour when a client needs it - more
    /// with skill and good spirits, fewer at a high price and outside office hours. The servers must keep up: IT support
    /// needs a blade server per four workplaces, software development a rack server per four, cloud services a rack per four
    /// and a mainframe per twelve; short of them the staff work at a third. Servers cost power every day.</summary>
    public class ItSim : BusinessSimulator
    {
        public override void SimulateCurrentHour()
        {
            var reg = buildingRegistration;
            if (reg == null || !BizPack.On("ModuleItFirm")) return;
            var def = BizPack.TypeOf(reg.businessTypeName);
            if (def == null) return;
            string job = def.Skills[0];
            int desks = BizPack.Placed(reg, BizPack.IItDesk), blades = BizPack.Placed(reg, BizPack.IBlade), racks = BizPack.Placed(reg, BizPack.IRack), frames = BizPack.Placed(reg, BizPack.IMainframe);
            BizPack.EnsureProduct(reg, def.Product);
            float power = blades * 40f + racks * 80f + frames * 160f;
            BizPack.DailyCost(reg, currentHour, power, BizPack.TServers);
            var staff = BizPack.OnShift(reg, new[] { job }, currentHour);
            if (staff.Count == 0) return;
            int seats = Mathf.Max(1, Mathf.Max(desks, staff.Count));
            bool servers = def.Name == BizPack.ItSupport ? blades * 4 >= seats
                         : def.Name == BizPack.SoftDev ? racks * 4 >= seats
                         : racks * 4 >= seats && frames * 12 >= seats;
            float draw = BizPack.PriceDraw(reg, def.Product) * BizPack.DayCurve(currentHour, true) * (servers ? 1f : 0.33f);
            float price = ItemHelper.GetPrice(def.Product, reg);
            foreach (var (emp, skill) in staff)
            {
                float work = (0.55f + 0.45f * skill / 100f) * (0.6f + 0.4f * emp.satisfaction / 100f) * draw;
                if (BizPack.Draw(Mathf.Clamp01(work)) > 0) BizPack.Sell(reg, def.Product, price, skill * emp.satisfaction / 100f);
            }
        }

        public override void OnTimeMachineEnd(BuildingRegistration registration) { }
    }

    /// <summary>Cleaning company: the cleaners and crew leads on shift clean - about 120 m² an hour each, a fifth more with a
    /// floor scrubber, a tenth with a carpet extractor, a twentieth with a window kit (per piece in the office, at most one
    /// per person), a tenth more while a crew lead works. First the player's own other businesses (their dirt goes away,
    /// biggest dirt first), then clients of the city, who pay by the hour - the more, the friendlier the price and the
    /// more of the evening and the night. Cleaning supplies cost a little per hour.</summary>
    public class CleaningSim : BusinessSimulator
    {
        public override void SimulateCurrentHour()
        {
            var reg = buildingRegistration;
            if (reg == null || !BizPack.On("ModuleCleaning")) return;
            BizPack.EnsureProduct(reg, BizPack.SClean);
            var staff = BizPack.OnShift(reg, new[] { BizPack.SkCleaning, BizPack.SkCleanLead }, currentHour);
            float supplies = 0f;
            if (staff.Count > 0)
            {
                int n = staff.Count;
                float gear = 1f + 0.20f * Mathf.Min(1f, BizPack.Placed(reg, BizPack.IScrubber) / (float)n)
                                + 0.10f * Mathf.Min(1f, BizPack.Placed(reg, BizPack.IExtractor) / (float)n)
                                + 0.05f * Mathf.Min(1f, BizPack.Placed(reg, BizPack.IWindowKit) / (float)n);
                bool lead = staff.Exists(x => x.emp.characterData.skills.Exists(k => k.name == BizPack.SkCleanLead));
                float sqm = 0f;
                foreach (var (emp, skill) in staff) sqm += 120f * (0.6f + 0.4f * skill / 100f) * (0.7f + 0.3f * emp.satisfaction / 100f) * gear * (lead ? 1.1f : 1f);
                sqm = CleanOwn(reg, sqm);
                float hours = sqm / 120f;
                float clients = BizPack.PriceDraw(reg, BizPack.SClean) * (currentHour >= 18 || currentHour < 6 ? 1f : 0.35f);
                int billed = BizPack.Draw(hours * clients);
                float avgSkill = 0f; foreach (var x in staff) avgSkill += x.skill; avgSkill /= n;
                BizPack.Sell(reg, BizPack.SClean, ItemHelper.GetPrice(BizPack.SClean, reg), avgSkill, billed);
                supplies = n * 1.5f;
            }
            float day = BizPack.AddToDay(reg, supplies);
            if (currentHour == 23) { BizPack.DailyCost(reg, currentHour, day, BizPack.TSupplies); BizPack.ClearDay(reg); }
        }

        /// <summary>Cleans the player's other businesses with the square metres the crews have this hour; returns what is
        /// left for paying clients.</summary>
        static float CleanOwn(BuildingRegistration company, float sqm)
        {
            var game = SaveGameManager.Current;
            if (game?.BuildingRegistrations == null || sqm <= 0f) return sqm;
            var own = new List<(BuildingRegistration reg, float dirt)>();
            foreach (var r in game.BuildingRegistrations)
            {
                if (r == null || r == company || !r.RentedByPlayer || r.dirtSpots == null || r.dirtSpots.Count == 0) continue;
                if (string.IsNullOrEmpty(r.businessTypeName) || r.businessTypeName == "ba:businesstype_empty") continue;
                float d = 0f; foreach (var s in r.dirtSpots) if (s != null) d += s.dirtiness;
                if (d > r.dirtSpots.Count * 2f) own.Add((r, d));
            }
            own.Sort((a, b) => b.dirt.CompareTo(a.dirt));
            foreach (var (r, _) in own)
            {
                if (sqm <= 1f) break;
                float size = 100f;
                try { size = Mathf.Max(30f, r.BuildingCached.totalSqm); } catch { }
                float share = Mathf.Clamp01(sqm / size);
                for (int i = 0; i < r.dirtSpots.Count; i++)
                {
                    var s = r.dirtSpots[i];
                    if (s == null || s.dirtiness <= 0f) continue;
                    s.dirtiness = Mathf.Max(0f, s.dirtiness - 100f * share);
                    try { if (BuildingManager.IsInsideBuilding && InstanceBehavior<BuildingManager>.Instance.buildingRegistration == r) InstanceBehavior<BuildingManager>.Instance.UpdateDirtinessInSpecificSpot(i); } catch { }
                }
                sqm -= size * share;
            }
            return sqm;
        }

        public override void OnTimeMachineEnd(BuildingRegistration registration) { }
    }

    /// <summary>Laundromat: people of the neighbourhood come with their laundry - more where many people pass by, more in the
    /// evening, fewer at high prices. Each washer takes a load an hour (a 12 kg one a load and a half, the 27 kg giant three),
    /// most loads go into a dryer afterwards (15 kg: one an hour, 30 kg: two); with an attendant on shift some leave their
    /// laundry for wash and fold (folding tables help). A bill changer brings a few more. Water, power and detergent cost
    /// about 0.80 $ a load.</summary>
    public class LaundromatSim : BusinessSimulator
    {
        public override void SimulateCurrentHour()
        {
            var reg = buildingRegistration;
            if (reg == null || !BizPack.On("ModuleLaundromat")) return;
            foreach (var it in new[] { BizPack.SWash, BizPack.SDry, BizPack.SFold }) BizPack.EnsureProduct(reg, it);
            float costs = 0f;
            float washers = BizPack.Placed(reg, BizPack.IWasher8) + 1.5f * BizPack.Placed(reg, BizPack.IWasher12) + 3f * BizPack.Placed(reg, BizPack.IWasher27);
            float dryers = BizPack.Placed(reg, BizPack.IDryer15) + 2f * BizPack.Placed(reg, BizPack.IDryer30);
            if (washers > 0f)
            {
                float traffic = 50f;
                try { traffic = Mathf.Clamp(reg.BuildingCached.trafficIndex, 1f, 100f); } catch { }
                float people = (2f + traffic / 12f) * BizPack.DayCurve(currentHour, false) * (BizPack.Placed(reg, BizPack.IChanger) > 0 ? 1.1f : 1f);
                float wantWash = people * BizPack.PriceDraw(reg, BizPack.SWash);
                int washes = BizPack.Draw(Mathf.Min(wantWash, washers));
                int dries = Mathf.Min(BizPack.Draw(washes * 0.8f * BizPack.PriceDraw(reg, BizPack.SDry)), Mathf.FloorToInt(dryers));
                var staff = BizPack.OnShift(reg, new[] { BizPack.SkService }, currentHour);
                int folds = 0;
                if (staff.Count > 0)
                {
                    float cap = staff.Count * (1.2f + 0.5f * Mathf.Min(BizPack.Placed(reg, BizPack.IFoldTable), staff.Count));
                    folds = BizPack.Draw(Mathf.Min(cap, people * 0.25f * BizPack.PriceDraw(reg, BizPack.SFold)));
                }
                float service = staff.Count > 0 ? staff.Average(x => x.skill) : 60f;
                BizPack.Sell(reg, BizPack.SWash, ItemHelper.GetPrice(BizPack.SWash, reg), service, washes);
                BizPack.Sell(reg, BizPack.SDry, ItemHelper.GetPrice(BizPack.SDry, reg), service, dries);
                BizPack.Sell(reg, BizPack.SFold, ItemHelper.GetPrice(BizPack.SFold, reg), service, folds);
                costs = 0.8f * (washes + folds * 2) + 0.3f * dries;
            }
            float day = BizPack.AddToDay(reg, costs);
            if (currentHour == 23) { BizPack.DailyCost(reg, currentHour, day, BizPack.TLaundry); BizPack.ClearDay(reg); }
        }

        public override void OnTimeMachineEnd(BuildingRegistration registration) { }
    }
}
