using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>
    /// SAVE-NOTICE-1 (Going Public Server 1.0.9): on a dedicated server a player sees when the world is saved -
    /// "Saving on the server..." when the host's SaveNow arrives, then "Saved on the server" or "Saving on the server
    /// failed" from the host's SaveStored answer to this player's upload (or after 90 s without one), as the game's own
    /// notifications at the top right (SAVE-NOTICE-2). CLIENT side;
    /// Begin/Stored/Failed come from the network thread and only record what happened - Tick translates and shows the
    /// notice on the main thread.
    /// </summary>
    internal static class ServerSaveNotice
    {
        private const float AnswerTimeoutSeconds = 90f;

        private enum Show { None, Running, Ok, Failed }

        private static readonly object _lock = new object();
        private static Show _show = Show.None;
        private static bool _pending, _begun;
        private static float _deadline = -1f;
        private static string _reason = "";

        /// <summary>Network thread: the host asked everyone to save.</summary>
        public static void Begin(string session)
        {
            if (!MPClient.HostIsDedicated) return;
            lock (_lock) { _begun = true; _pending = true; _show = Show.Running; }
        }

        /// <summary>Network thread: the host's answer to this player's upload.</summary>
        public static void Stored(SaveStoredPayload? p)
        {
            if (p == null || !MPClient.HostIsDedicated) return;
            lock (_lock) { _pending = false; _show = p.Ok ? Show.Ok : Show.Failed; _reason = p.Reason ?? ""; }
            if (!p.Ok) Plugin.Logger.LogWarning($"[SaveNotice] the server did not store '{p.SessionName}': {p.Reason}");
        }

        /// <summary>Any thread: this player's upload could not be sent.</summary>
        public static void Failed(string reason)
        {
            if (!MPClient.HostIsDedicated) return;
            lock (_lock) { _pending = false; _show = Show.Failed; _reason = reason ?? ""; }
        }

        /// <summary>Main thread, every frame.</summary>
        public static void Tick()
        {
            Show show; string reason;
            lock (_lock)
            {
                float now = Time.realtimeSinceStartup;
                if (_begun) { _begun = false; _deadline = now + AnswerTimeoutSeconds; }
                if (_pending && _deadline > 0f && now > _deadline) { _pending = false; _show = Show.Failed; _reason = ""; }
                show = _show; reason = _reason; _show = Show.None;
            }
            if (show == Show.None) return;
            string msg = show == Show.Running ? Loc.T("gps_save_running", "Saving the game on the server...")
                       : show == Show.Ok      ? Loc.T("gps_save_ok", "Game saved on the server.")
                       : Loc.T("gps_save_failed", "Saving on the server failed ({0}).",
                               reason.Length > 0 ? reason : Loc.T("gps_save_noanswer", "no answer from the server"));
            // SAVE-NOTICE-2 (user 2026-11-02: "the saving notice should appear at the top right too, not in the middle of
            // the game"): the game's own notifications - quiet, and kept out of the save's notification list (an autosave
            // every few minutes would fill it); a failure sounds and stays longer. No duplicateIdentifier: the game then
            // leaves the refused copy behind, inactive.
            var type = show == Show.Running ? UI.Notification.NotificationType.Info
                     : show == Show.Ok      ? UI.Notification.NotificationType.Success
                     : UI.Notification.NotificationType.Error;
            float seconds = show == Show.Running ? 3f : show == Show.Ok ? 4f : 8f;
            try { UI.Notification.Notifications.Show(type, msg, null, seconds, null, null, show == Show.Failed, false); }
            catch { try { PassengerHud.Toast(msg, 4f); } catch { } }
        }
    }
}
