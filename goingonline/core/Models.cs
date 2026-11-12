using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GO-MODELS-1 (user 2026-11-07: "baue für den Mod alle Möbel und Server sowie Modems usw. Alle Geräte, die
    /// man kaufen kann mit Going Online" and "nimm als Vorlage Bilder aus dem Web"): own 3D models for the pieces of headend
    /// equipment (since 0.8.1 also vCMTS, streaming cache, UPS and generator: "erstelle noch die fehlenden 3D-Modelle für die
    /// neuen Sachen"), the device table and the five customer devices, made in Blender after reference photos (no brands; the source is
    /// ~/Big-Ambitions-Server-Code/ba-art/goingonline/go_models.py) and shipped as assets/models/&lt;id&gt;.gom - gzip, then "GOM1", the materials (name,
    /// sRGB colour, metallic, smoothness, emission, role) and one sub-mesh per material in the game's axes.
    /// The game's template prefab stays as it is (collider, ItemController, attachment point, walk targets, shop sign); only
    /// the mesh and the materials of its own renderer are swapped, so placing, selling and the outline work as before.
    /// Materials are shared by name across all models; the blinking LEDs are a few shared materials one clock switches
    /// (ModelLights), so nothing fights the game's own material handling. Without a model file the old look stays.</summary>
    internal static class Models
    {
        internal sealed class Part
        {
            public string Name = "";
            public Color Base, Emit;
            public float Metallic, Smoothness, Strength;
            public byte Role, Group;            // role 0 plain, 1 status light, 2 traffic light, 3 display, 4 the item's colour
        }

        internal sealed class Model
        {
            public Mesh Mesh = null!;
            public Part[] Parts = null!;
            /// <summary>The sub-mesh of a part (by its material name), -1 if there is none.</summary>
            public int PartIndex(string name) { for (int i = 0; i < Parts.Length; i++) if (Parts[i].Name == name) return i; return -1; }
            /// <summary>Named spots of the model (position, outward direction, size), e.g. where the business logo goes.</summary>
            public Dictionary<string, (Vector3 pos, Vector3 normal, float size)> Markers = new Dictionary<string, (Vector3, Vector3, float)>();
        }

        // GO-LIGHTS-1 (user 2026-11-08, the racks in a sunny office: "keine Aktivitäten sichtbar"): HDRP took the glow as real
        // light (exposure weight 1), so in daylight the LEDs were too faint to see. They now ignore the camera's exposure
        // (_EmissiveExposureWeight 0) and are as bright on screen by day as by night; the values are screen brightness.
        const float LedGlow = 3f, ScreenGlow = 0.8f, LedOff = 0.03f, PhysicalLed = 22f, PhysicalScreen = 5f;
        static readonly Dictionary<string, Model?> _models = new Dictionary<string, Model?>();
        static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();
        static readonly List<(Material m, Vector4 on, int group, int color)> _traffic = new List<(Material, Vector4, int, int)>();
        // GO-LIGHTS-3/4 (the user's game, 2026-11-08: "7 racks in the world, 0 of them show those materials", then "blinkt nichts -
        // schau zur Hilfe an, wie das Bremslicht der Autos funktioniert"): a placed piece does not use our shared materials, so
        // the clock switches each placed piece's LED parts the way the game's cars switch their brake lights
        // (GleyTrafficSystem.VehicleLightsComponent): a MaterialPropertyBlock on the renderer, per material slot - whatever
        // material copy the game gave the piece. Each piece's ModelLights registers its LED slots.
        static readonly Dictionary<string, (Vector4 on, int group, int color)> _trafficByName = new Dictionary<string, (Vector4, int, int)>();
        static readonly List<(Renderer r, int index, Vector4 on, int group, int color)> _slots = new List<(Renderer, int, Vector4, int, int)>();
        static readonly HashSet<Renderer> _scanned = new HashSet<Renderer>();
        static readonly HashSet<Material> _known = new HashSet<Material>();
        static MaterialPropertyBlock? _mpb;
        static readonly int EmissiveId = Shader.PropertyToID("_EmissiveColor");
        static Shader? _lit;
        static bool _litLooked, _reported;
        static int _loaded, _failed;

        /// <summary>The model of an item (id without the "goingonline:itemname_" part), or null if it has none.</summary>
        public static Model? Get(string id)
        {
            if (_models.TryGetValue(id, out var have)) return have;
            Model? m = null;
            try
            {
                string path = Path.Combine(Go.Host?.ModRoot ?? "", "assets", "models", id + ".gom");
                if (File.Exists(path)) { m = Load(path, id); if (m != null) _loaded++; else _failed++; }
            }
            catch (System.Exception ex) { _failed++; Go.Warn($"model {id}: {ex.Message} (GO-MODELS-1)."); }
            _models[id] = m;
            return m;
        }

        public static bool Has(string id) => Get(id) != null;

        static Model? Load(string path, string id)
        {
            using var fs = File.OpenRead(path);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var r = new BinaryReader(gz);
            if (new string(r.ReadChars(4)) != "GOM1") { Go.Warn($"model {id}: not a model file (GO-MODELS-1)."); return null; }
            int version = r.ReadUInt16();                       // 2: sub-meshes may carry texture coordinates (GO-LIVERY-1)
            int n = r.ReadUInt16();
            var parts = new Part[n];
            for (int i = 0; i < n; i++)
            {
                var p = new Part { Name = new string(r.ReadChars(r.ReadByte())) };
                p.Base = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                p.Metallic = r.ReadSingle();
                p.Smoothness = r.ReadSingle();
                p.Emit = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), 1f);
                p.Strength = r.ReadSingle();
                p.Role = r.ReadByte();
                p.Group = r.ReadByte();
                parts[i] = p;
            }
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            bool anyUv = false;
            var tris = new int[n][];
            for (int i = 0; i < n; i++)
            {
                r.ReadUInt16();
                int vc = (int)r.ReadUInt32(), b = verts.Count;
                for (int k = 0; k < vc; k++) verts.Add(new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));
                for (int k = 0; k < vc; k++) norms.Add(new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));
                if (version >= 2 && r.ReadByte() == 1)
                {
                    anyUv = true;
                    for (int k = 0; k < vc; k++) uvs.Add(new Vector2(r.ReadSingle(), r.ReadSingle()));
                }
                else for (int k = 0; k < vc; k++) uvs.Add(Vector2.zero);
                int ic = (int)r.ReadUInt32();
                var t = new int[ic];
                for (int k = 0; k < ic; k++) t[k] = b + (int)r.ReadUInt32();
                tris[i] = t;
            }
            // GO-VAN-7: the van's blinkers left and right apart - the right side's amber lamps become a part of their own
            // (put at the end, so every other part keeps its slot), which VanLights switches on its own
            if (id == "govan" && SplitBySide(ref parts, ref tris, verts, "lamp_amber", "lamp_amber_r")) n = parts.Length;
            // GO-VAN-15: the roof beacon's two lamps apart - Van.Beacons flashes them in turn
            if (id == "govan" && SplitBySide(ref parts, ref tris, verts, "beacon", "beacon_r")) n = parts.Length;
            var mesh = new Mesh { name = "GO_" + id };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            if (anyUv) mesh.SetUVs(0, uvs);
            mesh.subMeshCount = n;
            for (int i = 0; i < n; i++) mesh.SetTriangles(tris[i], i, false);
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var model = new Model { Mesh = mesh, Parts = parts };
            for (int i = 0; i < 6; i++) r.ReadSingle();          // bounds (the mesh has its own)
            int markers = r.ReadUInt16();
            for (int i = 0; i < markers; i++)
            {
                string name = new string(r.ReadChars(r.ReadByte()));
                var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                var normal = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                model.Markers[name] = (pos, normal, r.ReadSingle());
            }
            return model;
        }

        /// <summary>GO-VAN-7: the triangles of one part on the right side (+x: the game's right) go to a new part of their own.</summary>
        static bool SplitBySide(ref Part[] parts, ref int[][] tris, List<Vector3> verts, string name, string right)
        {
            int i = System.Array.FindIndex(parts, p => p.Name == name);
            if (i < 0) return false;
            var t = tris[i];
            var keep = new List<int>(t.Length);
            var move = new List<int>(t.Length);
            for (int k = 0; k + 2 < t.Length; k += 3)
            {
                var dst = verts[t[k]].x + verts[t[k + 1]].x + verts[t[k + 2]].x > 0f ? move : keep;
                dst.Add(t[k]); dst.Add(t[k + 1]); dst.Add(t[k + 2]);
            }
            if (move.Count == 0 || keep.Count == 0) return false;
            var s = parts[i];
            var copy = new Part
            {
                Name = right, Base = s.Base, Emit = s.Emit, Metallic = s.Metallic, Smoothness = s.Smoothness,
                Strength = s.Strength, Role = s.Role, Group = s.Group,
            };
            System.Array.Resize(ref parts, parts.Length + 1);
            parts[parts.Length - 1] = copy;
            tris[i] = keep.ToArray();
            System.Array.Resize(ref tris, tris.Length + 1);
            tris[tris.Length - 1] = move.ToArray();
            return true;
        }

        /// <summary>Puts the item's own model on the template copy's renderer. False: no model (the old look stays).</summary>
        public static bool Apply(GameObject go, string id, Color tint)
        {
            var model = Get(id);
            if (model == null) return false;
            var mf = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
            if (mf == null || mr == null) { Go.Warn($"model {id}: the template has no renderer of its own (GO-MODELS-1)."); return false; }
            var mats = MaterialsFor(model, id, tint, mr.sharedMaterial);
            mf.sharedMesh = model.Mesh;
            mr.sharedMaterials = mats;
            return true;
        }

        /// <summary>The materials of a model, one per sub-mesh (shared by name; the item's colour on its accent part).</summary>
        public static Material[] MaterialsFor(Model model, string id, Color tint, Material? fallback)
        {
            var mats = new Material[model.Parts.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = MaterialFor(model.Parts[i], id, tint, fallback);
            return mats;
        }

        public static Shader? LitShader => Lit();

        /// <summary>GO-VAN-18: the service van model's revision. A new model alone (assets/models/govan.gom) leaves both DLLs
        /// - and so the build ids in the package names - as they were; bumping this with each new van model changes them,
        /// and Report logs it with the file's own short hash (which van a player has).</summary>
        internal const string VanModelRev = "GO-VAN-18";

        /// <summary>Once, after the first pass over all items: how many of the shipped model files are in use.</summary>
        public static void Report()
        {
            if (_reported || _models.Count == 0) return;
            _reported = true;
            int files = 0;
            try
            {
                var dir = Path.Combine(Go.Host?.ModRoot ?? "", "assets", "models");
                if (Directory.Exists(dir)) files = Directory.GetFiles(dir, "*.gom").Length;
            }
            catch { }
            string van = "";
            try
            {
                var gom = Path.Combine(Go.Host?.ModRoot ?? "", "assets", "models", "govan.gom");
                if (File.Exists(gom))
                    using (var sha = System.Security.Cryptography.SHA256.Create())
                        van = $", van model {VanModelRev} ({System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(gom)), 0, 4).Replace("-", "").ToLowerInvariant()})";
            }
            catch { }
            Go.Log($"own models: {_loaded} of {files} loaded{(_failed > 0 ? $", {_failed} failed" : "")}, shader {(Lit() != null ? "HDRP/Lit" : "of the template")}{van} (GO-MODELS-1).");
        }

        static Shader? Lit()
        {
            if (!_litLooked) { _litLooked = true; try { _lit = Shader.Find("HDRP/Lit"); } catch { _lit = null; } }
            return _lit;
        }

        static Material MaterialFor(Part p, string id, Color tint, Material? fallback)
        {
            string key = p.Role == 4 ? p.Name + "|" + id : p.Name;
            if (_mats.TryGetValue(key, out var have) && have != null) return have;
            var sh = Lit();
            var m = sh != null ? new Material(sh) : fallback != null ? new Material(fallback) : new Material(Shader.Find("Standard"));
            m.name = "GO_" + p.Name;
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var c = p.Role == 4 && tint != Color.white ? new Color(tint.r, tint.g, tint.b, 1f) : p.Base;
            foreach (var prop in new[] { "_BaseColor", "_Color" }) if (m.HasProperty(prop)) m.SetColor(prop, c);
            foreach (var tex in new[] { "_BaseColorMap", "_BaseMap", "_MainTex", "_MaskMap", "_NormalMap" }) if (m.HasProperty(tex)) m.SetTexture(tex, null);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", p.Metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", p.Smoothness);
            if (p.Strength > 0f)
            {
                bool screen = p.Role == 3;
                bool flat = m.HasProperty("_EmissiveExposureWeight");
                if (flat) m.SetFloat("_EmissiveExposureWeight", 0f);
                var on = Glow(m, p.Emit, flat ? (screen ? ScreenGlow : LedGlow) : (screen ? PhysicalScreen : PhysicalLed));
                if (p.Role == 2)
                {
                    int color = p.Name.Contains("amber") ? 1 : p.Name.Contains("blue") ? 2 : 0;
                    _traffic.Add((m, on, p.Group, color));
                    _trafficByName[m.name] = (on, p.Group, color);
                    _known.Add(m);
                }
            }
            _mats[key] = m;
            return m;
        }

        /// <summary>HDRP takes the glow as a linear colour (SetVector: no gamma conversion); other shaders their own way.</summary>
        static Vector4 Glow(Material m, Color c, float k)
        {
            var lin = c.linear;
            var on = new Vector4(lin.r * k, lin.g * k, lin.b * k, 1f);
            if (m.HasProperty("_EmissiveColor")) m.SetVector("_EmissiveColor", on);
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 2.5f); }
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return on;
        }

        static int _tickFrame = -1;
        static bool _tickLogged;
        static float _nextProbe;
        static float _tickNext;
        static readonly float[] _phase = { 0.13f, 0.41f, 0.67f, 0.89f };
        static readonly float[] _speed = { 5.1f, 3.3f, 7.7f, 2.4f };

        /// <summary>The traffic lights' clock: every 80 ms each group is on or off after its own rhythm (the same for every
        /// rack, but four groups per colour look like busy ports). Runs once a frame, whichever rack calls it.</summary>
        public static void Tick()
        {
            if (_tickFrame == Time.frameCount) return;
            _tickFrame = Time.frameCount;
            // GO-LIGHTS-2 (user 2026-11-08: "kein Blinken"): driven every frame by Core.Tick as well, not only by the copies of
            // the racks (ModelLights), and on the real clock - the game clock stands still in a pause
            float t = Time.unscaledTime;
            if (_traffic.Count == 0 || t < _tickNext) return;
            _tickNext = t + 0.08f;
            if (!_tickLogged && t >= _nextProbe)
            {   // every 20 s until pieces stand in the world, then once: how many have LED slots the clock switches
                _nextProbe = t + 20f;
                int pieces = 0, blinking = 0;
                try
                {
                    _scanned.RemoveWhere(x => x == null);
                    var withSlots = new HashSet<Renderer>();
                    foreach (var sl in _slots) if (sl.r != null) withSlots.Add(sl.r);
                    foreach (var ml in UnityEngine.Object.FindObjectsOfType<ModelLights>())
                    {
                        pieces++;
                        foreach (var r in ml.GetComponentsInChildren<Renderer>()) if (withSlots.Contains(r)) { blinking++; break; }
                    }
                }
                catch { }
                if (pieces > 0)
                {
                    _tickLogged = true;
                    Go.Log($"rack lights: {_slots.Count} LED slots switched like the cars' brake lights; {pieces} pieces in the world, {blinking} of them blink (GO-LIGHTS-4).");
                }
            }
            for (int i = _traffic.Count - 1; i >= 0; i--)
            {   // the shared materials (the copies kept for placing)
                var (m, on, g, color) = _traffic[i];
                if (m != null && m.HasProperty("_EmissiveColor")) m.SetVector("_EmissiveColor", Led(on, g, color, t));
            }
            _mpb ??= new MaterialPropertyBlock();
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                var (r, index, on, g, color) = _slots[i];
                if (r == null) { _slots.RemoveAt(i); continue; }   // the piece is gone
                if (!r.isVisible) continue;
                r.GetPropertyBlock(_mpb, index);
                _mpb.SetVector(EmissiveId, Led(on, g, color, t));
                r.SetPropertyBlock(_mpb, index);
            }
        }

        static Vector4 Led(Vector4 on, int g, int color, float t)
        {
            float wave = Mathf.PerlinNoise(t * _speed[g & 3], _phase[g & 3] * 10f + color * 3.1f);
            return wave > 0.42f ? on : on * LedOff;
        }

        /// <summary>GO-LIGHTS-4: a placed piece's LED slots - every material slot of its renderers whose material is (a copy of)
        /// one of our blinking LED materials (by the original's name; the game names a copy "... (Instance)").</summary>
        public static void Adopt(Component piece)
        {
            if (_trafficByName.Count == 0) return;
            foreach (var r in piece.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || _scanned.Contains(r)) continue;
                _scanned.Add(r);
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    string name = m.name;
                    while (name.EndsWith(" (Instance)")) name = name.Substring(0, name.Length - " (Instance)".Length);
                    if (_trafficByName.TryGetValue(name, out var def)) _slots.Add((r, i, def.on, def.group, def.color));
                }
            }
        }
    }

    /// <summary>On every placed piece with its own model (GO-MODELS-1): drives the shared traffic lights' clock.</summary>
    internal sealed class ModelLights : MonoBehaviour
    {
        float _adoptAt;

        void Update()
        {
            try
            {
                if (Time.unscaledTime >= _adoptAt) { _adoptAt = Time.unscaledTime + 5f; Models.Adopt(this); }   // GO-LIGHTS-3: again later, the game may swap them
                Models.Tick();
            }
            catch { }
        }
    }
}
