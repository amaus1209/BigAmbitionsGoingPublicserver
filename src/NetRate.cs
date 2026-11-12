using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>NET-RATE-1 (user 2026-10-31: "make driving smoother - servers mostly have 1 Gbit; offer levels, more
    /// players need more"): how often the moving things go over the network, chosen by the server admin in AMP (NetRate,
    /// ServerRules - the clients get the server's level with the world snapshot, so sending and smoothing match).
    ///   level  own position + own cars   traffic (host snapshots; the clients draw them this far behind)
    ///   0      10 Hz                     5 Hz   (before 1.0.14)
    ///   1      20 Hz                     10 Hz
    ///   2      30 Hz                     15 Hz  (default)
    ///   3      60 Hz                     30 Hz
    /// TrafficSync's playback delay is the traffic interval itself (its two-state buffer needs delay == interval, see
    /// PlaybackDelay there), so a higher level also halves how far behind the cars are drawn. The server relays every
    /// player's stream to every other player: the traffic grows with the square of the players - fewer, faster or more,
    /// slower is the admin's choice.</summary>
    internal static class NetRate
    {
        private static readonly float[] Move = { 0.1f, 0.05f, 1f / 30f, 1f / 60f };
        private static readonly float[] Traffic = { 0.2f, 0.1f, 1f / 15f, 1f / 30f };
        private static int _level = 2, _frame = -1, _logged = -1;

        /// <summary>0..3 - the server's level in a server's world (and on the server), else the default.</summary>
        internal static int Level
        {
            get
            {
                if (_frame == Time.frameCount) return _level;
                _frame = Time.frameCount;
                _level = Mathf.Clamp(ServerRules.NetLevel, 0, 3);
                if (_level != _logged)
                {
                    _logged = _level;
                    Plugin.Logger.LogInfo($"[Net] rate level {_level}: positions and cars {1f / Move[_level]:0} Hz, traffic {1f / Traffic[_level]:0} Hz (NET-RATE-1).");
                }
                return _level;
            }
        }

        /// <summary>Seconds between two sends of the own position and the own driven cars.</summary>
        internal static float MoveInterval => Move[Level];

        /// <summary>Seconds between two traffic snapshots of the host - and how far behind the clients draw them.</summary>
        internal static float TrafficInterval => Traffic[Level];

        internal static float MoveIntervalAt(int level) => Move[Mathf.Clamp(level, 0, 3)];
        internal static float TrafficIntervalAt(int level) => Traffic[Mathf.Clamp(level, 0, 3)];
    }

    /// <summary>NET-ADAPT-1 (1.0.16, user 2026-11-01: "a lower network rate for the player with the weak connection, the
    /// others stay high"): every 3 s the server reads each player's link - round trip and packet loss (LiteNetLib's own
    /// statistics, Steam's connection quality) - and caps what it streams to him: 3 % loss or 200 ms one level lower, 8 %
    /// or 350 ms down to Normal. A cap rises again one level after 30 s of good readings. Only what goes TO that player is
    /// thinned (traffic snapshots, the other players' positions); everybody else keeps the server's level, and the
    /// clients' playback buffers measure the real spacing of what they receive.</summary>
    internal static class NetAdapt
    {
        private sealed class Link { public long Sent, Lost; public int Cap = 3; public float GoodSince = -1f; public float Loss = -1f; public int Ping = -1; }
        private static readonly Dictionary<string, Link> _links = new Dictionary<string, Link>();
        private static readonly ConcurrentDictionary<string, int> _cap = new ConcurrentDictionary<string, int>();   // read by the network thread
        private static readonly ConcurrentDictionary<long, int> _moveCount = new ConcurrentDictionary<long, int>();
        private static volatile int _serverLevel = 2;
        private static float _nextAt;

        /// <summary>Main thread, every frame on the host (TrafficSync's host tick).</summary>
        internal static void Tick()
        {
            if (!MPServer.IsRunning) return;
            _serverLevel = NetRate.Level;
            float now = Time.unscaledTime;
            if (now < _nextAt) return;
            _nextAt = now + 3f;
            var live = new HashSet<string>();
            foreach (var (link, pid) in MPServer.ConnectedClientPeers())
            {
                if (link == null || string.IsNullOrEmpty(pid)) continue;
                live.Add(pid);
                if (!_links.TryGetValue(pid, out var st)) _links[pid] = st = new Link();
                float loss = -1f;
                try
                {
                    if (link.TryGetLoss(out long sent, out long lost, out float direct))
                    {
                        if (direct >= 0f) loss = direct;
                        else if (st.Sent > 0 && sent > st.Sent + 20) loss = Math.Max(0, lost - st.Lost) * 100f / (sent - st.Sent);
                        if (direct < 0f && (st.Sent == 0 || sent > st.Sent + 20)) { st.Sent = sent; st.Lost = lost; }
                    }
                }
                catch { }
                int ping = -1;
                try { ping = link.PingMs; } catch { }
                if (loss >= 0f) st.Loss = loss;
                st.Ping = ping;
                int want = 3;
                if (st.Loss >= 8f || ping >= 350) want = 0;
                else if (st.Loss >= 3f || ping >= 200) want = 1;
                int was = st.Cap;
                if (want < st.Cap) { st.Cap = want; st.GoodSince = -1f; }
                else if (want > st.Cap)
                {
                    if (st.GoodSince < 0f) st.GoodSince = now;
                    else if (now - st.GoodSince >= 30f) { st.Cap++; st.GoodSince = -1f; }
                }
                else st.GoodSince = -1f;
                _cap[pid] = st.Cap;
                if (st.Cap != was)
                    Plugin.Logger.LogInfo($"[NetAdapt] '{pid}': loss {(st.Loss >= 0f ? st.Loss.ToString("0.0") + " %" : "?")}, ping {(ping >= 0 ? ping + " ms" : "?")} - "
                        + $"stream to this player at level {Math.Min(_serverLevel, st.Cap)} (server {_serverLevel}) (NET-ADAPT-1).");
            }
            foreach (var pid in _links.Keys.Where(k => !live.Contains(k)).ToList()) { _links.Remove(pid); _cap.TryRemove(pid, out _); }
        }

        /// <summary>The level the server streams to this player (the server's, lowered for a weak link). Any thread.</summary>
        internal static int LevelFor(string pid) => Math.Min(_serverLevel, _cap.TryGetValue(pid ?? "", out var c) ? c : 3);

        /// <summary>Main thread: this player's last reading - round trip in ms and packet loss in %, -1 = not known yet
        /// (the ranking page's server section, NET-PAGE-1).</summary>
        internal static void Reading(string pid, out int ping, out float loss)
        {
            ping = -1; loss = -1f;
            if (pid != null && _links.TryGetValue(pid, out var st)) { ping = st.Ping; loss = st.Loss; }
        }

        /// <summary>Traffic: send to this player on this tick of the server's traffic beat?</summary>
        internal static bool TrafficDue(string pid, int tick)
        {
            int lv = LevelFor(pid);
            if (lv >= _serverLevel) return true;
            int every = Mathf.Max(1, Mathf.RoundToInt(NetRate.TrafficIntervalAt(lv) / NetRate.TrafficIntervalAt(_serverLevel)));
            return tick % every == 0;
        }

        /// <summary>Positions: relay this move of <paramref name="from"/> to <paramref name="to"/>? Any thread (the relay).</summary>
        internal static bool MoveDue(string to, string from)
        {
            int lv = LevelFor(to), sl = _serverLevel;
            if (lv >= sl) return true;
            int every = Math.Max(1, (int)Math.Round(MoveIntervalAtSafe(lv) / MoveIntervalAtSafe(sl)));
            long key = ((long)(to ?? "").GetHashCode() << 32) ^ (uint)(from ?? "").GetHashCode();
            int n = _moveCount.AddOrUpdate(key, 1, (_, c) => c + 1);
            return n % every == 0;
        }

        private static float MoveIntervalAtSafe(int level) => level <= 0 ? 0.1f : level == 1 ? 0.05f : level == 2 ? 1f / 30f : 1f / 60f;   // no Unity call: network thread
    }
}
