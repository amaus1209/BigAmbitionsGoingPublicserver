namespace BigAmbitionsMP
{
    internal static class MyPluginInfo
    {
        public const string PLUGIN_GUID = "com.bigambitions.multiplayer";
        public const string PLUGIN_NAME = "BigAmbitionsMP";
        public const string PLUGIN_VERSION = "1.0.17.3";   // Going Public Server (fork of Going Public 0.3.3)

        /// <summary>Official public name (Steam Workshop title, UI, bug reports,
        /// release notes).  NAMING POLICY (2026-07-09): only DISPLAY text uses
        /// this — every technical identifier stays "BigAmbitionsMP"/"BAMP_"
        /// because it is install- or save-persisted: the assembly + ModsLocal
        /// folder name (existing installs), PLUGIN_GUID/Harmony id, _BAMP_MP
        /// save folders, BAMP_DUTY_ employee ids (repair sweeps key on the
        /// prefix), config paths, the "BAMP:version:" wire tag, and the
        /// X-BAMP-Key relay header.</summary>
        public const string DISPLAY_NAME = "Going Public Server: Multiplayer for Big Ambitions with a dedicated server";
        /// <summary>Short form for tight UI spots.</summary>
        public const string SHORT_NAME = "Going Public Server";

        /// <summary>True only in a `-c Dev` build (BAMP_DEV defined) — the
        /// test/diagnostic scaffolding is compiled in.  Shipped builds are false.</summary>
        public const bool IsDevBuild =
#if BAMP_DEV
            true;
#else
            false;
#endif

        /// <summary>Human-readable build tag.  Logged at startup AND embedded in the
        /// DLL (the log reference inlines the literal), so the packaging script can
        /// byte-scan the DLL and refuse to ship a "DEV build" by mistake.</summary>
        public const string BuildTag =
#if BAMP_DEV
            "DEV build (diagnostics on)";
#else
            "release build";
#endif

        private static string? _buildId;

        /// <summary>BUILD-ID-1 (user 2026-11-01): the first 8 hex digits of the SHA-256 of this DLL - the id the Workshop zip's
        /// name carries (install-into-amp.sh), so the version line tells builds of one version apart. "" when unreadable.</summary>
        public static string BuildId
        {
            get
            {
                if (_buildId != null) return _buildId;
                try
                {
                    // the loaded file; a loader that reads the DLL from bytes leaves Location empty - then the mod's folder
                    string path = typeof(MyPluginInfo).Assembly.Location;
                    if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) path = System.IO.Path.Combine(MPConfig.ModRootPath, "BigAmbitionsMP.dll");
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    var h = sha.ComputeHash(System.IO.File.ReadAllBytes(path));
                    _buildId = System.BitConverter.ToString(h, 0, 4).Replace("-", "").ToLowerInvariant();
                }
                catch { _buildId = ""; }
                return _buildId;
            }
        }
    }
}
