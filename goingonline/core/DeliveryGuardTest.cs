using System.Collections.Generic;
using System.Linq;
using Entities;
using HarmonyLib;

namespace GoingOnline
{
    /// <summary>GO-FURNDELIVERY-2 check for the developer: runs at the start of the self-test (SelfTest.Run - Going Public
    /// Server starts it on a dedicated server whose mod folder holds goingonline-selftest.txt). It puts three made-up due
    /// orders in front of the game's furniture round - one with an item the game does not know, aimed at a real building (the
    /// order that blocked every delivery), an empty entry, one without a target - runs the round
    /// (FurnitureDeliveryHelper.RunHourly) and checks: the unknown-item order waits unpaid, the other two are gone, nothing
    /// threw, the money is unchanged. Its own orders leave the list again afterwards. It does not run while real orders are
    /// due (they would arrive early) or when the guard is not patched in (the unknown-item order would then add a delivery
    /// spot to that building before failing).</summary>
    internal static class DeliveryGuardTest
    {
        internal static void Run()
        {
            string result;
            var sgm = SaveGameManager.Current;
            var list = sgm?.FurnitureDeliveryContracts;
            if (sgm == null || list == null) { Go.Warn("delivery guard test: no save loaded."); return; }
            int day = sgm.Day, hour = sgm.Hour;
            if (list.Any(d => d != null && day >= d.dayOfDelivery && hour >= d.hourOfDelivery))
            {
                Go.Warn("delivery guard test: real phone orders are due - skipped (GO-FURNDELIVERY-2).");
                return;
            }
            var helper = typeof(Buildings.BuildingTypes.Special.FurnitureStore.FurnitureDeliveryHelper);
            var round = AccessTools.Method(helper, "DoDeliveriesInDayAndHour", new[] { typeof(int), typeof(int) });
            var info = round == null ? null : Harmony.GetPatchInfo(round);
            if (info == null || !info.Prefixes.Any(p => p.PatchMethod.DeclaringType == typeof(GamePatches.Patch_FurnitureDelivery_Round_Guard)))
            {
                Go.Warn("delivery guard test: the guard is not patched in - skipped, the game's round stays untouched (GO-FURNDELIVERY-2).");
                return;
            }
            var target = sgm.BuildingRegistrations?.FirstOrDefault(r => r != null && r.Address != null);
            if (target == null) { Go.Warn("delivery guard test: no building in the save - skipped."); return; }

            var unknown = new FurnitureDeliveryContract
            {
                fromAddress = target.Address, toAddress = target.Address, dayOfDelivery = day, hourOfDelivery = 0,
                itemsToDeliver = new List<FurnitureDeliveryItem> { new FurnitureDeliveryItem { itemName = "goingonline:deliverytest-unknown", amount = 1, pricePerUnit = 0f } },
            };
            var noTarget = new FurnitureDeliveryContract
            {
                dayOfDelivery = day, hourOfDelivery = 0,
                itemsToDeliver = new List<FurnitureDeliveryItem> { new FurnitureDeliveryItem { itemName = "goingonline:deliverytest-unknown", amount = 1, pricePerUnit = 0f } },
            };
            float moneyBefore = sgm.Money;
            int othersBefore = list.Count;
            list.Insert(0, unknown);
            list.Insert(1, null!);
            list.Insert(2, noTarget);
            try
            {
                Buildings.BuildingTypes.Special.FurnitureStore.FurnitureDeliveryHelper.RunHourly();
                bool waits = list.Contains(unknown) && GamePatches.Patch_FurnitureDelivery_Round_Guard.IsWaiting(unknown);
                bool noTargetGone = !list.Contains(noTarget);
                bool nullGone = !list.Any(x => x == null);
                bool moneySame = System.Math.Abs(sgm.Money - moneyBefore) < 0.001f;
                bool ok = waits && noTargetGone && nullGone && moneySame;
                result = (ok ? "OK" : "FAILED") + $": unknown-item order waits unpaid {waits}, order without target done {noTargetGone}, " +
                         $"empty entry removed {nullGone}, money unchanged {moneySame}";
            }
            catch (System.Exception ex)
            {
                result = "FAILED: the round threw " + ex.GetType().Name + ": " + ex.Message;
            }
            finally
            {
                list.Remove(unknown);
                list.Remove(noTarget);
                list.RemoveAll(x => x == null);
            }
            Go.Log($"delivery guard test (GO-FURNDELIVERY-2): {result}; orders in the list before {othersBefore}, after {list.Count}.");
        }
    }
}
