using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Doorstop
{
    /// <summary>
    /// Server-only preloader, started by Unity Doorstop (winhttp.dll + doorstop_config.ini next to the game exe)
    /// before any game code runs.
    ///
    /// Why: the game's mod loader (BigAmbitions.HGModLoader) only discovers mods - ModsLocal included - after
    /// SteamAPI.OnSteamInit fires. Under AMP there is no Steam client, SteamClient.Init fails, the event never
    /// fires, and Going Public (headless mode, telemetry) never loads. This patches
    /// HGModLoader.TryInitializeModsForPlaySession: when Steam is not running it starts mod initialization
    /// directly, exactly as OnSteamInit would have. With Steam running the original code runs unchanged.
    ///
    /// Only active when the game was started with -bamp-config (the AMP / run-headless.sh server start).
    /// Players never install this, so it does not count as a mod the server requires.
    ///
    /// STANDALONE-1 (1.0.16, the Windows/Linux server without AMP): two more switches, given by the server manager.
    ///   -bamp-nosteam          the game never connects to a Steam client: SteamAPI.InitSteam (HGPlugins) is skipped,
    ///                          the same state as a server without Steam. A Steam running on that Windows PC stays the
    ///                          player's own - no "already running", no Workshop mods of that account on the server.
    ///   -bamp-mods-dir &lt;dir&gt;   the game's local mod folder (ModDiscoveryRegistry.ModsLocalPath, normally
    ///                          LocalLow\Hovgaard Games\Big Ambitions\ModsLocal) is this folder: the server's mods never
    ///                          mix with the mods of a Big Ambitions played on the same Windows account.
    /// </summary>
    public static class Entrypoint
    {
        private const string Tag = "[BAMP-Preloader] ";
        private static string _dir;
        private static string _logPath;
        private static bool _patched, _steamPatched, _modsPatched;
        private static bool _bypassLogged;
        private static bool _noSteam;
        private static string _modsDir;

        public static void Start()
        {
            try
            {
                _dir = Path.GetDirectoryName(typeof(Entrypoint).Assembly.Location);
                _logPath = Path.Combine(_dir, "bamp-preloader.log");
                try { File.WriteAllText(_logPath, ""); } catch { }
                if (!Environment.GetCommandLineArgs().Contains("-bamp-config"))
                {
                    Log("no -bamp-config on the command line - not a server start, doing nothing.");
                    return;
                }
                var args = Environment.GetCommandLineArgs();
                _noSteam = args.Contains("-bamp-nosteam");
                int m = Array.IndexOf(args, "-bamp-mods-dir");
                if (m >= 0 && m + 1 < args.Length && args[m + 1].Trim().Length > 0)
                    _modsDir = args[m + 1].Trim();
                AppDomain.CurrentDomain.AssemblyResolve += ResolveFromOwnFolder;
                AppDomain.CurrentDomain.AssemblyLoad += (_, e) => TryPatch(e.LoadedAssembly);
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    TryPatch(asm);
                if (!_patched)
                    Log("waiting for BigAmbitions.dll ...");
            }
            catch (Exception e)
            {
                Log("start failed: " + e);
            }
        }

        /// <summary>0Harmony.dll sits next to this assembly, not in the game's Managed folder.</summary>
        private static Assembly ResolveFromOwnFolder(object sender, ResolveEventArgs args)
        {
            try
            {
                var file = Path.Combine(_dir, new AssemblyName(args.Name).Name + ".dll");
                return File.Exists(file) ? Assembly.LoadFrom(file) : null;
            }
            catch
            {
                return null;
            }
        }

        private static void TryPatch(Assembly asm)
        {
            string name = asm.GetName().Name;
            if (name == "BigAmbitions" && !_patched)
            {
                _patched = true;
                try
                {
                    ApplyPatch(asm);
                }
                catch (Exception e)
                {
                    Log("patch failed - mods will only load with Steam: " + e);
                }
            }
            else if (name == "HGPlugins" && _noSteam && !_steamPatched)
            {
                _steamPatched = true;
                try { PatchNoSteam(asm); }
                catch (Exception e) { Log("-bamp-nosteam: patch failed - the game may connect to a running Steam: " + e); }
            }
            else if (name == "BigAmbitions.ModsInternal" && _modsDir != null && !_modsPatched)
            {
                _modsPatched = true;
                try { PatchModsDir(asm); }
                catch (Exception e) { Log("-bamp-mods-dir: patch failed - mods load from the normal ModsLocal folder: " + e); }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PatchNoSteam(Assembly asm)
        {
            var api = asm.GetType("SteamAPI", true);
            var init = api.GetMethod("InitSteam", BindingFlags.Instance | BindingFlags.NonPublic)
                       ?? throw new MissingMethodException(api.FullName, "InitSteam");
            var prefix = typeof(Entrypoint).GetMethod(nameof(SkipSteam), BindingFlags.Static | BindingFlags.NonPublic);
            new HarmonyLib.Harmony("bamp.steamless").Patch(init, prefix: new HarmonyLib.HarmonyMethod(prefix));
            Log("-bamp-nosteam: the game does not connect to a Steam client.");
        }

        private static bool SkipSteam() => false;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PatchModsDir(Assembly asm)
        {
            var reg = asm.GetType("BigAmbitions.ModsInternal.ModDiscoveryRegistry", true);
            var getter = reg.GetProperty("ModsLocalPath", BindingFlags.Static | BindingFlags.Public)?.GetGetMethod()
                         ?? throw new MissingMethodException(reg.FullName, "ModsLocalPath");
            var postfix = typeof(Entrypoint).GetMethod(nameof(ModsDirPostfix), BindingFlags.Static | BindingFlags.NonPublic);
            new HarmonyLib.Harmony("bamp.steamless").Patch(getter, postfix: new HarmonyLib.HarmonyMethod(postfix));
            Log("-bamp-mods-dir: local mods come from " + _modsDir + " (now reads " + getter.Invoke(null, null) + ").");
        }

        private static void ModsDirPostfix(ref string __result)
        {
            __result = _modsDir;
        }

        /// <summary>Kept out of TryPatch so Harmony types are only touched once the resolver is in place.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ApplyPatch(Assembly asm)
        {
            var loader = asm.GetType("BigAmbitions.HGModLoader", true);
            var target = loader.GetMethod("TryInitializeModsForPlaySession", BindingFlags.Instance | BindingFlags.NonPublic)
                         ?? throw new MissingMethodException(loader.FullName, "TryInitializeModsForPlaySession");
            var prefix = typeof(Entrypoint).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic);
            new HarmonyLib.Harmony("bamp.steamless").Patch(target, prefix: new HarmonyLib.HarmonyMethod(prefix));
            Log("HGModLoader patched - mods load without Steam when Steam is not running.");
        }

        private static bool Prefix(object __instance)
        {
            if (SteamRunning())
                return true;
            try
            {
                if (!Application_isPlaying())
                    return true;
                if (!_bypassLogged)
                {
                    _bypassLogged = true;
                    Log("Steam is not running - starting mod initialization without it.");
                }
                __instance.GetType()
                    .GetMethod("OnSteamInitialized", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(__instance, null);
                return false;
            }
            catch (Exception e)
            {
                Log("bypass failed, falling back to the original code: " + e);
                return true;
            }
        }

        private static bool SteamRunning()
        {
            try
            {
                var steamApi = Type.GetType("SteamAPI, HGPlugins");
                var prop = steamApi?.GetProperty("isRunningAndValid", BindingFlags.Static | BindingFlags.Public);
                return prop != null && (bool)prop.GetValue(null, null);
            }
            catch
            {
                return false;
            }
        }

        private static bool Application_isPlaying()
        {
            var app = Type.GetType("UnityEngine.Application, UnityEngine.CoreModule");
            var prop = app?.GetProperty("isPlaying", BindingFlags.Static | BindingFlags.Public);
            return prop == null || (bool)prop.GetValue(null, null);
        }

        private static void Log(string message)
        {
            var line = Tag + message;
            try { Console.WriteLine(line); } catch { }
            try { File.AppendAllText(_logPath, DateTime.Now.ToString("HH:mm:ss ") + line + Environment.NewLine); } catch { }
        }
    }
}
