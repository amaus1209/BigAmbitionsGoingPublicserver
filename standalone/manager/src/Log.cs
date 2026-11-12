namespace GpsManager;

/// <summary>The manager's own log: the console (the window on Windows, the journal / docker logs on Linux),
/// manager/manager.log (5 MB, one old copy) and the web console.</summary>
internal static class Log
{
    private static readonly object _lock = new();
    private static string _file = "";

    internal static void Init(string file) => _file = file;

    internal static void Info(string message) => Write("INFO", message);
    internal static void Warn(string message) => Write("WARN", message);
    internal static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        lock (_lock)
        {
            try { Console.WriteLine(line); } catch { }
            if (_file.Length > 0)
            {
                try
                {
                    var fi = new FileInfo(_file);
                    if (fi.Exists && fi.Length > 5_000_000) File.Move(_file, _file + ".1", true);
                    File.AppendAllText(_file, line + Environment.NewLine);
                }
                catch { }
            }
        }
        ConsoleBuffer.Add(level == "INFO" ? "manager" : "manager-" + level.ToLowerInvariant(), message);
    }
}

/// <summary>The language of the manager's own texts (log, jobs, the access data): German or English. The web pages
/// switch on their own; this follows manager/config.json "Language", else GPS_LANG, the locale (LANG) or Windows.</summary>
internal static class Lang
{
    internal static bool German { get; private set; }

    internal static void Init(string configured)
    {
        string l = configured;
        if (string.IsNullOrWhiteSpace(l)) l = Environment.GetEnvironmentVariable("GPS_LANG") ?? "";
        if (string.IsNullOrWhiteSpace(l))
            foreach (var v in new[] { "LC_ALL", "LC_MESSAGES", "LANG", "LANGUAGE" })
            {
                l = Environment.GetEnvironmentVariable(v) ?? "";
                if (l.Length > 0 && l != "C" && !l.StartsWith("C.", StringComparison.Ordinal) && l != "POSIX") break;
                l = "";
            }
        if (string.IsNullOrWhiteSpace(l) && OperatingSystem.IsWindows())
        {
            try { l = (Win32Window.UiLanguage() & 0x3FF) == 0x07 ? "de" : "en"; } catch { }
        }
        German = l.Trim().StartsWith("de", StringComparison.OrdinalIgnoreCase);
    }

    internal static string T(string de, string en) => German ? de : en;
}
