using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BAModAPI;
using HarmonyLib;
using UnityEngine;

// The loader discovers mod classes ONLY through this assembly attribute —
// extending ModBigAmbitionsBase alone logs "[ModDiscovery] No
// RegisterModClassAttribute attributes were found" and nothing runs.
[assembly: RegisterModClass(typeof(BigAmbitionsMP.ModEntry))]

namespace BigAmbitionsMP
{
    /// <summary>
    /// EA 0.11+ entry point — loaded by the game's OFFICIAL mod loader
    /// (BigAmbitions.ModsInternal): ModsLocal\BigAmbitionsMP\BigAmbitionsMP.dll
    /// with Harmony + LiteNetLib in the Dependencies\ subfolder.
    /// (The BepInEx/IL2CPP entry for EA 0.10 lives on the 'main' branch.)
    ///
    /// Scope: Initialization is the loader's PERSISTENT scope (ModLifecycleLoader.
    /// LifetimeScope) — loaded once at boot and NOT unloaded on menu↔city scene
    /// transitions, matching the old BepInEx chainloader lifetime.  MainMenu/City
    /// scopes get unloaded on every transition, which would tear down our
    /// patches, net stack and DontDestroyOnLoad UI mid-session.
    /// </summary>
    [ModEntryOnInitializationLoad]
    public class ModEntry : ModBigAmbitionsBase
    {
        public static ModEntry Instance { get; private set; } = null!;

        /// <summary>The three interchangeable Facepunch.Steamworks builds the game can ship
        /// (Windows x64 / macOS+Linux / Windows x86). Same namespaces, same types.</summary>
        private static readonly string[] SteamworksNames =
            { "Facepunch.Steamworks.Win64", "Facepunch.Steamworks.Posix", "Facepunch.Steamworks.Win32" };

        /// <summary>Re-entrancy guard: Assembly.Load of a name that is not installed raises
        /// AssemblyResolve again, so without this the handler would call itself forever.
        /// ThreadStatic because resolve events can arrive on more than one thread.</summary>
        [ThreadStatic] private static bool _resolving;

        /// <summary>
        /// Runs when the loader instantiates this entry, before OnLoadAsync. This assembly is
        /// compiled against ONE Steamworks build by name, but the Mac game ships a different
        /// one, so a run-time resolver makes the three names interchangeable and a single DLL
        /// loads on both platforms. On Windows the referenced Win64 assembly is always there
        /// and resolves normally — the handler is never asked for it, so nothing changes there.
        /// The body is wrapped: a static constructor that throws kills the whole mod.
        /// </summary>
        static ModEntry()
        {
            try { AppDomain.CurrentDomain.AssemblyResolve += ResolveSteamworks; } catch { }
        }

        /// <summary>Answers a request for any Steamworks build with whichever build is already
        /// loaded, else tries to load one of the others. Deliberately names no Steamworks TYPE:
        /// touching one here would need the very assembly this is trying to resolve.</summary>
        private static Assembly? ResolveSteamworks(object sender, ResolveEventArgs args)
        {
            try
            {
                if (_resolving) return null;

                string requested;
                try { requested = new AssemblyName(args.Name).Name; } catch { return null; }
                if (Array.IndexOf(SteamworksNames, requested) < 0) return null;   // not ours — answer at once

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string loaded;
                    try { loaded = asm.GetName().Name; } catch { continue; }
                    if (Array.IndexOf(SteamworksNames, loaded) >= 0) return ServedBy(requested, loaded, asm);
                }

                _resolving = true;
                try
                {
                    foreach (var other in SteamworksNames)
                    {
                        if (other == requested) continue;
                        try
                        {
                            var asm = Assembly.Load(other);
                            if (asm != null) return ServedBy(requested, other, asm);
                        }
                        catch { }
                    }
                }
                finally { _resolving = false; }

                LogBinding($"[Plugin] Steamworks binding: requested {requested}, no Facepunch.Steamworks assembly available");
                return null;
            }
            catch { return null; }
        }

        private static Assembly ServedBy(string requested, string served, Assembly asm)
        {
            LogBinding($"[Plugin] Steamworks binding: requested {requested}, served {served}");
            return asm;
        }

        /// <summary>The resolver can fire before Plugin's logger is up; fall back to Unity's.</summary>
        private static void LogBinding(string line)
        {
            try
            {
                var log = Plugin.Logger;
                if (log != null) log.LogInfo(line);
                else UnityEngine.Debug.Log(line);
            }
            catch { }
        }

        /// <summary>Patch classes that failed or bound nothing at load — a dead patch class is
        /// SILENT FEATURE LOSS (2026-07-09 audit: the slice-4 wallet guard shipped unapplied for two
        /// days). Rides every bug report so the field self-reports it.</summary>
        public static readonly System.Collections.Generic.List<string> PatchIssues = new();

        /// <summary>Round-229 (field 20260801-193731): count of OUR patch classes that THREW
        /// during boot patching (the loop walks this assembly only — other mods' hook failures
        /// never enter this number). One machine had all 253 fail (TypeLoadException storm,
        /// machine-local environment) yet the mod offered Host/Join as healthy — the host's own
        /// world load silently bounced while clients were sent into the world.</summary>
        public static int PatchFailCount;

        /// <summary>Two-tier response (user-directed light touch): a few failures WARN but
        /// never block (a game hotfix nicking one optional hook must not lock everyone out —
        /// the retired Escape guard shipped harmlessly dead for five weeks); at this many the
        /// failure is systemic (broken install, conflicting mod, or a game update newer than
        /// the mod) and MP entry is refused to protect shared saves.</summary>
        public const int PatchFailHardBlock = 10;

        public static bool MpDisabledByPatchFailure => PatchFailCount >= PatchFailHardBlock;
        public static bool PatchesDegraded => PatchFailCount > 0;

        /// <summary>BOOT-TERMINAL-1: what the load did, for the main menu's status panel (MPCanvasUI.TickBootTerminal).
        /// Plain facts only - the panel words them in the player's language once the game's texts are loaded.</summary>
        internal static int BootOkClasses, BootFailClasses, BootDeadClasses, BootPatchedMethods;
        internal static bool BootHarmonyDead, BootHarmonyOwnCopy, BootTinySwitch;
        internal static string BootHarmonyVersion = "", BootHarmonyFolder = "";
        internal static int BootTinySkipped { get { lock (_skippedTiny) return _skippedTinyMethods.Count; } }

        /// <summary>Round-254: the Harmony version this build compiled against (set by the
        /// provenance probe) and our own install root — both feed the culprit line when the
        /// SHARED Harmony copy another mod loaded first turns out broken or outdated.</summary>
        public static string CompiledHarmonyVersion = "?";
        internal static string ModRootForDiag = "";

        /// <summary>Round-254: name the loaded 0Harmony's ORIGIN so a player can self-diagnose
        /// which mod to remove. Load order is deterministic (workshop id ascending, decompile-
        /// verified ModDiscoveryRegistry:188), our id is young/high, so a broken OLDER mod wins
        /// the race every launch — the culprit is whoever's folder the loaded copy sits in.</summary>
        internal static string DescribeLoadedHarmony()
        {
            try
            {
                System.Reflection.Assembly? h = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    if (a.GetName().Name == "0Harmony") { h = a; break; }
                if (h == null) return "no Harmony library is loaded at all";
                string loc = ""; try { loc = h.Location ?? ""; } catch { }
                string ver = h.GetName().Version?.ToString() ?? "?";
                string mod = "an unknown mod";
                try
                {
                    if (IsInOwnFolder(loc))
                        mod = "this multiplayer mod's own copy";
                    else
                    {
                        var wm = System.Text.RegularExpressions.Regex.Match(loc, @"[\\/]workshop[\\/]content[\\/]1331550[\\/](\d+)[\\/]");
                        if (wm.Success)
                            mod = $"Workshop mod {wm.Groups[1].Value} (steamcommunity.com/sharedfiles/filedetails/?id={wm.Groups[1].Value})";
                        else
                        {
                            var lm = System.Text.RegularExpressions.Regex.Match(loc, @"[\\/]ModsLocal[\\/]([^\\/]+)[\\/]");
                            if (lm.Success) mod = $"locally installed mod '{lm.Groups[1].Value}'";
                        }
                    }
                }
                catch { }
                return $"Harmony v{ver}, loaded from {mod}\nFile: {loc}";
            }
            catch (Exception ex) { return $"(could not inspect the loaded Harmony: {ex.Message})"; }
        }

        /// <summary>Separator normalization is load-bearing: Unity's persistentDataPath
        /// uses FORWARD slashes while the loaded DLL's Location uses backslashes
        /// — the first rig test fell to the fallback branch on exactly this.</summary>
        private static string Norm(string p) => p.Replace('/', '\\').TrimEnd('\\');

        /// <summary>OWN-COPY-1 (field ff7c7fd9): a ModsLocal install's root reads
        /// 'C:/users/.../Big Ambitions\ModsLocal\GoingPublicServer' (forward slashes up to
        /// persistentDataPath) and the loaded Harmony's Location only has backslashes, so the
        /// plain StartsWith missed our own copy and the banner named this mod as "another".</summary>
        private static bool IsInOwnFolder(string loc)
            => !string.IsNullOrEmpty(ModRootForDiag) && !string.IsNullOrEmpty(loc)
               && Norm(loc).StartsWith(Norm(ModRootForDiag) + "\\", StringComparison.OrdinalIgnoreCase);

        /// <summary>Round-254b (user ruling: the banner names the mod, nothing else — the
        /// remedy is identical either way). Returns the short culprit line, and whether it
        /// actually points at ANOTHER mod (naming ourselves or nothing = generic message).</summary>
        /// <summary>2026-08-18 (user: "thats not how names work"): players see NAMES in
        /// their mod list, never Workshop ids — the Lord-Wolf report relayed "not showing
        /// up" instead of the culprit because the banner led with a number.  Resolve the
        /// culprit folder to its DISPLAY NAME via the game's own loader registry
        /// (ModDiscoveryRegistry.Entries → DiscoveredModEntry.ModFolder/ModDisplayName;
        /// names live inside mod DLLs, not on disk, so the registry is the only source).
        /// Reflection, not a compile reference: this path runs once, on a failing boot.
        /// The id + URL stay as fine print — support still wants them.</summary>
        private static string ResolveModDisplayName(string culpritFolder)
        {
            try
            {
                string want = Norm(culpritFolder);
                string wantLeaf = System.IO.Path.GetFileName(want);
                string leafHit = "";
                var regT = HarmonyLib.AccessTools.TypeByName("BigAmbitions.ModsInternal.ModDiscoveryRegistry");
                var entriesProp = regT == null ? null : HarmonyLib.AccessTools.Property(regT, "Entries");
                if (entriesProp?.GetValue(null) is not System.Collections.IDictionary byScope) return "";
                foreach (System.Collections.DictionaryEntry scope in byScope)
                {
                    if (scope.Value is not System.Collections.IEnumerable list) continue;
                    foreach (var entry in list)
                    {
                        if (entry == null) continue;
                        var t = entry.GetType();
                        string folder = Norm(HarmonyLib.AccessTools.Property(t, "ModFolder")?.GetValue(entry) as string ?? "");
                        if (folder.Length == 0) continue;
                        string name = HarmonyLib.AccessTools.Property(t, "ModDisplayName")?.GetValue(entry) as string ?? "";
                        if (string.Equals(folder, want, StringComparison.OrdinalIgnoreCase)
                            || want.StartsWith(folder + "\\", StringComparison.OrdinalIgnoreCase))
                            return name;
                        // Last-resort: same leaf folder name (covers differently-rooted
                        // registry paths); only used if no full-path match exists.
                        if (leafHit.Length == 0 && wantLeaf.Length > 0
                            && string.Equals(System.IO.Path.GetFileName(folder), wantLeaf, StringComparison.OrdinalIgnoreCase))
                            leafHit = name;
                    }
                }
                return leafHit;
            }
            catch { }
            return "";
        }

        internal static string CulpritShort(out bool isOtherMod)
        {
            isOtherMod = false;
            try
            {
                System.Reflection.Assembly? h = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    if (a.GetName().Name == "0Harmony") { h = a; break; }
                string loc = ""; try { loc = h?.Location ?? ""; } catch { }
                if (string.IsNullOrEmpty(loc)) return "";
                if (IsInOwnFolder(loc)) return "";
                var wm = System.Text.RegularExpressions.Regex.Match(loc, @"[\\/]workshop[\\/]content[\\/]1331550[\\/](\d+)[\\/]");
                if (wm.Success)
                {
                    isOtherMod = true;
                    string folder = "";
                    try { folder = loc.Substring(0, wm.Index + wm.Length).TrimEnd('\\', '/'); } catch { }
                    string name = folder.Length > 0 ? ResolveModDisplayName(folder) : "";
                    string headline = name.Length > 0
                        ? $"\"{name}\""
                        : $"Workshop mod {wm.Groups[1].Value}";   // name unresolvable → the id is still better than nothing
                    return $"{headline}\n(Workshop item {wm.Groups[1].Value} — steamcommunity.com/sharedfiles/filedetails/?id={wm.Groups[1].Value})"
                         + (folder.Length > 0 ? $"\nFolder: {folder}" : "");
                }
                var lm = System.Text.RegularExpressions.Regex.Match(loc, @"^(.*[\\/]ModsLocal[\\/][^\\/]+)");
                if (lm.Success)
                {
                    isOtherMod = true;
                    string folder = lm.Groups[1].Value;
                    string name = ResolveModDisplayName(folder);
                    return (name.Length > 0 ? $"\"{name}\"\n" : $"Local mod '{System.IO.Path.GetFileName(folder)}'\n")
                         + $"Folder: {folder}";
                }
                return "";
            }
            catch { return ""; }
        }

        /// <summary>Round-254b: the ONE banner text for every Harmony-conflict shape —
        /// concise, names the conflicting mod, presents both removals neutrally. The
        /// window itself never times out (dismiss-on-OK), so it is read at leisure.
        /// All classification detail (incomplete vs outdated, versions, file paths)
        /// stays in the LOG lines the callers emit.</summary>
        internal static string BuildConflictBanner()
        {
            // Round-254c: EXACT user-specified wording (2026-08-13) — name the culprit,
            // nothing else. No remedy text, no technical detail (all of that is in the log).
            string culprit = CulpritShort(out bool other);
            return other
                ? $"Another installed mod is incompatible with {MyPluginInfo.DISPLAY_NAME}:\n\n{culprit}"
                : $"{MyPluginInfo.DISPLAY_NAME} failed to load and is disabled. Details are in the game log.";
        }

        public static string PatchFailureNotice =>
            $"{MyPluginInfo.SHORT_NAME} could not attach to the game ({PatchFailCount} hook(s) failed) — " +
            "multiplayer is disabled to protect your saves. Verify the game files in Steam " +
            "(right-click the game > Properties > Installed Files), and check for a mod update; " +
            "if neither fixes it, remove other mods and retry.";

        public static string PatchDegradedNotice =>
            $"{MyPluginInfo.SHORT_NAME}: {PatchFailCount} game hook(s) failed to install. Multiplayer will run, " +
            "but some features may misbehave. If you notice problems, verify the game files in Steam " +
            "and check for a mod update.";

        private Harmony? _harmony;
        private GameObject? _uiHost;

        /// <summary>BOOT-TERMINAL-2: async so the load can hand frames back to the game's loading screen between steps
        /// (BootConsole.Frame) - the console then shows each step as it happens. On the dedicated server (no console) the
        /// awaits complete at once and the load runs straight through, as it always did.</summary>
        public override async Task OnLoadAsync(ModContext context)
        {
            Instance = this;
            Plugin.Logger = new ModLog(context.Logger);

            // ── Double-install guard (Workshop readiness) ─────────────────────
            // The game loads mods from BOTH ModsLocal and Steam Workshop with no
            // dedup — a Manager install plus a Workshop subscription would run
            // TWO copies of this mod (two Harmony patch sets = breakage).
            // Assembly statics are per-copy, so coordinate through AppDomain
            // data slots: the first copy claims the singleton slot; any later
            // copy records itself in the duplicate slot (the live copy's UI
            // polls it and warns the player) and loads NOTHING.
            var priorRoot = AppDomain.CurrentDomain.GetData("BAMP_SINGLETON_ROOT") as string;
            if (priorRoot != null)
            {
                AppDomain.CurrentDomain.SetData("BAMP_DUPLICATE_ROOT", context.ModRootPath ?? "unknown");
                Plugin.Logger.LogError(
                    $"{MyPluginInfo.DISPLAY_NAME}: another copy is already loaded from '{priorRoot}' — " +
                    $"this copy ('{context.ModRootPath}') will NOT start. Keep ONE install: either the " +
                    "Steam Workshop subscription or the local ModsLocal copy, not both.");
                return;
            }
            AppDomain.CurrentDomain.SetData("BAMP_SINGLETON_ROOT", context.ModRootPath ?? "unknown");
            ModRootForDiag = context.ModRootPath ?? "";   // round-254: culprit-vs-own-copy check

            Plugin.Logger.LogInfo($"BigAmbitionsMP loading (official loader, modId='{context.ModId}', root='{context.ModRootPath}')...");

            MPConfig.Init(context.ModRootPath);
            // LOG-LEVEL-1: a dedicated server's console shows the important lines unless AMP asks for everything
            ModLog.Quiet = MPConfig.DedicatedHost && !string.Equals(MPConfig.GetOption("LogLevel", "normal"), "verbose", StringComparison.OrdinalIgnoreCase);
            SteamNetConfig.StartWatch();   // H-STEAMNET-1: Steam networking config (direct path + send caps), applied once Steam is valid
            // 2026-09-05 colours: register the game's Mods tab section (no rows known yet at load, so this is a no-op /
            // removal until a session provides names); every later roster change re-registers it from PlayerColours.Tick.
            try { PlayerColours.ModId = context.ModId; PlayerColours.RegisterOptions(); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Colours] options registration: {ex.Message}"); }
            MPBugReport.MarkSessionStarted();
            PlayerReports.InstallHook();   // PLAYER-REPORTS-1: the log's errors and check lines are counted from here on (the player's game only)
            MPBugReport.CachePaths();   // bug-report v2: persistentDataPath is main-thread-only; the peer-log responder runs off-thread
            MPBugReport.OutboxOnLaunch();   // H-REPORTLOSS-1: log + enforce the undelivered-report outbox, one retry each at launch

            // Persistent host object for our UI component (Mono: custom
            // MonoBehaviours need no registration — AddComponent just works).
            _uiHost = new GameObject("BigAmbitionsMP");
            UnityEngine.Object.DontDestroyOnLoad(_uiHost);
            _uiHost.AddComponent<MPCanvasUI>();
            try { HeadlessHost.Init(); }   // HEADLESS-1: server mode (no-op unless -batchmode / -bamp-headless / Headless=true)
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Plugin] headless init: {ex.Message}"); }

            // BOOT-TERMINAL-2: the console comes first, then every step adds its line (never on the dedicated server)
            BootConsole.Open();
            BootConsole.SourceLine(context.ModRootPath ?? "");
            BootConsole.Line(BootConsole.TagOk, Loc.T("gps_boot_settings", "Settings loaded: player {0}", MPConfig.PlayerId));
            await BootConsole.Frame(now: true);

#if BAMP_DEV
            RigSaveRoot.Apply();   // RIG-SAVEROOT: dev rig only (BAMP_RIG_SAVEROOT) - must precede EnsureVersionCached below
#endif
            // PROTON-1: resolve and cache the game's save version folder BEFORE a single one
            // of our patches is applied.  Field bundle bamp-bug-20260907-115848 (Linux/Proton):
            // SaveGamePathHelper.CurrentVersionFolderPath() answered correctly at startup and
            // then threw NullReferenceException on EVERY call once our redirect patch was
            // attached -- for the game's own callers too.  With the cache empty, SpVersionFolder()
            // returns "" and every MP store path silently becomes RELATIVE.  Caching here means
            // that one known-good window is never missed.
            try
            {
                MPSaveManager.EnsureVersionCached();
                Plugin.Logger.LogInfo($"[MPSave] version folder cached before patching: '{MPSaveManager.CachedVersionFolderOrEmpty}'");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[MPSave] version folder caching before patching FAILED: {ex}");
            }
            MPSaveManager.PatchingStarted = true;

            // Apply Harmony patches per-class so a single bad class can't take
            // down the rest (PatchAll aborts on the first throw).
            //
            // Round-241 provenance probe (field 20260803-222126: 253/253 classes threw
            // TypeLoadException on a byte-correct game install with 34 mods — the shape of a
            // SHARED-HARMONY VERSION CONFLICT, but the bundle could not name the culprit).
            // One line settles it in the next such bundle: which 0Harmony actually loaded,
            // from whose folder, versus the version we compiled against.
            try
            {
                var loaded = typeof(Harmony).Assembly;
                string compiledAgainst = "?";
                foreach (var r in typeof(ModEntry).Assembly.GetReferencedAssemblies())
                    if (r.Name == "0Harmony") { compiledAgainst = r.Version?.ToString() ?? "?"; break; }
                CompiledHarmonyVersion = compiledAgainst;   // round-254: reused by the failure banner
                string loc = "";
                try { loc = loaded.Location; } catch { }
                var hv = loaded.GetName().Version;
                BootHarmonyVersion = hv == null ? "?" : $"{hv.Major}.{hv.Minor}.{hv.Build}";
                BootHarmonyOwnCopy = IsInOwnFolder(loc);
                try { BootHarmonyFolder = System.IO.Path.GetDirectoryName(loc) ?? ""; } catch { }
                Plugin.Logger.LogInfo($"[Plugin] Harmony provenance: loaded v{loaded.GetName().Version} from '{(string.IsNullOrEmpty(loc) ? "(in-memory)" : loc)}', compiled against v{compiledAgainst}.");
                // HARMONYVER-1 (field 20260919-081216): an OLDER shared Harmony is not cosmetic. We compile against
                // 2.4.x; any call the compiler resolved to a 2.4-only overload raises MissingMethodException when the
                // method containing it is JIT-compiled — inside a type initialiser that kills every later call into
                // that class (SharedShopVisibility lost BizMan and both building lists that way). Say it once, naming
                // the folder the loaded copy came from, so the next bundle names the mod to update or remove.
                // LOG ONLY — it never blocks anything.
                var vLoaded = loaded.GetName().Version;
                if (vLoaded != null && Version.TryParse(compiledAgainst, out var vCompiled) && vLoaded < vCompiled)
                    Plugin.Logger.LogWarning(
                        $"[Plugin] Harmony is OLDER than the copy this build was compiled against (loaded v{vLoaded}, compiled v{vCompiled}), " +
                        $"from '{(string.IsNullOrEmpty(loc) ? "(in-memory)" : loc)}' — another mod's copy won the load race. " +
                        "Anything we call that only exists in the newer Harmony would fail there; if whole panels or lists are dead, that folder's mod is the one to update or remove.");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Plugin] Harmony provenance probe: {ex.Message}"); }
            BootConsole.HarmonyLine();
            await BootConsole.Frame();
            // ── Round-254: the Harmony BOOTSTRAP itself can die before any patching ──
            // Field 2026-08-13 (Vitinhoooo): an OLDER mod (load order = workshop id
            // ascending, always beats our young id) shipped 0Harmony without
            // MonoMod.RuntimeDetour — Harmony's type initializer threw ONCE, stayed
            // poisoned, and our unwrapped `new Harmony(...)` rethrew it, killing
            // OnLoadAsync entirely: no mod, no button, no explanation. Now: catch,
            // CLASSIFY (incomplete package vs anything else), name the culprit's
            // folder, and return NORMALLY — the canvas (built above, Harmony-free)
            // survives purely to show the player which mod to remove (user ruling
            // 2026-08-13: identify the incompatible mod; the player decides which
            // of the two goes).
            int okClasses = 0, failClasses = 0, deadClasses = 0, totalPatched = 0;
            try
            {
            _harmony = new Harmony("com.bamp.bigambitionsmp");
            ArmWineDirectJumps();   // WINE-JUMPS-1: under Wine/Proton every redirect is a self-contained jump (no pointer cells)
            ArmSkipTinyPatches();   // TINY-DETOUR-2 (test switch): skip-tiny-patches.txt next to the DLL
            var patchTypes = typeof(ModEntry).Assembly.GetTypes().Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), true).Any()).ToList();
            int patchDone = 0;
            BootConsole.HooksBusy(0, patchTypes.Count);
            foreach (var t in patchTypes)
            {
                if (BootConsole.Live && ++patchDone % 10 == 0)   // BOOT-TERMINAL-2: count up live, a frame every ~50 ms
                {
                    BootConsole.HooksBusy(patchDone, patchTypes.Count);
                    await BootConsole.Frame();
                }
                try
                {
                    var before = _harmony.GetPatchedMethods().Count();
                    int tinyBefore; lock (_skippedTiny) tinyBefore = _skippedTiny.Count;
                    // Patch() returns the methods THIS class attached to — the per-class truth. The old
                    // global-target diff couldn't tell "target shared with an earlier class" (benign,
                    // the round-15 goose chase) from "bound NOTHING" (dead class = silent feature loss;
                    // 2026-07-09 audit: the slice-4 wallet guard shipped unapplied behind exactly this
                    // ambiguity, next to eleven unreviewable zero lines).
                    var bound = _harmony.CreateClassProcessor(t).Patch();
                    var added = _harmony.GetPatchedMethods().Count() - before;
                    totalPatched += added;
                    int boundCount = bound?.Count ?? 0;
                    if (boundCount == 0)
                    {
                        deadClasses++;
                        PatchIssues.Add($"{t.Name}: bound nothing");
                        Plugin.Logger.LogWarning($"[Plugin] Patch class {t.Name} BOUND NOTHING — dead class (empty TargetMethod(s)); whatever it guards is OFF.");
                    }
                    else
                    {
                        okClasses++;
                        int tinySkipped; lock (_skippedTiny) tinySkipped = _skippedTiny.Count - tinyBefore;
                        Plugin.Logger.LogInfo($"[Plugin] Patched {t.Name}: {boundCount} method(s)"
                            + (added == 0 ? " (targets shared with an earlier class)" : "")
                            + (tinySkipped > 0 ? $" - {tinySkipped} tiny target(s) NOT redirected (TINY-DETOUR-2 test, feature off)" : ""));
                    }
                }
                catch (Exception ex)
                {
                    // Walk to the ROOT cause (round-65): HarmonyException wraps the real
                    // TargetMethod failure — the outer message alone left the MIREL
                    // report undiagnosable (which of 34 mods broke the scan? unknowable).
                    var root = ex;
                    while (root.InnerException != null) root = root.InnerException;
                    string inner = ReferenceEquals(root, ex) ? "" : $" ← {root.GetType().Name}: {root.Message}";
                    failClasses++;
                    PatchIssues.Add($"{t.Name}: {ex.GetType().Name}: {ex.Message}{inner}");
                    Plugin.Logger.LogError($"[Plugin] Patch class {t.Name} FAILED: {ex.GetType().Name}: {ex.Message}{inner}");
                    // Round-229: the root Message alone can be nameless ("Failure has occurred
                    // while loading a type") — for the first few failures keep the FULL root
                    // (stack + TypeLoadException.TypeName when present) so a field bundle
                    // names what would not load. Capped: 253 identical stacks help nobody.
                    if (failClasses <= 3)
                    {
                        string? tln = (root as TypeLoadException)?.TypeName;
                        Plugin.Logger.LogError($"[Plugin] {t.Name} root detail{(string.IsNullOrEmpty(tln) ? "" : $" (type '{tln}')")}: {root}");
                    }
                }
            }
            PatchFailCount = failClasses;
            BootOkClasses = okClasses; BootFailClasses = failClasses; BootDeadClasses = deadClasses; BootPatchedMethods = totalPatched;
            Plugin.Logger.LogInfo($"[Plugin] Patch summary: {okClasses} class(es) OK, {failClasses} failed, {deadClasses} dead, {totalPatched} method(s) patched total.");
            LogTinyPatchTargets();
            lock (_skippedTiny) if (_skippedTiny.Count > 0) Plugin.Logger.LogWarning($"[Plugin] TINY-DETOUR-2: {_skippedTiny.Count} tiny redirect(s) skipped: {string.Join(", ", _skippedTiny)}");
            BootConsole.HooksLine();
            await BootConsole.Frame();
            DetourCheck.Snapshot();   // DETOUR-CHECK-1: before ModTranslations adds its own patches
            ModTranslations.Init();   // MOD-TRANSLATIONS-1/2: other mods in the player's language (tables shipped + from the repository)
            BootConsole.Line(BootConsole.TagOk, Loc.T("gps_boot_modtr", "Translations for other mods"));
            if (MpDisabledByPatchFailure)
            {
                Plugin.Logger.LogError($"[Plugin] {failClasses} patch class(es) FAILED (>= {PatchFailHardBlock}) — systemic; multiplayer entry is DISABLED this run (broken install, conflicting mod, or game update newer than the mod).");
                // Round-254 case B (the round-241 shape): Harmony STARTED but our patches
                // failed en masse — classic OUTDATED shared copy loaded first. Full
                // classification goes to the LOG; the banner just names the mod (254b).
                string src = DescribeLoadedHarmony();
                MPCanvasUI.FatalBootBanner = BuildConflictBanner();
                Plugin.Logger.LogError($"[Plugin] Harmony origin for the failure above: {src.Replace("\n", " | ")} (we compiled against v{CompiledHarmonyVersion}).");
            }
            else if (PatchesDegraded)
                Plugin.Logger.LogWarning($"[Plugin] {failClasses} patch class(es) FAILED (< {PatchFailHardBlock}) — multiplayer stays ENABLED; affected features may misbehave.");
            }
            catch (Exception bootEx)
            {
                // Round-254 case A: the shared Harmony never initialized at all.
                Exception root = bootEx;
                while (root.InnerException != null) root = root.InnerException;
                string src = DescribeLoadedHarmony();
                string what;
                if (root is System.IO.FileNotFoundException fnf)
                {
                    string missing = fnf.FileName ?? fnf.Message;
                    int comma = missing.IndexOf(',');                       // 'MonoMod.RuntimeDetour, Version=...' → short name
                    if (comma > 0) missing = missing.Substring(0, comma);
                    what = $"an INCOMPLETE copy of Harmony (the patching library all mods share): its companion file '{missing}' is missing";
                }
                else
                    what = $"a broken copy of Harmony (the patching library all mods share) — it failed to start ({root.GetType().Name}: {root.Message})";
                PatchFailCount = 9999;   // every existing "MP disabled" gate engages
                BootHarmonyDead = true;
                BootConsole.HooksLine();
                MPCanvasUI.FatalBootBanner = BuildConflictBanner();   // 254b: concise, mod-naming only
                Plugin.Logger.LogError($"[Plugin] HARMONY BOOTSTRAP FAILED (round-254): {what}. Origin: {src.Replace("\n", " | ")}. "
                    + $"Multiplayer is disabled this session; the on-screen banner names the culprit. Root: {root}");
            }

            // Field 20260830-170644: the exit notification rides the game's own completion
            // event now (fires after the street teleport, and on every real exit path) —
            // see MPPatches.ExitBuildingNotifier for the two defects the old prefix had.
            try { MPPatches.ExitBuildingNotifier.Install(); } catch (Exception ex) { Plugin.Logger.LogWarning($"[Plugin] exit notifier: {ex.Message}"); }

            Plugin.Logger.LogInfo($"{MyPluginInfo.DISPLAY_NAME} (BigAmbitionsMP) v{MyPluginInfo.PLUGIN_VERSION} ({MyPluginInfo.BuildTag}, build {MyPluginInfo.BuildId}) loaded. Canvas UI active.");
            BootConsole.FinishLine();
        }

        /// <summary>TINY-DETOUR-2 (test switch for the Proton crashes): with "skip-tiny-patches.txt" next to the mod's DLL,
        /// Harmony writes no redirect into a game method with <= 16 bytes of IL (its PatchFunctions.UpdateWrapper is skipped
        /// for those), so if the long jump of a tiny method overwrote the method behind it, the crashes stop. The features
        /// those patches carry are off while the switch is on. No file = nothing changes.</summary>
        private static readonly List<string> _skippedTiny = new List<string>();
        private static readonly HashSet<MethodBase> _skippedTinyMethods = new HashSet<MethodBase>();   // the same method skipped for every class that patches it counts once

        private void ArmSkipTinyPatches()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(typeof(ModEntry).Assembly.Location) ?? "";
                if (!System.IO.File.Exists(System.IO.Path.Combine(dir, "skip-tiny-patches.txt"))) return;
                var uw = AccessTools.Method("HarmonyLib.PatchFunctions:UpdateWrapper");
                if (uw == null) { Plugin.Logger.LogWarning("[Plugin] TINY-DETOUR-2: Harmony's UpdateWrapper not found - switch has no effect."); return; }
                _harmony!.Patch(uw, prefix: new HarmonyMethod(typeof(ModEntry), nameof(SkipTinyUpdateWrapper)));
                BootTinySwitch = true;
                Plugin.Logger.LogWarning("[Plugin] TINY-DETOUR-2: skip-tiny-patches.txt found - game methods with <= 16 bytes of IL are NOT redirected (test).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Plugin] TINY-DETOUR-2: {ex.Message}"); }
        }

        /// <summary>TINY-SKIP-FIX-1 (field ff7c7fd9 / ec5e61eb): the skipped wrapper used to answer null, Harmony's next step
        /// (HarmonySharedState.UpdatePatchInfo) threw ArgumentNullException on it and failed the whole patch class - 41
        /// such classes switched multiplayer off and showed the conflict banner. The original now stands in for the
        /// replacement: Harmony keeps its bookkeeping, and still no redirect is written. Constructors are patched as before.</summary>
        private static bool SkipTinyUpdateWrapper(MethodBase original, ref MethodInfo? __result)
        {
            try
            {
                if (original is not MethodInfo mi) return true;
                int il = mi.GetMethodBody()?.GetILAsByteArray()?.Length ?? -1;
                if (il < 0 || il > 16 || mi.DeclaringType?.Assembly == typeof(Harmony).Assembly) return true;
                __result = mi;
                lock (_skippedTiny) { _skippedTiny.Add($"{mi.DeclaringType?.FullName}.{mi.Name}={il}"); _skippedTinyMethods.Add(mi); }
                return false;
            }
            catch { return true; }
        }

        /// <summary>WINE-JUMPS-1 (field a6373798 + crash dump d16e2e4d, Linux/Proton): Harmony's detour engine (MonoMod.Core)
        /// writes a redirect as a direct jump when the replacement code is within 2 GB of the game method, else as a 6-byte
        /// jump through an 8-byte pointer cell it allocates next to the game's code (Rel32Ind64). Under Wine the replacement
        /// code lands far away, so nearly every redirect takes a cell - and those cells do not stay intact there: patched
        /// methods failed at their very first instruction (BizManBusiness.SetInitialTab: "Open in BizMan" showed the empty
        /// list; MyEmployeesMassActionsUI.UpdateMassActionDropdown; SaveGamePathHelper.CurrentVersionFolderPath, PROTON-1),
        /// and the dump shows the CPU executing at 0x40010000a, data read as a jump target. Under Wine the cells are
        /// refused, so MonoMod writes its self-contained 14-byte jump (Abs64) instead. Windows is not touched.</summary>
        internal static bool BootWineDirect;
        private static int _wineCellsRefused;
        private static PropertyInfo? _reqBase, _reqSize, _reqExec;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string moduleName);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Ansi, BestFitMapping = false)]
        private static extern IntPtr GetProcAddress(IntPtr module, string procName);

        internal static int WineCellsRefused => _wineCellsRefused;

        /// <summary>Wine (and so Proton) exports wine_get_version from its ntdll; Windows does not.</summary>
        internal static bool RunningOnWine()
        {
            try
            {
                if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsServer) return false;
                var ntdll = GetModuleHandleW("ntdll.dll");
                return ntdll != IntPtr.Zero && GetProcAddress(ntdll, "wine_get_version") != IntPtr.Zero;
            }
            catch { return false; }
        }

        private void ArmWineDirectJumps()
        {
            try
            {
                if (!RunningOnWine()) return;
                // the allocator of the very Harmony copy that writes our patches (another mod may have loaded its own)
                var alloc = typeof(Harmony).Assembly.GetType("MonoMod.Core.Platforms.Memory.PagedMemoryAllocator");
                var m = alloc == null ? null : AccessTools.Method(alloc, "TryAllocateInRange");
                if (m == null) { Plugin.Logger.LogWarning("[Plugin] WINE-JUMPS-1: running on Wine, but this Harmony has no MonoMod.Core allocator - redirects stay as Harmony writes them."); return; }
                _harmony!.Patch(m, prefix: new HarmonyMethod(typeof(ModEntry), nameof(RefuseWinePointerCell)));
                BootWineDirect = true;
                Plugin.Logger.LogInfo("[Plugin] WINE-JUMPS-1: running on Wine/Proton - game hooks are written as self-contained jumps (no pointer cells).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Plugin] WINE-JUMPS-1: {ex.Message}"); }
        }

        /// <summary>Prefix of PagedMemoryAllocator.TryAllocateInRange: a pointer cell (8 bytes, not executable - the only
        /// request x86_64Arch.ComputeDetourInfo makes) is refused; executable requests (alternate entry points) go through.</summary>
        private static bool RefuseWinePointerCell(object[] __args, ref bool __result)
        {
            try
            {
                object? req = __args != null && __args.Length > 0 ? __args[0] : null;
                if (req == null) return true;
                _reqBase ??= req.GetType().GetProperty("Base");
                object? b = _reqBase?.GetValue(req);
                if (b == null) return true;
                _reqSize ??= b.GetType().GetProperty("Size");
                _reqExec ??= b.GetType().GetProperty("Executable");
                if (_reqSize?.GetValue(b) is int size && size == IntPtr.Size && _reqExec?.GetValue(b) is bool exec && !exec)
                {
                    System.Threading.Interlocked.Increment(ref _wineCellsRefused);
                    __result = false;
                    return false;
                }
            }
            catch { }
            return true;
        }

        /// <summary>TINY-DETOUR-1 (diagnostic): every patched game method with a very small IL body. Harmony redirects a
        /// method by writing a jump over the start of its machine code; under Wine/Proton that jump is the long form
        /// (14 bytes), so a method whose compiled code is shorter could have the start of the NEXT method overwritten -
        /// matching the Proton crashes at the first instruction of unrelated methods (PROTON-1). Logged once at load.</summary>
        private static void LogTinyPatchTargets()
        {
            try
            {
                var tiny = new List<string>();
                int total = 0;
                foreach (var m in HarmonyLib.Harmony.GetAllPatchedMethods())
                {
                    total++;
                    int il;
                    try { il = m.GetMethodBody()?.GetILAsByteArray()?.Length ?? -1; } catch { il = -1; }
                    if (il >= 0 && il <= 16) tiny.Add($"{m.DeclaringType?.FullName}.{m.Name}={il}");
                }
                tiny.Sort(StringComparer.Ordinal);
                Plugin.Logger.LogInfo($"[Plugin] TINY-DETOUR-1: {tiny.Count} of {total} patched methods have <= 16 bytes of IL: {string.Join(", ", tiny)}");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Plugin] TINY-DETOUR-1: {ex.Message}"); }
        }

        /// <summary>QUIT-CRASH-UNPATCH-1: is the game's mod loader inside its application-quit unload? Its private
        /// ModLifecycleLoader.IsShuttingDown is set ONLY there (UnloadAllScopesAsync, reached only from Application.quitting)
        /// and cleared when it finishes. Unreadable (a renamed field) -> false, the old unpatching behaviour.</summary>
        private static bool LoaderShuttingDown()
        {
            try
            {
                var f = HarmonyLib.AccessTools.Field(typeof(global::BigAmbitions.ModsInternal.ModLifecycleLoader), "IsShuttingDown");
                if (f == null) { Plugin.Logger.LogWarning("[Plugin] unload: the mod loader's quitting state is not readable - unpatching as before."); return false; }
                return f.GetValue(null) is bool b && b;
            }
            catch (Exception ex) { try { Plugin.Logger.LogWarning($"[Plugin] unload: quitting state: {ex.Message}"); } catch { } return false; }
        }

        public override Task OnUnloadAsync()
        {
            try { MPPatches.ExitBuildingNotifier.Uninstall(); } catch { }
            try { MPBugReport.MarkCleanShutdown(); } catch { }
            // QUIT-CRASH-UNPATCH-1: on an APPLICATION QUIT the game's mod loader unloads every mod from its
            // Application.quitting handler (ModLifecycleLoader.OnApplicationQuitting -> UnloadAllScopesAsync, which sets its
            // own IsShuttingDown first), and UnpatchAll there crashed the quitting process natively. The process is
            // exiting, so the patches are left in place; a real mod unload (the mods panel) still unpatches.
            bool appQuitting = false;
            try { appQuitting = LoaderShuttingDown(); } catch { appQuitting = false; }
            if (appQuitting) { try { Plugin.Logger.LogInfo("[Plugin] unload on quit - patches left in place (the process is exiting)."); } catch { } }
            else try { _harmony?.UnpatchAll(_harmony.Id); } catch { }
            try { MPServer.Stop(); } catch { }
            try { MPClient.Disconnect(); } catch { }
            if (_uiHost != null) UnityEngine.Object.Destroy(_uiHost);
            return Task.CompletedTask;
        }
    }

    /// <summary>Static façade kept so the rest of the codebase compiles
    /// unchanged: Plugin.Logger.Log* everywhere routes to the official
    /// loader's IModLogger and Unity's console.</summary>
    public static class Plugin
    {
        public static ModLog Logger { get; internal set; } = new ModLog(null);
    }

    /// <summary>BepInEx-ManualLogSource-shaped wrapper over IModLogger.</summary>
    public class ModLog
    {
        private readonly IModLogger? _inner;
        public ModLog(IModLogger? inner) { _inner = inner; }

        public void LogInfo(object msg)
        {
            string m = msg?.ToString() ?? "";
            MPLog.Record("INFO", m);              // the ring (bug dumps) always keeps everything
            if (Quiet && IsChatty(m)) return;     // LOG-LEVEL-1
            var s = $"[BAMP] {m}";
            if (_inner != null) _inner.Info(s); else Debug.Log(s);
        }

        /// <summary>LOG-LEVEL-1 (1.0.16, user 2026-11-01): on a dedicated server with AMP's "Log detail" on Normal, the
        /// frequent status lines of the systems stay out of the console (one player's hour was 15,000 lines: change
        /// broadcasts, slow-frame notes, weather, traffic budgets, probes). Warnings and errors always pass, and the
        /// ring buffer behind the bug dumps keeps every line.</summary>
        internal static bool Quiet;
        private static readonly string[] ChattyPrefixes =
        {
            "[BusinessSync] Sent change", "[BusinessSync] Broadcast", "[Rhythm]", "[Weather]", "[TrafficSync] traffic budget",
            "[TrafficSync] snapshots on the", "[PROBE]", "[Patcher/Host] Client business applied", "[CarStack]", "[TrafficCensus]",
            "[ParkedSync] HOST", "[ParkedSync] instant resync", "[StaffRoster]", "[UI] Periodic market snapshot", "[PatchCost]",
            "[Server] Broadcast rivals stats", "[Server] AI stats via", "[CarHit]", "[LnlLink] paced mirror", "[GetLogoTex]",
            "[CustProbe]", "[DrainDiag]", "[Perf/", "[Patcher] Refresh", "[Patcher] No logo bytes", "[InteriorSync] owner cargo sync",
            "[Patcher] Interior applied", "[Patcher] enabled buyer purchasing", "[MPSave] Wrote manifest",
            // QUIET-2 (1.0.16.1, user 2026-11-01: "makes the server very slow" - a console of status lines): the periodic
            // diagnostics of a running world - traffic tallies (the ranking page shows them), the consistency audit, each
            // wallet booking, per-save file lines, stored self-reports and paperwork, the HQ / company-list fan-outs
            "[NetStats]", "[Audit]", "[EconProbe] wallet ledger", "[MPSave] Stored '", "[MPSave] Mirrored member file",
            "[Server] Stored self-stats", "[Paperwork] stored for", "[HQDiag]", "[CompanyLists] fanned out", "[StaffDiag",
            "[Rest] skip vote WAITING", "[MPSave] Persisted", "[Colours]", "[AmpPick]", "[Patcher] rival re-price suppressed",
            "[TMBlock]", "[SynthStaff]", "[LnlLink] express", "[TrafficGuard]", "[Server] GameTimeSync",
        };
        /// <summary>QUIET-2: the diagnostic probes that speak as WARNINGS - frame rhythm, the consistency audit, the car-stack
        /// probe - are no server owner's business either; Normal leaves them out like the status lines above (the ring keeps
        /// them for bug dumps, Detailed shows them). Every other warning and every error still passes.</summary>
        private static readonly string[] ChattyWarningPrefixes = { "[Rhythm]", "[Audit]", "[CarStack]", "[CarHit]", "[DrainDiag]" };
        private static bool IsChattyWarning(string m)
        {
            foreach (var p in ChattyWarningPrefixes) if (m.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsChatty(string m)
        {
            foreach (var p in ChattyPrefixes) if (m.StartsWith(p, StringComparison.Ordinal)) return true;
            if (m.StartsWith("[Patcher] ", StringComparison.Ordinal) && m.Contains(" restocked IN PLACE")) return true;
            return false;
        }
        public void LogWarning(object msg)
        {
            string m = msg?.ToString() ?? "";
            MPLog.Record("WARN", m);
            if (Quiet && IsChattyWarning(m)) return;   // QUIET-2
            var s = $"[BAMP] {m}";  if (_inner != null) _inner.Warn(s);  else Debug.LogWarning(s);
        }
        public void LogError(object msg)   { MPLog.Record("ERR ", msg?.ToString() ?? ""); var s = $"[BAMP] {msg}";  if (_inner != null) _inner.Error(s); else Debug.LogError(s); }
        public void LogDebug(object msg)   => LogInfo(msg);
    }
}
