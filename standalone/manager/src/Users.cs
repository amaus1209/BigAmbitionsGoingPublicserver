using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

namespace GpsManager;

internal sealed class UserRecord
{
    public string Name { get; set; } = "";
    public string Salt { get; set; } = "";
    public string Hash { get; set; } = "";
    public int Iterations { get; set; } = Users.Iterations;
    public DateTime Changed { get; set; }
}

/// <summary>The accounts of the web interface: manager/users.json (only the user may read it on Linux), PBKDF2-SHA256
/// with a salt per user - no password is stored. The first start creates "admin" with a random password and shows it
/// once (Credentials); "reset-password" makes a new one. A change from outside (reset while the manager runs) is
/// picked up at the next login and ends every open session.</summary>
internal static class Users
{
    internal const int Iterations = 210_000;
    internal const int MinLength = 10;
    private static readonly object _lock = new();
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static List<UserRecord> _list = new();
    private static DateTime _stamp = DateTime.MinValue;

    internal static bool Any()
    {
        lock (_lock) { Reload(); return _list.Count > 0; }
    }

    internal static string FirstName()
    {
        lock (_lock) { Reload(); return _list.Count > 0 ? _list[0].Name : "admin"; }
    }

    /// <summary>Creates the user or gives him a new random password; returns the password.</summary>
    internal static string NewPasswordFor(string name)
    {
        string pass = RandomPassword();
        lock (_lock)
        {
            Reload();
            var u = _list.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (u == null) { u = new UserRecord { Name = name }; _list.Add(u); }
            SetHash(u, pass);
            Save();
        }
        return pass;
    }

    internal static bool Verify(string name, string password)
    {
        UserRecord? u;
        lock (_lock)
        {
            Reload();
            u = _list.FirstOrDefault(x => x.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        if (u == null)
        {
            // the same work for an unknown name, so the answer time does not tell which names exist
            Derive(password, new byte[16], Iterations, 32);
            return false;
        }
        try
        {
            byte[] want = Convert.FromBase64String(u.Hash);
            byte[] got = Derive(password, Convert.FromBase64String(u.Salt), u.Iterations, want.Length);
            return CryptographicOperations.FixedTimeEquals(want, got);
        }
        catch { return false; }
    }

    /// <summary>"" = done, else the reason (in the manager's language).</summary>
    internal static string ChangePassword(string name, string current, string next)
    {
        if (!Verify(name, current)) return Lang.T("Das aktuelle Passwort stimmt nicht.", "The current password is wrong.");
        if (next.Length < MinLength) return Lang.T($"Das neue Passwort braucht mindestens {MinLength} Zeichen.", $"The new password needs at least {MinLength} characters.");
        lock (_lock)
        {
            Reload();
            var u = _list.First(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            SetHash(u, next);
            Save();
        }
        return "";
    }

    /// <summary>16 characters from 54 that cannot be mistaken for each other, in groups of four (about 92 bits).</summary>
    internal static string RandomPassword()
    {
        const string alphabet = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = new List<char>();
        for (int i = 0; i < 16; i++)
        {
            if (i > 0 && i % 4 == 0) chars.Add('-');
            chars.Add(alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]);
        }
        return new string(chars.ToArray());
    }

    private static void SetHash(UserRecord u, string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        u.Salt = Convert.ToBase64String(salt);
        u.Iterations = Iterations;
        u.Hash = Convert.ToBase64String(Derive(password, salt, Iterations, 32));
        u.Changed = DateTime.UtcNow;
    }

    /// <summary>PBKDF2-HMAC-SHA256 (password as UTF-8). The system's own (Windows: BCryptDeriveKeyPBKDF2), else the same
    /// computed here - Wine's bcrypt lacks it (tested 2026-11-01: "Unknown error 0xc1000008"); both give the same bytes.</summary>
    internal static byte[] Derive(string password, byte[] salt, int iterations, int length)
    {
        try { return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, length); }
        catch (CryptographicException) { }
        using var hmac = new HMACSHA256(System.Text.Encoding.UTF8.GetBytes(password));
        var result = new byte[length];
        for (int block = 1, done = 0; done < length; block++)
        {
            byte[] u = hmac.ComputeHash(salt.Concat(new[] { (byte)(block >> 24), (byte)(block >> 16), (byte)(block >> 8), (byte)block }).ToArray());
            byte[] t = (byte[])u.Clone();
            for (int i = 1; i < iterations; i++)
            {
                u = hmac.ComputeHash(u);
                for (int j = 0; j < t.Length; j++) t[j] ^= u[j];
            }
            int n = Math.Min(t.Length, length - done);
            Buffer.BlockCopy(t, 0, result, done, n);
            done += n;
        }
        return result;
    }

    private static void Reload()
    {
        try
        {
            if (!File.Exists(Paths.UsersFile)) { _list = new List<UserRecord>(); _stamp = DateTime.MinValue; return; }
            var t = File.GetLastWriteTimeUtc(Paths.UsersFile);
            if (t == _stamp) return;
            bool changedOutside = _stamp != DateTime.MinValue;
            _list = JsonSerializer.Deserialize<List<UserRecord>>(File.ReadAllText(Paths.UsersFile), Options) ?? new List<UserRecord>();
            _stamp = t;
            if (changedOutside) Sessions.EndAll();
        }
        catch (Exception e) { Log.Warn($"manager/users.json: {e.Message}"); }
    }

    private static void Save()
    {
        Files.WriteAtomic(Paths.UsersFile, JsonSerializer.Serialize(_list, Options), secret: true);
        _stamp = File.GetLastWriteTimeUtc(Paths.UsersFile);
    }
}

/// <summary>Logged-in browsers: a random token in an HttpOnly cookie, kept in memory (a manager restart logs everyone
/// out), valid 7 days from the last request. Five wrong logins from one address within 10 minutes lock that address
/// out for 10 minutes.</summary>
internal static class Sessions
{
    internal const string Cookie = "gps_session";
    internal static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private sealed record Session(string User, DateTime Expires);
    private sealed record Fails(int Count, DateTime First, DateTime LockedUntil);
    private static readonly ConcurrentDictionary<string, Session> _sessions = new();
    private static readonly ConcurrentDictionary<string, Fails> _fails = new();

    internal static string Create(string user)
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        _sessions[token] = new Session(user, DateTime.UtcNow + Lifetime);
        return token;
    }

    internal static string? UserOf(string? token)
    {
        if (string.IsNullOrEmpty(token) || !_sessions.TryGetValue(token, out var s)) return null;
        var now = DateTime.UtcNow;
        if (s.Expires < now) { _sessions.TryRemove(token, out _); return null; }
        if (s.Expires - now < Lifetime - TimeSpan.FromMinutes(5)) _sessions[token] = s with { Expires = now + Lifetime };
        return s.User;
    }

    internal static void End(string? token)
    {
        if (!string.IsNullOrEmpty(token)) _sessions.TryRemove(token, out _);
    }

    internal static void EndAll() => _sessions.Clear();

    /// <summary>How long this address is still locked out (zero = not).</summary>
    internal static TimeSpan LockedFor(string ip)
    {
        if (!_fails.TryGetValue(ip, out var f)) return TimeSpan.Zero;
        var left = f.LockedUntil - DateTime.UtcNow;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    internal static void Failed(string ip)
    {
        var now = DateTime.UtcNow;
        _fails.AddOrUpdate(ip, _ => new Fails(1, now, DateTime.MinValue), (_, f) =>
        {
            if (now - f.First > TimeSpan.FromMinutes(10)) return new Fails(1, now, DateTime.MinValue);
            int n = f.Count + 1;
            return new Fails(n, f.First, n >= 5 ? now + TimeSpan.FromMinutes(10) : f.LockedUntil);
        });
    }

    internal static void Succeeded(string ip) => _fails.TryRemove(ip, out _);
}
