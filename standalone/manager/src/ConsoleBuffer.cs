using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace GpsManager;

internal sealed record ConsoleLine(long Seq, long Time, string Src, string Text);

/// <summary>The web console: the last 3000 lines of the game, the manager and the jobs (SteamCMD, Proton, updates),
/// numbered, so a browser asks only for what it has not seen. Live viewers get every new line through a channel
/// (server-sent events). ANSI colours are cut, Wine's and Unity's noise is dropped the way AMP's template drops it, and
/// so are Wine's debug channels (0024:err:...) and the mod's log copy of every [BAMP-HEADLESS] line (Unity's log goes to
/// the console too, "-logFile -").</summary>
internal static partial class ConsoleBuffer
{
    private const int Max = 3000;
    private static readonly object _lock = new();
    private static readonly Queue<ConsoleLine> _lines = new();
    private static readonly List<Channel<ConsoleLine>> _subs = new();
    private static long _seq;

    [GeneratedRegex(@"\x1B\[[0-9;?]*[A-Za-z]|\x1B[()][0-9A-Za-z]")]
    private static partial Regex Ansi();

    // AMP template: Console.ThrowawayMessageRegex=^(?!.*\[BAMP-HEADLESS\]).*(Shader|wine: |fixme:).*$
    [GeneratedRegex(@"^(?!.*\[BAMP-HEADLESS\]).*(Shader|wine: |fixme:)|^[0-9a-f]{4}:(err|warn|trace|fixme):|^\[Mod:GoingPublicServer\] \[BAMP\] \[Headless\] ")]
    private static partial Regex Noise();

    internal static bool IsNoise(string text) => Noise().IsMatch(text);

    internal static void Add(string src, string text)
    {
        text = Ansi().Replace(text ?? "", "").TrimEnd('\r', '\n');
        if (text.Length == 0) return;
        ConsoleLine line;
        Channel<ConsoleLine>[] subs;
        lock (_lock)
        {
            line = new ConsoleLine(++_seq, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), src, text.Length > 4000 ? text[..4000] + " ..." : text);
            _lines.Enqueue(line);
            while (_lines.Count > Max) _lines.Dequeue();
            subs = _subs.ToArray();
        }
        foreach (var s in subs) s.Writer.TryWrite(line);
    }

    internal static List<ConsoleLine> After(long seq, int max = 1000)
    {
        lock (_lock) return _lines.Where(l => l.Seq > seq).TakeLast(max).ToList();
    }

    internal static Channel<ConsoleLine> Subscribe()
    {
        var ch = Channel.CreateBounded<ConsoleLine>(new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.DropOldest });
        lock (_lock) _subs.Add(ch);
        return ch;
    }

    internal static void Unsubscribe(Channel<ConsoleLine> ch)
    {
        lock (_lock) _subs.Remove(ch);
        ch.Writer.TryComplete();
    }
}
