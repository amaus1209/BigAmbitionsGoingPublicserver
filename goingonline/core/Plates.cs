using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GO-PLATE-1 (user 2026-11-10: "Auto-Kennzeichen GO-amaus1212 ... GO1-amaus1212 für Servicewagen 1, GO2-amaus1212
    /// für Servicewagen 2 usw."): the service van's two number plates show a picture of their own - "GO", the van's number
    /// and the player's name. The plates are the model's "card_white" part, which has texture coordinates since this model
    /// (0..1 across each plate, the right way round from outside); the picture goes onto that part of the van's renderer by
    /// a property block, so the livery's big texture stays shared between the vans.
    /// - The player's own vans count 1, 2, 3 ... in the order they were bought; a van keeps its number (IspState.VanNumbers,
    ///   in the save), and the number of a van that is sold is free again for the next one.
    /// - Another player's van (Going Public Server) shows that player's own plate (they send their plates with their
    ///   livery: Livery.RemotePlate), else "GO-" and their name; the traffic's vans and the dealer's showroom van show
    ///   "GO-" and the player's name.
    /// - The name: Going Public Server's player name, else the Steam name, else the company's.
    /// Main thread.</summary>
    internal static class Plates
    {
        public const string Part = "card_white";
        const int W = 512, H = 104;
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, int> _loose = new Dictionary<string, int>();   // numbers without a provider (not kept)
        static MaterialPropertyBlock? _mpb;
        static readonly int BaseMap = Shader.PropertyToID("_BaseColorMap"), BaseColor = Shader.PropertyToID("_BaseColor");
        static string _steam = "\u0001";
        static bool _logged;

        /// <summary>The name on the plates.</summary>
        internal static string Name()
        {
            string n = "";
            try { n = Livery.PlayerName?.Invoke() ?? ""; } catch { }
            if (n.Trim().Length == 0) n = SteamName();
            if (n.Trim().Length == 0 && Core.S != null) n = Livery.CompanyName(Core.S);
            n = n.Trim();
            return n.Length > 18 ? n.Substring(0, 18) : n;
        }

        /// <summary>The Steam name (the game's Facepunch.Steamworks, by reflection: nothing else of Steam is used).</summary>
        static string SteamName()
        {
            if (_steam != "\u0001") return _steam;
            _steam = "";
            try
            {
                var t = System.Type.GetType("Steamworks.SteamClient, Facepunch.Steamworks.Win64");
                var p = t?.GetProperty("Name", BindingFlags.Public | BindingFlags.Static);
                _steam = p?.GetValue(null) as string ?? "";
            }
            catch { _steam = ""; }
            return _steam;
        }

        static Dictionary<string, int> Map() => Core.S?.VanNumbers ?? _loose;

        static bool Own(VehicleInstance v, IspState? s)
            => v != null && v.vehicleTypeName == Van.TypeName && !string.IsNullOrEmpty(v.id)
               && !v.id.StartsWith("BAMP_", System.StringComparison.Ordinal) && (s == null || !s.LeaseIds.Contains(v.id));

        /// <summary>The number of one of the player's own vans (1, 2, ...): kept once given; sold vans free theirs.</summary>
        internal static int Number(string id)
        {
            var map = Map();
            if (map.TryGetValue(id, out int n)) return n;
            var g = SaveGameManager.Current;
            var s = Core.S;
            if (g?.VehicleInstances != null)
            {
                var have = new HashSet<string>();
                foreach (var v in g.VehicleInstances) if (v != null && !string.IsNullOrEmpty(v.id)) have.Add(v.id);
                foreach (var k in new List<string>(map.Keys)) if (!have.Contains(k)) map.Remove(k);
                // the vans there already get theirs in the order they were bought (the game's list keeps that order)
                foreach (var v in g.VehicleInstances)
                    if (Own(v, s) && !map.ContainsKey(v.id)) map[v.id] = Lowest(map);
            }
            if (!map.TryGetValue(id, out n)) { n = Lowest(map); map[id] = n; }
            return n;
        }

        static int Lowest(Dictionary<string, int> map)
        {
            var used = new HashSet<int>(map.Values);
            int n = 1;
            while (used.Contains(n)) n++;
            return n;
        }

        internal static string OwnText(string id) => "GO" + Number(id) + "-" + Name();

        /// <summary>The player's own vans' plates (vehicle id -> text), for Going Public Server to send to the others.</summary>
        internal static Dictionary<string, string> Local()
        {
            var d = new Dictionary<string, string>();
            var g = SaveGameManager.Current;
            if (g?.VehicleInstances == null) return d;
            var s = Core.S;
            foreach (var v in g.VehicleInstances)
                if (Own(v, s) && d.Count < 40) d[v.id] = OwnText(v.id);
            return d;
        }

        /// <summary>What this van's plates say.</summary>
        static string TextFor(VanSkin k)
        {
            string? owner = null;
            try { owner = Livery.OwnerOf?.Invoke(k.gameObject); } catch { }
            if (owner != null)
            {   // another player's van
                string? sent = null;
                try { sent = Livery.RemotePlate?.Invoke(k.gameObject); } catch { }
                if (!string.IsNullOrEmpty(sent)) return sent!;
                return owner.Length > 0 && owner != "?" ? "GO-" + owner : "GO";
            }
            string id = "";
            if (!k.Showroom && k.FixedPaint < 0)
                try { var vc = k.GetComponent<VehicleController>(); id = vc != null && vc.vehicleInstance != null ? vc.vehicleInstance.id ?? "" : ""; } catch { }
            string name = Name();
            if (id.Length == 0 || id.StartsWith("BAMP_", System.StringComparison.Ordinal)) return name.Length > 0 ? "GO-" + name : "GO";
            return OwnText(id);
        }

        /// <summary>Van.Skin, about once a second per van: its plates, painted once per text.</summary>
        internal static void Apply(VanSkin k, MeshRenderer mr, Models.Model model)
        {
            int slot = model.PartIndex(Part);
            if (slot < 0 || slot >= mr.sharedMaterials.Length) return;
            string text = TextFor(k);
            if (text == k.Plate) return;
            var tex = Texture(text);
            if (tex == null) return;
            _mpb ??= new MaterialPropertyBlock();
            mr.GetPropertyBlock(_mpb, slot);
            _mpb.SetTexture(BaseMap, tex);
            _mpb.SetColor(BaseColor, Color.white);
            mr.SetPropertyBlock(_mpb, slot);
            k.Plate = text;
            if (!_logged) { _logged = true; Go.Log($"number plates: \"{text}\" on a service van (GO-PLATE-1)."); }
        }

        static Texture2D? Texture(string text)
        {
            if (_tex.TryGetValue(text, out var have) && have != null) return have;
            if (!Livery.Art(out var font, out _) || font == null) return null;
            var px = LiveryPaint.ComposePlate(text, font, W, H);
            var t = new Texture2D(W, H, TextureFormat.RGB24, true) { name = "GO_Plate", hideFlags = HideFlags.HideAndDontSave };
            t.SetPixelData(px, 0);   // the top level only - Apply(true) makes the rest (LoadRawTextureData wants every level)
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 4;
            t.Apply(true, true);
            _tex[text] = t;   // a few per player and van - kept for the session
            return t;
        }
    }
}
