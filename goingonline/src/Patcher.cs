using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace GoingOnline.Standalone
{
    /// <summary>GO-PATCHES-1 (user 2026-11-08: "Mit Harmony"): the stand-alone mod puts in the patches of core/GamePatches.cs -
    /// the provider in BizMan's list and pages, the wholesalers' delivery contracts, the furniture store's phone order, the
    /// recruitment dialog. Going Public Server patches the same classes itself, so this runs only while it is not loaded
    /// (the Runner asks first), once, when a game is loaded (all mods have loaded by then).
    ///
    /// Harmony comes in the mod's Dependencies folder. The game loads one copy per name for all mods - the first mod's
    /// (Workshop id ascending) - so another mod's older or broken copy can be the one in use. Then the classes that fail are
    /// named in the log and Going Online goes on without them: the app has everything, only the BizMan pages stay the
    /// game's. Under Wine/Proton the redirects are written as self-contained jumps (WINE-JUMPS-1, as Going Public Server
    /// does): the pointer cells Harmony's detour engine uses otherwise do not stay intact there.</summary>
    internal static class Patcher
    {
        static bool _armed;

        /// <summary>From the Runner, every frame while Going Public Server is not loaded.</summary>
        public static void Tick()
        {
            if (_armed) return;
            try { if (SaveGameManager.Current == null) return; } catch { return; }
            _armed = true;
            try { Arm(); }
            catch (Exception ex) { Go.Warn("Harmony is missing or broken (" + (ex.InnerException ?? ex).Message + ") - Going Online runs without its BizMan pages; the app has everything (GO-PATCHES-1)."); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]   // a missing Harmony fails at this call, inside Tick's catch
        static void Arm()
        {
            var h = new Harmony("goingonline.standalone");
            WineDirectJumps(h);
            Type[] types;
            try { types = typeof(GamePatches).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
            int ok = 0;
            var failed = new List<string>();
            foreach (var t in types)
            {
                if (t == null || t.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0) continue;
                try { h.CreateClassProcessor(t).Patch(); ok++; }
                catch (Exception ex) { failed.Add(t.Name + ": " + (ex.InnerException ?? ex).Message); }
            }
            GamePatches.Active = ok > 0;
            var hv = typeof(Harmony).Assembly.GetName().Version;
            string from = "";
            try { from = System.IO.Path.GetDirectoryName(typeof(Harmony).Assembly.Location) ?? ""; } catch { }
            bool own = from.Length > 0 && Entry.Root.Length > 0 && from.Replace('\\', '/').StartsWith(Entry.Root.Replace('\\', '/').TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
            Go.Log($"Harmony v{hv} ({(own ? "this mod's copy" : "another mod's copy")}): {ok} patch class(es) in, {failed.Count} failed - the provider in BizMan, deliveries and recruitment (GO-PATCHES-1).");
            foreach (var f in failed) Go.Warn("patch " + f + " (GO-PATCHES-1).");
        }

        // ── WINE-JUMPS-1, as Going Public Server's Plugin.cs does it ─────────────────────────────────────────────────

        static int _cellsRefused;
        static PropertyInfo? _reqBase, _reqSize, _reqExec;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern IntPtr GetModuleHandleW(string moduleName);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Ansi, BestFitMapping = false)]
        static extern IntPtr GetProcAddress(IntPtr module, string procName);

        /// <summary>Wine (and so Proton) exports wine_get_version from its ntdll; Windows does not.</summary>
        static bool OnWine()
        {
            try
            {
                if (Application.platform != RuntimePlatform.WindowsPlayer) return false;
                var ntdll = GetModuleHandleW("ntdll.dll");
                return ntdll != IntPtr.Zero && GetProcAddress(ntdll, "wine_get_version") != IntPtr.Zero;
            }
            catch { return false; }
        }

        /// <summary>Under Wine the 8-byte pointer cells Harmony's detour engine (MonoMod.Core) would place near the game's code
        /// are refused, so it writes its self-contained 14-byte jump instead (field reports of Going Public Server: patched
        /// methods failed at their first instruction - "Open in BizMan" showed an empty list). Windows is not touched.</summary>
        static void WineDirectJumps(Harmony h)
        {
            try
            {
                if (!OnWine()) return;
                var alloc = typeof(Harmony).Assembly.GetType("MonoMod.Core.Platforms.Memory.PagedMemoryAllocator");
                var m = alloc == null ? null : AccessTools.Method(alloc, "TryAllocateInRange");
                if (m == null) { Go.Warn("Wine/Proton, but this Harmony has no MonoMod.Core allocator - its redirects stay as it writes them (WINE-JUMPS-1)."); return; }
                h.Patch(m, prefix: new HarmonyMethod(typeof(Patcher), nameof(RefuseCell)));
                Go.Log("Wine/Proton: the game hooks are written as self-contained jumps (WINE-JUMPS-1).");
            }
            catch (Exception ex) { Go.Warn("WINE-JUMPS-1: " + ex.Message); }
        }

        /// <summary>Prefix of PagedMemoryAllocator.TryAllocateInRange: a pointer cell (8 bytes, not executable) is refused;
        /// executable requests go through.</summary>
        static bool RefuseCell(object[] __args, ref bool __result)
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
                    System.Threading.Interlocked.Increment(ref _cellsRefused);
                    __result = false;
                    return false;
                }
            }
            catch { }
            return true;
        }
    }
}
