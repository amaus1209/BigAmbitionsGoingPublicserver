using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Helpers;
using Player.Sound.Radio;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>RADIO-SHOP-1 (user 2026-11-01, the radio mod's "shop loudspeakers"): a building's loudspeakers play an internet
    /// stream too. The game plays a building's station through LoudSpeakersManager - ONE AudioSource whose volume and pan
    /// it works out every frame from the speakers around the listener (UpdateSpatialBlend), with the building's own volume
    /// and on/off - so the stream (RadioDecoder) goes through that same AudioSource and all of that applies to it.
    /// "Next station" on a loudspeaker goes on from the game's last station into the streams (the player's own, then the
    /// server's list) and back to the game's first; the phone's Radio app picks one directly. The choice belongs to the
    /// building and is the same for everybody: on a server it goes to the host, who takes it from whoever runs the building
    /// or holds its Business key, keeps it in the world's save (its manifest, so a loaded older save or a new world has its
    /// own) and replays it to every joiner. Single player keeps it per character in the game's data folder.</summary>
    internal static class ShopRadio
    {
        private static readonly Dictionary<string, (string name, string url)> _choice = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        private static RadioDecoder? _dec;
        private static string _decAddr = "";
        private static volatile List<string[]> _rows = new List<string[]>();   // _choice as rows, for other threads (manifest, join replay)
        private static bool _clipOn;
        private static bool? _loopWas;           // the loudspeakers' AudioSource loop flag before the stream took it (the game never sets it)
        private static string _loadedFor = "";   // single player: the character whose file _choice holds
        private static float _connectedAt, _nextTryAt, _silentSince = -1f, _mutedSince = -1f;
        private static string _problem = "", _problemAt = "";   // RADIO-FORMAT-1: why the stream of building _problemAt does not play
        private static string _formatUrl = "";                   // RADIO-FORMAT-1: the stream that sends a format NLayer cannot play

        private static bool InSession => MPServer.IsRunning || MPClient.IsConnected;

        /// <summary>The address key of the building the player stands in ("" outdoors).</summary>
        internal static string Here()
        {
            try
            {
                if (!BuildingManager.IsInsideBuilding) return "";
                var reg = InstanceBehavior<BuildingManager>.Instance?.buildingRegistration;
                return reg != null ? GameStateReader.AddressKey(reg) : "";
            }
            catch { return ""; }
        }

        /// <summary>This building has loudspeakers the game plays.</summary>
        internal static bool SpeakersHere
        {
            get { try { var m = InstanceBehavior<LoudSpeakersManager>.Instance; return m != null && m._speakersInitialized; } catch { return false; } }
        }

        internal static bool TryChoice(string addr, out string name, out string url)
        {
            name = url = "";
            if (addr.Length == 0 || !_choice.TryGetValue(addr, out var c)) return false;
            name = c.name; url = c.url;
            return true;
        }

        internal static string TitleHere => _dec != null && _decAddr == Here() ? _dec.Title : "";

        /// <summary>RADIO-FORMAT-1: why the stream of the building here does not play ("" = it plays, or nothing to say).</summary>
        internal static string ProblemHere => _problem.Length > 0 && _problemAt == Here() ? _problem : "";

        /// <summary>RADIO-TAB-1: this player may choose the loudspeakers of the building he stands in - he runs it (the game's
        /// own rule for the loudspeaker's panel), works in it with a Business key or a merger, or is a granted guest of the
        /// home (the same keys MPRadioSync's grant-aware getters read).</summary>
        internal static bool CanChooseHere(out string addr)
        {
            addr = Here();
            if (addr.Length == 0 || !SpeakersHere) return false;
            try
            {
                var reg = InstanceBehavior<BuildingManager>.Instance?.buildingRegistration;
                if (reg != null && reg.RentedByPlayer) return true;
            }
            catch { }
            return GrantSync.IsHelperBusiness(addr) || GrantSync.CanEnterGranted(addr);
        }

        /// <summary>The building's loudspeakers are switched off (the panel's on/off: a negative volume).</summary>
        internal static bool MutedHere
        {
            get { try { return InstanceBehavior<BuildingManager>.Instance.buildingRegistration.GetBusinessRadioVolume() < 0f; } catch { return false; } }
        }

        /// <summary>RADIO-TAB-1: what the loudspeakers here play - a stream (url set), or the building's game station.</summary>
        internal static bool NowHere(out RadioStation station, out string name, out string url)
        {
            station = RadioStation.Dance; name = url = "";
            string a = Here();
            if (a.Length == 0) return false;
            if (TryChoice(a, out name, out url)) return true;
            try { station = InstanceBehavior<BuildingManager>.Instance.buildingRegistration.GetBusinessRadioStation(); return true; }
            catch { return false; }
        }

        /// <summary>RADIO-TAB-1: one of the game's stations on the loudspeakers here - the building's own station, as the
        /// loudspeaker's "next station" sets it (MPRadioSync carries it to everybody), and the stream ends.</summary>
        internal static void PickGame(RadioStation st)
        {
            if (!CanChooseHere(out var a)) return;
            var mgr = InstanceBehavior<LoudSpeakersManager>.Instance;
            var reg = InstanceBehavior<BuildingManager>.Instance?.buildingRegistration;
            if (mgr == null || reg == null) return;
            reg.radioStation = st;
            if (TryChoice(a, out _, out _)) Choose(a, "", "", send: true);   // plays the building's station again
            else { try { mgr.PlayStation(st); } catch { } }
            MPRadioSync.SendStation();
        }

        /// <summary>RADIO-TAB-1: a stream on the loudspeakers here.</summary>
        internal static void PickStream(string name, string url)
        {
            if (CanChooseHere(out var a) && RadioStreams.IsStreamUrl(url)) Choose(a, name, url, send: true);
        }

        // ── the choice ──

        /// <summary>MAIN THREAD: set (url "" = back to the game's stations) a building's stream, here and - with send - on
        /// the server.</summary>
        internal static void Choose(string addr, string name, string url, bool send)
        {
            if (addr.Length == 0) return;
            LoadOnce();
            bool had = _choice.ContainsKey(addr);
            if (url.Length == 0) _choice.Remove(addr); else _choice[addr] = (name, url);
            Publish();
            if (url == _formatUrl) _formatUrl = "";   // picked again: one more try
            if (addr == Here())
            {
                var mgr = InstanceBehavior<LoudSpeakersManager>.Instance;
                StopStream(mgr);
                _problem = "";
                if (url.Length == 0 && had && mgr != null)
                {
                    try { mgr.PlayStation(InstanceBehavior<BuildingManager>.Instance.buildingRegistration.GetBusinessRadioStation()); } catch { }
                }
                _nextTryAt = 0f;
            }
            Plugin.Logger.LogInfo($"[ShopRadio] '{addr}': {(url.Length > 0 ? "stream " + name : "the game's stations")}.");
            if (!send) return;
            var p = new ShopRadioPayload { PlayerId = MPConfig.PlayerId, AddressKey = addr, Name = name, Url = url };
            if (MPServer.IsRunning) MPServer.HostShopRadio(p, MPConfig.PlayerId);
            else if (MPClient.IsConnected) MPClient.SendShopRadio(p);
            else SaveFile();   // single player keeps it on this machine, per character
        }

        /// <summary>MAIN THREAD: the host's word for a building (a change, or the join replay).</summary>
        internal static void Receive(ShopRadioPayload? p)
        {
            if (p == null || string.IsNullOrEmpty(p.AddressKey)) return;
            string url = p.Url ?? "";
            if (url.Length > 0 && !RadioStreams.IsStreamUrl(url)) return;
            if (TryChoice(p.AddressKey, out _, out var cur) && cur == url) return;
            Choose(p.AddressKey, p.Name ?? "", url, send: false);
        }

        private static void Publish()
        {
            var rows = new List<string[]>();
            foreach (var kv in _choice) rows.Add(new[] { kv.Key, kv.Value.name, kv.Value.url });
            _rows = rows;
        }

        private static void Fill(List<string[]>? rows)
        {
            _choice.Clear();
            if (rows != null)
                foreach (var r in rows)
                    if (r != null && r.Length >= 3 && !string.IsNullOrEmpty(r[0]) && RadioStreams.IsStreamUrl(r[2] ?? "")) _choice[r[0]] = (r[1] ?? "", r[2]);
            Publish();
        }

        /// <summary>HOST, any thread: every choice, for a joiner.</summary>
        internal static List<ShopRadioPayload> Snapshot()
        {
            var list = new List<ShopRadioPayload>();
            foreach (var r in _rows) list.Add(new ShopRadioPayload { PlayerId = "host", AddressKey = r[0], Name = r[1], Url = r[2] });
            return list;
        }

        /// <summary>HOST, any thread: the table for the manifest the host writes (MPSaveManager.WriteManifest).</summary>
        internal static List<string[]> HostRows() => _rows;

        /// <summary>HOST: the table of the world just loaded (MPServer.RestoreOwnershipFromManifest; null = a manifest from
        /// before the field = none).</summary>
        internal static void HostRestore(List<string[]>? rows)
        {
            GameStatePatcher.EnqueueOnMainThread(() =>
            {
                Fill(rows);
                Plugin.Logger.LogInfo($"[ShopRadio] {_choice.Count} building(s) with a stream in this world.");
            });
        }

        /// <summary>Any thread: a new session or world starts - nothing of the last one's choices plays on (a client then
        /// takes the host's word, which arrives after this in the main-thread queue).</summary>
        internal static void ResetSession()
        {
            GameStatePatcher.EnqueueOnMainThread(() =>
            {
                StopStream(InstanceBehavior<LoudSpeakersManager>.Instance);
                Fill(null);
                _loadedFor = "";
            });
        }

        // single player: one file per character in the game's data folder
        private static string CharacterId()
        {
            try { return SaveGameManager.Current?.characterId ?? ""; } catch { return ""; }
        }

        private static string FilePath(string character)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in character) if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_') sb.Append(ch);
            return Path.Combine(Application.persistentDataPath, "GoingPublicServer", "ShopRadio-" + (sb.Length > 0 ? sb.ToString() : "default") + ".json");
        }

        /// <summary>Single player: the choices kept on this machine.</summary>
        internal static void SaveFile()
        {
            if (InSession) return;   // a server keeps them in the world's manifest
            try
            {
                string path = FilePath(CharacterId());
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(_rows));
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[ShopRadio] save: {ex.Message}"); }
        }

        private static void LoadOnce()
        {
            if (InSession) return;   // the host's table (manifest / its word)
            string who = CharacterId();
            if (who == _loadedFor) return;
            _loadedFor = who;
            try
            {
                string path = FilePath(who);
                Fill(File.Exists(path) ? Newtonsoft.Json.JsonConvert.DeserializeObject<List<string[]>>(File.ReadAllText(path)) : null);
                if (_choice.Count > 0) Plugin.Logger.LogInfo($"[ShopRadio] {_choice.Count} building(s) with a stream (saved).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[ShopRadio] load: {ex.Message}"); }
        }

        // ── playing ──

        /// <summary>LoudSpeakersManager.PlaySong prefix: a building with a stream plays the stream, not the game's song.</summary>
        internal static bool AllowGameSong()
        {
            LoadOnce();
            string a = Here();
            return a.Length == 0 || !_choice.ContainsKey(a);
        }

        /// <summary>LoudSpeakersManager.Stop postfix (leaving the building, the last speaker taken down): the stream goes too.</summary>
        internal static void OnSpeakersStopped(LoudSpeakersManager mgr) => StopStream(mgr);

        /// <summary>MAIN THREAD, every frame in the world: the building's stream on its loudspeakers - paused with the game's
        /// menus, no download while the speakers are off, reconnected when it fails or stalls.</summary>
        internal static void Tick()
        {
            if (MPConfig.DedicatedHost) return;   // nobody listens on a dedicated server
            LoadOnce();
            var mgr = InstanceBehavior<LoudSpeakersManager>.Instance;
            string a = Here();
            if (mgr == null || a.Length == 0 || !SpeakersHere || !_choice.TryGetValue(a, out var c))
            {
                if (_dec != null) StopStream(mgr);
                return;
            }
            float now = Time.unscaledTime;
            if (_dec != null && (_decAddr != a || _dec.Url != c.url)) StopStream(mgr);
            AudioSource src;
            try { src = mgr.audioSource; } catch { return; }
            if (src == null) return;
            if (mgr._isPaused) { if (src.isPlaying) src.Pause(); return; }
            if (src.mute)
            {
                if (_mutedSince < 0f) _mutedSince = now;
                else if (now - _mutedSince > 10f && _dec != null) { StopStream(mgr); _nextTryAt = 0f; }   // on again: at once
                return;
            }
            _mutedSince = -1f;
            if (_dec == null)
            {
                if (c.url == _formatUrl) { _problemAt = a; return; }   // RADIO-FORMAT-1: stays silent, the app says why
                if (now < _nextTryAt) return;
                _nextTryAt = now + 20f;
                if (src.isPlaying) src.Stop();   // the game's song that played before
                _dec = new RadioDecoder(c.url);
                _dec.Start();
                _decAddr = a;
                _connectedAt = now;
                _clipOn = false;
                _silentSince = -1f;
                return;
            }
            var d = _dec;
            if (d.Ended && d.Buffered <= 0.05f)
            {
                if (d.Format.Length > 0)   // RADIO-FORMAT-1: no use trying this stream again (another building's still plays)
                {
                    Plugin.Logger.LogWarning($"[ShopRadio] '{c.name}' sends {d.Format} - only MP3 plays; not tried again.");
                    _problem = Loc.T("gps_radio_problem_format", "{0} sends {1} - only MP3 streams play.", c.name, d.Format);
                    _problemAt = a;
                    _formatUrl = c.url;   // a new pick of it (Choose) tries once more
                    StopStream(mgr);
                    return;
                }
                Plugin.Logger.LogWarning($"[ShopRadio] '{c.name}': {(d.Error.Length > 0 ? d.Error : "the station ended the stream")} - tries again in 20 s.");
                StopStream(mgr);
                return;
            }
            if (!_clipOn)
            {
                var clip = d.ClipWhenReady();
                if (clip == null) { if (now - _connectedAt > 25f) { Plugin.Logger.LogWarning($"[ShopRadio] '{c.name}': no audio within 25 s."); StopStream(mgr); } return; }
                _loopWas ??= src.loop;
                src.clip = clip;
                src.loop = true;   // the clip is a 2 s ring the decoder keeps filling
                src.Play();
                try { mgr._isActive = true; } catch { }
                _clipOn = true;
                _problem = "";
                Plugin.Logger.LogInfo($"[ShopRadio] '{a}' plays '{c.name}' on its loudspeakers ({d.SampleRate} Hz).");
                return;
            }
            if (!src.isPlaying) src.Play();   // after a menu pause
            if (d.Buffered > 0.05f) _silentSince = -1f;
            else if (_silentSince < 0f) _silentSince = now;
            else if (now - _silentSince > 8f) { Plugin.Logger.LogInfo($"[ShopRadio] '{c.name}': no audio for 8 s - reconnecting."); StopStream(mgr); _nextTryAt = 0f; }
        }

        private static void StopStream(LoudSpeakersManager? mgr)
        {
            var d = _dec;
            _dec = null;
            _clipOn = false;
            _decAddr = "";
            if (d == null) return;
            d.Stop();
            try
            {
                if (mgr != null && d.Clip != null && mgr.audioSource.clip == d.Clip) { mgr.audioSource.Stop(); mgr.audioSource.clip = null; }
                if (mgr != null && _loopWas.HasValue) mgr.audioSource.loop = _loopWas.Value;   // the game's songs end again
            }
            catch { }
            _loopWas = null;
            d.DestroyClip();
        }

        /// <summary>LoudSpeakersManager.PlayNextStation (the loudspeaker's "next station") prefix: from the game's last
        /// playable station into the streams, through them, and back to the game's first.</summary>
        internal static bool OnNext()
        {
            var mgr = InstanceBehavior<LoudSpeakersManager>.Instance;
            string a = Here();
            if (mgr == null || a.Length == 0 || !SpeakersHere) return true;
            // RADIO-FAV-2: with favourites, the loudspeaker's "next" goes through them (the player's own, as the Radio app's)
            var favs = RadioStreams.ResolvedFavorites(InstanceBehavior<GameManager>.Instance?.radioPlayer);
            if (favs.Count > 0 && CanChooseHere(out _))
            {
                string playing = TryChoice(a, out _, out var u) ? "url:" + u : "game:" + (int)InstanceBehavior<BuildingManager>.Instance.buildingRegistration.GetBusinessRadioStation();
                var f = favs[(favs.FindIndex(x => x.Key == playing) + 1) % favs.Count];
                if (f.Game) PickGame(f.Station); else PickStream(f.Name, f.Url);
                return false;
            }
            var list = RadioStreams.Load();
            if (TryChoice(a, out _, out var url))
            {
                int i = -1;
                for (int k = 0; k < list.Count; k++) if (list[k].Url == url) { i = k; break; }
                if (i >= 0 && i + 1 < list.Count) { Choose(a, list[i + 1].Name, list[i + 1].Url, send: true); return false; }
                try { InstanceBehavior<BuildingManager>.Instance.buildingRegistration.radioStation = RadioStation.Dance; } catch { }
                Choose(a, "", "", send: true);   // the game's first station (PlayStation skips an empty one itself)
                return false;
            }
            var rp = InstanceBehavior<GameManager>.Instance?.radioPlayer;
            if (rp == null || list.Count == 0) return true;
            var cur = InstanceBehavior<BuildingManager>.Instance.buildingRegistration.GetBusinessRadioStation();
            int count = Enum.GetValues(typeof(RadioStation)).Length;
            var n = (RadioStation)(((int)cur + 1) % count);
            for (int k = 0; k < count && n != cur && !rp.GetRadioStationData(n).HasPlayableClips; k++) n = (RadioStation)(((int)n + 1) % count);
            if ((int)n > (int)cur) return true;   // the game's next station
            Choose(a, list[0].Name, list[0].Url, send: true);
            return false;
        }
    }

    [HarmonyPatch(typeof(LoudSpeakersManager), "PlaySong")]
    public static class Patch_LoudSpeakersManager_PlaySong_Stream
    {
        static bool Prefix()
        {
            try { return ShopRadio.AllowGameSong(); } catch { return true; }
        }
    }

    /// <summary>RADIO-SHOP-1: the loudspeaker's panel names the stream and its song (the game would show its station and
    /// the clip's name).</summary>
    [HarmonyPatch(typeof(LoudSpeakersManager), nameof(LoudSpeakersManager.StationName), MethodType.Getter)]
    public static class Patch_LoudSpeakersManager_StationName_Stream
    {
        static void Postfix(ref string __result)
        {
            try { if (ShopRadio.TryChoice(ShopRadio.Here(), out var name, out _)) __result = name; } catch { }
        }
    }

    [HarmonyPatch(typeof(LoudSpeakersManager), nameof(LoudSpeakersManager.SongName), MethodType.Getter)]
    public static class Patch_LoudSpeakersManager_SongName_Stream
    {
        static void Postfix(ref string __result)
        {
            try
            {
                if (!ShopRadio.TryChoice(ShopRadio.Here(), out _, out _)) return;
                string t = ShopRadio.TitleHere, why = ShopRadio.ProblemHere;
                __result = why.Length > 0 ? why : t.Length > 0 ? t : Loc.T("gps_radio_internet", "Internet radio");
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(LoudSpeakersManager), "Stop")]
    public static class Patch_LoudSpeakersManager_Stop_Stream
    {
        static void Postfix(LoudSpeakersManager __instance)
        {
            try { ShopRadio.OnSpeakersStopped(__instance); } catch { }
        }
    }

    [HarmonyPatch(typeof(LoudSpeakersManager), nameof(LoudSpeakersManager.PlayNextStation), new Type[0])]
    public static class Patch_LoudSpeakersManager_PlayNextStation_Stream
    {
        static bool Prefix()
        {
            try { return ShopRadio.OnNext(); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[ShopRadio] next: {ex.Message}"); return true; }
        }
    }
}
