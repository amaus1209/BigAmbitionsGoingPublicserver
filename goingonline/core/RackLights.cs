using UnityEngine;

namespace GoingOnline
{
    /// <summary>The equipment's front lights (user 2026-11-06: "sind die Server mit Animation?"): a column of small LEDs on
    /// the front and the back of the rack that blink on their own - mostly green and blue, now and then amber - the way a
    /// running rack does. The game's cabinet the equipment is made from has none. Built when the piece stands in the world;
    /// the model template (an inactive copy) never starts.</summary>
    internal sealed class RackLights : MonoBehaviour
    {
        static Material? _green, _blue, _amber;
        GameObject[]? _leds;
        float[]? _next;

        void Start()
        {
            try
            {
                var rs = GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) return;
                // the piece's box in its own space
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), max = -min;
                foreach (var r in rs)
                {
                    var b = r.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                        var l = transform.InverseTransformPoint(c);
                        min = Vector3.Min(min, l); max = Vector3.Max(max, l);
                    }
                }
                if (max.y - min.y < 0.3f) return;
                Materials(rs[0].sharedMaterial);
                const int Rows = 7, Cols = 3;
                _leds = new GameObject[Rows * Cols * 2];
                _next = new float[_leds.Length];
                float w = max.x - min.x, h = max.y - min.y;
                int n = 0;
                foreach (float z in new[] { max.z + 0.004f, min.z - 0.004f })
                    for (int row = 0; row < Rows; row++)
                        for (int col = 0; col < Cols; col++)
                        {
                            var led = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            var col3d = led.GetComponent("BoxCollider");
                            if (col3d != null) Destroy(col3d);   // a light, nothing to bump into (by name: the stand-alone build has no physics reference)
                            led.name = "GO_Led";
                            led.transform.SetParent(transform, false);
                            led.transform.localScale = new Vector3(0.022f, 0.012f, 0.006f);
                            led.transform.localPosition = new Vector3(min.x + w * (0.62f + 0.09f * col), min.y + h * (0.86f - 0.105f * row), z);
                            var mr = led.GetComponent<MeshRenderer>();
                            if (mr != null)
                            {
                                mr.sharedMaterial = (row + col) % 5 == 4 ? _amber : (row + col) % 2 == 0 ? _green : _blue;
                                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                                mr.receiveShadows = false;
                            }
                            _leds[n] = led;
                            _next[n] = Time.unscaledTime + UnityEngine.Random.Range(0.05f, 1.2f);
                            n++;
                        }
            }
            catch { _leds = null; }
        }

        void Update()
        {
            if (_leds == null || _next == null) return;
            float now = Time.unscaledTime;
            for (int i = 0; i < _leds.Length; i++)
            {
                if (now < _next[i] || _leds[i] == null) continue;
                bool on = !_leds[i].activeSelf;
                _leds[i].SetActive(on);
                // short flickers for traffic, long glows for links
                _next[i] = now + (on ? UnityEngine.Random.Range(0.06f, (i % 3 == 0) ? 2.5f : 0.6f) : UnityEngine.Random.Range(0.04f, 0.35f));
            }
        }

        /// <summary>Glowing copies of the rack's own material (its shader is the game's).</summary>
        static void Materials(Material? source)
        {
            if (_green != null || source == null) return;
            _green = Glow(source, new Color(0.25f, 1f, 0.35f));
            _blue = Glow(source, new Color(0.30f, 0.65f, 1f));
            _amber = Glow(source, new Color(1f, 0.70f, 0.15f));
        }

        static Material Glow(Material source, Color c)
        {
            var m = new Material(source) { name = "GO_LedGlow" };
            foreach (var prop in new[] { "_BaseColor", "_Color" }) if (m.HasProperty(prop)) m.SetColor(prop, c);
            foreach (var tex in new[] { "_BaseMap", "_MainTex" }) if (m.HasProperty(tex)) m.SetTexture(tex, null);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor("_EmissionColor", c * 2.5f);
            }
            // GO-LIGHTS-1: HDRP glows through _EmissiveColor; the exposure weight 0 keeps the LEDs visible in daylight
            if (m.HasProperty("_EmissiveColor"))
            {
                var lin = c.linear;
                m.SetVector("_EmissiveColor", new Vector4(lin.r * 3f, lin.g * 3f, lin.b * 3f, 1f));
                if (m.HasProperty("_EmissiveExposureWeight")) m.SetFloat("_EmissiveExposureWeight", 0f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }
    }
}
