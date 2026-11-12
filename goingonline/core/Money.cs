using System.Collections.Generic;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>Money through the game's own bank (GameManager.ChangeMoneySafe): each kind of income and cost is a
    /// transaction type of its own, "goingonline:transaction_*", named in the mod's Locales like the game's
    /// ("{businessName}: subscriptions"), so the finance app (EconoView) lists and filters them. Not tax-deductible: the
    /// provider pays its own levy (Sim.EndOfDay).</summary>
    internal static class Money
    {
        public const string TSubs = "goingonline:transaction_subs", TRent = "goingonline:transaction_rent", TSold = "goingonline:transaction_sold",
                            TPayTv = "goingonline:transaction_paytv", TTransit = "goingonline:transaction_transit",
                            TPayTvBuy = "goingonline:transaction_paytvbuy", TWages = "goingonline:transaction_wages",
                            TComp = "goingonline:transaction_comp", TUpkeep = "goingonline:transaction_upkeep",
                            TShip = "goingonline:transaction_ship", TTax = "goingonline:transaction_tax",
                            TBuild = "goingonline:transaction_build", TGear = "goingonline:transaction_gear",
                            TPhone = "goingonline:transaction_phone", TFleet = "goingonline:transaction_fleet",
                            TContractors = "goingonline:transaction_contractors", TLease = "goingonline:transaction_lease",
                            TMaint = "goingonline:transaction_maint", TRepair = "goingonline:transaction_repair",
                            TPermits = "goingonline:transaction_permits", TFees = "goingonline:transaction_fees",
                            TMoveFees = "goingonline:transaction_movefees", TServiceFees = "goingonline:transaction_servicefees",
                            TPower = "goingonline:transaction_power", TMonitor = "goingonline:transaction_monitor",
                            TWholesale = "goingonline:transaction_wholesale", TBulk = "goingonline:transaction_bulk",
                            TEvents = "goingonline:transaction_events", TEventsIn = "goingonline:transaction_eventsin",   // GO-EVENTS-1
                            TPeerSubs = "goingonline:transaction_peersubs", TPeerRent = "goingonline:transaction_peerrent",
                            TPeerIn = "goingonline:transaction_peerin";   // GO-WHOLESALE-2

        // the self-test plays with its own cash (start money and founder loan), so it shows whether the start works
        internal static bool TestBudget;
        internal static float TestCash, TestLowest;
        internal static int TestLowestDay;

        public static float Cash { get { try { return SaveGameManager.Current?.Money ?? 0f; } catch { return 0f; } } }

        public static bool CanPay(float amount) => (Sim.Test && TestBudget ? TestCash : Cash) >= amount;

        static TransactionInfo Info(IspState s, string type) =>
            new TransactionInfo(type, new Dictionary<string, string> { { "businessName", s.Company.Length > 0 ? s.Company : "Going Online" } });

        /// <summary>An investment, paid at once: refused (with the game's "not enough money" note) when the money is
        /// not there.</summary>
        public static bool Invest(IspState s, float amount, bool devices = false)
        {
            if (amount <= 0f) return true;
            if (Sim.Test)
            {
                if (TestBudget)
                {
                    if (TestCash < amount) return false;
                    TestCash -= amount;
                }
                if (devices) s.Today.Gear += amount; else s.Today.Build += amount;
                return true;
            }
            try
            {
                if (!GameManager.ChangeMoneySafe(-amount, Info(s, devices ? TGear : TBuild), null, null, false, true)) return false;
                if (devices) s.Today.Gear += amount; else s.Today.Build += amount;
                return true;
            }
            catch (System.Exception ex) { Go.Warn("invest: " + ex.Message); return false; }
        }

        /// <summary>The day's bill: income in, running costs out (also into the red, as the game's rent).</summary>
        public static void Day(IspState s, DayBook b)
        {
            if (Sim.Test)
            {
                if (!TestBudget) return;
                TestCash += b.Operating - b.Tax;
                if (TestCash < TestLowest) { TestLowest = TestCash; TestLowestDay = b.Day; }
                return;
            }
            // GAME-TYPES-1: subscriptions, Pay-TV and device rent are the "Cable provider" business's own sales - the game
            // writes them into its daily report (BizMan, EconoView) and pays them at midnight; else as before
            if (!Sales(s, b))
            {
                Book(s, TSubs, b.Subs);
                Book(s, TRent, b.Rent);
                Book(s, TPayTv, b.PayTvIn);
            }
            Book(s, TSold, b.Sold);
            Book(s, TFees, b.Fees);
            Book(s, TMoveFees, b.MoveFees);
            Book(s, TServiceFees, b.ServiceFees);
            Book(s, TTransit, -b.Transit);
            Book(s, TPayTvBuy, -b.PayTvOut);
            Book(s, TWages, -b.Wages);
            Book(s, TComp, -b.Comp);
            Book(s, TUpkeep, -b.Upkeep);
            Book(s, TShip, -b.Ship);
            Book(s, TPhone, -b.Phone);
            Book(s, TFleet, -b.Fleet);
            Book(s, TContractors, -b.Contractors);
            Book(s, TLease, -b.Lease);
            Book(s, TMaint, -b.Maint);
            Book(s, TRepair, -b.Repair);
            Book(s, TPermits, -b.Permits);
            Book(s, TPower, -b.Power);
            Book(s, TMonitor, -b.Monitor);
            Book(s, TWholesale, b.Wholesale);
            Book(s, TBulk, b.Bulk);
            Book(s, TEvents, -b.Events);
            Book(s, TEventsIn, b.EventsIn);
            Book(s, TPeerSubs, b.PeerSubs);
            Book(s, TPeerRent, -b.PeerRent);
            Book(s, TPeerIn, b.PeerIn);
            Book(s, TTax, -b.Tax);
        }

        /// <summary>The day's subscriptions, Pay-TV packages and device rent as completed orders of the headend's "Cable
        /// provider" business (GameTypes: service lines "Cable connection", "Pay-TV package", "Device rent"): the game's own
        /// daily processing (BusinessHelper.ProcessDailyOrders) turns them into the business's report and pays them as its
        /// revenue. One order per customer (at most 3,000; above that the price per entry carries the rest). False when the
        /// headend is not such a business (then the provider books them itself).</summary>
        static bool Sales(IspState s, DayBook b)
        {
            try
            {
                var reg = City.Reg(s.HeadendKey);
                if (reg == null || !GameTypes.IsOurs(reg.businessTypeName) || reg.unprocessedCompletedOrders == null) return false;
                if (reg.cachedAvailableProducts == null) reg.cachedAvailableProducts = new List<string>();
                int cust = Mathf.Max(0, Sim.Customers(s)), paytv = 0;
                foreach (var p in s.PayTv) paytv += p.Subs;
                int rented = Sim.Devices(s, Cat.Dev30, Cat.OwnRent) + Sim.Devices(s, Cat.Dev31, Cat.OwnRent) + Sim.Devices(s, Cat.DevOnt, Cat.OwnRent);
                var lines = new List<(string item, int n, float total)>();
                if (b.Subs > 0.005f) lines.Add((GameTypes.IInternet, Mathf.Max(1, cust), b.Subs));
                if (b.PayTvIn > 0.005f) lines.Add((GameTypes.ITv, Mathf.Max(1, paytv), b.PayTvIn));
                if (b.Rent > 0.005f) lines.Add((GameTypes.IRent, Mathf.Max(1, rented), b.Rent));
                if (lines.Count == 0) return true;
                const int Cap = 3000;
                int orders = 0;
                foreach (var l in lines) orders = Mathf.Max(orders, Mathf.Min(l.n, Cap));
                var list = new List<Order>(orders);
                for (int i = 0; i < orders; i++) list.Add(new Order { completed = true });
                foreach (var (item, n, total) in lines)
                {
                    int entries = Mathf.Min(n, Cap);
                    float each = total / entries;
                    for (int i = 0; i < entries; i++)
                        list[i].entries.Add(new Entities.OrderEntry { itemName = item, price = each, available = true, priceAccceptable = true, paid = true });
                }
                reg.unprocessedCompletedOrders.AddRange(list);
                return true;
            }
            catch (System.Exception ex) { Go.Warn("sales: " + ex.Message); return false; }
        }

        static void Book(IspState s, string type, float amount)
        {
            if (amount > -0.005f && amount < 0.005f) return;
            amount = (float)System.Math.Round(amount, 2);
            try { GameManager.ChangeMoneySafe(amount, Info(s, type), null, City.Reg(s.HeadendKey)?.Address, true, false); }   // the headend's business
            catch (System.Exception ex) { Go.Warn($"book {type}: {ex.Message}"); }
        }
    }

    /// <summary>The game's notifications at the top right; a click opens the provider's page.</summary>
    internal static class Notify
    {
        public static void Good(string text, float seconds) => Show(UI.Notification.NotificationType.Success, text, seconds, false);
        public static void Warn(string text, float seconds) => Show(UI.Notification.NotificationType.Warning, text, seconds, true);
        public static void Bad(string text, float seconds) => Show(UI.Notification.NotificationType.Error, text, seconds, true);
        public static void Info(string text, float seconds) => Show(UI.Notification.NotificationType.Info, text, seconds, false);

        static void Show(UI.Notification.NotificationType type, string text, float seconds, bool sound)
        {
            if (Sim.Test) { Go.Log("note: " + text); return; }
            try { UI.Notification.Notifications.Show(type, text, null, seconds, null, () => App.Open(-1), sound, false); }
            catch (System.Exception ex) { Go.Warn("notify: " + ex.Message); }
        }
    }
}
