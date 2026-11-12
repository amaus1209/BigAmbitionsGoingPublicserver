using System;
using Localizor;

namespace BigAmbitionsMP
{
    /// <summary>
    /// MULTILANG-1: player-visible text in the player's game language. The mod ships Locales/&lt;lang&gt;.json next to its
    /// DLL; the game's mod loader registers that folder (ModDiscoveryRegistry.SyncDiscoveredLocalizationPaths) and the
    /// game's LocalizorManager serves the loaded language, falling back to English. Keys start with "gps_".
    ///
    /// <see cref="T"/> takes the English text as well: it is used when the key is missing (a newer key than the shipped
    /// tables, or the tables not loaded yet), so a missing translation never shows a raw key. Arguments are {0}, {1}...
    /// Server-side console and log text stays English.
    /// </summary>
    internal static class Loc
    {
        public static string T(string key, string english, params object[] args)
        {
            string s = english;
            try
            {
                if (LocalizorManager.IsLocalizedKey(key))
                {
                    string v = LocalizorManager.GetLocalization(key);
                    if (!string.IsNullOrEmpty(v)) s = v;
                }
            }
            catch { }
            if (args == null || args.Length == 0) return s;
            try { return string.Format(s, args); }
            catch
            {
                // a translation with broken placeholders must not cost the message
                try { return string.Format(english, args); } catch { return english; }
            }
        }

        private static System.Collections.Generic.Dictionary<string, string>? _server;

        /// <summary>ADMIN-1: text a host sends to every player (the chat notices: who left, who was kicked, a restart) - in
        /// the SERVER's language on a dedicated server. The game registers the mod's texts only where its mod loader runs
        /// normally, not on a headless server (2026-11-02: the restart warnings came in English on a German server), so the
        /// mod's own Locales/&lt;ServerLanguage&gt;.json is read directly there (as AmpSavePicker does). A player's own host: T.</summary>
        public static string Server(string key, string english, params object[] args)
        {
            if (!HeadlessHost.Active) return T(key, english, args);
            if (_server == null)
            {
                var d = new System.Collections.Generic.Dictionary<string, string>();
                try
                {
                    string lang = MPConfig.GetOption("ServerLanguage", "en").Trim().ToLowerInvariant();
                    string f = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Loc).Assembly.Location) ?? "", "Locales", lang + ".json");
                    if (System.IO.File.Exists(f))
                        d = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, string>>(System.IO.File.ReadAllText(f)) ?? d;
                }
                catch { }
                _server = d;
            }
            string s = _server.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : english;
            if (args == null || args.Length == 0) return s;
            try { return string.Format(s, args); }
            catch { try { return string.Format(english, args); } catch { return english; } }
        }
    }
}
