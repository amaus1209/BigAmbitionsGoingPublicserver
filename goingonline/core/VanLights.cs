using UnityEngine;

namespace GoingOnline
{
    /// <summary>GO-VAN-7 (user 2026-11-10 at the service van: "kein Bremslicht Funktion keine Blinker Funktion", "Front
    /// scheinwerfer ... fehlerhaft"): the van's own lamps follow what the game switches. The game does not switch lamps
    /// itself: it sets values on the car's body renderer for its vehicle shader, which reads them with the lamp masks of
    /// its texture - the driving physics (NWH lights of the player's car: _FrontLightStrength 50000 low beam,
    /// _BackLightStrength 20000 tail / 40000 brake, _ReverseLightStrength), the blinker keys (VehicleBlinker:
    /// _IsLeftBlinkerOn, _IsRightBlinkerOn on every body renderer, the beat _IsBlinkerOn on the first one) and, on traffic
    /// cars, Gley's VehicleLightsComponent with the same names. Our van's lamps are plain HDRP/Lit parts that read none of
    /// it - so the brake, tail and blinker lamps never lit, and the headlights glowed always like an LED (bright spots at
    /// night). This reads those values every frame and gives each lamp part its glow through a property block of its own
    /// material slot (the renderer's own block stays the game's). The blinkers: Models splits the van's amber lamps into
    /// the left ones ("lamp_amber") and the right ones ("lamp_amber_r").</summary>
    internal sealed class VanLights : MonoBehaviour
    {
        static readonly int Front = Shader.PropertyToID("_FrontLightStrength"), Back = Shader.PropertyToID("_BackLightStrength"),
            Reverse = Shader.PropertyToID("_ReverseLightStrength"), Beat = Shader.PropertyToID("_IsBlinkerOn"),
            Left = Shader.PropertyToID("_IsLeftBlinkerOn"), Right = Shader.PropertyToID("_IsRightBlinkerOn"),
            Emissive = Shader.PropertyToID("_EmissiveColor"), Weight = Shader.PropertyToID("_EmissiveExposureWeight");

        // the glow on screen (exposure weight 0, as the LEDs: GO-LIGHTS-1 - by day as by night)
        static readonly Color HeadColour = new Color(1f, 0.96f, 0.88f), RedColour = new Color(1f, 0.08f, 0.06f),
            AmberColour = new Color(1f, 0.52f, 0.05f), WhiteColour = new Color(1f, 1f, 1f);
        const float HeadOn = 3.5f, TailOn = 1.2f, BrakeOn = 4.5f, BlinkOn = 4.5f, ReverseOn = 3f;

        MeshRenderer? _mr;
        MeshFilter? _mf;
        Mesh? _mesh;
        Renderer? _beat;
        int _head = -1, _red = -1, _ambL = -1, _ambR = -1, _rev = -1;
        int _last = -1;
        MaterialPropertyBlock? _in, _out;
        Behaviour[]? _spotL, _spotR;   // GO-VAN-13: the headlight spot the game switches, and our copy that follows it

        /// <summary>On a renderer that now wears the van's body (Van.Dress).</summary>
        internal static void Attach(MeshRenderer mr, Models.Model model)
        {
            var l = mr.GetComponent<VanLights>();
            if (l == null) l = mr.gameObject.AddComponent<VanLights>();
            l.Setup(mr, model);
        }

        void Setup(MeshRenderer mr, Models.Model model)
        {
            _mr = mr;
            _mf = mr.GetComponent<MeshFilter>();
            _mesh = model.Mesh;
            _head = model.PartIndex("screen_headlight");
            _red = model.PartIndex("lamp_red");
            _ambL = model.PartIndex("lamp_amber");
            _ambR = model.PartIndex("lamp_amber_r");
            _rev = model.PartIndex("lamp_reverse");
            _beat = mr;
            try
            {
                var cf = mr.GetComponentInParent<CarFeatures>();
                if (cf != null && cf.bodyMeshes != null && cf.bodyMeshes.Length > 0 && cf.bodyMeshes[0] != null) _beat = cf.bodyMeshes[0];
            }
            catch { }
            _last = -1;
            _spotL = _spotR = null;
            try
            {
                var vc = mr.GetComponentInParent<VehicleController>();
                var root = vc != null ? vc.transform : mr.transform.root;
                var l = root.Find("Spotlights"); var r = root.Find(Van.RightSpot);
                if (l != null && r != null) { _spotL = l.GetComponents<Behaviour>(); _spotR = r.GetComponents<Behaviour>(); }
            }
            catch { }
            enabled = true;
        }

        /// <summary>GO-VAN-7 fix (the user 2026-11-10 on a3b1c55b: "Blinker Bremslicht keine Funktion"): the game makes every
        /// van as a copy of our prefab, and a copy keeps only serialized fields - this one's were all empty, so it did
        /// nothing. Each copy now finds its renderer and the van's parts itself.</summary>
        void Awake() { if (_mr == null) SelfSetup(); }

        void SelfSetup()
        {
            var mr = GetComponent<MeshRenderer>();
            var model = Models.Get(Van.Id);
            if (mr != null && model != null) Setup(mr, model);
        }

        void LateUpdate()
        {
            if (_mr == null)
            {
                SelfSetup();
                if (_mr == null) { enabled = false; return; }
            }
            if (_mf == null || _mf.sharedMesh != _mesh)
            {   // the car no longer wears the van (a traffic car given back): our glow goes, and so do we
                Clear();
                Destroy(this);
                return;
            }
            if (_spotL != null && _spotR != null)
                for (int i = 0; i < _spotL.Length && i < _spotR.Length; i++)
                    if (_spotL[i] != null && _spotR[i] != null && _spotR[i].enabled != _spotL[i].enabled) _spotR[i].enabled = _spotL[i].enabled;
            _in ??= new MaterialPropertyBlock();
            _mr.GetPropertyBlock(_in);
            float front = _in.GetFloat(Front), back = _in.GetFloat(Back), reverse = _in.GetFloat(Reverse);
            bool left = On(_in, Left), right = On(_in, Right);
            bool beat = false;
            if (left || right)
            {
                if (_beat != null && _beat != _mr) _beat.GetPropertyBlock(_in);
                beat = On(_in, Beat);
            }
            int state = (front > 1f ? 1 : 0) | (back > 30000f ? 2 : back > 1f ? 4 : 0) | (reverse > 1f ? 8 : 0)
                      | (left && beat ? 16 : 0) | (right && beat ? 32 : 0);
            if (state == _last) return;
            _last = state;
            Set(_head, HeadColour, (state & 1) != 0 ? HeadOn : 0f);
            Set(_red, RedColour, (state & 2) != 0 ? BrakeOn : (state & 4) != 0 ? TailOn : 0f);
            Set(_ambL, AmberColour, (state & 16) != 0 ? BlinkOn : 0f);
            Set(_ambR, AmberColour, (state & 32) != 0 ? BlinkOn : 0f);
            Set(_rev, WhiteColour, (state & 8) != 0 ? ReverseOn : 0f);
        }

        /// <summary>The blinker values come as SetInt (stored as a float) - read either kind.</summary>
        static bool On(MaterialPropertyBlock b, int id) => b.GetFloat(id) > 0.5f || b.GetInteger(id) > 0;

        void Set(int slot, Color c, float k)
        {
            if (_mr == null || slot < 0 || slot >= _mr.sharedMaterials.Length) return;
            _out ??= new MaterialPropertyBlock();
            _out.Clear();
            var lin = c.linear;
            _out.SetVector(Emissive, new Vector4(lin.r * k, lin.g * k, lin.b * k, 1f));
            _out.SetFloat(Weight, 0f);
            _mr.SetPropertyBlock(_out, slot);
        }

        void Clear()
        {
            if (_mr == null) return;
            foreach (int slot in new[] { _head, _red, _ambL, _ambR, _rev })
                if (slot >= 0 && slot < _mr.sharedMaterials.Length) _mr.SetPropertyBlock(null, slot);
        }

        void OnDestroy() { try { Clear(); } catch { } }
    }
}
