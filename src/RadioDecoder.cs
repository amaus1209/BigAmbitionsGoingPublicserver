using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>RADIO-STREAMS-3 (user 2026-11-01: "the streams don't play, whatever the station, in the car too"): Unity
    /// cannot play an endless MP3 stream on the desktop - its streamed DownloadHandlerAudioClip gives no clip for MPEG
    /// there, only for a finished download - so every MP3 station stayed silent. The stream is our own now:
    ///   * a background thread opens it with a plain socket (http, and https through SslStream) - so it needs neither
    ///     UnityWebRequest (whose insecureHttpOption forbids http) nor HttpWebRequest (which refuses the "ICY 200 OK"
    ///     answer of older Shoutcast servers), follows redirects and .m3u / .pls playlists;
    ///   * asks for the ICY metadata and takes the "StreamTitle" out of the stream (the song that plays);
    ///   * decodes the MP3 frames with NLayer (MIT, Dependencies/NLayer.dll) into a ring of 8 seconds;
    ///   * a streamed AudioClip reads that ring on Unity's audio thread (PCMReaderCallback), silence while it waits.
    /// The clip plays through an AudioSource of the game (the radio's, a shop's loudspeakers), so its volume, mute,
    /// mixer and pause stay the game's.
    /// RADIO-FORMAT-1 (user 2026-11-01): a station that sends AAC, Opus, OGG, FLAC or HLS (.m3u8) - or a web page instead of
    /// a stream - stops at once with Format set (its name for the player), instead of a silent connection.</summary>
    internal sealed class RadioDecoder
    {
        internal readonly string Url;
        internal volatile string Title = "";
        internal volatile string Error = "";
        internal volatile bool Ended;
        internal volatile string Format = "";   // RADIO-FORMAT-1: what the station sends when it is no MP3 ("" = MP3, or not known)
        internal volatile int SampleRate, Channels;   // known once the first frame is decoded

        private readonly object _lock = new object();
        private float[] _ring = new float[0];
        private int _head, _count;
        private volatile bool _stop;
        private Thread? _thread;
        private Stream? _net;
        private TcpClient? _tcp;
        private AudioClip? _clip;

        internal RadioDecoder(string url) { Url = url; }

        internal AudioClip? Clip => _clip;

        internal void Start()
        {
            _thread = new Thread(Run) { IsBackground = true, Name = "BAMP radio stream" };
            _thread.Start();
        }

        /// <summary>Any thread: ends the download (a pending read is unblocked by closing the socket).</summary>
        internal void Stop()
        {
            _stop = true;
            try { _net?.Dispose(); } catch { }
            try { _tcp?.Close(); } catch { }
        }

        /// <summary>MAIN THREAD: the streamed clip once enough audio waits (null before). Destroy() it with the decoder.</summary>
        internal AudioClip? ClipWhenReady(float prebufferSeconds = 1.5f)
        {
            if (_clip != null) return _clip;
            int sr = SampleRate, ch = Channels;
            if (sr <= 0 || ch <= 0 || Buffered < prebufferSeconds) return null;
            _clip = AudioClip.Create("BAMP_RadioStream", sr * 2, ch, sr, true, Read);
            return _clip;
        }

        internal void DestroyClip()
        {
            if (_clip != null) { try { UnityEngine.Object.Destroy(_clip); } catch { } _clip = null; }
        }

        /// <summary>Seconds of decoded audio waiting in the ring.</summary>
        internal float Buffered
        {
            get
            {
                int sr = SampleRate, ch = Channels;
                if (sr <= 0 || ch <= 0) return 0f;
                lock (_lock) return _count / (float)(sr * ch);
            }
        }

        /// <summary>Unity's audio thread: the next samples, silence on an underrun.</summary>
        private void Read(float[] data)
        {
            lock (_lock)
            {
                int n = Math.Min(data.Length, _count), len = _ring.Length;
                for (int i = 0; i < n; i++) { data[i] = _ring[_head]; _head++; if (_head == len) _head = 0; }
                _count -= n;
                for (int i = n; i < data.Length; i++) data[i] = 0f;
            }
        }

        /// <summary>A full ring (nobody listened for 8 s: a paused game, the loudspeakers paused under a menu) drops its
        /// oldest audio - a radio does not wait, and a socket left unread would be closed by the station.</summary>
        private void Write(float[] buf, int n)
        {
            lock (_lock)
            {
                int len = _ring.Length;
                if (len == 0) return;
                for (int k = 0; k < n; k++)
                {
                    if (_count == len) { _head++; if (_head == len) _head = 0; _count--; }
                    int tail = _head + _count;
                    if (tail >= len) tail -= len;
                    _ring[tail] = buf[k];
                    _count++;
                }
            }
        }

        private void Run()
        {
            try
            {
                string url = Url;
                for (int hop = 0; hop < 3 && IsPlaylist(url); hop++) url = FirstStreamOf(url);
                if (url.ToLowerInvariant().Split('?')[0].EndsWith(".m3u8")) throw new NotSupportedException("format:HLS");
                // NLayer reads Position even on a forward-only source (MpegStreamReader's read buffer) - a socket has none,
                // so the wrapper counts what it handed on (and takes the ICY titles out when the station sends them)
                var audio = new AudioStream(Open(url, out int metaInt), metaInt, t => Title = t);
                using (var mp3 = new NLayer.MpegFile(audio))
                {
                    int sr = mp3.SampleRate, ch = Math.Max(1, mp3.Channels);
                    lock (_lock) { _ring = new float[sr * ch * 8]; _head = 0; _count = 0; }
                    Channels = ch;
                    SampleRate = sr;
                    var buf = new float[4608];
                    while (!_stop)
                    {
                        int n = mp3.ReadSamples(buf, 0, buf.Length);
                        if (n <= 0) break;   // the station ended the stream
                        Write(buf, n);
                    }
                }
            }
            catch (NotSupportedException ex) when (ex.Message.StartsWith("format:", StringComparison.Ordinal)) { Format = ex.Message.Substring(7); Error = "format " + Format; }
            catch (Exception ex) { if (!_stop) Error = ex.GetType().Name + ": " + ex.Message; }
            finally { Ended = true; Stop(); }
        }

        private static bool IsPlaylist(string url)
        {
            string low = url.ToLowerInvariant().Split('?')[0];
            return low.EndsWith(".m3u") || low.EndsWith(".pls");   // .m3u8 is HLS (segments) - not a radio stream we can play
        }

        /// <summary>The first stream named in an .m3u / .pls playlist.</summary>
        private string FirstStreamOf(string url)
        {
            var s = Open(url, out _);
            string text;
            using (var r = new StreamReader(s, Encoding.UTF8)) text = ReadCapped(r, 65536);
            foreach (var raw in text.Split('\n'))
            {
                string l = raw.Trim();
                int eq = l.IndexOf('=');
                if (eq > 0 && l.StartsWith("File", StringComparison.OrdinalIgnoreCase)) l = l.Substring(eq + 1).Trim();
                if (l.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || l.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return l;
            }
            throw new IOException("playlist without a stream");
        }

        private static string ReadCapped(StreamReader r, int max)
        {
            var sb = new StringBuilder();
            var buf = new char[4096];
            int n;
            while (sb.Length < max && (n = r.Read(buf, 0, buf.Length)) > 0) sb.Append(buf, 0, n);
            return sb.ToString();
        }

        /// <summary>A GET over a plain socket (TLS for https) that accepts both "HTTP/1.x 200" and Shoutcast's "ICY 200",
        /// follows up to 5 redirects and leaves the stream at the first body byte.</summary>
        private Stream Open(string url, out int metaInt)
        {
            metaInt = 0;
            for (int hop = 0; hop < 5; hop++)
            {
                if (_stop) throw new OperationCanceledException();
                var uri = new Uri(url);
                var tcp = new TcpClient { ReceiveTimeout = 20000, SendTimeout = 15000 };
                var ar = tcp.BeginConnect(uri.Host, uri.Port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(10000)) { tcp.Close(); throw new TimeoutException("no connection to " + uri.Host); }
                tcp.EndConnect(ar);
                Stream s = tcp.GetStream();
                if (uri.Scheme == Uri.UriSchemeHttps)
                {
                    // A radio stream is public audio: a certificate chain the machine cannot build (Wine's store under Proton
                    // lacks roots) is accepted, a wrong host name is not.
                    var ssl = new SslStream(s, false, (o, cert, chain, errors) => (errors & SslPolicyErrors.RemoteCertificateNameMismatch) == 0
                                                                                    && (errors & SslPolicyErrors.RemoteCertificateNotAvailable) == 0);
                    ssl.AuthenticateAsClient(uri.Host, null, SslProtocols.Tls12, false);
                    s = ssl;
                }
                string head = $"GET {uri.PathAndQuery} HTTP/1.0\r\nHost: {uri.Host}\r\nUser-Agent: GoingPublicServer-Radio/1.0\r\n"
                            + "Accept: */*\r\nIcy-MetaData: 1\r\nConnection: close\r\n\r\n";
                var bytes = Encoding.ASCII.GetBytes(head);
                s.Write(bytes, 0, bytes.Length);
                s.Flush();
                string status = ReadLine(s);
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (string l = ReadLine(s); l.Length > 0; l = ReadLine(s))
                {
                    int c = l.IndexOf(':');
                    if (c > 0) headers[l.Substring(0, c).Trim()] = l.Substring(c + 1).Trim();
                }
                var m = Regex.Match(status, @"^(?:HTTP/\d(?:\.\d)?|ICY)\s+(\d{3})");
                int code = m.Success ? int.Parse(m.Groups[1].Value) : 0;
                if (code >= 300 && code < 400 && headers.TryGetValue("Location", out var loc) && loc.Length > 0)
                {
                    try { s.Dispose(); } catch { }
                    tcp.Close();
                    url = new Uri(uri, loc).ToString();
                    continue;
                }
                if (code != 200) { try { s.Dispose(); } catch { } tcp.Close(); throw new IOException(status.Length > 0 ? status : "no answer"); }
                if (headers.TryGetValue("Content-Type", out var ctype) && FormatOf(ctype) is string fmt)
                {
                    try { s.Dispose(); } catch { }
                    tcp.Close();
                    throw new NotSupportedException("format:" + fmt);
                }
                if (headers.TryGetValue("icy-metaint", out var mi)) int.TryParse(mi, out metaInt);
                _tcp = tcp;
                _net = s;
                return s;
            }
            throw new IOException("too many redirects");
        }

        /// <summary>RADIO-FORMAT-1: the name of a format NLayer cannot play, from the answer's Content-Type; null = MP3 (or unknown,
        /// which is tried).</summary>
        private static string? FormatOf(string contentType)
        {
            string t = contentType.ToLowerInvariant();
            if (t.Contains("mpegurl")) return "HLS";
            if (t.Contains("aacp")) return "AAC+";
            if (t.Contains("aac") || t.Contains("mp4")) return "AAC";
            if (t.Contains("opus")) return "Opus";
            if (t.Contains("ogg")) return "OGG";
            if (t.Contains("flac")) return "FLAC";
            if (t.Contains("text/html")) return "HTML";
            return null;
        }

        private static string ReadLine(Stream s)
        {
            var sb = new StringBuilder();
            while (sb.Length < 8192)
            {
                int b = s.ReadByte();
                if (b < 0) break;
                if (b == '\n') break;
                if (b != '\r') sb.Append((char)b);
            }
            return sb.ToString();
        }

        /// <summary>The audio of the stream, forward only, with the bytes handed on as its Position. For an ICY stream (metaInt
        /// above 0) every metaInt bytes the server weaves in one metadata block (a length byte x 16, then
        /// "StreamTitle='...';"), which is taken out here and handed on as the title.</summary>
        private sealed class AudioStream : Stream
        {
            private readonly Stream _src;
            private readonly int _metaInt;
            private readonly Action<string> _onTitle;
            private int _left;
            private long _pos;

            internal AudioStream(Stream src, int metaInt, Action<string> onTitle) { _src = src; _metaInt = metaInt; _left = metaInt; _onTitle = onTitle; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_metaInt > 0)
                {
                    if (_left == 0) { ReadMeta(); _left = _metaInt; }
                    count = Math.Min(count, _left);
                }
                int n = _src.Read(buffer, offset, count);
                if (n > 0) { if (_metaInt > 0) _left -= n; _pos += n; }
                return n;
            }

            private void ReadMeta()
            {
                int len = _src.ReadByte();
                if (len <= 0) return;   // -1: the end; 0: no new title
                var meta = new byte[len * 16];
                int got = 0;
                while (got < meta.Length) { int r = _src.Read(meta, got, meta.Length - got); if (r <= 0) return; got += r; }
                string text = Encoding.UTF8.GetString(meta);
                if (text.IndexOf('�') >= 0) text = Encoding.GetEncoding(28591).GetString(meta);   // not UTF-8: Latin-1
                var m = Regex.Match(text, @"StreamTitle='(.*?)';");
                if (m.Success) _onTitle(m.Groups[1].Value.Trim());
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => _pos; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing) { if (disposing) { try { _src.Dispose(); } catch { } } base.Dispose(disposing); }
        }
    }
}
