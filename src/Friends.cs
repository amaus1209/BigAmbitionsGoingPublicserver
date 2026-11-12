using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Steamworks;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>
    /// FRIENDS-1 (Going Public Server 1.0.12, user-approved design 2026-10-30): who is a friend, the Steam name above every
    /// player, and friends on the city map.
    ///
    ///   * A FRIEND is a player who is my Steam friend (checked on THIS machine against my own Steam friends list, with the
    ///     SteamID64 each player sends in its PlayerProfile) or with whom I made an in-game friendship: one sends a request,
    ///     the other accepts (Business page, tab "Friends"). Both are mutual by nature, so two players always see each other
    ///     the same way.
    ///   * The HOST keeps the in-game friendships and open requests by STABLE id (a name change keeps them) in
    ///     &lt;data folder&gt;/friends.json, and tells every connected player its own state (FriendState) whenever it changes or
    ///     a player's profile arrives.
    ///   * Above every player: the character name, under it smaller the Steam name; friends in green.
    ///   * City map: a marker that follows every friend; other players are not shown.
    /// </summary>
    internal static class Friends
    {
        // ── every machine: Steam names and ids from the profiles ──────────────────────────────────────────────────────
        internal static readonly ConcurrentDictionary<string, string> SteamNames = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, ulong> _steamIds = new ConcurrentDictionary<string, ulong>();

        // ── this machine's in-game friendships (from the host) ──────────────────────────────────────────────────────
        private static volatile HashSet<string> _friends = new HashSet<string>(), _incoming = new HashSet<string>(), _outgoing = new HashSet<string>();
        /// <summary>Bumped on every change the UI shows (state from the host, a Steam friendship found).</summary>
        internal static int Version;

        internal static bool IsGameFriend(string pid) => _friends.Contains(pid);
        internal static bool IsIncoming(string pid) => _incoming.Contains(pid);
        internal static bool IsOutgoing(string pid) => _outgoing.Contains(pid);
        internal static int IncomingCount => _incoming.Count;
        internal static bool IsFriend(string pid) => IsGameFriend(pid) || IsSteamFriend(pid);

        /// <summary>This machine's Steam name and id for its PlayerProfile ("" when Steam is not running). Main thread.</summary>
        internal static void OwnSteam(out string name, out string id)
        {
            name = ""; id = "";
            try
            {
                if (!SteamClient.IsValid) return;
                name = SteamClient.Name ?? "";
                id = SteamClient.SteamId.Value.ToString();
            }
            catch { name = ""; id = ""; }
        }

        /// <summary>Any thread: a PlayerProfile arrived (host and clients).</summary>
        internal static void NoteProfile(PlayerProfilePayload p)
        {
            if (p == null || string.IsNullOrEmpty(p.PlayerId)) return;
            if (!string.IsNullOrWhiteSpace(p.SteamName)) SteamNames[p.PlayerId] = p.SteamName.Trim();
            if (ulong.TryParse(p.SteamId ?? "", out var id) && id != 0)
            {
                if (!_steamIds.TryGetValue(p.PlayerId, out var old) || old != id) _steamFriend.TryRemove(p.PlayerId, out _);
                _steamIds[p.PlayerId] = id;
            }
            Version++;
        }

        // Steam friendship per player id, re-checked every 30 s (my Steam friends list can change while playing).
        private static readonly ConcurrentDictionary<string, (bool friend, float at)> _steamFriend = new ConcurrentDictionary<string, (bool, float)>();

        /// <summary>Is this player my Steam friend? Main thread (Steam API); off the main thread the last answer.</summary>
        internal static bool IsSteamFriend(string pid)
        {
            if (string.IsNullOrEmpty(pid) || !_steamIds.TryGetValue(pid, out var id)) return false;
            float now;
            try { now = Time.unscaledTime; } catch { return _steamFriend.TryGetValue(pid, out var c0) && c0.friend; }
            if (_steamFriend.TryGetValue(pid, out var c) && now - c.at < 30f) return c.friend;
            bool f = false;
            try { f = SteamClient.IsValid && id != SteamClient.SteamId.Value && new Friend(id).IsFriend; } catch { f = false; }
            if (!_steamFriend.TryGetValue(pid, out var prev) || prev.friend != f) Version++;
            _steamFriend[pid] = (f, now);
            return f;
        }

        /// <summary>Any thread: the host's FriendState for this machine.</summary>
        internal static void ApplyState(FriendStatePayload p)
        {
            if (p == null) return;
            var oldIn = _incoming; var oldFr = _friends;
            var fr = new HashSet<string>(p.Friends ?? new List<string>());
            var inc = new HashSet<string>(p.Incoming ?? new List<string>());
            _friends = fr; _incoming = inc; _outgoing = new HashSet<string>(p.Outgoing ?? new List<string>());
            Version++;
            var newReq = new List<string>(); var newFr = new List<string>();
            foreach (var x in inc) if (!oldIn.Contains(x)) newReq.Add(x);
            foreach (var x in fr) if (!oldFr.Contains(x)) newFr.Add(x);
            GameStatePatcher.EnqueueOnMainThread(() =>
            {
                foreach (var x in newReq) Notice(UI.Notification.NotificationType.Info, Loc.T("gps_friend_request", "{0} wants to be your friend (Business page, tab Friends).", Name(x)));
                foreach (var x in newFr) Notice(UI.Notification.NotificationType.Success, Loc.T("gps_friend_added", "{0} is now your friend.", Name(x)));
                RemotePlayerManager.RefreshAllLabels();
            });
        }

        private static void Notice(UI.Notification.NotificationType type, string text)
        {
            try { UI.Notification.Notifications.Show(type, text); } catch { }
        }

        internal static string Name(string pid) => RemotePlayerManager.ResolveDisplayName(pid);

        // ── FRIEND-BUILDINGS-1 (user 2026-10-31: with a friend, the Friends tab shows the buildings it rents and bought) ──
        private static volatile PlayerBuildingsPayload _buildings = new PlayerBuildingsPayload();

        /// <summary>Any thread: the host's list of who rents and bought what.</summary>
        internal static void ApplyBuildings(PlayerBuildingsPayload? p)
        {
            if (p == null) return;
            _buildings = p;
            Version++;
        }

        internal static IReadOnlyList<string> RentedOf(string pid) => _buildings.Rented != null && _buildings.Rented.TryGetValue(pid, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();
        internal static IReadOnlyList<string> OwnedOf(string pid) => _buildings.Owned != null && _buildings.Owned.TryGetValue(pid, out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();

        private static string _buildingsSig = "";
        private static float _buildingsSentAt = -999f;

        /// <summary>HOST, main thread, once a second (DedicatedHost.Tick): the connected players' rented and bought
        /// addresses to everyone - when they change, else every 30 s for the ones who joined since.</summary>
        internal static void HostTickBuildings()
        {
            try
            {
                if (!MPServer.IsRunning) return;
                var online = new HashSet<string>(Players());
                var p = new PlayerBuildingsPayload();
                void Collect(IEnumerable<KeyValuePair<string, string>> ledger, Dictionary<string, List<string>> into)
                {
                    foreach (var kv in ledger)
                    {
                        if (string.IsNullOrEmpty(kv.Key) || !online.Contains(kv.Value)) continue;
                        if (!into.TryGetValue(kv.Value, out var l)) into[kv.Value] = l = new List<string>();
                        l.Add(kv.Key);
                    }
                    foreach (var l in into.Values) l.Sort(StringComparer.OrdinalIgnoreCase);
                }
                Collect(MPServer.BuildingOwners, p.Rented);
                Collect(MPServer.BuildingRealEstateOwners, p.Owned);
                string sig = JsonConvert.SerializeObject(p);
                float now = Time.unscaledTime;
                if (sig == _buildingsSig && now - _buildingsSentAt < 30f) return;
                _buildingsSig = sig; _buildingsSentAt = now;
                var env = MessageEnvelope.Create(MessageType.PlayerBuildings, "host", p);
                foreach (var q in online) if (q != MPConfig.PlayerId) MPServer.SendToPlayer(q, env);
                if (!MPConfig.DedicatedHost) ApplyBuildings(p);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Friends] buildings: {ex.Message}"); }
        }

        /// <summary>Main thread: a click in the Friends tab. add | accept | decline | cancel | remove.</summary>
        internal static void Act(string action, string targetPid)
        {
            if (string.IsNullOrEmpty(targetPid)) return;
            if (MPServer.IsRunning) HostAction(MPConfig.PlayerId, action, targetPid);
            else if (MPClient.IsConnected) MPClient.SendFriendAction(action, targetPid);
        }

        /// <summary>A session ended: in-game state and Steam answers go (the next host sends its own).</summary>
        internal static void Reset()
        {
            _friends = new HashSet<string>(); _incoming = new HashSet<string>(); _outgoing = new HashSet<string>();
            _steamFriend.Clear();
            Version++;
            FriendMapMarkers.Clear();
        }

        // ══ HOST ═════════════════════════════════════════════════════════════════════════════════════════════════════
        private sealed class Store
        {
            public List<string> Pairs = new List<string>();      // "stableA|stableB", A < B (ordinal)
            public List<string> Requests = new List<string>();   // "fromStable>toStable"
        }
        private static Store? _store;
        private static readonly object _lock = new object();

        private static string StorePath => Path.Combine(MPConfig.DataRootPath, "friends.json");

        private static Store Load()
        {
            if (_store != null) return _store;
            try { if (File.Exists(StorePath)) _store = JsonConvert.DeserializeObject<Store>(File.ReadAllText(StorePath)); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Friends] {StorePath} unreadable ({ex.Message}) - starting empty."); }
            return _store ??= new Store();
        }

        private static void Save()
        {
            try
            {
                string tmp = StorePath + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(_store, Formatting.Indented));
                if (File.Exists(StorePath)) File.Delete(StorePath);
                File.Move(tmp, StorePath);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Friends] saving {StorePath}: {ex.Message}"); }
        }

        private static string Stable(string pid)
        {
            if (pid == MPConfig.PlayerId) return MPConfig.StableId ?? pid;
            return MPServer.StableIdByPlayer.TryGetValue(pid, out var s) && !string.IsNullOrEmpty(s) ? s : pid;
        }

        private static string Pair(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;

        /// <summary>The players that can have friends here: connected, plus the host itself unless it is a dedicated server.</summary>
        private static List<string> Players()
        {
            var all = new List<string>(MPServer.ConnectedPids());
            if (!MPConfig.DedicatedHost && !all.Contains(MPConfig.PlayerId)) all.Add(MPConfig.PlayerId);
            all.RemoveAll(p => string.IsNullOrEmpty(p) || (MPConfig.DedicatedHost && p == MPConfig.PlayerId));
            return all;
        }

        /// <summary>HOST, any thread: actor (the sending connection's player, never the payload) acts on target.</summary>
        internal static void HostAction(string actor, string action, string target)
        {
            if (string.IsNullOrEmpty(actor) || string.IsNullOrEmpty(target) || actor == target) return;
            var players = Players();
            if (!players.Contains(actor) || !players.Contains(target)) { Plugin.Logger.LogInfo($"[Friends] '{action}' from '{actor}' to '{target}' ignored - not both in the session."); return; }
            string a = Stable(actor), t = Stable(target), pair = Pair(a, t), mine = a + ">" + t, theirs = t + ">" + a;
            lock (_lock)
            {
                var st = Load();
                bool changed = false;
                switch (action)
                {
                    case "add":
                    case "accept":
                        if (st.Pairs.Contains(pair)) break;
                        if (st.Requests.Contains(theirs))   // they asked first (or this is the answer): friends
                        {
                            st.Requests.Remove(theirs); st.Requests.Remove(mine); st.Pairs.Add(pair); changed = true;
                        }
                        else if (action == "add" && !st.Requests.Contains(mine)) { st.Requests.Add(mine); changed = true; }
                        break;
                    case "decline": changed = st.Requests.Remove(theirs); break;
                    case "cancel": changed = st.Requests.Remove(mine); break;
                    case "remove": changed = st.Pairs.Remove(pair); break;
                }
                if (!changed) return;
                Save();
            }
            Plugin.Logger.LogInfo($"[Friends] '{actor}' {action} '{target}'.");
            HostSendState(actor);
            HostSendState(target);
        }

        /// <summary>HOST, any thread: tell every player its state (a profile arrived = someone joined or changed name).</summary>
        internal static void HostSendStateAll()
        {
            foreach (var p in Players()) HostSendState(p);
        }

        private static void HostSendState(string pid)
        {
            var players = Players();
            if (!players.Contains(pid)) return;
            var ps = new FriendStatePayload();
            string me = Stable(pid);
            lock (_lock)
            {
                var st = Load();
                foreach (var q in players)
                {
                    if (q == pid) continue;
                    string s = Stable(q);
                    if (st.Pairs.Contains(Pair(me, s))) ps.Friends.Add(q);
                    else if (st.Requests.Contains(s + ">" + me)) ps.Incoming.Add(q);
                    else if (st.Requests.Contains(me + ">" + s)) ps.Outgoing.Add(q);
                }
            }
            if (pid == MPConfig.PlayerId) ApplyState(ps);
            else MPServer.SendToPlayer(pid, MessageEnvelope.Create(MessageType.FriendState, "host", ps));
        }
    }

    /// <summary>FRIENDS-1: a city-map marker that follows every friend (CityMap.AddPoi, the game's own points of
    /// interest); other players get none. Main thread, MPCanvasUI.Update, once a second.</summary>
    internal static class FriendMapMarkers
    {
        private static readonly Dictionary<string, PointOfInterest> _pois = new Dictionary<string, PointOfInterest>();
        private static float _nextAt;
        private static Sprite? _icon;
        private static readonly Color Green = new Color(0.20f, 0.70f, 0.35f, 1f);

        internal static void Tick()
        {
            if (Time.unscaledTime < _nextAt) return;
            _nextAt = Time.unscaledTime + 1f;
            try
            {
                var map = InstanceBehavior<CityManager>.Instance?.cityMap;
                if (map == null || (!MPServer.IsRunning && !MPClient.IsConnected)) { Clear(); return; }
                var seen = new HashSet<string>();
                foreach (var kv in RemotePlayerManager.Spawned())
                {
                    string pid = kv.Key;
                    if (kv.Value == null || pid == MPClient.DedicatedHostPlayerId || !Friends.IsFriend(pid)) continue;
                    seen.Add(pid);
                    string name = Friends.Name(pid);
                    if (_pois.TryGetValue(pid, out var poi) && poi != null)
                    {
                        if (poi.target != kv.Value.transform) poi.target = kv.Value.transform;
                        poi.SetText(name);
                        continue;
                    }
                    _icon ??= MPPhoneButton.LoadIconFile("BAMP_HubIcon.png");
                    poi = map.AddPoi(kv.Value.transform, _icon!, Green, name);
                    poi.offset = new Vector3(0f, 2.2f, 0f);
                    if (CityMap.IsOpen) poi.gameObject.SetActive(true);   // AddPoi leaves it hidden until the next map open
                    map.UpdateNonePermanentPointOfInterests();
                    _pois[pid] = poi;
                    Plugin.Logger.LogInfo($"[Friends] map marker for '{pid}'.");
                }
                foreach (var pid in new List<string>(_pois.Keys))
                    if (!seen.Contains(pid)) Drop(pid);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Friends] map markers: {ex.Message}"); }
        }

        private static void Drop(string pid)
        {
            if (_pois.TryGetValue(pid, out var poi) && poi != null) UnityEngine.Object.Destroy(poi.gameObject);   // its OnDestroy leaves the map's list
            _pois.Remove(pid);
        }

        internal static void Clear()
        {
            foreach (var pid in new List<string>(_pois.Keys)) { try { Drop(pid); } catch { } }
            _pois.Clear();
        }
    }
}
