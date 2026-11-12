using System.Text.Json.Nodes;

namespace GpsManager;

/// <summary>Linux: Proton GE (GloriousEggroll) runs the Windows game - downloaded from its GitHub releases into
/// server/proton, the newest or the one set under Installation (manager/config.json ProtonVersion). The prefix
/// (server/prefix) is set up once by a Proton call other than "runinprefix" (only those update the prefix files); the
/// game itself starts with "runinprefix", as AMP's template does.</summary>
internal static class ProtonGE
{
    internal static bool Installed => File.Exists(Paths.ProtonScript);

    /// <summary>"GE-Proton10-25" from proton/version ("&lt;stamp&gt; GE-Proton10-25"), "" when not installed.</summary>
    internal static string Version
    {
        get
        {
            try
            {
                string f = Path.Combine(Paths.Proton, "version");
                return File.Exists(f) ? File.ReadAllText(f).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "" : "";
            }
            catch { return ""; }
        }
    }

    internal static async Task Install(Job job, string? tag, CancellationToken ct)
    {
        tag = (tag ?? "").Trim();
        job.Message = Lang.T("Proton-GE-Version wird gesucht ...", "Looking up the Proton GE release ...");
        job.Line(job.Message);
        string api = tag.Length == 0
            ? "https://api.github.com/repos/GloriousEggroll/proton-ge-custom/releases/latest"
            : $"https://api.github.com/repos/GloriousEggroll/proton-ge-custom/releases/tags/{Uri.EscapeDataString(tag)}";
        var rel = await Net.GetJson(api, ct);
        string name = (string?)rel["tag_name"] ?? "";
        // the x86_64 archive (newer releases carry one per architecture: <tag>-x86_64.tar.gz, <tag>-aarch64.tar.gz)
        var tars = (rel["assets"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
            .Where(a => ((string?)a["name"] ?? "").EndsWith(".tar.gz", StringComparison.Ordinal)).ToList();
        var asset = tars.FirstOrDefault(a => ((string?)a["name"] ?? "").Contains("x86_64", StringComparison.Ordinal))
                    ?? tars.FirstOrDefault(a => !((string?)a["name"] ?? "").Contains("aarch64", StringComparison.Ordinal) && !((string?)a["name"] ?? "").Contains("arm64", StringComparison.Ordinal));
        string url = (string?)asset?["browser_download_url"] ?? "";
        if (name.Length == 0 || url.Length == 0) throw new InvalidOperationException(Lang.T($"Keine Proton-GE-Datei in {api}", $"No Proton GE file in {api}"));
        if (Installed && Version == name)
        {
            job.Message = Lang.T($"{name} ist schon installiert.", $"{name} is installed already.");
            job.Line(job.Message);
            await SetupPrefix(job, ct);
            return;
        }
        bool wasRunning = await Jobs.StopGameFor(job);
        job.Message = Lang.T($"{name} wird heruntergeladen (gut 500 MB) ...", $"Downloading {name} (about 500 MB) ...");
        job.Line(job.Message);
        string file = Path.Combine(Paths.Root, "proton-download.tar.gz");
        await Net.Download(url, file, job, ct);
        job.Progress = -1;
        job.Message = Lang.T("Proton wird entpackt ...", "Unpacking Proton ...");
        job.Line(job.Message);
        string fresh = Paths.Proton + ".new";
        if (Directory.Exists(fresh)) Directory.Delete(fresh, true);
        Directory.CreateDirectory(fresh);
        if (await Proc.Run("tar", new[] { "-xzf", file, "-C", fresh, "--strip-components=1" }, job, ct) != 0)
            throw new InvalidOperationException("tar: Proton could not be unpacked");
        File.Delete(file);
        if (Directory.Exists(Paths.Proton)) Directory.Delete(Paths.Proton, true);
        Directory.Move(fresh, Paths.Proton);
        job.Line(Lang.T($"{name} installiert.", $"{name} installed."));
        await SetupPrefix(job, ct);
        job.State = "done";
        job.Message = Lang.T($"{name} ist bereit.", $"{name} is ready.");
        if (wasRunning) GameServer.Start(Lang.T("nach dem Proton-Update", "after the Proton update"));
    }

    /// <summary>The prefix's folders and files (a first start, and after a Proton update): "getcompatpath" runs Proton's
    /// prefix setup and then only converts one path - it starts nothing.</summary>
    internal static async Task SetupPrefix(Job? job, CancellationToken ct)
    {
        if (!Installed) return;
        EnsureHome();
        void Out(string s)
        {
            if (job != null) job.Line(s);
            else ConsoleBuffer.Add("proton", s);
        }
        if (job != null) job.Message = Lang.T("Wine-Präfix wird eingerichtet (beim ersten Mal etwa eine Minute) ...", "Setting up the Wine prefix (about a minute the first time) ...");
        Out(Lang.T("Wine-Präfix wird eingerichtet ...", "Setting up the Wine prefix ..."));
        int rc = await Proc.Run(Paths.ProtonScript, new[] { "getcompatpath", Paths.Game }, null, ct, Platform.ProtonEnv, Paths.Root, Out);
        if (!Directory.Exists(Path.Combine(Paths.Prefix, "pfx", "drive_c")))
            throw new InvalidOperationException(Lang.T($"Das Wine-Präfix wurde nicht angelegt (Proton-Code {rc}). Fehlen Pakete? (python3, siehe Anleitung)",
                                                       $"The Wine prefix was not created (Proton exit {rc}). Missing packages? (python3, see the guide)"));
        Platform.AfterGameExit();
        Out(Lang.T("Wine-Präfix bereit.", "Wine prefix ready."));
    }

    /// <summary>The HOME Proton sees (as AMP's template makes it): ~/.steam/steam, sdk32/sdk64 with SteamCMD's
    /// steamclient.so, ~/.config/protonfixes.</summary>
    internal static void EnsureHome()
    {
        Directory.CreateDirectory(Paths.Prefix);
        foreach (var d in new[] { Path.Combine(".steam", "steam"), Path.Combine(".steam", "sdk32"), Path.Combine(".steam", "sdk64"), Path.Combine(".config", "protonfixes") })
            Directory.CreateDirectory(Path.Combine(Paths.Home, d));
        foreach (var (from, to) in new[] { ("linux32", "sdk32"), ("linux64", "sdk64") })
        {
            try
            {
                string src = Path.Combine(Paths.SteamCmd, from, "steamclient.so"), dst = Path.Combine(Paths.Home, ".steam", to, "steamclient.so");
                if (!File.Exists(src)) continue;
                if (File.Exists(dst) && new FileInfo(dst).Length == new FileInfo(src).Length) continue;
                File.Copy(src, dst, true);
            }
            catch { }
        }
    }
}
