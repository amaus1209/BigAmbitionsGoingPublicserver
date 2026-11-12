using System;
using System.Collections.Generic;
using Buildings;
using Entities;
using Helpers;
using Streets;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>DISSOLVE-SALE-1 (1.0.16.1, user rulings 2026-11-01: "on a de-merger every building and shop is sold, then the
    /// money is halved" - shops with goodwill, bought buildings at market value, every employee let go, private cars and
    /// homes sold too - and "both players have to agree"). When every member voted (MPServer DISSOLVE-VOTE-1) the host asks
    /// each member's machine to sell what THAT member owns - it lives in his own save - while the company still exists,
    /// so every payment flows into the shared wallet through MergerWallet's money forward; the host splits the wallet
    /// equally once every machine reported.
    /// On this machine, in this order: the player leaves an own building / gets out of an own car first (the game refuses
    /// a lease end with the player inside, and a car is never deleted under him); every own employee is let go (the
    /// MyEmployees fire: EmployeeInstance.RemoveEmployee); every own lease ends - shops, warehouses, headquarters, homes -
    /// with the game's own lease-end steps (GameStatePatcher.OwnerEndLeaseNoPayout, the ban forfeit's) and is paid what
    /// the game's terminate pays (furniture, the vehicles at the address, the deposit) plus a shop's goodwill (30 days'
    /// average profit, the business sale's arithmetic); every bought building is given up and paid at its market value;
    /// every remaining own vehicle is sold at the game's selling price. Each lease end and building sale is reported the
    /// way the mod reports a native one (vacate / sale completed). A merger partner's shops shown as mine here
    /// (MergerFlip) are never touched: his own machine sells them.</summary>
    internal static class MergerDissolveSale
    {
        private static MergerDissolveSalePayload? _job;
        private static float _leaveBy;
        private static bool _exitAsked, _carExitAsked;

        /// <summary>MAIN THREAD: the host's "sell now" (a repeat of the same sale is ignored).</summary>
        internal static void Begin(MergerDissolveSalePayload? p)
        {
            if (p == null || string.IsNullOrEmpty(p.SaleId) || SaveGameManager.Current == null) return;
            if (_job != null && _job.SaleId == p.SaleId) return;
            _job = p;
            _leaveBy = Time.unscaledTime + 20f;
            _exitAsked = _carExitAsked = false;
            Plugin.Logger.LogInfo($"[Dissolve] sale {p.SaleId}: every member agreed - selling what this player owns.");
            Notify(Loc.T("gps_dissolve_start", "The company is dissolved: everything you own is sold, then the money is shared."));
        }

        /// <summary>MAIN THREAD, every frame in a session (MPCanvasUI).</summary>
        internal static void Tick()
        {
            if (MPServer.IsRunning) { try { MPServer.TickDissolveSaleHost(); } catch (Exception ex) { Plugin.Logger.LogWarning($"[Dissolve] host tick: {ex.Message}"); } }
            var job = _job;
            if (job == null) return;
            try
            {
                if (MustLeaveFirst() && Time.unscaledTime < _leaveBy) return;   // walked out / got out first; after 20 s it sells what it can
                Sell(job);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Dissolve] sale {job.SaleId}: {ex.Message}"); Report(job, 0f, 0, 0, 0, 0); }
            _job = null;
        }

        /// <summary>True while the player is still inside an own building or in an own car (the game's own exit is asked once).</summary>
        private static bool MustLeaveFirst()
        {
            try
            {
                if (BuildingManager.IsInsideBuilding)
                {
                    var reg = InstanceBehavior<BuildingManager>.Instance?.buildingRegistration;
                    if (reg != null && IsMine(reg))
                    {
                        if (!_exitAsked) { _exitAsked = true; InstanceBehavior<BuildingManager>.Instance!.ExitFromBuilding(0); Plugin.Logger.LogInfo("[Dissolve] the player is inside an own building - walked out with the game's own exit first."); }
                        return true;
                    }
                }
                var cur = VehicleHelper.GetCurrentVehicle();
                if (cur != null && IsOwnVehicle(cur))
                {
                    if (!_carExitAsked) { _carExitAsked = true; VehicleHelper.GetCurrentVehicleBase()?.ExitVehicle(); Plugin.Logger.LogInfo("[Dissolve] the player sits in an own vehicle - the game's own get-out first."); }
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool IsMine(BuildingRegistration reg)
        {
            try { return (reg.RentedByPlayer && MergerFlip.TrulyMine(reg)) || reg.BuildingOwnedByPlayer; } catch { return false; }
        }

        private static bool IsOwnVehicle(VehicleInstance v)
        {
            var list = SaveGameManager.Current?.VehicleInstances;
            return list != null && list.Contains(v);
        }

        private static void Sell(MergerDissolveSalePayload job)
        {
            var gi = SaveGameManager.Current;
            float before = 0f; try { before = gi.Money; } catch { }
            float total = 0f;
            int staff = 0, leases = 0, deeds = 0, cars = 0;

            // the goodwill of every own shop, before anything ends (the business sale's own arithmetic)
            var goodwill = new Dictionary<BuildingRegistration, float>();
            try { foreach (var o in BusinessSale.Offers()) if (o.Goodwill > 0f && MergerFlip.TrulyMine(o.Reg)) goodwill[o.Reg] = o.Goodwill; }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Dissolve] goodwill: {ex.Message}"); }

            // 1. every own employee goes (candidates, other players' copies and stand-ins are not this player's staff)
            foreach (var e in new List<EmployeeInstance>(gi.EmployeeInstances))
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                if (MPRegisterSync.IsInjectedStaff(e.id) || MPRegisterSync.IsSyntheticDuty(e.id)) continue;
                bool candidate = false; try { candidate = e.IsCandidate; } catch { }
                if (candidate) continue;
                try { e.RemoveEmployee(); staff++; }
                catch (Exception ex) { Plugin.Logger.LogWarning($"[Dissolve] let go '{e.id}': {ex.Message}"); }
            }
            if (staff > 0) { try { GameEvent.Invoke(string.Empty); } catch { } }

            // 2. every own lease ends - shops, warehouses, headquarters, homes - and is paid what the game's terminate pays
            foreach (var reg in new List<BuildingRegistration>(gi.BuildingRegistrations))
            {
                if (reg == null) continue;
                bool mine = false; try { mine = reg.RentedByPlayer && MergerFlip.TrulyMine(reg); } catch { }
                if (!mine) continue;   // not rented, or a partner's shop shown as mine - his machine sells it
                string addr = GameStateReader.AddressKey(reg);
                try
                {
                    float refund = Contents(reg) + Math.Max(0f, reg.lastDeposit);
                    goodwill.TryGetValue(reg, out var gw);
                    GameStatePatcher.OwnerEndLeaseNoPayout(reg, addr, out _, out _);
                    ReportVacate(addr);
                    Pay(refund, "ba:transaction_depositreturnfurniture", reg.Address);
                    if (gw > 0f) Pay(gw, "ba:transaction_businessinventorysold", reg.Address);
                    total += refund + gw;
                    leases++;
                    Plugin.Logger.LogInfo($"[Dissolve] lease '{addr}' ended: ${refund:N0} furniture/vehicles/deposit{(gw > 0f ? $" + ${gw:N0} goodwill" : "")}.");
                }
                catch (Exception ex) { Plugin.Logger.LogWarning($"[Dissolve] lease '{addr}': {ex.Message}"); }
            }

            // 3. every bought building is given up at its market value (a Hamptons house with its furniture and cars, as the
            //    game's own AI purchase pays for it)
            foreach (var re in new List<RealEstate>(gi.realEstate))
            {
                if (re == null) continue;
                BuildingRegistration? reg = null;
                try { reg = BuildingHelper.GetBuildingRegistration(re.address); } catch { }
                if (reg == null) continue;
                string addr = GameStateReader.AddressKey(reg);
                try
                {
                    bool hamptons = false; try { hamptons = reg.BuildingCached != null && reg.BuildingCached.IsHamptonsHouse(); } catch { }
                    float value = 0f; try { value = reg.BuildingCached != null ? reg.BuildingCached.GetMarketValue() : 0f; } catch { }
                    float contents = hamptons ? Contents(reg) : 0f;
                    GameStatePatcher.OwnerGiveUpBuilding(reg, addr, hamptons, out _, out _);
                    ReportSold(addr);
                    Pay(value + contents, "ba:transaction_buildingsold", reg.Address);
                    total += value + contents;
                    deeds++;
                    Plugin.Logger.LogInfo($"[Dissolve] building '{addr}' sold: ${value:N0} market value{(contents > 0f ? $" + ${contents:N0} contents" : "")}.");
                }
                catch (Exception ex) { Plugin.Logger.LogWarning($"[Dissolve] building '{addr}': {ex.Message}"); }
            }

            // 4. every remaining own vehicle is sold at the game's selling price (the item panel's sale)
            VehicleInstance? sitting = null; try { sitting = VehicleHelper.GetCurrentVehicle(); } catch { }
            foreach (var v in new List<VehicleInstance>(gi.VehicleInstances))
            {
                if (v == null) continue;
                if (sitting != null && ReferenceEquals(v, sitting)) { Plugin.Logger.LogWarning("[Dissolve] the vehicle the player could not get out of is kept."); continue; }
                try
                {
                    float price = 0f; try { price = v.GetSellingPrice(); } catch { }
                    VehicleController? vc = null; try { vc = VehicleHelper.GetVehicleController(v); } catch { }
                    if (vc != null) { try { TrafficSync.NotifyCollidersRemoved(vc.gameObject); } catch { } }
                    string type = ""; try { type = v.vehicleTypeName ?? ""; } catch { }
                    v.Delete(vc);
                    var data = new Dictionary<string, string> { { "itemSoldInfo", type } };
                    if (price > 0f) GameManager.ChangeMoneySafe(price, new TransactionInfo("ba:transaction_itemsold", data), null, null, force: true);
                    total += Math.Max(0f, price);
                    cars++;
                }
                catch (Exception ex) { Plugin.Logger.LogWarning($"[Dissolve] vehicle: {ex.Message}"); }
            }

            try { RealEstateHelper.AddNoHomeModifierIfNeeded(); } catch { }
            try { SaveGameManager.MarkChange(); } catch { }
            float after = before; try { after = gi.Money; } catch { }
            Plugin.Logger.LogInfo($"[Dissolve] sale {job.SaleId} done: {staff} employee(s) let go, {leases} lease(s) ended, {deeds} building(s) sold, {cars} vehicle(s) sold - ${total:N0} (money here {before:N0} -> {after:N0}).");
            Notify(Loc.T("gps_dissolve_done", "Sold: {0} shop(s)/home(s), {1} building(s), {2} vehicle(s); {3} employee(s) let go - {4} into the company account, which is now shared.",
                leases, deeds, cars, staff, ShopInsights.Money(total)));
            Report(job, total, leases, deeds, cars, staff);
        }

        /// <summary>What the game's terminate pays for an address: its furniture and the vehicles kept there (selling prices).</summary>
        private static float Contents(BuildingRegistration reg)
        {
            float sum = 0f;
            try { if (reg.itemInstances != null) foreach (var i in reg.itemInstances.Values) if (i != null) sum += i.GetSellingPrice(); } catch { }
            try
            {
                foreach (var v in SaveGameManager.Current.VehicleInstances)
                    if (v != null && v.Address == reg.Address) sum += v.GetSellingPrice();
            }
            catch { }
            return Math.Max(0f, sum);
        }

        private static void Pay(float amount, string key, Address address)
        {
            if (amount <= 0f) return;
            var data = new Dictionary<string, string> { { "address", address.ToFormattedString() } };
            GameManager.ChangeMoneySafe(amount, new TransactionInfo(key, data), null, address, force: true);
        }

        /// <summary>A lease end reported the way MPPatches.Patch_TerminateContract reports the game's own (the lease-end watch
        /// that keys on the refund below is told it went out, so it stands down).</summary>
        private static void ReportVacate(string addr)
        {
            if (MPClient.IsConnected)
            {
                bool sent = false; try { sent = MPClient.RequestVacateBuilding(addr); } catch { }
                if (sent) LeaseEndWatch.MarkSent(addr);
                else Plugin.Logger.LogWarning($"[Dissolve] '{addr}': the vacate could not be sent.");
            }
            else if (MPServer.IsRunning)
            {
                MPServer.BuildingOwners.TryRemove(addr, out _);
                MPServer.BroadcastVacate(addr);
                LeaseEndWatch.MarkSent(addr);
                MPServer.RefreshBuildingAccess();
            }
        }

        /// <summary>A building sale reported the way the AI purchase of a listed building is (MPPatches'
        /// SimulateCompetitorBuyingPlayerBuildings postfix).</summary>
        private static void ReportSold(string addr)
        {
            if (MPClient.IsConnected) { try { MPClient.SendSaleCompleted(addr); } catch { } }
            else if (MPServer.IsRunning)
            {
                MPServer.BuildingRealEstateOwners.TryRemove(addr, out _);
                GameStatePatcher.HostRemoveFromForSale(addr);
            }
        }

        private static void Report(MergerDissolveSalePayload job, float total, int leases, int deeds, int cars, int staff)
        {
            var done = new MergerDissolveSalePayload
            {
                PlayerId = MPConfig.PlayerId, GroupId = job.GroupId, SaleId = job.SaleId,
                Total = total, Leases = leases, Buildings = deeds, Vehicles = cars, Staff = staff,
            };
            if (MPServer.IsRunning) MPServer.HostDissolveSaleDone(MPConfig.PlayerId, done);
            else MPClient.SendDissolveSaleDone(done);
        }

        private static void Notify(string text)
        {
            try { UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, text); } catch { }
            try { PassengerHud.Toast(text, 8f); } catch { }
        }
    }
}
