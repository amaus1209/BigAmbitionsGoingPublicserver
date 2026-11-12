using System.IO.Compression;

namespace GpsManager;

/// <summary>Backups of the multiplayer saves (every world of this server, every player's slot) and bamp-server.json:
/// backups/saves-YYYYMMDD-HHMM.zip, daily at the set hour and on demand; the newest N stay (as backup-saves.sh under
/// AMP). A running server saves first. Restoring stops the server, keeps the current saves next to them
/// (_BAMP_MP.before-restore-...) and unpacks the backup.</summary>
internal static class Backups
{
    internal static List<FileInfo> List()
    {
        if (!Directory.Exists(Paths.Backups)) return new List<FileInfo>();
        return new DirectoryInfo(Paths.Backups).GetFiles("saves-*.zip").OrderByDescending(f => f.Name, StringComparer.Ordinal).ToList();
    }

    internal static string? Find(string name)
    {
        if (name.Contains('/') || name.Contains('\\') || name.Contains("..") || !name.StartsWith("saves-", StringComparison.Ordinal) || !name.EndsWith(".zip", StringComparison.Ordinal)) return null;
        string f = Path.Combine(Paths.Backups, name);
        return File.Exists(f) ? f : null;
    }

    internal static async Task Create(Job job, CancellationToken ct)
    {
        if (!Directory.Exists(Paths.MpSaves))
        {
            job.State = "failed";
            job.Message = Lang.T("Noch keine Mehrspieler-Spielstände auf diesem Server.", "No multiplayer saves on this server yet.");
            return;
        }
        if (GameServer.Alive && GameServer.ServerState is "running" or "idle")
        {
            job.Message = Lang.T("Server speichert ...", "Server is saving ...");
            job.Line(job.Message);
            GameServer.Command("save", echo: false);
            await Task.Delay(TimeSpan.FromSeconds(20), ct);
        }
        Directory.CreateDirectory(Paths.Backups);
        string name = $"saves-{DateTime.Now:yyyyMMdd-HHmm}.zip";
        string file = Path.Combine(Paths.Backups, name), tmp = file + ".part";
        job.Message = Lang.T("Backup wird geschrieben ...", "Writing the backup ...");
        int count = 0;
        using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            foreach (var f in Directory.EnumerateFiles(Paths.MpSaves, "*", SearchOption.AllDirectories))
            {
                Add(zip, f, Path.Combine("_BAMP_MP", Path.GetRelativePath(Paths.MpSaves, f)));
                count++;
            }
            if (File.Exists(Paths.ServerJson)) Add(zip, Paths.ServerJson, "bamp-server.json");
        }
        File.Move(tmp, file, true);
        foreach (var old in List().Skip(Math.Max(1, ManagerConfig.Current.BackupKeep))) { try { old.Delete(); } catch { } }
        Files.WriteAtomic(Paths.BackupStamp, DateTime.Now.ToString("yyyy-MM-dd") + "\n");
        job.State = "done";
        job.Message = Lang.T($"Backup {name}: {count} Dateien, {new FileInfo(file).Length / 1048576.0:0.0} MB.", $"Backup {name}: {count} files, {new FileInfo(file).Length / 1048576.0:0.0} MB.");
        job.Line(job.Message);
    }

    private static void Add(ZipArchive zip, string file, string entry)
    {
        using var src = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var e = zip.CreateEntry(entry.Replace('\\', '/'), CompressionLevel.Optimal);
        e.LastWriteTime = File.GetLastWriteTime(file);
        using var dst = e.Open();
        src.CopyTo(dst);
    }

    internal static async Task Restore(Job job, string file, CancellationToken ct)
    {
        bool wasRunning = await Jobs.StopGameFor(job);
        string tmp = Path.Combine(Paths.Data, "restore-tmp");
        if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        ZipFile.ExtractToDirectory(file, tmp, true);
        string saves = Path.Combine(tmp, "_BAMP_MP");
        if (!Directory.Exists(saves)) throw new InvalidOperationException(Lang.T("Im Backup sind keine Spielstände (_BAMP_MP).", "The backup has no saves (_BAMP_MP)."));
        if (Directory.Exists(Paths.MpSaves))
        {
            string keep = Paths.MpSaves + ".before-restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.Move(Paths.MpSaves, keep);
            job.Line(Lang.T($"Bisherige Spielstände aufgehoben: {keep}", $"Current saves kept: {keep}"));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Paths.MpSaves)!);
        MoveDir(saves, Paths.MpSaves);
        string conf = Path.Combine(tmp, "bamp-server.json");
        if (File.Exists(conf) && File.Exists(Paths.ServerJson)) File.Copy(Paths.ServerJson, Paths.ServerJson + ".before-restore", true);
        if (File.Exists(conf)) File.Copy(conf, Paths.ServerJson, true);
        Directory.Delete(tmp, true);
        job.State = "done";
        job.Message = Lang.T($"{Path.GetFileName(file)} wiederhergestellt.", $"{Path.GetFileName(file)} restored.");
        job.Line(job.Message);
        if (wasRunning) GameServer.Start(Lang.T("nach dem Wiederherstellen", "after the restore"));
    }

    /// <summary>Directory.Move does not cross drives (Windows: the data on D:, the saves in C:\Users\...).</summary>
    private static void MoveDir(string from, string to)
    {
        try { Directory.Move(from, to); }
        catch (IOException)
        {
            Files.CopyTree(from, to);
            Directory.Delete(from, true);
        }
    }

    /// <summary>Once a minute: the daily backup at the set hour.</summary>
    internal static void Tick()
    {
        int hour = ManagerConfig.Current.BackupHour;
        if (hour is < 0 or > 23 || DateTime.Now.Hour != hour || Jobs.Busy || !Directory.Exists(Paths.MpSaves)) return;
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        try { if (File.Exists(Paths.BackupStamp) && File.ReadAllText(Paths.BackupStamp).Trim() == today) return; } catch { }
        Jobs.Start("backup", Lang.T("Tägliches Backup", "Daily backup"), Create);
    }
}
