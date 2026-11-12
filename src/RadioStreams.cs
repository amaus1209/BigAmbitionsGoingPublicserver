using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>RADIO-STREAMS-1 (user 2026-11-01: "add that the game's radio can play radio streams from a file"): the
    /// game's radio gets internet stations from a file - &lt;the game's Radio folder&gt;/streams.txt (made on first use, with
    /// the format explained in it), one station per line "Name = address", and every .m3u / .pls playlist in that folder.
    /// The radio's "next station" goes through the game's stations first, then the streams, then back to the first
    /// station. A stream plays through the radio's own AudioSource, so the game's radio volume, the mute on leaving the
    /// car and headphones work as for its own songs; the label shows "Stream: name". MP3 and OGG streams (Unity's
    /// streamed DownloadHandlerAudioClip); a .m3u / .pls address is opened for its first stream. A dropped stream
    /// reconnects; a muted one stops downloading until the radio is heard again. Riders hear the driver's stream
    /// (PASSENGER-RADIO-1 carries its address).
    /// RADIO-STREAMS-2 (user 2026-11-01: "the list from GitHub, all German streams, the language chosen in AMP"): on a
    /// server, the station list of the language AMP's "Radio station list" names (ServerRules RadioList) comes from the
    /// template repository - radio/&lt;language&gt;.txt, the same format - once per game session, kept as
    /// github-&lt;language&gt;.txt in the Radio folder for a start without the internet; the player's own streams.txt
    /// comes first.
    /// RADIO-STREAMS-3 (user 2026-11-01: "the streams don't play, whatever the station"): Unity cannot play an endless MP3
    /// stream on the desktop, so the stream is our own (RadioDecoder: a socket, the ICY title, NLayer) - http plays again,
    /// and the label shows the song when the station sends it.
    /// RADIO-ONFOOT-1 (user 2026-11-01: "radio on foot"): a switch in the phone's Radio app - the radio keeps playing out of
    /// the car, also without headphones; inside a building it is silent (the shop's loudspeakers play there).
    /// RADIO-SORT-1 (user 2026-11-01: "sort the stations alphabetically"): a switch in the Radio app - the streams by name,
    /// the player's own first, then the server's list; "next station" goes in the same order. Off = the files' order (the
    /// server's list: the most popular first).
    /// RADIO-FAV-2 (user 2026-11-01): with favourites, "next station" (in the car, and at a loudspeaker - ShopRadio) goes
    /// through them only - 250 stations are too many to click through.
    /// RADIO-FORMAT-1: a station in a format that does not play says so on the radio's label and in the Radio app, and is
    /// not tried again (a pick of it tries once more).</summary>
    internal static class RadioStreams
    {
        internal sealed class Station
        {
            public string Name = "";
            public string Url = "";
            public bool Server;   // from the server's list (RADIO-STREAMS-2), not the player's own
        }

        private static List<Station> _stations = new List<Station>();
        private static int _index = -1;                 // the file's station playing (-1 = none, or a rider's stream)
        private static string _name = "", _url = "";     // what plays now ("" = the game's own stations)
        private static RadioDecoder? _dec;              // RADIO-STREAMS-3: our own stream (Unity's download cannot play MP3 streams)
        private static bool _clipOn;
        private static bool? _loopWas;                  // the radio AudioSource's loop flag before a stream took it (the game never sets it)
        private static string _shownTitle = "";
        private static float _connectedAt, _silentSince = -1f, _nextTryAt;
        private static string _problem = "";   // RADIO-FORMAT-1: why the stream that should play does not ("" = fine)
        private static bool _ownCall;

        internal static bool Active => _url.Length > 0;
        internal static string CurrentName => _name;
        internal static string CurrentUrl => _url;
        /// <summary>RADIO-STREAMS-3: the song the station says plays now ("" when it sends none).</summary>
        internal static string CurrentTitle => _dec?.Title ?? "";
        internal static string FilePath => Path.Combine(RadioPlayer.GetRadioPath(), "streams.txt");
        /// <summary>RADIO-TAB-1: the list as last read (Load).</summary>
        internal static IReadOnlyList<Station> Stations => _stations;
        internal static bool NewListWaiting => _fetched;
        internal static bool ListUnreachable => _fetchFailed;
        internal static string ServerListLanguage => ServerLang();

        private const string RemoteBase = "https://raw.githubusercontent.com/amaus1209/BigAmbitionsGoingPublicServer/main/radio/";
        private static string _fetchLang = "";      // the server list asked for in this game session
        private static volatile bool _fetched;      // a download landed - the next Load() reads it
        private static volatile bool _fetchFailed;  // GitHub could not be reached this session
        private static float _nextPrefetch;


        // ── the file ──

        internal static List<Station> Load()
        {
            var list = new List<Station>();
            try
            {
                string dir = RadioPlayer.GetRadioPath();
                Directory.CreateDirectory(dir);
                if (!File.Exists(FilePath)) File.WriteAllText(FilePath, Template());
                ReadList(list, File.ReadAllLines(FilePath));
                foreach (var f in Directory.GetFiles(dir))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".m3u" || ext == ".m3u8") ReadM3u(list, File.ReadAllLines(f));
                    else if (ext == ".pls") ReadPls(list, File.ReadAllLines(f));
                }
                string lang = ServerLang();   // RADIO-STREAMS-2: then the server's list
                if (lang.Length > 0)
                {
                    Fetch(lang);
                    string cache = CachePath(lang);
                    if (File.Exists(cache)) ReadList(list, File.ReadAllLines(cache, Encoding.UTF8), server: true);
                }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[RadioStreams] {FilePath}: {ex.Message}"); }
            if (SortAz)   // RADIO-SORT-1: by name within each group (stable: equal names keep the files' order)
                list = list.Where(s => !s.Server).OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
                           .Concat(list.Where(s => s.Server).OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)).ToList();
            _fetched = false;
            _stations = list;
            if (_url.Length > 0) _index = list.FindIndex(s => s.Url == _url);   // a re-read list keeps "next" on the stream that plays
            return list;
        }

        private static void ReadList(List<Station> list, string[] lines, bool server = false)
        {
            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal)) continue;
                int eq = line.IndexOf(" = ", StringComparison.Ordinal);
                if (eq < 0) eq = line.IndexOf('|');
                string name = eq > 0 ? line.Substring(0, eq).Trim() : "";
                string url = (eq > 0 ? line.Substring(eq + (line[eq] == '|' ? 1 : 3)) : line).Trim();
                Add(list, name, url, server);
            }
        }

        // ── RADIO-STREAMS-2: the server's list ──

        /// <summary>The server's list language ("" = none: single player, or AMP says off).</summary>
        private static string ServerLang()
        {
            if (!MPServer.IsRunning && !MPClient.IsClientInWorld) return "";
            string l = ServerRules.RadioList;
            if (l.Length == 0 || l == "off" || l.Length > 8) return "";
            foreach (char c in l) if (!(c == '-' || (c >= 'a' && c <= 'z'))) return "";
            return l;
        }

        private static string CachePath(string lang) => Path.Combine(RadioPlayer.GetRadioPath(), "github-" + lang + ".txt");

        /// <summary>MAIN THREAD, every frame in a session (MPCanvasUI): ask for the server's list soon after joining, so
        /// it is there by the first "next station".</summary>
        internal static void Prefetch()
        {
            if (Time.unscaledTime < _nextPrefetch) return;
            _nextPrefetch = Time.unscaledTime + 3f;
            if (MPConfig.DedicatedHost) return;   // nobody listens on a dedicated server
            try { string lang = ServerLang(); if (lang.Length > 0) Fetch(lang); } catch { }
        }

        /// <summary>Once per game session and language, in the background - the way the News and the translation tables
        /// come from the same repository. The copy is rewritten only when GitHub's list changed.</summary>
        private static void Fetch(string lang)
        {
            if (lang == _fetchLang) return;
            _fetchLang = lang;
            _fetchFailed = false;
            string path = CachePath(lang);   // Unity's paths only on the main thread
            Task.Run(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    string text;
                    using (var wc = new WebClient { Encoding = Encoding.UTF8 })
                        text = wc.DownloadString(RemoteBase + lang + ".txt?t=" + DateTime.UtcNow.Ticks) ?? "";
                    int n = 0;
                    foreach (var l in text.Split('\n')) if (l.TrimStart().StartsWith("http", StringComparison.OrdinalIgnoreCase) || l.Contains(" = http")) n++;   // http plays too (RADIO-STREAMS-3)
                    if (n == 0) { Plugin.Logger.LogInfo($"[RadioStreams] station list '{lang}' on GitHub holds no station - kept the saved copy."); return; }
                    bool same = File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) == text;
                    if (!same) { File.WriteAllText(path, text, Encoding.UTF8); _fetched = true; }
                    Plugin.Logger.LogInfo($"[RadioStreams] station list '{lang}' from GitHub: {n} station(s){(same ? ", unchanged" : "")}.");
                }
                catch (Exception ex)
                {
                    _fetchFailed = true;
                    Plugin.Logger.LogInfo($"[RadioStreams] station list '{lang}' not loaded from GitHub ({ex.Message}) - {(File.Exists(path) ? "the saved copy plays" : "only streams.txt")}.");
                }
            });
        }

        private static void Add(List<Station> list, string name, string url, bool server = false)
        {
            if (!IsStreamUrl(url)) return;
            foreach (var s in list) if (string.Equals(s.Url, url, StringComparison.OrdinalIgnoreCase)) return;   // the same stream twice: once
            if (name.Length == 0) { try { name = new Uri(url).Host; } catch { name = url; } }
            list.Add(new Station { Name = name.Length > 40 ? name.Substring(0, 40) : name, Url = url, Server = server });
        }

        private static void ReadM3u(List<Station> list, string[] lines)
        {
            string title = "";
            foreach (var raw in lines)
            {
                string l = raw.Trim();
                if (l.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase)) { int c = l.IndexOf(','); title = c >= 0 ? l.Substring(c + 1).Trim() : ""; }
                else if (l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal)) { Add(list, title, l); title = ""; }
            }
        }

        private static void ReadPls(List<Station> list, string[] lines)
        {
            var files = new SortedDictionary<int, string>();
            var titles = new Dictionary<int, string>();
            foreach (var raw in lines)
            {
                string l = raw.Trim();
                int eq = l.IndexOf('=');
                if (eq <= 0) continue;
                string k = l.Substring(0, eq).Trim(), v = l.Substring(eq + 1).Trim();
                if (k.StartsWith("File", StringComparison.OrdinalIgnoreCase) && int.TryParse(k.Substring(4), out int n)) files[n] = v;
                else if (k.StartsWith("Title", StringComparison.OrdinalIgnoreCase) && int.TryParse(k.Substring(5), out int m)) titles[m] = v;
            }
            foreach (var kv in files) Add(list, titles.TryGetValue(kv.Key, out var t) ? t : "", kv.Value);
        }

        /// <summary>RADIO-STREAMS-3: http and https both play (our own socket - the game's ban on plain http binds only
        /// UnityWebRequest, which no longer carries the streams).</summary>
        internal static bool IsStreamUrl(string url) =>
            url.Length > 9 && url.Length < 600
            && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

        private static string Template() => string.Join(Environment.NewLine, new[]
        {
            "# Going Public Server - Internetradio im Spiel / internet radio in the game",
            "#",
            "# Eine Zeile pro Sender:   Name = Adresse   (ein MP3-Stream, http oder https; .m3u- und .pls-Links gehen auch)",
            "# One station per line:   Name = address   (an MP3 stream, http or https; .m3u and .pls links work too)",
            "# AAC- und HLS-Streams (.m3u8) spielen nicht. / AAC and HLS streams (.m3u8) do not play.",
            "# Auch .m3u- und .pls-Dateien in diesem Ordner werden gelesen. / .m3u and .pls files in this folder are read too.",
            "#",
            "# Auf einem Server folgt die Senderliste des Servers (in AMP gewählt, von GitHub; Kopie hier: github-<Sprache>.txt).",
            "# On a server, the server's station list follows (chosen in AMP, from GitHub; a copy here: github-<language>.txt).",
            "#",
            "# Im Auto: Radio, \"nächster Sender\" - nach den Sendern des Spiels kommen diese, dann wieder der erste.",
            "# In the car: the radio's \"next station\" - after the game's stations come these, then the first again.",
            "# Mitfahrer hören den Sender des Fahrers. / Passengers hear the driver's station.",
            "#",
            "# Beispiel (ohne # davor) / example (without the # in front):",
            "# Mein Sender = https://stream.example.org/live.mp3",
            "# TruckersFM = https://radio.truckers.fm/",
            "",
        });

        // ── playing ──

        /// <summary>The radio's "next station": after the game's last station the streams, after the last stream the game's first.</summary>
        internal static bool OnNext(RadioPlayer rp)
        {
            // RADIO-FAV-2: with favourites, "next" goes through them
            var favs = ResolvedFavorites(rp);
            if (favs.Count > 0)
            {
                string playing = Active ? "url:" + _url : "game:" + (int)rp.GetCurrentStation();
                var f = favs[(favs.FindIndex(x => x.Key == playing) + 1) % favs.Count];   // not among them: the first
                if (f.Game)
                {
                    if (Active) End(rp, f.Station); else rp.PlayStation(f.Station);
                    PlayerPrefSettings.RadioStation = (int)f.Station;
                }
                else Resume(rp, f.Name, f.Url);
                return false;
            }
            if (Active)
            {
                if (_fetched) Load();   // RADIO-STREAMS-2: GitHub's list just landed
                var list = _stations;
                if (_index >= 0 && _index + 1 < list.Count) { PlayIndex(rp, _index + 1); return false; }
                End(rp, RadioStation.Dance);
                PlayerPrefSettings.RadioStation = (int)RadioStation.Dance;
                return false;
            }
            var cur = rp.GetCurrentStation();
            int count = Enum.GetValues(typeof(RadioStation)).Length;
            RadioStation Next(RadioStation r) => (RadioStation)(((int)r + 1) % count);   // the game's own Next() wraps the same way
            var n = Next(cur);
            for (int i = 0; i < 6 && n != cur && !rp.GetRadioStationData(n).HasPlayableClips; i++) n = Next(n);
            if ((int)n > (int)cur) return true;          // the game's next station
            if (Load().Count == 0) return true;           // no streams: the game wraps around as always
            PlayIndex(rp, 0);
            return false;
        }

        /// <summary>RADIO-TAB-1: a station picked in the phone's Radio app ("next" goes on from it).</summary>
        internal static void PlayListed(RadioPlayer rp, Station s)
        {
            int i = _stations.IndexOf(s);
            if (i >= 0) PlayIndex(rp, i); else Play(rp, s.Name, s.Url);
        }

        /// <summary>PASSENGER-RADIO-1: the player's own stream again (a rider getting out), "next" going on from it.</summary>
        internal static void Resume(RadioPlayer rp, string name, string url)
        {
            if (url == _url && _dec != null) return;
            int i = _stations.FindIndex(s => s.Url == url);
            if (i >= 0) PlayIndex(rp, i); else Play(rp, name, url);
        }

        private static void PlayIndex(RadioPlayer rp, int i)
        {
            var s = _stations[i];
            Play(rp, s.Name, s.Url);
            _index = i;
        }

        /// <summary>Plays this stream on the radio (a file's station, or the driver's for a rider).</summary>
        internal static void Play(RadioPlayer rp, string name, string url)
        {
            if (!IsStreamUrl(url)) return;
            StopDownload();
            _problem = "";
            _index = -1;
            _name = name.Length > 0 ? name : url;
            _url = url;
            _nextTryAt = 0f;
            try { rp.audioSource.Stop(); } catch { }
            Connect();
            try { rp.onNewSong.Invoke(_name); } catch { }
            Plugin.Logger.LogInfo($"[RadioStreams] '{_name}'.");
        }

        /// <summary>Back to the game's stations.</summary>
        internal static void End(RadioPlayer rp, RadioStation station)
        {
            if (!Active) return;
            StopDownload();
            _url = _name = "";
            _problem = "";
            _index = -1;
            try { rp.audioSource.Stop(); } catch { }
            _ownCall = true;
            try { rp.PlayStation(station); } finally { _ownCall = false; }
        }

        private static void Connect()
        {
            _dec = new RadioDecoder(_url);
            _dec.Start();
            _connectedAt = Time.unscaledTime;
            _silentSince = -1f;
            _clipOn = false;
            _shownTitle = "";
        }

        /// <summary>RADIO-STREAMS-3: the radio's label - the station, and the song when the station sends one.</summary>
        internal static string Label()
        {
            string t = CurrentTitle;
            return t.Length > 0 ? _name + "\n" + t : Loc.T("gps_radio_stream_label", "Stream: {0}", _name);
        }

        /// <summary>RADIO-FORMAT-1: why the stream that should play does not ("" = it plays, or nothing to say).</summary>
        internal static string Problem => _problem;

        private static void Fail(RadioPlayer rp, string why)
        {
            string fmt = _dec?.Format ?? "";
            if (fmt.Length > 0)
            {
                Plugin.Logger.LogWarning($"[RadioStreams] '{_name}' sends {fmt} - only MP3 plays; not tried again.");
                StopDownload();
                _nextTryAt = float.MaxValue;   // a pick of the station again (Play) tries once more
                _problem = Loc.T("gps_radio_problem_format", "{0} sends {1} - only MP3 streams play.", _name, fmt);
                try { rp.onNewSong.Invoke(Loc.T("gps_radio_stream_format", "{0} ({1} does not play)", _name, fmt)); } catch { }
                return;
            }
            _problem = Loc.T("gps_radio_problem_offline", "No connection to {0} - trying again.", _name);
            bool ran = _clipOn && Time.unscaledTime - _connectedAt > 60f;   // it played a while: a dropped line - again at once
            Plugin.Logger.LogWarning($"[RadioStreams] '{_name}': {why} - tries again in {(ran ? 2 : 20)} s.");
            StopDownload();
            _nextTryAt = Time.unscaledTime + (ran ? 2f : 20f);
            try { rp.onNewSong.Invoke(Loc.T("gps_radio_stream_offline", "{0} (no connection)", _name)); } catch { }
        }

        private static void StopDownload()
        {
            var d = _dec;
            _dec = null;
            _clipOn = false;
            if (d == null) return;
            d.Stop();
            try
            {
                var rp = InstanceBehavior<GameManager>.Instance?.radioPlayer;
                if (rp != null && d.Clip != null && rp.audioSource.clip == d.Clip) { rp.audioSource.Stop(); rp.audioSource.clip = null; }
                if (rp != null && _loopWas.HasValue) rp.audioSource.loop = _loopWas.Value;   // the game's songs end again
            }
            catch { }
            _loopWas = null;
            d.DestroyClip();
        }

        /// <summary>RadioPlayer.Update while a stream plays (the game's song clock is skipped): the clip goes on once enough
        /// audio waits, the label follows the song, a stalled or failed stream reconnects, a muted radio downloads nothing.</summary>
        internal static void Tick(RadioPlayer rp)
        {
            float now = Time.unscaledTime;
            if (rp.IsMuted)
            {
                if (_dec != null) { StopDownload(); try { rp.audioSource.Stop(); } catch { } }
                _nextTryAt = 0f;
                return;
            }
            if (_dec == null) { if (now >= _nextTryAt) { _nextTryAt = now + 20f; Connect(); } return; }
            var d = _dec;
            if (d.Ended && d.Buffered <= 0.05f) { Fail(rp, d.Error.Length > 0 ? d.Error : "the station ended the stream"); return; }
            if (!_clipOn)
            {
                var clip = d.ClipWhenReady();
                if (clip == null) { if (now - _connectedAt > 25f) Fail(rp, "no audio within 25 s"); return; }
                try { var a = rp.audioSource; _loopWas ??= a.loop; a.clip = clip; a.loop = true; a.Play(); } catch { }   // the clip is a 2 s ring
                _clipOn = true;
                _problem = "";
                Plugin.Logger.LogInfo($"[RadioStreams] '{_name}' plays ({d.SampleRate} Hz, {d.Channels} channel(s)).");
            }
            string t = d.Title;
            if (t != _shownTitle) { _shownTitle = t; try { rp.onNewSong.Invoke(Label()); } catch { } }
            if (d.Buffered > 0.05f) _silentSince = -1f;
            else if (_silentSince < 0f) _silentSince = now;
            else if (now - _silentSince > 8f)
            {
                Plugin.Logger.LogInfo($"[RadioStreams] '{_name}': no audio for 8 s - reconnecting.");
                StopDownload();
                _nextTryAt = 0f;
            }
        }

        // ── RADIO-SORT-1 ──

        private const string SortPref = "BAMP_RadioSortAZ";
        private static int _sortAz = -1;                  // -1 = not read yet

        /// <summary>The player's switch (this machine only, PlayerPrefs): the streams in alphabetical order.</summary>
        internal static bool SortAz
        {
            get { if (_sortAz < 0) { try { _sortAz = UnityEngine.PlayerPrefs.GetInt(SortPref, 0); } catch { _sortAz = 0; } } return _sortAz == 1; }
            set
            {
                _sortAz = value ? 1 : 0;
                try { UnityEngine.PlayerPrefs.SetInt(SortPref, _sortAz); UnityEngine.PlayerPrefs.Save(); } catch { }
                Plugin.Logger.LogInfo($"[RadioStreams] stations {(value ? "in alphabetical order" : "in the files' order")}.");
                Load();   // the new order now ("next" goes on from the stream that plays)
            }
        }

        // ── RADIO-FAV-1 ──

        private const string FavPref = "BAMP_RadioFavorites";
        private static List<string>? _favs;   // the Radio app's row keys ("game:<n>", "url:<address>") in the order marked

        /// <summary>RADIO-FAV-1 (user 2026-11-01: "a favourites list in the radio that stands at the top - marked with a
        /// heart"): the player's favourites (this machine only, PlayerPrefs), in the order he marked them.</summary>
        internal static IReadOnlyList<string> Favorites { get { LoadFavs(); return _favs!; } }

        internal static bool IsFavorite(string key) { LoadFavs(); return _favs!.Contains(key); }

        internal static void ToggleFavorite(string key)
        {
            LoadFavs();
            bool added = !_favs!.Remove(key);
            if (added) _favs.Add(key);
            try { UnityEngine.PlayerPrefs.SetString(FavPref, string.Join("\n", _favs)); UnityEngine.PlayerPrefs.Save(); } catch { }
            Plugin.Logger.LogInfo($"[RadioStreams] favourite {(added ? "added" : "removed")}: {key} ({_favs.Count} in all).");
        }

        private static void LoadFavs()
        {
            if (_favs != null) return;
            _favs = new List<string>();
            try
            {
                foreach (var l in UnityEngine.PlayerPrefs.GetString(FavPref, "").Split('\n'))
                    if (l.Length > 0 && !_favs.Contains(l)) _favs.Add(l);
            }
            catch { }
        }

        /// <summary>RADIO-FAV-2: a favourite that can play now - a game station with songs, a stream in the lists.</summary>
        internal sealed class Fav
        {
            public string Key = "", Name = "", Url = "";
            public RadioStation Station;
            public bool Game;
        }

        /// <summary>The favourites that can play now, in the order marked (by name with "Alphabetical").</summary>
        internal static List<Fav> ResolvedFavorites(RadioPlayer? rp)
        {
            LoadFavs();
            var list = new List<Fav>();
            if (_favs!.Count == 0) return list;
            if (_fetched || _stations.Count == 0) Load();   // the lists as they are now
            foreach (var key in _favs)
            {
                if (key.StartsWith("game:", StringComparison.Ordinal) && int.TryParse(key.Substring(5), out int gi) && Enum.IsDefined(typeof(RadioStation), gi))
                {
                    var st = (RadioStation)gi;
                    bool ok = false;
                    try { ok = rp != null && rp.GetRadioStationData(st).HasPlayableClips; } catch { }
                    if (ok) list.Add(new Fav { Key = key, Name = GameName(st), Station = st, Game = true });
                }
                else if (key.StartsWith("url:", StringComparison.Ordinal))
                {
                    string url = key.Substring(4);
                    var s = _stations.Find(x => x.Url == url);
                    if (s != null) list.Add(new Fav { Key = key, Name = s.Name, Url = url });
                }
            }
            if (SortAz) list = list.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            return list;
        }

        /// <summary>A game station's name as the Radio app shows it (the game shows the enum name).</summary>
        internal static string GameName(RadioStation st) =>
            st == RadioStation.LocalFiles ? Loc.T("gps_radio_localfiles", "Your own music (Radio folder)") : st.ToString();

        // ── RADIO-ONFOOT-1 ──

        private const string OnFootPref = "BAMP_RadioOnFoot";
        private static int _onFoot = -1;                  // -1 = not read yet
        private static bool _ownMute, _wasInCar;

        /// <summary>The player's switch (this machine only, PlayerPrefs).</summary>
        internal static bool OnFoot
        {
            get { if (_onFoot < 0) { try { _onFoot = UnityEngine.PlayerPrefs.GetInt(OnFootPref, 0); } catch { _onFoot = 0; } } return _onFoot == 1; }
            set
            {
                _onFoot = value ? 1 : 0;
                try { UnityEngine.PlayerPrefs.SetInt(OnFootPref, _onFoot); UnityEngine.PlayerPrefs.Save(); } catch { }
                Plugin.Logger.LogInfo($"[RadioStreams] radio on foot {(value ? "on" : "off")}.");
                if (!value)
                {
                    // back to the game's rule: on foot without headphones the radio is silent
                    var rp = InstanceBehavior<GameManager>.Instance?.radioPlayer;
                    if (rp != null && !rp.IsMuted && !Helpers.PlayerHelper.IsUsingVehicle && !PassengerRide.IsSeated && !Helpers.PlayerHelper.IsWearingHeadset()) SetMute(rp, true);
                }
            }
        }

        /// <summary>RadioPlayer.ToggleMute prefix: with the switch on, the game does not mute the radio outside a building
        /// (getting out of a car, taking the headphones off, the start of a game); inside one it may - the shop's
        /// loudspeakers play there - and the mod's own calls always pass.</summary>
        internal static bool AllowMute(bool isMute)
        {
            if (!isMute || _ownMute || !OnFoot) return true;
            try { return BuildingManager.IsInsideBuilding; } catch { return true; }
        }

        /// <summary>MAIN THREAD, every frame (RadioPhonePage.Tick, single player too): with the switch on, the radio is
        /// silent inside a building (headphones still hear it, as in the game) and plays again outside it.</summary>
        internal static void TickOnFoot()
        {
            if (!OnFoot || MPConfig.DedicatedHost) return;
            var rp = InstanceBehavior<GameManager>.Instance?.radioPlayer;
            if (rp == null) return;
            bool inside = false, car = false, phones = false;
            try { inside = BuildingManager.IsInsideBuilding; car = Helpers.PlayerHelper.IsUsingVehicle; phones = Helpers.PlayerHelper.IsWearingHeadset(); } catch { return; }
            bool leftCar = _wasInCar && !car;
            _wasInCar = car;
            if (car) return;   // the game's own rule in a car
            if (inside)
            {
                if (!phones && !rp.IsMuted) SetMute(rp, true);
                return;
            }
            if (rp.IsMuted) SetMute(rp, false);
            else if (leftCar) ShowControls(rp, true);   // the game hid the radio's controls on getting out
        }

        private static void SetMute(RadioPlayer rp, bool mute)
        {
            _ownMute = true;
            try { rp.ToggleMute(mute); } finally { _ownMute = false; }
            ShowControls(rp, !mute);
        }

        private static void ShowControls(RadioPlayer rp, bool on)
        {
            try
            {
                var rc = InstanceBehavior<UI.UIs>.Instance?.smartphoneUI?.radioControls;
                if (rc != null) { if (on) rc.EnableUI(); else rc.DisableUI(); }
            }
            catch { }
            try { rp.onRadioToggle.Invoke(on); } catch { }
        }

        internal static bool OwnCall => _ownCall;

        /// <summary>The game plays one of its stations itself (a load, a rider's tune-in): the stream ends first.</summary>
        internal static void Interrupt(RadioPlayer rp)
        {
            if (!Active || _ownCall) return;
            StopDownload();
            _url = _name = "";
            _index = -1;
            try { rp.audioSource.Stop(); } catch { }
        }
    }

    [HarmonyPatch(typeof(RadioPlayer), nameof(RadioPlayer.PlayNextStation))]
    public static class Patch_RadioPlayer_PlayNextStation_Streams
    {
        static bool Prefix(RadioPlayer __instance)
        {
            try { return RadioStreams.OnNext(__instance); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[RadioStreams] next: {ex.Message}"); return true; }
        }
    }

    [HarmonyPatch(typeof(RadioPlayer), "Update")]
    public static class Patch_RadioPlayer_Update_Streams
    {
        static bool Prefix(RadioPlayer __instance)
        {
            if (!RadioStreams.Active) return true;
            try { RadioStreams.Tick(__instance); } catch { }
            return false;
        }
    }

    [HarmonyPatch(typeof(RadioPlayer), "PlayStation")]
    public static class Patch_RadioPlayer_PlayStation_Streams
    {
        static void Prefix(RadioPlayer __instance)
        {
            try { RadioStreams.Interrupt(__instance); } catch { }
        }
    }

    [HarmonyPatch(typeof(RadioPlayer), nameof(RadioPlayer.ToggleMute))]
    public static class Patch_RadioPlayer_ToggleMute_OnFoot
    {
        static bool Prefix(bool isMute)
        {
            try { return RadioStreams.AllowMute(isMute); } catch { return true; }
        }
    }

    [HarmonyPatch(typeof(RadioControls), nameof(RadioControls.GetSongLabel))]
    public static class Patch_RadioControls_GetSongLabel_Streams
    {
        static void Postfix(ref string __result)
        {
            if (RadioStreams.Active) __result = RadioStreams.Label();
        }
    }
}
