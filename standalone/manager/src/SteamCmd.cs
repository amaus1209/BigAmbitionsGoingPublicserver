using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace GpsManager;

/// <summary>The game through Valve's SteamCMD: Big Ambitions (app 1331550) as the Windows version, into server/1331550.
/// The game has to be owned, so SteamCMD logs in with the owner's Steam account - name and password come from the web
/// page and go straight to SteamCMD (never into a file or a log); SteamCMD remembers the login itself, later updates
/// need only the name. Steam Guard: a code from the mail or the app goes in as the third login value, or the login
/// is confirmed in the Steam app.</summary>
internal static partial class SteamCmd
{
    internal static string Exe => OperatingSystem.IsWindows() ? Path.Combine(Paths.SteamCmd, "steamcmd.exe") : Path.Combine(Paths.SteamCmd, "steamcmd.sh");
    internal static bool Installed => File.Exists(Exe);

    [GeneratedRegex(@"progress: ([\d.]+) \((\d+) / (\d+)\)")]
    private static partial Regex ProgressRx();

    internal static async Task EnsureInstalled(Job job, CancellationToken ct)
    {
        if (Installed) return;
        Directory.CreateDirectory(Paths.SteamCmd);
        job.Message = Lang.T("SteamCMD wird heruntergeladen ...", "Downloading SteamCMD ...");
        job.Line(job.Message);
        string url = OperatingSystem.IsWindows()
            ? "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip"
            : "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz";
        string file = Path.Combine(Paths.SteamCmd, Path.GetFileName(url));
        await Net.Download(url, file, job, ct);
        if (OperatingSystem.IsWindows()) ZipFile.ExtractToDirectory(file, Paths.SteamCmd, true);
        else if (await Proc.Run("tar", new[] { "-xzf", file, "-C", Paths.SteamCmd }, job, ct) != 0)
            throw new InvalidOperationException("tar: SteamCMD could not be unpacked");
        File.Delete(file);
    }

    /// <summary>Installs or updates the game. The job ends "done", "needs-code", "needs-password" or "failed".</summary>
    internal static async Task InstallGame(Job job, string user, string? password, string? code, CancellationToken ct)
    {
        user = user.Trim();
        if (user.Length == 0) throw new InvalidOperationException(Lang.T("Steam-Benutzername fehlt.", "Steam user name missing."));
        await EnsureInstalled(job, ct);
        bool wasRunning = await Jobs.StopGameFor(job);
        Directory.CreateDirectory(Paths.Game);
        if (!OperatingSystem.IsWindows()) ProtonGE.EnsureHome();

        var args = new List<string>
        {
            "+@ShutdownOnFailedCommand", "1", "+@NoPromptForPassword", "1", "+@sSteamCmdForcePlatformType", "windows",
            "+force_install_dir", Paths.Game, "+login", user,
        };
        if (!string.IsNullOrEmpty(password))
        {
            args.Add(password);
            if (!string.IsNullOrWhiteSpace(code)) args.Add(code.Trim());
        }
        args.AddRange(new[] { "+app_update", "1331550", "validate", "+quit" });

        job.Message = Lang.T("SteamCMD meldet sich an und lädt das Spiel (beim ersten Mal einige GB) ...",
                             "SteamCMD logs in and downloads the game (several GB the first time) ...");
        job.Line(job.Message);
        string result = await Run(job, args, password, ct);
        switch (result)
        {
            case "ok":
                ManagerConfig.Current.SteamUser = user;
                ManagerConfig.Save();
                job.State = "done";
                job.Message = Lang.T("Spiel installiert bzw. aktuell.", "Game installed / up to date.");
                break;
            case "code":
                job.State = "needs-code";
                job.Message = Lang.T("Steam Guard: den Code aus der Mail oder der Steam-App eingeben (oder die Anmeldung in der App bestätigen) und noch einmal starten.",
                                     "Steam Guard: enter the code from the mail or the Steam app (or confirm the login in the app) and start again.");
                break;
            case "password":
                job.State = "needs-password";
                job.Message = Lang.T("SteamCMD braucht das Passwort (die gespeicherte Anmeldung fehlt oder ist abgelaufen).",
                                     "SteamCMD needs the password (its saved login is missing or expired).");
                break;
            case "badpass":
                job.State = "needs-password";
                job.Message = Lang.T("Benutzername oder Passwort falsch.", "Wrong user name or password.");
                break;
            case "badcode":
                job.State = "needs-code";
                job.Message = Lang.T("Der Steam-Guard-Code stimmt nicht (oder ist abgelaufen).", "The Steam Guard code is wrong (or expired).");
                break;
            case "notowned":
                job.State = "failed";
                job.Message = Lang.T("Dieses Steam-Konto besitzt Big Ambitions nicht.", "This Steam account does not own Big Ambitions.");
                break;
            case "ratelimit":
                job.State = "failed";
                job.Message = Lang.T("Steam: zu viele Anmeldeversuche - etwas warten und dann erneut versuchen.", "Steam: too many login attempts - wait a while and try again.");
                break;
            default:
                job.State = "failed";
                job.Message = Lang.T("SteamCMD ist fehlgeschlagen: ", "SteamCMD failed: ") + result;
                break;
        }
        job.Line(job.Message);
        if (job.State == "done" && (wasRunning || (ManagerConfig.Current.AutoStart && !GameServer.Alive && (OperatingSystem.IsWindows() || ProtonGE.Installed))))
        {
            string why = GameServer.Start(Lang.T("nach der Installation", "after the installation"));
            if (why.Length > 0) job.Line(why);
        }
    }

    /// <summary>"ok", "code", "password", "badpass", "badcode", "notowned", "ratelimit", or the last error line.</summary>
    private static async Task<string> Run(Job job, List<string> args, string? password, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(Exe)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, WorkingDirectory = Paths.SteamCmd,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (!OperatingSystem.IsWindows()) psi.Environment["HOME"] = Paths.Home;   // ~/.steam (the steamclient.so links Proton uses) inside server/home
        using var p = new Process { StartInfo = psi };
        string result = "";
        string lastError = "";
        bool success = false;
        p.Start();
        string Mask(string s) => string.IsNullOrEmpty(password) ? s : s.Replace(password, "********");
        var gate = new object();
        void Seen(string raw)
        {
            lock (gate) SeenLocked(raw);
        }
        void SeenLocked(string raw)
        {
            string s = Mask(raw).Trim();
            if (s.Length == 0) return;
            var m = ProgressRx().Match(s);
            if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double pct))
                job.Progress = Math.Clamp(pct / 100.0, 0, 1);
            job.Line(s);
            if (s.Contains("Success! App '1331550'", StringComparison.OrdinalIgnoreCase) || s.Contains("fully installed", StringComparison.OrdinalIgnoreCase)) success = true;
            else if (s.Contains("Two-factor code mismatch", StringComparison.OrdinalIgnoreCase) || s.Contains("Invalid Login Auth Code", StringComparison.OrdinalIgnoreCase)) result = "badcode";
            else if (s.Contains("Account Logon Denied", StringComparison.OrdinalIgnoreCase) || s.Contains("Steam Guard code", StringComparison.OrdinalIgnoreCase)
                     || s.Contains("Two-factor code:", StringComparison.OrdinalIgnoreCase) || s.Contains("need two-factor", StringComparison.OrdinalIgnoreCase)) result = result.Length == 0 ? "code" : result;
            else if (s.Contains("Invalid Password", StringComparison.OrdinalIgnoreCase)) result = "badpass";
            else if (s.Contains("Cached credentials not found", StringComparison.OrdinalIgnoreCase) || s.Contains("password:", StringComparison.OrdinalIgnoreCase)
                     || s.Contains("No cached credentials", StringComparison.OrdinalIgnoreCase)) result = result.Length == 0 ? "password" : result;
            else if (s.Contains("No subscription", StringComparison.OrdinalIgnoreCase)) result = "notowned";
            else if (s.Contains("Rate Limit Exceeded", StringComparison.OrdinalIgnoreCase)) result = "ratelimit";
            else if (s.Contains("Please confirm the login in the Steam Mobile app", StringComparison.OrdinalIgnoreCase))
                job.Message = Lang.T("Bitte die Anmeldung in der Steam-App auf dem Handy bestätigen ...", "Please confirm the login in the Steam app on your phone ...");
            if (s.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) || s.Contains("FAILED", StringComparison.Ordinal)) lastError = s;
            if (result is "code" or "password" or "badpass" or "badcode" or "ratelimit")
                try { p.Kill(entireProcessTree: true); } catch { }   // it would wait for an answer on its console
        }
        var err = Task.Run(async () =>
        {
            string? l;
            while ((l = await p.StandardError.ReadLineAsync(ct)) != null) Seen(l);
        }, ct);
        // stdout char by char: a prompt ("Steam Guard code:") has no line end
        var buf = new char[4096];
        var line = new StringBuilder();
        int n;
        while ((n = await p.StandardOutput.ReadAsync(buf, ct)) > 0)
        {
            for (int i = 0; i < n; i++)
            {
                char c = buf[i];
                if (c == '\n' || c == '\r') { Seen(line.ToString()); line.Clear(); }
                else line.Append(c);
            }
            string partial = line.ToString();
            if (partial.EndsWith(':') && (partial.Contains("code", StringComparison.OrdinalIgnoreCase) || partial.Contains("password", StringComparison.OrdinalIgnoreCase)))
            {
                Seen(partial);
                line.Clear();
            }
        }
        if (line.Length > 0) Seen(line.ToString());
        await p.WaitForExitAsync(ct);
        try { await err; } catch { }
        if (success && File.Exists(Paths.GameExe)) return "ok";
        if (result.Length > 0) return result;
        if (p.ExitCode == 0 && File.Exists(Paths.GameExe)) return "ok";
        return lastError.Length > 0 ? lastError : $"exit code {p.ExitCode}";
    }
}
