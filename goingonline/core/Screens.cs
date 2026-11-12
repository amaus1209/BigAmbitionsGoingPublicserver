using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace GoingOnline
{
    /// <summary>GO-SCREENS-1 (user 2026-11-08: "auf den Monitoren könnte auch irgendwas ablaufen, so als wenn die Mitarbeiter sich
    /// bei Going Online durchklicken"): while the player is in one of the provider's buildings, the monitors of Going Online's
    /// workplaces show the app - its bar and one of its pages with the provider's own numbers (orders and faults, the
    /// neighbourhoods' load, the overview, the staff), a marker moving down the rows as if someone clicked through, the page
    /// changing every few seconds (each monitor on its own rhythm). The workplaces are the game's desktop computer: its screen
    /// is the mesh's second part (material "ScreenMaterial", 61 x 34 cm, facing the person); a canvas lies on it.</summary>
    internal static class Screens
    {
        sealed class Screen
        {
            public GameObject Root = null!;
            public TextMeshProUGUI Title = null!;
            public TextMeshProUGUI[] Rows = null!;
            public Image Marker = null!;
            public Image[] Bars = null!;
            public int Page, Row;
            public float NextPage, NextRow;
        }

        const int RowCount = 6;
        static string _in = "";
        static readonly List<Screen> _screens = new List<Screen>();
        static readonly HashSet<int> _done = new HashSet<int>();
        static float _scanAt, _paintAt;
        static bool _warned;

        static readonly Color Back = new Color(0.06f, 0.08f, 0.11f, 1f), Bar = new Color(0.10f, 0.62f, 0.75f, 1f),
                              Mark = new Color(0.10f, 0.62f, 0.75f, 0.35f), Green = new Color(0.25f, 0.75f, 0.45f, 0.45f);

        /// <summary>Main thread, every frame (cheap outside the provider's buildings).</summary>
        public static void Tick()
        {
            if (Sim.Test) return;
            string key = "";
            BuildingManager? bm = null;
            try
            {
                if (BuildingManager.IsInsideBuilding && Core.S != null)
                {
                    bm = InstanceBehavior<BuildingManager>.Instance;
                    var reg = bm != null ? bm.buildingRegistration : null;
                    if (reg != null && GameTypes.IsOurs(reg.businessTypeName)) key = City.KeyOf(reg.Address);
                }
            }
            catch { key = ""; }
            if (key != _in) { Clear(); _in = key; _scanAt = 0f; }
            if (_in.Length == 0 || bm == null) return;
            try
            {
                float now = Time.unscaledTime;
                if (now >= _scanAt) { _scanAt = now + 3f; Scan(bm); }
                if (now >= _paintAt) { _paintAt = now + 0.25f; Paint(now); }
            }
            catch (System.Exception ex)
            {
                if (!_warned) { _warned = true; Go.Warn("monitors: " + ex.Message + " (GO-SCREENS-1)."); }
            }
        }

        public static void Clear()
        {
            foreach (var sc in _screens) if (sc.Root != null) UnityEngine.Object.Destroy(sc.Root);
            _screens.Clear(); _done.Clear();
        }

        static bool Workplace(string? item) => item == GameTypes.IDesk || item == GameTypes.ITechDesk || item == GameTypes.IControlDesk;

        static void Scan(BuildingManager bm)
        {
            foreach (var ic in bm.allItemControllers)
            {
                if (ic == null || !Workplace(ic.itemName) || _done.Contains(ic.GetInstanceID())) continue;
                _done.Add(ic.GetInstanceID());
                foreach (var mr in ic.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mats = mr.sharedMaterials;
                    for (int i = 0; i < mats.Length && i < mf.sharedMesh.subMeshCount; i++)
                    {
                        if (mats[i] == null || !mats[i].name.StartsWith("ScreenMaterial")) continue;
                        var sc = Build(mr.transform, mf.sharedMesh.GetSubMesh(i).bounds, mf.sharedMesh.bounds.center);
                        if (sc != null) _screens.Add(sc);
                    }
                }
            }
        }

        static Screen? Build(Transform parent, Bounds screen, Vector3 body)
        {
            float w = screen.size.x, h = screen.size.y;
            if (w < 0.1f || h < 0.05f) return null;
            bool front = body.z >= screen.center.z;   // the keyboard side is the person's side
            var root = new GameObject("GO_Monitor");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(screen.center.x, screen.center.y, screen.center.z + (front ? 0.003f : -0.003f));
            root.transform.localRotation = front ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)root.transform).sizeDelta = new Vector2(800f, 450f);
            root.transform.localScale = new Vector3(w / 800f, h / 450f, 1f);
            var back = Lobby.Child<Image>(root.transform, "back", Vector2.zero, new Vector2(800f, 450f));
            back.color = Back;
            var bar = Lobby.Child<Image>(root.transform, "bar", new Vector2(0f, 190f), new Vector2(800f, 70f));
            bar.color = Bar;
            var app = Lobby.Label(root.transform, "app", new Vector2(-230f, 190f), new Vector2(320f, 60f), 34f, FontStyles.Bold);
            app.text = "Going Online";
            var title = Lobby.Label(root.transform, "title", new Vector2(170f, 190f), new Vector2(420f, 60f), 30f, FontStyles.Normal);
            title.alignment = TextAlignmentOptions.MidlineRight;
            var marker = Lobby.Child<Image>(root.transform, "marker", new Vector2(0f, 110f), new Vector2(780f, 52f));
            marker.color = Mark;
            var rows = new TextMeshProUGUI[RowCount];
            var bars = new Image[RowCount];
            for (int i = 0; i < RowCount; i++)
            {
                float y = 110f - i * 58f;
                bars[i] = Lobby.Child<Image>(root.transform, "fill" + i, new Vector2(-385f, y), new Vector2(0f, 40f));
                bars[i].rectTransform.pivot = new Vector2(0f, 0.5f);
                bars[i].color = Green;
                rows[i] = Lobby.Label(root.transform, "row" + i, new Vector2(0f, y), new Vector2(760f, 52f), 27f, FontStyles.Normal);
                rows[i].enableWordWrapping = false;
                rows[i].overflowMode = TextOverflowModes.Ellipsis;
            }
            return new Screen { Root = root, Title = title, Rows = rows, Marker = marker, Bars = bars, Page = Random.Range(0, 4),
                                NextPage = Time.unscaledTime + Random.Range(3f, 8f), NextRow = Time.unscaledTime + Random.Range(0.5f, 2f) };
        }

        static void Paint(float now)
        {
            var s = Core.S;
            if (s == null) return;
            for (int i = _screens.Count - 1; i >= 0; i--)
            {
                var sc = _screens[i];
                if (sc.Root == null) { _screens.RemoveAt(i); continue; }
                if (now >= sc.NextPage) { sc.Page = (sc.Page + 1 + Random.Range(0, 2)) % 4; sc.Row = 0; sc.NextPage = now + Random.Range(5f, 11f); }
                if (now >= sc.NextRow) { sc.Row = (sc.Row + 1) % RowCount; sc.NextRow = now + Random.Range(0.6f, 2.2f); }
                Fill(s, sc);
                sc.Marker.rectTransform.anchoredPosition = new Vector2(0f, 110f - sc.Row * 58f);
            }
        }

        /// <summary>One page of the app with the provider's own numbers.</summary>
        static void Fill(IspState s, Screen sc)
        {
            var lines = new List<string>();
            var load = new List<float>();
            int hour = 0;
            try { hour = SaveGameManager.Current?.Hour ?? 0; } catch { }
            switch (sc.Page)
            {
                case 0:   // orders and faults
                    sc.Title.text = Go.T("go_tab_orders", "Orders");
                    foreach (var t in s.Tickets)
                    {
                        if (lines.Count >= RowCount) break;
                        string where = t.Site.Length > 0 ? City.Name(t.Site) : City.HoodName(t.Hood);
                        lines.Add(Sim.FaultName(t.Type) + "  |  " + where);
                    }
                    if (lines.Count == 0) lines.Add(Go.T("go_screen_noorders", "No open orders"));
                    break;
                case 1:   // the neighbourhoods' load
                    sc.Title.text = Go.T("go_tab_network", "Network");
                    foreach (var h in s.Hoods.Values)
                    {
                        if (lines.Count >= RowCount) break;
                        if (h.Nodes == 0) continue;
                        float u = h.Util != null && h.Util.Length == 24 ? h.Util[hour] : 0f;
                        lines.Add(City.HoodName(h.Id) + "  " + Go.Pct(u));
                        load.Add(Mathf.Clamp01(u));
                    }
                    break;
                case 2:   // the overview
                    sc.Title.text = Go.T("go_tab_overview", "Overview");
                    lines.Add(Go.T("go_ov_customers", "Customers") + ": " + Go.Num(Sim.Customers(s)));
                    lines.Add(Go.T("go_screen_today", "Today: {0} new, {1} cancelled", s.Today.Joined, s.Today.Left));
                    lines.Add(Go.T("go_ov_satisfaction", "Customer satisfaction") + ": " + Go.Num(s.Satisfaction) + " / 100");
                    lines.Add(Go.T("go_screen_calls", "Calls today: {0}", Go.Num(s.CallsToday)));
                    lines.Add(Go.T("go_screen_faults", "Faults today: {0} | fixed {1}", s.Today.Faults, s.Today.Fixed));
                    break;
                default:  // the staff
                    sc.Title.text = Go.T("go_tab_staff", "Staff");
                    foreach (var t in s.Techs)
                    {
                        if (lines.Count >= RowCount) break;
                        lines.Add(t.Name + "  |  " + Act.RoleName(t.Role) + (t.Missing == 0 ? "" : "  -"));
                    }
                    break;
            }
            for (int i = 0; i < RowCount; i++)
            {
                sc.Rows[i].text = i < lines.Count ? lines[i] : "";
                float f = sc.Page == 1 && i < load.Count ? load[i] : 0f;
                sc.Bars[i].rectTransform.sizeDelta = new Vector2(770f * f, 40f);
                sc.Bars[i].color = f >= 1f ? new Color(0.85f, 0.30f, 0.25f, 0.55f) : f >= 0.8f ? new Color(0.90f, 0.65f, 0.20f, 0.5f) : new Color(0.25f, 0.75f, 0.45f, 0.45f);
            }
        }
    }
}
