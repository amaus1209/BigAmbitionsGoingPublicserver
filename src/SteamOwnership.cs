using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Steamworks;

namespace BigAmbitionsMP
{
    /// <summary>STEAM-OWNER-1 (user 2026-11-02: "we still need a check whether the Steam user really owns the game or only
    /// emulates Steam"): a dedicated server with AMP "SteamOwnerCheck" on and a Steam Web API key ("SteamWebApiKey") lets in
    /// only players whose Steam account owns Big Ambitions.
    ///   * client: when a connection starts, it asks Steam for a ticket (GetAuthTicketForWebApi) and sends it in the Hello;
    ///     once the world arrived, or the connection ended, the ticket is cancelled and opens nothing any more. Its identity
    ///     is bound to the key the player joins with (IdentityFor) - a ticket one server saw is no good on another.
    ///   * server: asks Steam (ISteamUserAuth/AuthenticateUserTicket - the game-server path on api.steampowered.com, which
    ///     takes a normal Web API key) whether the ticket is genuine, for this game, and of the SteamID the Hello claims.
    ///     A Steam emulator cannot make such a ticket. Family Sharing counts (Steam lends the game). Steam not answering
    ///     (an outage, a refused Web API key) lets the player in and says so in the log - an outage must not lock
    ///     everybody out.
    ///   * STEAM-OWNER-2 (2026-11-02: a genuine owner on Linux/Proton refused with 105 "Identity parameter did not match"
    ///     even with the ticket in its own length): on a 105 the log tells what identity the ticket names, and Steam is
    ///     asked once more without the identity - a genuine ticket of the game still proves the owner; only its binding to
    ///     this server is then missing, which the log says.
    /// Neither a ticket nor the Web API key is ever logged; the key stays on the server.</summary>
    internal static class SteamOwnership
    {
        internal const uint AppId = 1331550;
        private const string IdentityBase = "GoingPublicServer";

        /// <summary>The ticket's identity: bound to the server key (its first 16 hex digits of SHA-256, DedicatedHost.KeyFolder),
        /// the bare name for a server without a key.</summary>
        internal static string IdentityFor(string key)
        {
            key = (key ?? "").Trim();
            return key.Length == 0 ? IdentityBase : IdentityBase + "-" + DedicatedHost.KeyFolder(key);
        }

        /// <summary>A Steam identity's SteamID64 ("steam-7656..." -> "7656..."), "" for a guid- identity.</summary>
        internal static string SteamIdOf(string stableId)
        {
            string s = (stableId ?? "").Trim();
            return s.StartsWith("steam-", StringComparison.Ordinal) && ulong.TryParse(s.Substring(6), out _) ? s.Substring(6) : "";
        }

        // ── client ──────────────────────────────────────────────────────────────────────────────────────────────────

        private static volatile Task<AuthTicket>? _task;
        private static volatile string _identity = "";
        private static volatile bool _wanted;

        // Facepunch's GetAuthTicketForWebApiAsync hands back the callback's whole 2560-byte buffer (GubTicket), not the ticket
        // in its own length (the callback's field Ticket) - Steam then reads the padding as part of the ticket and answers
        // "Identity parameter did not match" (105; a genuine owner was refused, 2026-11-02). The length comes from the same
        // callback (SteamUser.OnGetTicketForWebApiResponse, internal), heard here as well.
        private static readonly Dictionary<uint, int> _lengths = new Dictionary<uint, int>();
        private static bool _hooked;

        private static void HookLengths()
        {
            if (_hooked) return;
            _hooked = true;
            try
            {
                var ev = typeof(SteamUser).GetEvent("OnGetTicketForWebApiResponse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var type = ev?.EventHandlerType?.GetGenericArguments().FirstOrDefault();
                if (ev == null || type == null) { Plugin.Logger.LogWarning("[SteamCheck] the ticket callback of the Steam library was not found - ticket lengths unknown."); return; }
                var heard = typeof(SteamOwnership).GetMethod(nameof(Heard), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.MakeGenericMethod(type);
                ev.GetAddMethod(true)!.Invoke(null, new object[] { Delegate.CreateDelegate(ev.EventHandlerType!, heard) });
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[SteamCheck] ticket callback: {ex.Message}"); }
        }

        private static void Heard<T>(T response)
        {
            try
            {
                object o = response!;
                var t = o.GetType();
                const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
                uint handle = Convert.ToUInt32(t.GetField("AuthTicket", F)?.GetValue(o) ?? 0u);
                int length = Convert.ToInt32(t.GetField("Ticket", F)?.GetValue(o) ?? 0);
                if (handle != 0 && length > 0) lock (_lengths) _lengths[handle] = length;
            }
            catch { }
        }

        /// <summary>A fresh ticket for the connection that starts now (asked for on the main thread).</summary>
        internal static void RequestTicket()
        {
            Cancel();
            string identity = IdentityFor(MPConfig.JoinKey);
            _wanted = true;
            GameStatePatcher.EnqueueOnMainThread(() =>
            {
                try
                {
                    if (!SteamClient.IsValid) { _wanted = false; return; }
                    HookLengths();
                    _identity = identity;
                    _task = SteamUser.GetAuthTicketForWebApiAsync(identity, 8.0);
                }
                catch (Exception ex) { _wanted = false; Plugin.Logger.LogWarning($"[SteamCheck] no ticket from Steam: {ex.Message}"); }
            });
        }

        /// <summary>Network thread (the Hello): the ticket as hex and its identity - waiting up to waitMs for Steam; "" without.</summary>
        internal static string TicketHex(out string identity, int waitMs = 3000)
        {
            identity = "";
            var until = DateTime.UtcNow.AddMilliseconds(waitMs);
            while (_wanted && DateTime.UtcNow < until)
            {
                var t = _task;
                if (t != null && t.IsCompleted) break;
                Thread.Sleep(20);
            }
            try
            {
                var t = _task;
                if (t == null || t.Status != TaskStatus.RanToCompletion) { if (_wanted) Plugin.Logger.LogWarning("[SteamCheck] Steam gave no ticket in time - the Hello goes without."); return ""; }
                var ticket = t.Result;
                var data = ticket?.Data;
                if (ticket == null || data == null || data.Length == 0) return "";
                int length;
                lock (_lengths) { if (!_lengths.TryGetValue(ticket.Handle, out length)) length = 0; _lengths.Remove(ticket.Handle); }
                bool heard = length > 0 && length <= data.Length;
                if (!heard)
                {   // the callback was not heard: the ticket without the buffer's zero padding
                    length = data.Length;
                    while (length > 0 && data[length - 1] == 0) length--;
                }
                Plugin.Logger.LogInfo($"[SteamCheck] Steam ticket for the Hello: {length} bytes ({(heard ? "length from Steam's callback" : "callback not heard, padding cut")}).");
                identity = _identity;
                return BitConverter.ToString(data, 0, length).Replace("-", "");
            }
            catch { return ""; }
        }

        /// <summary>The world arrived or the connection ended: the ticket is spent (cancelled with Steam once it is there).</summary>
        internal static void Cancel()
        {
            var t = _task;
            _task = null;
            _wanted = false;
            if (t == null) return;
            t.ContinueWith(x => GameStatePatcher.EnqueueOnMainThread(() =>
            {
                try { if (x.Status == TaskStatus.RanToCompletion) x.Result?.Cancel(); } catch { }
            }));
        }

        // ── server ──────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>A dedicated server with the check on and a Web API key.</summary>
        internal static bool Active => MPConfig.DedicatedHost && MPConfig.SteamOwnerCheck && MPConfig.SteamWebApiKey.Length > 0;

        /// <summary>Server start: one line on whether the check runs - and, with a key, whether Steam takes it (a made-up ticket:
        /// Steam answers "invalid ticket" to a good key and HTTP 403 to a wrong one), so a typo in AMP shows at once.</summary>
        internal static void LogState()
        {
            if (!MPConfig.DedicatedHost) return;
            Plugin.Logger.LogInfo(Active ? "[SteamCheck] only players who own Big Ambitions on Steam may join (Steam Web API key set)."
                : MPConfig.SteamOwnerCheck ? "[SteamCheck] the ownership check is on, but no Steam Web API key is set - nobody is checked."
                : "[SteamCheck] the ownership check is off.");
            if (!Active) return;
            string apiKey = MPConfig.SteamWebApiKey;
            Task.Run(() =>
            {
                var c = Ask(apiKey, new string('0', 32), IdentityFor(""));
                if (c.State == Answer.Refused) Plugin.Logger.LogInfo("[SteamCheck] Steam takes the Web API key.");
                else if (c.Why.StartsWith("Steam refused", StringComparison.Ordinal))
                    Plugin.Logger.LogWarning($"[SteamCheck] {c.Why} - check it in AMP; until then players are let in unchecked.");
                else Plugin.Logger.LogInfo($"[SteamCheck] could not try the Web API key with Steam now ({c.Why}).");
            });
        }

        internal enum Answer { Pending, Busy, Owner, Refused, Unreachable }

        private sealed class Check
        {
            public Answer State;
            public string SteamId = "", Why = "";
            public int Code;   // Steam's error code, 0 without
            public DateTime At = DateTime.UtcNow;
        }

        private static readonly Dictionary<string, Check> _checks = new Dictionary<string, Check>();
        private static int _asking;
        private const int MaxAsking = 8;

        /// <summary>A Steam ticket's form: hex, of a sane length.</summary>
        internal static bool IsTicket(string s)
        {
            if (s.Length < 16 || s.Length > 8192 || s.Length % 2 != 0) return false;
            foreach (char c in s) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        /// <summary>Network thread (the Hello): Steam's answer for this ticket. Pending = asked now (resume runs once Steam
        /// answered) or still asking; Busy = too many asked at once. An answer is used once.</summary>
        internal static Answer For(string ticket, string identity, Action resume, out string steamId, out string why)
        {
            steamId = why = "";
            string k = Hash(ticket);
            lock (_checks)
            {
                var old = new List<string>();
                foreach (var kv in _checks) if (kv.Value.State != Answer.Pending && (DateTime.UtcNow - kv.Value.At).TotalMinutes > 2) old.Add(kv.Key);
                foreach (var o in old) _checks.Remove(o);
                if (_checks.TryGetValue(k, out var c))
                {
                    if (c.State == Answer.Pending) return Answer.Pending;
                    _checks.Remove(k);
                    steamId = c.SteamId; why = c.Why;
                    return c.State;
                }
                if (_asking >= MaxAsking) return Answer.Busy;
                _checks[k] = new Check { State = Answer.Pending };
                _asking++;
            }
            string apiKey = MPConfig.SteamWebApiKey;
            // a game with the build of 2026-11-02 before this fix sends the library's whole 2560-byte buffer: its zero padding
            // goes, or Steam reads it as part of the ticket ("Identity parameter did not match")
            string sent = ticket.Length == 2 * 2560 ? ticket.TrimEnd('0') : ticket;
            if (sent.Length % 2 != 0) sent += "0";
            Task.Run(() =>
            {
                var c = Ask(apiKey, sent, identity);
                if (c.State == Answer.Refused && c.Code == 105)
                {   // STEAM-OWNER-2: what the ticket names, and Steam's word on it without the identity
                    Plugin.Logger.LogInfo($"[SteamCheck] {Describe(sent, identity)}");
                    var bare = Ask(apiKey, sent, "");
                    if (bare.State == Answer.Owner)
                    {
                        Plugin.Logger.LogWarning("[SteamCheck] Steam confirms the ticket only without this server's identity - the player's Steam made it without; they are let in as the owner, the ticket's binding to this server is missing.");
                        bare.Why = "ticket without this server's identity" + (bare.Why.Length > 0 ? ", " + bare.Why : "");
                        c = bare;
                    }
                    else Plugin.Logger.LogInfo($"[SteamCheck] without the identity Steam says: {(bare.Why.Length > 0 ? bare.Why : bare.State.ToString())}.");
                }
                lock (_checks) { _checks[k] = c; _asking--; }
                try { resume(); } catch (Exception ex) { Plugin.Logger.LogWarning($"[SteamCheck] resume: {ex.Message}"); }
            });
            return Answer.Pending;
        }

        /// <summary>STEAM-OWNER-2, for the log: the ticket's size and what it names of its identity in plain text, if at all
        /// (never the ticket, nor the identity's part from the key).</summary>
        private static string Describe(string hex, string identity)
        {
            try
            {
                var b = new byte[hex.Length / 2];
                for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                var mark = System.Text.Encoding.ASCII.GetBytes(IdentityBase);
                int at = -1;
                for (int i = 0; i + mark.Length <= b.Length && at < 0; i++)
                {
                    int j = 0;
                    while (j < mark.Length && b[i + j] == mark[j]) j++;
                    if (j == mark.Length) at = i;
                }
                string size = $"the ticket has {b.Length} bytes";
                if (at < 0) return size + $" and names no '{IdentityBase}' in plain text.";
                int end = at;
                while (end < b.Length && b[end] >= 0x20 && b[end] < 0x7f) end++;
                string named = System.Text.Encoding.ASCII.GetString(b, at, end - at);
                return size + (named == identity ? $"; it names the identity the Hello claims ({named.Length} characters)."
                    : identity.StartsWith(named, StringComparison.Ordinal) ? $"; it names that identity cut off after {named.Length} of {identity.Length} characters."
                    : $"; it names another identity ({named.Length} characters, the Hello's has {identity.Length}).");
            }
            catch (Exception ex) { return "the ticket could not be read: " + ex.Message; }
        }

        private static string Hash(string s)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes(s))).Replace("-", "").Substring(0, 24);
        }

        /// <summary>Background thread: Steam's word on the ticket.</summary>
        private static Check Ask(string apiKey, string ticket, string identity)
        {
            var c = new Check();
            try
            {
                try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
                string url = "https://api.steampowered.com/ISteamUserAuth/AuthenticateUserTicket/v1/?key=" + Uri.EscapeDataString(apiKey)
                           + "&appid=" + AppId + "&ticket=" + ticket + (identity.Length > 0 ? "&identity=" + Uri.EscapeDataString(identity) : "");
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 8000; req.ReadWriteTimeout = 8000; req.UserAgent = "GoingPublicServer";
                int status;
                string body;
                try
                {
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var sr = new StreamReader(resp.GetResponseStream()))
                    { status = (int)resp.StatusCode; body = sr.ReadToEnd(); }
                }
                catch (WebException wex) when (wex.Response is HttpWebResponse er)
                {
                    status = (int)er.StatusCode;
                    using (er)
                    using (var sr = new StreamReader(er.GetResponseStream()))
                        body = sr.ReadToEnd();
                }
                Newtonsoft.Json.Linq.JObject? root = null;
                try { root = Newtonsoft.Json.Linq.JObject.Parse(body); } catch { }
                var p = root?["response"]?["params"];
                if (p != null && string.Equals((string?)p["result"], "OK", StringComparison.OrdinalIgnoreCase))
                {
                    c.SteamId = (string?)p["steamid"] ?? "";
                    string owner = (string?)p["ownersteamid"] ?? "";
                    if (owner == "0") { c.State = Answer.Refused; c.Why = "Steam names no owner of the game"; }
                    else { c.State = Answer.Owner; if (owner.Length > 0 && owner != c.SteamId) c.Why = "Family Sharing"; }
                    return c;
                }
                var e = root?["response"]?["error"];
                if (e != null)
                {
                    c.State = Answer.Refused;
                    c.Code = (int?)e["errorcode"] ?? 0;
                    c.Why = $"Steam: {(string?)e["errordesc"] ?? "error"} ({c.Code})";
                    return c;
                }
                c.State = Answer.Unreachable;
                c.Why = status == 401 || status == 403 ? $"Steam refused the server's Web API key (HTTP {status})" : $"HTTP {status}";
            }
            catch (Exception ex)
            {
                c.State = Answer.Unreachable;
                string m = ex.Message ?? "";
                if (apiKey.Length > 0) m = m.Replace(apiKey, "***");   // an address in a message must not carry the key
                c.Why = ex.GetType().Name + ": " + m.Replace(ticket, "…");
            }
            return c;
        }
    }
}
