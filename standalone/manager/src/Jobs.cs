namespace GpsManager;

/// <summary>A long task (installing the game, Proton, updates, a backup) with its output, progress and result.
/// One at a time; the web page shows the current one.</summary>
internal sealed class Job
{
    private readonly Queue<string> _lines = new();
    public string Id { get; } = Guid.NewGuid().ToString("n")[..8];
    public string Kind { get; init; } = "";
    public string Title { get; init; } = "";
    /// <summary>running | done | failed | needs-code | needs-password</summary>
    public string State { get; set; } = "running";
    public string Message { get; set; } = "";
    /// <summary>0..1, or -1 when unknown.</summary>
    public double Progress { get; set; } = -1;
    public DateTime Started { get; } = DateTime.UtcNow;
    public DateTime? Ended { get; set; }

    internal void Line(string s)
    {
        lock (_lines)
        {
            _lines.Enqueue(s);
            while (_lines.Count > 400) _lines.Dequeue();
        }
        ConsoleBuffer.Add("job", s);
    }

    internal string[] Lines()
    {
        lock (_lines) return _lines.ToArray();
    }
}

internal static class Jobs
{
    private static readonly object _lock = new();
    private static Job? _current;

    internal static Job? Current => _current;
    internal static bool Busy => _current is { State: "running" };
    /// <summary>A running task that needs the game off (game files, Proton, server files).</summary>
    internal static bool BlocksGame => _current is { State: "running", Kind: "game" or "proton" or "files" or "restore" };

    /// <summary>null = another task is running.</summary>
    internal static Job? Start(string kind, string title, Func<Job, CancellationToken, Task> work)
    {
        Job job;
        lock (_lock)
        {
            if (Busy) return null;
            job = new Job { Kind = kind, Title = title };
            _current = job;
        }
        Log.Info(Lang.T($"Aufgabe: {title}", $"Task: {title}"));
        _ = Task.Run(async () =>
        {
            try
            {
                await work(job, CancellationToken.None);
                if (job.State == "running") job.State = "done";
                if (job.State == "done" && job.Message.Length == 0) job.Message = Lang.T("Fertig.", "Done.");
            }
            catch (Exception e)
            {
                job.State = "failed";
                job.Message = e.Message;
                job.Line(Lang.T("Fehler: ", "Error: ") + e.Message);
                Log.Warn($"{title}: {e.Message}");
            }
            finally
            {
                job.Ended = DateTime.UtcNow;
                if (job.State != "running") job.Progress = job.State == "done" ? 1 : job.Progress;
            }
        });
        return job;
    }

    /// <summary>For a task that needs the game off: stops it (saving) and says whether it ran.</summary>
    internal static async Task<bool> StopGameFor(Job job)
    {
        if (!GameServer.Alive && GameServer.State != GameState.Starting) return false;
        job.Message = Lang.T("Server wird gestoppt (speichert) ...", "Stopping the server (saving) ...");
        job.Line(job.Message);
        await GameServer.StopAndWait(job.Title, TimeSpan.FromSeconds(240));
        return true;
    }
}
