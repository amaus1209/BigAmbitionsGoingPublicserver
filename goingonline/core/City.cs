using System.Collections.Generic;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>A building of the city as the provider sees it: where it stands and how many customers it can have. The
    /// game's totalSqm is the whole building (residential median 1,448 m², offices 8,258 m², read from the game files
    /// 2026-11-06), so the size of a house decides its households - the player's "je nach Größe".</summary>
    public sealed class House
    {
        public string Key = "";
        public string Hood = "";
        public string Type = "";
        public int Sqm;
        public byte Kind;
        public int Homes, Biz;
        public Vector3 Pos;
        public CityBuildingController? Cbc;

        public int Units => Homes + Biz;
        public bool Connectable => Units > 0;
    }

    internal static class City
    {
        public static readonly List<House> All = new List<House>();
        public static readonly Dictionary<string, House> ByKey = new Dictionary<string, House>();
        public static readonly List<string> Hoods = new List<string>();
        static CityManager? _for;

        /// <summary>The key a building is known by in the save: "8 ba:street_firststreet" (Going Public Server's form too).</summary>
        public static string KeyOf(Address? a) => a == null ? "" : a.streetNumber + " " + a.streetName;

        /// <summary>Reads the city once per city scene. False while it is not there yet.</summary>
        public static bool Ensure()
        {
            CityManager? cm = null;
            try { cm = InstanceBehavior<CityManager>.Instance; } catch { }
            if (cm == null || cm.cityBuildingControllers == null || cm.cityBuildingControllers.Length == 0) return false;
            if (ReferenceEquals(cm, _for) && All.Count > 0) return true;
            All.Clear(); ByKey.Clear(); Hoods.Clear();
            foreach (var cbc in cm.cityBuildingControllers)
            {
                try
                {
                    if (cbc == null || cbc.building == null) continue;
                    var bd = cbc.building;
                    var b = new House
                    {
                        Key = KeyOf(bd.Address), Hood = bd.Neighbourhood ?? "", Type = bd.BuildingType ?? "", Sqm = bd.totalSqm, Cbc = cbc,
                    };
                    try
                    {
                        var door = cbc.entranceDoors != null && cbc.entranceDoors.Length > 0 ? cbc.entranceDoors[0] : null;
                        b.Pos = door != null && door.doorTransform != null ? door.doorTransform.position : cbc.transform.position;
                    }
                    catch { b.Pos = cbc.transform.position; }
                    Size(b);
                    if (b.Key.Length == 0 || ByKey.ContainsKey(b.Key)) continue;
                    All.Add(b);
                    ByKey[b.Key] = b;
                    if (b.Hood.Length > 0 && b.Connectable && !Hoods.Contains(b.Hood)) Hoods.Add(b.Hood);
                }
                catch { }
            }
            Hoods.Sort();
            _for = cm;
            int homes = 0, biz = 0;
            foreach (var b in All) { homes += b.Homes; biz += b.Biz; }
            Go.Log($"city read: {All.Count} buildings, {Hoods.Count} neighbourhoods, {homes} households, {biz} businesses.");
            return All.Count > 0;
        }

        public static void Forget() { All.Clear(); ByKey.Clear(); Hoods.Clear(); _for = null; }

        /// <summary>Households and business units by the building's kind and size (KONZEPT.md "Die Stadt").</summary>
        static void Size(House b)
        {
            bool hamptons = b.Hood.EndsWith("thehamptons");
            switch (b.Type)
            {
                case "ba:buildingtype_residential":
                    if (hamptons) { b.Kind = Cat.KindVilla; b.Homes = 1; }
                    else { b.Kind = Cat.KindHome; b.Homes = Mathf.Max(1, Mathf.RoundToInt(b.Sqm / 90f)); }
                    break;
                case "ba:buildingtype_retail":
                    b.Kind = Cat.KindRetail;
                    b.Homes = hamptons ? 0 : Mathf.RoundToInt(b.Sqm * 0.6f / 90f);
                    b.Biz = 1;
                    break;
                case "ba:buildingtype_office":
                    b.Kind = Cat.KindOffice; b.Biz = Mathf.Max(1, Mathf.RoundToInt(b.Sqm / 450f));
                    break;
                case "ba:buildingtype_warehouse":
                    b.Kind = Cat.KindWarehouse; b.Biz = 1;
                    break;
            }
        }

        /// <summary>The address as the game writes it ("8 1st Street").</summary>
        public static string Name(string key)
        {
            if (ByKey.TryGetValue(key, out var b) && b.Cbc != null && b.Cbc.building != null)
                try { return Streets.AddressHelper.ToFormattedString(b.Cbc.building.Address); } catch { }
            return key;
        }

        public static string HoodName(string id) => Go.Game(id);

        /// <summary>The building's registration (who rents and owns it), null if the city has none for it.</summary>
        public static BuildingRegistration? Reg(string key) =>
            ByKey.TryGetValue(key, out var b) && b.Cbc != null ? b.Cbc.buildingRegistration : null;

        /// <summary>The player rents or owns it.</summary>
        public static bool Mine(string key)
        {
            try
            {
                var r = Reg(key);
                return r != null && (r.RentedByPlayer || r.BuildingOwnedByPlayer);
            }
            catch { return false; }
        }

        /// <summary>An office the player rents or owns: where a headend may stand (GO-OFFICE-1: headend and customer center
        /// in one office; before 0.7 a warehouse too).</summary>
        public static IEnumerable<House> HeadendChoices()
        {
            foreach (var b in All)
                if (b.Type == "ba:buildingtype_office" && Mine(b.Key)) yield return b;
        }

        public static bool BusinessName(string key, out string name)
        {
            name = "";
            try
            {
                var r = Reg(key);
                if (r != null && !string.IsNullOrWhiteSpace(r.BusinessName)) { name = r.BusinessName; return true; }
            }
            catch { }
            return false;
        }

        /// <summary>The middle of a neighbourhood's connected buildings (where technicians drive for a node).</summary>
        public static Vector3 HoodCenter(IspState s, string hood)
        {
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var site in s.Sites.Values)
                if (site.Hood == hood && ByKey.TryGetValue(site.Key, out var b)) { sum += b.Pos; n++; }
            return n > 0 ? sum / n : Vector3.zero;
        }

        public static Vector3 PosOf(string key) => ByKey.TryGetValue(key, out var b) ? b.Pos : Vector3.zero;

        /// <summary>Points the game's navigation at a building (as the city map's "Set destination" does).</summary>
        public static bool Navigate(string key)
        {
            try
            {
                if (!ByKey.TryGetValue(key, out var b) || b.Cbc == null || b.Cbc.building == null) return false;
                UI.Guiders.GuidersManager.SetGuiderTarget(b.Cbc.building.Address, UI.Guiders.DirectionGuiderType.Destination);
                return true;
            }
            catch (System.Exception ex) { Go.Warn("navigate: " + ex.Message); return false; }
        }

        /// <summary>The building in a neighbourhood that is nearest to its middle (a node's place for the navigation).</summary>
        public static string HoodSite(IspState s, string hood)
        {
            var c = HoodCenter(s, hood);
            string best = ""; float d = float.MaxValue;
            foreach (var site in s.Sites.Values)
            {
                if (site.Hood != hood || !ByKey.TryGetValue(site.Key, out var b)) continue;
                float dd = (b.Pos - c).sqrMagnitude;
                if (dd < d) { d = dd; best = site.Key; }
            }
            return best;
        }
    }
}
