using System.Reflection;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Entities;
using Extensions;
using HarmonyLib;
using Localizor.LanguageChangeEvent;
using UnityEngine;
using WhDrivers = UI.Smartphone.Apps.BizMan.Warehouse.Drivers;
using WhStation = UI.Smartphone.Apps.BizMan.Warehouse.DriverStation;

namespace GoingOnline
{
    /// <summary>GO-PATCHES-1 (user 2026-11-08: "Going Online soll jetzt auf aktuellen Stand gebracht werden als Einzel-Mod", then
    /// "Mit Harmony"): the cable provider in the game's own pages, for both builds. Until 0.8.0 these patches were Going Public
    /// Server's own (src/GoingOnlineGameUi.cs) and the stand-alone mod had none; now both compile them from here - the server
    /// mod patches them with its other classes, the stand-alone mod with the Harmony in its Dependencies folder
    /// (src/Patcher.cs, with the Proton fix WINE-JUMPS-1). They reach the game's private members through Harmony's injected
    /// fields (____name) and <see cref="Get{T}"/>, so they compile without the publicizer the server mod uses.
    ///
    /// The history (user 2026-11-07, five reports from the first test with a provider of their own; since Going Online 0.7 the
    /// provider is one business in an office, GO-OFFICE-1 - a headend of 0.6 still runs in a warehouse). The game showed it as
    /// a warehouse everywhere:
    /// - GO-BIZMAN-1 "der Kabelanbieter steht als Lagerhaus, was aber ein separater Eintrag sein könnte, mit Kundenanzahl":
    ///   BizMan lists it with the businesses (type and connections, the provider's satisfaction), not with the warehouses;
    ///   its page opens on Insight and gets the tabs Deliveries, Fleet (the game's Drivers tab) and Inventory.
    /// - GO-FLEET-1 "kann die Techniker nicht zuweisen, da immer noch als Lieferfahrer drin steht; genauso müsste drin
    ///   stehen 2 Leasingfahrzeuge": the Drivers tab shows the provider's fleet - its own vehicles at the headend and the
    ///   leased ones - with who drives which this hour, and each vehicle can get a person of its own.
    /// - GO-DELIVERY-1 "kann Kabelanbieter-Unternehmen nicht auswählen" (a wholesaler's delivery contract): the headend
    ///   takes the wholesalers' deliveries of the devices onto its storage shelves (pallet shelves too).
    /// - GO-HIRE-1 (City Workforce Inc.: "bietet keine Kandidaten mit diesem Fertigkeitstyp an"): picking a business of
    ///   ours in the recruitment dialog makes sure the agency offers our jobs, and logs once what it found. GO-WORK-1
    ///   ("anhand der Arbeitsplätze kann man die Leute einstellen"): the jobs are those whose workplaces stand in that
    ///   building; without one the dialog says what to set up. The map's filter rows (GO-MAP-1) are in GameUi.cs.</summary>
    internal static class GamePatches
    {
        /// <summary>Set by Going Public Server: its business pack's types and jobs ride along on the same patches (the
        /// furniture store's phone order, the recruitment dialog).</summary>
        internal static Func<string, bool>? AlsoOurType;
        internal static Action<Buildings.RecruitmentAgencySettings>? AlsoOfferJobs;

        /// <summary>The patches are in: texts may send the player to BizMan's Deliveries tab.</summary>
        internal static bool Active;

        static readonly HashSet<string> _once = new HashSet<string>(StringComparer.Ordinal);

        static void Once(string key, string message) { if (_once.Add(key)) Go.Log(message); }
        static bool Ours(BuildingRegistration? r) { try { return r != null && GameTypes.IsOurs(r.businessTypeName); } catch { return false; } }
        static bool Headend(BuildingRegistration? r) { try { return GameUi.IsHeadend(r); } catch { return false; } }
        static bool AlsoOurs(string type) { try { return AlsoOurType != null && AlsoOurType(type); } catch { return false; } }

        static readonly Dictionary<string, FieldInfo?> _fields = new Dictionary<string, FieldInfo?>(StringComparer.Ordinal);

        /// <summary>A private field of a game object (the fleet cards' parts); null when the game has renamed it.</summary>
        internal static T? Get<T>(object? o, string name) where T : class
        {
            if (o == null) return null;
            string key = o.GetType().FullName + "|" + name;
            if (!_fields.TryGetValue(key, out var fi))
            {
                fi = AccessTools.Field(o.GetType(), name);
                _fields[key] = fi;
                if (fi == null) Once("field|" + key, $"the game has no field {o.GetType().Name}.{name} any more - that part of the page stays the game's (GO-PATCHES-1).");
            }
            try { return fi?.GetValue(o) as T; } catch { return null; }
        }

        // ── GO-VAN-2: the service van at the car dealers ─────────────────────────────────────────────────────────────

        /// <summary>GO-VAN-2 (user 2026-11-09: "Echtes Fahrzeug - beim Autohändler kaufen ... mit Firmenlogo"): a dealer's
        /// contract (in the shop or by phone) lists the vehicles of its showroom; a dealer that sells vans lists Going Online's
        /// service van as well - without a showroom car the game sells and delivers it as its own (ContractVehicleForSale).</summary>
        [HarmonyPatch(typeof(Buildings.VehicleContractSettings), "SetListOfVehiclesForSale")]
        public static class Patch_VehicleContract_List_OurVan
        {
            /// <summary>GO-LIVERY-1: the list is read from the dealer's layout, which may hold the showroom piece - its
            /// item must be registered before the game looks it up.</summary>
            static void Prefix()
            {
                try { Van.EnsureShowcaseNow(); } catch { }
            }

            static void Postfix(Buildings.VehicleContractSettings __instance)
            {
                try { Van.OfferAt(__instance); }
                catch (Exception ex) { Once("van-offer", $"service van at a dealer: {ex.Message} (GO-VAN-2)."); }
            }
        }

        /// <summary>GO-LIVERY-1 (the proposal the user said yes to on 2026-11-10: "Ein Servicewagen steht in der Ausstellung
        /// neben den Transportern, schon in deiner Beklebung. Ein Klick öffnet den Kauf des Spiels mit Farbwahl"): a dealer's
        /// showroom is built from its layout (BusinessLayoutSetHelper keeps each one once loaded); the layouts of the three
        /// dealers that sell vans get the service van's showroom piece (Van.Showroom). Clicking it opens the game's own
        /// purchase with the colour choice; the colour shows on the piece at once (VanSkin).</summary>
        [HarmonyPatch(typeof(BusinessLayoutSets.BusinessLayoutSetHelper), nameof(BusinessLayoutSets.BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet))]
        public static class Patch_LayoutSet_Showroom
        {
            static void Postfix(global::BusinessLayoutSet __result)
            {
                try { Van.Showroom(__result); }
                catch (Exception ex) { Once("van-showroom", $"service van in a showroom: {ex.Message} (GO-LIVERY-1)."); }
            }
        }

        /// <summary>GO-LIVERY-1: the interior is built from the layout now - the piece's item and model must be there (else
        /// the layout gets the game's own car back for this visit).</summary>
        [HarmonyPatch(typeof(BuildingManager), nameof(BuildingManager.LoadBusinessLayoutSet))]
        public static class Patch_Interior_Showroom
        {
            static void Prefix(global::BusinessLayoutSet businessLayoutSet)
            {
                try { Van.BeforeInterior(businessLayoutSet); }
                catch (Exception ex) { Once("van-interior", $"service van in a showroom (interior): {ex.Message} (GO-LIVERY-1)."); }
            }
        }

        // ── GO-FURNDELIVERY-1: the furniture store's phone order to an office of ours ──────────────────────────────────

        /// <summary>GO-FURNDELIVERY-1 (user 2026-11-08, a screenshot of the phone order at Factory Supply Depot with the CMTS,
        /// OLT and workplaces in the basket: "kann Adresse Kabel-Guru nicht auswählen"): the store delivers by phone only to the
        /// building types in its FurnitureStoreSettings.allowedDeliveryBuildingTypes - Factory Supply Depot only to warehouses.
        /// Going Online's headend was a warehouse until 0.6; since 0.7 the cable provider is an office (GO-OFFICE-1), and its
        /// CMTS, OLT and workplaces are sold there - as are the business pack's server racks and cleaning gear for the IT firm
        /// and the cleaning company (Going Public Server), offices as well. When a store limits its deliveries, the player's
        /// offices with one of our business types are offered too.</summary>
        [HarmonyPatch(typeof(UI.Dialog.FurnitureDeliveryContractSettings), "GetDeliveryDestinations")]
        public static class Patch_FurnitureDelivery_Destinations_OurOffices
        {
            static void Postfix(ref List<BuildingRegistration> __result)
            {
                try
                {
                    if (__result == null) return;
                    int added = 0;
                    var all = SaveGameManager.Current?.BuildingRegistrations;
                    if (all == null) return;
                    foreach (var reg in all)   // rented or bought: an office the player bought counts as well
                    {
                        if (reg == null || !(reg.RentedByPlayer || reg.BuildingOwnedByPlayer) || __result.Contains(reg)) continue;
                        string type = reg.businessTypeName ?? "";
                        if (!(GameTypes.IsOurs(type) || AlsoOurs(type))) continue;
                        if (reg.GetBuildingType() != "ba:buildingtype_office") continue;
                        __result.Add(reg);
                        added++;
                    }
                    if (added > 0) Once("furndelivery", $"the store's phone order offers {added} office(s) of our business types too (GO-FURNDELIVERY-1).");
                }
                catch (Exception ex) { Once("furndelivery-err", $"phone order addresses: {ex.Message} (GO-FURNDELIVERY-1)."); }
            }
        }

        /// <summary>GO-WORK-1 on the job board (GO-ASSIGN-2): the cable provider's type names all its jobs now, so the hourly job
        /// board candidate (a building with a job board) would come for any of them. For a business of ours it comes only for
        /// the jobs whose workplaces stand there, and not at all without one; the next round of GameTypes.UpdateJobs names
        /// all jobs again.</summary>
        [HarmonyPatch(typeof(UI.Smartphone.Apps.MyEmployees.JobBoardCandidateGenerator), "GenerateJobBoardCandidate")]
        public static class Patch_JobBoard_OurWorkplaces
        {
            static bool Prefix(BuildingRegistration registration, out bool __state)
            {
                __state = false;
                try
                {
                    if (!Ours(registration)) return true;
                    __state = true;
                    return GameTypes.JobsFor(registration).Count > 0;
                }
                catch { return true; }
            }

            static void Postfix(bool __state)
            {
                if (!__state) return;
                try { GameTypes.UpdateJobs(true); } catch { }
            }
        }

        // ── GO-BIZMAN-3: the stock on the "Inventory & prices" tab ─────────────────────────────────────────────────────

        /// <summary>GO-BIZMAN-3 (user 2026-11-08, BizMan "Inventar & Preise" of the cable provider with an empty table:
        /// "Lagerbestand fehlt"): the table lists the building's cachedAvailableProducts - the goods on its sale shelves, and an
        /// office has none - so the devices on the provider's storage shelves showed nowhere (the warehouse Inventory tab went
        /// with GO-BIZMAN-2). While the table loads, the devices join that list (with 0 in stock too) and leave it after, so
        /// nothing else (customers, demand, the save) takes them for goods on sale. Their price field shows "-" and stays
        /// locked: the provider's prices are set in the Going Online app.</summary>
        [HarmonyPatch(typeof(InventoryProductsScrollerController), nameof(InventoryProductsScrollerController.LoadProducts))]
        public static class Patch_InventoryProducts_Load_OurDevices
        {
            static void Prefix(BizManBusiness ____bizManBusiness, out List<string>? __state)
            {
                __state = null;
                try
                {
                    var reg = ____bizManBusiness?.buildingRegistration;
                    if (!Ours(reg) || reg!.cachedAvailableProducts == null) return;
                    var added = new List<string>();
                    foreach (var d in GameTypes.DeviceItems)
                        if (!reg.cachedAvailableProducts.Contains(d)) { reg.cachedAvailableProducts.Add(d); added.Add(d); }
                    __state = added;
                }
                catch { }
            }

            static void Postfix(BizManBusiness ____bizManBusiness, List<string>? __state)
            {
                if (__state == null || __state.Count == 0) return;
                try
                {
                    var reg = ____bizManBusiness?.buildingRegistration;
                    if (reg?.cachedAvailableProducts != null) foreach (var d in __state) reg.cachedAvailableProducts.Remove(d);
                    Once("bizman-stock", $"BizMan: the provider's Inventory & prices lists its {__state.Count} devices with their stock (GO-BIZMAN-3).");
                }
                catch { }
            }
        }

        [HarmonyPatch(typeof(InventoryProductCellView), nameof(InventoryProductCellView.SetData))]
        public static class Patch_InventoryProductCell_OurDevicePrice
        {
            static void Postfix(InventoryProductCellView __instance, InventoryProductCellView.InventoryProductModel data)
            {
                try
                {
                    if (data?.Item == null || !GameTypes.IsDevice(data.Item.itemName)) return;
                    __instance.retailPrice.SetText("-", notify: false);
                    __instance.retailPrice.tmpInputField.interactable = false;
                    __instance.retailPricePlusButton.interactable = false;
                    __instance.retailPriceMinusButton.interactable = false;
                }
                catch { }
            }
        }

        // ── GO-HIRE-1: the recruitment dialog ─────────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(UI.Dialog.RecruitmentSettings), "SelectBusiness")]
        public static class Patch_RecruitmentSettings_SelectBusiness_OurJobs
        {
            static bool Prefix(UI.Dialog.RecruitmentSettings __instance, int businessIndex, List<BuildingRegistration> ____buildingRegistrations,
                               ref string[] ____availableSkills, UI.Elements.Dropdown ___skillsDropdown, out string __state)
            {
                __state = "";
                try
                {
                    var regs = ____buildingRegistrations;
                    if (regs == null || businessIndex < 0 || businessIndex >= regs.Count) return true;
                    var reg = regs[businessIndex];
                    string type = reg?.businessTypeName ?? "";
                    bool go = GameTypes.IsOurs(type), bp = AlsoOurs(type), hq = type == GameTypes.Hq;
                    if (!go && !bp && !hq) return true;
                    if (hq) GameTypes.HqJobsNow();   // GO-HQ-1: network masters for the headquarters
                    var contact = DialogController.current?.contact;
                    var building = contact != null ? Helpers.BuildingHelper.GetBuilding(contact.Address) : null;
                    var ra = building?.SpecialService?.settings as Buildings.RecruitmentAgencySettings;
                    string agency = "?";
                    try { agency = Helpers.BuildingHelper.GetBuildingRegistration(contact!.Address)?.BusinessName ?? "?"; } catch { }
                    int before = ____availableSkills?.Length ?? -1;
                    if (ra != null)
                    {
                        GameTypes.OfferJobs(ra);
                        try { AlsoOfferJobs?.Invoke(ra); } catch { }
                        ____availableSkills = ra.availableEmployeeSkills;   // the dialog read them when it opened
                    }
                    string note = "";
                    if (ra == null) note = "no agency settings found";
                    else if (before >= 0 && before != (ra.availableEmployeeSkills?.Length ?? 0)) note = $"the dialog had {before} jobs of the agency, now {ra.availableEmployeeSkills?.Length ?? 0}";
                    if (go && reg != null)
                    {
                        // GO-WORK-1: the jobs whose workplaces stand in this building - and if there are none for this
                        // agency, the reason instead of the game's "no candidates of this kind"
                        var jobs = GameTypes.JobsFor(reg);
                        note = (note.Length > 0 ? note + "; " : "") + "workplaces for " + (jobs.Count > 0 ? string.Join(", ", jobs) : "no job");
                        var have = ____availableSkills ?? new string[0];
                        bool cleaning = have.Contains("ba:skill_cleaning") && (Buildings.BuildingTypeHelper.GetData(reg)?.NeedsCleaning ?? false);
                        string why = GameTypes.NoJobsNote(reg, have);
                        if (why.Length > 0)
                        {
                            Notify.Warn(why, 10f);
                            if (cleaning) note += "; only the cleaning (no workplace for this agency's jobs)";   // the game offers the cleaning still
                            else
                            {
                                __instance.selectedBusiness = reg;
                                __instance.businessSkills = new List<string>();
                                if (___skillsDropdown != null) { ___skillsDropdown.ResetSelectedOption(); ___skillsDropdown.SetInteractable(false); }
                                __instance.hasSelectedSkill = false;
                                __state = agency + "\u0001" + reg.BusinessName + "\u0001" + type + "\u0001" + note + "; nobody without a workplace for it";
                                return false;
                            }
                        }
                    }
                    __state = agency + "\u0001" + (reg?.BusinessName ?? "") + "\u0001" + type + "\u0001" + note;
                }
                catch (Exception ex) { Go.Warn("recruitment: " + ex.Message); }
                return true;
            }

            static void Postfix(UI.Dialog.RecruitmentSettings __instance, string __state)
            {
                if (string.IsNullOrEmpty(__state)) return;
                try
                {
                    var p = __state.Split('\u0001');
                    var jobs = __instance.businessSkills ?? new List<string>();
                    Once("hire|" + p[0] + "|" + p[2] + "|" + jobs.Count + "|" + p[3],
                         $"recruitment at {p[0]} for '{p[1]}' ({p[2]}): {jobs.Count} job(s) offered ({string.Join(", ", jobs)}){(p[3].Length > 0 ? " - " + p[3] : "")} (GO-HIRE-1).");
                }
                catch { }
            }
        }

        // ── GO-BIZMAN-1: BizMan's list ────────────────────────────────────────────────────────────────────────────────

        /// <summary>The list of businesses takes the game's revenue businesses; ours are in it too.</summary>
        [HarmonyPatch(typeof(UI.Smartphone.Apps.BizMan.BusinessScrollerController), "IsVisibleBusiness")]
        public static class Patch_BusinessScroller_IsVisibleBusiness_Ours
        {
            static void Postfix(BuildingRegistration registration, ref bool __result)
            {
                if (!__result && Ours(registration)) __result = true;
            }
        }

        /// <summary>... and not in the warehouses column (a headend of 0.6 is a warehouse building).</summary>
        [HarmonyPatch(typeof(UI.Smartphone.Apps.BizMan.WarehouseList), "SetUpEntry")]
        public static class Patch_WarehouseList_SetUpEntry_NotOurs
        {
            static bool Prefix(Entities.Warehouse warehouse) => !Ours(warehouse);
        }

        /// <summary>The satisfaction column: the provider's own (the game has no customers of its own there: 0 %).</summary>
        [HarmonyPatch(typeof(BusinessCellView.BusinessModel), MethodType.Constructor, new Type[] { typeof(BuildingRegistration) })]
        public static class Patch_BusinessModel_Ctor_OurSatisfaction
        {
            static void Postfix(BusinessCellView.BusinessModel __instance, BuildingRegistration instance)
            {
                try { int sat = GameUi.RowSatisfaction(instance); if (sat >= 0) __instance.Satisfaction = sat; } catch { }
            }
        }

        /// <summary>The type column: "Cable provider (91 connections)".</summary>
        [HarmonyPatch(typeof(BusinessCellView), nameof(BusinessCellView.SetData))]
        public static class Patch_BusinessCellView_SetData_OurType
        {
            static void Postfix(BusinessCellView __instance, BusinessCellView.BusinessModel data)
            {
                try
                {
                    if (data == null || data.IsRealEstate || string.IsNullOrEmpty(data.BusinessName) || !GameTypes.IsOurs(data.BusinessType)) return;
                    string? label = GameUi.RowType(Helpers.BuildingHelper.GetBuildingRegistration(data.Address));
                    if (label != null && __instance.businessType != null) __instance.businessType.SetValue(label, true);
                }
                catch { }
            }
        }

        // ── GO-BIZMAN-1: the business page's tabs ─────────────────────────────────────────────────────────────────────

        /// <summary>The headend's page: the game's tabs for a business plus Deliveries (the wholesalers' contracts), Fleet
        /// (the game's Drivers tab, renamed) and - in a warehouse - Inventory (what lies on its pallet shelves), in the menu's
        /// own order.</summary>
        [HarmonyPatch(typeof(BizManBusiness), "SetUpTabs")]
        public static class Patch_BizManBusiness_SetUpTabs_OurTabs
        {
            static void Postfix(BizManBusiness __instance, Transform ___menu, List<string> ____tabs)
            {
                try
                {
                    var menu = ___menu;
                    bool ours = Headend(__instance.buildingRegistration) && __instance.buildingRegistration.RentedByPlayer;
                    Relabel(menu != null ? menu.Find("Drivers") : null, "bizman_menu_drivers", ours ? GameUi.FleetTitle : null);
                    if (!ours || menu == null) return;
                    var tabs = ____tabs;
                    if (tabs == null || !tabs.Contains("Schedule")) return;   // the game's business tabs are there
                    // GO-BIZMAN-2 (a player's error report 2026-11-08: InvalidCastException in Warehouse.Inventory.RefreshData on
                    // the provider's page): the game's Inventory tab reads its pallet shelves as Entities.Warehouse. Since 0.7 the
                    // provider runs in an office (GO-OFFICE-1), whose stock the game's own "Inventory & prices" tab shows - the
                    // Inventory tab only for a provider still in a warehouse. The Fleet tab is ours entirely (GO-FLEET-1).
                    bool warehouse = __instance.buildingRegistration is Entities.Warehouse;
                    foreach (var t in new[] { "Deliveries", "Drivers", "Inventory" })
                    {
                        if (t == "Inventory" && !warehouse) continue;
                        if (!tabs.Contains(t) && menu.Find(t) != null) tabs.Add(t);
                    }
                    var order = new Dictionary<string, int>();
                    foreach (Transform item in menu) order[item.name] = item.GetSiblingIndex();
                    tabs.Sort((a, b) => (order.TryGetValue(a, out var x) ? x : 999).CompareTo(order.TryGetValue(b, out var y) ? y : 999));
                    foreach (Transform item in menu) item.gameObject.SetActive(tabs.Contains(item.name));
                    Once("tabs|" + City.KeyOf(__instance.buildingRegistration.Address), $"BizMan: the headend's page has the tabs {string.Join(", ", tabs)} (GO-BIZMAN-1).");
                }
                catch (Exception ex) { Go.Warn("BizMan tabs: " + ex.Message); }
            }
        }

        /// <summary>The game opens a page in a warehouse building on Drivers - for ours on Insight.</summary>
        [HarmonyPatch(typeof(BizManBusiness), nameof(BizManBusiness.SetInitialTab))]
        public static class Patch_BizManBusiness_SetInitialTab_Ours
        {
            static void Postfix(BizManBusiness __instance, string tab, ref string ____selectedTab)
            {
                try
                {
                    if (tab != null || !Ours(__instance.buildingRegistration) || !__instance.buildingRegistration.RentedByPlayer) return;
                    if (____selectedTab == "Drivers") ____selectedTab = "Insight";
                }
                catch { }
            }
        }

        // ── GO-FLEET-1: the Drivers tab shows the provider's fleet ────────────────────────────────────────────────────

        [HarmonyPatch(typeof(WhDrivers), "RefreshData")]
        public static class Patch_Drivers_RefreshData_OurFleet
        {
            [HarmonyPriority(Priority.High)]
            static bool Prefix(WhDrivers __instance, BizManBusiness ____bizManBusiness)
            {
                try
                {
                    var reg = ____bizManBusiness?.buildingRegistration;
                    bool ours = Headend(reg);
                    Relabel(__instance.transform, "bizman_drivers", ours ? GameUi.FleetTitle : null, __instance.driverStationTemplate != null ? __instance.driverStationTemplate.transform : null);
                    if (!ours) return true;
                    BuildFleet(__instance);
                    return false;
                }
                catch (Exception ex) { Go.Warn("fleet page: " + ex.Message); return true; }
            }
        }

        static void BuildFleet(WhDrivers page)
        {
            var tpl = page.driverStationTemplate;
            if (tpl == null) return;
            tpl.transform.ResetTemplate();
            var cards = GameUi.FleetCards();
            if (cards.Count == 0)
            {
                var st = Card(tpl);
                if (st == null) return;
                SetCard(st, GameUi.FleetNoneSlot, GameUi.FleetNone);
                try { var dd0 = Get<UI.Elements.Dropdown>(st, "driverDropdown"); if (dd0 != null) { dd0.SetOptions(new List<string> { "-" }, false, 0); dd0.SetInteractable(false); } } catch { }
                return;
            }
            foreach (var c in cards)
            {
                var st = Card(tpl);
                if (st == null) return;
                SetCard(st, c.Title, c.Vehicle);
                var dd = Get<UI.Elements.Dropdown>(st, "driverDropdown");
                if (dd == null) continue;
                dd.SetOptions(c.Labels.ToList(), false, c.Selected);
                dd.SetInteractable(c.Ids.Count > 1);
                dd.onOptionSelected.RemoveAllListeners();
                string carId = c.CarId;
                var ids = c.Ids.ToList();
                dd.onOptionSelected.AddListener(i =>
                {
                    // the cards are built again (one person has one vehicle: another card may lose them); ResetTemplate
                    // destroys the old ones only at the end of the frame, so this dropdown outlives its own event
                    try { if (i >= 0 && i < ids.Count) { GameUi.SetPin(carId, ids[i]); if (page != null && page.isActiveAndEnabled) BuildFleet(page); } }
                    catch (Exception ex) { Go.Warn("fleet pick: " + ex.Message); }
                });
            }
            Once("fleet", $"BizMan: the headend's fleet page shows {cards.Count} vehicle(s) (GO-FLEET-1).");
        }

        static WhStation? Card(WhStation tpl)
        {
            var go = UnityEngine.Object.Instantiate(tpl.gameObject, tpl.transform.parent);
            go.SetActive(true);
            return go.GetComponent<WhStation>();
        }

        static void SetCard(WhStation st, string title, string vehicle)
        {
            try { Get<TextLocalizationComponent>(st, "slotNumberField")?.SetValue(title, true); } catch { }
            try { var none = Get<GameObject>(st, "noVehicleAssignedObj"); if (none != null) none.SetActive(false); } catch { }
            try { var vn = Get<TMPro.TMP_Text>(st, "vehicleNameField"); if (vn != null) { vn.gameObject.SetActive(true); vn.SetText(vehicle); } } catch { }
            Relabel(st.transform, "bizman_drivers_driver", GameUi.FleetPinLabel);   // a clone: nothing to put back
        }

        /// <summary>Gives the game's labels with that key under root a text of ours (text null: puts back the game's).</summary>
        static readonly List<KeyValuePair<TextLocalizationComponent, string>> _relabelled = new List<KeyValuePair<TextLocalizationComponent, string>>();
        static void Relabel(Transform? root, string key, string? text, Transform? skip = null)
        {
            if (root == null) return;
            try
            {
                for (int i = _relabelled.Count - 1; i >= 0; i--)
                {
                    var kv = _relabelled[i];
                    if (kv.Key == null) { _relabelled.RemoveAt(i); continue; }
                    if (kv.Value != key || !kv.Key.transform.IsChildOf(root)) continue;
                    if (text == null) { kv.Key.Key = kv.Value; _relabelled.RemoveAt(i); }
                    else kv.Key.SetValue(text, true);
                }
                if (text == null) return;
                foreach (var c in root.GetComponentsInChildren<TextLocalizationComponent>(true))
                {
                    if (c == null || c.Key != key) continue;
                    if (skip != null && c.transform.IsChildOf(skip)) continue;
                    c.SetValue(text, true);
                    if (!c.transform.IsChildOf(root) || _relabelled.Exists(r => r.Key == c)) continue;
                    _relabelled.Add(new KeyValuePair<TextLocalizationComponent, string>(c, key));
                }
            }
            catch (Exception ex) { if (_once.Add("relabel|" + key)) Go.Warn($"label '{key}': {ex.Message}"); }
        }

        // ── GO-DELIVERY-1: the wholesalers deliver the devices to the headend ─────────────────────────────────────────

        /// <summary>The contract dialog lists the businesses whose building takes wholesale deliveries - an office or a
        /// warehouse does not; the headend does.</summary>
        [HarmonyPatch(typeof(UI.Dialog.DeliveryContractSettings), "PlayerBuildingFilter")]
        public static class Patch_DeliveryContractSettings_Filter_Headend
        {
            static void Postfix(BuildingRegistration buildingRegistration, ref bool __result)
            {
                if (!__result && Headend(buildingRegistration)) __result = true;
            }
        }

        static BuildingRegistration? _palletsFor;

        /// <summary>The dialog wants a shop's storage shelf in the business; the headend's pallet shelves count (scope).</summary>
        [HarmonyPatch(typeof(Dialogs.WholesaleStoreManagerDialog), "OnDeliveryContractSettingsSet")]
        public static class Patch_WholesaleDialog_ContractSet_Scope
        {
            static void Prefix()
            {
                _palletsFor = null;
                try
                {
                    var set = DialogController.current?.GetInputComponent<UI.Dialog.DeliveryContractSettings>();
                    if (Headend(set?.selectedBusiness)) _palletsFor = set!.selectedBusiness;
                }
                catch { }
            }
            static void Finalizer() { _palletsFor = null; }
        }

        /// <summary>... the check itself is a lambda of that method (x => x.ItemCached.HasTag(isbusinessstorage)).</summary>
        [HarmonyPatch]
        public static class Patch_WholesaleDialog_ShelfCheck_Pallets
        {
            static MethodBase? _target;
            static bool Prepare()
            {
                if (_target != null) return true;
                try
                {
                    foreach (var nt in typeof(Dialogs.WholesaleStoreManagerDialog).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
                        foreach (var m in nt.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                        {
                            if (!m.Name.Contains("<OnDeliveryContractSettingsSet>") || m.ReturnType != typeof(bool)) continue;
                            var ps = m.GetParameters();
                            if (ps.Length == 1 && ps[0].ParameterType == typeof(ItemInstance)) { _target = m; break; }
                        }
                }
                catch { }
                if (_target == null) Go.Warn("the wholesaler's shelf check was not found - the headend's pallet shelves do not count for a delivery contract (GO-DELIVERY-1).");
                return _target != null;
            }
            static MethodBase TargetMethod() => _target!;
            static void Postfix(ItemInstance __0, ref bool __result)
            {
                if (__result || _palletsFor == null) return;
                try { if (__0?.ItemCached != null && __0.ItemCached.HasTag(TagRef.Itemtag.iswarehousestorage)) __result = true; } catch { }
            }
        }

        /// <summary>BizMan's Deliveries tab offers what the wholesaler sells and the business type deals in - for the
        /// headend: the devices.</summary>
        [HarmonyPatch(typeof(UI.Smartphone.Apps.BizMan.BizManDeliveriesProductsScrollerController), "AddMissingProducts")]
        public static class Patch_DeliveriesProducts_AddMissing_Devices
        {
            static bool Prefix(DeliveryContract deliveryContract)
            {
                try
                {
                    if (deliveryContract == null || !Headend(Helpers.BuildingHelper.GetBuildingRegistration(deliveryContract.businessAddress))) return true;
                    var shop = Helpers.BuildingHelper.GetBuildingRegistration(deliveryContract.wholesaleAddress);
                    var sells = shop?.GetListOfItemsForSale();
                    if (sells == null) return true;
                    var devices = GameTypes.Pieces.Where(p => p.Kind == 2).Select(p => p.Name).ToList();
                    var have = deliveryContract.items.Select(x => x.itemName).ToList();
                    foreach (var item in sells)
                    {
                        if (devices.Contains(item)) { if (!have.Contains(item)) deliveryContract.items.Add(new DeliveryContractItem { itemName = item, amount = 0 }); }
                        else if (have.Contains(item))   // anything else: as the game does for goods the business does not deal in
                        {
                            if (!deliveryContract.enabled) deliveryContract.items.RemoveAll(x => x.itemName == item);
                            else deliveryContract.items.RemoveAll(x => x.itemName == item && x.amount == 0);
                        }
                    }
                    return false;
                }
                catch (Exception ex) { Go.Warn("delivery products: " + ex.Message); return true; }
            }
        }

        /// <summary>The delivery itself goes onto a shop's storage shelves (isbusinessstorage) - at the headend onto its
        /// pallet shelves too.</summary>
        [HarmonyPatch(typeof(ItemHelper), nameof(ItemHelper.DeliverCargoToBuilding))]
        public static class Patch_ItemHelper_DeliverCargo_HeadendPallets
        {
            static void Prefix(BuildingRegistration buildingRegistration, ref Func<ItemInstance, bool> filter)
            {
                try
                {
                    if (filter == null || !Headend(buildingRegistration)) return;
                    var f = filter;
                    filter = x => f(x) || (x?.ItemCached != null && x.ItemCached.HasTag(TagRef.Itemtag.iswarehousestorage));
                }
                catch { }
            }
        }
    }
}
