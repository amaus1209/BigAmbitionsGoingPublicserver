using System.IO;
using Newtonsoft.Json;

namespace BigAmbitionsMP
{
    /// <summary>GO-STREET-2 (user 2026-11-11: "Für alle Spieler sichtbar - Verteilerkästen mit Firmenlogo und Name"): every
    /// player's cable network shows in the street on every machine - the server's part (goingonline/core/Street.cs draws).
    /// - Each machine sends its own network (company, livery colour, buildings and what happens there: connected, being
    ///   connected, somebody at work) when it changes, at most every 10 s and again every 5 minutes (ProviderStreet).
    /// - The host keeps the latest of every player, also of players who are away (go-street.json across restarts), passes
    ///   each change to everybody and gives a joiner all of them with the join replay.
    /// - Each machine keeps the others' networks for Street.Remote; the logos come with the van liveries.
    /// Host: main thread; messages arrive on the network thread and are queued.</summary>
    internal static class GoingOnlineStreet
    {
        const int MaxSites = 3000;

        // ── every machine: the others' networks ───────────────────────────────────────────────────────────────────
        static readonly object _lock = new object();
        static readonly Dictionary<string, GoingOnline.StreetNet> _nets = new Dictionary<string, GoingOnline.StreetNet>();

        internal static List<GoingOnline.StreetNet> Nets()
        {
            lock (_lock) return GoingOnlineLines.InSession ? new List<GoingOnline.StreetNet>(_nets.Values) : new List<GoingOnline.StreetNet>();
        }

        /// <summary>A network arrived (any thread): kept, or dropped when the provider is gone.</summary>
        internal static void Apply(ProviderStreetPayload? p)
        {
            if (p == null || string.IsNullOrEmpty(p.PlayerId)) return;
            var n = new GoingOnline.StreetNet { Owner = p.PlayerId, Company = p.Company ?? "", Color = p.Color & 0xFFFFFF };
            foreach (var e in p.Sites ?? new List<string>())
            {
                int bar = e == null ? -1 : e.LastIndexOf('|');
                if (bar <= 0) continue;
                byte st = byte.TryParse(e!.Substring(bar + 1), out var b) && b <= 2 ? b : (byte)0;
                n.Sites[e.Substring(0, bar)] = st;
            }
            lock (_lock)
            {
                if (n.Company.Length == 0 || n.Sites.Count == 0) _nets.Remove(n.Owner);
                else _nets[n.Owner] = n;
            }
        }

        internal static void ResetSession() { lock (_lock) _nets.Clear(); _sentSig = ""; }

        // ── every machine: its own network to the others ──────────────────────────────────────────────────────────
        static string _sentSig = "";
        static float _nextSend, _nextForce;
        static bool _wasOnline;

        /// <summary>MAIN THREAD, every frame (GoingOnlineModule.Tick).</summary>
        internal static void ClientTick()
        {
            bool client = MPClient.IsConnected, host = MPServer.IsRunning;
            if (!client && !host) { if (_wasOnline) { _wasOnline = false; ResetSession(); } return; }
            if (!_wasOnline) { _wasOnline = true; _sentSig = ""; }
            float now = UnityEngine.Time.unscaledTime;
            if (now < _nextSend) return;
            _nextSend = now + 10f;
            var s = ServerRules.Module("ModuleGoingOnline") ? GoingOnline.Core.S : null;
            var net = GoingOnline.Street.Local(s);
            var p = new ProviderStreetPayload { PlayerId = MPConfig.PlayerId, Company = net.Company, Color = net.Color };
            foreach (var kv in net.Sites)
            {
                if (p.Sites.Count >= MaxSites) break;
                p.Sites.Add(kv.Key + "|" + kv.Value);
            }
            p.Sites.Sort(StringComparer.Ordinal);
            string sig = JsonConvert.SerializeObject(p);
            if (sig == _sentSig && now < _nextForce) return;
            if (_sentSig.Length == 0 && p.Sites.Count == 0 && p.Company.Length == 0) { _sentSig = sig; _nextForce = now + 300f; return; }   // nothing to say yet
            _sentSig = sig;
            _nextForce = now + 300f;
            if (host) HostStore(MPConfig.PlayerId, p);
            else MPClient.SendProviderStreet(p);
        }

        // ── the host ──────────────────────────────────────────────────────────────────────────────────────────────
        static readonly Dictionary<string, ProviderStreetPayload> _store = new Dictionary<string, ProviderStreetPayload>();   // under _lock
        static bool _loaded, _dirty;
        static float _nextSave;

        static string FilePath
        {
            get
            {
                string dir = HeadlessHost.DirPath;
                return Path.Combine(string.IsNullOrEmpty(dir) ? MPConfig.ModRootPath : dir, "go-street.json");
            }
        }

        /// <summary>HOST, MAIN THREAD: a player's network - cleaned, kept, passed on to everybody.</summary>
        internal static void HostStreet(ProviderStreetPayload p, string senderPid)
        {
            if (p == null || string.IsNullOrEmpty(senderPid) || p.PlayerId != senderPid) return;
            HostStore(senderPid, p);
        }

        static void HostStore(string pid, ProviderStreetPayload p)
        {
            if (!_loaded) Load();
            string company = (p.Company ?? "").Trim();
            var clean = new ProviderStreetPayload { PlayerId = pid, Company = company.Length > 40 ? company.Substring(0, 40) : company, Color = p.Color & 0xFFFFFF };
            foreach (var e in p.Sites ?? new List<string>())
                if (clean.Sites.Count < MaxSites && !string.IsNullOrEmpty(e) && e.Length <= 90) clean.Sites.Add(e);
            lock (_lock)
            {
                if (clean.Company.Length == 0 || clean.Sites.Count == 0) _store.Remove(pid);
                else _store[pid] = clean;
            }
            _dirty = true;
            Apply(clean);   // a playing host's own list
            MPServer.BroadcastHub(MessageType.ProviderStreet, clean);
        }

        /// <summary>The join replay: every network the host knows.</summary>
        internal static List<ProviderStreetPayload> Snapshot()
        {
            if (!_loaded) Load();
            lock (_lock) return new List<ProviderStreetPayload>(_store.Values);
        }

        /// <summary>HOST, MAIN THREAD, every frame: keeps the file up to date (at most every 30 s).</summary>
        internal static void HostTick()
        {
            if (!MPServer.IsRunning) { if (_loaded) { _loaded = false; lock (_lock) _store.Clear(); } return; }
            if (!_loaded) Load();
            float now = UnityEngine.Time.unscaledTime;
            if (_dirty && now >= _nextSave) { _nextSave = now + 30f; Save(); }
        }

        static void Load()
        {
            _loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                var list = JsonConvert.DeserializeObject<List<ProviderStreetPayload>>(File.ReadAllText(FilePath));
                if (list == null) return;
                lock (_lock)
                    foreach (var p in list)
                        if (p != null && !string.IsNullOrEmpty(p.PlayerId) && !_store.ContainsKey(p.PlayerId)) _store[p.PlayerId] = p;
                foreach (var p in list) Apply(p);
                Plugin.Logger.LogInfo($"[GoingOnline] street: {_store.Count} networks kept (GO-STREET-2)");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[GoingOnline] street file: {ex.Message} (GO-STREET-2)"); }
        }

        static void Save()
        {
            _dirty = false;
            try
            {
                List<ProviderStreetPayload> list;
                lock (_lock) list = new List<ProviderStreetPayload>(_store.Values);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(list));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[GoingOnline] street file: {ex.Message} (GO-STREET-2)"); }
        }
    }
}
