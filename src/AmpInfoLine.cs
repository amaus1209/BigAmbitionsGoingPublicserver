using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace BigAmbitionsMP
{
    /// <summary>AMP-INFO-2 (user 2026-11-11: the AMP template for CubeCoders' AMPTemplates may start only the game, not
    /// bamp-start.sh): the status page line bamp-start.sh printed (AMP-INFO-1, INFO-30S-2, ADMIN-LINK-1, BUILD-ID-1) now
    /// comes from the mod: "[Server] Info: Port=… | Schluessel=… | Version=&lt;version&gt;-&lt;build&gt;[ | Admin=&lt;page&gt;]",
    /// read by the template's Console.ServerInfoRegex.
    /// - Every 30 s: AMP hands the values only to pages that are open when the line arrives.
    /// - Not while a stop or restart is announced and not once the game stops (a line then kept AMP's restart waiting).
    /// - Only on an AMP instance (GenericModule.kvp beside the root folder), so a server without AMP gets no key on its
    ///   console every 30 s.
    /// - The admin link only where the instance's template reads it (an older regex would not match the longer line); a
    ///   link that vanishes for a moment (AMP rewriting GenericModule.kvp) is kept for two more lines.</summary>
    internal static class AmpInfoLine
    {
        static float _next;
        static string _last = "";
        static int _adminGone;

        /// <summary>Main thread, from HeadlessHost.Tick (once a second).</summary>
        internal static void Tick(float now, string state, bool countdown)
        {
            if (now < _next) return;
            _next = now + 30f;
            try
            {
                if (state == "stopping" || state == "stopped" || countdown) return;
                string root = RootDir();
                if (root.Length == 0) return;
                string kvp = Path.Combine(Path.GetDirectoryName(root) ?? "", "GenericModule.kvp");
                if (!File.Exists(kvp)) return;

                int port = MPConfig.HostPort;
                string line = $"[Server] Info: Port={port} | Schluessel={Key()} | Version={MyPluginInfo.PLUGIN_VERSION}-{MyPluginInfo.BuildId}";
                string admin = "";
                string web = WebBase(root);
                if (web.Length > 0 && ReadsAdmin(kvp)) admin = $" | Admin={web}/monitor/{port}/index.html#admin";
                if (admin.Length == 0 && _last.StartsWith(line + " | Admin=", StringComparison.Ordinal) && _adminGone < 2)
                {
                    _adminGone++;
                    line = _last;
                }
                else
                {
                    _adminGone = 0;
                    line += admin;
                }
                _last = line;
                HeadlessHost.ConsoleLine(line);
            }
            catch { }
        }

        /// <summary>The instance's root folder (App.RootDir): the parent of the headless folder.</summary>
        static string RootDir()
        {
            string dir = HeadlessHost.DirPath.TrimEnd('/', '\\');
            return dir.Length == 0 ? "" : Path.GetDirectoryName(dir) ?? "";
        }

        /// <summary>The key players join with, as bamp-start.sh showed it: letters, digits, '-' and '_', at most 64.</summary>
        static string Key()
        {
            var sb = new StringBuilder();
            foreach (char c in DedicatedHost.ServerJoinKey())
                if ((c < 128 && char.IsLetterOrDigit(c)) || c == '-' || c == '_') { sb.Append(c); if (sb.Length == 64) break; }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        static readonly Regex BaseInAmpJson = new Regex("\"base\"\\s*:\\s*\"(https?://[A-Za-z0-9.:-]+)\"", RegexOptions.CultureInvariant);
        static readonly Regex PlainBase = new Regex("^(https?://[A-Za-z0-9.:-]+)/*$", RegexOptions.CultureInvariant);

        /// <summary>AMP's address (bamp-web-sync.sh writes telemetry-data/web/amp.json from ADS's Network.BaseURL), else the
        /// PlayerWebUrl setting; "" when neither is a plain http(s)://host[:port].</summary>
        static string WebBase(string root)
        {
            try
            {
                string p = Path.Combine(root, "telemetry-data", "web", "amp.json");
                if (File.Exists(p))
                {
                    var m = BaseInAmpJson.Match(File.ReadAllText(p));
                    if (m.Success) return m.Groups[1].Value;
                }
            }
            catch { }
            try
            {
                var m = PlainBase.Match(MPConfig.PlayerWebUrl ?? "");
                if (m.Success) return m.Groups[1].Value;
            }
            catch { }
            return "";
        }

        static bool ReadsAdmin(string kvp)
        {
            try { return File.ReadAllText(kvp).IndexOf("Admin_Seite", StringComparison.Ordinal) >= 0; }
            catch { return false; }
        }
    }
}
