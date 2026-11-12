using System.Collections;
using System.Reflection;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>The cable provider in the game's own pages (user 2026-11-07, after the first test with a provider of their
    /// own): the city map's filter (GO-MAP-1), and the numbers and texts BizMan shows for it - the Harmony patches of
    /// GamePatches.cs put them in place, in both builds since 0.8.1 (GO-PATCHES-1).</summary>
    internal static class GameUi
    {
        const BindingFlags IF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>The multiplayer mod's patches for BizMan, the delivery contracts and the recruitment dialog are there
        /// (set by its module); the stand-alone mod has the game's warehouse pages.</summary>
        internal static bool InMultiplayerMod;

        // ── GO-MAP-1: the city map's filter ───────────────────────────────────────────────────────────────────────────

        static FieldInfo? _fMapFilters, _fInit, _fEntries, _fCatFilters;
        static MethodInfo? _mCreate;
        static bool _mapLogged, _mapWarned;

        /// <summary>GO-MAP-1 (user 2026-11-07: "Kabelanbieter sind nicht in der Liste"): the map's list of businesses
        /// (category "active businesses") holds the business types that make revenue in the game's sense - the cable
        /// provider is not one of them and is left out, and giving it that tag would also make the citizens wish for
        /// one in every neighbourhood and let the game simulate walk-in customers. So the two types get a row of their own,
        /// made with the game's own CreateFilter (a registered row: the game saves its tick and lights up the buildings of
        /// that type), at the end of that category; the row's button moves the map to the player's own building. Every two
        /// seconds while a city is open - cheap when the rows are there; a new map panel (a new game, a language change)
        /// gets them again.</summary>
        internal static void MapFilters()
        {
            if (SaveGameManager.Current == null) return;
            object? uis = null;
            try { uis = InstanceBehavior<global::UI.UIs>.Instance; } catch { }
            if (uis == null) return;
            _fMapFilters ??= typeof(global::UI.UIs).GetField("mapFilters", IF);
            if (!(_fMapFilters?.GetValue(uis) is CityMapFilters mf) || mf == null) return;
            _fInit ??= typeof(CityMapFilters).GetField("_initialized", IF);
            _fEntries ??= typeof(CityMapFilters).GetField("_filterEntries", IF);
            _mCreate ??= typeof(CityMapFilters).GetMethod("CreateFilter", IF);
            _fCatFilters ??= typeof(global::City.CityMap.CityMapFilterCategory).GetField("_filters", IF);
            if (_fInit == null || _fEntries == null || _mCreate == null)
            {
                if (!_mapWarned) { _mapWarned = true; Go.Warn("map filter: the game's map list has changed - no rows for the cable provider (GO-MAP-1)."); }
                return;
            }
            if (!(_fInit.GetValue(mf) is bool init) || !init) return;
            if (!(_fEntries.GetValue(mf) is IDictionary entries) || entries.Count == 0) return;
            var cat = mf.GetCategory("citymap_category_active_businesses");
            if (cat == null) return;
            int added = 0;
            foreach (var name in new[] { GameTypes.Cable, GameTypes.Center })
            {
                if (entries.Contains(name)) continue;
                if (name == GameTypes.Center && !AnyOf(name)) continue;   // GO-CENTERS-1: the customer centers' row once the player has one
                var t = Helpers.BusinessTypeHelper.GetData(name);
                if (t == null) continue;
                Transform? last = null;
                try
                {
                    if (_fCatFilters?.GetValue(cat) is IList rows && rows.Count > 0 && rows[rows.Count - 1] is Component c && c != null) last = c.transform;
                }
                catch { }
                var data = new global::City.CityMap.CityMapFilterData { businessTypeName = name };
                string key = name;
                System.Func<Vector3?> focus = () => Focus(key);
                var ps = _mCreate.GetParameters();
                var args = new object?[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                    args[i] = ps[i].ParameterType.IsValueType && !(ps[i].ParameterType.IsGenericType && ps[i].ParameterType.GetGenericTypeDefinition() == typeof(System.Nullable<>))
                        ? System.Activator.CreateInstance(ps[i].ParameterType) : null;
                args[0] = name; args[1] = t.icon; args[2] = data; args[3] = cat;
                if (ps.Length >= 7) args[6] = focus;
                var row = _mCreate.Invoke(mf, args) as Component;
                if (row != null && last != null && row.transform.parent == last.parent) row.transform.SetSiblingIndex(last.GetSiblingIndex() + 1);
                added++;
            }
            // GO-VAN-12: the leased vans' points on the map - a row of its own; the game keeps its tick, VanPins reads it
            if (!entries.Contains(VanPins.FilterName) && (Core.S?.LeaseVans ?? 0) > 0)
            {
                var icon = Icon.Load("GO_AppIcon.png");
                if (icon != null)
                {
                    Transform? last = null;
                    try { if (_fCatFilters?.GetValue(cat) is IList rows && rows.Count > 0 && rows[rows.Count - 1] is Component c && c != null) last = c.transform; } catch { }
                    var ps = _mCreate.GetParameters();
                    var args = new object?[ps.Length];
                    for (int i = 0; i < ps.Length; i++)
                        args[i] = ps[i].ParameterType.IsValueType && !(ps[i].ParameterType.IsGenericType && ps[i].ParameterType.GetGenericTypeDefinition() == typeof(System.Nullable<>))
                            ? System.Activator.CreateInstance(ps[i].ParameterType) : null;
                    args[0] = VanPins.FilterName; args[1] = icon; args[2] = null; args[3] = cat;
                    if (ps.Length >= 7) args[6] = (System.Func<Vector3?>)VanFocus;
                    var row = _mCreate.Invoke(mf, args) as Component;
                    if (row != null)
                    {
                        if (last != null && row.transform.parent == last.parent) row.transform.SetSiblingIndex(last.GetSiblingIndex() + 1);
                        VanPins.Hook(row);
                        Go.Log("map filter: the row for the leased vans' points (GO-VAN-12).");
                    }
                }
            }
            if (added > 0 && !_mapLogged) { _mapLogged = true; Go.Log($"map filter: {added} row(s) for the cable provider in the list of businesses (GO-MAP-1)."); }
        }

        /// <summary>GO-VAN-12b: the row's button - the next leased van (VanPins.Focus), else the office.</summary>
        static Vector3? VanFocus()
        {
            var van = VanPins.Focus();
            if (van != null) return van;
            var st = Core.S;
            if (st == null || string.IsNullOrEmpty(st.HeadendKey)) return null;
            var p = City.PosOf(st.HeadendKey);
            return p == Vector3.zero ? (Vector3?)null : p;
        }

        static bool AnyOf(string type)
        {
            var g = SaveGameManager.Current;
            if (g?.BuildingRegistrations == null) return false;
            foreach (var r in g.BuildingRegistrations) if (r != null && r.businessTypeName == type) return true;
            return false;
        }

        /// <summary>The player's own building of that type, for the row's button (none: the button does nothing).</summary>
        static Vector3? Focus(string type)
        {
            var s = Core.S;
            if (s == null) return null;
            string key = s.HeadendKey;
            if (type == GameTypes.Center)
            {   // GO-CENTERS-1: the customer center of the headend's neighbourhood, else any
                var here = Places.CentersIn(s, Places.HeadHood(s));
                key = here.Count > 0 ? here[0] : "";
                if (key.Length == 0) foreach (var k in Places.AllKeys(s)) if (!Places.IsOffice(s, k)) { key = k; break; }
            }
            if (string.IsNullOrEmpty(key)) return null;
            var p = City.PosOf(key);
            return p == Vector3.zero ? (Vector3?)null : p;
        }

        // ── GO-SUMMARY-1: the day's summary counts the cable provider ─────────────────────────────────────────────────

        static int _sumDay = -1, _sumCount;

        /// <summary>GO-SUMMARY-1 (user 2026-11-08: "Unternehmen 1, obwohl 2 vorhanden sind"): the game's daily summary counts the
        /// rented businesses whose type generates revenue (UI.DailySummary.DailySummary) - the cable provider's type does not
        /// carry that tag on purpose (it would bring the game's walk-in demand, pricing managers and marketing onto it), so it
        /// was missing. While the summary shows, its business count gets the provider's headend (the business with the sales)
        /// added. Every frame from Core.Tick; cheap while the summary is not shown.</summary>
        internal static void SummaryCount()
        {
            try
            {
                var ui = InstanceBehavior<UI.UIs>.Instance;
                var ds = ui != null ? ui.dailySummary : null;
                if (ds == null || ds.panel == null || ds.panel.alpha < 0.01f || ds.businessesLabel == null) return;
                var g = SaveGameManager.Current;
                if (g?.BuildingRegistrations == null) return;
                if (g.Day != _sumDay)
                {
                    _sumDay = g.Day; _sumCount = 0;
                    string head = Core.S?.HeadendKey ?? "";
                    foreach (var reg in g.BuildingRegistrations)
                    {
                        if (reg == null) continue;
                        bool ours = head.Length > 0 && reg.businessTypeName == GameTypes.Cable && City.KeyOf(reg.Address) == head && (reg.RentedByPlayer || reg.BuildingOwnedByPlayer);
                        bool game = false;
                        if (!ours && reg.RentedByPlayer)
                            try { var t = Helpers.BusinessTypeHelper.GetData(reg); game = t != null && t.HasTag(BigAmbitions.Tags.TagRef.Businesstag.generatesrevenue); } catch { }
                        if (ours || game) _sumCount++;
                    }
                }
                string want = _sumCount.ToString();
                if (ds.businessesLabel.text != want) ds.businessesLabel.text = want;
            }
            catch { }
        }

        // ── GO-BIZMAN-1: the provider's row in BizMan's list ──────────────────────────────────────────────────────────

        /// <summary>The type column of BizMan's list (user 2026-11-07: "ein separater Eintrag mit Kundenanzahl"): the type and,
        /// for the headend, the connections; null = not ours.</summary>
        internal static string? RowType(BuildingRegistration? reg)
        {
            if (reg == null || !GameTypes.IsOurs(reg.businessTypeName)) return null;
            string type = Go.Game(reg.businessTypeName);
            var s = Core.S;
            string key = City.KeyOf(reg.Address);
            if (s == null) return type;
            if (key == s.HeadendKey) return Go.T("go_bizman_row", "{0} ({1} connections)", type, Go.Num(Sim.Customers(s)));
            // GO-HUBS-1, GO-CENTERS-1: a hub or a customer center, with its neighbourhood
            string hood = City.ByKey.TryGetValue(key, out var b) ? City.HoodName(b.Hood) : "";
            return GameTypes.InOffice(reg) ? Go.T("go_bizman_row_hub", "{0} (hub {1})", type, hood) : Go.T("go_bizman_row_center", "{0} ({1})", type, hood);
        }

        /// <summary>The satisfaction column: the provider's own (the game has no customers of its own for these types and
        /// would show 0 %); -1 = not ours or no provider.</summary>
        internal static int RowSatisfaction(BuildingRegistration? reg)
        {
            if (reg == null || !GameTypes.IsOurs(reg.businessTypeName)) return -1;
            var s = Core.S;
            if (s == null) return -1;
            return Mathf.Clamp(Mathf.RoundToInt(s.Satisfaction), 0, 100);   // GO-HUBS-1: every building of the provider shows its satisfaction
        }

        // ── GO-FLEET-1: BizMan's fleet page ───────────────────────────────────────────────────────────────────────────

        internal sealed class FleetCard
        {
            public string CarId = "", Title = "", Vehicle = "";
            public readonly List<int> Ids = new List<int>();          // 0 = automatic, then the people who may drive it
            public readonly List<string> Labels = new List<string>();
            public int Selected;
        }

        /// <summary>The provider's headend is this registration (the fleet page and the other BizMan pages are for it) - since
        /// 0.7 the office that is headend and customer center in one.</summary>
        internal static bool IsHeadend(BuildingRegistration? reg)
        {
            var s = Core.S;
            return reg != null && s != null && GameTypes.IsOurs(reg.businessTypeName) && City.KeyOf(reg.Address) == s.HeadendKey;
        }

        /// <summary>One card per vehicle at the headend - the player's own (parked within 80 m) and the leased ones - with who
        /// drives it this hour and who may have it as their own.</summary>
        internal static List<FleetCard> FleetCards()
        {
            var list = new List<FleetCard>();
            var s = Core.S;
            if (s == null) return list;
            int n = 0;
            foreach (var c in Fleet.AtHeadend(s))
            {
                var card = new FleetCard { CarId = c.Id, Title = Go.T("go_fleet_slot", "Vehicle {0}", ++n) };
                var now = s.Techs.Find(t => t.CarId == c.Id);
                card.Vehicle = c.Name + (c.Van || c.Id.StartsWith("lease-") ? "" : " (" + Go.T("go_staff_car", "car") + ")")
                             + " - " + (now != null ? Go.T("go_fleet_now", "now: {0}", now.Name) : Go.T("go_fleet_free", "free now"));
                card.Ids.Add(0); card.Labels.Add(Go.T("go_fleet_auto", "Automatic (each shift)"));
                foreach (var t in s.Techs)
                {
                    if (!Fleet.CanDrive(t, c)) continue;
                    card.Ids.Add(t.Id);
                    card.Labels.Add(t.Name + " (" + Act.RoleName(t.Role) + ")");
                    if (t.Pin == c.Id) card.Selected = card.Ids.Count - 1;
                }
                list.Add(card);
            }
            return list;
        }

        internal static string FleetTitle => Go.T("go_fleet_title", "Fleet");
        internal static string FleetPinLabel => Go.T("go_fleet_pin", "Assigned to");
        internal static string FleetNone => Go.T("go_fleet_none2", "No vehicle yet - lease one in the Going Online app (Staff) or park your own at the office (within 80 m).");
        internal static string FleetNoneSlot => Go.T("go_fleet_none_slot", "No vehicle");

        /// <summary>A vehicle's own driver from the fleet page (0: automatic again).</summary>
        internal static void SetPin(string carId, int techId)
        {
            var s = Core.S;
            if (s == null) return;
            Fleet.Pin(s, carId, techId);
            var who = techId == 0 ? null : s.Techs.Find(t => t.Id == techId);
            Go.Log($"fleet: {carId} {(who != null ? "is now " + who.Name + "'s own" : "goes round again")} (GO-FLEET-1).");
        }
    }
}
