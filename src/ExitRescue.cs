using System;
using UnityEngine;
using UnityEngine.AI;

namespace BigAmbitionsMP
{
    /// <summary>EXIT-NAVMESH-1 (user 2026-11-06 with a Player.log: "Spielfigur spinnt verrückt, schwebt über Wasser" at the
    /// 6th Avenue pier): self-driving stopped at BlueStone Imports (4 Pier), where no parking spot was free, the player got
    /// out, and the game said "Failed to create agent because it is not close enough to the NavMesh" - the character stood
    /// beside the walkable mesh by the water. Without the mesh the game still turns the character to the keys but cannot move
    /// it (PlayerController: the rotation, then navmeshAgent.Move does nothing), and the sit repair (NAV HEAL,
    /// Character.Reset) left it there too. The game has no recovery for that (RIDE-EXIT-1 met the same for passengers). So
    /// for 3 s after getting out of a vehicle, and after a NAV HEAL, the character is checked: off the mesh, it goes to the
    /// nearest walkable point (5 m, else 25 m, else 60 m).</summary>
    internal static class ExitRescue
    {
        private static bool _wasInVehicle;
        private static float _watchUntil = -1f, _graceUntil, _next;
        private static string _why = "";

        /// <summary>Watch the character for the next 3 s (after something that can leave it off the mesh).</summary>
        internal static void Arm(string why, float grace = 0.6f)
        {
            float now = Time.unscaledTime;
            _watchUntil = now + 3f + grace;
            _graceUntil = now + grace;
            _why = why;
        }

        /// <summary>Main thread, every frame in the city.</summary>
        internal static void Tick()
        {
            try
            {
                float now = Time.unscaledTime;
                bool inVehicle = Helpers.VehicleHelper.GetCurrentVehicleBase() != null;
                if (_wasInVehicle && !inVehicle) Arm("got out of a vehicle");
                _wasInVehicle = inVehicle;
                if (_watchUntil < 0f || now < _graceUntil || now < _next) return;
                _next = now + 0.25f;
                if (now > _watchUntil || inVehicle) { _watchUntil = -1f; return; }
                var pc = Helpers.PlayerHelper.PlayerController;
                var ch = pc != null ? pc.Character : null;
                var agent = ch != null ? ch.navmeshAgent : null;
                if (ch == null || agent == null) return;
                if (agent.enabled && agent.isOnNavMesh) { _watchUntil = -1f; return; }   // fine
                if (MPRestSync.Seated || pc!.NavigationDisabled) return;              // sitting or in a menu: off on purpose
                try { if (UI.UiFader.isFading) return; } catch { }                    // a door or a skip is under way
                Vector3 at = ch.transform.position, to = at;
                string how = "";
                foreach (float r in new[] { 5f, 25f, 60f })
                {
                    bool hit = false; NavMeshHit h = default;
                    try { hit = NavMesh.SamplePosition(at, out h, r, PlayerController.navMeshQueryFilter); } catch { }
                    if (!hit) { try { hit = NavMesh.SamplePosition(at, out h, r, NavMesh.AllAreas); } catch { } }
                    if (hit) { to = h.position; how = r.ToString("0") + " m"; break; }
                }
                _watchUntil = -1f;
                if (how.Length == 0)
                {
                    Plugin.Logger.LogWarning($"[ExitRescue] {_why}: the character stands off the walkable mesh at ({at.x:F0}, {at.z:F0}) and none is within 60 m (EXIT-NAVMESH-1).");
                    return;
                }
                if (!agent.enabled) { try { ch.transform.position = to; agent.enabled = true; } catch { } }
                try { agent.Warp(to); } catch { try { ch.transform.position = to; } catch { } }
                try { pc!.ResetNavigation(); } catch { }
                bool ok = false;
                try { ok = agent.enabled && agent.isOnNavMesh; } catch { }
                Plugin.Logger.LogInfo($"[ExitRescue] {_why}: the character stood off the walkable mesh at ({at.x:F0}, {at.z:F0}) - put on the nearest walkable point {Vector3.Distance(at, to):F1} m away (within {how}), on the mesh now: {ok} (EXIT-NAVMESH-1).");
            }
            catch (Exception ex) { _watchUntil = -1f; Plugin.Logger.LogWarning($"[ExitRescue] {ex.Message}"); }
        }
    }
}
