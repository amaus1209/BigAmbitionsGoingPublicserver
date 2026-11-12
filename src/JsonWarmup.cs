using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace BigAmbitionsMP
{
    /// <summary>JSON-WARMUP-1 (user 2026-11-06, test server 1 died with "Got a UNKNOWN while executing native code"):
    /// the managed stack was MonoCustomAttrs.GetCustomAttributes under Newtonsoft's CreateObjectContract under
    /// MessageEnvelope.GetPayload on the BAMP-Server network thread; test server 3 died the same way on 2026-11-01, there
    /// inside Lookup.GetGrouping under GetFieldsAndProperties. Both times the network thread was building a message type's
    /// JSON contract for the first time - reflection, attribute objects, new generic types - seconds after a player came
    /// into the world, while the main thread loaded logos and sent the world snapshots. The steady parsing (megabytes of
    /// vehicle data per player and minute) never crashed in 117 logs. So that first-time runtime work moves off the network
    /// threads: every type a message can carry gets its contract here, once, on the main thread, before the first
    /// connection. The network threads then only read Newtonsoft's shared cache (JsonConvert's default resolver), and one
    /// round trip through a new object of each of our own types makes the value accessors and creators as well.</summary>
    internal static class JsonWarmup
    {
        private static bool _done;

        /// <summary>Main thread only (the mod's Update, MPServer.Start, MPClient.Connect). Once per process.</summary>
        internal static void Ensure()
        {
            if (_done) return;
            _done = true;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int contracts = 0, trips = 0, failed = 0;
            try
            {
                var resolver = JsonSerializer.CreateDefault().ContractResolver;   // the instance JsonConvert itself uses
                var own = typeof(MessageEnvelope).Assembly;
                var queue = new Queue<Type>();
                queue.Enqueue(typeof(MessageEnvelope));
                queue.Enqueue(typeof(BuildingForSaleInfo));   // the one payload not named ...Payload
                foreach (var t in OwnTypes(own))
                    if (t.Name.EndsWith("Payload", StringComparison.Ordinal) || t.Name.EndsWith("Dto", StringComparison.Ordinal))
                        queue.Enqueue(t);
                var seen = new HashSet<Type>();
                while (queue.Count > 0)
                {
                    var t = queue.Dequeue();
                    if (t == null || !seen.Add(t)) continue;
                    JsonContract c;
                    try { c = resolver.ResolveContract(t); contracts++; }
                    catch { failed++; continue; }
                    switch (c)
                    {
                        case JsonObjectContract o:
                            foreach (var p in o.Properties)
                                if (!p.Ignored && p.PropertyType != null) queue.Enqueue(p.PropertyType);
                            break;
                        case JsonArrayContract a:
                            if (a.CollectionItemType != null) queue.Enqueue(a.CollectionItemType);
                            break;
                        case JsonDictionaryContract d:
                            if (d.DictionaryKeyType != null) queue.Enqueue(d.DictionaryKeyType);
                            if (d.DictionaryValueType != null) queue.Enqueue(d.DictionaryValueType);
                            break;
                    }
                    if (t.Assembly == own && c is JsonObjectContract && RoundTrip(t)) trips++;
                }
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Json] warm-up: {ex.Message}"); }
            Plugin.Logger.LogInfo($"[Json] {contracts} message type(s) prepared on the main thread ({trips} round trip(s), {failed} failed) in {sw.ElapsedMilliseconds} ms - the network threads only read them (JSON-WARMUP-1).");
        }

        /// <summary>Our public data classes: top level, concrete, not Unity objects.</summary>
        private static IEnumerable<Type> OwnTypes(Assembly own)
        {
            Type[] all;
            try { all = own.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { all = Array.FindAll(ex.Types, x => x != null)!; }
            foreach (var t in all)
            {
                if (t == null || !t.IsPublic || t.IsAbstract || t.IsInterface || t.IsGenericTypeDefinition) continue;
                if (typeof(UnityEngine.Object).IsAssignableFrom(t)) continue;
                yield return t;
            }
        }

        /// <summary>A fresh object out and back in: Newtonsoft makes its getters, setters and creator on first use.</summary>
        private static bool RoundTrip(Type t)
        {
            try
            {
                if (!t.IsValueType && t.GetConstructor(Type.EmptyTypes) == null) return false;
                var o = Activator.CreateInstance(t);
                JsonConvert.DeserializeObject(JsonConvert.SerializeObject(o), t);
                return true;
            }
            catch { return false; }
        }
    }
}
