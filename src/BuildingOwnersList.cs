using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>BUILDING-LIST-1 (user 2026-11-03: "see on the web page which buildings are bought by which player" - first as a
    /// map, then "without the map, the list is enough"): a dedicated server writes buildings.json for the ranking page, next to
    /// ranking.json in the key's folder, every 20 s when something changed (and every 5 min anyway, for "updated"): per
    /// building of a player its address in the server's language, the business's name, the player who bought it
    /// (MPServer.BuildingRealEstateOwners) and the one who rents or runs it (BuildingOwners). The server's own character
    /// appears nowhere. Only on the server; ranking.html shows the list.</summary>
    internal static class BuildingOwnersList
    {
        private const float Every = 20f, Anyway = 300f;
        private static string _last = "";
        private static float _nextAt, _wroteAt = -1e9f;
        private static bool _warned, _loaded, _dirty;

        /// <summary>BUILDING-LIST-2: each player's last own report (stable id -> buildings), kept across restarts in
        /// headless/ownership.json (not public: the list shows names only).</summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<OwnedBuildingInfo>> _reports = new();
        private static string StorePath => Path.Combine(HeadlessHost.DirPath, "ownership.json");

        /// <summary>A player's report (network thread): the buildings as their own game has them, cleaned up.</summary>
        internal static void Record(string playerId, OwnershipReportPayload p)
        {
            if (string.IsNullOrEmpty(playerId) || p?.Buildings == null) return;
            string stable = MPServer.StableIdByPlayer.TryGetValue(playerId, out var s) && !string.IsNullOrEmpty(s) ? s : playerId;
            var list = new List<OwnedBuildingInfo>();
            foreach (var b in p.Buildings)
            {
                if (b == null || string.IsNullOrWhiteSpace(b.Key) || b.Key.Length > 80 || (!b.Bought && !b.Rented)) continue;
                var name = new System.Text.StringBuilder();
                foreach (char ch in (b.Name ?? "").Trim()) { if (!char.IsControl(ch)) name.Append(ch); if (name.Length >= 60) break; }
                list.Add(new OwnedBuildingInfo { Key = b.Key, Name = name.ToString(), Bought = b.Bought, Rented = b.Rented });
                if (list.Count >= 300) break;
            }
            _reports[stable] = list;
            _dirty = true;
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(StorePath)) return;
                var d = JsonConvert.DeserializeObject<Dictionary<string, List<OwnedBuildingInfo>>>(File.ReadAllText(StorePath));
                if (d != null) foreach (var kv in d) if (kv.Value != null) _reports.TryAdd(kv.Key, kv.Value);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Buildings] could not read {StorePath}: {ex.Message}"); }
        }

        /// <summary>Main thread, ~once a second on a dedicated headless host (DedicatedHost).</summary>
        internal static void Tick()
        {
            if (!HeadlessHost.Active || !MPServer.IsRunning || !MPServer.WorldRunning) return;
            float now = Time.realtimeSinceStartup;
            if (now < _nextAt) return;
            _nextAt = now + Every;
            try { Write(now); }
            catch (Exception ex)
            {
                if (!_warned) { _warned = true; Plugin.Logger.LogWarning($"[Buildings] {ex.Message}"); }
            }
        }

        private static void Write(float now)
        {
            var rows = new SortedDictionary<string, string[]>(StringComparer.Ordinal);   // key -> [address, business, bought by, rented by]
            void Add(string key, string owner, int slot)
            {
                string name = PlayerLedger.OwnerName(owner);
                if (name.Length == 0) return;
                if (!rows.TryGetValue(key, out var r)) rows[key] = r = new[] { key, "", "", "" };
                r[slot] = name;
            }
            // BUILDING-LIST-2: a player who reported shows exactly what their own game has; the host's maps only stand in for
            // players who have not reported since 1.0.17
            Load();
            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in _reports)
            {
                string name = PlayerLedger.NameOfStable(kv.Key);
                if (name.Length == 0 || name == "?") continue;
                reported.Add(kv.Key);
                foreach (var b in kv.Value)
                {
                    if (!rows.TryGetValue(b.Key, out var r)) rows[b.Key] = r = new[] { b.Key, "", "", "" };
                    if (b.Name.Length > 0) r[1] = b.Name;
                    if (b.Bought) r[2] = name;
                    if (b.Rented) r[3] = name;
                }
            }
            bool Reported(string owner)
            {
                string st = MPServer.StableIdByPlayer.TryGetValue(owner, out var x) && !string.IsNullOrEmpty(x) ? x : owner;
                return reported.Contains(st);
            }
            foreach (var kv in MPServer.BuildingRealEstateOwners) if (!Reported(kv.Value)) Add(kv.Key, kv.Value, 2);
            foreach (var kv in MPServer.BuildingOwners) if (!Reported(kv.Value)) Add(kv.Key, kv.Value, 3);
            if (rows.Count > 0) Names(rows);
            if (_dirty)
            {
                _dirty = false;
                try { PlayerLedger.WriteAtomic(StorePath, JsonConvert.SerializeObject(new Dictionary<string, List<OwnedBuildingInfo>>(_reports), Formatting.None)); }
                catch (Exception ex) { Plugin.Logger.LogWarning($"[Buildings] could not save {StorePath}: {ex.Message}"); }
            }
            string body = JsonConvert.SerializeObject(rows.Values);
            if (body == _last && now - _wroteAt < Anyway) return;
            string path = PlayerLedger.PublicFile("buildings.json");
            string dir = Path.GetDirectoryName(path) ?? "";
            if (dir.Length == 0) return;
            if (!Directory.Exists(dir) && Directory.Exists(Path.GetDirectoryName(dir) ?? "")) Directory.CreateDirectory(dir);
            if (!Directory.Exists(dir)) return;
            PlayerLedger.WriteAtomic(path, "{\"updatedUnix\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"buildings\":" + body + "}");
            _last = body;
            _wroteAt = now;
        }

        /// <summary>The address in the server's language and the business's name, from the city's building (a building not
        /// found keeps its address key).</summary>
        private static void Names(SortedDictionary<string, string[]> rows)
        {
            var cm = InstanceBehavior<CityManager>.Instance;
            if (cm == null || cm.cityBuildingControllers == null) return;
            foreach (var cbc in cm.cityBuildingControllers)
            {
                if (cbc == null || cbc.buildingRegistration == null) continue;
                var reg = cbc.buildingRegistration;
                if (!rows.TryGetValue(GameStateReader.AddressKey(reg), out var r)) continue;
                try
                {
                    string a = cbc.building != null ? Streets.AddressHelper.ToFormattedString(cbc.building.Address) : "";
                    if (!string.IsNullOrWhiteSpace(a)) r[0] = a;
                }
                catch { }
                if (r[1].Length == 0 && !string.IsNullOrWhiteSpace(reg.BusinessName)) r[1] = reg.BusinessName;   // the owner's report names it first
            }
        }
    }

    /// <summary>BUILDING-LIST-2: the player's side - once a minute, and at once when something changed, the buildings this
    /// player bought or rents in their own game, with the business names, go to the server (OwnershipReportPayload).</summary>
    internal static class OwnershipReporter
    {
        private static float _nextAt, _sentAt = -1e9f;
        private static string _last = "";

        /// <summary>Main thread, every frame while in a multiplayer game (MPCanvasUI).</summary>
        internal static void Tick()
        {
            if (!MPClient.IsConnected || MPServer.IsRunning) return;
            float now = Time.unscaledTime;
            if (now < _nextAt) return;
            _nextAt = now + 10f;
            try
            {
                var game = SaveGameManager.Current;
                if (game?.BuildingRegistrations == null) return;
                var p = new OwnershipReportPayload { PlayerId = MPConfig.PlayerId };
                var sig = new System.Text.StringBuilder();
                foreach (var reg in game.BuildingRegistrations)
                {
                    if (reg == null) continue;
                    bool bought = reg.BuildingOwnedByPlayer, rented = reg.RentedByPlayer;
                    if (!bought && !rented) continue;
                    string key = GameStateReader.AddressKey(reg);
                    string name = reg.businessTypeName == null || reg.businessTypeName == "ba:businesstype_empty" ? "" : (reg.BusinessName ?? "");
                    p.Buildings.Add(new OwnedBuildingInfo { Key = key, Name = name, Bought = bought, Rented = rented });
                    sig.Append(key).Append('|').Append(name).Append('|').Append(bought).Append(rented).Append(';');
                    if (p.Buildings.Count >= 300) break;
                }
                string now2 = sig.ToString();
                if (now2 == _last && now - _sentAt < 60f) return;
                _last = now2; _sentAt = now;
                MPClient.SendOwnershipReport(p);
            }
            catch { }
        }
    }
}
