using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>NET-PAGE-1 (1.0.16, user 2026-11-01: "network in and out together, with two colours" - "put the infos on
    /// the ranking page, then AMP's overview is not so full"): AMP draws every metric in a box of its own, one line in
    /// its own colour, so the server's numbers left AMP's status page (AMP-METRICS-1) for the ranking page. Every 15 s
    /// the dedicated server takes a sample - KB/s and messages/s in and out (MPNetStats' totals, every wire send and
    /// receive), the frames it really runs, the players connected and how many of them get a thinner stream for a weak
    /// link (NetAdapt) - keeps the last hour and writes it with each player's link (round trip, loss, the level he
    /// gets) to net.json in the key folder next to ranking.json; ranking.html draws it. After a restart within the
    /// hour the history goes on. Main thread.</summary>
    internal static class ServerStats
    {
        internal const int EverySeconds = 15;
        private const long KeepSeconds = 3600;
        // one sample: unix time, KB/s in, KB/s out, messages/s in, messages/s out, frames/s, players connected, of them throttled
        private const int Fields = 8;
        private static readonly List<double[]> _samples = new List<double[]>();
        private static long _in = -1, _out, _inMsg, _outMsg;
        private static int _frame;
        private static float _at;
        private static bool _loaded, _failedLogged;

        private sealed class NetFile { public List<double[]>? samples; }

        /// <summary>HeadlessHost, every 15 s on the dedicated headless host.</summary>
        internal static void Tick(float now)
        {
            if (HeadlessHost.DirPath.Length == 0) return;
            try
            {
                long i = MPNetStats.InBytesTotal, o = MPNetStats.OutBytesTotal, im = MPNetStats.InMsgsTotal, om = MPNetStats.OutMsgsTotal;
                int f = Time.frameCount;
                double dt = now - _at;
                bool take = _in >= 0 && dt > 0.5;
                long pIn = _in, pOut = _out, pInMsg = _inMsg, pOutMsg = _outMsg;
                int pFrame = _frame;
                _in = i; _out = o; _inMsg = im; _outMsg = om; _frame = f; _at = now;
                if (!take) return;

                string path = PlayerLedger.PublicFile("net.json");
                Load(path);
                long unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                int level = NetRate.Level;
                var players = new List<(string name, int ping, float loss, int level)>();
                if (MPServer.IsRunning)
                    foreach (var (link, pid) in MPServer.ConnectedClientPeers())
                    {
                        if (link == null || string.IsNullOrEmpty(pid)) continue;
                        NetAdapt.Reading(pid, out int ping, out float loss);
                        if (ping < 0) { try { ping = link.PingMs; } catch { } }
                        string name = "";
                        try { name = MPNames.Resolve(pid); } catch { }
                        players.Add((string.IsNullOrWhiteSpace(name) ? "?" : name, ping, loss, NetAdapt.LevelFor(pid)));
                    }
                int throttled = players.Count(p => p.level < level);
                _samples.Add(new double[]
                {
                    unix, Math.Round((i - pIn) / 1024.0 / dt, 1), Math.Round((o - pOut) / 1024.0 / dt, 1),
                    Math.Round((im - pInMsg) / dt), Math.Round((om - pOutMsg) / dt), Math.Round((f - pFrame) / dt),
                    players.Count, throttled,
                });
                _samples.RemoveAll(s => s[0] < unix - KeepSeconds);

                string dir = Path.GetDirectoryName(path) ?? "";
                if (dir.Length > 0 && Directory.Exists(Path.GetDirectoryName(dir) ?? "")) Directory.CreateDirectory(dir);
                if (dir.Length == 0 || !Directory.Exists(dir)) return;
                int target = -1;
                try { target = Application.targetFrameRate; } catch { }
                var pub = new
                {
                    updatedUnix = unix,
                    every = EverySeconds,
                    level,
                    fpsTarget = target > 0 ? target : (int?)null,
                    players = players.OrderBy(p => p.name, StringComparer.OrdinalIgnoreCase).Select(p => new
                    {
                        p.name,
                        ping = p.ping >= 0 ? p.ping : (int?)null,
                        loss = p.loss >= 0f ? Math.Round(p.loss, 1) : (double?)null,
                        p.level,
                    }),
                    samples = _samples,
                };
                PlayerLedger.WriteAtomic(path, JsonConvert.SerializeObject(pub));
            }
            catch (Exception ex)
            {
                if (!_failedLogged) { _failedLogged = true; Plugin.Logger.LogWarning($"[ServerStats] {ex.Message}"); }
            }
        }

        /// <summary>Once: the samples of the last hour from before a restart.</summary>
        private static void Load(string path)
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(path)) return;
                var old = JsonConvert.DeserializeObject<NetFile>(File.ReadAllText(path));
                long cut = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - KeepSeconds;
                if (old?.samples != null) _samples.AddRange(old.samples.Where(s => s != null && s.Length == Fields && s[0] >= cut));
            }
            catch { }
        }
    }
}
