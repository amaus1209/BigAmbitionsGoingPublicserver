using System.Collections.Concurrent;
using System.IO;
using Newtonsoft.Json;

namespace BigAmbitionsMP
{
    /// <summary>GO-WHOLESALE-2 (user 2026-11-11, "Leitungen an Mitspieler"): Going Online's lines between players - the
    /// server's part (goingonline/core/Peers.cs is the provider's).
    /// - Offers: a provider report carries the network on offer (GoingOnlineModule.Report); the host keeps them with the
    ///   ranking's records and sends all of them round (ProviderOffers) at a change, after a join and every minute. The
    ///   offers of players who are away stay on offer (kept in go-linebills.json across restarts).
    /// - Bills: the renter's machine pays its day itself and sends the bill (LineBill); the host keeps it until the
    ///   network's owner is online, sends it again every 20 s until the owner's machine answers that it booked it (Ack),
    ///   and only then forgets it - so no payment is lost to a player who is away or a dropped message. The owner's
    ///   machine books each renter's day once (Peers.Receive).
    /// Host: main thread (HostTick); messages arrive on the network thread and are queued.</summary>
    internal static class GoingOnlineLines
    {
        // ── every machine: the latest offers ──────────────────────────────────────────────────────────────────────
        static readonly object _lock = new object();
        static List<GoingOnline.PeerOffer> _offers = new List<GoingOnline.PeerOffer>();

        internal static bool InSession => MPClient.IsConnected || MPServer.IsRunning;

        internal static List<GoingOnline.PeerOffer> Offers()
        {
            lock (_lock) return InSession ? new List<GoingOnline.PeerOffer>(_offers) : new List<GoingOnline.PeerOffer>();
        }

        /// <summary>The host's list arrived (any thread).</summary>
        internal static void ApplyOffers(ProviderOffersPayload? p)
        {
            var list = new List<GoingOnline.PeerOffer>();
            foreach (var o in p?.Offers ?? new List<LineOffer>())
            {
                if (o == null || string.IsNullOrEmpty(o.Owner)) continue;
                var po = new GoingOnline.PeerOffer { Owner = o.Owner, Company = o.Company ?? "", Price = o.Price };
                foreach (var h in o.Hoods ?? new List<LineOfferHood>())
                    if (h != null && !string.IsNullOrEmpty(h.Id))
                        po.Hoods.Add(new GoingOnline.PeerHood { Id = h.Id, Name = h.Name ?? h.Id, Homes = h.Homes, Out = h.Out, Cong = h.Cong });
                list.Add(po);
            }
            lock (_lock) _offers = list;
        }

        /// <summary>The provider's day: its bill for one owner (main thread).</summary>
        internal static void SendBill(GoingOnline.PeerBill b)
        {
            var p = new LineBillPayload
            {
                Id = b.Renter + "|" + b.Owner + "|" + b.Day, Owner = b.Owner, Renter = b.Renter, Company = b.Company,
                Day = b.Day, Lines = new Dictionary<string, int>(b.Lines), Amount = b.Amount,
            };
            if (MPServer.IsRunning) HostReceive(MPConfig.PlayerId, p);
            else MPClient.SendLineBill(p);
        }

        /// <summary>A bill for this player's network arrived (main thread): book it, then tell the host.</summary>
        internal static void ClientReceive(LineBillPayload p)
        {
            if (p == null || p.Ack || p.Owner != MPConfig.PlayerId) return;
            bool done = GoingOnline.Peers.Receive(GoingOnline.Core.S, new GoingOnline.PeerBill
            {
                Owner = p.Owner, Renter = p.Renter, Company = p.Company ?? "", Day = p.Day,
                Lines = p.Lines ?? new Dictionary<string, int>(), Amount = p.Amount,
            });
            if (!done) return;   // no provider loaded yet: the host sends it again
            var ack = new LineBillPayload { Id = p.Id, Owner = MPConfig.PlayerId, Ack = true };
            if (MPServer.IsRunning) HostReceive(MPConfig.PlayerId, ack);
            else MPClient.SendLineBill(ack);
        }

        // ── the host ──────────────────────────────────────────────────────────────────────────────────────────────
        sealed class FileState
        {
            public List<LineBillPayload> Bills { get; set; } = new List<LineBillPayload>();
            public List<LineOffer> Offers { get; set; } = new List<LineOffer>();
        }

        static readonly ConcurrentQueue<(string sender, LineBillPayload p)> _inbox = new ConcurrentQueue<(string, LineBillPayload)>();
        static readonly List<LineBillPayload> _pending = new List<LineBillPayload>();
        static readonly Dictionary<string, LineOffer> _kept = new Dictionary<string, LineOffer>();   // offers of players away
        static bool _loaded, _dirty, _kick = true;
        static float _nextDeliver, _nextOffers, _nextForce;
        static string _offersSig = "";
        static ProviderOffersPayload? _current;

        /// <summary>The join replay: the offers as last sent round (null before the first).</summary>
        internal static ProviderOffersPayload? Current() => _current;

        /// <summary>Network thread: a renter's bill or an owner's receipt - the main thread handles it.</summary>
        internal static void HostReceive(string senderPid, LineBillPayload p)
        {
            if (p != null && !string.IsNullOrEmpty(senderPid)) _inbox.Enqueue((senderPid, p));
        }

        /// <summary>Somebody joined: send the offers round soon.</summary>
        internal static void NoteJoin() => _kick = true;

        static string FilePath
        {
            get
            {
                string dir = HeadlessHost.DirPath;
                return Path.Combine(string.IsNullOrEmpty(dir) ? MPConfig.ModRootPath : dir, "go-linebills.json");
            }
        }

        /// <summary>MAIN THREAD, every frame on a host (GoingOnlineModule.Tick).</summary>
        internal static void HostTick()
        {
            if (!MPServer.IsRunning) { if (_loaded) { _loaded = false; _pending.Clear(); _kept.Clear(); _offersSig = ""; } return; }
            try
            {
                if (!_loaded) Load();
                while (_inbox.TryDequeue(out var it)) Handle(it.sender, it.p);
                float now = UnityEngine.Time.unscaledTime;
                if (_kick || now >= _nextOffers)
                {
                    bool force = _kick || now >= _nextForce;
                    if (force) _nextForce = now + 60f;
                    _kick = false;
                    _nextOffers = now + 10f;
                    Broadcast(force);
                }
                if (now >= _nextDeliver) { _nextDeliver = now + 20f; Deliver(); }
                if (_dirty) Save();
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[GoingOnline] lines: {ex.Message} (GO-WHOLESALE-2)"); _nextDeliver = UnityEngine.Time.unscaledTime + 60f; }
        }

        static void Handle(string sender, LineBillPayload p)
        {
            if (p.Ack)
            {
                int n = _pending.RemoveAll(x => x.Id == p.Id && x.Owner == sender);
                if (n > 0) _dirty = true;
                return;
            }
            if (p.Renter != sender || string.IsNullOrEmpty(p.Owner) || p.Owner == sender) return;   // only the renter bills, never itself
            if (_pending.Exists(x => x.Id == p.Id)) return;
            p.Amount = float.IsNaN(p.Amount) ? 0f : Math.Max(0f, Math.Min(1_000_000f, p.Amount));
            var lines = new Dictionary<string, int>();
            foreach (var kv in p.Lines ?? new Dictionary<string, int>())
                if (lines.Count < 20 && !string.IsNullOrEmpty(kv.Key) && kv.Key.Length <= 64) lines[kv.Key] = Math.Max(0, Math.Min(1_000_000, kv.Value));
            p.Lines = lines;
            p.Company = (p.Company ?? "").Length > 40 ? p.Company!.Substring(0, 40) : p.Company ?? "";
            _pending.Add(p);
            if (_pending.Count > 5000) _pending.RemoveAt(0);
            _dirty = true;
            Plugin.Logger.LogInfo($"[GoingOnline] lines: a bill of {p.Amount:F2} for day {p.Day} ({_pending.Count} waiting) (GO-WHOLESALE-2)");
            _nextDeliver = 0f;
        }

        /// <summary>Every waiting bill to its owner when that player is here (the host's own at once).</summary>
        static void Deliver()
        {
            foreach (var p in _pending.ToArray())
            {
                if (p.Owner == MPConfig.PlayerId && !MPConfig.DedicatedHost) ClientReceive(p);
                else if (MPServer.IsPlayerConnected(p.Owner)) MPServer.SendHubTo(p.Owner, MessageType.LineBill, p);
            }
        }

        static void Broadcast(bool force)
        {
            var list = new List<LineOffer>();
            var here = new HashSet<string>();
            foreach (var r in MPServer.ProviderByStableId.Values)
            {
                if (r == null || string.IsNullOrEmpty(r.PlayerId)) continue;
                here.Add(r.PlayerId);
                if (r.OfferHoods == null || r.OfferHoods.Count == 0 || string.IsNullOrEmpty(r.Company)) { if (_kept.Remove(r.PlayerId)) _dirty = true; continue; }
                var o = new LineOffer { Owner = r.PlayerId, Company = r.Company, Price = r.OfferPrice, Hoods = r.OfferHoods };
                list.Add(o);
                string was = _kept.TryGetValue(r.PlayerId, out var k) ? JsonConvert.SerializeObject(k) : "";
                if (was != JsonConvert.SerializeObject(o)) { _kept[r.PlayerId] = o; _dirty = true; }
            }
            foreach (var kv in _kept) if (!here.Contains(kv.Key)) list.Add(kv.Value);   // away since the last restart
            list.Sort((a, b) => string.CompareOrdinal(a.Owner, b.Owner));
            string sig = JsonConvert.SerializeObject(list);
            if (!force && sig == _offersSig) return;
            _offersSig = sig;
            var payload = new ProviderOffersPayload { Offers = list };
            _current = payload;
            ApplyOffers(payload);   // a playing host's own list
            MPServer.BroadcastHub(MessageType.ProviderOffers, payload);
        }

        static void Load()
        {
            _loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                var st = JsonConvert.DeserializeObject<FileState>(File.ReadAllText(FilePath));
                if (st == null) return;
                _pending.Clear();
                _pending.AddRange(st.Bills ?? new List<LineBillPayload>());
                _kept.Clear();
                foreach (var o in st.Offers ?? new List<LineOffer>()) if (o != null && !string.IsNullOrEmpty(o.Owner)) _kept[o.Owner] = o;
                Plugin.Logger.LogInfo($"[GoingOnline] lines: {_pending.Count} bills waiting, {_kept.Count} offers kept (GO-WHOLESALE-2)");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[GoingOnline] lines file: {ex.Message} (GO-WHOLESALE-2)"); }
        }

        static void Save()
        {
            _dirty = false;
            try
            {
                var st = new FileState { Bills = new List<LineBillPayload>(_pending), Offers = new List<LineOffer>(_kept.Values) };
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(st));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[GoingOnline] lines file: {ex.Message} (GO-WHOLESALE-2)"); }
        }
    }
}
