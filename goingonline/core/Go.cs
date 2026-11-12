using System.Globalization;
using Localizor;

namespace GoingOnline
{
    /// <summary>What the mod around the provider supplies: the stand-alone mod (src/) or Going Public Server
    /// (src/GoingOnlineModule.cs there).</summary>
    public interface IGoHost
    {
        /// <summary>The mod's folder (icons in assets\).</summary>
        string ModRoot { get; }
        /// <summary>False switches the provider off (the server's module switch, a dedicated server).</summary>
        bool Enabled { get; }
        void Log(string message);
        void Warn(string message);
        /// <summary>Opens the game's full menu, so the provider's page can take it over.</summary>
        bool OpenFullMenu();
        /// <summary>The host's own pages in the full menu step aside (Going Public Server's Business and Radio).</summary>
        void HideOtherPages();
        /// <summary>A setting as a percentage (100 = as designed): "faults", "growth", "unlock31", "unlockfiber".</summary>
        int Percent(string id, int fallback);
    }

    /// <summary>The provider's entry points and small helpers shared by everything in core/.</summary>
    public static class Go
    {
        public static IGoHost? Host;

        public static void Log(string m) { try { Host?.Log("[GoingOnline] " + m); } catch { } }
        public static void Warn(string m) { try { Host?.Warn("[GoingOnline] " + m); } catch { } }

        /// <summary>Text in the player's game language: the mod's Locales/&lt;lang&gt;.json via the game's localizer,
        /// the English text when the key is missing. Arguments are {0}, {1} ...</summary>
        public static string T(string key, string english, params object[] args)
        {
            string s = english;
            try
            {
                if (LocalizorManager.IsLocalizedKey(key))
                {
                    string v = LocalizorManager.GetLocalization(key);
                    if (!string.IsNullOrEmpty(v)) s = v;
                }
            }
            catch { }
            if (args == null || args.Length == 0) return s;
            try { return string.Format(s, args); }
            catch { try { return string.Format(english, args); } catch { return english; } }
        }

        /// <summary>The game's own text for one of its keys (a neighbourhood's name), the key's tail if there is none.</summary>
        public static string Game(string key)
        {
            try
            {
                if (!string.IsNullOrEmpty(key) && LocalizorManager.IsLocalizedKey(key))
                {
                    string v = LocalizorManager.GetLocalization(key);
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
            catch { }
            int i = key.LastIndexOf('_');
            return i >= 0 && i < key.Length - 1 ? key.Substring(i + 1) : key;
        }

        static CultureInfo Culture
        {
            get
            {
                try
                {
                    string lang = (LocalizorManager.LoadedLocale ?? "").ToLowerInvariant();
                    if (lang.StartsWith("de")) return CultureInfo.GetCultureInfo("de-DE");
                }
                catch { }
                return CultureInfo.GetCultureInfo("en-US");
            }
        }

        /// <summary>Money as the player reads it: "$1,234" (whole dollars from 100 up), "$1.49" below.</summary>
        public static string Money(float v)
        {
            var c = Culture;
            string sign = v < 0 ? "-" : "";
            float a = System.Math.Abs(v);
            return sign + "$" + (a >= 100f ? a.ToString("#,0", c) : a.ToString("0.00", c));
        }

        public static string Num(float v, int decimals = 0) => v.ToString(decimals == 0 ? "#,0" : "#,0." + new string('0', decimals), Culture);

        public static string Pct(float frac) => (frac * 100f).ToString("0", Culture) + " %";

        /// <summary>A bandwidth: "800 Mbit/s", "12.5 Gbit/s".</summary>
        public static string Bw(float mbit) => mbit >= 1000f ? (mbit / 1000f).ToString(mbit >= 10000f ? "0" : "0.0", Culture) + " Gbit/s"
                                                            : mbit.ToString("0", Culture) + " Mbit/s";

        /// <summary>A number the player typed: "1.49", "1,49" and "$1.49" all work.</summary>
        public static bool ParseNum(string? s, out float v)
        {
            v = 0f;
            if (string.IsNullOrWhiteSpace(s)) return false;
            string t = s!.Trim().Replace("$", "").Replace(" ", "").Replace(",", ".");
            int dots = 0;
            foreach (char ch in t) if (ch == '.') dots++;
            if (dots > 1) t = t.Replace(".", "");   // "1.234.5" - take it as a whole number of thousands
            return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }
    }
}
