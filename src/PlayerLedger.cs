using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>
    /// DEDICATED-RANK-1: every player who ever played on this dedicated server, for the ranking page next to the
    /// telemetry monitor. HOST side, main thread (DedicatedHost.Tick, once a second).
    ///
    /// Keyed by the stable id (the same player keeps one row across name changes). Per row: character name, first and
    /// last visit, total online time, sessions, and from the host's own records the last known cash
    /// (MPServer.CashByStableId), businesses run (MPServer.BuildingOwners) and buildings bought
    /// (MPServer.BuildingRealEstateOwners). Kept in &lt;headless&gt;/players.json; a public copy without ids goes to
    /// &lt;root&gt;/telemetry-data/web/ranking.json, which ranking.html reads. Written every 60 s, at a leave and at quit.
    /// </summary>
    internal static class PlayerLedger
    {
        internal sealed class Row
        {
            public string StableId = "";
            public string Name = "";
            public long FirstSeenUnix;
            public long LastSeenUnix;
            public double OnlineSeconds;
            public int Sessions;
            public float Cash = -1f;
            public int Businesses;
            public int BuildingsOwned;
            public string MergedWith = "";   // RANK-MERGER-1: the other members' names of its merged company ("" = none)
            public string Notice = "";       // MERGER-AUTO-DISSOLVE-1: told to the player the next time he is on (never public)
            public double AwaySeconds;       // MERGER-AUTO-DISSOLVE-1: away while the server's world ran (0 while he is on)
            // GOING-ONLINE-RANK-1: the player's cable provider (module 8) as last reported ("" = none), and since when
            public string Provider = "";
            public int ProviderCustomers, ProviderBuildings, ProviderHoods, ProviderDay;
            public float ProviderReputation;
            public long ProviderSinceUnix;
        }

        private static Dictionary<string, Row>? _rows;
        private static readonly HashSet<string> _online = new HashSet<string>();   // stable ids present at the last tick
        private static readonly Dictionary<string, float> _onlineSince = new Dictionary<string, float>();   // MERGER-AUTO-DISSOLVE-1
        private static float _lastTickAt = -1f, _nextWriteAt;
        private static bool _dirty, _failedLogged;

        private static string LedgerPath => Path.Combine(HeadlessHost.DirPath, "players.json");
        // DEDICATED-KEY-1: the public copy sits in a folder named after the server key (AMP does not list folders), so
        // only players who have the key see the ranking.
        private static string PublicPath => PublicFile("ranking.json");

        /// <summary>A file of the ranking page's data folder (NET-PAGE-1's net.json sits next to ranking.json).</summary>
        internal static string PublicFile(string name)
        {
            string web = Path.Combine(Path.GetDirectoryName(HeadlessHost.DirPath.TrimEnd('/', '\\')) ?? "", "telemetry-data", "web");
            string key = DedicatedHost.ServerJoinKey();
            return key.Length > 0 ? Path.Combine(web, DedicatedHost.KeyFolder(key), name) : Path.Combine(web, name);
        }

        /// <summary>Main thread, ~once a second, on a dedicated headless host with a world running.</summary>
        public static void Tick()
        {
            if (!HeadlessHost.Active || HeadlessHost.DirPath.Length == 0) return;
            try
            {
                Load();
                float now = Time.realtimeSinceStartup;
                double dt = _lastTickAt < 0f ? 0 : Math.Min(10.0, now - _lastTickAt);
                _lastTickAt = now;
                long unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var present = new HashSet<string>();
                if (MPServer.IsRunning && MPServer.WorldRunning)
                {
                    foreach (var pid in MPServer.LobbyPlayers)
                    {
                        if (pid == MPConfig.PlayerId) continue;   // the server's own character
                        string stable = MPServer.StableIdByPlayer.TryGetValue(pid, out var s) && !string.IsNullOrEmpty(s) ? s : "pid:" + pid;
                        present.Add(stable);
                        if (!_rows!.TryGetValue(stable, out var r)) { r = new Row { StableId = stable, FirstSeenUnix = unix }; _rows[stable] = r; }
                        if (!_online.Contains(stable)) { r.Sessions++; _dirty = true; _onlineSince[stable] = now; }
                        // MERGER-AUTO-DISSOLVE-1: a notice kept for him - two minutes after he came, so his world is up
                        if (r.Notice.Length > 0 && _onlineSince.TryGetValue(stable, out var since) && now - since >= 120f)
                        {
                            MPServer.NoticePlayer(pid, r.Notice);
                            r.Notice = "";
                            _dirty = true;
                        }
                        r.LastSeenUnix = unix;
                        r.OnlineSeconds += dt;
                        string name = "";
                        try { name = MPNames.Resolve(pid); } catch { }
                        if (!string.IsNullOrWhiteSpace(name)) r.Name = name;
                        if (MPServer.CashByStableId.TryGetValue(stable, out var cash)) r.Cash = cash;
                        r.Businesses = MPServer.BuildingOwners.Count(kv => kv.Value == pid);
                        if (MPServer.ProviderByStableId.TryGetValue(stable, out var prov))
                        {
                            string company = prov.Company ?? "";
                            if (company != r.Provider)
                            {
                                if (company.Length > 0)
                                {
                                    r.ProviderSinceUnix = unix;
                                    Plugin.Logger.LogInfo($"[Ranking] {(string.IsNullOrWhiteSpace(r.Name) ? "?" : r.Name)} runs the cable provider '{company}' now.");
                                }
                                r.Provider = company;
                            }
                            r.ProviderCustomers = prov.Customers; r.ProviderBuildings = prov.Buildings; r.ProviderHoods = prov.Hoods;
                            r.ProviderReputation = prov.Reputation; r.ProviderDay = prov.FoundedDay;
                        }
                        r.BuildingsOwned = MPServer.BuildingRealEstateOwners.Count(kv => kv.Value == pid);
                    }
                    // RANK-MERGER-1 (user 2026-10-31: a merger shows in the ranking - merged players share e.g. the cash):
                    // every row, online or not, names the other members of its company from the host's merger store
                    foreach (var r in _rows!.Values)
                    {
                        string g = MergerSync.GroupOfStable(r.StableId);
                        var others = new List<string>();
                        if (g.Length > 0)
                            foreach (var m in MergerSync.JoinOrderOfGroup(g))
                                if (m != r.StableId) others.Add(_rows.TryGetValue(m, out var o) && !string.IsNullOrWhiteSpace(o.Name) ? o.Name : m);
                        string merged = string.Join(", ", others);
                        if (merged != r.MergedWith) { r.MergedWith = merged; _dirty = true; }
                    }
                }
                // MERGER-AUTO-DISSOLVE-1: the time away counts only while the server runs a world - a server that was off for a
                // week did not keep anybody away; written to disk once an hour of it is full (the file stays quiet while empty)
                bool worldUp = MPServer.IsRunning && MPServer.WorldRunning;
                foreach (var r in _rows!.Values)
                {
                    if (present.Contains(r.StableId)) { if (r.AwaySeconds > 0) { r.AwaySeconds = 0; _dirty = true; } continue; }
                    if (!worldUp || dt <= 0) continue;
                    long hourBefore = (long)(r.AwaySeconds / 3600.0);
                    r.AwaySeconds += dt;
                    if ((long)(r.AwaySeconds / 3600.0) != hourBefore) _dirty = true;
                }
                bool someoneLeft = _online.Any(id => !present.Contains(id));
                _online.Clear(); _online.UnionWith(present);
                if (present.Count > 0) _dirty = true;
                if (_dirty && (someoneLeft || now >= _nextWriteAt)) Save();
            }
            catch (Exception ex)
            {
                if (!_failedLogged) { _failedLogged = true; Plugin.Logger.LogWarning($"[Ranking] {ex.Message}"); }
            }
        }

        /// <summary>BUILDING-LIST-1: an owner value of MPServer.BuildingOwners / BuildingRealEstateOwners (a player id while he
        /// is on, his stable id while he is away) as the player's name in the ledger; "" for the server's own character and
        /// for nobody known (never an id - the list is public to whoever has the key).</summary>
        internal static string OwnerName(string owner)
        {
            if (string.IsNullOrEmpty(owner) || owner == "host" || owner == MPConfig.PlayerId) return "";
            Load();
            string stable = MPServer.StableIdByPlayer.TryGetValue(owner, out var s) && !string.IsNullOrEmpty(s) ? s : owner;
            if (!_rows!.TryGetValue(stable, out var row) && !_rows.TryGetValue("pid:" + owner, out row)) return "";
            return string.IsNullOrWhiteSpace(row.Name) ? "?" : row.Name;
        }

        /// <summary>MERGER-AUTO-DISSOLVE-1: how long this player has been away, in days while the server ran a world (0 while he
        /// is on); -1 for nobody known. Counted from 1.0.16.3 on - a row from before starts at 0.</summary>
        internal static double AwayDays(string stable)
        {
            Load();
            return !string.IsNullOrEmpty(stable) && _rows!.TryGetValue(stable, out var r) ? r.AwaySeconds / 86400.0 : -1;
        }

        /// <summary>MERGER-AUTO-DISSOLVE-1: the player's name in the ledger ("?" for nobody known - never the id).</summary>
        internal static string NameOfStable(string stable)
        {
            Load();
            return !string.IsNullOrEmpty(stable) && _rows!.TryGetValue(stable, out var r) && !string.IsNullOrWhiteSpace(r.Name) ? r.Name : "?";
        }

        /// <summary>MERGER-AUTO-DISSOLVE-1: a notice for an absent player, told when he is back (kept in players.json).</summary>
        internal static void SetNotice(string stable, string text)
        {
            Load();
            if (string.IsNullOrEmpty(stable) || !_rows!.TryGetValue(stable, out var r)) return;
            r.Notice = text ?? "";
            _dirty = true;
            Save();
        }

        /// <summary>At quit: write what we have.</summary>
        public static void Flush()
        {
            try { if (_rows != null && _dirty) Save(); } catch { }
        }

        private static void Load()
        {
            if (_rows != null) return;
            _rows = new Dictionary<string, Row>();
            try
            {
                if (File.Exists(LedgerPath))
                    foreach (var r in JsonConvert.DeserializeObject<List<Row>>(File.ReadAllText(LedgerPath)) ?? new List<Row>())
                        if (!string.IsNullOrEmpty(r.StableId)) _rows[r.StableId] = r;
                Plugin.Logger.LogInfo($"[Ranking] {_rows.Count} player(s) in {LedgerPath}.");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Ranking] could not read {LedgerPath} ({ex.Message}) - starting a new list."); }
        }

        private static void Save()
        {
            _dirty = false;
            _nextWriteAt = Time.realtimeSinceStartup + 60f;
            var rows = _rows!.Values.OrderByDescending(r => r.OnlineSeconds).ToList();
            WriteAtomic(LedgerPath, JsonConvert.SerializeObject(rows, Formatting.Indented));
            string dir = Path.GetDirectoryName(PublicPath) ?? "";
            if (dir.Length > 0 && Directory.Exists(Path.GetDirectoryName(dir) ?? "")) Directory.CreateDirectory(dir);
            if (dir.Length > 0 && Directory.Exists(dir))
            {
                var pub = new
                {
                    updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    server = MyPluginInfo.SHORT_NAME + " " + MyPluginInfo.PLUGIN_VERSION,
                    players = rows.Select(r => new
                    {
                        name = string.IsNullOrWhiteSpace(r.Name) ? "?" : r.Name,
                        online = _online.Contains(r.StableId),
                        hours = Math.Round(r.OnlineSeconds / 3600.0, 2),
                        sessions = r.Sessions,
                        firstSeen = r.FirstSeenUnix,
                        lastSeen = r.LastSeenUnix,
                        cash = r.Cash < 0 ? (float?)null : (float)Math.Round(r.Cash),
                        businesses = r.Businesses,
                        buildings = r.BuildingsOwned,
                        merged = r.MergedWith ?? "",
                        provider = string.IsNullOrEmpty(r.Provider) ? null : new
                        {
                            name = r.Provider,
                            customers = r.ProviderCustomers,
                            buildings = r.ProviderBuildings,
                            hoods = r.ProviderHoods,
                            reputation = Math.Round(r.ProviderReputation),
                            day = r.ProviderDay,
                            since = r.ProviderSinceUnix,
                        },
                    }),
                };
                WriteAtomic(PublicPath, JsonConvert.SerializeObject(pub));
            }
        }

        internal static void WriteAtomic(string path, string text)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }
}
