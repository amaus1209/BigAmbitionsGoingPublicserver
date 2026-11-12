using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>AUTO-UPDATE-1 (user 2026-11-12: "meldung mit update info", then "dann überprüfen nach neue built auf github und
    /// automatische update funktion mit restart und server ankündigung"): a dedicated server under AMP looks for a new build of
    /// the mod on GitHub by itself and puts it in.
    ///
    /// Every hour (the first time 3 minutes after the world is ready; console "update" at once) it reads the latest release of
    /// github.com/amaus1209/BigAmbitionsGoingPublicServer. Its asset GoingPublicServer-Update.json names the build of that
    /// release's GoingPublicServer-Server.zip, its version and what is new (per language). A build other than the running one
    /// is downloaded in the background into bigambitions/bamp-update/, checked (the DLL's SHA-256 must give the announced
    /// build - BUILD-ID-1), and only then put in: bamp-mod/GoingPublicServer (the next start copies it into the game - the
    /// pre-start step "mod" under Linux, "mods" under Windows), bamp-tools/ and the ranking page. Files of the running game
    /// (1331550/) are left to AMP's own "Update". Then every screen gets the notice "Server update to ... - what is new", and
    /// the server restarts: after the announced countdown (AMP setting AutoUpdateWarnMinutes, default 5) while players are
    /// on, at once when nobody is. A new version number also tells the players to update the mod (Steam does it at the next
    /// game start). An announced restart or AMP's stop already running is never cut short; the update waits for it.
    ///
    /// Only on an AMP layout (bamp-mod/GoingPublicServer next to headless/) - the stand-alone server updates through its own
    /// page. AMP setting AutoUpdate, on by default. The previous mod folder stays in bamp-update/previous until the next update.</summary>
    internal static class AutoUpdate
    {
        const string Repo = "amaus1209/BigAmbitionsGoingPublicServer";
        const string InfoAsset = "GoingPublicServer-Update.json", ServerAsset = "GoingPublicServer-Server.zip";
        const float FirstCheck = 180f, Every = 3600f;
        static readonly string[] Take = { "bamp-mod/GoingPublicServer/", "bamp-tools/", "telemetry-data/web/" };

        sealed class Found { public string Build = "", Version = "", Notes = ""; }

        static float _nextCheck = -1f;
        static int _busy;
        static volatile Found? _ready;
        static string _failedBuild = "", _doneBuild = "";
        static bool _announced, _reply, _loggedLayout;

        static string Root
        {
            get
            {
                string d = HeadlessHost.DirPath;
                return string.IsNullOrEmpty(d) ? "" : (Path.GetDirectoryName(d.TrimEnd('/', '\\')) ?? "");
            }
        }

        static bool AmpLayout(string root) => root.Length > 0 && File.Exists(Path.Combine(root, "bamp-mod", "GoingPublicServer", "BigAmbitionsMP.dll"));

        /// <summary>Main thread, once a second (HeadlessHost.Tick) while the world runs.</summary>
        internal static void Tick(float now, bool worldUp)
        {
            if (!worldUp || !MPConfig.AutoUpdate) return;
            var r = _ready;
            if (r != null && !_announced)
            {
                if (HeadlessHost.Stopping || HeadlessHost.RestartPending) return;   // wait for the countdown that runs
                _announced = true;
                Announce(r);
                return;
            }
            if (_nextCheck < 0f) { _nextCheck = now + FirstCheck; return; }
            if (now < _nextCheck || _announced) return;
            _nextCheck = now + Every;
            Start(false);
        }

        /// <summary>Console "update": look now, and say what came of it.</summary>
        internal static string Command()
        {
            if (!MPConfig.AutoUpdate) return "ERR the AMP setting \"Automatic updates\" is off";
            if (_announced) return $"OK build {_ready?.Build} is in - the restart is announced";
            if (!Start(true)) return "OK a check is already running";
            return "OK looking for a new build on GitHub - the answer follows";
        }

        static bool Start(bool reply)
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return false;
            _reply = reply;
            string root = Root;
            string running = MyPluginInfo.BuildId;
            new Thread(() =>
            {
                try { Check(root, running); }
                catch (Exception ex) { Say($"update: {ex.Message} (AUTO-UPDATE-1)", true); }
                finally { Interlocked.Exchange(ref _busy, 0); }
            }) { IsBackground = true, Name = "BAMP-AutoUpdate" }.Start();
            return true;
        }

        static void Say(string text, bool warn = false)
        {
            if (warn) Plugin.Logger.LogWarning("[Update] " + text); else Plugin.Logger.LogInfo("[Update] " + text);
            if (_reply) HeadlessHost.Out(text);
        }

        static WebClient Client()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "GoingPublicServer-AutoUpdate";
            return wc;
        }

        /// <summary>Background thread: the latest release, its build, and - when it is another - the download.</summary>
        static void Check(string root, string running)
        {
            if (!AmpLayout(root))
            {
                if (!_loggedLayout || _reply) Say("no AMP layout (bamp-mod/GoingPublicServer) - this server is updated by its own page or by hand (AUTO-UPDATE-1).");
                _loggedLayout = true;
                return;
            }
            JObject rel;
            using (var wc = Client()) { wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json"; rel = JObject.Parse(wc.DownloadString($"https://api.github.com/repos/{Repo}/releases/latest")); }
            string infoUrl = "", zipUrl = "";
            foreach (var a in (JArray?)rel["assets"] ?? new JArray())
            {
                string n = (string?)a["name"] ?? "";
                if (n == InfoAsset) infoUrl = (string?)a["browser_download_url"] ?? "";
                if (n == ServerAsset) zipUrl = (string?)a["browser_download_url"] ?? "";
            }
            string tag = (string?)rel["tag_name"] ?? "?";
            if (infoUrl.Length == 0 || zipUrl.Length == 0) { Say($"release {tag} has no {(infoUrl.Length == 0 ? InfoAsset : ServerAsset)} - nothing to compare (AUTO-UPDATE-1)."); return; }
            JObject info;
            using (var wc = Client()) info = JObject.Parse(wc.DownloadString(infoUrl));
            var f = new Found
            {
                Build = ((string?)info["build"] ?? "").Trim().ToLowerInvariant(),
                Version = ((string?)info["version"] ?? "").Trim(),
                Notes = NotesFor(info["notes"] as JObject),
            };
            if (f.Build.Length != 8) { Say($"release {tag}: {InfoAsset} names no build (AUTO-UPDATE-1).", true); return; }
            if (f.Build == running || f.Build == _doneBuild) { Say($"up to date: build {running} is the newest on GitHub ({tag}) (AUTO-UPDATE-1)."); return; }
            if (f.Build == _failedBuild && !_reply) return;
            Say($"build {f.Build} ({f.Version}) is on GitHub, this server runs {running} - downloading (AUTO-UPDATE-1).");

            string work = Path.Combine(root, "bamp-update");
            string fresh = Path.Combine(work, "new");
            if (Directory.Exists(fresh)) Directory.Delete(fresh, true);
            Directory.CreateDirectory(fresh);
            string zip = Path.Combine(work, ServerAsset);
            using (var wc = Client()) wc.DownloadFile(zipUrl, zip);
            int files = 0;
            using (var za = ZipFile.OpenRead(zip))
                foreach (var e in za.Entries)
                {
                    string name = e.FullName.Replace('\\', '/');
                    if (name.EndsWith("/") || name.Contains("..") || name.StartsWith("/") || name.Contains(":")) continue;
                    bool wanted = false;
                    foreach (var t in Take) if (name.StartsWith(t, StringComparison.Ordinal)) wanted = true;
                    if (!wanted) continue;
                    string dst = Path.Combine(fresh, name.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    using (var s = e.Open()) using (var o = File.Create(dst)) s.CopyTo(o);
                    files++;
                }
            File.Delete(zip);
            string dll = Path.Combine(fresh, "bamp-mod", "GoingPublicServer", "BigAmbitionsMP.dll");
            string got = "";
            if (File.Exists(dll))
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    got = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(dll)), 0, 4).Replace("-", "").ToLowerInvariant();
            if (got != f.Build)
            {
                _failedBuild = f.Build;
                Say($"the downloaded server zip holds build '{got}', not the announced {f.Build} - left out, the server keeps {running} (AUTO-UPDATE-1).", true);
                return;
            }

            // put it in: the mod folder whole (the old one stays in bamp-update/previous), the tools and the page file by file
            string mod = Path.Combine(root, "bamp-mod", "GoingPublicServer");
            string prev = Path.Combine(work, "previous");
            if (Directory.Exists(prev)) Directory.Delete(prev, true);
            Directory.Move(mod, prev);
            try { Directory.Move(Path.Combine(fresh, "bamp-mod", "GoingPublicServer"), mod); }
            catch { Directory.Move(prev, mod); throw; }
            foreach (var t in new[] { "bamp-tools", Path.Combine("telemetry-data", "web") })
            {
                string src = Path.Combine(fresh, t);
                if (!Directory.Exists(src)) continue;
                foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                {
                    string dst = Path.Combine(root, t, file.Substring(src.Length).TrimStart('/', '\\'));
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    File.Copy(file, dst, true);
                }
            }
            Directory.Delete(fresh, true);
            _doneBuild = f.Build;
            Say($"build {f.Build} ({f.Version}) is in place ({files} file(s)); it runs from the next start (AUTO-UPDATE-1).");
            _ready = f;
        }

        /// <summary>What is new, in the server's language (else English, else the first there is).</summary>
        static string NotesFor(JObject? notes)
        {
            if (notes == null) return "";
            string lang = MPConfig.GetOption("ServerLanguage", "en").Trim().ToLowerInvariant();
            foreach (var k in new[] { lang, "en" })
                if (notes[k] is JToken t && ((string?)t ?? "").Trim().Length > 0) return ((string?)t)!.Trim();
            foreach (var p in notes.Properties()) if (((string?)p.Value ?? "").Trim().Length > 0) return ((string?)p.Value)!.Trim();
            return "";
        }

        /// <summary>Main thread: the notice on every screen, then the restart (a countdown while players are on).</summary>
        static void Announce(Found f)
        {
            string text = Loc.Server("gps_update_notice", "Server update to version {0} (build {1}).", f.Version, f.Build);
            if (f.Notes.Length > 0) text += " " + Loc.Server("gps_update_new", "New: {0}", f.Notes);
            if (f.Version.Length > 0 && f.Version != MyPluginInfo.PLUGIN_VERSION)
                text += " " + Loc.Server("gps_update_workshop", "Players: the mod needs the new version too - Steam updates it at the next game start (Steam Workshop).");
            int secs = HeadlessHost.PlayerCount > 0 ? MPConfig.AutoUpdateWarnMinutes * 60 : 0;
            Plugin.Logger.LogInfo($"[Update] announced: build {f.Build}, restart in {secs} s (AUTO-UPDATE-1).");
            HeadlessHost.Out($"update to build {f.Build} announced - restart in {(secs > 0 ? secs / 60 + " min" : "a moment")}");
            string why = Loc.Server("gps_update_reason", "Update to version {0} (build {1}).", f.Version, f.Build);
            HeadlessHost.AnnounceRestart(secs, text, why);
        }
    }
}
