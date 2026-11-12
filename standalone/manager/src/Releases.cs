using System.IO.Compression;
using System.Text.Json.Nodes;

namespace GpsManager;

/// <summary>Updates of the server files from GitHub: the newest release of the template repository carries
/// GoingPublicServer-Server.zip (the same file AMP's update stage installs). The players need the same version from the
/// Steam Workshop - the server refuses other builds. The program (this manager) itself comes as a new package.</summary>
internal static class Releases
{
    internal static string LatestTag { get; private set; } = "";
    internal static string LatestUrl { get; private set; } = "";
    internal static string LatestPage { get; private set; } = "";
    internal static DateTime CheckedAt { get; private set; } = DateTime.MinValue;
    internal static string CheckError { get; private set; } = "";

    internal static async Task Check(CancellationToken ct)
    {
        try
        {
            var rel = await Net.GetJson($"https://api.github.com/repos/{ManagerConfig.Current.ReleaseRepo}/releases/latest", ct);
            LatestTag = (string?)rel["tag_name"] ?? "";
            LatestPage = (string?)rel["html_url"] ?? "";
            LatestUrl = (rel["assets"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
                .Where(a => (string?)a["name"] == "GoingPublicServer-Server.zip")
                .Select(a => (string?)a["browser_download_url"] ?? "").FirstOrDefault() ?? "";
            CheckError = "";
        }
        catch (Exception e) { CheckError = e.Message; }
        CheckedAt = DateTime.UtcNow;
    }

    internal static bool UpdateAvailable =>
        LatestTag.Length > 0 && ServerFiles.CompareVersions(ServerFiles.VersionOf(LatestTag), ServerFiles.InstalledVersion) > 0;

    internal static async Task UpdateServerFiles(Job job, CancellationToken ct)
    {
        await Check(ct);
        if (LatestUrl.Length == 0) throw new InvalidOperationException(CheckError.Length > 0 ? CheckError : Lang.T("Kein Server-Zip im neuesten Release.", "No server zip in the newest release."));
        bool wasRunning = await Jobs.StopGameFor(job);
        job.Message = Lang.T($"Server-Dateien {LatestTag} werden geladen ...", $"Downloading the server files {LatestTag} ...");
        job.Line(job.Message);
        string tmp = Path.Combine(Paths.Data, "update-tmp");
        if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        string zip = Path.Combine(Paths.Data, "update-server.zip");
        await Net.Download(LatestUrl, zip, job, ct);
        ZipFile.ExtractToDirectory(zip, tmp, true);
        if (!File.Exists(Path.Combine(tmp, "bamp-mod", "GoingPublicServer", "BigAmbitionsMP.dll")))
            throw new InvalidOperationException(Lang.T("Das Zip enthält keine Mod - nichts geändert.", "The zip has no mod in it - nothing changed."));
        ServerFiles.Apply(tmp);
        ServerFiles.Stamp("github " + LatestTag);
        Directory.Delete(tmp, true);
        File.Delete(zip);
        Manifest.Ensure();
        job.State = "done";
        job.Message = Lang.T($"Server-Dateien {LatestTag} installiert. Die Spieler brauchen dieselbe Version aus dem Steam Workshop.",
                             $"Server files {LatestTag} installed. The players need the same version from the Steam Workshop.");
        job.Line(job.Message);
        if (wasRunning) GameServer.Start(Lang.T("nach dem Update", "after the update"));
    }
}
