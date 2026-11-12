using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>LEAVE-SERVER-1 (Going Public Server 1.0.11): a "Leave server" button in the pause menu (Escape), right under
    /// "Save and Exit to Desktop", for a player connected to someone else's server.
    ///
    /// WHY: a player who wanted to stop had only "Save and Exit", which quits the whole game - on Proton that quit could
    /// hang. Leaving the server is what they meant.
    ///
    /// WHAT the click does: close the menu, send the player's progress to the server exactly as "Save and Exit" does
    /// (MPSaveCoordinator.MenuSave(exiting: true) - the host saves the session and gets this player's save), wait until the
    /// transport has sent it (at most UploadCeilingS), then the deliberate leave (MPClient.DisconnectAfterLeavePublish) and
    /// the game's own city exit to the main menu. The game keeps running.
    ///
    /// The button is a copy of the menu's own "Save and Exit" button (look and layout), its persistent listeners replaced.
    /// It is shown only while connected as a player; a host and single player never see it.</summary>
    internal static class MenuLeaveServer
    {
        private const string ObjName = "BAMP_LeaveServerButton";
        private const float UploadCeilingS = 10f;
        private static bool _leaving;

        [HarmonyPatch]
        public static class Patch_MiniMenu_Toggle_LeaveButton
        {
            static System.Reflection.MethodBase? TargetMethod()
                => MPPatches.MiniMenuUtil.Resolve()?.GetMethod("Toggle",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                    null, new Type[] { typeof(bool) }, null);

            static void Postfix(object __instance, bool show)
            {
                if (!show) return;
                try { Refresh(__instance as MonoBehaviour); }
                catch (Exception ex) { Plugin.Logger.LogWarning($"[LeaveServer] menu button: {ex.Message}"); }
            }
        }

        // LEAVE-SERVER-2 (user ruling 2026-10-30): while connected as a player the pause menu offers only "Leave server" -
        // the buttons that save, quit or go to the main menu (and the save-name box) are hidden; Resume and Options stay.
        private static readonly string[] HiddenWhileConnected = { "SaveGame", "SaveAndExitToDesktop", "QuitToDesktop", "OpenMainMenu" };
        private static readonly List<GameObject> _hidden = new List<GameObject>();

        private static void Refresh(MonoBehaviour? menu)
        {
            if (menu == null) return;
            bool wanted = MPClient.IsConnected && !MPServer.IsRunning && !_leaving;
            HideExitButtons(menu, wanted);
            var existing = FindChild(menu.transform, ObjName);
            if (existing != null) { existing.gameObject.SetActive(wanted); return; }
            if (!wanted) return;

            var saveExit = FindButtonCalling(menu.transform, "SaveAndExitToDesktop");
            if (saveExit == null) { Plugin.Logger.LogWarning("[LeaveServer] the menu's Save and Exit button was not found - no Leave server button (game update?)."); return; }
            var go = UnityEngine.Object.Instantiate(saveExit.gameObject, saveExit.transform.parent);
            go.name = ObjName;
            go.transform.SetSiblingIndex(saveExit.transform.GetSiblingIndex() + 1);
            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();   // drop the copied persistent call to SaveAndExitToDesktop
            var m = menu;
            button.onClick.AddListener(() => OnClick(m));
            // the copied label would be re-localized to "Save and Exit" by the game's text components: remove them, set ours
            foreach (var c in go.GetComponentsInChildren<Component>(true))
                if (c != null && c.GetType().Name.IndexOf("Locali", StringComparison.OrdinalIgnoreCase) >= 0)
                    UnityEngine.Object.DestroyImmediate(c);
            foreach (var t in go.GetComponentsInChildren<TMPro.TMP_Text>(true))
                t.text = Loc.T("gps_menu_leave_server", "Leave server");
            go.SetActive(true);
            Plugin.Logger.LogInfo("[LeaveServer] Leave server button added to the pause menu.");
        }

        private static void OnClick(MonoBehaviour menu)
        {
            if (_leaving || !MPClient.IsConnected || MPServer.IsRunning) return;
            _leaving = true;
            try { MPPatches.MiniMenuUtil.Close(menu); } catch { }
            var ui = MPCanvasUI.Instance;
            if (ui == null) { Plugin.Logger.LogWarning("[LeaveServer] no UI host - leaving without waiting for the upload."); Finish(); return; }
            ui.StartCoroutine(Leave());
        }

        private static IEnumerator Leave()
        {
            Plugin.Logger.LogInfo("[LeaveServer] leaving the server: sending the progress first.");
            try { UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, Loc.T("gps_notice_leaving_server", "Sending your progress to the server...")); } catch { }
            try { MPSaveCoordinator.MenuSave(exiting: true, saveName: ""); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[LeaveServer] save: {ex.Message}"); }
            // wait until the transport has sent the upload (LiteNetLib counts unsent packets only: one more second for the
            // packets in flight), bounded so a dead link cannot keep the player here
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < UploadCeilingS && MPClient.IsConnected && MPClient.PendingReliableNow > 0)
                yield return null;
            float sentAfter = Time.unscaledTime - start;
            yield return new WaitForSecondsRealtime(1f);
            Plugin.Logger.LogInfo($"[LeaveServer] upload handed to the network after {sentAfter:0.0} s ({MPClient.PendingReliableNow} unit(s) still queued, -1 = unknown) - leaving.");
            Finish();
        }

        private static void Finish()
        {
            try { if (MPClient.IsConnected) MPClient.DisconnectAfterLeavePublish("leave server"); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[LeaveServer] disconnect: {ex.Message}"); }
            try { MPClient.ExitToMainMenu("left the server"); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[LeaveServer] exit: {ex.Message}"); }
            _leaving = false;
        }

        private static void HideExitButtons(MonoBehaviour menu, bool hide)
        {
            try
            {
                if (!hide)
                {
                    foreach (var go in _hidden) if (go != null) go.SetActive(true);
                    _hidden.Clear();
                    return;
                }
                foreach (var b in menu.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                {
                    if (b == null || b.name == ObjName || !b.gameObject.activeSelf) continue;
                    for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                        if (Array.IndexOf(HiddenWhileConnected, b.onClick.GetPersistentMethodName(i)) >= 0)
                        { b.gameObject.SetActive(false); _hidden.Add(b.gameObject); break; }
                }
                var box = menu.GetComponentInChildren<TMPro.TMP_InputField>(true);   // the save-name box (MiniMenu.saveGameName)
                if (box != null && box.gameObject.activeSelf) { box.gameObject.SetActive(false); _hidden.Add(box.gameObject); }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[LeaveServer] hide exit buttons: {ex.Message}"); }
        }

        private static Transform? FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        private static UnityEngine.UI.Button? FindButtonCalling(Transform root, string method)
        {
            foreach (var b in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (b == null || b.name == ObjName) continue;
                for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                    if (b.onClick.GetPersistentMethodName(i) == method) return b;
            }
            return null;
        }
    }
}
