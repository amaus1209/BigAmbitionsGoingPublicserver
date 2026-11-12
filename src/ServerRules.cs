using Buildings;
using Entities;
using HarmonyLib;
using Helpers;
using SpecialServices.Bank;
using Streets;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>
    /// SERVER-RULES-1 (Going Public Server 1.0.12, user ruling 2026-10-30): the server admin's switches on AMP's settings
    /// page - one per own module (on by default) and the SmartRent game rules (the game's own values by default).
    ///   AMP writes them into bamp-server.json (MPConfig.ServerOverridableKeys), the dedicated server sends them to every
    ///   joiner in the world snapshot (WorldSnapshotPayload.Rules), and each player's game obeys them for that session.
    ///   Single player, and a server too old to send rules, keep every module on and the game's own rules.
    /// The rules act on every machine the same way (the host included), so a building costs the same for everybody:
    ///   RuleRentReturn      x RealEstate.DailyIncome - the rent paid by the tenants of owned buildings (shown and paid)
    ///   RuleBuildingPrice   % Building.GetMarketValue - buying, selling, listings and the value shown all follow it
    ///   RuleLoanLimit       x BankDialog.GetEconomyMaxLoanAmount - the wealth/income part of the bank's limit; the bank's
    ///                         own maximum stays
    ///   RuleBusinessSale (+ MinDays, ProfitDays) - "Sell business" in Insights (BusinessSale below)
    /// None of the three patched methods is "tiny" (IL 29 / 35 / 118 bytes, TINY-DETOUR-2 draws the line at 16).
    /// </summary>
    internal static class ServerRules
    {
        /// <summary>bamp-server.json key -> default (= AMP's DefaultValue).</summary>
        internal static readonly (string key, string def)[] Keys =
        {
            ("ModuleIrsPhone", "true"), ("ModuleShopSigns", "true"), ("ModuleCamera", "true"), ("ModulePlanner", "true"), ("ModuleInsights", "true"),
            ("ModuleNavigation", "true"), ("ModulePhonePages", "true"),   // PHONE-PAGES-1: the phone's apps in pages
            ("ModuleGoingOnline", "true"),   // GOING-ONLINE-1: the cable provider
            ("ModuleItFirm", "true"), ("ModuleCleaning", "true"), ("ModuleLaundromat", "true"),   // BUSINESS-PACK-1: IT firm, cleaning company, laundromat
            ("RuleRentReturn", "1"), ("RuleBuildingPrice", "100"), ("RuleBusinessSale", "false"),
            ("RuleBusinessSaleMinDays", "25"), ("RuleBusinessSaleProfitDays", "30"), ("RuleLoanLimit", "1"),
            // SERVER-SETTINGS-1 (user ruling 2026-10-30): on a server the modules' settings come from AMP only, the same for
            // every player - the RPG camera's (F8 window) and the navigation's route line
            ("CamFootPitch", "20"), ("CamFootDist", "8"), ("CamCarPitch", "18"), ("CamCarDist", "15"), ("CamHeight", "1.5"),
            ("CamShoulder", "0"), ("CamSensitivity", "1"), ("CamFov", "55"), ("CamViewDistance", "1"), ("CamDeadZone", "3"),
            ("CamRecenter", "true"), ("CamIndoors", "false"), ("NavRouteLine", "true"),
            ("NavPhone", "true"),   // NAV-PHONE-1/2: the sat-nav lies in the BizPhone (off: on top of it)
            ("SteamFriendJoin", "true"),   // STEAM-FRIEND-JOIN-1: the players' Steam friends see Join Game for this server
            ("NetRate", "2"),   // NET-RATE-1: network level 0-3 (NetRate)
            ("RadioList", "de"),   // RADIO-STREAMS-2: the language of the radio station list from GitHub ("off" = none)
            ("PlayerReports", "true"),   // PLAYER-REPORTS-1: the players' error reports to this dedicated server (each player is asked once)
            ("ChatTranslate", "true"),   // CHAT-TRANSLATE-1: this dedicated server translates the chat German <-> English
        };

        private static volatile Dictionary<string, string>? _fromServer;   // client: what the server sent this session
        private static Dictionary<string, string>? _cache;
        private static int _cacheMode = -1;
        private static Dictionary<string, string>? _cacheSource;

        /// <summary>Host: the values the server sends (bamp-server.json, defaults where missing).</summary>
        internal static Dictionary<string, string> HostValues()
        {
            var d = new Dictionary<string, string>();
            // a decimal written with a comma (a German AMP or system) still reads as a number
            foreach (var (k, def) in Keys) d[k] = MPConfig.GetOption(k, def).Trim().ToLowerInvariant().Replace(',', '.');
            if (!HeadlessHost.Active) d["PlayerReports"] = "false";   // PLAYER-REPORTS-1: a player's own game hosting friends never collects
            if (!HeadlessHost.Active) d["ChatTranslate"] = "false";   // CHAT-TRANSLATE-1: only a dedicated server runs the translator
            return d;
        }

        /// <summary>PLAYER-REPORTS-1: this client's server collects error reports - only when the server sent the rule
        /// itself (a server without it knows no such message, the default must not switch it on).</summary>
        internal static bool ServerCollectsReports
        {
            get
            {
                var src = _fromServer;
                return Mode() == 2 && src != null && src.TryGetValue("PlayerReports", out var v) && v == "true";
            }
        }

        /// <summary>CHAT-TRANSLATE-1: this client's server translates the chat - only when the server sent the rule itself
        /// (an older server knows no ChatPrefs message).</summary>
        internal static bool ServerTranslatesChat
        {
            get
            {
                var src = _fromServer;
                return Mode() == 2 && src != null && src.TryGetValue("ChatTranslate", out var v) && v == "true";
            }
        }

        /// <summary>Client: the world snapshot arrived (null = a new connection starts or the server sent none).</summary>
        internal static void Apply(Dictionary<string, string>? rules)
        {
            Version++;
            _fromServer = rules == null || rules.Count == 0 ? null : new Dictionary<string, string>(rules);
            if (_fromServer != null)
                Plugin.Logger.LogInfo($"[Rules] from the server: {string.Join(", ", _fromServer.Select(kv => kv.Key + "=" + kv.Value))}");
        }

        /// <summary>0 = single player (defaults), 1 = the host itself, 2 = a client in a server's world.</summary>
        private static int Mode() => MPServer.IsRunning ? 1 : MPClient.IsClientInWorld ? 2 : 0;

        private static Dictionary<string, string> Values()
        {
            int mode = Mode();
            var src = _fromServer;
            if (_cache != null && mode == _cacheMode && ReferenceEquals(src, _cacheSource)) return _cache;
            var d = new Dictionary<string, string>();
            foreach (var (k, def) in Keys) d[k] = def;
            if (mode == 1) d = HostValues();
            else if (mode == 2 && src != null)
                foreach (var kv in src) if (d.ContainsKey(kv.Key)) d[kv.Key] = kv.Value;
            _cache = d; _cacheMode = mode; _cacheSource = src;
            return d;
        }

        private static string Get(string key) => Values().TryGetValue(key, out var v) ? v : "";

        private static float Num(string key, float def, float min, float max)
            => float.TryParse(Get(key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? Mathf.Clamp(f, min, max) : def;

        /// <summary>Bumped whenever a server's values arrive or a connection starts.</summary>
        internal static volatile int Version;

        /// <summary>A client in a server's world that sent its settings: the modules' settings are the server's (AMP), the
        /// players' own ones are not used and their settings windows only show them.</summary>
        internal static bool FromServer => Mode() == 2 && _fromServer != null;

        internal static float Value(string key, float def, float min, float max) => Num(key, def, min, max);
        internal static bool Flag(string key) => Get(key) == "true";

        internal static bool Module(string key) => Get(key) != "false";
        internal static float RentFactor => Num("RuleRentReturn", 1f, 1f, 18f);
        internal static float BuildingPriceFactor => Num("RuleBuildingPrice", 100f, 10f, 100f) / 100f;
        internal static float LoanFactor => Num("RuleLoanLimit", 1f, 1f, 10f);
        internal static bool BusinessSale => Get("RuleBusinessSale") == "true";
        internal static int SaleMinDays => Mathf.RoundToInt(Num("RuleBusinessSaleMinDays", 25f, 0f, 1000f));
        internal static int SaleProfitDays => Mathf.RoundToInt(Num("RuleBusinessSaleProfitDays", 30f, 0f, 60f));
        /// <summary>NET-RATE-1: how often positions, cars and traffic go over the network (0-3, see NetRate).</summary>
        /// <summary>RADIO-STREAMS-2: the station list's language code (radio/&lt;code&gt;.txt on GitHub), "off" = none.</summary>
        internal static string RadioList => Get("RadioList");
        internal static int NetLevel => Mathf.RoundToInt(Num("NetRate", 2f, 0f, 3f));

        private static float _noticeAt = -10f;

        /// <summary>A switched-off module's key was pressed: say so (at most every 2 s).</summary>
        internal static void ModuleOff(string feature)
        {
            if (Time.unscaledTime - _noticeAt < 2f) return;
            _noticeAt = Time.unscaledTime;
            try { UI.Notification.Notifications.Show(UI.Notification.NotificationType.Warning, Loc.T("gps_rules_module_off", "{0} is switched off on this server.", feature)); } catch { }
        }

        /// <summary>A setting the server decides was to be changed here: say so (at most every 2 s).</summary>
        internal static void ServerSet(string feature)
        {
            if (Time.unscaledTime - _noticeAt < 2f) return;
            _noticeAt = Time.unscaledTime;
            try { UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, Loc.T("gps_rules_server_set", "On this server the admin sets {0} in AMP.", feature)); } catch { }
        }

        // ── the rules' patches ──

        [HarmonyPatch(typeof(RealEstate), nameof(RealEstate.DailyIncome), MethodType.Getter)]
        public static class Patch_RealEstate_DailyIncome_Rule
        {
            static void Postfix(ref float __result)
            {
                try { float f = RentFactor; if (f != 1f) __result *= f; } catch { }
            }
        }

        [HarmonyPatch(typeof(Building), nameof(Building.GetMarketValue))]
        public static class Patch_Building_GetMarketValue_Rule
        {
            static void Postfix(ref float __result)
            {
                try { float f = BuildingPriceFactor; if (f != 1f) __result *= f; } catch { }
            }
        }

        /// <summary>The game's own formula (BankDialog.GetEconomyMaxLoanAmount) with its wealth/income part times the factor.</summary>
        [HarmonyPatch(typeof(Dialogs.BankDialog), "GetEconomyMaxLoanAmount")]
        public static class Patch_BankDialog_EconomyMaxLoan_Rule
        {
            static void Postfix(BankSettings bankSettings, ref float __result)
            {
                try
                {
                    float f = LoanFactor;
                    if (f <= 1f) return;
                    float a = 0f;
                    if (TutorialHelper.IsTutorialEnabled() && !SaveGameManager.Current.CompletedQuestEntries.Contains("tutorial_quest_establish_first_business_objective_1")) a = 15500f;
                    a = Mathf.Max(a, PlayerHelper.GetPersonalWealth().WealthBeforeLoans);
                    a = Mathf.Max(a, PlayerHelper.CalculateDailyIncome() * LoanHelper.CalculatePayBackDays(bankSettings) * 0.25f);
                    float remaining = 0f;
                    foreach (var loan in SaveGameManager.Current.Loans) if (loan != null) remaining += loan.remainingAmount;
                    __result = Mathf.Max(__result, Mathf.FloorToInt(Mathf.Max(0f, a * f - remaining)));
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// SERVER-RULES-1 / RuleBusinessSale: sell one of your businesses from Insights. The game itself only knows ending the
    /// lease (BizManPresentation.TerminateContract): the furniture and vehicles are sold and the deposit comes back, and the
    /// mod's own patches on it free the building for every player. A sale is exactly that - BizMan opens on the business
    /// and the game asks its own question - plus a goodwill of 30 days' average daily profit, paid once the lease has
    /// really ended. A business may be sold when it is RuleBusinessSaleMinDays old (creationDay) and had
    /// RuleBusinessSaleProfitDays days with profit in the game's books (they reach back one game year).
    /// </summary>
    internal static class BusinessSale
    {
        internal sealed class Offer
        {
            public BuildingRegistration Reg = null!;
            public string Key = "", Name = "";
            public int Age, ProfitDays, KnownDays;
            public float AvgProfit, Closure, Goodwill;
            public bool Eligible;
        }

        private sealed class Pending { public BuildingRegistration Reg = null!; public string Key = "", Name = ""; public float Goodwill, At; public int Stage; }
        private static Pending? _pending;

        internal static bool IsPending(string key) => _pending != null && _pending.Key == key;

        internal static List<Offer> Offers()
        {
            var list = new List<Offer>();
            int day = ShopInsights.Today();
            var sums = ShopInsights.Last(ShopInsights.MaxDays);   // [0] = yesterday
            foreach (var reg in ShopInsights.Own())
            {
                try
                {
                    if (!ShopInsights.SellsToCustomers(reg)) continue;
                    var o = new Offer { Reg = reg, Key = ShopInsights.Key(reg.Address), Name = reg.BusinessName };
                    o.Age = reg.creationDay >= 0 ? Math.Max(0, day - reg.creationDay) : 0;
                    float last30 = 0f; int n30 = 0;
                    foreach (var s in sums)
                    {
                        var st = s?.businessIncomeStatements?.Find(x => x != null && ShopInsights.Key(x.Address) == o.Key);
                        if (st == null) continue;
                        o.KnownDays++;
                        if (st.TotalProfit > 0f) o.ProfitDays++;
                        if (n30 < 30) { last30 += st.TotalProfit; n30++; }
                    }
                    o.AvgProfit = n30 > 0 ? last30 / n30 : 0f;
                    o.Goodwill = Mathf.Max(0f, o.AvgProfit) * 30f;
                    try
                    {
                        float items = reg.itemInstances?.Values.Where(i => i != null).Sum(i => i.GetSellingPrice()) ?? 0f;
                        float cars = SaveGameManager.Current.VehicleInstances.Where(v => v != null && v.Address == reg.Address).Sum(v => v.GetSellingPrice());
                        o.Closure = items + cars + reg.lastDeposit;
                    }
                    catch { }
                    o.Eligible = o.Age >= ServerRules.SaleMinDays && o.ProfitDays >= ServerRules.SaleProfitDays;
                    list.Add(o);
                }
                catch { }
            }
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        /// <summary>Main thread: open BizMan on the business; Tick asks the game's own question a moment later.</summary>
        internal static string Start(Offer o)
        {
            if (!ServerRules.BusinessSale || !o.Eligible) return "";
            if (BuildingManager.IsInsideBuilding && InstanceBehavior<BuildingManager>.Instance?.buildingRegistration == o.Reg)
                return Loc.T("gps_ins_sell_inside", "Leave the building first.");
            _pending = new Pending { Reg = o.Reg, Key = o.Key, Name = o.Name, Goodwill = o.Goodwill, At = Time.unscaledTime, Stage = 0 };
            try { InstanceBehavior<UI.UIs>.Instance.fullMenu.bizMan.Open(o.Reg.Address, "Presentation"); }
            catch (Exception ex) { _pending = null; Plugin.Logger.LogWarning($"[Sale] open BizMan: {ex.Message}"); return ex.Message; }
            Plugin.Logger.LogInfo($"[Sale] '{o.Name}': BizMan opened for the sale (goodwill {o.Goodwill:0}).");
            return Loc.T("gps_ins_sell_confirm_hint", "Confirm the end of the lease in BizMan. Then you also receive {0} for {1}.", ShopInsights.Money(o.Goodwill), o.Name);
        }

        /// <summary>Main thread, every frame in the world.</summary>
        internal static void Tick()
        {
            var p = _pending;
            if (p == null) return;
            try
            {
                if (p.Stage == 0)
                {
                    if (Time.unscaledTime - p.At < 0.4f) return;
                    var pres = InstanceBehavior<UI.UIs>.Instance?.fullMenu?.bizMan?.GetComponentInChildren<BizManPresentation>(true);
                    if (pres != null && pres.bizManBusiness != null && pres.bizManBusiness.buildingRegistration == p.Reg)
                    {
                        p.Stage = 1; p.At = Time.unscaledTime;
                        pres.TerminateContract();   // the game's own question; its answer ends the lease (or not)
                    }
                    else if (Time.unscaledTime - p.At > 5f) { Plugin.Logger.LogWarning($"[Sale] '{p.Name}': BizMan did not show the business - no sale."); _pending = null; }
                    return;
                }
                if (!p.Reg.RentedByPlayer)
                {
                    _pending = null;
                    if (p.Goodwill > 0f)
                    {
                        var data = new Dictionary<string, string> { { "address", p.Reg.Address.ToFormattedString() } };
                        GameManager.ChangeMoneySafe(p.Goodwill, new TransactionInfo("ba:transaction_businessinventorysold", data), null, null, force: true);
                    }
                    Plugin.Logger.LogInfo($"[Sale] '{p.Name}' sold: lease ended by the game, goodwill {p.Goodwill:0} paid.");
                    try { UI.Notification.Notifications.Show(UI.Notification.NotificationType.Success, Loc.T("gps_ins_sold", "{0} sold: {1} goodwill received.", p.Name, ShopInsights.Money(p.Goodwill))); } catch { }
                    return;
                }
                if (Time.unscaledTime - p.At > 120f) { Plugin.Logger.LogInfo($"[Sale] '{p.Name}': the lease was not ended - no sale."); _pending = null; }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Sale] tick: {ex.Message}"); _pending = null; }
        }
    }
}
