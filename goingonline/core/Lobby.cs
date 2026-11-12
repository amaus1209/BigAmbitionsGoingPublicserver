using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Controllers;
using Random = UnityEngine.Random;

namespace GoingOnline
{
    /// <summary>GO-LOBBY-1 (user 2026-11-08: "Laden mit Kundschaft", then "vielleicht im Kundencenter ein Tisch mit den Geräten
    /// für die Kunden und ein TV mit einem Spot vom Namen des Kabelanbieters, Warteplätze, bevor ein Kunde zum Kundenplatz
    /// hingeht"): life in a customer center while the player is inside one.
    /// - Visitors: people of the neighbourhood (the game's own customer figures, from the building's customer pool, but walked
    ///   by this code instead of a behaviour tree) come in, look at the device table, sit down on a free seat (the game's
    ///   waiting room chairs, sofas, benches - any seat), wait for a free customer desk (Going Online's customer center
    ///   workplace with someone on shift now), talk there and leave. Only while the shop is open; more of them the more
    ///   customers the neighbourhood has.
    /// - Promo TV: every television standing in the center shows the provider's spot - logo, name, the fastest tariff and its
    ///   price, the channels, "switch now" - slide after slide (a world-space canvas on the screen).
    /// - The device table (GameTypes.IDeviceTable, model godevicetable) shows the provider's logo on its front panel.
    /// Nothing of this is saved; it is built when the player comes in and taken down when they leave.</summary>
    internal static class Lobby
    {
        sealed class Visitor
        {
            public Customer C = null!;
            public int Step;
            public float Until;
            public bool Arrived;
            public SeatController? Seat;
            public Transform? SeatPos;
            public ItemController? Desk;
        }

        static string _in = "";
        static readonly List<Visitor> _visitors = new List<Visitor>();
        static readonly List<GameObject> _decor = new List<GameObject>();
        static readonly HashSet<int> _decorated = new HashSet<int>();
        static readonly List<(GameObject root, Image back, RawImage logo, TextMeshProUGUI title, TextMeshProUGUI line)> _screens = new List<(GameObject, Image, RawImage, TextMeshProUGUI, TextMeshProUGUI)>();
        static float _nextSpawn, _nextScan, _slideAt;
        static int _slide;
        static bool _warned;

        /// <summary>Main thread, every frame (cheap outside our customer centers).</summary>
        public static void Tick()
        {
            if (Sim.Test) return;
            string key = "";
            BuildingManager? bm = null;
            try
            {
                bm = BuildingManager.IsInsideBuilding ? InstanceBehavior<BuildingManager>.Instance : null;
                var reg = bm != null ? bm.buildingRegistration : null;
                if (reg != null && reg.businessTypeName == GameTypes.Center && (reg.RentedByPlayer || reg.BuildingOwnedByPlayer) && Core.S != null)
                    key = City.KeyOf(reg.Address);
            }
            catch { key = ""; }
            if (key != _in) { Clear(); _in = key; _nextSpawn = Time.unscaledTime + 3f; _nextScan = 0f; }
            if (_in.Length == 0 || bm == null) return;
            try
            {
                if (Time.unscaledTime >= _nextScan) { _nextScan = Time.unscaledTime + 4f; Decorate(bm); }
                Slides();
                Visitors(bm);
            }
            catch (System.Exception ex)
            {
                if (!_warned) { _warned = true; Go.Warn("customer center: " + ex.Message + " (GO-LOBBY-1)."); }
            }
        }

        /// <summary>Everything of ours goes: the visitors back into the game's pool, the logo and the spots.</summary>
        public static void Clear()
        {
            BuildingManager? bm = null;
            try { bm = InstanceBehavior<BuildingManager>.Instance; } catch { }
            foreach (var v in _visitors) Release(bm, v);
            _visitors.Clear();
            foreach (var go in _decor) if (go != null) UnityEngine.Object.Destroy(go);
            _decor.Clear(); _decorated.Clear(); _screens.Clear();
        }

        // ── the visitors ──────────────────────────────────────────────────────────────────────────────────────────────

        static bool Paused()
        {
            try { return InstanceBehavior<UI.UIs>.Instance.gameSpeed.Paused; } catch { return false; }
        }

        static void Visitors(BuildingManager bm)
        {
            for (int i = _visitors.Count - 1; i >= 0; i--)
            {
                var v = _visitors[i];
                if (v.C == null || !v.C.gameObject.activeInHierarchy) { _visitors.RemoveAt(i); continue; }
                if (Step(bm, v)) { Release(bm, v); _visitors.RemoveAt(i); }
            }
            if (Paused() || !bm.isOpen || Time.unscaledTime < _nextSpawn) return;
            var s = Core.S!;
            string hood = City.ByKey.TryGetValue(_in, out var b) ? b.Hood : "";
            int customers = 0;
            foreach (var site in s.Sites.Values) if (site.Hood == hood) customers += Sim.CustomersOf(site);
            int max = Mathf.Clamp(1 + customers / 150, 1, 6);
            bool staffed = Desks(bm, true).Count > 0;
            if (!staffed) max = Mathf.Min(max, 1);
            _nextSpawn = Time.unscaledTime + Random.Range(staffed ? 14f : 40f, staffed ? 34f : 80f);
            if (_visitors.Count >= max || bm.exitZones == null || bm.exitZones.Count == 0 || bm.customerSpawner == null) return;
            try { Spawn(bm, hood); }
            catch (System.Exception ex)
            {   // no customer figures in this building: no visitors here (the spot and the logo stay)
                _nextSpawn = Time.unscaledTime + 600f;
                if (!_warned) { _warned = true; Go.Warn("customer center visitors: " + ex.Message + " (GO-LOBBY-1)."); }
            }
        }

        static void Spawn(BuildingManager bm, string hood)
        {
            var c = bm.customerSpawner.GetCustomer(CustomerType.SelfService);
            if (c == null) return;
            var zone = bm.exitZones[Random.Range(0, bm.exitZones.Count)];
            Vector3 pos = zone.transform.position;
            var jitter = Random.insideUnitCircle * 0.5f;
            pos.x += jitter.x; pos.z += jitter.y;
            try { c.citizenData = AI.Citizens.CitizenHelper.CreateRandomCitizenDataFromNeighborhood(bm.buildingRegistration.Neighborhood); } catch { }
            c.order = new Order();
            c.transform.SetPositionAndRotation(pos, zone.transform.rotation);
            c.gameObject.SetActive(true);
            c.behaviorTree.DisableBehavior();   // we walk them ourselves
            c.tpc.navmeshAgent.enabled = true;
            c.tpc.navmeshAgent.Warp(pos);
            c.ResetVisibilityValues();
            c.tpc.animator.enabled = true;
            try { if (c.citizenData != null) c.tpc.appearanceSetter.SetRandomAppearance(c.citizenData.Gender, c.citizenData.AppearanceTags); } catch { }
            c.tpc.animator.Update(0f);
            var v = new Visitor { C = c, Step = 0 };
            _visitors.Add(v);
            var table = Find(bm, GameTypes.IDeviceTable);
            if (table != null && Random.value < 0.75f) Walk(v, table.transform.position, Front(table, 1.0f));
            else { v.Step = 2; v.Arrived = true; }
        }

        /// <summary>One visitor's next move; true = gone (back to the pool).</summary>
        static bool Step(BuildingManager bm, Visitor v)
        {
            if (Paused()) { v.Until += Time.unscaledDeltaTime; return false; }
            float now = Time.unscaledTime;
            switch (v.Step)
            {
                case 0:   // walking to the device table
                    if (v.Arrived) { v.Step = 1; v.Until = now + Random.Range(4f, 9f); }
                    else if (now > v.Until) { v.Step = 2; }
                    return false;
                case 1:   // looking at the devices
                    if (now >= v.Until) v.Step = 2;
                    return false;
                case 2:   // to a seat - or straight to a desk that is free
                {
                    var desk = FreeDesk(bm);
                    if (desk != null && Random.value < 0.35f) { GoToDesk(v, desk); return false; }
                    var (seat, pos) = FreeSeat(bm);
                    if (seat != null && pos != null)
                    {
                        v.Seat = seat; v.SeatPos = pos;
                        Walk(v, pos.position + pos.forward, NearNav(pos.position + pos.forward * 0.55f));
                        v.Step = 3;
                    }
                    else { v.Step = 5; v.Until = now + Random.Range(15f, 30f); }   // no seat: they stand and wait
                    return false;
                }
                case 3:   // walking to the seat
                    if (v.Arrived && v.SeatPos != null && v.Seat != null && v.Seat.IsSittingPositionAvailable(v.SeatPos))
                    {
                        v.C.StopAllCoroutines();
                        v.C.tpc.SitOnChair(v.SeatPos);
                        v.Step = 4; v.Until = now + Random.Range(20f, 45f);
                    }
                    else if (v.Arrived || now > v.Until) { v.Seat = null; v.SeatPos = null; v.Step = 5; v.Until = now + Random.Range(10f, 20f); }
                    return false;
                case 4:   // sitting, until a desk is free (or they have waited long enough)
                case 5:   // standing, the same
                {
                    var desk = FreeDesk(bm);
                    if (desk != null && now >= v.Until - 15f) { StandUp(v); GoToDesk(v, desk); return false; }
                    if (now >= v.Until + 25f) { StandUp(v); Leave(bm, v); }   // nobody came: they go
                    return false;
                }
                case 6:   // walking to the desk
                    if (v.Arrived) { v.Step = 7; v.Until = now + Random.Range(15f, 32f); }
                    else if (now > v.Until) { v.Desk = null; Leave(bm, v); }
                    return false;
                case 7:   // at the desk
                    if (now >= v.Until) { v.Desk = null; Leave(bm, v); }
                    return false;
                case 8:   // walking out
                    return v.Arrived || now > v.Until;
            }
            return true;
        }

        static void StandUp(Visitor v)
        {
            if (v.C.tpc.isSittingOn == null) return;
            var front = v.SeatPos != null ? NearNav(v.SeatPos.position + v.SeatPos.forward * 0.55f) : v.C.transform.position;
            v.C.tpc.Reset();
            v.C.tpc.WarpSafely(front);
            v.Seat = null; v.SeatPos = null;
        }

        static void GoToDesk(Visitor v, ItemController desk)
        {
            v.Desk = desk;
            Walk(v, desk.transform.position, CustomerSpot(desk));
            v.Step = 6;
        }

        static void Leave(BuildingManager bm, Visitor v)
        {
            var zone = bm.exitZones[0];
            float best = float.MaxValue;
            foreach (var z in bm.exitZones)
            {
                float d = (z.transform.position - v.C.transform.position).sqrMagnitude;
                if (d < best) { best = d; zone = z; }
            }
            var target = zone.despawner != null ? zone.despawner.transform.position : zone.transform.position;
            Walk(v, zone.transform.position, target);
            v.Step = 8;
        }

        /// <summary>Walks a visitor on the navmesh (the game's own move, which also turns them to look at the target).</summary>
        static void Walk(Visitor v, Vector3 look, Vector3 target)
        {
            v.Arrived = false;
            v.Until = Time.unscaledTime + 30f;
            v.C.StopAllCoroutines();
            v.C.StartCoroutine(v.C.tpc.MoveToPosition(look, target, 0.6f, true, () => v.Arrived = true, 0f, null, true));
        }

        static void Release(BuildingManager? bm, Visitor v)
        {
            if (v.C == null) return;
            try
            {
                if (v.C.tpc.isSittingOn != null) v.C.tpc.Reset();
                v.C.StopAllCoroutines();
                if (bm != null && bm.customerSpawner != null) bm.customerSpawner.ReleaseCustomer(v.C, CustomerType.SelfService);
                else v.C.gameObject.SetActive(false);
            }
            catch { }
        }

        // ── places in the room ────────────────────────────────────────────────────────────────────────────────────────

        static ItemController? Find(BuildingManager bm, string item)
        {
            foreach (var ic in bm.allItemControllers) if (ic != null && ic.itemName == item) return ic;
            return null;
        }

        /// <summary>A point in front of an item on the navmesh.</summary>
        static Vector3 Front(ItemController ic, float metres)
        {
            try { var p = ic.GetNavMeshTargetPosition(0); if (p != Vector3.zero) return p; } catch { }
            return NearNav(ic.transform.position + ic.transform.forward * metres);
        }

        static Vector3 NearNav(Vector3 p) => NavMesh.SamplePosition(p, out var hit, 1.5f, -1) ? hit.position : p;

        /// <summary>A free seat: any of the game's seats in the room (waiting room chairs, sofas, benches, chairs) with a free
        /// sitting position nobody of ours is walking to.</summary>
        static (SeatController?, Transform?) FreeSeat(BuildingManager bm)
        {
            var taken = new HashSet<Transform>();
            foreach (var v in _visitors) if (v.SeatPos != null) taken.Add(v.SeatPos);
            var options = new List<(SeatController, Transform)>();
            foreach (var ic in bm.allItemControllers)
            {
                if (!(ic is SeatController seat) || seat.Occupied && seat.SittingPosition == null) continue;
                var pos = seat.SittingPosition;
                if (pos == null || taken.Contains(pos) || !seat.IsSittingPositionAvailable(pos)) continue;
                // not the employees' chairs at the desks
                bool atDesk = false;
                foreach (var d in Desks(bm, false)) if ((d.transform.position - seat.transform.position).sqrMagnitude < 1.6f * 1.6f) { atDesk = true; break; }
                if (!atDesk) options.Add((seat, pos));
            }
            if (options.Count == 0) return (null, null);
            var pick = options[Random.Range(0, options.Count)];
            return (pick.Item1, pick.Item2);
        }

        /// <summary>The customer desks (Going Online's customer center workplace) in the room - staffedOnly: with someone on
        /// shift there now.</summary>
        static List<ItemController> Desks(BuildingManager bm, bool staffedOnly)
        {
            var r = new List<ItemController>();
            var reg = bm.buildingRegistration;
            int hour = 0;
            try { hour = SaveGameManager.Current?.Hour ?? 0; } catch { }
            BigAmbitions.DayNightCycle.DayOfWeekOrdered today = default;
            try { today = TimeHelper.GetDayOfWeek(); } catch { }
            foreach (var ic in bm.allItemControllers)
            {
                if (ic == null || ic.itemName != GameTypes.IDesk) continue;
                if (!staffedOnly) { r.Add(ic); continue; }
                string id = "";
                try { id = ic.ItemInstance?.id ?? ""; } catch { }
                if (id.Length == 0 || reg?.scheduleDays == null) continue;
                bool on = false;
                foreach (var day in reg.scheduleDays)
                {
                    if (day == null || day.day != today || day.workShifts == null) continue;
                    foreach (var ws in day.workShifts)
                        if (ws != null && ws.itemInstanceId == id && ws.startingHour <= hour && ws.endingHour > hour) { on = true; break; }
                }
                if (on) r.Add(ic);
            }
            return r;
        }

        static ItemController? FreeDesk(BuildingManager bm)
        {
            foreach (var d in Desks(bm, true))
            {
                bool busy = false;
                foreach (var v in _visitors) if (v.Desk == d) { busy = true; break; }
                if (!busy) return d;
            }
            return null;
        }

        /// <summary>Where a customer stands at a desk: across from the person working there - away from the nearest chair,
        /// else behind the computer's back.</summary>
        static Vector3 CustomerSpot(ItemController desk)
        {
            var p = desk.transform.position;
            Vector3 away = -desk.transform.forward;
            float best = 2.0f * 2.0f;
            try
            {
                var bm = InstanceBehavior<BuildingManager>.Instance;
                foreach (var ic in bm.allItemControllers)
                {
                    if (!(ic is SeatController)) continue;
                    var d = ic.transform.position - p; d.y = 0f;
                    if (d.sqrMagnitude < best) { best = d.sqrMagnitude; away = -d.normalized; }
                }
            }
            catch { }
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -desk.transform.forward;
            return NearNav(p + away.normalized * 1.0f);
        }

        // ── the logo on the device table and the spot on the televisions ───────────────────────────────────────────

        static Texture2D? LogoTexture()
        {
            var s = Core.S;
            if (s == null || !City.BusinessName(s.HeadendKey, out var name)) return null;
            try { return LogoHelper.GetBusinessLogoTexture(name, LogoSize.SquareSign, true); } catch { return null; }
        }

        static void Decorate(BuildingManager bm)
        {
            var model = Models.Get("godevicetable");
            foreach (var ic in bm.allItemControllers)
            {
                if (ic == null || _decorated.Contains(ic.GetInstanceID())) continue;
                if (ic.itemName == GameTypes.IDeviceTable && model != null)
                {
                    var mat = ServiceVans.Logo();
                    if (mat == null) continue;   // the logo is not made yet: next round
                    foreach (var mf in ic.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf.sharedMesh != model.Mesh) continue;
                        foreach (var kv in model.Markers)
                            if (kv.Key.StartsWith("logo")) _decor.Add(ServiceVans.Quad(mf.transform, kv.Value.pos, kv.Value.normal, kv.Value.size, mat));
                    }
                    _decorated.Add(ic.GetInstanceID());
                }
                else if (IsTv(ic))
                {
                    var screen = Screen(ic);
                    if (screen != null) _decor.Add(screen);
                    _decorated.Add(ic.GetInstanceID());
                }
            }
        }

        static bool IsTv(ItemController ic)
        {
            string n = ic.itemName ?? "";
            return n.StartsWith("ba:itemname_") && (n.Contains("wallmountedtv") || n.EndsWith("tv")) && !n.EndsWith("stand");
        }

        /// <summary>The spot on a television: a canvas on the front of its screen (the side the set stands out from the wall).</summary>
        static GameObject? Screen(ItemController tv)
        {
            MeshFilter? big = null; float vol = 0f;
            foreach (var mf in tv.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null) continue;
                var sz = mf.sharedMesh.bounds.size;
                float v = sz.x * sz.y * Mathf.Max(0.01f, sz.z);
                if (v > vol && sz.x > 0.4f && sz.y > 0.25f) { vol = v; big = mf; }
            }
            if (big == null) return null;
            var bounds = big.sharedMesh.bounds;
            bool front = bounds.center.z >= 0f;
            float w = bounds.size.x * 0.9f, h = bounds.size.y * 0.86f;
            var root = new GameObject("GO_PromoSpot");
            root.transform.SetParent(big.transform, false);
            root.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, front ? bounds.max.z + 0.004f : bounds.min.z - 0.004f);
            root.transform.localRotation = front ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1280f, 720f);
            root.transform.localScale = new Vector3(w / 1280f, h / 720f, 1f);
            var back = Child<Image>(root.transform, "back", new Vector2(0f, 0f), new Vector2(1280f, 720f));
            back.color = new Color(0.03f, 0.06f, 0.09f, 1f);
            var band = Child<Image>(root.transform, "band", new Vector2(0f, -250f), new Vector2(1280f, 120f));
            band.color = new Color(0.10f, 0.62f, 0.75f, 1f);
            // GO-LOBBY-2 (user 2026-11-11: "das Logo überschreibt die Schrift", then "das Logo vom TV-Spot kann oben links
            // angezeigt werden"): the logo small in the top left corner, the texts over the whole width below it, each
            // sized to fit its box (before: a big logo on the left and the texts right up against it)
            var logo = Child<RawImage>(root.transform, "logo", new Vector2(-530f, 255f), new Vector2(170f, 170f));
            logo.texture = LogoTexture();
            logo.enabled = logo.texture != null;
            var title = Label(root.transform, "title", new Vector2(0f, 60f), new Vector2(1160f, 170f), 84f, FontStyles.Bold);
            var line = Label(root.transform, "line", new Vector2(0f, -95f), new Vector2(1160f, 150f), 52f, FontStyles.Normal);
            title.enableAutoSizing = true; title.fontSizeMin = 40f; title.fontSizeMax = 84f;
            line.enableAutoSizing = true; line.fontSizeMin = 30f; line.fontSizeMax = 52f;
            var foot = Label(root.transform, "foot", new Vector2(0f, -250f), new Vector2(1200f, 110f), 50f, FontStyles.Bold);
            foot.text = Go.T("go_spot_foot", "Internet  |  TV  |  Phone");
            foot.alignment = TextAlignmentOptions.Center;
            _screens.Add((root, back, logo, title, line));
            _slideAt = 0f;
            return root;
        }

        internal static T Child<T>(Transform parent, string name, Vector2 pos, Vector2 size) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return go.AddComponent<T>();
        }

        internal static TextMeshProUGUI Label(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, FontStyles style)
        {
            var t = Child<TextMeshProUGUI>(parent, name, pos, size);
            if (Ui.Font != null) t.font = Ui.Font;
            t.fontSize = fontSize; t.fontStyle = style; t.color = Color.white;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.enableWordWrapping = true;
            return t;
        }

        /// <summary>The spot's slides, one every five seconds, gently fading in.</summary>
        static void Slides()
        {
            if (_screens.Count == 0) return;
            var s = Core.S;
            if (s == null) return;
            float now = Time.unscaledTime;
            if (now >= _slideAt)
            {
                _slideAt = now + 5f;
                _slide++;
                var all = SlidesOf(s);
                var (title, line) = all[_slide % all.Count];
                var logo = LogoTexture();   // GO-LOGO-2: the game's current picture (Going Public Server may have renewed it)
                foreach (var sc in _screens)
                {
                    if (sc.root == null) continue;
                    sc.title.text = title; sc.line.text = line;
                    if (logo != null && sc.logo.texture != logo) sc.logo.texture = logo;
                    sc.logo.enabled = sc.logo.texture != null;
                }
            }
            float a = Mathf.Clamp01((now - (_slideAt - 5f)) / 0.6f);
            foreach (var sc in _screens)
            {
                if (sc.root == null) continue;
                sc.title.alpha = a; sc.line.alpha = a;
                float pulse = 1f + 0.03f * Mathf.Sin(now * 2.2f);
                sc.logo.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        /// <summary>GO-LOBBY-2 (user 2026-11-11: "mach mehr Werbung drauf, z.B. zeige nacheinander die Tarife,
        /// Kundenzufriedenheit usw."): the spot's slides, five seconds each, from what the provider has now - the
        /// greeting, every tariff on sale one by one, the cheapest price, the customer satisfaction (from 60), the number of
        /// connections, the TV channels, the phone line, fibre in this neighbourhood, the technicians, the connection fee, a
        /// test win and a sports promotion while they run, and "switch now".</summary>
        static List<(string title, string line)> SlidesOf(IspState s)
        {
            var list = new List<(string, string)>();
            string hood = "", hoodId = "";
            if (City.ByKey.TryGetValue(_in, out var b)) { hoodId = b.Hood; hood = City.HoodName(b.Hood); }
            list.Add((s.Company, Go.T("go_spot_hello", "Your cable provider in {0}", hood)));
            if (Events.AwardNow(s)) list.Add((Go.T("go_spot_award", "Test winner"), Go.T("go_spot_award_line", "the best cable provider in the city")));
            var match = Events.PromoMatch(s);
            if (match != null) list.Add((Go.T("go_spot_match", "The {0} live", match), Go.T("go_spot_match_line", "with our TV tariffs")));
            Tariff? cheap = null;
            foreach (var t in s.Tariffs)
            {
                if (!t.Active) continue;
                if (cheap == null || t.Price < cheap.Price) cheap = t;
                var parts = new List<string>();
                if (t.Mbit > 0) parts.Add(Go.Bw(t.Mbit) + (t.Fiber ? " " + Go.T("go_spot_fibre", "fibre") : ""));
                if (t.Tv || t.Mbit <= 0) parts.Add(Go.T("go_spot_feat_tv", "TV"));
                if (t.Phone && t.Mbit > 0) parts.Add(Go.T("go_spot_feat_phone", "phone"));
                parts.Add(t.Term > 0 ? Go.T("go_spot_term", "{0} days minimum", t.Term) : Go.T("go_spot_noterm", "no minimum term"));
                list.Add((t.Name, Go.T("go_spot_tariff_line", "{0} - {1} a day", string.Join(" | ", parts), Go.Money(t.Price))));
            }
            if (cheap != null) list.Add((Go.T("go_spot_from", "From {0} a day", Go.Money(cheap.Price)), Go.T("go_spot_from_line2", "internet, TV and phone from one provider")));
            if (s.Satisfaction >= 60f)
                list.Add((Go.T("go_spot_sat", "Customer satisfaction {0}", Go.Pct(s.Satisfaction / 100f)), Go.T("go_spot_sat_line", "what our customers say about us")));
            int customers = Sim.Customers(s);
            if (customers >= 50) list.Add((Go.T("go_spot_customers", "{0} connections already", Go.Num(customers)), Go.T("go_spot_customers_line", "surf, watch and call with {0}", s.Company)));
            if (Sim.Channels(s) > 0) list.Add((Go.T("go_spot_tv", "{0} TV channels", Sim.Channels(s)), Go.T("go_spot_tv_line", "on the cable - no dish, no box")));
            if (Sim.PhoneReady(s)) list.Add((Go.T("go_spot_phone", "Phone flat rate"), Go.T("go_spot_phone_line", "take your number with you")));
            if (hoodId.Length > 0 && Sim.OltAt(s, hoodId)) list.Add((Go.T("go_spot_fiber", "Fibre in {0}", hood), Go.T("go_spot_fiber_line", "right into your flat")));
            int techs = 0;
            foreach (var t in s.Techs) if (t.Role == Cat.RoleTech) techs++;
            if (techs > 0) list.Add((Go.T("go_spot_service", "A fault? We come"), Go.T("go_spot_service_line", "our technicians are on duty in {0}", hood)));
            list.Add((s.Fee <= 0.005f ? Go.T("go_spot_fee_free", "Connection free of charge") : Go.T("go_spot_fee", "Connection for {0}", Go.Money(s.Fee)),
                      Go.T("go_spot_fee_line", "no hidden costs")));
            list.Add((Go.T("go_spot_switch", "Switch now"), Go.T("go_spot_switch_line", "right here at the customer center - we take care of the rest")));
            return list;
        }
    }
}
