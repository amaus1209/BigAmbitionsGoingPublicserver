using System;
using BigAmbitions.Items;
using Controllers;
using HarmonyLib;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>WORKSTATION-CHAIR-1 (user 2026-11-11: "Spielfigur hängt und kein ESC mehr möglich" - the player worked at
    /// a computer in a Going Online customer center and could not get up again).
    /// The game's WorkstationController keeps the chair of the person working there in _chairItemController.
    /// FindChair(forceCheck: true) runs whenever a seat joins or leaves the desk (OnParentNewChild/OnParentChildRemoved)
    /// and takes only FREE seats, so it drops the occupied one: the field turns null (or names another, free chair) while
    /// somebody sits there. UnassignEmployee then throws at `_chairItemController.Occupied = false`, and every way out of
    /// the work throws with it (ESC → WorkActivity.Finish, the stand-up button, the shift's end).
    /// - FindChair: while somebody sits at the workstation, the seat under that person stays its chair.
    /// - UnassignEmployee: no chair known → the seat under the person; none at all → the workstation stands in for the
    ///   chair (its own flag is put back afterwards), so the screen and the game's unassign still run. No employee → only
    ///   the chair and the screen (the game's code would throw on the missing employee).
    /// - The same report had the city desks' SitPlayerOnChair throw on a desk without a customer chair: the talk now starts
    ///   standing instead of not at all.</summary>
    internal static class WorkstationChairGuard
    {
        static int _notes;
        internal static bool WasOccupied;

        /// <summary>The seat the workstation's employee (or the player working there) sits on, or null.</summary>
        internal static ItemController? SeatUnder(WorkstationController ws)
        {
            try
            {
                var emp = ws.employee;
                if (emp == null || emp.employeeTpc == null) return null;
                Transform pos = emp.employeeTpc.isSittingOn;
                if (pos == null) return null;
                var seat = pos.GetComponentInParent<ItemController>();
                if (seat == null || seat == ws || seat.Item == null || (seat.Item.type & ItemType.Seat) == 0) return null;
                return seat;
            }
            catch { return null; }
        }

        internal static void Note(string what)
        {
            if (_notes >= 20) return;
            _notes++;
            try { Plugin.Logger.LogWarning($"[Workstation] {what} (WORKSTATION-CHAIR-1)"); } catch { }
        }
    }

    [HarmonyPatch(typeof(WorkstationController), nameof(WorkstationController.FindChair))]
    internal static class Patch_WorkstationController_FindChair_KeepSeated
    {
        static void Postfix(WorkstationController __instance, bool forceCheck, ref ItemController __result)
        {
            try
            {
                if (!forceCheck) return;
                var seat = WorkstationChairGuard.SeatUnder(__instance);
                if (seat == null || seat == __result) return;
                __instance._chairItemController = seat;
                __instance.employeeChair = seat.SittingPosition;
                __result = seat;
                WorkstationChairGuard.Note($"'{__instance.name}': a seat changed at the desk while somebody worked there - the chair in use stays the workstation's");
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(WorkstationController), nameof(WorkstationController.UnassignEmployee))]
    internal static class Patch_WorkstationController_UnassignEmployee_NoChair
    {
        static bool Prefix(WorkstationController __instance, out bool __state)
        {
            __state = false;
            try
            {
                if (__instance.employee == null)
                {
                    if (__instance._chairItemController != null) __instance._chairItemController.Occupied = false;
                    __instance.StopVideoOnScreen();
                    return false;
                }
                if (__instance._chairItemController != null) return true;
                var seat = WorkstationChairGuard.SeatUnder(__instance);
                if (seat != null)
                {
                    __instance._chairItemController = seat;
                    WorkstationChairGuard.Note($"'{__instance.name}': the chair was lost while somebody worked there - the seat under the person is freed");
                    return true;
                }
                __state = true;
                WorkstationChairGuard.WasOccupied = __instance.occupied;
                __instance._chairItemController = __instance;
                WorkstationChairGuard.Note($"'{__instance.name}': no chair known when the work ends - stood up without one");
            }
            catch (Exception ex) { WorkstationChairGuard.Note($"guard: {ex.Message}"); }
            return true;
        }

        static void Finalizer(WorkstationController __instance, bool __state)
        {
            if (!__state) return;
            try
            {
                __instance._chairItemController = null;
                __instance.occupied = WorkstationChairGuard.WasOccupied;
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(BusinessEmployeeController), nameof(BusinessEmployeeController.SitPlayerOnChair))]
    internal static class Patch_BusinessEmployeeController_SitPlayerOnChair_NoChair
    {
        static bool Prefix(BusinessEmployeeController __instance)
        {
            try
            {
                var chair = __instance._customerChair;
                if (chair != null && chair.GetComponent<SeatController>() != null) return true;
                WorkstationChairGuard.Note($"'{__instance.name}': no customer chair at this desk - the talk starts standing");
                return false;
            }
            catch { return true; }
        }
    }
}
