using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HarmonyLib;
using Localizor;
using Newtonsoft.Json;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>
    /// MOD-TRANSLATIONS-1 (Going Public Server 1.0.10): other mods in the player's game language, where they lack it.
    /// The tables only ever ADD what a mod does not have itself.
    ///
    /// 1. Locales/&lt;Mod DLL name&gt;/&lt;lang&gt;.json - for mods that use the game's localization (their own Locales/ folder).
    ///    The game merges the Locales folders of all mods into one table per language (LocalizorManager), so a language
    ///    the mod lacks can come from here: merged into persistentDataPath/GoingPublicServer/ModTranslations/ and
    ///    registered like a mod's folder. A language the mod ships itself is left out, so the author's own text wins.
    ///    The game's mod loader replaces the whole set of registered folders (SyncExternalLocalizationPaths), so this
    ///    re-registers after every sync.
    /// 2. Texts/&lt;Mod DLL name&gt;/&lt;lang&gt;.json - for mods with fixed English text in their code: { "English": "translation" }.
    ///    When such an assembly loads, every method of it that loads one of these exact strings is patched to pass the
    ///    string through <see cref="T"/>, which returns the translation for the game's current language (or the English).
    ///    Only listed strings are touched, so internal names, keys and file names of the mod stay as they are.
    ///
    /// MOD-TRANSLATIONS-2: the tables come from two places - the copy shipped in &lt;this mod&gt;/ModTranslations/ and a cache
    /// of the public repository's ModTranslations/ (index.json: version + sha256 per file), fetched in the background at
    /// every start. The set with the higher index version wins file by file, so new translations reach every player
    /// without a mod update: language packs at once, fixed texts of newly listed strings at the next start (they are
    /// patched when the mod's assembly loads). Only JSON text tables, by fixed name pattern, sha256-checked.
    /// </summary>
    internal static class ModTranslations
    {
        private const string LocaleId = "GoingPublicServer.ModTranslations";
        private const string RemoteBase = "https://raw.githubusercontent.com/amaus1209/BigAmbitionsGoingPublicServer/main/ModTranslations/";
        private static readonly Regex SafeRel = new Regex(@"^(Locales|Texts)/[A-Za-z0-9._-]+/[a-z]{2}(-[a-z]{2})?\.json$", RegexOptions.Compiled);

        private static string BundledDir => Path.Combine(Path.GetDirectoryName(typeof(ModTranslations).Assembly.Location) ?? "", "ModTranslations");
        private static string CacheDir => Path.Combine(Application.persistentDataPath, "GoingPublicServer", "ModTranslationsCache");

        private sealed class Index { public int version = 0; public Dictionary<string, string> files = new Dictionary<string, string>(); }

        private static Index ReadIndex(string dir)
        {
            try
            {
                string p = Path.Combine(dir, "index.json");
                if (File.Exists(p)) return JsonConvert.DeserializeObject<Index>(File.ReadAllText(p)) ?? new Index();
            }
            catch { }
            return new Index();
        }

        /// <summary>Relative path -> full path of every table in use: the set with the higher index version first, the
        /// other filling in what it lacks.</summary>
        private static Dictionary<string, string> ActiveFiles()
        {
            var roots = new List<(string dir, int version)> { (CacheDir, ReadIndex(CacheDir).version), (BundledDir, ReadIndex(BundledDir).version) };
            roots.Sort((a, b) => b.version.CompareTo(a.version));
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (dir, _) in roots)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var kind in new[] { "Locales", "Texts" })
                {
                    string k = Path.Combine(dir, kind);
                    if (!Directory.Exists(k)) continue;
                    foreach (var modDir in Directory.GetDirectories(k))
                        foreach (var f in Directory.GetFiles(modDir, "*.json"))
                        {
                            string rel = $"{kind}/{Path.GetFileName(modDir)}/{Path.GetFileName(f)}";
                            if (SafeRel.IsMatch(rel) && !files.ContainsKey(rel)) files[rel] = f;
                        }
                }
            }
            return files;
        }

        // ── 1. language packs for mods that use the game's localization ─────────────────────────────────────────────
        private static bool _registering, _localeFailLogged;

        /// <summary>Main thread: after the game registered the mods' Locales folders, and after a download.</summary>
        public static void RefreshLocalePacks()
        {
            if (_registering || Off) return;
            try
            {
                var registered = AccessTools.Field(typeof(LocalizorManager), "RegisteredExternalPaths")?.GetValue(null) as Dictionary<string, string>;
                var paths = registered == null ? new List<string>() : registered.Where(kv => kv.Key != LocaleId).Select(kv => kv.Value).ToList();
                var merged = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                var skipped = new List<string>();
                foreach (var kv in ActiveFiles().Where(f => f.Key.StartsWith("Locales/")).OrderBy(f => f.Key))
                {
                    var parts = kv.Key.Split('/');
                    string asm = parts[1], lang = Path.GetFileNameWithoutExtension(parts[2]).ToLowerInvariant();
                    // the mod's own Locales folder, when it is installed: <mod folder>/<Assembly>.dll next to it
                    string? own = paths.FirstOrDefault(p => File.Exists(Path.Combine(Path.GetDirectoryName(p.TrimEnd('/', '\\')) ?? "", asm + ".dll")));
                    if (own != null && File.Exists(Path.Combine(own, lang + ".json"))) { skipped.Add($"{asm}/{lang}"); continue; }
                    var table = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(kv.Value));
                    if (table == null) continue;
                    if (!merged.TryGetValue(lang, out var into)) merged[lang] = into = new Dictionary<string, string>();
                    foreach (var e in table) into[e.Key] = e.Value;
                }
                // MOD-TRANSLATIONS-3: a mod's own regional file for a game language it lacks ("pt-BR.json" where the
                // game loads "pt.json") counts as that language - the author's own translation, under the game's code
                var aliased = new List<string>();
                string[] gameLangs = Array.Empty<string>();
                try { gameLangs = LocalizorManager.GetAvailableLanguages(false); } catch { }
                foreach (var dir in paths)
                {
                    if (!Directory.Exists(dir)) continue;
                    var files = Directory.GetFiles(dir, "*.json");
                    foreach (var lang in gameLangs)
                    {
                        if (files.Any(f => string.Equals(Path.GetFileNameWithoutExtension(f), lang, StringComparison.OrdinalIgnoreCase))) continue;
                        string? variant = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).StartsWith(lang + "-", StringComparison.OrdinalIgnoreCase));
                        if (variant == null) continue;
                        var table = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(variant));
                        if (table == null) continue;
                        if (!merged.TryGetValue(lang, out var into)) merged[lang] = into = new Dictionary<string, string>();
                        foreach (var e in table) if (!into.ContainsKey(e.Key)) into[e.Key] = e.Value;
                        aliased.Add($"{Path.GetFileName(Path.GetDirectoryName(dir.TrimEnd('/', '\\')))}/{Path.GetFileName(variant)}→{lang}");
                    }
                }
                string outDir = Path.Combine(Application.persistentDataPath, "GoingPublicServer", "ModTranslations");
                Directory.CreateDirectory(outDir);
                foreach (var old in Directory.GetFiles(outDir, "*.json"))
                    if (!merged.ContainsKey(Path.GetFileNameWithoutExtension(old))) File.Delete(old);
                foreach (var kv in merged)
                    File.WriteAllText(Path.Combine(outDir, kv.Key + ".json"), JsonConvert.SerializeObject(kv.Value, Formatting.Indented));
                _registering = true;
                LocalizorManager.RegisterExternalLocalization(LocaleId, outDir, true);
                Plugin.Logger.LogInfo($"[ModTranslations] language packs for other mods: {string.Join(", ", merged.Keys.OrderBy(k => k))}"
                    + (skipped.Count > 0 ? $"; left to the mods themselves: {string.Join(", ", skipped)}" : "")
                    + (aliased.Count > 0 ? $"; regional files used: {string.Join(", ", aliased)}" : ""));
            }
            catch (Exception ex)
            {
                if (!_localeFailLogged) { _localeFailLogged = true; Plugin.Logger.LogWarning($"[ModTranslations] language packs: {ex.Message}"); }
            }
            finally { _registering = false; }
        }

        // ── 2. fixed English text in other mods' code ───────────────────────────────────────────────────────────────
        private static volatile Dictionary<string, Dictionary<string, string>> _texts = new Dictionary<string, Dictionary<string, string>>();
        private static volatile HashSet<string> _english = new HashSet<string>(StringComparer.Ordinal);
        private static volatile HashSet<string> _targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _patched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();
        private static Harmony? _harmony;
        private static MethodInfo? _tMethod;

        /// <summary>Once, at load: read the tables, patch the listed mods now and whenever one loads later, and fetch
        /// newer tables from the repository in the background.</summary>
        /// <summary>MOD-TRANSLATIONS-5: a file "ModTranslations.off" next to the mod's DLL switches the whole feature off
        /// (no tables, no patches of other mods, no language packs, no download) - for telling it apart from a fault.</summary>
        private static readonly bool Off = SwitchedOff();

        private static bool SwitchedOff()
        {
            try { return File.Exists(Path.Combine(Path.GetDirectoryName(typeof(ModTranslations).Assembly.Location) ?? "", "ModTranslations.off")); }
            catch { return false; }
        }

        public static void Init()
        {
            if (Off) { Plugin.Logger.LogWarning("[ModTranslations] switched off (ModTranslations.off) - other mods keep their own languages."); return; }
            try
            {
                LoadTexts();
                _harmony = new Harmony("com.bamp.gps.modtranslations");
                _tMethod = AccessTools.Method(typeof(ModTranslations), nameof(T));
                AppDomain.CurrentDomain.AssemblyLoad += (s, e) => TryPatch(e.LoadedAssembly);
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) TryPatch(a);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[ModTranslations] texts: {ex.Message}"); }
            RefreshLocalePacks();
            Task.Run(Download);
        }

        private static void LoadTexts()
        {
            var texts = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var english = new HashSet<string>(StringComparer.Ordinal);
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in ActiveFiles().Where(f => f.Key.StartsWith("Texts/")))
            {
                var parts = kv.Key.Split('/');
                targets.Add(parts[1]);
                string lang = Path.GetFileNameWithoutExtension(parts[2]).ToLowerInvariant();
                Dictionary<string, string>? table = null;
                try { table = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(kv.Value)); } catch { }
                if (table == null) continue;
                if (!texts.TryGetValue(lang, out var into)) texts[lang] = into = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var e in table)
                    if (!string.IsNullOrEmpty(e.Key) && !string.IsNullOrEmpty(e.Value)) { into[e.Key] = e.Value; english.Add(e.Key); }
            }
            _texts = texts; _english = english; _targets = targets;   // whole-reference swaps: T reads without a lock
            Plugin.Logger.LogInfo($"[ModTranslations] fixed texts of {(targets.Count == 0 ? "no mod" : string.Join(", ", targets.OrderBy(t => t)))} in {texts.Count} language(s) ({english.Count} text(s)).");
        }

        /// <summary>The translation of a listed English text for the game's current language, else the text itself.</summary>
        public static string T(string english)
        {
            try
            {
                string lang = LocalizorManager.LoadedLocale ?? "";
                if (lang.Length > 0 && _texts.TryGetValue(lang, out var table) && table.TryGetValue(english, out var t)) return t;
            }
            catch { }
            return english;
        }

        private static void TryPatch(Assembly a)
        {
            if (_harmony == null) return;
            string name, file = "";
            try { name = a.GetName().Name; } catch { return; }
            try { file = Path.GetFileNameWithoutExtension(a.Location ?? ""); } catch { }
            // a listed mod by its DLL's file name or its assembly name, also with a version tail ("Immersion-1.2.0-test8")
            string? target = _targets.FirstOrDefault(t => string.Equals(t, file, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, name, StringComparison.OrdinalIgnoreCase) || name.StartsWith(t + "-", StringComparison.OrdinalIgnoreCase));
            if (target == null) return;
            lock (_lock) { if (!_patched.Add(name)) return; }
            int methods = 0, failed = 0;
            Type[] types;
            try { types = a.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[ModTranslations] {name}: types not readable ({ex.Message})."); return; }
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var type in types)
            {
                if (type.IsGenericTypeDefinition) continue;
                IEnumerable<MethodBase> members;
                try { members = type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)); }
                catch { continue; }
                foreach (var m in members)
                {
                    try
                    {
                        if (m.IsAbstract || m.ContainsGenericParameters || !LoadsListedString(m)) continue;
                        _harmony.Patch(m, transpiler: new HarmonyMethod(typeof(ModTranslations), nameof(Transpiler)));
                        methods++;
                    }
                    catch (Exception ex)
                    {
                        if (++failed <= 3) Plugin.Logger.LogWarning($"[ModTranslations] {name}: {type.Name}.{m.Name} not patched ({ex.Message}).");
                    }
                }
            }
            Plugin.Logger.LogInfo($"[ModTranslations] {name}: {methods} method(s) show translated text{(failed > 0 ? $", {failed} failed" : "")}.");
        }

        /// <summary>Does the method's IL load one of the listed strings? (ldstr = 0x72 + string token; a byte that only
        /// looks like one resolves to no string or another one and costs nothing but a skipped patch.)</summary>
        private static bool LoadsListedString(MethodBase m)
        {
            byte[]? il = m.GetMethodBody()?.GetILAsByteArray();
            if (il == null) return false;
            var english = _english;
            for (int i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] != 0x72 || il[i + 4] != 0x70) continue;   // a string token's table byte is 0x70
                try { if (english.Contains(m.Module.ResolveString(BitConverter.ToInt32(il, i + 1)))) return true; }
                catch { }
            }
            return false;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var english = _english;
            foreach (var ins in instructions)
            {
                yield return ins;
                if (ins.opcode == OpCodes.Ldstr && ins.operand is string s && english.Contains(s))
                    yield return new CodeInstruction(OpCodes.Call, _tMethod);
            }
        }

        // ── MOD-TRANSLATIONS-2: newer tables from the repository ────────────────────────────────────────────────────
        private static volatile bool _reloadPending;

        /// <summary>Main thread, every frame (MPCanvasUI.Update): apply a finished download.</summary>
        public static void Tick()
        {
            if (!_reloadPending || Off) return;
            _reloadPending = false;
            LoadTexts();
            RefreshLocalePacks();
        }

        private static void Download()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                Index remote;
                using (var wc = new WebClient())
                    remote = JsonConvert.DeserializeObject<Index>(wc.DownloadString(RemoteBase + "index.json?t=" + DateTime.UtcNow.Ticks)) ?? new Index();
                var cache = ReadIndex(CacheDir);
                int have = Math.Max(cache.version, ReadIndex(BundledDir).version);
                if (remote.version <= have) { Plugin.Logger.LogInfo($"[ModTranslations] repository tables v{remote.version}: nothing newer (have v{have})."); return; }
                string stage = CacheDir + ".new";
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
                int fetched = 0;
                foreach (var f in remote.files)
                {
                    if (!SafeRel.IsMatch(f.Key) || !Regex.IsMatch(f.Value ?? "", "^[0-9a-f]{64}$")) continue;
                    string dst = Path.Combine(stage, f.Key.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    string old = Path.Combine(CacheDir, f.Key.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(old) && Sha256(File.ReadAllBytes(old)) == f.Value) { File.Copy(old, dst); continue; }
                    byte[] data;
                    using (var wc = new WebClient()) data = wc.DownloadData(RemoteBase + f.Key + "?h=" + f.Value);
                    if (data.Length > 4 * 1024 * 1024 || Sha256(data) != f.Value) throw new IOException($"checksum mismatch for {f.Key}");
                    File.WriteAllBytes(dst, data);
                    fetched++;
                }
                File.WriteAllText(Path.Combine(stage, "index.json"), JsonConvert.SerializeObject(remote));
                if (Directory.Exists(CacheDir)) Directory.Delete(CacheDir, true);
                Directory.Move(stage, CacheDir);
                Plugin.Logger.LogInfo($"[ModTranslations] repository tables v{remote.version}: {fetched} file(s) downloaded, {remote.files.Count} in use.");
                _reloadPending = true;
            }
            catch (Exception ex) { Plugin.Logger.LogInfo($"[ModTranslations] repository tables not loaded ({ex.Message}) - using the local ones."); }
        }

        private static string Sha256(byte[] data)
        {
            using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>MOD-TRANSLATIONS-1: the game's mod loader replaces the registered Locales folders - add ours again.</summary>
    [HarmonyPatch(typeof(LocalizorManager), nameof(LocalizorManager.SyncExternalLocalizationPaths))]
    internal static class Patch_LocalizorSync_ModTranslations
    {
        static void Postfix() => ModTranslations.RefreshLocalePacks();
    }
}
