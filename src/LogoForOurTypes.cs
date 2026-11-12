using System;
using HarmonyLib;

namespace BigAmbitionsMP
{
    /// <summary>LOGO-GO-1 (user 2026-11-07: "Firmenlogo nicht auswählbar ... besser wäre Standard-Schilder und Mod zu verbinden,
    /// für beide zur Auswahl"): BizMan hides its logo editor for a business in a building tagged "hasnologo" - every warehouse
    /// - and makes a warehouse's logo the plain warehouse one (the type's icon in black on white: black all over with the
    /// cable provider's square icon). The cable provider of Going Online runs in a warehouse but is a brand customers see, so
    /// for our business types the editor shows (shape, colours, font, the game's own icons folder) and saving the business
    /// makes a real logo. The shop signs window (F7) can put the game's logo back on a sign too (MPSignManager).</summary>
    internal static class LogoForOurTypes
    {
        internal static bool Ours(BuildingRegistration? reg) => reg != null && GoingOnline.GameTypes.IsOurs(reg.businessTypeName);

        [HarmonyPatch(typeof(BizManSettings), "RefreshData")]
        public static class Patch_BizManSettings_RefreshData_OurLogo
        {
            static void Postfix(BizManSettings __instance)
            {
                try
                {
                    if (!Ours(__instance._bizManBusiness?.buildingRegistration)) return;
                    if (__instance.logoCustomizer != null && !__instance.logoCustomizer.gameObject.activeSelf)
                        __instance.logoCustomizer.gameObject.SetActive(true);
                }
                catch (Exception ex) { Plugin.Logger.LogWarning($"[Logo] BizMan settings: {ex.Message}"); }
            }
        }

        [HarmonyPatch(typeof(BizManSettings), "GenerateNewLogo")]
        public static class Patch_BizManSettings_GenerateNewLogo_OurLogo
        {
            static void Prefix(BizManSettings __instance, ref bool isNotWarehouse)
            {
                try { if (Ours(__instance._bizManBusiness?.buildingRegistration)) isNotWarehouse = true; }
                catch { }
            }
        }
    }
}
