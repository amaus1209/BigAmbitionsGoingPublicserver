using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using GoingOnline;
using UnityEngine;

namespace BigAmbitionsMP
{
    /// <summary>CAR-LIVERY-1 (user 2026-11-10: "In Public Going jede Seite des Fahrzeugs könnte man dort einstellen, was für
    /// Farbwahl und Logo-Plätze, nachdem man das Auto gekauft hat"): a livery of its own for every vehicle the player owns,
    /// set after the purchase - for the left, the right, the back and the front each a look (nothing, logo and name, a colour
    /// panel, a colour band, the whole side in the colour; Going Online's panels on its service van), its colour and where
    /// the logo stands; for the vehicle the business whose logo and name it shows, the name, a slogan and a contact line.
    /// Cars of the game wear it as decals (CarWrap.cs), the service van painted into its own texture (goingonline/core).
    /// Other players see it (VanLiveries, MessageType.VanLivery). This file: the livery, the player's own ones and where
    /// they are kept - in the save, one entry "gps:carlivery:" + base64(gzip(JSON)) in
    /// GameInstance.CollapsedCitymapFilterCategories (as Going Online keeps its provider: the game only asks that list
    /// whether a map filter group is folded).</summary>
    internal sealed class CarLivery
    {
        public string Biz = "", Name = "", Slogan = "", Contact = "";
        /// <summary>Left, right, back, front.</summary>
        public LiveryPaint.Look[] Sides = { new LiveryPaint.Look(), new LiveryPaint.Look(), new LiveryPaint.Look(), new LiveryPaint.Look() };

        public bool Any
        {
            get { foreach (var l in Sides) if (l != null && l.Design != LiveryPaint.DNone) return true; return false; }
        }

        public CarLivery Copy()
        {
            var c = new CarLivery { Biz = Biz, Name = Name, Slogan = Slogan, Contact = Contact };
            for (int i = 0; i < 4; i++) c.Sides[i] = (Sides[i] ?? new LiveryPaint.Look()).Copy();
            return c;
        }

        /// <summary>"2|biz|name|slogan|contact|left|right|back|front", a side as design.colour.place; "|" in texts as "¦".</summary>
        public string Pack()
        {
            var sb = new StringBuilder("2|");
            sb.Append(Esc(Biz)).Append('|').Append(Esc(Name)).Append('|').Append(Esc(Slogan)).Append('|').Append(Esc(Contact));
            foreach (var l in Sides) sb.Append('|').Append((l ?? new LiveryPaint.Look()).Key());
            return sb.ToString();
        }

        public static CarLivery? Unpack(string? s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var p = s!.Split('|');
            if (p.Length < 9 || p[0] != "2") return null;
            try
            {
                var c = new CarLivery { Biz = Cut(p[1], 60), Name = Cut(p[2], 40), Slogan = Cut(p[3], 40), Contact = Cut(p[4], 60) };
                for (int i = 0; i < 4; i++)
                {
                    var q = p[5 + i].Split('.');
                    if (q.Length < 3) return null;
                    c.Sides[i] = new LiveryPaint.Look
                    {
                        Design = Mathf.Clamp(int.Parse(q[0], CultureInfo.InvariantCulture), 0, LiveryPaint.DCount - 1),
                        Color = int.Parse(q[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture) & 0xFFFFFF,
                        Place = Mathf.Clamp(int.Parse(q[2], CultureInfo.InvariantCulture), 0, 2),
                    };
                    if (q.Length >= 5)
                    {   // GO-LIVERY-4: the lettering's and the logo's size
                        c.Sides[i].Text = Mathf.Clamp(int.Parse(q[3], CultureInfo.InvariantCulture), 50, 200);
                        c.Sides[i].Logo = Mathf.Clamp(int.Parse(q[4], CultureInfo.InvariantCulture), 50, 200);
                    }
                }
                return c;
            }
            catch { return null; }
        }

        static string Esc(string t) => (t ?? "").Replace('|', '¦');
        static string Cut(string t, int n) => t.Length > n ? t.Substring(0, n) : t;
    }

    internal static class CarLiveries
    {
        const string Prefix = "gps:carlivery:";
        public const string VanType = GoingOnline.Van.TypeName;

        static readonly Dictionary<string, CarLivery> _own = new Dictionary<string, CarLivery>();
        static GameInstance? _game;
        static bool _hooked;
        static int _logged;

        /// <summary>Rises with every change: the cars look again, the others hear of it.</summary>
        internal static int Version;

        /// <summary>MAIN THREAD, every frame in the game (MPCanvasUI): the liveries of a newly loaded game, the save hook,
        /// Going Online's question for a van's own livery, the cars' decals.</summary>
        internal static void Tick()
        {
            if (Application.isBatchMode) return;
            GameInstance? g = null;
            try { g = SaveGameManager.Current; } catch { }
            if (!ReferenceEquals(g, _game))
            {
                _game = g;
                _own.Clear();
                if (g != null) Load(g);
                Version++;
            }
            if (!_hooked)
            {
                _hooked = true;
                GlobalEvents.onSaveGame += OnSave;
                GoingOnline.Livery.PerVehicle = VanLook;
            }
            if (g != null) CarWraps.Tick();
        }

        // ── the player's own liveries ───────────────────────────────────────────────────────────────────────────────

        internal static CarLivery? Get(string vid) => vid != null && _own.TryGetValue(vid, out var l) ? l : null;

        internal static void Set(string vid, CarLivery? l)
        {
            if (string.IsNullOrEmpty(vid)) return;
            if (l == null) _own.Remove(vid); else _own[vid] = l;
            Version++;
            GoingOnline.Livery.Version++;
        }

        /// <summary>Every own vehicle's livery as sent to the others (vehicle id -> pack); the sold ones are left out.</summary>
        internal static Dictionary<string, string> Packs()
        {
            var d = new Dictionary<string, string>();
            var have = OwnIds();
            foreach (var kv in _own)
                if (have.Contains(kv.Key) && kv.Value != null) d[kv.Key] = kv.Value.Pack();
            return d;
        }

        static HashSet<string> OwnIds()
        {
            var set = new HashSet<string>();
            try
            {
                var list = SaveGameManager.Current?.VehicleInstances;
                if (list != null) foreach (var v in list) if (v != null && !string.IsNullOrEmpty(v.id)) set.Add(v.id);
            }
            catch { }
            return set;
        }

        /// <summary>The vehicles a livery can go on: not the hand truck, the flatbed, the scooter or the delivery job's van.</summary>
        internal static bool Eligible(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return false;
            string t = typeName.ToLowerInvariant();
            return !(t.Contains("handtruck") || t.Contains("flatbed") || t.Contains("scooter") || t.Contains("deliveryjob")
                     || t.Contains("privatedriver") || t.Contains("refuelrepair") || t.Contains("alternatetravel"));
        }

        /// <summary>A first livery for a vehicle: the service van as the company's van looks in Going Online, every other
        /// car a colour panel on both sides and the logo and the name on the back.</summary>
        internal static CarLivery Proposal(string vid, string typeName)
        {
            var l = new CarLivery { Biz = DefaultBiz(vid, typeName == VanType) };
            int col = GoingOnline.Livery.Palette[0];
            if (typeName == VanType)
            {
                var st = GoingOnline.Core.S?.Style;
                int motif = st != null ? Mathf.Clamp(st.Motif, 0, 3) : 0;
                if (st != null) col = st.Panel & 0xFFFFFF;
                int d = motif == 1 ? LiveryPaint.DGoTech : motif == 2 ? LiveryPaint.DLogoName : motif == 3 ? LiveryPaint.DNone : LiveryPaint.DGoFibre;
                l.Sides[0] = new LiveryPaint.Look { Design = d, Color = col, Place = 2 };
                l.Sides[1] = new LiveryPaint.Look { Design = d, Color = col, Place = 2 };
                l.Sides[2] = new LiveryPaint.Look { Design = d == LiveryPaint.DLogoName ? d : d == LiveryPaint.DNone ? d : LiveryPaint.DGoFibre, Color = col, Place = 1 };
                l.Sides[3] = new LiveryPaint.Look { Design = d == LiveryPaint.DNone ? d : LiveryPaint.DLogoName, Color = col, Place = 1 };
                return l;
            }
            l.Sides[0] = new LiveryPaint.Look { Design = LiveryPaint.DPanel, Color = col, Place = 1 };
            l.Sides[1] = new LiveryPaint.Look { Design = LiveryPaint.DPanel, Color = col, Place = 1 };
            l.Sides[2] = new LiveryPaint.Look { Design = LiveryPaint.DLogoName, Color = col, Place = 1 };
            l.Sides[3] = new LiveryPaint.Look { Design = LiveryPaint.DNone, Color = col, Place = 1 };
            return l;
        }

        // ── the businesses and their logos ──────────────────────────────────────────────────────────────────────────

        /// <summary>The player's businesses by name (their logos can go on a car), the cable provider first.</summary>
        internal static List<string> Businesses()
        {
            var names = new List<string>();
            try
            {
                var regs = SaveGameManager.Current?.BuildingRegistrations;
                if (regs != null)
                    foreach (var reg in regs)
                    {
                        if (reg == null || string.IsNullOrWhiteSpace(reg.BusinessName)) continue;
                        bool mine = false;
                        try { mine = MergerFlip.TrulyMine(reg); } catch { }
                        if (!mine) continue;
                        string n = reg.BusinessName.Trim();
                        if (!names.Contains(n)) names.Add(n);
                    }
            }
            catch { }
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            string go = GoCompany();
            if (go.Length > 0 && names.Remove(go)) names.Insert(0, go);
            return names;
        }

        static string GoCompany()
        {
            try { var s = GoingOnline.Core.S; return s != null ? GoingOnline.Livery.CompanyName(s) : ""; } catch { return ""; }
        }

        /// <summary>Whose logo a vehicle shows first: the service van the cable provider's; a vehicle in a warehouse's slot
        /// that warehouse's business; else the headquarters, else the first business.</summary>
        static string DefaultBiz(string vid, bool van)
        {
            var all = Businesses();
            if (all.Count == 0) return "";
            if (van) { string go = GoCompany(); if (go.Length > 0 && all.Contains(go)) return go; }
            try
            {
                var regs = SaveGameManager.Current?.BuildingRegistrations;
                if (regs != null)
                {
                    foreach (var reg in regs)
                        if (reg is Entities.Warehouse w && w.vehicleSlots != null && !string.IsNullOrWhiteSpace(w.BusinessName))
                            foreach (var slot in w.vehicleSlots)
                                if (slot != null && slot.vehicleInstanceId == vid && all.Contains(w.BusinessName.Trim())) return w.BusinessName.Trim();
                    foreach (var reg in regs)
                        if (reg != null && reg.businessTypeName == "ba:businesstype_headquarters" && !string.IsNullOrWhiteSpace(reg.BusinessName)
                            && all.Contains(reg.BusinessName.Trim())) return reg.BusinessName.Trim();
                }
            }
            catch { }
            return all[0];
        }

        /// <summary>A business's logo (the game's square sign of it) and a key that changes with it; null until the game
        /// has made it.</summary>
        internal static Texture? LogoOf(string biz, out string key)
        {
            key = "";
            if (string.IsNullOrEmpty(biz)) return null;
            Texture2D? t = null;
            try { t = LogoHelper.GetBusinessLogoTexture(biz, LogoSize.SquareSign, true); } catch { }
            if (t == null || t.width < 16) return null;
            key = "biz:" + biz + ":" + t.GetInstanceID();
            return t;
        }

        /// <summary>The name a livery shows: its own, else the business's.</summary>
        internal static string NameOf(CarLivery l) => l.Name.Trim().Length > 0 ? l.Name.Trim() : l.Biz.Trim();

        // ── Going Online's service van ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Going Online asks for a van's own livery (Livery.PerVehicle): the player's own van from here, another
        /// player's from what they sent. The company's look stays where a van has none.</summary>
        static LiveryPaint.Spec? VanLook(GameObject car, LiveryPaint.Spec? company, ref Texture? logo, ref string logoKey)
        {
            if (car == null) return null;
            string vid = "", owner = "";
            if (VehicleManager.GhostOf(car, out var gv, out var go)) { vid = gv; owner = go; }
            else
            {
                var vc = car.GetComponentInParent<VehicleController>();
                string id = vc != null && vc.vehicleInstance != null ? vc.vehicleInstance.id ?? "" : "";
                if (id.Length == 0 || id.StartsWith("BAMP_", StringComparison.Ordinal)) return null;
                vid = id;
            }
            var l = owner.Length == 0 ? Get(vid) : VanLiveries.CarOf(owner, vid);
            if (l == null) return null;
            var s = company != null ? company.Clone() : new LiveryPaint.Spec { Services = 0, Tech = false, Cable = false };
            s.Sides = new LiveryPaint.Look[4];
            for (int i = 0; i < 4; i++) s.Sides[i] = (l.Sides[i] ?? new LiveryPaint.Look()).Copy();
            string name = NameOf(l);
            if (name.Length > 0) s.Name = name;
            if (l.Slogan.Trim().Length > 0) s.Slogan = l.Slogan.Trim();
            else if (company == null || (l.Biz.Length > 0 && l.Biz != GoCompanyOf(owner))) s.Slogan = "";
            if (l.Contact.Trim().Length > 0) s.Contact = l.Contact.Trim();
            else if (company == null) s.Contact = "";
            Texture? lg; string lk;
            if (owner.Length == 0) lg = LogoOf(l.Biz, out lk);
            else lg = VanLiveries.LogoOf(owner, l.Biz, out lk);
            if (lg != null) { logo = lg; logoKey = lk; }
            s.Logo = logo != null;
            return s;
        }

        /// <summary>The cable provider's name of the player (own: "") - only the own one is known here.</summary>
        static string GoCompanyOf(string owner) => owner.Length == 0 ? GoCompany() : "";

        // ── in the save ─────────────────────────────────────────────────────────────────────────────────────────────

        static void Load(GameInstance game)
        {
            try
            {
                var list = game.CollapsedCitymapFilterCategories;
                if (list == null) return;
                foreach (var e in list)
                {
                    if (e == null || !e.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                    var map = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(Unzip(e.Substring(Prefix.Length)));
                    if (map == null) continue;
                    foreach (var kv in map)
                    {
                        var l = CarLivery.Unpack(kv.Value);
                        if (l != null && !string.IsNullOrEmpty(kv.Key)) _own[kv.Key] = l;
                    }
                }
                if (_own.Count > 0) Plugin.Logger.LogInfo($"[CarLivery] {_own.Count} vehicle livery(ies) read from the save (CAR-LIVERY-1).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[CarLivery] the saved liveries could not be read: {ex.Message} (CAR-LIVERY-1)."); }
        }

        /// <summary>The game is about to write the save (GlobalEvents.onSaveGame, inside SaveGameManager.Save): the liveries
        /// of the vehicles the player still has go in.</summary>
        static void OnSave()
        {
            try
            {
                var game = SaveGameManager.Current;
                if (game == null || !ReferenceEquals(game, _game)) return;
                var list = game.CollapsedCitymapFilterCategories;
                if (list == null) { list = new List<string>(); game.CollapsedCitymapFilterCategories = list; }
                list.RemoveAll(e => e != null && e.StartsWith(Prefix, StringComparison.Ordinal));
                var packs = Packs();
                if (packs.Count == 0) return;
                list.Add(Prefix + Zip(Newtonsoft.Json.JsonConvert.SerializeObject(packs)));
                if (_logged++ < 3) Plugin.Logger.LogInfo($"[CarLivery] {packs.Count} vehicle livery(ies) into the save (CAR-LIVERY-1).");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[CarLivery] save: {ex.Message} (CAR-LIVERY-1)."); }
        }

        static string Zip(string json)
        {
            var raw = Encoding.UTF8.GetBytes(json);
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) gz.Write(raw, 0, raw.Length);
            return Convert.ToBase64String(ms.ToArray());
        }

        static string Unzip(string b64)
        {
            var raw = Convert.FromBase64String(b64);
            using var ms = new MemoryStream(raw);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gz.CopyTo(outMs);
            return Encoding.UTF8.GetString(outMs.ToArray());
        }
    }
}
