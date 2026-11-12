using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>DETOUR-CHECK-1 (diagnostic, Linux/Proton crashes of 2026-10-29): Harmony redirects a patched game method by
    /// writing a jump over the start of its machine code. Players on Proton saw crashes at the FIRST instruction of methods
    /// and jumps to nonsense addresses (0x1500100082, 0x1d00100072) - as if those jumps were overwritten after patching,
    /// e.g. by the code of a method compiled later into the bytes behind a tiny patched method.
    ///
    /// At load (right after patching) this records the first 16 bytes at the entry of every patched method; at world
    /// ready and then once a minute it compares them and logs, once per method, every entry that changed - naming the
    /// method and both byte strings. Reads memory only; changes nothing.
    ///
    /// WINE-JUMPS-1 (field a6373798: "0 of 766 entries changed", yet SetInitialTab failed at its first instruction): an entry
    /// can also be a 6-byte jump through a pointer cell elsewhere (FF 25 + offset) - then the CELL is what breaks. The
    /// snapshot now names the kinds (direct, inline 14-byte, through a cell) and the checks watch every cell too: its page
    /// must stay readable and its 8 bytes unchanged.</summary>
    internal static class DetourCheck
    {
        private const int Bytes = 16;
        private sealed class Entry { public string Name = ""; public IntPtr At; public byte[] Was = Array.Empty<byte>(); public bool Reported; public IntPtr Cell; public long CellWas; public bool CellReported; }
        private static readonly List<Entry> _entries = new List<Entry>();
        private static float _nextAt;
        private static int _checks;

        /// <summary>Right after patching, main thread.</summary>
        internal static void Snapshot()
        {
            try
            {
                foreach (var m in HarmonyLib.Harmony.GetAllPatchedMethods())
                {
                    try
                    {
                        if (m.IsAbstract || m.ContainsGenericParameters) continue;
                        IntPtr at = m.MethodHandle.GetFunctionPointer();
                        if (at == IntPtr.Zero) continue;
                        var e = new Entry { Name = $"{m.DeclaringType?.FullName}.{m.Name}", At = at, Was = Read(at) };
                        e.Cell = CellOf(e);
                        if (e.Cell != IntPtr.Zero && Readable(e.Cell)) e.CellWas = Marshal.ReadInt64(e.Cell);
                        _entries.Add(e);
                    }
                    catch { }
                }
                int direct = 0, inline = 0, cells = 0, other = 0;
                foreach (var e in _entries)
                {
                    if (e.Was[0] == 0xE9) direct++;
                    else if (e.Cell != IntPtr.Zero) cells++;
                    else if (e.Was[0] == 0xFF && e.Was[1] == 0x25) inline++;
                    else other++;
                }
                Plugin.Logger.LogInfo($"[DetourCheck] DETOUR-CHECK-1: watching the entry bytes of {_entries.Count} patched method(s) - jumps: {direct} direct, "
                    + $"{inline} inline (14 bytes), {cells} through a pointer cell, {other} other{(ModEntry.BootWineDirect ? $"; Wine: {ModEntry.WineCellsRefused} pointer cell(s) refused (WINE-JUMPS-1)" : "")}.");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[DetourCheck] snapshot: {ex.Message}"); }
        }

        /// <summary>Main thread, every frame (MPCanvasUI.Update): a full compare at most once a minute; Now() forces one.</summary>
        internal static void Tick()
        {
            if (_entries.Count == 0 || Time.unscaledTime < _nextAt) return;
            Now("periodic");
        }

        internal static void Now(string why)
        {
            _nextAt = Time.unscaledTime + 60f;
            if (_entries.Count == 0) return;
            _checks++;
            int changed = 0, cellBad = 0;
            foreach (var e in _entries)
            {
                byte[] now;
                try { now = Read(e.At); } catch { continue; }
                if (e.Cell != IntPtr.Zero && !e.CellReported)
                {
                    // WINE-JUMPS-1: the cell the entry jumps through must stay readable and unchanged
                    bool ok = Readable(e.Cell);
                    long cell = 0;
                    if (ok) { try { cell = Marshal.ReadInt64(e.Cell); } catch { ok = false; } }
                    if (!ok || cell != e.CellWas)
                    {
                        e.CellReported = true;
                        cellBad++;
                        Plugin.Logger.LogError($"[DetourCheck] WINE-JUMPS-1: the jump cell of {e.Name} at 0x{e.Cell.ToInt64():x} "
                            + (ok ? $"CHANGED ({why}): was 0x{e.CellWas:x}, now 0x{cell:x}" : $"is NOT READABLE any more ({why})") + " - calling it fails at its first instruction.");
                    }
                }
                if (Same(now, e.Was)) continue;
                changed++;
                if (e.Reported) continue;
                e.Reported = true;
                Plugin.Logger.LogError($"[DetourCheck] DETOUR-CHECK-1: the entry of {e.Name} at 0x{e.At.ToInt64():x} CHANGED after patching ({why}): was {Hex(e.Was)}, now {Hex(now)}.");
            }
            if (_checks == 1 || changed > 0 || cellBad > 0)
                Plugin.Logger.LogInfo($"[DetourCheck] check #{_checks} ({why}): {changed} of {_entries.Count} patched entries changed, {cellBad} jump cell(s) newly broken.");
        }

        /// <summary>The pointer cell a "jmp [rip+offset]" entry (FF 25, offset not 0) jumps through; zero for any other entry.</summary>
        private static IntPtr CellOf(Entry e)
        {
            if (e.Was.Length < 6 || e.Was[0] != 0xFF || e.Was[1] != 0x25) return IntPtr.Zero;
            int disp = BitConverter.ToInt32(e.Was, 2);
            return disp == 0 ? IntPtr.Zero : new IntPtr(e.At.ToInt64() + 6 + disp);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryInfo
        {
            public IntPtr BaseAddress, AllocationBase;
            public uint AllocationProtect;
            public ushort PartitionId;
            public UIntPtr RegionSize;
            public uint State, Protect, Type;
        }

        [DllImport("kernel32.dll")]
        private static extern UIntPtr VirtualQuery(IntPtr address, out MemoryInfo info, UIntPtr length);

        /// <summary>Committed, readable, not a guard page (VirtualQuery; Windows and Wine only).</summary>
        private static bool Readable(IntPtr at)
        {
            try
            {
                if (VirtualQuery(at, out var mi, (UIntPtr)Marshal.SizeOf<MemoryInfo>()) == UIntPtr.Zero) return false;
                const uint Commit = 0x1000, ReadableMask = 0x02 | 0x04 | 0x08 | 0x20 | 0x40 | 0x80, Guard = 0x100;
                return mi.State == Commit && (mi.Protect & ReadableMask) != 0 && (mi.Protect & Guard) == 0;
            }
            catch { return false; }
        }

        private static byte[] Read(IntPtr at)
        {
            var b = new byte[Bytes];
            Marshal.Copy(at, b, 0, Bytes);
            return b;
        }

        private static bool Same(byte[] a, byte[] b)
        {
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static string Hex(byte[] b) => BitConverter.ToString(b).Replace("-", "");
    }
}
