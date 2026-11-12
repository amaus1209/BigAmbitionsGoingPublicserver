using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>
    /// SERVER-MODS-1: a dedicated server publishes the list of extra mods it runs (bamp-start.sh writes it next to the
    /// ranking: https://&lt;server&gt;/monitor/&lt;key folder&gt;/mods/mods.json). When it refuses a joiner for different mods
    /// it adds "dl=1" to the refusal tag, and this client then fetches the list and shows the mods it does not have.
    ///
    /// SEC-MODS-1 (user ruling 2026-10-29): the server hands out no mod files - a mod is program code. A Workshop mod
    /// (AMP stores it in a folder named after its item id) is installed by the player through Steam: "Open in Workshop"
    /// opens the item's page in the Steam client, one per click, and the player subscribes. A mod that is not in the
    /// Workshop is only named, for the player to ask the server admin. The game loads mods only at start, so the
    /// player restarts and joins again. Only the list of the server being joined, only with its key; from
    /// https://&lt;join address&gt; or the web address the server names in the tag ("|web=", AMP-WEB-1).
    /// </summary>
    internal static class ServerModDownload
    {
        private sealed class Entry { public string folder = ""; }
        private sealed class Manifest { public List<Entry> mods = new List<Entry>(); }

        private static int _running;
        private static volatile string _status = "";
        private static volatile List<string>? _workshop;   // item ids still to open, in order
        private static int _opened;

        /// <summary>Text appended to the Couldn't join card ("" = none).</summary>
        public static string Status => _status;

        /// <summary>True while the card should show the "Open in Workshop" button.</summary>
        public static bool Offered => _workshop != null;

        /// <summary>The button's label: which page the next click opens.</summary>
        public static string ButtonLabel
        {
            get
            {
                var w = _workshop;
                return w == null ? "" : Loc.T("gps_mods_ws_open", "Open in Workshop ({0}/{1})", Math.Min(_opened + 1, w.Count), w.Count);
            }
        }

        /// <summary>Network thread (disconnect handler). Fetches the server's mod list when the tag offers it; returns a first line.</summary>
        public static string OnRefusalTag(string tag, string host, string key)
        {
            try
            {
                if (tag.IndexOf("|dl=1", StringComparison.Ordinal) < 0) return "";
                // AMP-WEB-1: the server names its web address when it is not https://<join address>
                string web = "";
                foreach (var part in tag.Split('|'))
                    if (part.StartsWith("web=", StringComparison.Ordinal)) web = part.Substring(4).Trim().TrimEnd('/');
                if (!web.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !web.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) web = "";
                host = (host ?? "").Trim();
                int colon = host.LastIndexOf(':');
                if (colon > 0 && host.IndexOf(':') == colon) host = host.Substring(0, colon);   // the game port is not the web port
                if (web.Length == 0 && host.Length > 0 && !host.StartsWith("steam:", StringComparison.OrdinalIgnoreCase)) web = "https://" + host;
                if (web.Length == 0 || key.Length == 0)
                    return " " + Loc.T("gps_mods_dl_needkey", "The server lists its mods - join with its address and server key to see them.");
                if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return "";
                string url = $"{web}/monitor/{DedicatedHost.KeyFolder(key)}/mods/mods.json?t={DateTime.UtcNow.Ticks}";
                _workshop = null; _opened = 0;
                _status = " " + Loc.T("gps_mods_dl_checking", "Checking the server's mods...");
                Task.Run(() => Check(url));
                return "";
            }
            catch { return ""; }
        }

        /// <summary>Any disconnect: forget the last refusal's offer (a running list fetch writes its own result).</summary>
        public static void Reset()
        {
            if (Volatile.Read(ref _running) != 0) return;
            _workshop = null; _opened = 0; _status = "";
        }

        /// <summary>UI thread, "Open in Workshop": the next missing item's page in the Steam client.</summary>
        public static void OpenNext()
        {
            var w = _workshop;
            if (w == null || w.Count == 0) return;
            int i = Math.Min(_opened, w.Count - 1);
            Application.OpenURL("steam://url/CommunityFilePage/" + w[i]);
            Plugin.Logger.LogInfo($"[ServerMods] opened the Workshop page of item {w[i]} ({i + 1}/{w.Count}).");
            if (_opened < w.Count) _opened++;
        }

        private static void Check(string url)
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                Manifest m;
                using (var wc = new WebClient())
                    m = JsonConvert.DeserializeObject<Manifest>(wc.DownloadString(url)) ?? new Manifest();
                string modsLocal = Path.Combine(Path.GetDirectoryName(MPConfig.DataRootPath.TrimEnd('/', '\\')) ?? ".", "ModsLocal");
                string subscribed = "," + MPContentFingerprint.CachedWorkshopIds + ",";
                var workshop = new List<string>();
                var other = new List<string>();
                foreach (var e in m.mods)
                {
                    string f = (e.folder ?? "").Trim();
                    if (f.Length == 0 || f.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || f == "." || f == "..") continue;
                    bool isWorkshop = f.Length <= 20 && ulong.TryParse(f, out _);
                    if (isWorkshop && subscribed.Contains("," + f + ",")) continue;    // WS-LOCAL-1: already subscribed
                    if (Directory.Exists(Path.Combine(modsLocal, f))) continue;         // the player's own copy
                    if (isWorkshop) workshop.Add(f);
                    else other.Add(f.Replace("<", "").Replace(">", ""));                 // no TMP rich text from the server
                }
                string text = "";
                if (workshop.Count > 0)
                    text += " " + Loc.T("gps_mods_ws_offer", "The server uses {0} Steam Workshop mod(s) you do not have yet. Click \"Open in Workshop\", subscribe to each one, wait until Steam has downloaded them, then restart Big Ambitions and join again.", workshop.Count);
                if (other.Count > 0)
                    text += " " + Loc.T("gps_mods_ws_manual", "Not in the Steam Workshop - ask the server admin for: {0}.", string.Join(", ", other));
                if (text.Length == 0)
                    text = " " + Loc.T("gps_mods_dl_none", "You already have all of the server's mods - deactivate the extra ones listed above, restart Big Ambitions and join again.");
                _status = text;
                if (workshop.Count > 0) _workshop = workshop;
                Plugin.Logger.LogInfo($"[ServerMods] server mods missing here: {workshop.Count} in the Workshop ({string.Join(", ", workshop)}), {other.Count} other ({string.Join(", ", other)}).");
            }
            catch (Exception ex)
            {
                _status = " " + Loc.T("gps_mods_list_failed", "The server's mod list could not be loaded ({0}).", ex.Message);
                Plugin.Logger.LogWarning($"[ServerMods] mod list {url} failed: {ex.Message}");
            }
            finally { Interlocked.Exchange(ref _running, 0); }
        }
    }
}
