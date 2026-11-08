using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GpsManager;

/// <summary>STANDALONE-1 (user 2026-11-01: "the access data appear in a separate window on Windows and in the terminal on
/// Linux"): the web interface's address, user and password - once, when they were made (first start, reset-password).
/// Always as a box on the console (Linux: the terminal of install.sh or `docker compose up`), on Windows also in a window
/// of its own. Never into manager.log or the web console.</summary>
internal static class Credentials
{
    /// <summary>Returns the window's thread on Windows (null elsewhere).</summary>
    internal static Thread? Show(string user, string password, bool created)
    {
        var urls = Urls();
        string head = Lang.T("Zugangsdaten zur Weboberfläche", "Web interface access");
        string intro = created
            ? Lang.T("Der Server hat einen Zugang für seine Weboberfläche angelegt. Damit meldest du dich im Browser an.",
                     "The server made an account for its web interface. Log in with it in your browser.")
            : Lang.T("Neues Passwort für die Weboberfläche - das alte gilt nicht mehr.", "A new password for the web interface - the old one no longer works.");
        string note = Lang.T("Das Passwort wird nur jetzt angezeigt. Nach dem Anmelden unter „Konto“ ein eigenes setzen. Vergessen? Neues Passwort:",
                             "The password is shown only now. Set your own under \"Account\" after logging in. Forgot it? A new one:");
        string reset = ResetHint();

        var box = new List<string> { "", new string('=', 72), "  Going Public Server - " + head, new string('=', 72) };
        box.AddRange(Wrap(intro, 68).Select(l => "  " + l));
        box.Add("");
        string a = Lang.T("Adresse", "Address"), u = Lang.T("Benutzer", "User"), p = Lang.T("Passwort", "Password");
        int w = Math.Max(a.Length, Math.Max(u.Length, p.Length)) + 2;
        for (int i = 0; i < urls.Count; i++) box.Add("  " + (i == 0 ? a : "").PadRight(w) + urls[i]);
        box.Add("  " + u.PadRight(w) + user);
        box.Add("  " + p.PadRight(w) + password);
        box.Add("");
        box.AddRange(Wrap(note, 68).Select(l => "  " + l));
        box.Add("    " + reset);
        box.Add(new string('=', 72));
        box.Add("");
        try
        {
            lock (Console.Out)
                foreach (var l in box) Console.WriteLine(l);
            Console.Out.Flush();
        }
        catch { }

        if (!OperatingSystem.IsWindows()) return null;
        return Win32Window.ShowAsync("Going Public Server - " + head, intro,
                new[] { new Win32Window.Row(a, urls[0]), new Win32Window.Row(u, user), new Win32Window.Row(p, password) },
                urls[0], Lang.T("Im Browser öffnen", "Open in browser"), Lang.T("Kopieren", "Copy"), Lang.T("Kopiert", "Copied"),
                Lang.T("Schließen", "Close"), note + " " + reset);
    }

    /// <summary>The addresses the web interface answers on, the one to use first. GPS_PUBLIC_URL wins (Docker, a proxy).</summary>
    internal static List<string> Urls()
    {
        var list = new List<string>();
        string pub = Environment.GetEnvironmentVariable("GPS_PUBLIC_URL") ?? "";
        if (pub.Length > 0) list.Add(pub.TrimEnd('/'));
        int port = ManagerConfig.Port;
        string bind = ManagerConfig.Bind;
        if (Paths.InDocker)
        {
            if (list.Count == 0) list.Add($"http://localhost:{port}");
            return list;
        }
        if (bind is "0.0.0.0" or "*" or "::" or "")
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !ua.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                            list.Add($"http://{ua.Address}:{port}");
                }
            }
            catch { }
            list = list.Distinct().Take(3).ToList();
            list.Add($"http://localhost:{port}");
        }
        else list.Add($"http://{bind}:{port}");
        return list.Distinct().ToList();
    }

    private static string ResetHint()
    {
        string hint = Environment.GetEnvironmentVariable("GPS_RESET_HINT") ?? "";
        if (hint.Length > 0) return hint;
        if (OperatingSystem.IsWindows()) return Lang.T("Reset-Password.bat im Programmordner starten.", "Run Reset-Password.bat in the program folder.");
        string exe = Path.Combine(Paths.App, "GoingPublicServer");
        return $"{exe} reset-password --data {Paths.Data}";
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        var line = new System.Text.StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width) { yield return line.ToString(); line.Clear(); }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0) yield return line.ToString();
    }
}
