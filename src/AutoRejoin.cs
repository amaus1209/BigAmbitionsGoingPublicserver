using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>RECONNECT-1 (1.0.16, user 2026-11-01: "join again by itself after a lost connection"): when a dedicated
    /// server was lost by the network (a timeout, no answer) or because it restarted, the main menu's "SERVER CONNECTION
    /// LOST" box counts down and the game joins the same server again by itself - up to five tries, 10, 20, 30, 60 and 60
    /// seconds apart. "Cancel" in the box (or Enter / Esc) stops it. Never after a kick, a ban or a refused join.
    /// RECONNECT-2 (user 2026-11-08, a restart staged while he played: "automatisches Verbinden hat aufgehört"): a try
    /// counted as done the moment the connection stood. The server was still loading its world then, the join was parked,
    /// the link ended - and nothing tried again. Now a try is done only once the player is back in the world
    /// (MPClient.InMpGame); a link that ends before that is a failed try unless the server refused the join (a tagged
    /// close). Eight tries over about seven minutes: a restart with the whole city takes two.</summary>
    internal static class AutoRejoin
    {
        private static readonly float[] Delays = { 10f, 20f, 30f, 30f, 60f, 60f, 90f, 120f };
        private static bool _armed, _trying;
        private static int _tries;
        private static float _nextAt;
        private static string _addr = "";
        private static int _port;

        internal static bool Armed => _armed;

        /// <summary>MPClient, main thread: the dedicated server was lost this way - try again from the main menu.</summary>
        internal static void Arm(string why)
        {
            _addr = MPConfig.HostIP ?? "";
            _port = MPConfig.Port;
            if (_addr.Length == 0) return;
            _armed = true; _trying = false; _tries = 0;
            _nextAt = Time.unscaledTime + Delays[0];
            Plugin.Logger.LogInfo($"[Rejoin] armed after '{why}' - first try in {Delays[0]:F0} s (RECONNECT-1).");
        }

        internal static void Cancel(string why)
        {
            if (!_armed) return;
            _armed = false; _trying = false;
            Plugin.Logger.LogInfo($"[Rejoin] cancelled ({why}).");
        }

        /// <summary>MPClient: a try ended without a connection.</summary>
        internal static void TryFailed()
        {
            if (!_armed || !_trying) return;
            _trying = false;
            if (_tries >= Delays.Length)
            {
                _armed = false;
                Plugin.Logger.LogInfo($"[Rejoin] gave up after {_tries} tries.");
                MPCanvasUI.NoteMenuNotice(Loc.T("gps_notice_server_lost_title", "SERVER CONNECTION LOST"),
                    Loc.T("gps_rejoin_gaveup", "The server did not answer {0} times. Join again later from Multiplayer.", _tries));
                return;
            }
            _nextAt = Time.unscaledTime + Delays[_tries];
            MPCanvasUI.NoteMenuNotice(Loc.T("gps_notice_server_lost_title", "SERVER CONNECTION LOST"),
                Loc.T("gps_rejoin_retry", "Try {0} of {1} did not reach the server.", _tries, Delays.Length));
            Plugin.Logger.LogInfo($"[Rejoin] try {_tries} failed - next in {Delays[_tries]:F0} s.");
        }

        /// <summary>MPClient: the connection stands - the try goes on until the player is in the world (RECONNECT-2).</summary>
        internal static void Connected()
        {
            if (!_armed) return;
            Plugin.Logger.LogInfo($"[Rejoin] try {_tries}: connected - waiting until the server lets us into the world (RECONNECT-2).");
        }

        /// <summary>MPClient, main thread: a link ended (wasConnected: it had stood). During a try that is a failed try -
        /// unless the server refused the join (a tagged close), which ends the rejoining.</summary>
        internal static void LinkEnded(bool wasConnected, bool refused)
        {
            if (!_armed || !_trying) return;
            if (refused) { Cancel("the server refused the join"); return; }
            if (wasConnected) Plugin.Logger.LogInfo($"[Rejoin] try {_tries}: the link ended before we were in the world (RECONNECT-2).");
            TryFailed();
        }

        /// <summary>The live line of the box while it waits ("" = none).</summary>
        internal static string Countdown()
        {
            if (!_armed) return "";
            if (_trying || MPClient.IsConnecting) return Loc.T("gps_rejoin_connecting", "Joining again ...");
            int left = Mathf.Max(0, Mathf.CeilToInt(_nextAt - Time.unscaledTime));
            return Loc.T("gps_rejoin_in", "Joining again in {0} s ({1} of {2}).", left, _tries + 1, Delays.Length);
        }

        /// <summary>Main thread, every frame (MPCanvasUI.Update): only in the main menu, never while connected.</summary>
        internal static void Tick()
        {
            if (_armed && _trying && MPClient.IsConnected && MPClient.InMpGame)
            {
                _armed = false; _trying = false;
                Plugin.Logger.LogInfo($"[Rejoin] back in the world after {_tries} tr{(_tries == 1 ? "y" : "ies")} (RECONNECT-2).");
                return;
            }
            if (!_armed || _trying) return;
            if (MPClient.IsConnected || MPClient.IsConnecting || MPServer.IsRunning) return;
            if (SaveGameManager.Current != null || MPClient.BanExitPending) return;   // still on the way out of the city
            if (Time.unscaledTime < _nextAt) return;
            _tries++;
            _trying = true;
            Plugin.Logger.LogInfo($"[Rejoin] try {_tries} of {Delays.Length}.");
            if (_addr.StartsWith("steam:", System.StringComparison.OrdinalIgnoreCase) && ulong.TryParse(_addr.Substring(6).Trim(), out ulong id))
            {
                if (!MPClient.ConnectSteam(id)) { _trying = true; TryFailed(); }
            }
            else MPClient.Connect(_addr, _port);
        }
    }
}
