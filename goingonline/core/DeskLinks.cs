using System.Collections.Generic;
using BigAmbitions.Items;
using UnityEngine;

namespace GoingOnline
{
    /// <summary>GO-DESK-LINK-1 (user 2026-11-11, screenshot of the customer center at 19 9th Avenue: "Kundencenter-
    /// Arbeitsplatz - Fehlende Anforderungen: Schreibtisch"): interiors installed from our templates before GO-BLUEPRINT-6
    /// have their computers and chairs standing at the desks without belonging to them - the game counts a computer as
    /// "on a desk" and a chair as its chair only through the desk's stackedItems. Without the link the workplace misses
    /// its desk, and nobody can work there (WorkstationController.FindChair walks up to the desk and finds no seat).
    /// While the player is inside one of the provider's buildings, every computer and chair without a parent that stands
    /// on a free attachment point of a desk it fits (Item.stackType, within 35 cm across and 30 cm in height) is linked
    /// to it the way the game's placement does it (PlacementSystem.SetItemPosition): SetToParentPlaceableItem, then the
    /// desk's stackedItems with the point's index. Nothing moves; it is saved with the building like a placement.</summary>
    public static class DeskLinks
    {
        const float Across = 0.35f, Height = 0.30f;
        static string _in = "";
        static float _nextScan;

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
                    if (reg != null && GameTypes.IsOurs(reg.businessTypeName) && (reg.RentedByPlayer || reg.BuildingOwnedByPlayer))
                        key = City.KeyOf(reg.Address);
                }
            }
            catch { key = ""; }
            if (key != _in) { _in = key; _nextScan = Time.unscaledTime + 3f; }   // the items are set up after entering
            if (_in.Length == 0 || bm == null || Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 10f;   // again now and then: a template installed while inside
            try
            {
                int n = Link(bm.allItemControllers);
                if (n > 0) Go.Log($"desks: {n} computer(s) and chair(s) at {_in} linked to the desk they stand on (GO-DESK-LINK-1).");
                int t = Orient(bm.allItemControllers);
                if (t > 0) Go.Log($"desks: {t} workplace(s) at {_in} turned so their screen faces the staff's chair (GO-DESK-LINK-3).");
            }
            catch (System.Exception ex) { Go.Warn("desks: " + ex.Message + " (GO-DESK-LINK-1)."); _nextScan = Time.unscaledTime + 60f; }
        }

        static int Link(List<ItemController> items)
        {
            if (items == null || items.Count == 0) return 0;
            var desks = new List<ItemController>();
            foreach (var it in items)
                if (it != null && it.ItemInstance != null && it.AttachmentPoints != null && it.AttachmentPoints.Length > 0) desks.Add(it);
            if (desks.Count == 0) return 0;
            int linked = 0;
            foreach (var child in items)
            {
                if (child == null || child.parentItemController != null || child.ItemInstance == null) continue;
                var item = child.Item;
                if (item == null) continue;
                var fits = item.stackType & (AttachmentPointType.Computer | AttachmentPointType.Seat);
                if (fits == 0) continue;   // only the workplace's parts; everything else stays as the player put it
                Vector3 at = child.transform.position;
                ItemController? best = null;
                AttachmentPoint? bestPoint = null;
                int bestIndex = -1;
                float bestDist = Across;
                foreach (var desk in desks)
                {
                    if (desk == child || desk.parentItemController == child) continue;
                    var points = desk.AttachmentPoints;
                    for (int i = 0; i < points.Length; i++)
                    {
                        var ap = points[i];
                        if (ap == null || (ap.AttachmentPointType & fits) == 0 || Taken(desk, ap)) continue;
                        Vector3 p = ap.transform.position;
                        if (Mathf.Abs(p.y - at.y) > Height) continue;
                        float d = new Vector2(p.x - at.x, p.z - at.z).magnitude;
                        if (d < bestDist) { bestDist = d; best = desk; bestPoint = ap; bestIndex = i; }
                    }
                }
                if (best == null || bestPoint == null || best.ItemInstance == null) continue;
                child.SetToParentPlaceableItem(best, bestPoint);
                if (child.parentItemController != best) continue;   // the game refused it
                best.ItemInstance.stackedItems ??= new List<AttachableChild>();
                best.ItemInstance.stackedItems.RemoveAll(x => x != null && x.childId == child.ItemInstance.id);
                best.ItemInstance.stackedItems.Add(new AttachableChild
                {
                    attachmentIndex = bestIndex,
                    childId = child.ItemInstance.id,
                    childItemName = child.ItemInstance.itemName,
                });
                linked++;
            }
            return linked;
        }

        /// <summary>GO-DESK-LINK-3 (user 2026-11-12: "mitarbeiter setzen sich auf dem sessel"): customer centers from our
        /// templates before GO-BLUEPRINT-7 had the workplace's screen facing the armchair. The staff now take the office chair
        /// (GO-COUNTER-SEAT-1), so a workplace on a service counter whose staff seat stands behind it (-forward) is turned
        /// half round, saved with the building like a turn in the placement mode.</summary>
        static int Orient(List<ItemController> items)
        {
            int turned = 0;
            foreach (var ic in items)
            {
                if (ic == null || ic.ItemInstance == null) continue;
                var root = GamePatches.CounterOf(ic);
                if (root == null) continue;
                var seat = GamePatches.StaffSeat(ic, root, false);
                if (seat == null || GamePatches.IsArmchair(seat)) continue;   // no office chair there: leave it as it is
                var d = seat.transform.position - ic.transform.position; d.y = 0f;
                if (d.sqrMagnitude < 0.01f || Vector3.Dot(d, ic.transform.forward) >= 0f) continue;
                ic.transform.rotation = Quaternion.AngleAxis(180f, Vector3.up) * ic.transform.rotation;
                ic.ItemInstance.yRotation = ic.transform.eulerAngles.y;
                turned++;
            }
            return turned;
        }

        static bool Taken(ItemController desk, AttachmentPoint ap)
        {
            foreach (var c in desk.childItemControllers)
                if (c != null && c.parentAttachmentPoint == ap) return true;
            return false;
        }
    }
}
