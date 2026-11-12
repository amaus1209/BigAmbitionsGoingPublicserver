using System;
using HarmonyLib;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>
    /// MODULE-IRS-PHONE-1 (Going Public Server 1.0.12, module 1 of the server's own features, user ruling 2026-10-30):
    /// pay taxes by phone. The Internal Revenue Service contact gets a Call button (the game shows none for it); the
    /// call opens the game's own tax payment window (PurchaseUI, type IRS - the window the IRS counter opens, with the
    /// same amount field, so partial payments work), and paying runs the same TaxHelper.PayCurrentTaxes / PayBackTaxes the
    /// IRS clerk runs (IRSEmployee.ServeCustomer). Nothing about taxes changes, nothing is written to the save; the
    /// counter keeps working.
    ///
    /// The contact exists once the game has added it (the first tax notice / IRS visit), so the call is there from then on.
    /// Local to each player: taxes are per player; the money change reaches the server like any other.
    /// </summary>
    internal static class IrsPhone
    {
        private static readonly System.Reflection.FieldInfo? _callButton = AccessTools.Field(typeof(UI.Smartphone.Apps.Contacts.ContactsApp), "callButton");

        [HarmonyPatch(typeof(UI.Smartphone.Apps.Contacts.ContactsApp), "SetUpHeader")]
        public static class Patch_ContactsApp_SetUpHeader_IrsCall
        {
            static void Postfix(UI.Smartphone.Apps.Contacts.ContactsApp __instance, Entities.Contact contact)
            {
                try
                {
                    if (!Helpers.TaxHelper.IsIrsContact(contact)) return;
                    if (!ServerRules.Module("ModuleIrsPhone")) return;   // SERVER-RULES-1: switched off by the server admin
                    if (!(_callButton?.GetValue(__instance) is UnityEngine.UI.Button button)) return;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(Call);
                    button.interactable = true;
                    button.gameObject.SetActive(true);
                }
                catch (Exception ex) { Plugin.Logger.LogWarning($"[IrsPhone] call button: {ex.Message}"); }
            }
        }

        private static void Call()
        {
            try
            {
                bool current = Helpers.TaxHelper.HasCurrentTaxesToPay(), back = Helpers.TaxHelper.HasBackTaxesToPay();
                if (!current && !back)
                {
                    Notice(UI.Notification.NotificationType.Info, Loc.T("gps_irs_phone_nothing", "Internal Revenue Service: you have no taxes to pay right now."));
                    return;
                }
                var uis = InstanceBehavior<UI.UIs>.Instance;
                var purchase = uis?.playerHUD?.purchaseUI;
                if (uis == null || purchase == null) return;
                if (purchase.IsDoingPurchase) return;
                try { uis.CloseActiveUis(); } catch { }   // the phone closes, the payment window opens over the world
                var type = current ? Entities.TaxPaymentType.CurrentTaxes : Entities.TaxPaymentType.BackTaxes;
                // the pay click runs PlaceOrder, which goes on after its callback (queue label, slide-out): pay and close a frame later
                purchase.Open(UI.Purchase.PurchaseUI.Type.IRS, _ => { }, _ => GameStatePatcher.EnqueueOnMainThread(Pay), null, UI.Purchase.PurchaseUI.Mode.NoChanges, type);
                Plugin.Logger.LogInfo($"[IrsPhone] tax payment window opened by phone ({type}).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[IrsPhone] call: {ex.Message}"); }
        }

        /// <summary>The payment, exactly as the IRS clerk makes it (IRSEmployee.ServeCustomer).</summary>
        private static void Pay()
        {
            try
            {
                var purchase = InstanceBehavior<UI.UIs>.Instance.playerHUD.purchaseUI;
                float amount = purchase.CurrentTaxPaymentAmount;
                bool ok = purchase.CurrentTaxPaymentType == Entities.TaxPaymentType.BackTaxes
                    ? Helpers.TaxHelper.PayBackTaxes(amount)
                    : Helpers.TaxHelper.PayCurrentTaxes(amount);
                if (!ok) UI.Notification.Notifications.ShowInsufficientMoney();
                purchase.Close();
                if (ok) Notice(UI.Notification.NotificationType.Success, Loc.T("gps_irs_phone_paid", "Taxes paid by phone: ${0}.", amount.ToString("N0")));
                Plugin.Logger.LogInfo($"[IrsPhone] {(ok ? "paid" : "not enough money for")} ${amount:N0} ({purchase.CurrentTaxPaymentType}).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[IrsPhone] pay: {ex.Message}"); }
        }

        private static void Notice(UI.Notification.NotificationType type, string text)
        {
            try { UI.Notification.Notifications.Show(type, text); } catch { }
        }
    }
}
