using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace BigAmbitionsMP
{
    /// <summary>JOIN-OUTSIDE-1: a player joining a server starts in front of the building they saved in, never inside it.
    /// A save made indoors makes the game enter that building while it places the player (CityManager.PositionPlayerInScene
    /// -> LoadIndoors -> EnterBuilding), and on a client joining a server that load failed: it fell back to the main menu
    /// ("the save is damaged", the lobby waiting) on every try - in the field each such save was made indoors, while the
    /// same file loaded fine on the server. The saved building's entrance (else the game's own start) becomes the spot.
    /// Clients only; the host and single player keep the game's behaviour.</summary>
    [HarmonyPatch(typeof(CityManager), "PositionPlayerInScene")]
    public static class Patch_CityManager_PositionPlayerInScene_JoinOutside
    {
        static void Prefix()
        {
            try
            {
                if (MPServer.IsRunning || !MPClient.IsConnected) return;
                var gi = SaveGameManager.Current;
                if (gi == null || string.IsNullOrEmpty(gi.CurrentStreetName)) return;
                var where = new Address(gi.CurrentStreetName, gi.CurrentStreetNumber);
                Vector3 spot = new Vector3(215f, 0f, 0f);   // the game's start (GameInstance.LastPlayerPosition default)
                if (Navigator.TryBuilding(where, out var door, out _) && NavMesh.SamplePosition(door, out var hit, 25f, NavMesh.AllAreas))
                    spot = hit.position;
                Plugin.Logger.LogInfo($"[Spawn] join: the save was made inside {where} - starting in front of it at ({spot.x:F1}, {spot.z:F1}) (JOIN-OUTSIDE-1).");
                gi.CurrentStreetName = "";
                gi.CurrentStreetNumber = 0;
                gi.LastPlayerPosition = spot;
            }
            catch (System.Exception ex) { Plugin.Logger.LogWarning($"[Spawn] join outside: {ex.Message}"); }
        }
    }
}
