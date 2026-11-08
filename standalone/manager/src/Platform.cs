using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GpsManager;

/// <summary>How the game runs on this system.
///   Windows: Big Ambitions.exe directly.
///   Linux:   Proton GE "runinprefix" (as AMP's template: no Steam, the prefix in server/prefix), under xvfb-run when there
///            is no display - Unity makes a hidden window even with -batchmode -nographics, and without a display it exits
///            ("Failed to create batch mode window").
/// Both: -bamp-config / -bamp-headless-dir as under AMP, -bamp-nosteam and -bamp-mods-dir (the preloader: no Steam client,
/// the server's own mod folder server/mods), -disable-force-steam (the game never restarts itself through Steam), and
/// "-logFile -": Unity's log goes to stdout - the console and server/logs/console.log are the whole game log. With a
/// log FILE Unity sends stdout into that file and the server's console lines never reach the manager (tested
/// 2026-11-01); without -logFile, Windows writes a Player.log next to the one of a Big Ambitions played on that account.</summary>
internal static class Platform
{
    internal static ProcessStartInfo GameStart()
    {
        ProcessStartInfo psi;
        if (OperatingSystem.IsWindows())
        {
            psi = new ProcessStartInfo(Paths.GameExe);
        }
        else
        {
            bool xvfb = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) && Which("xvfb-run") != null;
            psi = new ProcessStartInfo(xvfb ? "xvfb-run" : Paths.ProtonScript);
            if (xvfb)
            {
                psi.ArgumentList.Add("-a");
                psi.ArgumentList.Add(Paths.ProtonScript);
            }
            psi.ArgumentList.Add("runinprefix");
            psi.ArgumentList.Add(Paths.GameExe);
            ProtonEnv(psi.Environment);
        }
        foreach (var a in new[]
                 {
                     "-batchmode", "-nographics", "-disable-force-steam", "-bamp-nosteam",
                     "-bamp-mods-dir", Paths.ForGame(Paths.Mods),
                     "-bamp-config", Paths.ForGame(Paths.ServerJson),
                     "-bamp-headless-dir", Paths.ForGame(Paths.Headless),
                     "-logFile", "-",
                 })
            psi.ArgumentList.Add(a);
        psi.WorkingDirectory = Paths.Game;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardInput = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.StandardOutputEncoding = Encoding.UTF8;
        psi.StandardErrorEncoding = Encoding.UTF8;
        psi.Environment["SteamAppId"] = "1331550";   // the game then never asks Steam to start it again (as under AMP)
        psi.Environment["SteamGameId"] = "1331550";
        return psi;
    }

    /// <summary>Linux: the environment Proton needs (the same as AMP's template sets).</summary>
    internal static void ProtonEnv(IDictionary<string, string?> env)
    {
        env["STEAM_COMPAT_DATA_PATH"] = Paths.Prefix;
        env["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = Path.Combine(Paths.Home, ".steam", "steam");
        env["HOME"] = Paths.Home;
        env["WINEDLLOVERRIDES"] = "winhttp=n,b";   // Doorstop's winhttp.dll (the server-only preloader) before Wine's own
        env["WINEDEBUG"] = "fixme-all";
        env["SteamAppId"] = "1331550";
        env["SteamGameId"] = "1331550";
        string lib = Path.Combine(Paths.SteamCmd, "linux64");
        env.TryGetValue("LD_LIBRARY_PATH", out var old);
        env["LD_LIBRARY_PATH"] = string.IsNullOrEmpty(old) ? lib : lib + ":" + old;
    }

    /// <summary>The game processes of THIS server that run without the manager (a manager that was killed): their command
    /// line names this server's headless folder - never another server's game on the same machine.</summary>
    internal static List<int> FindGameProcesses()
    {
        var found = new List<int>();
        try
        {
            if (OperatingSystem.IsWindows())
            {
                foreach (var p in Process.GetProcessesByName("Big Ambitions"))
                {
                    try
                    {
                        if (string.Equals(p.MainModule?.FileName, Paths.GameExe, StringComparison.OrdinalIgnoreCase)) found.Add(p.Id);
                    }
                    catch { }
                }
            }
            else
            {
                string mine = Paths.Headless, mineWin = mine.Replace('/', '\\');
                foreach (var dir in Directory.EnumerateDirectories("/proc"))
                {
                    if (!int.TryParse(Path.GetFileName(dir), out int pid) || pid == Environment.ProcessId) continue;
                    string cmd;
                    try { cmd = File.ReadAllText(Path.Combine(dir, "cmdline")).Replace('\0', ' '); } catch { continue; }
                    if (cmd.Contains("Big Ambitions.exe", StringComparison.Ordinal) && (cmd.Contains(mine, StringComparison.Ordinal) || cmd.Contains(mineWin, StringComparison.Ordinal)))
                        found.Add(pid);
                }
            }
        }
        catch { }
        return found;
    }

    internal static void Kill(IEnumerable<int> pids, bool hard)
    {
        foreach (int pid in pids)
        {
            try
            {
                if (OperatingSystem.IsWindows()) Process.GetProcessById(pid).Kill(entireProcessTree: true);
                else kill(pid, hard ? 9 : 15);
            }
            catch { }
        }
    }

    /// <summary>Linux: what Wine may leave behind after the game (wineserver and its helpers of this prefix).</summary>
    internal static void AfterGameExit()
    {
        if (OperatingSystem.IsWindows()) return;
        try
        {
            string ws = Path.Combine(Paths.Proton, "files", "bin", "wineserver");
            if (!File.Exists(ws)) return;
            var psi = new ProcessStartInfo(ws) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            psi.ArgumentList.Add("-k");
            psi.Environment["WINEPREFIX"] = Path.Combine(Paths.Prefix, "pfx");
            using var p = Process.Start(psi);
            p?.WaitForExit(10_000);
        }
        catch { }
    }

    internal static string? Which(string name)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string f = Path.Combine(dir, name);
            if (File.Exists(f)) return f;
        }
        return null;
    }

    internal static string Describe()
    {
        string os = OperatingSystem.IsWindows() ? "Windows" : Paths.InDocker ? "Linux (Docker)" : "Linux";
        return $"{os} {RuntimeInformation.OSArchitecture}";
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int sig);
}
