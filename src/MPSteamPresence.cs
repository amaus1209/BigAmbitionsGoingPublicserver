using System;
using Steamworks;

namespace BigAmbitionsMP
{
    /// <summary>Steam-connect slice 2 stage C (lobby-based, standard practice —
    /// user ruling 2026-07-10): hosting creates a friends-only Steam LOBBY used
    /// purely as an invite rendezvous — session traffic stays on the relay/UDP
    /// transports.  "Invite Friends" opens Steam's NATIVE invite dialog
    /// (OpenGameInviteOverlay needs a lobby — the friends-list overlay fallback
    /// was the pre-lobby stopgap).  Accepting an invite / Join Game delivers the
    /// lobby via OnGameLobbyJoinRequested (game running) or "+connect_lobby
    /// &lt;id&gt;" on the launch command line (Steam started the game); we join the
    /// lobby just long enough to read the host's SteamId from its data, leave,
    /// and relay-connect.  Rich presence "connect" stays set as well, so
    /// right-click → Join Game works even without an explicit invite.
    /// STEAM-FRIEND-JOIN-1 (user 2026-11-02: "check whether one can join the server through Steam when a friend is playing
    /// there" - "yes", with the key and an AMP switch): a player in a DEDICATED server's world tells Steam where he plays -
    /// rich presence "connect" = "bamp:&lt;address&gt;/&lt;port&gt;[/&lt;key&gt;]" (TickServerJoin) - so his friends see Join Game, and
    /// Invite to Game in their friends list, and land on that server with one click; the key only when the server needs
    /// one (MPClient.HostNeedsKey). The server's admin switches it off with AMP "SteamFriendJoin". Not for an address a
    /// friend could not reach (loopback, a private network, a local name), not while hosting (AdvertiseHosting's relay
    /// connect stands then). The address and the key are never logged (IP-IN-LOGS-1).</summary>
    public static class MPSteamPresence
    {
        private const string LobbyHostKey = "bamp_host";

        private static bool _hooked;
        private static Steamworks.Data.Lobby? _lobby;   // live while hosting

        /// <summary>Join target awaiting the menu (0 = none).  Set here, consumed
        /// on the main thread by MPCanvasUI.TickSteamJoin.</summary>
        public static ulong  PendingJoinId;
        public static string PendingJoinVia = "";
        /// <summary>STEAM-FRIEND-JOIN-1: a server join awaiting the menu ("" = none), consumed like PendingJoinId.</summary>
        public static string PendingJoinAddr = "", PendingJoinKey = "";
        public static int    PendingJoinPort;
        private static string _serverConnect = "";
        private static float _nextServerTick;

        /// <summary>Idempotent; called once SteamClient reports valid (probe tick).</summary>
        public static void EnsureHooked()
        {
            if (_hooked) return;
            _hooked = true;
            try
            {
                SteamFriends.OnGameLobbyJoinRequested += (lobby, friendId) =>
                    ResolveLobby(lobby, $"lobby invite/join via '{friendId}'");
                SteamFriends.OnGameRichPresenceJoinRequested += (friend, connect) =>
                    QueueJoin(connect, $"rich-presence join from '{friend.Name}'");
                // Cold start: Steam launched the game from an invite/Join click. STEAM-FRIEND-JOIN-1: Steam puts the join on
                // the PROCESS command line unless the game's own Steam page says otherwise - so that one is read too.
                string cl = "";
                try { cl = SteamApps.CommandLine ?? ""; } catch { }
                try { cl += " " + Environment.CommandLine; } catch { }
                int li = cl.IndexOf("+connect_lobby", StringComparison.OrdinalIgnoreCase);
                if (li >= 0 && ulong.TryParse(LeadingDigits(cl.Substring(li + 14)), out ulong lobbyId) && lobbyId != 0)
                    JoinLobbyById(lobbyId, "launch command line (+connect_lobby)");
                else if (cl.IndexOf("bamp:", StringComparison.OrdinalIgnoreCase) >= 0)
                    QueueJoin(cl.Substring(cl.IndexOf("bamp:", StringComparison.OrdinalIgnoreCase)), "launch command line");
                else if (cl.IndexOf("steam:", StringComparison.OrdinalIgnoreCase) >= 0)
                    QueueJoin(cl.Substring(cl.IndexOf("steam:", StringComparison.OrdinalIgnoreCase)), "launch command line");
                Plugin.Logger.LogInfo("[SteamJoin] lobby + rich-presence join hooks installed.");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[SteamJoin] hook: {ex.Message}"); }
        }

        /// <summary>Host is up — create the invite lobby + advertise presence.</summary>
        public static async void AdvertiseHosting()
        {
            try
            {
                if (!SteamClient.IsValid) return;
                SteamFriends.SetRichPresence("connect", $"steam:{(ulong)SteamClient.SteamId}");
                SteamFriends.SetRichPresence("status", $"Hosting {MyPluginInfo.SHORT_NAME} multiplayer");
                var created = await SteamMatchmaking.CreateLobbyAsync(16);
                if (created is { } lobby)
                {
                    lobby.SetFriendsOnly();
                    lobby.SetJoinable(true);
                    lobby.SetData(LobbyHostKey, ((ulong)SteamClient.SteamId).ToString());
                    _lobby = lobby;
                    Plugin.Logger.LogInfo($"[SteamJoin] invite lobby up ({lobby.Id}) — friends can Join Game / be invited.");
                }
                else Plugin.Logger.LogWarning("[SteamJoin] lobby create failed — invites limited to right-click Join Game.");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[SteamJoin] advertise: {ex.Message}"); }
        }

        public static void ClearAdvertise()
        {
            try { _lobby?.Leave(); } catch { }
            _lobby = null;
            try { if (SteamClient.IsValid) SteamFriends.ClearRichPresence(); } catch { }
        }

        /// <summary>Lobby "Invite Friends" — Steam's native invite dialog when the
        /// lobby is up; overlay friends list as the degraded fallback.</summary>
        public static void OpenInviteDialog()
        {
            try
            {
                if (_lobby is { } lobby) { SteamFriends.OpenGameInviteOverlay(lobby.Id); return; }
                Plugin.Logger.LogWarning("[SteamJoin] no invite lobby (create failed / not hosting) — opening friends list instead.");
                SteamFriends.OpenOverlay("friends");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[SteamJoin] invite dialog: {ex.Message}"); }
        }

        // ── friend side ──────────────────────────────────────────────────────

        private static async void ResolveLobby(Steamworks.Data.Lobby lobby, string via)
        {
            try
            {
                var enter = await lobby.Join();
                if (enter != RoomEnter.Success)
                { Plugin.Logger.LogWarning($"[SteamJoin] lobby join failed ({enter}) — {via}."); return; }
                string hostStr = lobby.GetData(LobbyHostKey) ?? "";
                try { lobby.Leave(); } catch { }   // rendezvous only — the session runs on the relay
                if (!ulong.TryParse(hostStr, out ulong hostId) || hostId == 0)
                { Plugin.Logger.LogWarning($"[SteamJoin] lobby {lobby.Id} carries no {LobbyHostKey} — not a {MyPluginInfo.SHORT_NAME} lobby? ({via})."); return; }
                PendingJoinVia = via;
                PendingJoinId  = hostId;
                Plugin.Logger.LogInfo($"[SteamJoin] queued join → {hostId} ({via}).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[SteamJoin] resolve lobby: {ex.Message}"); }
        }

        private static async void JoinLobbyById(ulong lobbyId, string via)
        {
            try
            {
                var joined = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
                if (joined is { } lobby) ResolveJoined(lobby, via);
                else Plugin.Logger.LogWarning($"[SteamJoin] could not join lobby {lobbyId} ({via}).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[SteamJoin] join lobby {lobbyId}: {ex.Message}"); }
        }

        private static void ResolveJoined(Steamworks.Data.Lobby lobby, string via)
        {
            string hostStr = lobby.GetData(LobbyHostKey) ?? "";
            try { lobby.Leave(); } catch { }
            if (!ulong.TryParse(hostStr, out ulong hostId) || hostId == 0)
            { Plugin.Logger.LogWarning($"[SteamJoin] lobby {lobby.Id} carries no {LobbyHostKey} ({via})."); return; }
            PendingJoinVia = via;
            PendingJoinId  = hostId;
            Plugin.Logger.LogInfo($"[SteamJoin] queued join → {hostId} ({via}).");
        }

        private static void QueueJoin(string connect, string via)
        {
            try
            {
                string s = (connect ?? "").Trim();
                if (QueueServer(s, via)) return;   // STEAM-FRIEND-JOIN-1
                int i = s.IndexOf("steam:", StringComparison.OrdinalIgnoreCase);
                if (i < 0) { Plugin.Logger.LogWarning($"[SteamJoin] unrecognized connect string '{connect}' ({via})."); return; }
                if (!ulong.TryParse(LeadingDigits(s.Substring(i + 6)), out ulong id) || id == 0)
                { Plugin.Logger.LogWarning($"[SteamJoin] bad SteamId in connect string '{connect}' ({via})."); return; }
                PendingJoinVia = via;
                PendingJoinId  = id;
                Plugin.Logger.LogInfo($"[SteamJoin] queued join → {id} ({via}).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[SteamJoin] queue: {ex.Message}"); }
        }

        // ── STEAM-FRIEND-JOIN-1: a dedicated server's players advertise it ──────────────────────────────────────────

        /// <summary>Main thread, every frame (works every 2 s): sets or withdraws this player's "connect" for the server he is on.</summary>
        public static void TickServerJoin()
        {
            if (UnityEngine.Time.unscaledTime < _nextServerTick) return;
            _nextServerTick = UnityEngine.Time.unscaledTime + 2f;
            if (MPServer.IsRunning) { _serverConnect = ""; return; }   // hosting: AdvertiseHosting's relay connect stands
            string want = "";
            try { want = ServerConnect(); } catch { }
            if (want == _serverConnect) return;
            try
            {
                if (!SteamClient.IsValid) return;
                SteamFriends.SetRichPresence("connect", want);   // "" takes it away
                bool was = _serverConnect.Length > 0;
                _serverConnect = want;
                if (want.Length > 0) Plugin.Logger.LogInfo("[SteamJoin] this player's Steam friends can join the server they are on (Join Game).");
                else if (was) Plugin.Logger.LogInfo("[SteamJoin] Join Game for the server withdrawn.");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[SteamJoin] presence: {ex.Message}"); }
        }

        /// <summary>The "connect" for the dedicated server this player is in ("" = none to give).</summary>
        private static string ServerConnect()
        {
            if (!MPClient.IsConnected || MPClient.LastConnectPath != "ip" || !MPClient.HostIsDedicated || !ServerRules.FromServer) return "";
            if (!ServerRules.Flag("SteamFriendJoin")) return "";
            string host = (MPConfig.HostIP ?? "").Trim();
            int port = MPClient.LastConnectPort;
            if (!Reachable(host) || port < 1024 || port > 65535) return "";
            string key = MPClient.HostNeedsKey ? MPConfig.JoinKey : "";
            if (!MPConfig.IsKey(key)) key = "";
            return "bamp:" + host + "/" + port + (key.Length > 0 ? "/" + key : "");
        }

        /// <summary>An address a friend elsewhere could reach: no loopback, private or link-local one, no local name.</summary>
        private static bool Reachable(string host)
        {
            if (!ValidHost(host)) return false;
            if (System.Net.IPAddress.TryParse(host, out var ip))
            {
                if (System.Net.IPAddress.IsLoopback(ip)) return false;
                var b = ip.GetAddressBytes();
                if (b.Length == 4) return !(b[0] == 10 || b[0] == 0 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254) || (b[0] == 100 && b[1] >= 64 && b[1] <= 127));
                return !(ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (b[0] & 0xFE) == 0xFC);
            }
            string h = host.ToLowerInvariant().TrimEnd('.');
            if (h == "localhost" || h.IndexOf('.') < 0) return false;
            foreach (var local in new[] { ".local", ".lan", ".home", ".internal", ".localdomain", ".home.arpa", ".localhost" })
                if (h.EndsWith(local, StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>A host name or an IP address - nothing else gets near a connect.</summary>
        private static bool ValidHost(string host)
        {
            if (host.Length == 0 || host.Length > 253) return false;
            foreach (char c in host) if (!(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == ':')) return false;
            return true;
        }

        /// <summary>A "bamp:" join (a Steam friend on a dedicated server): queued for the menu; true = it was one.</summary>
        private static bool QueueServer(string s, string via)
        {
            int i = s.IndexOf("bamp:", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return false;
            string rest = s.Substring(i + 5);
            int end = 0;
            while (end < rest.Length && !char.IsWhiteSpace(rest[end]) && rest[end] != '"') end++;
            var parts = rest.Substring(0, end).Split('/');
            string host = parts[0].Trim();
            int port = parts.Length > 1 && int.TryParse(parts[1], out int p) ? p : 0;
            string key = parts.Length > 2 ? parts[2].Trim() : "";
            if (!ValidHost(host) || port < 1024 || port > 65535 || (key.Length > 0 && !MPConfig.IsKey(key)))
            {
                Plugin.Logger.LogWarning($"[SteamJoin] a join request names no usable server ({via}).");
                return true;
            }
            PendingJoinVia  = via;
            PendingJoinKey  = key;
            PendingJoinPort = port;
            PendingJoinAddr = host;
            Plugin.Logger.LogInfo($"[SteamJoin] queued join → a Going Public server ({via}).");
            return true;
        }

        private static string LeadingDigits(string s)
        {
            s = s.TrimStart();
            int end = 0; while (end < s.Length && char.IsDigit(s[end])) end++;
            return s.Substring(0, end);
        }
    }
}
