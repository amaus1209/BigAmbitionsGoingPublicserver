using HarmonyLib;

namespace BigAmbitionsMP
{
    /// <summary>SKIP-BRIDGE-OFF-1 (user 2026-11-08: "die 60 km/h auf der Brücke erhöhen, dass die Wartezeit nicht zu lange ist,
    /// und den Button Überspringen rausnehmen"). The game's "Skip bridge" button (Player.SkipBridgeUi: while a car is on a
    /// bridge more than 100 m from its end, switched on by its ButtonCheckCoroutine) puts the car at the other end and skips
    /// the game time of the drive (SkipBridgeHelper: 0.04 minutes a metre, about an hour on a long bridge) on this machine
    /// only. In a shared world the host keeps the clock, so that jump does not fit. In a multiplayer session the check
    /// coroutine is not started and a button that is up goes away; the key (Interact) only works while the button is up.
    /// The bridge itself stays known to the game (SkipBridgeHelper.skipBridgeEnabled), which self-driving uses for its bridge
    /// speed (AUTO-DRIVE-5). Single player keeps the button.</summary>
    [HarmonyPatch(typeof(global::Player.SkipBridgeUi), "OnSkipBridgeEnabled")]
    public static class Patch_SkipBridgeUi_Enabled
    {
        static bool Prefix(global::Player.SkipBridgeUi __instance)
        {
            if (!SkipBridgeOff.InSession) return true;
            SkipBridgeOff.Hide(__instance, "on a bridge");
            return false;
        }
    }

    /// <summary>SKIP-BRIDGE-OFF-1: a button that was already up when the session began goes away before the game reads its key.</summary>
    [HarmonyPatch(typeof(global::Player.SkipBridgeUi), "Update")]
    public static class Patch_SkipBridgeUi_Update
    {
        static void Prefix(global::Player.SkipBridgeUi __instance)
        {
            if (SkipBridgeOff.InSession) SkipBridgeOff.Hide(__instance, null);
        }
    }

    internal static class SkipBridgeOff
    {
        private static bool _logged;

        internal static bool InSession
        {
            get { try { return MPClient.IsConnected || MPServer.IsRunning; } catch { return false; } }
        }

        internal static void Hide(global::Player.SkipBridgeUi ui, string? why)
        {
            try
            {
                var b = ui.skipBridgeButton;
                if (b != null && b.activeSelf) b.SetActive(false);
                if (ui._buttonCheckCoroutine != null) { ui.StopCoroutine(ui._buttonCheckCoroutine); ui._buttonCheckCoroutine = null; }
                if (why != null && !_logged)
                {
                    _logged = true;
                    Plugin.Logger.LogInfo($"[Bridge] {why}: the game's \"Skip bridge\" button stays hidden in multiplayer - self-driving goes up to 90 km/h there instead (SKIP-BRIDGE-OFF-1).");
                }
            }
            catch { }
        }
    }
}
