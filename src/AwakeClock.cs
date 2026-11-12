namespace BigAmbitionsMP
{
    /// <summary>
    /// HOST-FREEZE-1 (2026-11-12, user: "mach"): a clock for the headless watchdogs that does not count the time the whole
    /// process stood still. On 2026-11-11 the machine ran out of memory twice (20:39-21:27 and 21:31-21:57 UTC, the system
    /// log says "oom-kill"): no thread of any server ran, AMP's own scheduler fell 47 minutes behind. When the machine came
    /// back, every server's stall watchdog saw "main loop has not ticked for 300 s" and killed its game - all five at once,
    /// AMP restarted all five at once, the memory ran full again, and after the second kill AMP gave up ("failed to start
    /// 2 times, not restarting"): servers 01, 02, 03 and 0401 stayed down all night, and the first world of server 04 was
    /// lost twice before its first save.
    ///
    /// A watchdog thread calls <see cref="Beat"/> about once a second. A beat that comes more than <see cref="GapMs"/> after
    /// the one before means that thread could not run either - the machine (or the whole process) stood still, the game's
    /// main loop was not stuck by itself - and that time is left out of <see cref="AwakeMs"/>. A stand-still that is still
    /// going on (no beat yet) is left out as well, so it makes no difference which thread runs first afterwards. The value
    /// never goes backwards: during a stand-still it stays where it was after <see cref="GapMs"/>, and the beat that ends it
    /// continues from there. A main loop that hangs while the machine runs is unchanged: the beats keep coming and the
    /// main-loop watch fires after its time as before.
    ///
    /// Unity-free on purpose (the scratchpad test compiles it on its own); every member is thread-safe.
    /// </summary>
    public sealed class AwakeClock
    {
        /// <summary>A beat later than this after the one before = a stand-still.</summary>
        public readonly long GapMs;
        private readonly object _lock = new object();
        private long _lastBeatMs, _frozenMs, _longestMs;
        private int _standstills;

        public AwakeClock(long startMs, long gapMs = 30_000)
        {
            _lastBeatMs = startMs;
            GapMs = gapMs < 1 ? 1 : gapMs;
        }

        /// <summary>A watchdog thread, about once a second. Returns how long the process stood still (ms, from the beat
        /// before) when this beat ends a stand-still, else 0.</summary>
        public long Beat(long nowMs)
        {
            lock (_lock)
            {
                long gap = nowMs - _lastBeatMs;
                if (nowMs > _lastBeatMs) _lastBeatMs = nowMs;
                if (gap <= GapMs) return 0;
                _frozenMs += gap - GapMs;
                _standstills++;
                if (gap > _longestMs) _longestMs = gap;
                return gap;
            }
        }

        /// <summary>Milliseconds since the start in which the process could run: the clock minus the stand-stills, the
        /// one going on included. Any thread.</summary>
        public long AwakeMs(long nowMs)
        {
            lock (_lock)
            {
                long open = nowMs - _lastBeatMs - GapMs;
                return nowMs - _frozenMs - (open > 0 ? open : 0);
            }
        }

        /// <summary>Stand-stills ended so far, and the longest one (ms).</summary>
        public int Standstills { get { lock (_lock) return _standstills; } }
        public long LongestMs { get { lock (_lock) return _longestMs; } }
        /// <summary>Time left out so far (ms), the stand-still going on not included.</summary>
        public long FrozenMs { get { lock (_lock) return _frozenMs; } }
    }
}
