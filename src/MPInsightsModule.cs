using Entities;
using Extensions;
using Helpers;
using Localizor;
using TMPro;
using UI.Smartphone;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>
    /// MODULE-INSIGHTS-1 (Going Public Server 1.0.12, module 5 of the server's own features; replaces the read-only part of
    /// the SmartSuite mod - SmartInsights - with own code): Shift+F10 opens "Insights" about the local player's own
    /// businesses and buildings, read from the game's own books:
    ///   - the daily FinancialSummary the game writes at midnight and keeps for a game year (FinancialSummaryHelper:
    ///     per business sales, goods, wages, rent, marketing, theft and licensing fees, per building the rent income,
    ///     loans and the other personal lines);
    ///   - each registration's orderHistory (units, revenue and wholesale cost per product, customers per day);
    ///   - the employees (hourly wage, weekly hours, satisfaction) and SaveGameManager.realEstate (purchase price and day,
    ///     occupancy, daily income).
    /// Tabs: Overview (period results, portfolio, alerts of the last 7 days), Profit (per business), History (daily
    /// profit bars), Staff, Sales (per product), Real estate. Nothing is written and nothing is sent - in multiplayer
    /// every player sees only the books of this machine.
    /// The rule-changing parts of SmartSuite (rent and purchase-price profiles, business sale, loan scaling, rival price
    /// responses) are NOT here: they change the game for everybody and need the server's say first.
    /// </summary>
    internal static class ShopInsights
    {
        internal sealed class BizRow
        {
            public string Key = "", Name = "";
            public float Sales, Goods, Wages, Rent, Marketing, Theft, Other, Profit;
            public int Days, ProfitDays;
        }
        internal sealed class StaffRow { public string Name = ""; public int Count, Unhappy; public float WagesWeek, AvgSat; }
        internal sealed class PaidRow { public string Name = "", Biz = ""; public float Wage; }
        internal sealed class ItemRow { public string Item = ""; public int Units; public float Revenue, Cost; }
        internal sealed class EstateRow { public string Name = ""; public double Bought; public int DaysOwned, Let, Payback; public float PerDay, Period; }
        internal sealed class Totals { public float Business, Estate, Housing, Loans, Other, Total; public int Customers; }

        /// <summary>The game keeps a year of daily summaries (gameVariables.daysPerYear, 60 by default).</summary>
        internal static int MaxDays
        {
            get { try { return Math.Max(1, Math.Min(60, SaveGameManager.Current.gameVariables.daysPerYear)); } catch { return 30; } }
        }

        /// <summary>The last n days' summaries, [0] = yesterday; a missing day is an empty summary.</summary>
        internal static List<FinancialSummary> Last(int n)
        {
            try { return FinancialSummaryHelper.GetLastFinancialSummaries(Mathf.Clamp(n, 1, MaxDays)); }
            catch { return new List<FinancialSummary>(); }
        }

        internal static string Key(Address? a) => a == null ? "" : a.ToString();

        internal static string NameAt(Address? a)
        {
            try
            {
                var reg = a == null ? null : BuildingHelper.GetBuildingRegistration(a);
                if (reg != null && !string.IsNullOrWhiteSpace(reg.BusinessName)) return reg.BusinessName;
            }
            catch { }
            return a?.ToString() ?? "?";
        }

        internal static string Money(float v) => ((double)v).ToShortCurrencyFormat(true);

        internal static string L(string key)
        {
            // a missing key comes back as the (lower-cased) key itself
            try { string s = key.GetLocalization(); if (!string.IsNullOrEmpty(s) && !string.Equals(s, key, StringComparison.OrdinalIgnoreCase)) return s; } catch { }
            int c = key.LastIndexOf('_');
            return c >= 0 && c < key.Length - 1 ? key.Substring(c + 1) : key;
        }

        /// <summary>The local player's own non-residential registrations (businesses, headquarters, warehouses).</summary>
        internal static List<BuildingRegistration> Own()
        {
            var list = new List<BuildingRegistration>();
            var gi = SaveGameManager.Current;
            if (gi?.BuildingRegistrations == null) return list;
            foreach (var reg in gi.BuildingRegistrations)
            {
                try
                {
                    if (reg == null || !reg.RentedByPlayer || string.IsNullOrWhiteSpace(reg.BusinessName)) continue;
                    if (GameStatePatcher.IsForeignPlayerBusiness(reg)) continue;
                    if (BuildingHelper.GetBuilding(reg.Address)?.BuildingType == "ba:buildingtype_residential") continue;
                    list.Add(reg);
                }
                catch { }
            }
            return list;
        }

        internal static bool SellsToCustomers(BuildingRegistration reg)
        {
            string t = reg.businessTypeName ?? "";
            return t.Length > 0 && t != "ba:businesstype_empty" && t != "ba:businesstype_headquarters" && t != "ba:businesstype_warehouse";
        }

        internal static List<BizRow> Businesses(List<FinancialSummary> sums)
        {
            var map = new Dictionary<string, BizRow>();
            foreach (var s in sums)
            {
                if (s?.businessIncomeStatements == null) continue;
                foreach (var st in s.businessIncomeStatements)
                {
                    if (st?.Address == null) continue;
                    string k = Key(st.Address);
                    if (!map.TryGetValue(k, out var r)) map[k] = r = new BizRow { Key = k, Name = NameAt(st.Address) };
                    r.Sales += st.TotalSales; r.Goods += st.TotalResources; r.Wages += st.SalaryExpenses; r.Rent += st.RentExpenses;
                    r.Marketing += st.MarketingExpenses; r.Theft += st.Theft; r.Other += st.Theft + st.LicensingFees; r.Profit += st.TotalProfit;
                    r.Days++;
                    if (st.TotalProfit > 0f) r.ProfitDays++;
                }
            }
            var list = map.Values.ToList();
            list.Sort((a, b) => b.Profit.CompareTo(a.Profit));
            return list;
        }

        internal static Totals Sum(List<FinancialSummary> sums, int days)
        {
            var t = new Totals();
            foreach (var s in sums)
            {
                if (s == null) continue;
                t.Business += s.totalBusinessProfit; t.Estate += s.totalRealEstate; t.Housing += s.totalResidentialExpenses;
                t.Loans += s.totalLoanExpenses; t.Total += s.totalProfit;
            }
            t.Other = t.Total - t.Business - t.Estate + t.Housing - t.Loans;
            int day = Today();
            foreach (var reg in Own())
                foreach (var e in reg.orderHistory ?? new List<OrderHistoryEntry>())
                    if (e != null && e.dayNumber >= day - days && e.dayNumber < day) t.Customers += e.totalCustomers;
            return t;
        }

        internal static int Today() { try { return SaveGameManager.Current.Day; } catch { return 0; } }

        /// <summary>Daily business profit, oldest first: one business (its key) or all ("").</summary>
        internal static List<(int day, float profit, bool known)> History(int n, string key)
        {
            var sums = Last(n);
            int day = Today();
            var list = new List<(int, float, bool)>();
            for (int i = sums.Count - 1; i >= 0; i--)
            {
                var s = sums[i];
                float p = 0f; bool known = false;
                if (s?.businessIncomeStatements != null)
                    foreach (var st in s.businessIncomeStatements)
                        if (st != null && (key.Length == 0 || Key(st.Address) == key)) { p += st.TotalProfit; known = true; }
                list.Add((day - (i + 1), p, known));
            }
            return list;
        }

        internal static (List<StaffRow> rows, List<PaidRow> top, int total) Staff()
        {
            var rows = new List<StaffRow>();
            var paid = new List<PaidRow>();
            int total = 0;
            foreach (var reg in Own())
            {
                List<EmployeeInstance> emps;
                try { emps = EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { withAssignedAddress = reg.Address }); }
                catch { continue; }
                if (emps.Count == 0) continue;
                var r = new StaffRow { Name = reg.BusinessName, Count = emps.Count };
                float sat = 0f;
                foreach (var e in emps)
                {
                    if (e == null) continue;
                    r.WagesWeek += e.hourlyWage * e.assignedWeeklyHours;
                    sat += e.satisfaction;
                    if (e.satisfaction < 0.4f) r.Unhappy++;
                    paid.Add(new PaidRow { Name = e.characterData?.name ?? "?", Biz = reg.BusinessName, Wage = e.hourlyWage });
                }
                r.AvgSat = sat * 100f / emps.Count;
                total += emps.Count;
                rows.Add(r);
            }
            rows.Sort((a, b) => b.WagesWeek.CompareTo(a.WagesWeek));
            paid.Sort((a, b) => b.Wage.CompareTo(a.Wage));
            return (rows, paid.Take(5).ToList(), total);
        }

        internal static List<ItemRow> Items(int n)
        {
            var map = new Dictionary<string, ItemRow>();
            int day = Today();
            foreach (var reg in Own())
                foreach (var e in reg.orderHistory ?? new List<OrderHistoryEntry>())
                {
                    if (e?.itemSales == null || e.dayNumber < day - n || e.dayNumber >= day) continue;
                    foreach (var it in e.itemSales)
                    {
                        if (it == null || string.IsNullOrEmpty(it.itemName)) continue;
                        if (!map.TryGetValue(it.itemName, out var r)) map[it.itemName] = r = new ItemRow { Item = L(it.itemName) };
                        r.Units += it.amountSold; r.Revenue += it.totalPrice; r.Cost += it.totalWholesalePrice;
                    }
                }
            var list = map.Values.ToList();
            list.Sort((a, b) => b.Revenue.CompareTo(a.Revenue));
            return list;
        }

        internal static List<EstateRow> Estate(List<FinancialSummary> sums)
        {
            var list = new List<EstateRow>();
            var gi = SaveGameManager.Current;
            if (gi?.realEstate == null) return list;
            int day = Today();
            foreach (var re in gi.realEstate)
            {
                try
                {
                    if (re?.address == null) continue;
                    if (re.Building != null && re.Building.IsHamptonsHouse()) continue;   // the game leaves these out of its books too
                    string k = Key(re.address), name = re.address.ToString();
                    string biz = NameAt(re.address);
                    if (biz != name) name += " · " + biz;
                    var r = new EstateRow { Name = name, Bought = re.purchasePrice, DaysOwned = Math.Max(0, day - re.purchaseDay) };
                    try { r.Let = re.OccupancyPercentage; r.PerDay = re.DailyIncome; } catch { }
                    foreach (var s in sums)
                        if (s?.realEstateStatements != null)
                            foreach (var st in s.realEstateStatements)
                                if (st != null && Key(st.Address) == k) r.Period += st.Amount;
                    r.Payback = r.PerDay > 0f ? (int)Math.Ceiling(re.purchasePrice / r.PerDay) : -1;
                    list.Add(r);
                }
                catch { }
            }
            list.Sort((a, b) => b.PerDay.CompareTo(a.PerDay));
            return list;
        }

        /// <summary>What needs a look, from the last 7 days.</summary>
        internal static List<string> Alerts()
        {
            var alerts = new List<string>();
            var rows = Businesses(Last(7));
            foreach (var reg in Own())
            {
                try
                {
                    var row = rows.Find(r => r.Key == Key(reg.Address));
                    string n = reg.BusinessName;
                    if (row != null && row.Days > 0 && row.Profit < 0f) alerts.Add(Loc.T("gps_ins_a_loss", "{0}: loss of {1}.", n, Money(-row.Profit)));
                    if (SellsToCustomers(reg))
                    {
                        int open = 0;
                        try { open = reg.scheduleDays?.Sum(d => d != null && d.isOpen ? d.GetHoursOpen : 0) ?? 0; } catch { }
                        if (row != null && row.Days > 0 && row.Sales <= 0f && open > 0) alerts.Add(Loc.T("gps_ins_a_nosales", "{0}: no sales.", n));
                        int sat = reg.satisfaction?.overall ?? 100;
                        if (row != null && row.Sales > 0f && sat < 50) alerts.Add(Loc.T("gps_ins_a_satisfaction", "{0}: customer satisfaction only {1}%.", n, sat));
                        if (row != null && row.Theft > 0f && row.Theft > row.Sales * 0.05f) alerts.Add(Loc.T("gps_ins_a_theft", "{0}: theft cost {1}.", n, Money(row.Theft)));
                    }
                    int unhappy = 0;
                    try { unhappy = EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { withAssignedAddress = reg.Address }).Count(e => e != null && e.satisfaction < 0.4f); } catch { }
                    if (unhappy > 0) alerts.Add(Loc.T("gps_ins_a_unhappy", "{0}: {1} unhappy employee(s).", n, unhappy));
                }
                catch { }
            }
            foreach (var e in Estate(new List<FinancialSummary>()))
                if (e.Let < 50) alerts.Add(Loc.T("gps_ins_a_vacancy", "{0}: only {1}% let.", e.Name, e.Let));
            return alerts;
        }
    }

    public partial class MPCanvasUI
    {
        private GameObject? _inRoot;
        private bool _inOpen;
        private int _inTab, _inPeriod = 7, _inHistDays = 14, _inPage, _inBizPage;
        private string _inHistKey = "", _inStatus = "";
        private readonly List<(RectTransform rt, int act, int idx)> _inHits = new List<(RectTransform, int, int)>();

        private static bool InKey() => ModKeys.Insights.Down();   // MOD-KEYS-1: Shift+F10 unless the player chose another key

        /// <summary>Main thread, every frame from Update: Shift+F10 toggles the Insights window in the world.</summary>
        private void TickInsights()
        {
            if (HeadlessHost.Active) return;
            try
            {
                bool inWorld = SaveGameManager.Current != null && PlayerHelper.PlayerController != null && !GameManager.isCitySceneBeingUnloaded;
                if (!inWorld) { if (_inOpen) InClose(); return; }
                BusinessSale.Tick();   // SERVER-RULES-1: a sale waiting for the game's end of the lease
                if (!_inOpen)
                {
                    if (InKey() && !MPChat.SuppressGameInput && !GameManager.HasInputSelected() && !FullMenu.IsOpen)
                    {
                        if (ServerRules.Module("ModuleInsights")) InOpen();   // SERVER-RULES-1
                        else ServerRules.ModuleOff(Loc.T("gps_ins_title", "Insights"));
                    }
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Escape) || InKey()) { InClose(); ConsumeEscape(); return; }
                if (!Input.GetMouseButtonDown(0)) return;
                var mp = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                foreach (var (rt, act, idx) in _inHits)
                    if (rt != null && rt.gameObject.activeInHierarchy && RectHit(rt, mp)) { InAct(act, idx); return; }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Insights] tick: {ex.Message}"); }
        }

        private void InOpen()
        {
            _inOpen = true;
            InRebuild();
            Plugin.Logger.LogInfo("[Insights] window opened.");
        }

        private void InClose()
        {
            _inOpen = false;
            ShieldPressUntilRelease();   // NAV-WALK-CLICK-1
            _inHits.Clear();
            if (_inRoot != null) { try { Destroy(_inRoot); } catch { } _inRoot = null; }
        }

        // acts: 1 tab (idx) · 2 period (idx = days) · 3/4 page -/+ · 5 history business (idx, -1 = all) · 6 history days (idx)
        //       7/8 history list page -/+ · 9 close
        private void InAct(int act, int idx)
        {
            switch (act)
            {
                case 1: _inTab = idx; _inPage = 0; _inStatus = ""; break;
                case 2: _inPeriod = idx; _inPage = 0; break;
                case 3: _inPage = Math.Max(0, _inPage - 1); break;
                case 4: _inPage++; break;
                case 5:
                    var own = ShopInsights.Own();
                    _inHistKey = idx >= 0 && idx < own.Count ? ShopInsights.Key(own[idx].Address) : "";
                    break;
                case 6: _inHistDays = idx; break;
                case 7: _inBizPage = Math.Max(0, _inBizPage - 1); break;
                case 8: _inBizPage++; break;
                case 9: InClose(); return;
                case 10:   // SERVER-RULES-1: sell a business (idx = its row in BusinessSale.Offers)
                    var offers = BusinessSale.Offers();
                    if (idx >= 0 && idx < offers.Count)
                    {
                        string msg = BusinessSale.Start(offers[idx]);
                        if (BusinessSale.IsPending(offers[idx].Key))
                        {
                            InClose();   // BizMan and the game's own question need the screen
                            try { UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, msg); } catch { }
                            return;
                        }
                        _inStatus = msg;
                    }
                    break;
            }
            InRebuild();
        }

        private string InPeriodLabel(int days) => days <= 1 ? Loc.T("gps_ins_period_1", "Yesterday") : days == 7 ? Loc.T("gps_ins_period_7", "7 days") : Loc.T("gps_ins_period_30", "30 days");

        // ── the window (lobby kit, mock-up units) ──
        private void InRebuild()
        {
            if (_canvasGO == null) return;
            if (_inRoot != null) { try { Destroy(_inRoot); } catch { } }
            _inHits.Clear();
            _inRoot = LMakeRoot("BAMP_Insights");
            _inRoot.transform.SetAsLastSibling();
            const float W = 1080f, H = 700f;
            var card = LMakeCard(_inRoot.transform, "Card", Loc.T("gps_ins_title", "Insights"), W, H);
            card.sub.text = ModKeys.Insights.Display();   // MOD-KEYS-1
            var ct = card.go.transform;

            var tabs = new List<string>
            {
                Loc.T("gps_ins_tab_overview", "Overview"), Loc.T("gps_ins_tab_profit", "Profit"), Loc.T("gps_ins_tab_history", "History"),
                Loc.T("gps_ins_tab_staff", "Staff"), Loc.T("gps_ins_tab_sales", "Sales"), Loc.T("gps_ins_tab_estate", "Real estate"),
            };
            if (ServerRules.BusinessSale) tabs.Add(Loc.T("gps_ins_tab_sell", "Sell business"));   // SERVER-RULES-1
            if (_inTab >= tabs.Count) _inTab = 0;
            float tw = (W - 52f) / tabs.Count;
            for (int i = 0; i < tabs.Count; i++)
            {
                var b = LButton(ct, "Tab" + i, tabs[i], 26f + i * tw, 56f, tw - 8f, 34f, i == _inTab ? 1 : 0, 12f);
                _inHits.Add((b.rt, 1, i));
            }
            if (_inTab != 2 && _inTab != 3 && _inTab != 6)
            {
                int[] periods = { 1, 7, 30 };
                for (int i = 0; i < periods.Length; i++)
                {
                    var b = LButton(ct, "Per" + i, InPeriodLabel(periods[i]), W - 26f - (3 - i) * 136f, 100f, 128f, 30f, _inPeriod == periods[i] ? 1 : 0, 11f);
                    _inHits.Add((b.rt, 2, periods[i]));
                }
            }

            try
            {
                switch (_inTab)
                {
                    case 0: InOverview(ct, W); break;
                    case 1: InProfit(ct, W, H); break;
                    case 2: InHistory(ct, W, H); break;
                    case 3: InStaff(ct, W, H); break;
                    case 4: InSales(ct, W, H); break;
                    case 6: InSell(ct, W, H); break;
                    default: InEstate(ct, W, H); break;
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[Insights] tab {_inTab}: {ex.Message}");
                LText(ct, ex.Message, 12f, L_ERR, 26f, 150f, W - 52f, 40f, TextAlignmentOptions.TopLeft, false, false, 0f, true);
            }
            var cl = LButton(ct, "Close", Loc.T("gps_ui_close", "Close"), W - 26f - 150f, H - 56f, 150f, 36f, 0, 12f); _inHits.Add((cl.rt, 9, 0));
        }

        private void InLine(Transform ct, string label, string value, float x, float y, float w, bool strong = false, Color? vc = null)
        {
            LText(ct, label, 13f, strong ? C_WHITE : L_MUTED, x, y, w - 170f, 24f, TextAlignmentOptions.Left, strong);
            LText(ct, value, 13f, vc ?? C_WHITE, x + w - 170f, y, 170f, 24f, TextAlignmentOptions.Right, strong);
        }

        private static Color InSign(float v) => v < 0f ? L_ERR : C_WHITE;

        private void InOverview(Transform ct, float W)
        {
            int days = Math.Min(_inPeriod, ShopInsights.MaxDays);
            var sums = ShopInsights.Last(days);
            var t = ShopInsights.Sum(sums, days);
            var rows = ShopInsights.Businesses(sums);
            const float LX = 26f, LW = 500f, RX = 560f;
            float RW = W - RX - 26f;
            float y = 146f;
            LText(ct, Loc.T("gps_ins_results", "Results: {0}", InPeriodLabel(_inPeriod)), 15f, C_WHITE, LX, y, LW, 26f, TextAlignmentOptions.Left, true);
            y += 32f;
            InLine(ct, Loc.T("gps_ins_business_profit", "Business profit"), ShopInsights.Money(t.Business), LX, y, LW, false, InSign(t.Business)); y += 26f;
            InLine(ct, Loc.T("gps_ins_estate_income", "Real estate income"), ShopInsights.Money(t.Estate), LX, y, LW); y += 26f;
            InLine(ct, Loc.T("gps_ins_housing", "Housing costs"), ShopInsights.Money(-t.Housing), LX, y, LW, false, InSign(-t.Housing)); y += 26f;
            InLine(ct, Loc.T("gps_ins_loans", "Loans"), ShopInsights.Money(t.Loans), LX, y, LW, false, InSign(t.Loans)); y += 26f;
            InLine(ct, Loc.T("gps_ins_other", "Other (insurance, fees, salary)"), ShopInsights.Money(t.Other), LX, y, LW, false, InSign(t.Other)); y += 26f;
            InLine(ct, Loc.T("gps_ins_total", "Total"), ShopInsights.Money(t.Total), LX, y, LW, true, InSign(t.Total)); y += 26f;
            InLine(ct, Loc.T("gps_ins_per_day", "Per day"), ShopInsights.Money(t.Total / Math.Max(1, days)), LX, y, LW, false, InSign(t.Total)); y += 26f;
            InLine(ct, Loc.T("gps_ins_customers", "Customers"), t.Customers.ToString("N0"), LX, y, LW); y += 36f;
            if (rows.Count > 0)
            {
                InLine(ct, Loc.T("gps_ins_best", "Best business") + ": " + rows[0].Name, ShopInsights.Money(rows[0].Profit), LX, y, LW, false, InSign(rows[0].Profit)); y += 26f;
                if (rows.Count > 1) { var w = rows[rows.Count - 1]; InLine(ct, Loc.T("gps_ins_worst", "Weakest business") + ": " + w.Name, ShopInsights.Money(w.Profit), LX, y, LW, false, InSign(w.Profit)); y += 26f; }
            }
            int props = ShopInsights.Estate(new List<FinancialSummary>()).Count;
            var st = ShopInsights.Staff();
            y += 10f;
            LText(ct, Loc.T("gps_ins_portfolio", "{0} business(es), {1} employee(s), {2} building(s) owned", ShopInsights.Own().Count, st.total, props),
                12f, L_MUTED, LX, y, LW, 22f, TextAlignmentOptions.Left);
            if (sums.All(s => s?.businessIncomeStatements == null || s.businessIncomeStatements.Count == 0))
                LText(ct, Loc.T("gps_ins_no_data", "No data yet - the game writes a day's results at midnight."), 12f, L_NOTICE, LX, y + 26f, LW, 40f, TextAlignmentOptions.TopLeft, false, false, 0f, true);

            LText(ct, Loc.T("gps_ins_alerts", "Alerts (last 7 days)"), 15f, C_WHITE, RX, 146f, RW, 26f, TextAlignmentOptions.Left, true);
            var alerts = ShopInsights.Alerts();
            if (alerts.Count == 0) LText(ct, Loc.T("gps_ins_no_alerts", "No alerts."), 13f, L_MUTED, RX, 178f, RW, 24f, TextAlignmentOptions.Left);
            for (int i = 0; i < Math.Min(alerts.Count, 16); i++)
            {
                var a = LText(ct, "• " + alerts[i], 12f, L_NOTICE, RX, 178f + i * 24f, RW, 24f, TextAlignmentOptions.Left);
                a.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        /// <summary>A table: header, rows (cells per column), optional totals row, pager. Columns: (header, width, right-aligned).</summary>
        private void InTable(Transform ct, float x, float y, (string h, float w, bool right)[] cols, List<string[]> rows, List<Color[]>? colors, string[]? totals, int perPage, float H)
        {
            float cx = x;
            foreach (var c in cols)
            {
                LText(ct, c.h, 11f, L_MUTED, cx + 4f, y, c.w - 8f, 22f, c.right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left, true, true, 1f);
                cx += c.w;
            }
            y += 26f;
            int pages = Math.Max(1, (rows.Count + perPage - 1) / perPage);
            _inPage = Mathf.Clamp(_inPage, 0, pages - 1);
            float rowW = cols.Sum(c => c.w);
            for (int r = 0; r < perPage; r++)
            {
                int i = _inPage * perPage + r;
                if (i >= rows.Count) break;
                var bg = LImg(ct, "Row", r % 2 == 0 ? L_ROW : L_SECT, LRoundSprite(false), 3f);
                LPos(bg.rectTransform, x, y + r * 27f, rowW, 25f);
                cx = x;
                for (int c = 0; c < cols.Length; c++)
                {
                    var t = LText(ct, rows[i][c], 12f, colors?[i][c] ?? C_WHITE, cx + 6f, y + r * 27f, cols[c].w - 12f, 25f, cols[c].right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
                    t.overflowMode = TextOverflowModes.Ellipsis;
                    cx += cols[c].w;
                }
            }
            float ty = y + perPage * 27f + 4f;
            if (totals != null)
            {
                cx = x;
                for (int c = 0; c < cols.Length; c++)
                {
                    LText(ct, totals[c], 12f, C_WHITE, cx + 6f, ty, cols[c].w - 12f, 25f, cols[c].right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left, true);
                    cx += cols[c].w;
                }
                ty += 30f;
            }
            if (pages > 1) SgPagerTo(_inHits, ct, x, ty, 300f, _inPage, pages, 3, 4);
        }

        private void InProfit(Transform ct, float W, float H)
        {
            int days = Math.Min(_inPeriod, ShopInsights.MaxDays);
            var rows = ShopInsights.Businesses(ShopInsights.Last(days));
            if (rows.Count == 0) { LText(ct, Loc.T("gps_ins_no_data", "No data yet - the game writes a day's results at midnight."), 13f, L_MUTED, 26f, 146f, W - 52f, 24f, TextAlignmentOptions.Left); return; }
            var cols = new (string, float, bool)[]
            {
                (Loc.T("gps_ins_col_business", "Business"), 252f, false), (Loc.T("gps_ins_col_sales", "Sales"), 96f, true),
                (Loc.T("gps_ins_col_goods", "Goods"), 96f, true), (Loc.T("gps_ins_col_wages", "Wages"), 96f, true),
                (Loc.T("gps_ins_col_rent", "Rent"), 96f, true), (Loc.T("gps_ins_col_marketing", "Marketing"), 96f, true),
                (Loc.T("gps_ins_col_other", "Other"), 96f, true), (Loc.T("gps_ins_col_profit", "Profit"), 96f, true),
                (Loc.T("gps_ins_col_margin", "Margin"), 96f, true),
            };
            var cells = new List<string[]>();
            var colors = new List<Color[]>();
            var tot = new ShopInsights.BizRow();
            foreach (var r in rows)
            {
                cells.Add(new[] { r.Name, ShopInsights.Money(r.Sales), ShopInsights.Money(r.Goods), ShopInsights.Money(r.Wages), ShopInsights.Money(r.Rent),
                                  ShopInsights.Money(r.Marketing), ShopInsights.Money(r.Other), ShopInsights.Money(r.Profit), r.Sales > 0f ? $"{r.Profit * 100f / r.Sales:0}%" : "-" });
                var c = Enumerable.Repeat(C_WHITE, 9).ToArray(); c[7] = InSign(r.Profit); c[8] = InSign(r.Profit);
                colors.Add(c);
                tot.Sales += r.Sales; tot.Goods += r.Goods; tot.Wages += r.Wages; tot.Rent += r.Rent; tot.Marketing += r.Marketing; tot.Other += r.Other; tot.Profit += r.Profit;
            }
            var totals = new[] { Loc.T("gps_ins_total", "Total"), ShopInsights.Money(tot.Sales), ShopInsights.Money(tot.Goods), ShopInsights.Money(tot.Wages), ShopInsights.Money(tot.Rent),
                                 ShopInsights.Money(tot.Marketing), ShopInsights.Money(tot.Other), ShopInsights.Money(tot.Profit), tot.Sales > 0f ? $"{tot.Profit * 100f / tot.Sales:0}%" : "-" };
            InTable(ct, 26f, 146f, cols, cells, colors, totals, 14, H);
        }

        private void InHistory(Transform ct, float W, float H)
        {
            var own = ShopInsights.Own();
            const float LX = 26f, LW = 300f, RX = 346f;
            float RW = W - RX - 26f;
            const int ROWS = 13;
            int pages = Math.Max(1, (own.Count + 1 + ROWS - 1) / ROWS);
            _inBizPage = Mathf.Clamp(_inBizPage, 0, pages - 1);
            for (int r = 0; r < ROWS; r++)
            {
                int i = _inBizPage * ROWS + r - 1;   // -1 = all businesses
                if (i >= own.Count) break;
                string key = i < 0 ? "" : ShopInsights.Key(own[i].Address);
                string name = i < 0 ? Loc.T("gps_ins_all_businesses", "All businesses") : own[i].BusinessName;
                var bg = LImg(ct, "Row", key == _inHistKey ? L_BTN[1] : L_ROW, LRoundSprite(false), 3f);
                LPos(bg.rectTransform, LX, 104f + r * 34f, LW, 30f);
                var t = LText(bg.transform, name, 13f, C_WHITE, 10f, 0f, LW - 20f, 30f, TextAlignmentOptions.Left);
                t.overflowMode = TextOverflowModes.Ellipsis;
                _inHits.Add((bg.rectTransform, 5, i));
            }
            if (pages > 1) SgPagerTo(_inHits, ct, LX, 104f + ROWS * 34f + 4f, LW, _inBizPage, pages, 7, 8);

            int[] ranges = { 7, 14, 30, 60 };
            for (int i = 0; i < ranges.Length; i++)
            {
                if (ranges[i] > ShopInsights.MaxDays && i > 0) break;
                var b = LButton(ct, "Days" + i, Loc.T("gps_ins_h_days", "{0} days", ranges[i]), RX + i * 128f, 104f, 120f, 30f, _inHistDays == ranges[i] ? 1 : 0, 11f);
                _inHits.Add((b.rt, 6, ranges[i]));
            }
            var hist = ShopInsights.History(Math.Min(_inHistDays, ShopInsights.MaxDays), _inHistKey);
            const float CY = 150f, CH = 300f;
            var frame = LImg(ct, "Chart", L_SECT, LRoundSprite(false), 4f).rectTransform;
            LPos(frame, RX, CY, RW, CH);
            if (hist.Count == 0 || hist.All(h => !h.known))
            {
                LText(ct, Loc.T("gps_ins_no_data", "No data yet - the game writes a day's results at midnight."), 13f, L_MUTED, RX + 16f, CY + 16f, RW - 32f, 40f, TextAlignmentOptions.TopLeft, false, false, 0f, true);
                return;
            }
            float max = Mathf.Max(0f, hist.Max(h => h.profit)), min = Mathf.Min(0f, hist.Min(h => h.profit));
            float span = Mathf.Max(1f, max - min);
            float zeroY = CY + 10f + (CH - 20f) * (max / span);
            var zero = LImg(ct, "Zero", new Color(1f, 1f, 1f, 0.35f), null, 0f).rectTransform;
            LPos(zero, RX + 8f, zeroY, RW - 16f, 1f);
            float bw = (RW - 16f) / hist.Count;
            var green = new Color(0.45f, 0.85f, 0.45f, 1f);
            for (int i = 0; i < hist.Count; i++)
            {
                float v = hist[i].profit;
                float h = (CH - 20f) * Mathf.Abs(v) / span;
                if (h < 1f) continue;
                var bar = LImg(ct, "Bar", v >= 0f ? green : L_ERR, null, 0f).rectTransform;
                LPos(bar, RX + 8f + i * bw + 1f, v >= 0f ? zeroY - h : zeroY, Mathf.Max(2f, bw - 2f), h);
            }
            LText(ct, ShopInsights.Money(max), 10f, L_MUTED, RX + 12f, CY + 4f, 200f, 16f, TextAlignmentOptions.Left);
            if (min < 0f) LText(ct, ShopInsights.Money(min), 10f, L_MUTED, RX + 12f, CY + CH - 20f, 200f, 16f, TextAlignmentOptions.Left);
            LText(ct, Loc.T("gps_ins_h_day", "Day {0}", hist[0].day), 10f, L_MUTED, RX, CY + CH + 2f, 200f, 16f, TextAlignmentOptions.Left);
            LText(ct, Loc.T("gps_ins_h_day", "Day {0}", hist[hist.Count - 1].day), 10f, L_MUTED, RX + RW - 200f, CY + CH + 2f, 200f, 16f, TextAlignmentOptions.Right);

            var known = hist.Where(h => h.known).ToList();
            float sum = known.Sum(h => h.profit);
            var best = known.OrderByDescending(h => h.profit).First();
            var worst = known.OrderBy(h => h.profit).First();
            float y = CY + CH + 28f;
            InLine(ct, Loc.T("gps_ins_h_sum", "Total"), ShopInsights.Money(sum), RX, y, 480f, true, InSign(sum)); y += 26f;
            InLine(ct, Loc.T("gps_ins_per_day", "Per day"), ShopInsights.Money(sum / Math.Max(1, known.Count)), RX, y, 480f, false, InSign(sum)); y += 26f;
            InLine(ct, Loc.T("gps_ins_h_best", "Best day (day {0})", best.day), ShopInsights.Money(best.profit), RX, y, 480f, false, InSign(best.profit)); y += 26f;
            InLine(ct, Loc.T("gps_ins_h_worst", "Worst day (day {0})", worst.day), ShopInsights.Money(worst.profit), RX, y, 480f, false, InSign(worst.profit)); y += 26f;
            InLine(ct, Loc.T("gps_ins_h_profitable", "Days with profit"), $"{known.Count(h => h.profit > 0f)} / {known.Count}", RX, y, 480f);
        }

        private void InStaff(Transform ct, float W, float H)
        {
            var st = ShopInsights.Staff();
            if (st.rows.Count == 0) { LText(ct, Loc.T("gps_ins_no_staff", "You have no employees yet."), 13f, L_MUTED, 26f, 104f, W - 52f, 24f, TextAlignmentOptions.Left); return; }
            var cols = new (string, float, bool)[]
            {
                (Loc.T("gps_ins_col_business", "Business"), 400f, false), (Loc.T("gps_ins_col_employees", "Employees"), 150f, true),
                (Loc.T("gps_ins_col_wages_week", "Wages a week"), 170f, true), (Loc.T("gps_ins_col_satisfaction", "Satisfaction"), 150f, true),
                (Loc.T("gps_ins_col_unhappy", "Unhappy"), 150f, true),
            };
            var cells = new List<string[]>();
            var colors = new List<Color[]>();
            int n = 0, unhappy = 0; float wages = 0f, sat = 0f;
            foreach (var r in st.rows)
            {
                cells.Add(new[] { r.Name, r.Count.ToString(), ShopInsights.Money(r.WagesWeek), $"{r.AvgSat:0}%", r.Unhappy.ToString() });
                colors.Add(new[] { C_WHITE, C_WHITE, C_WHITE, r.AvgSat < 40f ? L_ERR : C_WHITE, r.Unhappy > 0 ? L_NOTICE : C_WHITE });
                n += r.Count; unhappy += r.Unhappy; wages += r.WagesWeek; sat += r.AvgSat * r.Count;
            }
            var totals = new[] { Loc.T("gps_ins_total", "Total"), n.ToString(), ShopInsights.Money(wages), $"{sat / Math.Max(1, n):0}%", unhappy.ToString() };
            InTable(ct, 26f, 104f, cols, cells, colors, totals, 10, H);
            float y = 104f + 26f + 10 * 27f + 4f + 30f + 40f;
            LText(ct, Loc.T("gps_ins_highest_paid", "Highest paid"), 14f, C_WHITE, 26f, y, 600f, 24f, TextAlignmentOptions.Left, true);
            for (int i = 0; i < st.top.Count; i++)
                LText(ct, Loc.T("gps_ins_paid_line", "{0} ({1}): {2} an hour", st.top[i].Name, st.top[i].Biz, ShopInsights.Money(st.top[i].Wage)), 12f, L_MUTED, 26f, y + 26f + i * 22f, W - 260f, 22f, TextAlignmentOptions.Left);
        }

        private void InSales(Transform ct, float W, float H)
        {
            int days = Math.Min(_inPeriod, ShopInsights.MaxDays);
            var items = ShopInsights.Items(days);
            if (items.Count == 0) { LText(ct, Loc.T("gps_ins_no_data", "No data yet - the game writes a day's results at midnight."), 13f, L_MUTED, 26f, 146f, W - 52f, 24f, TextAlignmentOptions.Left); return; }
            float total = items.Sum(i => i.Revenue);
            var cols = new (string, float, bool)[]
            {
                (Loc.T("gps_ins_col_product", "Product"), 420f, false), (Loc.T("gps_ins_col_units", "Units"), 150f, true),
                (Loc.T("gps_ins_col_revenue", "Revenue"), 150f, true), (Loc.T("gps_ins_col_margin_money", "Gross margin"), 150f, true),
                (Loc.T("gps_ins_col_share", "Share"), 150f, true),
            };
            var cells = new List<string[]>();
            foreach (var it in items)
                cells.Add(new[] { it.Item, it.Units.ToString("N0"), ShopInsights.Money(it.Revenue), ShopInsights.Money(it.Revenue - it.Cost), total > 0f ? $"{it.Revenue * 100f / total:0.0}%" : "-" });
            var totals = new[] { Loc.T("gps_ins_total", "Total"), items.Sum(i => i.Units).ToString("N0"), ShopInsights.Money(total), ShopInsights.Money(total - items.Sum(i => i.Cost)), "100%" };
            InTable(ct, 26f, 146f, cols, cells, null, totals, 14, H);
        }

        /// <summary>SERVER-RULES-1 / RuleBusinessSale: the businesses, what a sale brings and whether it is allowed yet.</summary>
        private void InSell(Transform ct, float W, float H)
        {
            LText(ct, Loc.T("gps_ins_sell_rules", "A business can be sold after {0} days and {1} days with profit. Ending the lease returns the furniture, vehicles and deposit as usual; on top you get a goodwill of 30 days' average profit.",
                ServerRules.SaleMinDays, ServerRules.SaleProfitDays), 12f, L_MUTED, 26f, 100f, W - 52f, 40f, TextAlignmentOptions.TopLeft, false, false, 0f, true);
            var offers = BusinessSale.Offers();
            if (offers.Count == 0) { LText(ct, Loc.T("gps_ins_no_sellable", "You have no business that could be sold."), 13f, L_MUTED, 26f, 150f, W - 52f, 24f, TextAlignmentOptions.Left); return; }
            var cols = new (string h, float w, bool right)[]
            {
                (Loc.T("gps_ins_col_business", "Business"), 236f, false), (Loc.T("gps_ins_col_age", "Age (days)"), 96f, true),
                (Loc.T("gps_ins_col_profit_days", "Days with profit"), 120f, true), (Loc.T("gps_ins_col_avg_profit", "Profit a day"), 110f, true),
                (Loc.T("gps_ins_col_closure", "Furniture + deposit"), 150f, true), (Loc.T("gps_ins_col_goodwill", "Goodwill"), 110f, true),
                (Loc.T("gps_ins_col_you_get", "You get"), 110f, true),
            };
            float x = 26f, y = 148f, cx = x;
            foreach (var c in cols)
            {
                LText(ct, c.h, 11f, L_MUTED, cx + 4f, y, c.w - 8f, 22f, c.right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left, true, true, 1f);
                cx += c.w;
            }
            y += 26f;
            const int PER = 13;
            int pages = Math.Max(1, (offers.Count + PER - 1) / PER);
            _inPage = Mathf.Clamp(_inPage, 0, pages - 1);
            float rowW = cols.Sum(c => c.w);
            for (int r = 0; r < PER; r++)
            {
                int i = _inPage * PER + r;
                if (i >= offers.Count) break;
                var o = offers[i];
                var bg = LImg(ct, "Row", r % 2 == 0 ? L_ROW : L_SECT, LRoundSprite(false), 3f);
                LPos(bg.rectTransform, x, y + r * 30f, rowW, 27f);
                string[] cells =
                {
                    o.Name, o.Age.ToString(), $"{o.ProfitDays} / {ServerRules.SaleProfitDays}", ShopInsights.Money(o.AvgProfit),
                    ShopInsights.Money(o.Closure), ShopInsights.Money(o.Goodwill), ShopInsights.Money(o.Closure + o.Goodwill),
                };
                cx = x;
                for (int c = 0; c < cols.Length; c++)
                {
                    Color col = C_WHITE;
                    if (c == 1 && o.Age < ServerRules.SaleMinDays) col = L_NOTICE;
                    if (c == 2 && o.ProfitDays < ServerRules.SaleProfitDays) col = L_NOTICE;
                    if (c == 3) col = o.AvgProfit < 0f ? L_ERR : C_WHITE;
                    var t = LText(ct, cells[c], 12f, col, cx + 6f, y + r * 30f, cols[c].w - 12f, 27f, cols[c].right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
                    t.overflowMode = TextOverflowModes.Ellipsis;
                    cx += cols[c].w;
                }
                bool can = o.Eligible && !BusinessSale.IsPending(o.Key);
                var sb = LButton(ct, "Sell" + i, Loc.T("gps_ins_sell", "Sell"), x + rowW + 12f, y + r * 30f, W - 26f - (x + rowW + 12f), 27f, can ? 3 : 4, 11f);
                if (can) _inHits.Add((sb.rt, 10, i));
            }
            float py = y + PER * 30f + 6f;
            if (pages > 1) SgPagerTo(_inHits, ct, x, py, 300f, _inPage, pages, 3, 4);
            if (_inStatus.Length > 0)
                LText(ct, _inStatus, 12f, L_NOTICE, 26f, H - 104f, W - 220f, 40f, TextAlignmentOptions.TopLeft, false, false, 0f, true);
        }

        private void InEstate(Transform ct, float W, float H)
        {
            int days = Math.Min(_inPeriod, ShopInsights.MaxDays);
            var rows = ShopInsights.Estate(ShopInsights.Last(days));
            if (rows.Count == 0) { LText(ct, Loc.T("gps_ins_no_estate", "You own no buildings yet."), 13f, L_MUTED, 26f, 146f, W - 52f, 24f, TextAlignmentOptions.Left); return; }
            var cols = new (string, float, bool)[]
            {
                (Loc.T("gps_ins_col_property", "Building"), 330f, false), (Loc.T("gps_ins_col_bought", "Bought for"), 120f, true),
                (Loc.T("gps_ins_col_owned", "Days owned"), 110f, true), (Loc.T("gps_ins_col_let", "Let"), 100f, true),
                (Loc.T("gps_ins_col_per_day", "Income a day"), 120f, true), (Loc.T("gps_ins_col_period", "Income"), 120f, true),
                (Loc.T("gps_ins_col_payback", "Pays back in"), 128f, true),
            };
            var cells = new List<string[]>();
            var colors = new List<Color[]>();
            double bought = 0; float perDay = 0f, period = 0f;
            foreach (var r in rows)
            {
                cells.Add(new[] { r.Name, ShopInsights.Money((float)r.Bought), r.DaysOwned.ToString(), $"{r.Let}%", ShopInsights.Money(r.PerDay), ShopInsights.Money(r.Period),
                                  r.Payback > 0 ? Loc.T("gps_ins_payback_days", "{0} days", r.Payback) : "-" });
                colors.Add(new[] { C_WHITE, C_WHITE, C_WHITE, r.Let < 50 ? L_NOTICE : C_WHITE, C_WHITE, C_WHITE, C_WHITE });
                bought += r.Bought; perDay += r.PerDay; period += r.Period;
            }
            var totals = new[] { Loc.T("gps_ins_total", "Total"), ShopInsights.Money((float)bought), "", "", ShopInsights.Money(perDay), ShopInsights.Money(period),
                                 perDay > 0f ? Loc.T("gps_ins_payback_days", "{0} days", (int)Math.Ceiling(bought / perDay)) : "-" };
            InTable(ct, 26f, 146f, cols, cells, colors, totals, 14, H);
        }
    }
}
