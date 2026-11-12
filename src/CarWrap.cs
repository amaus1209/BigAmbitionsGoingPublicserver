using System.Collections.Concurrent;
using GoingOnline;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace BigAmbitionsMP
{
    /// <summary>CAR-LIVERY-1: on every vehicle of the player and of the other players. Once a second it checks which
    /// livery the vehicle should wear and puts a decal on each side that has a look (CarWraps).</summary>
    internal sealed class CarWrap : MonoBehaviour
    {
        internal string Vid = "", Owner = "", Type = "";
        internal string Key = "";
        internal readonly GameObject?[] Decals = new GameObject?[4];
        internal readonly string[] DecalKeys = { "", "", "", "" };
        internal float Next;
        internal int Version = -1;

        void Update()
        {
            float now = Time.unscaledTime;
            if (Version != CarLiveries.Version + VanLiveries.CarsVersion) { Version = CarLiveries.Version + VanLiveries.CarsVersion; Next = 0f; }
            if (now < Next) return;
            Next = now + 1f + UnityEngine.Random.Range(0f, 0.15f);
            try { CarWraps.Refresh(this); } catch (Exception ex) { CarWraps.WarnOnce("refresh: " + ex.Message); }
        }

        void OnDestroy() { try { CarWraps.Release(this); } catch { } }
    }

    /// <summary>CAR-LIVERY-1: the cars of the game wear a livery as HDRP decals (the game supports them in every quality
    /// level, with decal layers; its car shaders SH_Vehicle* receive them, its glass does not - checked in the game's files
    /// 2026-11-10). Where a decal may go is read from the car's own model once per vehicle type (CarAreas): the largest
    /// free part of each side's surface facing that way - no window, no lamp (the model's emission masks), no wheel, not
    /// the ends - the back's, and the hood in front of the windscreen. The decals sit on decal layer 7, and the car's body
    /// renderers get that layer, so with decal layers on only the body takes them (not the tyres or the inside).
    /// The pictures are painted by Going Online's LiveryPaint on a worker thread (RGBA, transparent ground), made into a
    /// texture and an HDRP/Decal material on the main thread (one a frame) and kept while a car wears them.</summary>
    internal static class CarWraps
    {
        static readonly string[] SideNames = { "Left", "Right", "Back", "Front" };
        const uint LayerBit = 1u << 15;   // decal layer 7: the renderer's rendering layers 8..15 are the decal layers (HDRP 14)

        static float _scanAt;
        static bool _warned, _shaderTried;
        static Shader? _decalShader;
        static int _painted, _logged;

        // ── finding the vehicles ────────────────────────────────────────────────────────────────────────────────────

        /// <summary>MAIN THREAD (CarLiveries.Tick): every 2 s the player's and the other players' vehicles get their CarWrap;
        /// the pictures painted on the worker thread become materials.</summary>
        internal static void Tick()
        {
            Collect();
            float now = Time.unscaledTime;
            if (now < _scanAt) return;
            _scanAt = now + 2f;
            try
            {
                foreach (var vc in Helpers.VehicleHelper.AllPlayerVehicles)
                {
                    if (vc == null || vc.vehicleInstance == null) continue;
                    string id = vc.vehicleInstance.id ?? "";
                    if (id.Length == 0 || id.StartsWith("BAMP_", StringComparison.Ordinal)) continue;   // another player's: below
                    Attach(vc.gameObject, id, "", vc.vehicleInstance.vehicleTypeName ?? "");
                }
            }
            catch (Exception ex) { WarnOnce("own vehicles: " + ex.Message); }
            try
            {
                foreach (var (vid, owner, type, go) in VehicleManager.GhostList()) Attach(go, vid, owner, type);
            }
            catch (Exception ex) { WarnOnce("other players' vehicles: " + ex.Message); }
            Trim();
        }

        static void Attach(GameObject go, string vid, string owner, string type)
        {
            if (go == null || vid.Length == 0) return;
            if (type == CarLiveries.VanType || !CarLiveries.Eligible(type)) return;   // the service van paints its own texture
            var w = go.GetComponent<CarWrap>();
            if (w == null)
            {
                if (go.GetComponent<CarFeatures>() == null) return;
                w = go.AddComponent<CarWrap>();
            }
            if (w.Vid != vid || w.Owner != owner) { w.Vid = vid; w.Owner = owner; w.Key = ""; w.Next = 0f; }
            w.Type = type;
        }

        // ── one car ─────────────────────────────────────────────────────────────────────────────────────────────────

        internal static void Refresh(CarWrap w)
        {
            var l = w.Owner.Length == 0 ? CarLiveries.Get(w.Vid) : VanLiveries.CarOf(w.Owner, w.Vid);
            var cf = w.GetComponent<CarFeatures>();
            if (l == null || !l.Any || cf == null) { Clear(w); return; }
            int paint = PaintOf(cf);
            string lk;
            Texture? logo = w.Owner.Length == 0 ? CarLiveries.LogoOf(l.Biz, out lk) : VanLiveries.LogoOf(w.Owner, l.Biz, out lk);
            var areas = CarAreas.For(w.gameObject, cf, w.Type);
            if (areas == null) { Clear(w); return; }
            string key = l.Pack() + "|" + paint.ToString("X6") + "|" + lk + "|" + areas.Stamp;
            if (key == w.Key) { Touch(w); return; }
            bool pending = false;
            for (int i = 0; i < 4; i++)
            {
                var look = l.Sides[i];
                var ar = areas.Side[i];
                string dk = look == null || look.Design == LiveryPaint.DNone || ar == null ? "" : SideKey(l, look, paint, lk, ar);
                if (dk == w.DecalKeys[i] && (dk.Length == 0 || w.Decals[i] != null)) continue;
                if (dk.Length == 0) { Remove(w, i); continue; }
                var mat = MaterialFor(dk, l, look!, paint, logo, lk, ar!, out bool wait);
                if (wait) { pending = true; continue; }   // still being painted: next round
                if (mat == null) { Remove(w, i); w.DecalKeys[i] = dk; continue; }   // it could not be made: none there
                Place(w, i, ar!, mat);
                w.DecalKeys[i] = dk;
            }
            Layer(cf);
            if (pending) { w.Next = Time.unscaledTime + 0.2f; return; }
            w.Key = key;
        }

        static string SideKey(CarLivery l, LiveryPaint.Look look, int paint, string logoKey, CarArea a)
            => look.Key() + "|" + CarLiveries.NameOf(l) + "|" + l.Slogan + "|" + l.Contact + "|" + paint.ToString("X6") + "|"
               + logoKey + "|"
               + a.W.ToString("0.00") + "x" + a.H.ToString("0.00") + "|" + a.FrontAt + a.Kind;

        static int PaintOf(CarFeatures cf)
        {
            var vc = cf.VehicleColor;
            if (vc == null) return 0x9A9A9A;
            Color32 t = vc.tint;
            return (t.r << 16) | (t.g << 8) | t.b;
        }

        static void Place(CarWrap w, int i, CarArea a, Material mat)
        {
            var go = w.Decals[i];
            DecalProjector? dp = null;
            if (go == null)
            {
                go = new GameObject("GPS_CarLivery_" + SideNames[i]);
                go.transform.SetParent(w.transform, false);
                go.layer = w.gameObject.layer;
                dp = go.AddComponent<DecalProjector>();
                w.Decals[i] = go;
            }
            else dp = go.GetComponent<DecalProjector>();
            if (dp == null) return;
            go.transform.localPosition = a.Pos;
            go.transform.localRotation = a.Rot;
            float s = Mathf.Max(0.01f, (w.transform.lossyScale.x + w.transform.lossyScale.y + w.transform.lossyScale.z) / 3f);
            dp.scaleMode = DecalScaleMode.ScaleInvariant;
            dp.size = new Vector3(a.W * s, a.H * s, a.Depth * s);
            dp.pivot = new Vector3(0f, 0f, a.Depth * s * 0.5f);
            dp.material = mat;
            dp.decalLayerMask = DecalLayerEnum.DecalLayer7;
            dp.drawDistance = 80f;
            dp.fadeScale = 0.9f;
            dp.startAngleFade = 60f;
            dp.endAngleFade = 88f;
            dp.fadeFactor = 1f;
        }

        static void Remove(CarWrap w, int i)
        {
            if (w.Decals[i] != null) UnityEngine.Object.Destroy(w.Decals[i]);
            w.Decals[i] = null;
            w.DecalKeys[i] = "";
        }

        static void Clear(CarWrap w)
        {
            for (int i = 0; i < 4; i++) Remove(w, i);
            w.Key = "";
        }

        internal static void Release(CarWrap w) => Clear(w);

        /// <summary>The body's renderers take decal layer 7 (and keep what they had) - not the wheels, which the game lists
        /// with the body (their rims take the paint).</summary>
        static void Layer(CarFeatures cf)
        {
            var body = cf.bodyMeshes;
            if (body == null) return;
            foreach (var r in body)
                if (r != null && !CarAreas.IsWheel(r.gameObject.name) && (r.renderingLayerMask & LayerBit) == 0) r.renderingLayerMask |= LayerBit;
        }

        // ── the pictures and their materials ────────────────────────────────────────────────────────────────────────

        sealed class Entry { public Texture2D? Tex; public Material? Mat; public float Used; }
        static readonly Dictionary<string, Entry> _mats = new Dictionary<string, Entry>();
        static readonly HashSet<string> _painting = new HashSet<string>();
        static readonly ConcurrentQueue<(string key, byte[]? px, int w, int h, long ms)> _done = new ConcurrentQueue<(string, byte[]?, int, int, long)>();
        static float _trimAt;

        /// <summary>The decal material for a side's look: from the cache, or null while its picture is painted (a worker
        /// thread paints it; the next round finds it).</summary>
        static Material? MaterialFor(string key, CarLivery l, LiveryPaint.Look look, int paint, Texture? logo, string logoKey, CarArea a, out bool wait)
        {
            wait = false;
            if (_mats.TryGetValue(key, out var e)) { e.Used = Time.unscaledTime; return e.Mat; }
            wait = true;
            if (_painting.Contains(key)) return null;
            if (!GoingOnline.Livery.Art(out var font, out var art) || font == null || art == null) { WarnOnce("the livery's font or pictures are missing"); wait = false; return null; }
            if (_painting.Count >= 3) return null;   // a few at a time
            var img = logo != null ? GoingOnline.Livery.LogoPixels(logoKey, logo) : null;
            var spec = new LiveryPaint.Spec
            {
                Name = CarLiveries.NameOf(l), Slogan = l.Slogan.Trim(), Contact = l.Contact.Trim(), Paint = paint & 0xFFFFFF,
                Logo = img != null, Fallback = "", Services = 0, Tech = false, Cable = false,
            };
            var lk = look.Copy();
            float wM = a.W, hM = a.H;
            int frontAt = a.FrontAt, kind = a.Kind;
            _painting.Add(key);
            Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                byte[]? px = null; int w = 0, h = 0;
                try { px = LiveryPaint.ComposeDecal(spec, lk, wM, hM, frontAt, kind, font, art, img, 256f, out w, out h); }
                catch { px = null; }
                _done.Enqueue((key, px, w, h, sw.ElapsedMilliseconds));
            });
            return null;
        }

        /// <summary>MAIN THREAD: the painted pictures become textures and materials (one a frame).</summary>
        static void Collect()
        {
            if (!_done.TryDequeue(out var d)) return;
            _painting.Remove(d.key);
            Texture2D? tex = null;
            Material? mat = null;
            if (d.px != null && d.w > 0 && d.h > 0)
            {
                try
                {
                    tex = new Texture2D(d.w, d.h, TextureFormat.RGBA32, true, false)
                    {
                        name = "GPS_CarLivery", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4,
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    tex.SetPixelData(d.px, 0);
                    tex.Apply(true, false);
                    if ((d.w & 3) == 0 && (d.h & 3) == 0) { try { tex.Compress(false); } catch { } }
                    tex.Apply(false, true);
                    mat = DecalMaterial(tex);
                }
                catch (Exception ex) { WarnOnce("texture: " + ex.Message); }
            }
            if (mat == null && tex != null) { UnityEngine.Object.Destroy(tex); tex = null; }
            _mats[d.key] = new Entry { Tex = tex, Mat = mat, Used = Time.unscaledTime };
            _painted++;
            if (_painted <= 3 || _painted % 50 == 0)
                Plugin.Logger.LogInfo($"[CarLivery] decal painted ({d.w}x{d.h} px in {d.ms} ms, {(tex != null ? tex.format.ToString() : "none")}); {_painted} so far, {_mats.Count} kept (CAR-LIVERY-1).");
        }

        /// <summary>An HDRP decal material that changes only the colour (the variant "_COLORMAP _MATERIAL_AFFECTS_ALBEDO" is
        /// in the game's build) - set up as HDRP's DecalAPI.SetupCommonDecalMaterialKeywordsAndPass would.</summary>
        static Material? DecalMaterial(Texture2D tex)
        {
            if (!_shaderTried)
            {
                _shaderTried = true;
                _decalShader = Shader.Find("HDRP/Decal");
                Plugin.Logger.LogInfo(_decalShader != null ? "[CarLivery] HDRP's decal shader found (CAR-LIVERY-1)." : "[CarLivery] HDRP's decal shader is missing - the cars stay without livery (CAR-LIVERY-1).");
            }
            if (_decalShader == null) return null;
            var m = new Material(_decalShader) { name = "GPS_CarLivery", hideFlags = HideFlags.HideAndDontSave };
            m.SetTexture("_BaseColorMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_AffectAlbedo", 1f);
            m.SetFloat("_AffectNormal", 0f);
            m.SetFloat("_AffectMetal", 0f);
            m.SetFloat("_AffectAO", 0f);
            m.SetFloat("_AffectSmoothness", 0f);
            m.SetFloat("_AffectEmission", 0f);
            m.SetFloat("_DecalBlend", 1f);
            m.EnableKeyword("_COLORMAP");
            m.EnableKeyword("_MATERIAL_AFFECTS_ALBEDO");
            foreach (var k in new[] { "_MATERIAL_AFFECTS_NORMAL", "_MATERIAL_AFFECTS_MASKMAP", "_NORMALMAP", "_MASKMAP", "_EMISSIVEMAP" }) m.DisableKeyword(k);
            m.SetInt("_DecalColorMask0", 15);   // ColorWriteMask.All: the colour
            m.SetInt("_DecalColorMask1", 0);
            m.SetInt("_DecalColorMask2", 0);
            m.SetInt("_DecalColorMask3", 0);
            m.SetShaderPassEnabled("DBufferProjector", true);
            m.SetShaderPassEnabled("DBufferMesh", true);
            m.SetShaderPassEnabled("DecalProjectorForwardEmissive", false);
            m.SetShaderPassEnabled("DecalMeshForwardEmissive", false);
            m.SetInt("_DecalStencilWriteMask", 16);
            m.SetInt("_DecalStencilRef", 16);
            m.renderQueue = 2000;
            m.enableInstancing = true;
            return m;
        }

        static void Touch(CarWrap w)
        {
            float now = Time.unscaledTime;
            foreach (var k in w.DecalKeys) if (k.Length > 0 && _mats.TryGetValue(k, out var e)) e.Used = now;
        }

        /// <summary>Pictures no car wore for two minutes go (beyond the 32 newest).</summary>
        static void Trim()
        {
            float now = Time.unscaledTime;
            if (now < _trimAt || _mats.Count <= 32) return;
            _trimAt = now + 15f;
            var worn = new HashSet<string>();
            foreach (var w in UnityEngine.Object.FindObjectsOfType<CarWrap>()) foreach (var k in w.DecalKeys) if (k.Length > 0) worn.Add(k);
            foreach (var key in new List<string>(_mats.Keys))
            {
                var e = _mats[key];
                if (worn.Contains(key) || now - e.Used < 120f) continue;
                if (e.Mat != null) UnityEngine.Object.Destroy(e.Mat);
                if (e.Tex != null) UnityEngine.Object.Destroy(e.Tex);
                _mats.Remove(key);
            }
        }

        internal static void WarnOnce(string what)
        {
            if (_warned) return;
            _warned = true;
            Plugin.Logger.LogWarning("[CarLivery] " + what + " (CAR-LIVERY-1).");
        }

        internal static void LogOnce(string what) { if (_logged++ < 40) Plugin.Logger.LogInfo("[CarLivery] " + what + " (CAR-LIVERY-1)."); }
    }

    /// <summary>Where a decal goes on a car (in the vehicle's own space): the projector's place and turn (it projects along
    /// its +z into the car), the picture's size in metres and the depth of the box. FrontAt: where the car's front is in the
    /// picture (0 left, 2 right, 1 none); Kind: 0 a side, 1 the back, 2 the hood.</summary>
    internal sealed class CarArea
    {
        public Vector3 Pos;
        public Quaternion Rot;
        public float W, H, Depth;
        public int FrontAt = 1, Kind;
    }

    internal sealed class CarAreaSet
    {
        public readonly CarArea?[] Side = new CarArea?[4];
        public string Stamp = "";
    }

    /// <summary>CAR-LIVERY-1: the free surface of each side of a car, read from its model once per vehicle type
    /// (CarAreaMath does the reading; this collects the model's triangles in the car's space).</summary>
    internal static class CarAreas
    {
        static readonly Dictionary<string, CarAreaSet?> _cache = new Dictionary<string, CarAreaSet?>();
        static readonly Dictionary<int, LightMask?> _lights = new Dictionary<int, LightMask?>();

        internal static CarAreaSet? For(GameObject car, CarFeatures cf, string type)
        {
            var body = cf.bodyMeshes;
            if (body == null || body.Length == 0) return null;
            var sb = new System.Text.StringBuilder(type);
            foreach (var r in body)
            {
                var mf = r != null ? r.GetComponent<MeshFilter>() : null;
                if (mf != null && mf.sharedMesh != null) sb.Append('|').Append(mf.sharedMesh.name).Append(':').Append(mf.sharedMesh.vertexCount);
            }
            string key = sb.ToString();
            if (_cache.TryGetValue(key, out var have)) return have;
            CarAreaSet? set = null;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try { set = Analyse(car.transform, cf); }
            catch (Exception ex) { CarWraps.WarnOnce("reading a car's model: " + ex.Message); }
            _cache[key] = set;
            if (set != null) set.Stamp = key.GetHashCode().ToString("X8");
            CarWraps.LogOnce($"free surface of {(type.Length > 0 ? type : car.name)} in {sw.ElapsedMilliseconds} ms: "
                + (set == null ? "none (model not readable)" : string.Join(", ", new[] { "left", "right", "back", "hood" }.Select((n, i) =>
                    n + " " + (set.Side[i] == null ? "-" : set.Side[i]!.W.ToString("0.00") + " x " + set.Side[i]!.H.ToString("0.00") + " m")))));
            return set;
        }

        static CarAreaSet? Analyse(Transform root, CarFeatures cf)
        {
            var tris = new List<CarAreaMath.Tri>(8192);
            var toRoot = root.worldToLocalMatrix;
            foreach (var r in cf.bodyMeshes)
            {
                if (!(r is MeshRenderer) || r == null || IsWheel(r.gameObject.name)) continue;   // the wheels: their boxes below
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null || !mesh.isReadable) continue;
                var m = toRoot * r.transform.localToWorldMatrix;
                bool flip = m.determinant < 0f;
                var v = mesh.vertices;
                var uv = mesh.uv;
                bool hasUv = uv != null && uv.Length == v.Length;
                var mats = r.sharedMaterials;
                for (int sm = 0; sm < mesh.subMeshCount; sm++)
                {
                    var mat = sm < mats.Length ? mats[sm] : null;
                    int kind = KindOf(mat);
                    if (kind < 0) continue;
                    var lamps = kind == 0 && hasUv ? LampsOf(mat!) : null;
                    var idx = mesh.GetTriangles(sm);
                    for (int t = 0; t + 2 < idx.Length; t += 3)
                    {
                        int i0 = idx[t], i1 = idx[t + 1], i2 = idx[t + 2];
                        var a = m.MultiplyPoint3x4(v[i0]); var b = m.MultiplyPoint3x4(v[i1]); var c = m.MultiplyPoint3x4(v[i2]);
                        var n = Vector3.Cross(b - a, c - a);
                        float len = n.magnitude;
                        if (len < 1e-9f) continue;
                        n /= len;
                        if (flip) n = -n;
                        int k = kind;
                        if (k == 0 && lamps != null && lamps.Lit((uv[i0] + uv[i1] + uv[i2]) / 3f)) k = 2;
                        tris.Add(new CarAreaMath.Tri { A = V(a), B = V(b), C = V(c), N = V(n), Kind = k });
                    }
                }
            }
            if (tris.Count < 50) return null;
            // the wheels (their own renderers): their boxes in the car's space
            var wheels = new List<CarAreaMath.Box>();
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr == null) continue;
                if (!IsWheel(mr.gameObject.name)) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var lb = mf.sharedMesh.bounds;
                var mm = toRoot * mr.transform.localToWorldMatrix;
                var wb = new Bounds(mm.MultiplyPoint3x4(lb.center), Vector3.zero);
                for (int c = 0; c < 8; c++)
                    wb.Encapsulate(mm.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
                wheels.Add(new CarAreaMath.Box { Min = V(wb.min), Max = V(wb.max) });
            }
            var found = CarAreaMath.Find(tris, wheels);
            var set = new CarAreaSet();
            for (int i = 0; i < 4; i++) set.Side[i] = Convert(found[i]);
            return set;
        }

        static CarAreaMath.V3 V(Vector3 p) => new CarAreaMath.V3(p.x, p.y, p.z);

        /// <summary>A wheel by its object's name ("FrontLeft", "SM_..._Front Left Wheel", "BMW_Wheel_BackLeft").</summary>
        internal static bool IsWheel(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            return n.Contains("wheel") || n == "frontleft" || n == "frontright" || n == "rearleft" || n == "rearright" || n.Contains("tyre") || n.Contains("tire");
        }

        /// <summary>The projector's turn for each direction: it projects along its +z; the picture's right is its +x.</summary>
        static CarArea? Convert(CarAreaMath.Area? a)
        {
            if (a == null) return null;
            Quaternion rot = a.Dir switch
            {
                0 => Quaternion.LookRotation(Vector3.right, Vector3.up),     // the left side: the picture's right is the car's back
                1 => Quaternion.LookRotation(Vector3.left, Vector3.up),      // the right side: the picture's right is the car's front
                2 => Quaternion.LookRotation(Vector3.forward, Vector3.up),   // the back: the picture's right is the car's right
                _ => Quaternion.LookRotation(Vector3.down, Vector3.back),    // the hood: the picture's top towards the windscreen
            };
            return new CarArea { Pos = new Vector3(a.Pos.X, a.Pos.Y, a.Pos.Z), Rot = rot, W = a.W, H = a.H, Depth = a.Depth, FrontAt = a.FrontAt, Kind = a.Kind };
        }

        /// <summary>0 the body (the game's car shaders), 1 glass, -1 anything else (shadow casters, boxes).</summary>
        static int KindOf(Material? mat)
        {
            if (mat == null) return -1;
            string sh = mat.shader != null ? mat.shader.name : "";
            string n = mat.name ?? "";
            if (n.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0 || mat.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")) return 1;
            if (sh.IndexOf("SH_Vehicle", StringComparison.OrdinalIgnoreCase) >= 0) return 0;
            if (mat.IsKeywordEnabled("_DISABLE_DECALS")) return 1;
            return -1;
        }

        /// <summary>Where a car's lamps are: its emission and indicator masks (read through a render texture).</summary>
        sealed class LightMask
        {
            public LiveryPaint.Img? A, B;
            public bool Lit(Vector2 uv) => On(A, uv) || On(B, uv);
            static bool On(LiveryPaint.Img? m, Vector2 uv)
            {
                if (m == null) return false;
                float u = uv.x - Mathf.Floor(uv.x), v = uv.y - Mathf.Floor(uv.y);
                int x = Mathf.Clamp((int)(u * m.W), 0, m.W - 1), y = Mathf.Clamp((int)(v * m.H), 0, m.H - 1);
                int i = (y * m.W + x) * 4;
                return Math.Max(m.Px[i], Math.Max(m.Px[i + 1], m.Px[i + 2])) > 60;
            }
        }

        static LightMask? LampsOf(Material mat)
        {
            int id = mat.GetInstanceID();
            if (_lights.TryGetValue(id, out var have)) return have;
            LightMask? lm = null;
            try
            {
                var a = mat.HasProperty("_EmissionMask") ? mat.GetTexture("_EmissionMask") : null;
                var b = mat.HasProperty("_IndicatorsMask") ? mat.GetTexture("_IndicatorsMask") : null;
                if (a != null || b != null) lm = new LightMask { A = GoingOnline.Livery.ReadTexture(a), B = GoingOnline.Livery.ReadTexture(b) };
            }
            catch { lm = null; }
            _lights[id] = lm;
            return lm;
        }
    }
}
