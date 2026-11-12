using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>
    /// DEDICATED-HOST-1: an always-on host. The machine that runs this mod keeps a normal host game open and acts as the
    /// "server NPC": its own character is the host's avatar, the world only advances while at least one other player is
    /// present, and it is paused (and saved) when the last player leaves.
    ///
    /// What it does (all of it host side, main thread):
    ///   1. IDLE PAUSE: with no remote player registered (and none waiting for approval) for DedicatedIdleGraceSeconds, the
    ///      world is paused for everyone through the same shared-pause path as the pause button (MPServer.SetIdlePause);
    ///      the moment a player is present it is lifted again. Composed with the deliberate and disconnect pauses, so
    ///      lifting one never cancels another.
    ///   2. AUTO-ACCEPT: mid-game joiners parked for the host's approval popup are accepted automatically (nobody is at the
    ///      keyboard). They already passed the ban list, the version/build gate and the mods gate at Hello.
    ///   3. DISCONNECT PAUSE: a crashed player's "paused until they rejoin" would freeze every OTHER player on an
    ///      unattended host; it is lifted as soon as anyone is present.
    ///   4. AUTO-START: with a configured save (DedicatedSession) the host hosts and loads the NEWEST save of that
    ///      playthrough by itself from the main menu (manual save, autosave, disconnect/quit checkpoint or midnight recover,
    ///      whichever is newest) - loading the manual save by name would roll the world back to the last time a human
    ///      saved by hand. A failed try falls back to the next older save. MPCanvasUI.TickDedicated drives the same lobby
    ///      path a click would.
    ///   5. SAME MODS FOR EVERYONE: a dedicated host always runs 'Different mods: Refuse' (MPConfig.RefuseModMismatch).
    ///
    /// Config keys (the JSON config in the BigAmbitionsMP data folder, or bamp-server.json next to the game):
    /// DedicatedHost = "true", DedicatedSession = "&lt;save name&gt;", DedicatedIdleGraceSeconds = "10" (0-600),
    /// DedicatedAutoAccept = "true". A headless process (HeadlessHost) is always a dedicated host.
    /// </summary>
    public static class DedicatedHost
    {
        private const int   MaxAutoStartAttempts = 5;
        private const float AutoStartSettleSeconds = 20f;   // let the main menu finish building before the first attempt
        private const float AutoStartRetrySeconds  = 30f;

        private static float _nextTickAt;
        private static float _emptySince = -1f;
        private static int   _attempts;
        private static float _lastAttemptAt = -999f;
        private static bool  _warnedNoSession;
        private static bool  _warnedGaveUp;
        // FIRST-WORLD-1 (user 2026-11-11, "Mod: Welt automatisch"): a server without any multiplayer save starts its first
        // world by itself instead of exiting for an operator - a fresh AMP instance has to reach Ready with the defaults.
        private const string FirstWorldName = "Server", FirstWorldDifficulty = "Normal";
        private static bool  _noSaveYet;   // the save list was read and holds no multiplayer world
        private static int   _lastPresent = -1;
        private const float  JoinSettleSeconds = 15f;
        private static volatile float _runningSince = -1f;   // read by the network thread (JoinSettled)
        private static bool  _heldLogged;
        private static bool  _saveHeldLogged;   // JOIN-AFTER-SAVE-1

        public static bool Enabled => MPConfig.DedicatedHost;
        /// <summary>JOIN-SETTLED-1: the hosted world has run long enough to take a joiner.</summary>
        public static bool JoinSettled => _runningSince >= 0f && _clockNow - _runningSince >= JoinSettleSeconds;   // any thread
        public static int  AttemptsMade => _attempts;

        /// <summary>Main thread, every frame (throttled to once a second inside). Safe to call outside a session.</summary>
        public static void Tick()
        {
            if (!MPConfig.DedicatedHost) return;
            _clockNow = Time.realtimeSinceStartup;
            if (Time.unscaledTime < _nextTickAt) return;
            _nextTickAt = Time.unscaledTime + 1f;
            PlayerLedger.Tick();   // DEDICATED-RANK-1: ranking of everyone who ever played here
            BuildingOwnersList.Tick();   // BUILDING-LIST-1: the players' buildings on the ranking page
            MPServer.TickMergerAutoDissolve();   // MERGER-AUTO-DISSOLVE-1: a member away too long leaves his company
            Friends.HostTickBuildings();   // FRIEND-BUILDINGS-1: who rents and bought what, for the Friends tab
            AbsentCars.Tick();             // CARS-STAY-1: the cars of players who left stay, parked at the roadside
            TrafficGuard.EnsureRunning();  // TRAFFIC-GUARD-1: traffic cars brake in time behind the car ahead
            MPServer.TickDisconnectPause(); // DISCONNECT-PAUSE-1: a "60 s" pause ends on time
            AmpSavePicker.Tick();  // AMP-PICK-1: the save / world dropdowns of AMP's settings page
            // JOIN-SETTLED-1: players who joined while the server was still starting were accepted at WorldReady,
            // while the fresh world was still settling (multi-second frames, heavy GC) - and their game then broke
            // (crash while loading the save, renting and building windows failing). Joiners wait until the world
            // has run for JoinSettleSeconds; they are told they are waiting (JOIN-WAIT-1) meanwhile.
            bool running = MPServer.IsRunning && MPLifecycle.Phase == MPLifecycle.MPPhase.Running;
            if (!running) _runningSince = -1f;
            else if (_runningSince < 0f) _runningSince = _clockNow;
            try
            {
                if (!MPServer.IsRunning) { _emptySince = -1f; _lastPresent = -1; return; }
                if (!MPServer.WorldRunning) return;   // lobby or still loading: the startup hold owns the clock
                _attempts = 0;                        // a hosted world reached running: the auto-start counter starts over
                _warnedGaveUp = false;

                bool settled = JoinSettled;

                // 2. auto-accept mid-game joiners (the approval popup needs a human)
                int waiting = 0;
                try
                {
                    var pending = MPServer.PendingJoinList;
                    waiting = pending.Count;
                    if (waiting > 0 && !settled && !_heldLogged)
                    {
                        _heldLogged = true;
                        Plugin.Logger.LogInfo($"[Dedicated] {waiting} joiner(s) wait until the world has run {JoinSettleSeconds:0} s (JOIN-SETTLED-1).");
                    }
                    if (settled) _heldLogged = false;
                    // JOIN-AFTER-SAVE-1 (user ruling 2026-10-31): first the save, then the join - never both at once.
                    bool saving = MPSaveCoordinator.SaveBusy;
                    if (waiting > 0 && settled && saving && !_saveHeldLogged)
                    {
                        _saveHeldLogged = true;
                        Plugin.Logger.LogInfo($"[Dedicated] {waiting} joiner(s) wait for the save under way (JOIN-AFTER-SAVE-1).");
                    }
                    if (!saving) _saveHeldLogged = false;
                    if (MPConfig.DedicatedAutoAccept && settled && !saving && waiting > 0 && PreJoinSaveDone())
                        foreach (var (peerId, playerId) in pending)
                        {
                            Plugin.Logger.LogInfo($"[Dedicated] auto-accepting '{playerId}'.");
                            MPServer.AcceptPendingJoin(peerId);
                        }
                }
                catch (Exception ex) { Plugin.Logger.LogWarning($"[Dedicated] auto-accept: {ex.Message}"); }

                int present = MPServer.RemotePlayerCount + waiting;
                if (present != _lastPresent)
                {
                    _lastPresent = present;
                    Plugin.Logger.LogInfo($"[Dedicated] players present: {present}.");
                }

                // 3. a dropped player's disconnect pause must not freeze the players who are still here
                if (present > 0 && MPServer.PausedByDisconnect)
                {
                    Plugin.Logger.LogInfo("[Dedicated] lifting the disconnect pause - other players are present.");
                    MPServer.ResumeFromDisconnectPause();
                }

                // 1. idle pause - also while every connected player is in the pause menu or away (DEDICATED-AFK-1)
                string away = present > 0 && waiting == 0 ? EveryoneAway() : "";
                if (present > 0 && away.Length == 0)
                {
                    _emptySince = -1f;
                    if (MPServer.PausedByIdle)
                    {
                        if (_pausedForAway) Plugin.Logger.LogInfo("[Dedicated] a player is active again - world resumes.");
                        _pausedForAway = false;
                        MPServer.SetIdlePause(false);
                    }
                }
                else if (present > 0)
                {
                    _emptySince = -1f;
                    if (!MPServer.PausedByIdle)
                    {
                        Plugin.Logger.LogInfo($"[Dedicated] pausing the world: {away}.");
                        _pausedForAway = true;
                        MPServer.SetIdlePause(true);
                    }
                }
                else
                {
                    if (_emptySince < 0f) _emptySince = Time.unscaledTime;
                    if (!MPServer.PausedByIdle && Time.unscaledTime - _emptySince >= MPConfig.DedicatedIdleGraceSeconds)
                        MPServer.SetIdlePause(true);
                }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Dedicated] tick: {ex.Message}"); }
        }

        /// <summary>DEDICATED-KEY-1: the key players join with - the TelemetryKey setting, else the key the collector
        /// created (&lt;root&gt;/telemetry-data/key.txt). "" = none known (joins are not checked). Any thread.</summary>
        public static string ServerJoinKey()
        {
            string k = MPConfig.TelemetryKey;
            if (k.Length > 0) return k;
            try
            {
                string dir = HeadlessHost.DirPath.TrimEnd('/', '\\');
                if (dir.Length == 0) return "";
                string p = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dir) ?? "", "telemetry-data", "key.txt");
                return System.IO.File.Exists(p) ? System.IO.File.ReadAllText(p).Trim() : "";
            }
            catch { return ""; }
        }

        /// <summary>DEDICATED-KEY-1: the web folder for key-protected files: the first 16 hex digits of SHA-256(lower-case key), so the
        /// address never shows the key itself. bamp-start.sh, ranking.html and the client compute the same.</summary>
        public static string KeyFolder(string key)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key.Trim().ToLowerInvariant()))).Replace("-", "").ToLowerInvariant().Substring(0, 16);
        }

        /// <summary>SERVER-MODS-1: bamp-start.sh published this server's extra mods for download
        /// (&lt;root&gt;/telemetry-data/web/&lt;KeyFolder&gt;/mods/mods.json lists at least one). Any thread.</summary>
        public static bool ModsPublished()
        {
            try
            {
                string key = ServerJoinKey(), dir = HeadlessHost.DirPath.TrimEnd('/', '\\');
                if (key.Length == 0 || dir.Length == 0) return false;
                string p = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dir) ?? "", "telemetry-data", "web", KeyFolder(key), "mods", "mods.json");
                return System.IO.File.Exists(p) && System.IO.File.ReadAllText(p).Contains("\"folder\"");
            }
            catch { return false; }
        }

        // ── DEDICATED-AFK-1: players in the pause menu / away ──────────────────────────────────────────────────────
        private sealed class Activity { public bool Menu; public int Idle; public float At; }
        private static readonly Dictionary<string, Activity> _activity = new Dictionary<string, Activity>();
        private static bool _pausedForAway;

        /// <summary>Network thread: a client's ClientActivity report.</summary>
        public static void ReportActivity(string playerId, bool inMenu, int idleSeconds)
        {
            lock (_activity) _activity[playerId] = new Activity { Menu = inMenu, Idle = Math.Max(0, idleSeconds), At = _clockNow };
        }
        private static volatile float _clockNow;   // main-thread clock mirrored for the network thread

        /// <summary>Main thread. "" while someone counts as active; otherwise why everyone is away. A player without a
        /// fresh report (older client, report lost) always counts as active; a running consensus skip is never paused
        /// (waiting for it is idle by design).</summary>
        private static string EveryoneAway()
        {
            try
            {
                if (MPRestSync.SkipActive) return "";
                int afk = MPConfig.DedicatedAfkMinutes * 60;
                bool menuCounts = MPConfig.DedicatedPauseInMenu;
                int n = 0, menus = 0;
                foreach (var pid in MPServer.LobbyPlayers)
                {
                    if (pid == MPConfig.PlayerId) continue;   // the server's own character
                    Activity? a;
                    lock (_activity) _activity.TryGetValue(pid, out a);
                    if (a == null || _clockNow - a.At > 20f) return "";
                    n++;
                    if (menuCounts && a.Menu) { menus++; continue; }
                    if (afk > 0 && a.Idle + (_clockNow - a.At) >= afk) continue;
                    return "";
                }
                if (n == 0) return "";
                return menus == n ? "every player is in the pause menu"
                     : menus == 0 ? $"no input from any player for {MPConfig.DedicatedAfkMinutes} min"
                     : "every player is in the pause menu or away";
            }
            catch { return ""; }
        }

        /// <summary>True when the hosted lobby is still open after an auto-start try: the load was refused (the lobby is left
        /// as it was) and the retry has to stop hosting first.</summary>
        public static bool StuckInLobby
        {
            get
            {
                try
                {
                    return MPServer.IsRunning && MPServer.IsInLobby && _attempts > 0
                        && Time.realtimeSinceStartup - _lastAttemptAt >= AutoStartRetrySeconds;
                }
                catch { return false; }
            }
        }

        /// <summary>True when the main menu should host and load the configured save now (4). The caller records the attempt
        /// with <see cref="NoteAutoStartAttempt"/>.</summary>
        public static bool WantsAutoStart(bool inGame, out string session)
        {
            session = "";
            try
            {
                if (!MPConfig.DedicatedHost) return false;
                if (inGame || MPClient.IsConnected || MPClient.IsConnecting) return false;
                if (MPServer.IsRunning && !StuckInLobby) return false;
                if (Time.realtimeSinceStartup < AutoStartSettleSeconds) return false;
                session = EffectiveSession();
                if (session.Length == 0)
                {
                    if (!_warnedNoSession)
                    {
                        _warnedNoSession = true;
                        Plugin.Logger.LogWarning("[Dedicated] no saved game to host: DedicatedSession is empty and the multiplayer saves could not be read (see the lines above).");
                    }
                    return false;
                }
                if (_attempts >= MaxAutoStartAttempts)
                {
                    if (!_warnedGaveUp)
                    {
                        _warnedGaveUp = true;
                        Plugin.Logger.LogWarning($"[Dedicated] auto-start gave up after {MaxAutoStartAttempts} attempts for '{session}' - check the save name and the log above.");
                    }
                    return false;
                }
                return Time.realtimeSinceStartup - _lastAttemptAt >= AutoStartRetrySeconds;
            }
            catch { return false; }
        }

        public static void NoteAutoStartAttempt()
        {
            _attempts++;
            _lastAttemptAt = Time.realtimeSinceStartup;
        }

        /// <summary>True when an unattended host cannot get going on its own (no save configured, or every auto-start try
        /// failed). A headless host exits then, so its supervisor (AMP, the start script) reports it instead of idling.</summary>
        public static bool NeedsOperator(out string why)
        {
            why = "";
            try
            {
                if (!MPConfig.DedicatedHost) return false;
                if (MPClient.IsConnected || MPClient.IsConnecting) return false;
                if (MPServer.IsRunning && !MPServer.IsInLobby) return false;   // a world is loading or running
                if (Time.realtimeSinceStartup >= 90f && EffectiveSession().Length == 0)
                {
                    why = "there is no saved game to host (DedicatedSession empty, the multiplayer saves could not be read).";
                    return true;
                }
                if (_attempts >= MaxAutoStartAttempts && Time.realtimeSinceStartup - _lastAttemptAt >= AutoStartRetrySeconds)
                {
                    why = $"auto-start failed {MaxAutoStartAttempts} times for '{EffectiveSession()}'.";
                    return true;
                }
            }
            catch { }
            return false;
        }

        // ── DEDICATED-NEWGAME-1: create the configured save when it does not exist yet ──────────────────────────────────
        // An unattended host cannot click through "Start New Game" and character creation. With DedicatedNewGame set and no
        // save of the DedicatedSession playthrough on disk, the main menu hosts and starts a new world instead of loading
        // (MPCanvasUI.TickDedicated), TickNewGame confirms character creation with PlayerId, and once the world runs it is
        // saved under the DedicatedSession name - from then on the normal auto-start loads it. One try per process: if it
        // fails, the load path (and its operator exit) takes over. FIRST-WORLD-1: the first world of a server without any
        // save ("Server") is made the same way, with Normal while DedicatedNewGame is off.
        private enum NewGamePhase { None, Customizer, WaitWorld, Done }
        private static NewGamePhase _newGamePhase;
        private static string _newGameSession = "";
        private static bool   _newGameTried;
        private static float  _newGamePhaseSince;
        private static float  _newGameNextTickAt;

        /// <summary>True when the configured save does not exist yet and DedicatedNewGame asks for a new world, or it is the
        /// first world of a server without any save (FIRST-WORLD-1). <paramref name="difficulty"/> = the one to use.</summary>
        public static bool WantsNewGame(string configured, out string difficulty)
        {
            difficulty = NewGameDifficulty(configured);
            if (_newGameTried || difficulty.Length == 0 || configured.Length == 0) return false;
            return SaveCandidates(configured).Count == 0;
        }

        /// <summary>The difficulty of a new world: "New world: difficulty"; while that is Off, Normal for the first world of a
        /// server without any multiplayer save (FIRST-WORLD-1), "" (no new world) for any other.</summary>
        private static string NewGameDifficulty(string world)
        {
            string d = MPConfig.DedicatedNewGame;
            if (d.Length == 0 && _noSaveYet && world == FirstWorldName && MPConfig.DedicatedSession.Length == 0) d = FirstWorldDifficulty;
            return d;
        }

        /// <summary>NEWGAME-CUSTOM-1: the settings of a new world. Easy / Normal / Hard are the game's own presets; "Custom" is the
        /// game's "Custom Game" screen on AMP's settings page (NewGame* keys): the Normal preset with every value the screen
        /// offers replaced, each clamped to the range of the screen's slider (CustomGameOptionsHandler, main menu scene).
        /// Multipliers are percentages on AMP's page, as on the game's sliders.</summary>
        public static GameVariablesDto NewGameSettings(string difficulty)
        {
            if (difficulty != "Custom") return MPServer.Preset(difficulty);
            var dto = MPServer.Preset("Normal");
            dto.Difficulty = "Custom";
            dto.TutorialEnabled = false;   // the game's Custom Game has no tutorial either (CustomGameOptionsHandler.SetPreset)
            int I(string key, int def, int min, int max)
                => int.TryParse(MPConfig.GetOption(key, "").Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v) ? Mathf.Clamp(v, min, max) : def;
            float P(string key, float def, int min, int max)   // AMP: percent -> the game's factor
                => float.TryParse(MPConfig.GetOption(key, "").Trim().TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? Mathf.Clamp(v, min, max) / 100f : def;
            bool B(string key) => string.Equals(MPConfig.GetOption(key, "").Trim(), "true", StringComparison.OrdinalIgnoreCase);
            dto.DaysPerYear                       = I("NewGameDaysPerYear", dto.DaysPerYear, 30, 365);
            dto.StartingMoney                     = I("NewGameStartingMoney", dto.StartingMoney, 0, 2000000000);
            dto.RivalsDifficultyMultiplier        = P("NewGameRivalAttacks", dto.RivalsDifficultyMultiplier, 0, 150);
            dto.StartingAge                       = I("NewGameStartingAge", dto.StartingAge, 18, 65);
            dto.DisableAging                      = B("NewGameDisableAging");
            dto.DisableHappiness                  = B("NewGameDisableHappiness");
            dto.DisableEnergy                     = B("NewGameDisableEnergy");
            // NEEDS-OFF-1 (user 2026-11-01, server 3: "hunger and energy are off, but run again after a reconnect / a server
            // restart"): the needs dial (MPNeedsTuning) re-aligns the game's disableEnergy on every machine with every 3 s
            // heartbeat - only drain 0 is "off". "Energy off" is drain 0 for this world; the save keeps it.
            if (dto.DisableEnergy) dto.NeedsDrainPercent = 0;
            dto.AllCoursesUnlocked                = B("NewGameAllCoursesUnlocked");
            dto.AllContactsUnlocked               = B("NewGameAllContactsUnlocked");
            dto.DisableVehicleDamage              = B("NewGameDisableVehicleDamage");
            dto.DisableVehicleFuel                = B("NewGameDisableVehicleFuel");
            dto.TaxPercentage                     = I("NewGameTaxPercentage", dto.TaxPercentage, 0, 100);
            dto.EmployeeHourlySalaryMultiplier    = P("NewGameSalary", dto.EmployeeHourlySalaryMultiplier, 50, 130);
            dto.MarketPriceMultiplier             = P("NewGamePublicPrices", dto.MarketPriceMultiplier, 70, 130);
            dto.BankInterestMultiplier            = P("NewGameBankInterest", dto.BankInterestMultiplier, 70, 130);
            dto.BaseCustomerPromotionMultiplier   = P("NewGameCustomerPromotion", dto.BaseCustomerPromotionMultiplier, 10, 100);
            dto.WholesaleUrgentFeeMultiplier      = P("NewGameWholesaleUrgentFee", dto.WholesaleUrgentFeeMultiplier, 0, 200);
            dto.ImporterUrgentFeeMultiplier       = P("NewGameImporterUrgentFee", dto.ImporterUrgentFeeMultiplier, 0, 200);
            dto.DisableWholesaleAndImportLimits   = B("NewGameDisableImportLimits");
            dto.AllProductsAvailableFromImporters = B("NewGameAllProductsFromImporters");
            dto.ExportMultiplier                  = P("NewGameExportPrices", dto.ExportMultiplier, 10, 130);
            dto.SellingMultiplier                 = P("NewGameResaleValue", dto.SellingMultiplier, 10, 100);
            Plugin.Logger.LogInfo($"[Dedicated] new world, Custom: days/year {dto.DaysPerYear}, money {dto.StartingMoney}, age {dto.StartingAge}, tax {dto.TaxPercentage}%, "
                + $"rivals x{dto.RivalsDifficultyMultiplier:0.00}, salary x{dto.EmployeeHourlySalaryMultiplier:0.00}, prices x{dto.MarketPriceMultiplier:0.00}, "
                + $"interest x{dto.BankInterestMultiplier:0.00}, promotion x{dto.BaseCustomerPromotionMultiplier:0.00}, urgent x{dto.WholesaleUrgentFeeMultiplier:0.00}/x{dto.ImporterUrgentFeeMultiplier:0.00}, "
                + $"export x{dto.ExportMultiplier:0.00}, resale x{dto.SellingMultiplier:0.00}; off: aging={dto.DisableAging} happiness={dto.DisableHappiness} energy={dto.DisableEnergy} "
                + $"damage={dto.DisableVehicleDamage} fuel={dto.DisableVehicleFuel} limits={dto.DisableWholesaleAndImportLimits}; all courses={dto.AllCoursesUnlocked} contacts={dto.AllContactsUnlocked} importer products={dto.AllProductsAvailableFromImporters}.");
            return dto;
        }

        /// <summary>Main thread, right after MPServer.StartNewGame was called for <paramref name="session"/>.</summary>
        public static void BeginNewGame(string session, string difficulty)
        {
            _newGameTried = true;
            _newGameSession = session;
            _newGamePhase = NewGamePhase.Customizer;
            _newGamePhaseSince = Time.realtimeSinceStartup;
            Plugin.Logger.LogInfo($"[Dedicated] no save '{session}' yet - starting a new world ({difficulty}); it will be saved under that name.");
        }

        /// <summary>Main thread, every frame (throttled inside). Drives character creation and the first save of a new world.</summary>
        public static void TickNewGame()
        {
            if (_newGamePhase == NewGamePhase.None || _newGamePhase == NewGamePhase.Done) return;
            if (Time.realtimeSinceStartup < _newGameNextTickAt) return;
            _newGameNextTickAt = Time.realtimeSinceStartup + 0.5f;
            try
            {
                if (_newGamePhase == NewGamePhase.Customizer)
                {
                    if (MPServer.WorldRunning) { SetNewGamePhase(NewGamePhase.WaitWorld); return; }
                    var cust = MPLifecycle.IntroInstance;
                    if (cust == null || !cust.isActiveAndEnabled) { _newGamePhaseSince = Time.realtimeSinceStartup; return; }
                    // let the screen finish its Start()/coroutines (the TestDrive charconfirm lever's proven minimum)
                    if (Time.realtimeSinceStartup - _newGamePhaseSince < 2f) return;
                    var picker = cust.characterNamePicker;
                    if (picker != null && (!picker.HasANameSet || picker.HasInvalidCharacters))
                    {
                        string name = MPSaveCoordinator.SanitizeSession(MPConfig.PlayerId);
                        picker.nameField.text = name.Length > 0 ? name : "Server";
                    }
                    cust.StartGame();
                    Plugin.Logger.LogInfo($"[Dedicated] new world: character '{picker?.GetName()}' confirmed - loading the city.");
                    SetNewGamePhase(NewGamePhase.WaitWorld);
                    return;
                }
                // WaitWorld: save once the world has run for a moment
                if (!MPServer.IsRunning) { Plugin.Logger.LogWarning("[Dedicated] new world: hosting stopped before the first save."); SetNewGamePhase(NewGamePhase.Done); return; }
                if (!MPServer.WorldRunning) { _newGamePhaseSince = Time.realtimeSinceStartup; return; }
                if (Time.realtimeSinceStartup - _newGamePhaseSince < 10f) return;
                MPSaveCoordinator.MenuSave(false, _newGameSession);
                Plugin.Logger.LogInfo($"[Dedicated] new world saved as '{_newGameSession}' - the next start loads it.");
                SetNewGamePhase(NewGamePhase.Done);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[Dedicated] new world ({_newGamePhase}): {ex.Message}");
                SetNewGamePhase(NewGamePhase.Done);
            }
        }

        private static void SetNewGamePhase(NewGamePhase p)
        {
            _newGamePhase = p;
            _newGamePhaseSince = Time.realtimeSinceStartup;
        }

        // ── DEDICATED-SAVES-1: which world, which save ──────────────────────────────────────────────────────────────────
        private static string _effective = "";
        private static float _effectiveAt = -999f;
        private static string? _override;   // null = not resolved yet this process
        private static string _overrideFrom = "";
        private const string LoadNextFile = "load-next.txt", LoadAppliedFile = "load-applied.txt";

        /// <summary>The world to host: DedicatedSession; empty = the world played last (newest multiplayer save on this
        /// machine); with no save at all a new world called "Server" (FIRST-WORLD-1; with DedicatedNewGame on also when the
        /// saves cannot be read, as before). "" = nothing to host. Re-resolved at most every 30 s (file IO).</summary>
        public static string EffectiveSession()
        {
            string fresh = NewWorldWanted();   // NEW-WORLD-NAME-1: a world asked for by name comes first, until it exists
            if (fresh.Length > 0) return fresh;
            string cfg = MPConfig.DedicatedSession;
            if (cfg.Length > 0) return cfg;
            if (Time.realtimeSinceStartup - _effectiveAt < 30f) return _effective;
            _effectiveAt = Time.realtimeSinceStartup;
            string found = "";
            int count = -1;   // -1 = the list could not be read
            try
            {
                var runs = MPSaveManager.ListPlaythroughs();   // newest playthrough first
                count = runs.Count;
                if (count > 0) found = runs[0].Base;
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Dedicated] newest world lookup: {ex.Message}"); }
            // FIRST-WORLD-1: only when the list was read - a read error must never put a new world in place of existing ones
            _noSaveYet = count == 0;
            if (found.Length == 0 && (_noSaveYet || MPConfig.DedicatedNewGame.Length > 0)) found = FirstWorldName;
            if (found != _effective)
                Plugin.Logger.LogInfo(found.Length == 0 ? "[Dedicated] DedicatedSession is empty and the multiplayer saves could not be read."
                    : _noSaveYet ? $"[Dedicated] no multiplayer save on this machine yet - the first world '{found}' is created with difficulty {NewGameDifficulty(found)} (FIRST-WORLD-1)."
                    : $"[Dedicated] DedicatedSession is empty - hosting the world played last: '{found}'.");
            _effective = found;
            return found;
        }

        // JOIN-AFTER-SAVE-1 (user ruling 2026-10-31): "first save, then the join" - one coordinated save for the joiners who
        // wait, then they are let in once it has run. A save that is held (someone else is loading in) or deferred gets a
        // minute before the joiners are let in anyway.
        private static int _preJoinSeq = -1;
        private static float _preJoinAt;

        private static bool PreJoinSaveDone()
        {
            if (_preJoinSeq < 0)
            {
                _preJoinSeq = MPSaveCoordinator.SavesStarted;
                _preJoinAt = _clockNow;
                Plugin.Logger.LogInfo("[Dedicated] saving the world before letting the joiner(s) in (JOIN-AFTER-SAVE-1).");
                MPSaveCoordinator.HostSaveNow("join");
                return false;
            }
            if (MPSaveCoordinator.SavesStarted <= _preJoinSeq && _clockNow - _preJoinAt < 60f) return false;
            if (MPSaveCoordinator.SaveBusy) return false;
            _preJoinSeq = -1;
            return true;
        }

        private static string _freshName = "";
        private static bool _freshMissing, _freshOffWarned;
        private static float _freshAt = -100f;

        /// <summary>NEW-WORLD-NAME-1: AMP's "New world: name" while no save of that world exists yet, else "". The world played
        /// last could never be replaced by a new one before: "World" only lists worlds that exist, and "New world: difficulty"
        /// acted only on a world that does not. Looked up at most every 30 s.</summary>
        private static string NewWorldWanted()
        {
            string name = MPConfig.DedicatedNewWorld;
            if (name.Length == 0) return "";
            if (MPConfig.DedicatedNewGame.Length == 0)
            {
                if (!_freshOffWarned)
                {
                    _freshOffWarned = true;
                    Plugin.Logger.LogWarning($"[Dedicated] \"New world: name\" is '{name}', but \"New world: difficulty\" is Off - no new world is created.");
                }
                return "";
            }
            if (name != _freshName || Time.realtimeSinceStartup - _freshAt >= 30f)
            {
                bool missing = SaveCandidates(name).Count == 0;
                if (missing && (!_freshMissing || name != _freshName))
                    Plugin.Logger.LogInfo($"[Dedicated] new world '{name}' (\"New world: name\") does not exist yet - it is created with difficulty {MPConfig.DedicatedNewGame}.");
                _freshName = name;
                _freshAt = Time.realtimeSinceStartup;
                _freshMissing = missing;
            }
            return _freshMissing ? name : "";
        }

        /// <summary>A save chosen for THIS start instead of the newest: the console's 'load' (headless/load-next.txt, used
        /// once) or the DedicatedLoadSave setting (used once per value - headless/load-applied.txt remembers it, so the
        /// next restart goes back to the newest save). "" = none.</summary>
        private static string LoadOverride()
        {
            if (_override != null) return _override;
            _override = "";
            string dir = HeadlessHost.DirPath;
            try
            {
                string next = dir.Length > 0 ? System.IO.Path.Combine(dir, LoadNextFile) : "";
                if (next.Length > 0 && System.IO.File.Exists(next))
                {
                    _override = System.IO.File.ReadAllText(next).Trim();
                    System.IO.File.Delete(next);
                    _overrideFrom = "console 'load'";
                }
                else
                {
                    string want = MPConfig.DedicatedLoadSave;
                    string applied = dir.Length > 0 ? System.IO.Path.Combine(dir, LoadAppliedFile) : "";
                    string done = applied.Length > 0 && System.IO.File.Exists(applied) ? System.IO.File.ReadAllText(applied).Trim() : "";
                    if (want.Length > 0 && !string.Equals(want, done, StringComparison.Ordinal))
                    {
                        _override = want;
                        _overrideFrom = "setting DedicatedLoadSave";
                        if (applied.Length > 0) System.IO.File.WriteAllText(applied, want);
                    }
                }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Dedicated] load choice: {ex.Message}"); }
            return _override;
        }

        /// <summary>'#N' (N-th newest of the world) or an exact save name of any world -> (save, playthrough); null = no match.</summary>
        private static (string Name, string Pid)? ResolveSave(string world, string choice)
        {
            var cands = SaveCandidates(world);
            if (choice.StartsWith("#") && int.TryParse(choice.Substring(1), out int n))
                return n >= 1 && n <= cands.Count ? cands[n - 1] : ((string, string)?)null;
            foreach (var c in cands)
                if (string.Equals(c.Name, choice, StringComparison.OrdinalIgnoreCase)) return c;
            try
            {
                foreach (var run in MPSaveManager.ListPlaythroughs())
                    foreach (var v in run.Variants)
                        if (string.Equals(v.SessionName, choice, StringComparison.OrdinalIgnoreCase)) return (v.SessionName, v.PlaythroughId ?? "");
            }
            catch { }
            return null;
        }

        /// <summary>Console 'saves': the worlds on this machine and the loadable saves of the hosted one, numbered newest first.</summary>
        public static List<string> DescribeSaves()
        {
            var lines = new List<string>();
            string world = EffectiveSession();
            try
            {
                var runs = MPSaveManager.ListPlaythroughs();
                lines.Add(runs.Count == 0 ? "no multiplayer saves on this server" :
                    "worlds: " + string.Join(", ", runs.ConvertAll(r => $"'{r.Base}' (day {r.NewestDay}, {Stamp(r.NewestUnix)})")));
                foreach (var run in runs)
                {
                    if (!string.Equals(run.Base, MPSaveManager.StripToBase(world), StringComparison.OrdinalIgnoreCase)) continue;
                    var usable = run.Variants.FindAll(v => v.Kind == "Main" || v.Kind == "Autosave" || v.Kind == "Disconnect" || v.Kind == "Recover");
                    usable.Sort((a, b) => b.SavedAtUnix.CompareTo(a.SavedAtUnix));
                    lines.Add($"saves of '{run.Base}' (load with: load #N or load <name>):");
                    for (int i = 0; i < usable.Count; i++)
                        lines.Add($"#{i + 1} {usable[i].SessionName} - {usable[i].Kind}, day {usable[i].Day}, {Stamp(usable[i].SavedAtUnix)}");
                }
            }
            catch (Exception ex) { lines.Add("ERR " + ex.Message); }
            return lines;
        }

        private static string Stamp(long unix)
            => unix > 0 ? DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "?";

        /// <summary>Console 'load': checks the choice, then remembers it for the next start (the caller restarts).</summary>
        public static string RequestLoad(string choice, out bool ok)
        {
            ok = false;
            choice = (choice ?? "").Trim();
            if (choice.Length == 0) return "ERR usage: load #N | load <save name>  (see: saves)";
            var hit = ResolveSave(EffectiveSession(), choice);
            if (hit == null) return $"ERR no save '{choice}' - see: saves";
            string dir = HeadlessHost.DirPath;
            if (dir.Length == 0) return "ERR no headless folder to remember the choice in";
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, LoadNextFile), hit.Value.Name);
            ok = true;
            return $"OK '{hit.Value.Name}' will be loaded - restarting the server (the running world is saved first)";
        }

        /// <summary>Main thread. The save to load for this attempt: the newest usable save of the playthrough the configured
        /// name belongs to; each further attempt takes the next older one. Falls back to the name as configured.
        /// DEDICATED-SAVES-1: the first attempt honours a chosen save (console 'load' / DedicatedLoadSave).</summary>
        public static string PickSaveToLoad(string configured, out string playthroughId)
        {
            playthroughId = "";
            string choice = _attempts <= 1 ? LoadOverride() : "";
            if (choice.Length > 0)
            {
                var hit = ResolveSave(configured, choice);
                if (hit != null)
                {
                    playthroughId = hit.Value.Pid;
                    Plugin.Logger.LogInfo($"[Dedicated] loading the chosen save '{hit.Value.Name}' ({_overrideFrom}: '{choice}') instead of the newest.");
                    return hit.Value.Name;
                }
                Plugin.Logger.LogWarning($"[Dedicated] chosen save '{choice}' ({_overrideFrom}) not found - loading the newest instead.");
            }
            var cands = SaveCandidates(configured);
            if (cands.Count == 0)
            {
                Plugin.Logger.LogWarning($"[Dedicated] no loadable multiplayer save is listed for '{configured}' - trying the name exactly as configured.");
                return configured;
            }
            int idx = Math.Min(Math.Max(0, _attempts - 1), cands.Count - 1);
            var pick = cands[idx];
            playthroughId = pick.Pid;
            Plugin.Logger.LogInfo($"[Dedicated] '{configured}': {cands.Count} save(s) of that playthrough, loading '{pick.Name}' (newest #{idx + 1}).");
            return pick.Name;
        }

        /// <summary>Usable saves of ONE playthrough (the newest world with that name - two worlds sharing a name are never
        /// mixed), newest first.</summary>
        private static List<(string Name, string Pid)> SaveCandidates(string configured)
        {
            var list = new List<(string Name, string Pid)>();
            try
            {
                string wantBase = MPSaveManager.StripToBase(configured);
                foreach (var run in MPSaveManager.ListPlaythroughs())   // newest playthrough first
                {
                    bool match = string.Equals(run.Base, wantBase, StringComparison.OrdinalIgnoreCase);
                    if (!match)
                        foreach (var v in run.Variants)
                            if (string.Equals(MPSaveManager.StripToBase(v.SessionName), wantBase, StringComparison.OrdinalIgnoreCase)) { match = true; break; }
                    if (!match) continue;
                    var usable = new List<MPSaveManager.MpVariant>();
                    foreach (var v in run.Variants)
                        if (v.Kind == "Main" || v.Kind == "Autosave" || v.Kind == "Disconnect" || v.Kind == "Recover") usable.Add(v);
                    usable.Sort((a, b) => b.SavedAtUnix.CompareTo(a.SavedAtUnix));
                    foreach (var v in usable) list.Add((v.SessionName, v.PlaythroughId ?? ""));
                    break;
                }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Dedicated] save lookup for '{configured}': {ex.Message}"); }
            return list;
        }
    }
}
