using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Steamworks;

namespace BigAmbitionsMP
{
    /// <summary>
    /// Holds runtime connection settings.
    /// PlayerId is auto-detected from Steam on first run; settings persist in
    /// a JSON file inside the mod's own folder (official loader's
    /// ModRootPath) — the BepInEx config system is gone with the Mono port.
    /// </summary>
    public static class MPConfig
    {
        public sealed class BugReportDiscordTag
        {
            public string Label = "";
            public string Id = "";
        }

        public static string PlayerId { get; private set; } = "Player1";
        public static string HostIP   { get; private set; } = "127.0.0.1";
        public static int    Port     { get; private set; } = 7777;
        /// <summary>Lobby port option A (user-approved 2026-09-28): the HOSTING port, its own key. Joins keep Port/HostIP
        /// (the last address joined); hosting reads only this, so joining a friend on 8000 no longer makes you host on 8000.
        /// First launch with this key missing copies Port (a hand-edited Port survives). Saved only by the lobby's Change
        /// port (the port the player CHOSE - never a busy-port fallback, never a Steam 0).</summary>
        public static int    HostPort { get; private set; } = 7777;

        private static string? _cachedLanIp;
        /// <summary>This machine's best-guess LAN IPv4 — the address other players on
        /// the SAME network use to join (e.g. 192.168.x.x).  Detected locally from the
        /// network adapters; makes NO external calls.  Returns "" if none is found
        /// (callers fall back to the configured HostIP).  For internet play the host
        /// still needs their PUBLIC ip + a forwarded port; this is the LAN address.</summary>
        public static string LocalLanIp()
        {
            if (_cachedLanIp != null) return _cachedLanIp;
            _cachedLanIp = "";
            try
            {
                // SCORE candidates rather than taking the first match: a machine with
                // a VPN / VirtualBox / Hamachi / WSL adapter would otherwise hand out
                // that adapter's address instead of the real Wi-Fi/Ethernet LAN IP.
                string best = ""; int bestScore = int.MinValue;
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    var props = ni.GetIPProperties();
                    if (props.GatewayAddresses.Count == 0) continue;   // no route → not a usable LAN adapter
                    string nm = (ni.Name + " " + ni.Description).ToLowerInvariant();
                    if (nm.Contains("virtual") || nm.Contains("vmware") || nm.Contains("virtualbox")
                        || nm.Contains("hyper-v") || nm.Contains("hamachi") || nm.Contains("zerotier")
                        || nm.Contains("tailscale") || nm.Contains("vpn") || nm.Contains("wsl")
                        || nm.Contains("docker") || nm.Contains("pseudo") || nm.Contains("tap-")
                        || nm.Contains("tunnel")) continue;   // skip virtual/VPN adapters by name
                    foreach (var ua in props.UnicastAddresses)
                    {
                        var a = ua.Address;
                        if (a.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || System.Net.IPAddress.IsLoopback(a)) continue;
                        int score = 0;
                        if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet) score += 3;
                        else if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211) score += 2;
                        var b = a.GetAddressBytes();
                        if (b[0] == 192 && b[1] == 168) score += 2;                       // typical home LAN
                        else if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) score += 1;
                        else if (b[0] == 10) score += 1;
                        if (score > bestScore) { bestScore = score; best = a.ToString(); }
                    }
                }
                _cachedLanIp = best;
            }
            catch { }
            return _cachedLanIp;
        }

        /// <summary>
        /// STABLE, immutable identity used as the key for persistent progress
        /// (saves + building ownership) — NOT the display name (PlayerId), which
        /// players can change.  Prefers the Steam account's SteamID64 (permanent);
        /// falls back to a per-machine GUID persisted in the config so it survives
        /// renames + restarts.  Namespaced ("steam-…" / "guid-…").
        /// </summary>
        public static string StableId { get; private set; } = "";

        /// <summary>H-IDENT-1: true for this launch when the Steam account differs from the one the stored id was minted
        /// under (two machines that once shared an account kept ONE identity after switching). Set inside ResolveStableId.</summary>
        public static bool AccountChanged { get; private set; }

        /// <summary>H-IDENT-1 r2: true once an account switch has been FULLY handled (id switched, name re-detected and
        /// persisted) - at Init, or later at the Steam probe via OnSteamReady. Keeps the probe from redoing the switch.</summary>
        private static bool _accountSwitchHandled;

        // ── Tiny persisted key-value store (JSON in the mod folder) ───────────
        private static string _cfgPath = "";
        private static Dictionary<string, string> _cfg = new();

        // HEADLESS-1: a server file's keys win over the persisted config (see LoadServerOverrides). Empty unless that file exists.
        private static Dictionary<string, string> _serverOverrides = new();

        private static string Get(string key, string def = "")
            => _serverOverrides.TryGetValue(key, out var o) ? o : (_cfg.TryGetValue(key, out var v) ? v : def);

        private static void Set(string key, string value)
        {
            _cfg[key] = value;
            try { File.WriteAllText(_cfgPath, JsonConvert.SerializeObject(_cfg, Formatting.Indented)); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] save: {ex.Message}"); }
        }

        /// <summary>Host-controlled minutes between coordinated MP autosaves.
        /// Read LIVE (so the host can retune mid-session without a restart).
        /// 0 = mirror the single-player autosave setting.</summary>
        public static int AutosaveMinutesLive()
        {
            try { return int.TryParse(Get("AutosaveMinutes", "0"), out var m) ? m : 0; }
            catch { return 0; }
        }

        /// <summary>The mod's install folder (ModsLocal\BigAmbitionsMP or the Steam
        /// Workshop item dir) — STATIC CONTENT ONLY (icons, preview).  Never write
        /// here: Workshop installs are Steam-managed and re-validated on update.</summary>
        public static string ModRootPath { get; private set; } = ".";

        /// <summary>Per-user runtime-data root (config, ring logs, bug reports):
        /// LocalLow\Hovgaard Games\Big Ambitions\BigAmbitionsMP.  Survives Steam
        /// Workshop updates and never rides a Workshop upload.  Set in Init;
        /// falls back to ModRootPath only if persistentDataPath is unavailable.</summary>
        public static string DataRootPath { get; private set; } = ".";
        public static string ConfigPath => _cfgPath;

        /// <summary>The chat window's place on THIS machine (user U2 2026-09-28): "x,y,w,h" in canvas units, anchored
        /// bottom-right; "" = the default next to the phone. Written on a drag / resize release only.</summary>
        public static string ChatWindowPlace { get { try { return Get("ChatWindowPlace"); } catch { return ""; } } }

        // MODS-GATE-1 (user-approved 2026-09-29, decisions 16-17): the host's lobby 'Different mods' setting.
        // false = Allow (a joiner with a different mod list joins with the mismatch warnings - the behaviour before this
        // setting existed, and the default); true = Refuse (MPServer refuses that joiner at Hello, before admission).
        // Read on the NETWORK thread by the Hello check, so it lives in a volatile field loaded at Init and written only by
        // the lobby click (SetRefuseModMismatch), which also persists it.
        private static volatile bool _refuseModMismatch;
        public static bool RefuseModMismatch => _refuseModMismatch || _dedicatedHost;   // DEDICATED-HOST-1: a dedicated host always refuses different mods
        /// <summary>The raw persisted value ("" when never saved) - for the DEV lever's persistence check.</summary>
        public static string RefuseModMismatchSaved { get { try { return Get("RefuseModMismatch", ""); } catch { return ""; } } }
        public static void SetRefuseModMismatch(bool refuse)
        {
            _refuseModMismatch = refuse;
            try { Set("RefuseModMismatch", refuse ? "true" : "false"); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] RefuseModMismatch save: {ex.Message}"); }
        }

        // ── DEDICATED-HOST-1: always-on host mode (keys in the JSON config; no lobby UI) ──────────────────────────────
        // DedicatedHost = "true" turns it on (see DedicatedHost.cs); it also forces 'Different mods: Refuse' so every player
        // runs the same mods. AutoDownloadHostMods is the CLIENT's opt-in for fetching a host's Workshop mods (ModAutoDownload.cs).
        // Both flags are read on the network thread (Hello check / disconnect handler), so they are volatile snapshots loaded at Init.
        private static volatile bool _dedicatedHost;
        private static volatile bool _autoDownloadHostMods;
        public static bool DedicatedHost => _dedicatedHost;
        public static bool AutoDownloadHostMods => _autoDownloadHostMods;
        /// <summary>The saved game the dedicated host loads by itself from the main menu ("" = none configured).</summary>
        public static string DedicatedSession { get { try { return AmpSavePicker.FromPicker(Get("DedicatedSession", "")); } catch { return ""; } } }   // AMP-PICK-1: a name or AMP's dropdown value
        /// <summary>DEDICATED-NEWGAME-1: when DedicatedSession names a save that does not exist yet, start a new world with this
        /// difficulty ("Easy" / "Normal" / "Hard", or "Custom" = the NewGame* settings, NEWGAME-CUSTOM-1) and save it under that
        /// name. "" (default) = off: a missing save stays an error - except on a server without any multiplayer save, which
        /// starts its first world "Server" with Normal (FIRST-WORLD-1, DedicatedHost.NewGameDifficulty).</summary>
        public static string DedicatedNewGame
        {
            get
            {
                try
                {
                    string v = Get("DedicatedNewGame", "").Trim();
                    foreach (var d in new[] { "Easy", "Normal", "Hard", "Custom" })
                        if (string.Equals(v, d, StringComparison.OrdinalIgnoreCase)) return d;
                    return "";
                }
                catch { return ""; }
            }
        }
        /// <summary>NEW-WORLD-NAME-1: a world to create, by name ("" = none). While no save of it exists, the dedicated host creates
        /// it with DedicatedNewGame and plays it; afterwards it is a world like any other. Named the way its save folder is.</summary>
        public static string DedicatedNewWorld { get { try { return MPSaveCoordinator.SanitizeSession(Get("DedicatedNewWorld", "")); } catch { return ""; } } }
        /// <summary>DEDICATED-SAVES-1: load THIS save of the world once (a save name as 'saves' lists it, e.g. "Test-auto-2", or
        /// "#3" = the third newest) instead of the newest. "" = newest. Applied once per value (DedicatedHost remembers it).</summary>
        public static string DedicatedLoadSave { get { try { return AmpSavePicker.FromPicker(Get("DedicatedLoadSave", "")); } catch { return ""; } } }   // AMP-PICK-1
        /// <summary>DEDICATED-AFK-1: minutes without keyboard/mouse input after which a connected player counts as away
        /// (0 = never). The world pauses while every connected player is away or in the pause menu.</summary>
        public static int DedicatedAfkMinutes
        {
            get { try { return int.TryParse(Get("DedicatedAfkMinutes", "5").Trim(), out var m) ? Math.Max(0, Math.Min(240, m)) : 5; } catch { return 5; } }
        }
        /// <summary>AMP-SLOTS-1: how many '-auto' saves rotate on this server (1-10); 0 = the game's own option (default 3).</summary>
        public static int AutosaveSlots
        {
            get { try { return int.TryParse(Get("AutosaveSlots", "0").Trim(), out var n) ? Math.Max(0, Math.Min(10, n)) : 0; } catch { return 0; } }
        }
        /// <summary>AMP-MAXPLAYERS-1: players allowed at once, the server's own character not counted (0 = no limit).</summary>
        public static int MaxPlayers
        {
            get { try { return int.TryParse(Get("MaxPlayers", "0").Trim(), out var n) ? Math.Max(0, Math.Min(64, n)) : 0; } catch { return 0; } }
        }
        /// <summary>AMP-WEB-1: where players reach this server's pages (AMP's address, e.g. https://amp.example.org or
        /// http://1.2.3.4:8080), without a trailing slash. "" = https://&lt;the address the player joined&gt;.</summary>
        public static string PlayerWebUrl
        {
            get
            {
                try
                {
                    string v = Get("PlayerWebUrl", "").Trim().TrimEnd('/');
                    bool web = v.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || v.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                    return web && v.IndexOf('|') < 0 && v.IndexOf(' ') < 0 ? v : "";
                }
                catch { return ""; }
            }
        }
        /// <summary>DEDICATED-KEY-1: players need the server key (= the telemetry key) to join (default true).</summary>
        public static bool DedicatedRequireKey
        {
            get { try { return !string.Equals(Get("DedicatedRequireKey", "true").Trim(), "false", StringComparison.OrdinalIgnoreCase); } catch { return true; } }
        }
        /// <summary>STEAM-OWNER-1: a dedicated server lets in only players whose Steam account owns the game (default true;
        /// it works with a SteamWebApiKey only).</summary>
        public static bool SteamOwnerCheck
        {
            get { try { return !string.Equals(Get("SteamOwnerCheck", "true").Trim(), "false", StringComparison.OrdinalIgnoreCase); } catch { return true; } }
        }
        /// <summary>ADMIN-1: when AMP stops, restarts or updates the server ("stop warn", its App.ExitString) with players on,
        /// they are warned this many seconds ahead (0 = at once; at most 90 - AMP kills a server that has not ended after 150).</summary>
        public static int StopWarnSeconds
        {
            get { try { return int.TryParse(Get("StopWarnSeconds", "60").Trim(), out int s) ? Math.Max(0, Math.Min(90, s)) : 60; } catch { return 60; } }
        }
        /// <summary>MERGER-AUTO-DISSOLVE-1: a company member away this many real days leaves it by itself (0 = never, at most 365).</summary>
        public static int MergerAutoDissolveDays
        {
            get { try { return int.TryParse(Get("MergerAutoDissolveDays", "0").Trim(), out int d) ? Math.Max(0, Math.Min(365, d)) : 0; } catch { return 0; } }
        }
        /// <summary>STEAM-OWNER-1: the admin's Steam Web API key for the ownership check ("" = none). Server only - never sent,
        /// never logged.</summary>
        public static string SteamWebApiKey { get { try { return Get("SteamWebApiKey", "").Trim(); } catch { return ""; } } }
        /// <summary>DEDICATED-AFK-1: a player with the pause menu open counts as away (default true).</summary>
        public static bool DedicatedPauseInMenu
        {
            get { try { return !string.Equals(Get("DedicatedPauseInMenu", "true").Trim(), "false", StringComparison.OrdinalIgnoreCase); } catch { return true; } }
        }
        /// <summary>Seconds with no player present before the world pauses (0-600, default 10).</summary>
        public static int DedicatedIdleGraceSeconds
        {
            get
            {
                try { return int.TryParse(Get("DedicatedIdleGraceSeconds", "10").Trim(), out var s) ? Math.Max(0, Math.Min(600, s)) : 10; }
                catch { return 10; }
            }
        }
        /// <summary>Accept mid-game joiners without the host's popup (default true).</summary>
        public static bool DedicatedAutoAccept
        {
            get { try { return !string.Equals(Get("DedicatedAutoAccept", "true").Trim(), "false", StringComparison.OrdinalIgnoreCase); } catch { return true; } }
        }
        private static void LoadDedicatedFlags()
        {
            bool cfgDedicated;
            try { cfgDedicated = string.Equals(Get("DedicatedHost", "false").Trim(), "true", StringComparison.OrdinalIgnoreCase); }
            catch { cfgDedicated = false; }
            bool headless = false;
            try { headless = HeadlessHost.Detect(); } catch { headless = false; }
            _dedicatedHost = cfgDedicated || headless;   // HEADLESS-1: a process nobody can click is always a dedicated host
            try { _autoDownloadHostMods = string.Equals(Get("AutoDownloadHostMods", "false").Trim(), "true", StringComparison.OrdinalIgnoreCase); }
            catch { _autoDownloadHostMods = false; }
            Plugin.Logger.LogInfo($"[Config] Dedicated host: {(_dedicatedHost ? (headless ? "ON (headless; mods must match)" : "ON (mods must match)") : "off")}; auto-download of host mods: {(_autoDownloadHostMods ? "ON" : "off")}.");
        }

        // ── DEDICATED-KEY-1: the server key ───────────────────────────────────────────────────────────────────────────
        /// <summary>What the player typed into the "Server key" box of Join a Game. Until 1.0.10 that box was shared with the
        /// telemetry target ("key@host[:port]", "key@auto") and stored as "TelemetryServer": an old value still gives its
        /// key part.</summary>
        public static string JoinKey
        {
            get
            {
                try
                {
                    string k = Get("ServerKey", "").Trim();
                    if (k.Length > 0) return k;
                    string old = Get("TelemetryServer", "").Trim();
                    int at = old.LastIndexOf('@');
                    if (at > 0) old = old.Substring(0, at).Trim();
                    return IsKey(old) ? old : "";
                }
                catch { return ""; }
            }
        }
        public static void SetJoinKey(string v) { try { Set("ServerKey", (v ?? "").Trim()); } catch { } }

        /// <summary>A value that can be a server key: 4-64 letters, digits, '-' or '_'.</summary>
        public static bool IsKey(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length < 4 || s.Length > 64) return false;
            foreach (char c in s) if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) return false;
            return true;
        }

        /// <summary>Server side (bamp-server.json, AMP setting "Server key"): the key players must give; empty = the one
        /// bamp-start.sh created in telemetry-data/key.txt (DedicatedHost.ServerJoinKey).</summary>
        public static string TelemetryKey { get { try { return Get("TelemetryKey", "").Trim(); } catch { return ""; } } }

        /// <summary>HEADLESS-1: one option from the config (the server file first), default when missing. Lets HeadlessHost
        /// read its own keys while Get stays private.</summary>
        internal static string GetOption(string key, string def)
        {
            try { return Get(key, def); } catch { return def; }
        }

        // HEADLESS-1: server-side settings. An unattended host can neither click the lobby nor easily edit the per-install
        // config (its name carries an install key, and under Proton it sits deep in the prefix). An OPTIONAL flat JSON file
        // next to the game - "bamp-server.json", or the path after "-bamp-config" on the command line - sets the keys below
        // (AMP writes it from its settings page). Applied in memory only, never written into the player's own config.
        // No file = nothing changes, so a normal install is unaffected.
        private static readonly string[] ServerOverridableKeys =
        {
            "ServerLanguage", "AutosaveSlots", "MaxPlayers", "PlayerWebUrl", "DedicatedHost", "DedicatedSession", "DedicatedNewGame", "DedicatedNewWorld", "DedicatedLoadSave", "DedicatedIdleGraceSeconds", "DedicatedAfkMinutes", "DedicatedPauseInMenu", "DedicatedRequireKey", "DedicatedAutoAccept", "AutoDownloadHostMods",
            "Headless", "HeadlessFps", "HeadlessIdleFps", "HeadlessLoadTimeoutSeconds", "HeadlessStallSeconds",
            "HeadlessDisableCameras", "HeadlessDir", "HostPort", "Port", "PlayerId", "AutosaveMinutes", "RefuseModMismatch",
            "TelemetryKey",
            // SERVER-RULES-1: the admin's module switches and SmartRent rules (ServerRules.Keys), sent to every joiner
            "ModuleIrsPhone", "ModuleShopSigns", "ModuleCamera", "ModulePlanner", "ModuleInsights", "ModuleNavigation", "ModulePhonePages", "ModuleGoingOnline", "ModuleItFirm", "ModuleCleaning", "ModuleLaundromat",
            "RuleRentReturn", "RuleBuildingPrice", "RuleBusinessSale", "RuleBusinessSaleMinDays", "RuleBusinessSaleProfitDays", "RuleLoanLimit",
            // NEWGAME-CUSTOM-1: the game's "Custom Game" options for a new world (DedicatedNewGame = Custom)
            "NewGameDaysPerYear", "NewGameStartingMoney", "NewGameRivalAttacks", "NewGameStartingAge", "NewGameDisableAging",
            "NewGameDisableHappiness", "NewGameDisableEnergy", "NewGameAllCoursesUnlocked", "NewGameAllContactsUnlocked",
            "NewGameDisableVehicleDamage", "NewGameDisableVehicleFuel", "NewGameTaxPercentage", "NewGameSalary", "NewGamePublicPrices",
            "NewGameBankInterest", "NewGameCustomerPromotion", "NewGameWholesaleUrgentFee", "NewGameImporterUrgentFee",
            "NewGameDisableImportLimits", "NewGameAllProductsFromImporters", "NewGameExportPrices", "NewGameResaleValue",
            // SERVER-SETTINGS-1: the modules' settings for every player of the server (ServerRules.Keys)
            "CamFootPitch", "CamFootDist", "CamCarPitch", "CamCarDist", "CamHeight", "CamShoulder", "CamSensitivity", "CamFov",
            "CamViewDistance", "CamDeadZone", "CamRecenter", "CamIndoors", "NavRouteLine",
            "NavPhone",   // NAV-PHONE-1
            "SteamFriendJoin", "SteamOwnerCheck", "SteamWebApiKey",   // STEAM-FRIEND-JOIN-1, STEAM-OWNER-1
            "StopWarnSeconds",   // ADMIN-1
            "MergerAutoDissolveDays",   // MERGER-AUTO-DISSOLVE-1
            // NET-RATE-1 (was missing since 1.0.14: AMP's choice was ignored, the default level ran), and the 1.0.16
            // server settings: DISCONNECT-PAUSE-1, VERSION-GATE-1, LOG-LEVEL-1
            "NetRate", "DisconnectPause", "SameVersionOnly", "LogLevel",
            "RadioList",   // RADIO-STREAMS-2
            "PlayerReports",   // PLAYER-REPORTS-1
            "ChatTranslate",   // CHAT-TRANSLATE-1
        };

        private static void LoadServerOverrides()
        {
            try
            {
                string path = "";
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < args.Length; i++)
                    if (string.Equals(args[i], "-bamp-config", StringComparison.OrdinalIgnoreCase)) { path = args[i + 1]; break; }
                if (path.Length == 0) path = Path.Combine(GameRootPath, "bamp-server.json");
                if (!File.Exists(path)) return;
                var raw = JsonConvert.DeserializeObject<Dictionary<string, Newtonsoft.Json.Linq.JToken>>(File.ReadAllText(path));
                if (raw == null) return;
                var applied = new List<string>();
                var o = new Dictionary<string, string>();
                foreach (var kv in raw)
                {
                    if (kv.Value == null || kv.Value.Type == Newtonsoft.Json.Linq.JTokenType.Null) continue;
                    if (Array.IndexOf(ServerOverridableKeys, kv.Key) < 0)
                    {
                        Plugin.Logger.LogWarning($"[Config] server file: key '{kv.Key}' is not a server setting - ignored.");
                        continue;
                    }
                    string s = kv.Value.Type == Newtonsoft.Json.Linq.JTokenType.Boolean
                        ? (kv.Value.ToObject<bool>() ? "true" : "false")
                        : kv.Value.Type == Newtonsoft.Json.Linq.JTokenType.Float   // JValue.ToString follows the system culture ("1,5")
                            ? kv.Value.ToObject<double>().ToString(System.Globalization.CultureInfo.InvariantCulture)
                            : kv.Value.ToString();
                    o[kv.Key] = s.Trim();
                    applied.Add(kv.Key);
                }
                _serverOverrides = o;
                Plugin.Logger.LogInfo($"[Config] server file '{path}': {applied.Count} setting(s) applied ({string.Join(", ", applied)}).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] server file: {ex.Message} - ignored."); }
        }

        // ── BAN-PLAYERS-1 build A (owner-approved 2026-10-01): the host's SAVED ban list ─────────────────────────────
        // Key 'BannedPlayers' = a JSON list of BanEntry. A ban lasts across re-hosts and restarts until Unban, and belongs
        // to THIS host (its own config), never to a world. The Hello check reads it on the NETWORK thread, so - like
        // RefuseModMismatch - it is an in-memory SNAPSHOT loaded once at Init: every change builds a NEW list of NEW
        // entries (copy-on-write) on the MAIN thread, swaps the reference and persists. A published entry is never
        // mutated, so a reader holding the old list is safe.
        // Addresses are kept for MATCHING ONLY: never log or show one (streamer rule) - log AddressTag instead.
        public sealed class BanAddress
        {
            public string Ip = "";        // exact public address, no port
            public string SeenUtc = "";   // ISO-8601 UTC; the address stops matching BanIpDays after this
        }
        public sealed class BanEntry
        {
            public string Key = "";        // short random id - what Unban names
            public string Name = "";       // the player's name, for showing only (never matched)
            public string SteamId = "";    // Steam-VOUCHED SteamID64 from the link (Steam joins); "" when unknown
            public string StableId = "";   // the player's claimed stable id
            public List<BanAddress> Ips = new();   // IP joins only: public addresses, each expiring BanIpDays after last seen
            public int BanDay;             // game day of the ban (0 = no world loaded)
            public string BannedUtc = "";  // real date of the ban, ISO-8601 UTC
            public string Reason = "";     // ADMIN-1: why, as the admin gave it ("" = none)
            internal BanEntry Clone()
            {
                var c = new BanEntry { Key = Key, Name = Name, SteamId = SteamId, StableId = StableId, BanDay = BanDay, BannedUtc = BannedUtc, Reason = Reason };
                if (Ips != null) foreach (var a in Ips) if (a != null) c.Ips.Add(new BanAddress { Ip = a.Ip, SeenUtc = a.SeenUtc });
                return c;
            }
        }
        public const int BanIpDays = 7;
        private static volatile List<BanEntry> _bans = new();
        /// <summary>The saved bans (a snapshot - never mutate it; change it only through AddBan / NoteBannedAddress / RemoveBan).</summary>
        public static IReadOnlyList<BanEntry> BannedPlayers => _bans;

        private static void LoadBans()
        {
            try
            {
                string raw = Get("BannedPlayers", "").Trim();
                var list = raw.Length == 0 ? new List<BanEntry>() : (JsonConvert.DeserializeObject<List<BanEntry>>(raw) ?? new List<BanEntry>());
                var now = DateTime.UtcNow;
                int dropped = 0;
                var keep = new List<BanEntry>();
                foreach (var e in list)
                {
                    if (e == null) continue;
                    e.Ips ??= new List<BanAddress>();
                    dropped += e.Ips.RemoveAll(a => a == null || IpExpired(a.SeenUtc, now) || !IsRecordableIp(a.Ip));
                    if (string.IsNullOrEmpty(e.SteamId) && string.IsNullOrEmpty(e.StableId) && e.Ips.Count == 0) continue;   // nothing left to match
                    if (string.IsNullOrEmpty(e.Key)) e.Key = NewBanKey();
                    keep.Add(e);
                }
                _bans = keep;
                if (dropped > 0 || keep.Count != list.Count) SaveBans(keep);
                Plugin.Logger.LogInfo($"[Config] Saved bans: {keep.Count}{(dropped > 0 ? $" ({dropped} expired address(es) dropped)" : "")}.");
            }
            catch (Exception ex)
            {
                _bans = new List<BanEntry>();
                Plugin.Logger.LogWarning($"[Config] BannedPlayers load: {ex.Message} - no saved bans this run.");
            }
        }

        private static void SaveBans(List<BanEntry> list)
        {
            try { Set("BannedPlayers", JsonConvert.SerializeObject(list)); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] BannedPlayers save: {ex.Message}"); }
        }

        private static List<BanEntry> CloneBans()
        {
            var outp = new List<BanEntry>();
            foreach (var e in _bans) if (e != null) outp.Add(e.Clone());
            return outp;
        }

        private static string NewBanKey() => "b" + Guid.NewGuid().ToString("N").Substring(0, 7);

        private static bool IpExpired(string seenUtc, DateTime nowUtc)
        {
            try
            {
                if (!DateTime.TryParse(seenUtc, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var seen)) return true;
                return (nowUtc - seen).TotalDays > BanIpDays;
            }
            catch { return true; }
        }

        /// <summary>BAN-PLAYERS-1 build C1 (F1): was this address last seen more than <paramref name="age"/> ago? An
        /// unreadable time counts as old (it gets rewritten).</summary>
        private static bool SeenOlderThan(string seenUtc, DateTime nowUtc, TimeSpan age)
        {
            try
            {
                if (!DateTime.TryParse(seenUtc, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var seen)) return true;
                return (nowUtc - seen) > age;
            }
            catch { return true; }
        }

        private static System.Net.IPAddress? ParseIp(string ip)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ip) || !System.Net.IPAddress.TryParse(ip.Trim(), out var a)) return null;
                if (a.IsIPv4MappedToIPv6) a = a.MapToIPv4();
                return a;
            }
            catch { return null; }
        }

        /// <summary>BAN-PLAYERS-1: what kind of address this is - none / loopback / private / link-local / own (the host's own
        /// public address) / public. Only "public" is ever recorded in a ban.</summary>
        private static string AddressKind(System.Net.IPAddress? a)
        {
            try
            {
                if (a == null) return "none";
                if (System.Net.IPAddress.IsLoopback(a)) return "loopback";
                var b = a.GetAddressBytes();
                if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    if (b[0] == 0) return "private";
                    if (b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168)) return "private";
                    if (b[0] == 169 && b[1] == 254) return "link-local";
                }
                else if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    if (a.IsIPv6LinkLocal) return "link-local";
                    if (a.IsIPv6SiteLocal || (b[0] & 0xFE) == 0xFC) return "private";   // fec0::/10 + fc00::/7 unique-local
                    if (a.Equals(System.Net.IPAddress.IPv6Any) || a.Equals(System.Net.IPAddress.IPv6None)) return "none";
                }
                else return "none";
                var own = ParseIp(MPNet.PublicIp ?? "");
                if (own != null && own.Equals(a)) return "own";
                return "public";
            }
            catch { return "none"; }
        }

        /// <summary>BAN-PLAYERS-1 (A2): may this address go into a ban? Only a public address that is not the host's own -
        /// never 10.x / 172.16-31.x / 192.168.x / 127.x / ::1 / fe80:: (or other private/link-local) ones.</summary>
        public static bool IsRecordableIp(string ip) => AddressKind(ParseIp(ip)) == "public";

        private static string NormalizeIp(string ip) { var a = ParseIp(ip); return a == null ? "" : a.ToString(); }

        private static readonly byte[] _addrTagSalt = Guid.NewGuid().ToByteArray();   // per process: a tag cannot be reversed from the log
        /// <summary>BAN-PLAYERS-1 (A7, row IP-IN-LOGS-1): the ONLY way an address reaches a log - its kind plus a short keyed
        /// hash ("addr:public#3fa21c"). The key is random per process, so the tag tells two addresses apart within one run
        /// and reveals nothing about the address itself.</summary>
        public static string AddressTag(string ip)
        {
            try
            {
                var a = ParseIp(ip);
                if (a == null) return "addr:none";
                using var h = new System.Security.Cryptography.HMACSHA256(_addrTagSalt);
                var d = h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(a.ToString()));
                return $"addr:{AddressKind(a)}#{d[0]:x2}{d[1]:x2}{d[2]:x2}";
            }
            catch { return "addr:?"; }
        }

        /// <summary>BAN-PLAYERS-1 (A3) - NETWORK-THREAD SAFE (reads the snapshot only): the first saved ban matching this
        /// joiner by Steam id (from the link), claimed stable id, or an UNEXPIRED address; <paramref name="matchedBy"/> says which.</summary>
        public static BanEntry? FindBan(string steamId, string stableId, string ip, out string matchedBy)
        {
            matchedBy = "";
            try
            {
                var list = _bans;
                var now = DateTime.UtcNow;
                string ipN = NormalizeIp(ip);
                // C1 (F4): the host's own public address never matches (a joiner behind the host's own router shows it) -
                // checked here at match time too, since an entry may hold it from before MPNet.PublicIp was known.
                if (ipN.Length > 0 && AddressKind(ParseIp(ipN)) == "own") ipN = "";
                string steamStable = string.IsNullOrEmpty(steamId) ? "" : "steam-" + steamId;
                foreach (var e in list)
                {
                    if (e == null) continue;
                    // C1 (F2): a ban whose stable id is 'steam-<N>' IS Steam account N - it matches a joiner vouched as N.
                    if (!string.IsNullOrEmpty(steamId) && (e.SteamId == steamId || e.StableId == steamStable)) { matchedBy = "Steam id"; return e; }
                    if (!string.IsNullOrEmpty(stableId) && e.StableId == stableId) { matchedBy = "stable id"; return e; }
                    if (ipN.Length > 0 && e.Ips != null)
                        foreach (var a in e.Ips)
                            if (a != null && a.Ip == ipN && !IpExpired(a.SeenUtc, now)) { matchedBy = "address"; return e; }
                }
            }
            catch { }
            return null;
        }

        private static bool TouchIp(BanEntry e, string ipN, DateTime nowUtc)
        {
            foreach (var a in e.Ips)
                if (a.Ip == ipN)
                {
                    // BAN-PLAYERS-1 build C1 (F1): refresh when the last refresh is MORE than an hour old, never otherwise (no
                    // rewrite per retry) - the 7 days run from the last time the address was seen. Build A had this inverted:
                    // an unexpired address was never refreshed, so it expired 7 days after it was FIRST recorded.
                    if (!SeenOlderThan(a.SeenUtc, nowUtc, TimeSpan.FromHours(1))) return false;
                    a.SeenUtc = nowUtc.ToString("o"); return true;
                }
            e.Ips.Add(new BanAddress { Ip = ipN, SeenUtc = nowUtc.ToString("o") });
            return true;
        }

        /// <summary>BAN-PLAYERS-1 (A2/A4) - MAIN THREAD: record a ban (merged into an existing entry with the same stable id
        /// or Steam id) and persist it. The address is kept only when IsRecordableIp; the host's own stable id never is.
        /// Returns the entry, or null when nothing identifies the player.</summary>
        public static BanEntry? AddBan(string name, string steamId, string stableId, string ip, int gameDay, string reason = "")
        {
            try
            {
                steamId = (steamId ?? "").Trim(); stableId = (stableId ?? "").Trim();
                if (stableId == StableId) stableId = "";
                string ipN = IsRecordableIp(ip) ? NormalizeIp(ip) : "";
                if (steamId.Length == 0 && stableId.Length == 0 && ipN.Length == 0) return null;
                var list = CloneBans();
                BanEntry? e = null;
                foreach (var x in list)
                    if ((stableId.Length > 0 && x.StableId == stableId) || (steamId.Length > 0 && x.SteamId == steamId)) { e = x; break; }
                var now = DateTime.UtcNow;
                if (e == null) { e = new BanEntry { Key = NewBanKey(), BanDay = gameDay, BannedUtc = now.ToString("o") }; list.Add(e); }
                if (!string.IsNullOrEmpty(name)) e.Name = name;
                if (!string.IsNullOrWhiteSpace(reason)) e.Reason = reason.Trim();   // ADMIN-1
                if (steamId.Length > 0) e.SteamId = steamId;
                if (stableId.Length > 0) e.StableId = stableId;
                if (ipN.Length > 0) TouchIp(e, ipN, now);
                _bans = list;
                SaveBans(list);
                return e;
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] AddBan: {ex.Message}"); return null; }
        }

        /// <summary>BAN-PLAYERS-1 (A2) - MAIN THREAD: a banned player was seen at this address - record it (or refresh its
        /// 7 days). True when the saved list changed.</summary>
        public static bool NoteBannedAddress(string key, string ip)
        {
            try
            {
                if (!IsRecordableIp(ip)) return false;
                var list = CloneBans();
                foreach (var e in list)
                    if (e.Key == key)
                    {
                        if (!TouchIp(e, NormalizeIp(ip), DateTime.UtcNow)) return false;
                        _bans = list;
                        SaveBans(list);
                        return true;
                    }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] NoteBannedAddress: {ex.Message}"); }
            return false;
        }

        /// <summary>BAN-PLAYERS-1 build C1 (F2a) - MAIN THREAD: a refused joiner's link was Steam-vouched as
        /// <paramref name="steamId"/> and matched the entry whose stable id is 'steam-'+that id - keep the Steam id on it.
        /// Never for any other entry: a CLAIMED stable id (or a shared address) never assigns a player a Steam id.
        /// True when the saved list changed.</summary>
        public static bool NoteBannedSteamId(string key, string steamId)
        {
            try
            {
                steamId = (steamId ?? "").Trim();
                if (steamId.Length == 0) return false;
                var list = CloneBans();
                foreach (var e in list)
                    if (e.Key == key)
                    {
                        if (!string.IsNullOrEmpty(e.SteamId) || e.StableId != "steam-" + steamId) return false;
                        e.SteamId = steamId;
                        _bans = list;
                        SaveBans(list);
                        return true;
                    }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] NoteBannedSteamId: {ex.Message}"); }
            return false;
        }

        private static string _ownIpSwept = "";
        /// <summary>BAN-PLAYERS-1 build C1 (F4) - MAIN THREAD, ~1 Hz while hosting: once the host's own public address is
        /// known, drop every recorded ban address equal to it (one can be recorded before MPNet.PublicIp is known). One
        /// sweep per address; logged once, as a tag.</summary>
        public static void DropOwnPublicAddress()
        {
            try
            {
                string own = NormalizeIp(MPNet.PublicIp ?? "");
                if (own.Length == 0 || own == _ownIpSwept) return;
                _ownIpSwept = own;
                var list = CloneBans();
                int n = 0;
                foreach (var e in list) if (e.Ips != null) n += e.Ips.RemoveAll(a => a == null || NormalizeIp(a.Ip) == own);
                if (n == 0) return;
                _bans = list;
                SaveBans(list);
                Plugin.Logger.LogInfo($"[Config] Saved bans: {n} recorded address(es) equal to the host's own public address dropped ({AddressTag(own)}).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] DropOwnPublicAddress: {ex.Message}"); }
        }

        /// <summary>BAN-PLAYERS-1 build C1 (F7): a stable id / Steam id as it may reach a log - its kind plus a short keyed
        /// hash ("steam#3fa21c", "id#09be44"; the same per-process key as AddressTag), never the id itself.</summary>
        public static string IdTag(string id)
        {
            try
            {
                id = (id ?? "").Trim();
                if (id.Length == 0) return "-";
                using var h = new System.Security.Cryptography.HMACSHA256(_addrTagSalt);
                var d = h.ComputeHash(System.Text.Encoding.UTF8.GetBytes("id|" + id));
                bool steam = id.StartsWith("steam-", StringComparison.Ordinal) || (id.Length == 17 && ulong.TryParse(id, out _));
                return $"{(steam ? "steam" : "id")}#{d[0]:x2}{d[1]:x2}{d[2]:x2}";
            }
            catch { return "id#?"; }
        }

        /// <summary>BAN-PLAYERS-1 build C1: is this a saved ban's key ("b" + 7 hex)?</summary>
        public static bool IsBanKey(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 8 || s[0] != 'b') return false;
            for (int i = 1; i < 8; i++) if (Uri.IsHexDigit(s[i]) == false) return false;
            return true;
        }

        /// <summary>BAN-PLAYERS-1 build C1 (F7): one saved ban as the screens show it - NO address, no Steam id, no stable id.
        /// <see cref="Key"/> is opaque: it is what Unban / Remove property take.</summary>
        public sealed class BanRow
        {
            public string Key = "";
            public string Name = "";
            public int BanDay;             // game day of the ban (0 = no world was loaded)
            public string BannedUtc = "";  // real date of the ban, ISO-8601 UTC
            public string How = "";        // how the ban recognises them: "Steam account" or "Joined by IP"
            public string Reason = "";     // ADMIN-1: the admin's reason ("" = none)
        }

        /// <summary>BAN-PLAYERS-1 build C1 (F7): the saved bans for the screens (a fresh copy each call).</summary>
        public static List<BanRow> BanRows()
        {
            var outp = new List<BanRow>();
            try
            {
                foreach (var e in _bans)
                    if (e != null)
                        outp.Add(new BanRow
                        {
                            Key = e.Key, Name = e.Name, BanDay = e.BanDay, BannedUtc = e.BannedUtc, Reason = e.Reason ?? "",
                            How = !string.IsNullOrEmpty(e.SteamId) || (e.StableId ?? "").StartsWith("steam-", StringComparison.Ordinal) ? "Steam account" : "Joined by IP",
                        });
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] BanRows: {ex.Message}"); }
            return outp;
        }

#if BAMP_DEV
        /// <summary>BAN-PLAYERS-1 build C1 DEV lever ('ban devaddr age KEY HOURS'): move every recorded address of one saved
        /// ban back in time, as if last seen HOURS earlier. Prints no address.</summary>
        internal static string DevAgeAddresses(string key, double hours)
        {
            try
            {
                var list = CloneBans();
                var now = DateTime.UtcNow;
                foreach (var e in list)
                    if (e.Key == key)
                    {
                        int n = 0; double newest = double.MaxValue;
                        foreach (var a in e.Ips)
                        {
                            if (a == null || !DateTime.TryParse(a.SeenUtc, System.Globalization.CultureInfo.InvariantCulture,
                                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var seen)) continue;
                            seen = seen.AddHours(-hours);
                            a.SeenUtc = seen.ToString("o");
                            n++;
                            newest = Math.Min(newest, (now - seen).TotalHours);
                        }
                        _bans = list;
                        SaveBans(list);
                        return $"OK devaddr aged {n} address(es) of {key} by {hours.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}h: newest seen {(n == 0 ? "-" : newest.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))}h ago";
                    }
                return $"ERR devaddr age: no saved ban {key}";
            }
            catch (Exception ex) { return "ERR devaddr age: " + ex.Message; }
        }
#endif

        /// <summary>BAN-PLAYERS-1 (A4) - MAIN THREAD: remove every saved ban whose key, stable id, Steam id or name equals
        /// <paramref name="who"/> ("all" = every ban); persists. Returns how many were removed.</summary>
        public static int RemoveBan(string who)
        {
            try
            {
                who = (who ?? "").Trim();
                if (who.Length == 0) return 0;
                var list = CloneBans();
                int n = who == "all" ? list.Count
                      : list.RemoveAll(e => e.Key == who || e.StableId == who || e.SteamId == who || e.Name == who);
                if (who == "all") list.Clear();
                if (n == 0) return 0;
                _bans = list;
                SaveBans(list);
                return n;
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] RemoveBan: {ex.Message}"); return 0; }
        }

        /// <summary>BAN-PLAYERS-1: one line per saved ban for the DEV lever - never an address, only how many it holds; the
        /// stable id only as a kind + short tag (build C1, F7).</summary>
        public static string DescribeBans()
        {
            try
            {
                var list = _bans;
                var parts = new List<string>();
                foreach (var e in list)
                    if (e != null)
                        parts.Add($"{e.Key} '{e.Name}' stable={IdTag(e.StableId)} steam={(string.IsNullOrEmpty(e.SteamId) ? "no" : "yes")} ips={e.Ips?.Count ?? 0} day={e.BanDay} since={(e.BannedUtc.Length >= 10 ? e.BannedUtc.Substring(0, 10) : e.BannedUtc)}");
                return $"n={parts.Count} [{string.Join("; ", parts)}]";
            }
            catch (Exception ex) { return "n=? (" + ex.Message + ")"; }
        }
        public static void SetChatWindowPlace(string v)
        {
            try { Set("ChatWindowPlace", v ?? ""); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] chat place: {ex.Message}"); }
        }

        /// <summary>MODULE-NAVIGATION-1: this machine's navigation bookmarks (JSON list of {n, x, y, z}); "" = none.</summary>
        public static string NavBookmarks { get { try { return Get("NavBookmarks", "[]"); } catch { return "[]"; } } }
        public static void SetNavBookmarks(string v)
        {
            try { Set("NavBookmarks", v ?? "[]"); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] nav bookmarks: {ex.Message}"); }
        }

        /// <summary>PHONE-PAGES-2: this machine's order of the phone's apps ("app:&lt;AppName&gt;" for the game's, the tile's
        /// name for the mod's, joined by '|'); "" = the game's order, the mod's apps after it.</summary>
        public static string PhoneOrder { get { try { return Get("PhoneOrder"); } catch { return ""; } } }
        public static void SetPhoneOrder(string v)
        {
            try { Set("PhoneOrder", v ?? ""); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] phone order: {ex.Message}"); }
        }

        /// <summary>NAV-CHOICE-1: the self-driving parks at the destination (on) or stops there (off) - this machine.</summary>
        public static bool AutoPark { get { try { return Get("AutoPark", "true") != "false"; } catch { return true; } } }
        public static void SetAutoPark(bool v)
        {
            try { Set("AutoPark", v ? "true" : "false"); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] auto park: {ex.Message}"); }
        }

        /// <summary>MODULE-CAMERA-1: the RPG camera's settings on THIS machine ("k=v;...", see RpgCamera); "" = defaults.</summary>
        public static string CameraSettings { get { try { return Get("CameraSettings"); } catch { return ""; } } }
        public static void SetCameraSettings(string v)
        {
            try { Set("CameraSettings", v ?? ""); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] camera settings: {ex.Message}"); }
        }

        public static string BugReportDiscordWebhookUrlLive()
        {
            try
            {
                return GetLiveString("BugReportDiscordWebhookUrl").Trim();
            }
            catch { return ""; }
        }

        /// <summary>The bug-report RELAY endpoint. BUG-REPORT-OFF-1 (2026-10-31): empty - the baked-in relay was the
        /// original Going Public's, and reports are not uploaded at all any more (MPBugReport.CurrentUploadTarget).</summary>
        public const string DefaultBugReportRelayUrl = "";
        public static string BugReportRelayUrlLive()
        {
            try { var v = GetLiveString("BugReportRelayUrl").Trim(); return v.Length > 0 ? v : DefaultBugReportRelayUrl; }
            catch { return DefaultBugReportRelayUrl; }
        }

        /// <summary>Optional shared key sent as the X-BAMP-Key header to the relay (matches the
        /// Worker's RELAY_KEY secret).  Obfuscation only — it ships in the mod.</summary>
        public static string BugReportRelayKeyLive()
        {
            try { return GetLiveString("BugReportRelayKey").Trim(); }
            catch { return ""; }
        }

        public static string BugReportDiscordCrashTagIdLive()
        {
            try { return CleanDiscordTagId(GetLiveString("BugReportDiscordCrashTagId")); }
            catch { return ""; }
        }

        public static IReadOnlyList<BugReportDiscordTag> BugReportDiscordBugTagsLive()
        {
            var tags = new List<BugReportDiscordTag>();
            try
            {
                string raw = GetLiveString("BugReportDiscordBugTags");
                if (string.IsNullOrWhiteSpace(raw)) return tags;

                foreach (var part in raw.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string item = part.Trim();
                    if (item.Length == 0) continue;

                    string label;
                    string id;
                    int eq = item.IndexOf('=');
                    if (eq >= 0)
                    {
                        label = item.Substring(0, eq).Trim();
                        id = item.Substring(eq + 1).Trim();
                    }
                    else
                    {
                        label = "Bug";
                        id = item;
                    }

                    id = CleanDiscordTagId(id);
                    if (id.Length == 0) continue;
                    if (label.Length == 0) label = "Bug";
                    tags.Add(new BugReportDiscordTag { Label = label, Id = id });
                }
            }
            catch { }
            return tags;
        }

        public static bool AllowBugReportCrashTestLive()
        {
            try
            {
                string v = GetLiveString("AllowBugReportCrashTest");
                return v.Equals("true", StringComparison.OrdinalIgnoreCase)
                       || v.Equals("1", StringComparison.OrdinalIgnoreCase)
                       || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <summary>H-STEAMNET-2: settings-file key "SteamRateControl" (no UI), read live at each new Steam
        /// connection. Default (absent) true = each Steam connection's send rate is set by the mod's per-connection
        /// controller; false/0/no/off pins every Steam connection at 256 KB/s (the pre-controller behaviour).</summary>
        public static bool SteamRateControlLive()
        {
            try
            {
                string v = GetLiveString("SteamRateControl").Trim();
                if (v.Length == 0) return true;
                return !(v.Equals("false", StringComparison.OrdinalIgnoreCase) || v == "0"
                         || v.Equals("no", StringComparison.OrdinalIgnoreCase) || v.Equals("off", StringComparison.OrdinalIgnoreCase));
            }
            catch { return true; }
        }

        /// <summary>H-CARBUYSTACK-1: settings-file key "CarBuyFreeSpot" (no UI), read live at each dealership purchase.
        /// Default (absent) true = while a session is live, a purchase whose delivery spot holds a PARTNER's car is moved to
        /// a free spot next to it (CarBuySpot.cs); false/0/no/off = the game's own spawn on the spot (the behaviour before).</summary>
        public static bool CarBuyFreeSpotLive()
        {
            try
            {
                string v = GetLiveString("CarBuyFreeSpot").Trim();
                if (v.Length == 0) return true;
                return !(v.Equals("false", StringComparison.OrdinalIgnoreCase) || v == "0"
                         || v.Equals("no", StringComparison.OrdinalIgnoreCase) || v.Equals("off", StringComparison.OrdinalIgnoreCase));
            }
            catch { return true; }
        }

        /// <summary>H-RIVALPARITY-1 part E: settings-file key "RivalClientSellers" (no UI), read live at each seller recount on
        /// the HOST (ProductMarketHelper.FillProvidersDictionary: at load and daily). Default (absent) true = another player's
        /// shop whose host copy has an empty product list counts as a seller of the items on its own self-reported list
        /// (RivalClientSellers.cs); false/0/no/off = the game's own count alone (the behaviour before part E).</summary>
        public static bool RivalClientSellersLive()
        {
            try
            {
                string v = GetLiveString("RivalClientSellers").Trim();
                if (v.Length == 0) return true;
                return !(v.Equals("false", StringComparison.OrdinalIgnoreCase) || v == "0"
                         || v.Equals("no", StringComparison.OrdinalIgnoreCase) || v.Equals("off", StringComparison.OrdinalIgnoreCase));
            }
            catch { return true; }
        }

        private static string GetLiveString(string key)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_cfgPath) && File.Exists(_cfgPath))
                {
                    var live = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(_cfgPath));
                    if (live != null && live.TryGetValue(key, out var v))
                        return v ?? "";
                }
            }
            catch { }
            return Get(key);
        }

        private static string CleanDiscordTagId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            value = value.Trim();
            var chars = new List<char>(value.Length);
            foreach (char c in value)
                if (char.IsDigit(c)) chars.Add(c);
            return new string(chars.ToArray());
        }

        /// <summary>The GAME install root this process runs from (e.g.
        /// "C:\BigAmbitions2").  ModsLocal is SHARED by every install
        /// (persistentDataPath), so identity must key off this instead.</summary>
        public static string GameRootPath { get; private set; } = "";

        /// <summary>Filename-safe key for this game install ("Big_Ambitions",
        /// "BigAmbitions2", …) — suffixes the cfg file so each install keeps its
        /// OWN identity inside the shared ModsLocal folder.  Without this, host
        /// and client instances read one cfg = same PlayerId + same StableId
        /// (name and save-slot collisions; 0.10 had per-install BepInEx cfgs).</summary>
        private static string InstallKey()
        {
            try
            {
                var name = Path.GetFileName(GameRootPath.TrimEnd('\\', '/'));
                if (string.IsNullOrEmpty(name)) return "default";
                foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
                return name.Replace(' ', '_');
            }
            catch { return "default"; }
        }

        public static void Init(string modRootPath)
        {
            try
            {
                ModRootPath  = modRootPath ?? ".";
                // dataPath = "<gameRoot>/Big Ambitions_Data"
                GameRootPath = Path.GetDirectoryName(UnityEngine.Application.dataPath) ?? "";
                // Runtime data (config, ring logs, bug reports) lives OUTSIDE the mod
                // folder (Workshop readiness): on a Workshop install the mod folder is
                // Steam-managed content — updates re-validate/clobber it, and a config
                // wipe would destroy StableId (the player's save identity).  The
                // in-game Workshop upload also ships the WHOLE selected folder, so
                // runtime logs/configs must never live in it.  persistentDataPath is
                // per-user and update-safe; per-instance config filenames already
                // disambiguate the two local game installs via InstallKey.
                try
                {
                    DataRootPath = Path.Combine(UnityEngine.Application.persistentDataPath, "BigAmbitionsMP");
                    Directory.CreateDirectory(DataRootPath);
                }
                catch (Exception dx)
                {
                    DataRootPath = ModRootPath;
                    Plugin.Logger.LogWarning($"[Config] data root unavailable ({dx.Message}) — falling back to the mod folder.");
                }
                _cfgPath = Path.Combine(DataRootPath, $"BigAmbitionsMP.cfg.{InstallKey()}.json");
                // One-time migration: pre-0.1.11 the config lived in the mod folder.
                // MOVE it — StableId must survive or the player returns as a stranger.
                try
                {
                    string legacy = Path.Combine(ModRootPath, $"BigAmbitionsMP.cfg.{InstallKey()}.json");
                    if (!File.Exists(_cfgPath) && File.Exists(legacy))
                    {
                        File.Move(legacy, _cfgPath);
                        Plugin.Logger.LogInfo("[Config] migrated config out of the mod folder (Workshop-safe data root).");
                    }
                }
                catch (Exception mx) { Plugin.Logger.LogWarning($"[Config] config migration: {mx.Message}"); }
                Plugin.Logger.LogInfo($"[Config] install '{InstallKey()}' (root '{GameRootPath}') → cfg '{Path.GetFileName(_cfgPath)}' in '{DataRootPath}'.");
                if (File.Exists(_cfgPath))
                    _cfg = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(_cfgPath))
                           ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[Config] load: {ex.Message}");
                _cfg = new Dictionary<string, string>();
            }

            LoadServerOverrides();   // HEADLESS-1: before the first key is read

            HostIP = Get("HostIP", "127.0.0.1");
            Port   = int.TryParse(Get("Port", "7777"), out var p) ? p : 7777;
            // Round-59 SELF-HEAL (Tali/JP field case 2026-07-23): Steam-relay joins persisted
            // their portless "0" into the config, and the host/join validation (1024-65535)
            // then refused EVERY later attempt on that machine — "can't start multiplayer or
            // load any saves". Any out-of-range persisted port resets to the default.
            if (Port < 1024 || Port > 65535)
            {
                Plugin.Logger.LogWarning($"[Config] persisted port {Port} is invalid (Steam-join poisoning) — reset to 7777.");
                Port = 7777;
                try { Set("Port", "7777"); } catch { }
            }
            // Lobby port option A: first run with this build copies Port into HostPort; an invalid saved HostPort resets.
            string hpRaw = Get("HostPort", "").Trim();
            if (hpRaw.Length == 0)
            {
                HostPort = Port;
                try { Set("HostPort", HostPort.ToString()); } catch { }
                Plugin.Logger.LogInfo($"[Config] HostPort not set yet - copied from Port ({HostPort}).");
            }
            else if (!int.TryParse(hpRaw, out var hpv) || hpv < 1024 || hpv > 65535)
            {
                Plugin.Logger.LogWarning($"[Config] persisted HostPort '{hpRaw}' is invalid - reset to {Port}.");
                HostPort = Port;
                try { Set("HostPort", HostPort.ToString()); } catch { }
            }
            else HostPort = hpv;

            // MODS-GATE-1: anything but a saved "true" is Allow (the default).
            try { _refuseModMismatch = string.Equals(Get("RefuseModMismatch", "false").Trim(), "true", StringComparison.OrdinalIgnoreCase); }
            catch { _refuseModMismatch = false; }
            Plugin.Logger.LogInfo($"[Config] Different mods: {(_refuseModMismatch ? "Refuse" : "Allow")}.");
            LoadDedicatedFlags();   // DEDICATED-HOST-1

            LoadBans();   // BAN-PLAYERS-1: the saved ban list, loaded once (the network-thread Hello check reads this snapshot)

            StableId = ResolveStableId(true);
            Plugin.Logger.LogInfo($"[Config] Stable id: {StableId}");

            // Resolve the player ID: config override → old BepInEx cfg ("Host"/
            // "Client1" — keeps lobby identity continuity) → Steam name → fallback
            var stored = Get("PlayerId").Trim();
            if (string.IsNullOrEmpty(stored) || stored == "Player1" || AccountChanged)
            {
                string? oldName = null;
                if (!AccountChanged) TryMigrateFromBepInEx(out _, out oldName);
                if (!string.IsNullOrEmpty(oldName))
                {
                    PlayerId = oldName!;
                    Set("PlayerId", PlayerId);
                    Plugin.Logger.LogInfo($"[Config] Player name migrated from BepInEx cfg: {PlayerId}");
                }
                else
                {
                    PlayerId = DetectPlayerName();
                    Plugin.Logger.LogInfo($"[Config] Auto-detected player name: {PlayerId}{(AccountChanged ? " (Steam account changed — re-detected; H-IDENT-1)" : "")}");
                    if (AccountChanged) Set("PlayerId", PlayerId);
                    if (AccountChanged) _accountSwitchHandled = true;   // a switch caught at Init is fully handled here (H-IDENT-1 r2)
                }
            }
            else
            {
                PlayerId = stored;
                Plugin.Logger.LogInfo($"[Config] Using configured player name: {PlayerId}");
            }
        }

        /// <summary>Called by the UI when the user clicks Host or Join.
        /// Persists the player name so subsequent launches pre-fill the panel.</summary>
        public static void SetRuntime(string playerId, string? hostIp, int port)
        {
            PlayerId = playerId;
            // Round-59: Steam-relay joins pass port 0 (relay sessions are portless) — that must
            // never REPLACE the stored port, or the next launch's host/join validation refuses
            // everything (the Tali/JP "can't start anything" brick). Keep the last real port.
            bool portValid = port >= 1024 && port <= 65535;
            if (portValid) Port = port;
            if (hostIp != null)
                HostIP = hostIp;

            // Re-resolve for consistency only: the persisted id always wins here (a guid- id persists as minted - the old "upgrade" promise never held, review F-2026-09-06-AJ NOTE-7). An ACCOUNT change is never applied here - it runs at Init or on the first frame Steam is valid (MPCanvasUI.TickIdentityRecheck -> OnSteamReady), never mid-session (H-IDENT-1 r3).
            StableId = ResolveStableId(false);

            try
            {
                if (!string.IsNullOrWhiteSpace(playerId))
                {
                    Set("PlayerId", playerId);
                    if (hostIp != null) Set("HostIP", hostIp);
                    if (portValid) Set("Port", port.ToString());   // round-59: never persist a portless (0) Steam join
                    Plugin.Logger.LogInfo($"[Config] Persisted PlayerId='{playerId}' to disk.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[Config] Could not persist PlayerId: {ex.Message}");
            }
        }

        /// <summary>Lobby "Change port" Save (option A): persists the hosting port the player CHOSE. Refuses anything outside
        /// 1024-65535, so a Steam 0 can never land here; the caller never passes a busy-port fallback.</summary>
        public static bool SetHostPort(int port)
        {
            if (port < 1024 || port > 65535) return false;
            HostPort = port;
            try { Set("HostPort", port.ToString()); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] HostPort save: {ex.Message}"); }
            Plugin.Logger.LogInfo($"[Config] HostPort saved: {port}.");
            return true;
        }

        /// <summary>H-IDENT-1 r2: called once from the mod's Steam probe (MPCanvasUI), the first moment Steam is valid. If the stored
        /// identity was minted under another account, switch it now and re-detect the display name - BEFORE any Host/Join click, so
        /// the save list and the name field never show the old identity. Returns true when a switch happened; oldName = the name
        /// that was showing until now (the caller repaints the panel only if it still shows it).</summary>
        public static bool OnSteamReady(out string oldName)
        {
            oldName = PlayerId;
            try
            {
                if (_accountSwitchHandled) return false;
                string before = StableId;
                StableId = ResolveStableId(true);
                if (!AccountChanged) return false;
                _accountSwitchHandled = true;
                PlayerId = DetectPlayerName();
                Set("PlayerId", PlayerId);
                Plugin.Logger.LogInfo($"[Config] Steam account changed (detected at Steam probe): id '{before}' → '{StableId}', name '{oldName}' → '{PlayerId}' (H-IDENT-1). If this repeats every launch, the config file could not be written.");
                return true;
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] OnSteamReady: {ex.Message}"); return false; }
        }

        // ── Name resolution ───────────────────────────────────────────────────

        private static string DetectPlayerName()
        {
            var steamName = TryGetSteamName();
            if (steamName != null)
                return steamName;
            return GenerateFallbackName();
        }

        private static string? TryGetSteamName()
        {
            try
            {
                if (!SteamClient.IsValid)
                {
                    Plugin.Logger.LogWarning("[Config] Steam client not valid yet — cannot read username.");
                    return null;
                }
                var name = SteamClient.Name;
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[Config] Could not read Steam username: {ex.Message}");
            }
            return null;
        }

        // ── Stable identity resolution ────────────────────────────────────────

        /// <summary>Resolve the immutable identity key — persisted id ALWAYS
        /// wins (see main-branch history: re-deriving flipped guid↔steam and
        /// orphaned progress).  IMPORTANT for the 0.10→0.11 migration: the old
        /// BepInEx cfg held the id; this fresh store mints a NEW one unless the
        /// migration shim below finds the old value.</summary>
        private static string ResolveStableId(bool allowAccountSwitch)
        {
            // 1. Already established here → reuse verbatim.
            var stored = Get("StableId").Trim();
            if (!string.IsNullOrEmpty(stored))
            {
                // H-IDENT-1: the persisted id still wins - EXCEPT when it was minted under a Steam account that is
                // provably not the one logged in now (a guid- id, or Steam not valid yet, never triggers this; that
                // guard is what keeps the old guid<->steam flip bug fixed).
                if (allowAccountSwitch && stored.StartsWith("steam-", StringComparison.Ordinal))
                {
                    var now = TryGetSteamId64();
                    if (now != null && stored != "steam-" + now)
                    {
                        string fresh = "steam-" + now;
                        Set("StableIdPrevious", stored);
                        Set("StableId", fresh);
                        AccountChanged = true;
                        Plugin.Logger.LogWarning($"[Config] Steam account changed: stored id '{stored}' was minted under another account — now '{fresh}' (previous kept as StableIdPrevious; the old identity's saves belong to that account). H-IDENT-1 If this warning repeats every launch, the config file could not be written (F-2026-09-06-AH MINOR-6).");
                        return fresh;
                    }
                }
                return stored;
            }

            // 1b. Migration shim: lift the id out of THIS INSTALL's old BepInEx
            //     config so 0.10 progress (saves/ownership keyed by stable id)
            //     carries over.  Checked once; then persisted HERE forever.
            TryMigrateFromBepInEx(out var migrated, out _);
            if (!string.IsNullOrEmpty(migrated))
            {
                Set("StableId", migrated!);
                Plugin.Logger.LogInfo($"[Config] Stable id migrated from BepInEx config: {migrated}");
                return migrated!;
            }

            // 2. First time only — mint one and persist for good.
            var steam = TryGetSteamId64();
            string gen = steam != null ? "steam-" + steam : "guid-" + Guid.NewGuid().ToString("N");
            Set("StableId", gen);
            Plugin.Logger.LogInfo($"[Config] Established stable id (persisted, permanent): {gen}");
            return gen;
        }

        /// <summary>Read StableId + PlayerId out of THIS install's old BepInEx
        /// config (com.bigambitions.multiplayer.cfg — the plugin GUID name, NOT
        /// "BigAmbitionsMP.cfg"; the first migration shim looked for the wrong
        /// filename and silently minted a fresh id).  For the second install the
        /// 0.11 robocopy mirror overwrote its BepInEx folder with the HOST's
        /// copy, so the 0.10 archive holds the true client identity and is
        /// checked FIRST.</summary>
        private static void TryMigrateFromBepInEx(out string? stableId, out string? playerId)
        {
            stableId = null; playerId = null;
            try
            {
                var candidates = new List<string>();
                if (GameRootPath.TrimEnd('\\', '/').EndsWith("BigAmbitions2", StringComparison.OrdinalIgnoreCase))
                    candidates.Add(@"C:\BigAmbitions2-0.10-archive\BepInEx\config\com.bigambitions.multiplayer.cfg");
                if (!string.IsNullOrEmpty(GameRootPath))
                    candidates.Add(Path.Combine(GameRootPath, @"BepInEx\config\com.bigambitions.multiplayer.cfg"));

                foreach (var path in candidates)
                {
                    if (!File.Exists(path)) continue;
                    foreach (var line in File.ReadAllLines(path))
                    {
                        var t = line.Trim();
                        if (t.StartsWith("#") || t.StartsWith("[")) continue;
                        int eq = t.IndexOf('=');
                        if (eq < 0) continue;
                        var k = t.Substring(0, eq).Trim();
                        var v = t.Substring(eq + 1).Trim();
                        if (string.IsNullOrEmpty(v)) continue;
                        if (k.Equals("StableId", StringComparison.OrdinalIgnoreCase)) stableId ??= v;
                        if (k.Equals("PlayerId", StringComparison.OrdinalIgnoreCase)) playerId ??= v;
                    }
                    if (stableId != null || playerId != null)
                    {
                        Plugin.Logger.LogInfo($"[Config] BepInEx migration source: {path}");
                        return;   // one source only — never mix installs
                    }
                }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] BepInEx migration: {ex.Message}"); }
        }

        private static string? TryGetSteamId64()
        {
            try
            {
                if (!SteamClient.IsValid) return null;
                ulong v = SteamClient.SteamId.Value;
                if (v != 0UL) return v.ToString();
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Config] Could not read SteamID64: {ex.Message}"); }
            return null;
        }

        private static string GenerateFallbackName()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
            return $"Player-{suffix}";
        }
    }
}
