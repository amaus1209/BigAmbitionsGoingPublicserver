#!/usr/bin/env python3
"""GO-BLUEPRINT-1 (user 2026-11-10: "A2 Einzelhandel - baue eine Vorlage für das Kundencenter für 15 Kunden ... und weitere
Gebäude sowie Büro für Kabelanbieter"): building templates ("Vorlagen", the game's blueprints) for Going Online.

The game reads a blueprint from <persistentDataPath>/Blueprints/<name>/ - Metadata.json, Layout.json, Thumbnail.png
(Blueprints.BlueprintsFolderLoader); the interior installation firm offers it for a building of the same type, size and
version whose business is the blueprint's (or none), and installs it for the items' market prices plus its fee
(InteriorInstallationFirmDesignSettings.UpdateLayoutPrice). Walls and floors come from the game's own empty layout of that
building (StreamingAssets/BusinessLayouts/Empty/<size>/default.json) - nothing of other players' Workshop designs is
copied; the room's shape (where the partition, the back doors and the entrance are) was read from layouts of the same
building. Positions are the interior's own coordinates (the same for every building of a size and version).

    python3 -I tools/blueprints.py <game folder> <out folder>

writes one folder per template into <out folder>; the player copies them into the game's Blueprints folder.
Furniture sizes (m, width x depth) from the game's prefabs (their box colliders): waiting room chairs 1.81 x 0.69 with 3
seats, special employee desk (small) 2.0 x 1.0, console table (the device table) 2.0 x 0.5, armchair 0.66 x 0.74,
multipurpose chair 0.59 x 0.58, wall TV 1.5 wide, water cooler 0.5 x 0.5, storage shelf 2.97 x 0.5, toilet stall 1.0 x 1.18,
big cabinet (the headend's racks) 0.7 x 1.0, office desk 2.0 x 1.0.
"""
import base64, hashlib, json, math, os, sys, zlib, struct

GAME, OUT = sys.argv[1], sys.argv[2]
LAYOUTS = os.path.join(GAME, "Big Ambitions_Data", "StreamingAssets", "BusinessLayouts")
BUILD = 3674   # the game build the layouts were made with (a blueprint newer than the game is not shown)

# the market prices of the pieces (dump.py items --fields itemName,defaultMarketPrice; Going Online's from GameTypes/Cat)
PRICE = {
    "ba:itemname_waitingroomchairs": 700, "ba:itemname_specialemployeedesksmall": 900, "ba:itemname_armchair1": 130,
    "ba:itemname_multipurposechair": 1200, "ba:itemname_wallmountedtv": 300, "ba:itemname_groundplant02": 140,
    "ba:itemname_pottedplant1": 80, "ba:itemname_watercooler": 130, "ba:itemname_largewallclock": 60,
    "ba:itemname_toiletstall": 2100, "ba:itemname_sinkwallmodern": 650, "ba:itemname_sinksoapwallmounted": 65,
    "ba:itemname_mirrorwallmounted": 180, "ba:itemname_handdryer": 195, "ba:itemname_bathroomsignwallmounted": 45,
    "ba:itemname_signstaffonly": 125, "ba:itemname_exitsign": 500, "ba:itemname_storageshelf": 1200,
    "ba:itemname_cleaningstation": 100, "ba:itemname_uniformlocker": 1950, "ba:itemname_ceilinglamptube": 30,
    "ba:itemname_ceilinglampsquare": 30, "ba:itemname_classicloudspeakermedium": 500, "ba:itemname_securitycameraroof": 2700,
    "goingonline:itemname_godesk": 2500, "goingonline:itemname_godevicetable": 1800,
    "goingonline:itemname_gotechdesk": 2500, "goingonline:itemname_gocontroldesk": 2500,
    "goingonline:itemname_gocmts30": 25000, "goingonline:itemname_goolt": 150000, "goingonline:itemname_gosat": 25000,
    "goingonline:itemname_gocas": 40000, "goingonline:itemname_govoice": 30000, "goingonline:itemname_goiptv": 60000,
    "goingonline:itemname_goups": 12000, "goingonline:itemname_gogenerator": 35000,
}

N = 0


def ident(name, seq):
    """A stable id per template and piece (base64 of 16 bytes, as the game's)."""
    return base64.b64encode(hashlib.md5(f"{name}|{seq}".encode()).digest()).decode()


def item(tpl, item_name, x, y, z, yaw):
    global N
    N += 1
    a = math.radians(yaw) / 2.0
    return {  # (the add helpers return it - GO-BLUEPRINT-6 links desks with it)
        "id": ident(tpl, N),
        "rotation": {"w": round(math.cos(a), 7), "x": 0.0, "y": round(math.sin(a), 7), "z": 0.0},
        "position": {"x": round(x, 3), "y": round(y, 3), "z": round(z, 3)},
        "itemName": item_name,
        "playerItemPurchaserSettings": {"name": "", "enabled": False, "itemName": "", "itemQuantity": 1, "isQuantityItem": False},
        "stackedItems": [], "parentId": None, "dirtSpotsThatAffects": [], "customPositions": [], "customColors": [],
        "customValue": "", "worldSpaceTextValue": "", "linkedItemName": None, "dirtAffectedCells": None,
        "type": "BusinessLayoutSets.Item, BigAmbitions, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
    }


def ba(n):
    return "ba:itemname_" + n


def link(desk, *children):
    """GO-BLUEPRINT-6 (user 2026-11-11, the screenshot: "Kundencenter-Arbeitsplatz - fehlende Anforderungen: Stuhl,
    Schreibtisch"): the game knows a computer stands on a desk and a chair belongs to it only through the desk's
    stackedItems (childId, childItemName, attachmentIndex) - placed side by side without them the workplace misses both.
    The desks' points, from the game's own layouts: specialemployeedesksmall 1 computer (0, 0.74, 0), 2 the staff's
    chair (z +0.75, turned 180), 3 the customer's chair (z -1.0); officedesk1 0 monitor, 1 computer, 2 chair (z +0.75).
    children: (item, attachment index)."""
    desk["stackedItems"] = [{"childId": c["id"], "childItemName": c["itemName"], "attachmentIndex": i} for c, i in children]


# ── the customer center in a retail building A, version 2 ("A2"): 7.84 x 14.5 m ────────────────────────────────────
# main room x 996.58..1004.42, z 41.5 (shop window, entrance at x ~1000.5) .. 51.42 (the partition; its doors at x ~999.5
# to the staff room and ~1003 to the toilets); behind it the staff and stock room (left) and the toilets (right)
def center_a2():
    t = "GO Kundencenter A2"
    out = []
    add = lambda n, x, y, z, yaw: (out.append(item(t, n, x, y, z, yaw)), out[-1])[1]
    # the devices on show by the shop window, the entrance between them free
    add("goingonline:itemname_godevicetable", 997.9, 0.0, 42.1, 0)
    add("goingonline:itemname_godevicetable", 1003.1, 0.0, 42.1, 0)
    # 15 waiting seats: 5 x 3 - along both walls and two rows in the middle, facing the desks
    for z in (44.3, 46.4):
        add(ba("waitingroomchairs"), 996.95, 0.0, z, 90)
    add(ba("waitingroomchairs"), 1004.05, 0.0, 44.3, 270)
    for z in (45.2, 46.6):
        add(ba("waitingroomchairs"), 1000.5, 0.0, z, 0)
    # three customer desks with Going Online's customer center workplace: two in front of the partition (the doors free),
    # one at the right wall - the person working sits behind the desk, the customer's chair in front
    for x in (997.7, 1001.3):   # 997.7: 12 cm from the left wall
        d = add(ba("specialemployeedesksmall"), x, 0.0, 49.6, 0)
        link(d, (add("goingonline:itemname_godesk", x, 0.74, 49.6, 0), 1), (add(ba("armchair1"), x, 0.0, 50.35, 180), 2),
             (add(ba("multipurposechair"), x, 0.0, 48.6, 0), 3))
    d = add(ba("specialemployeedesksmall"), 1003.3, 0.0, 48.3, 90)
    link(d, (add("goingonline:itemname_godesk", 1003.3, 0.74, 48.3, 90), 1), (add(ba("armchair1"), 1004.05, 0.0, 48.3, 270), 2),
         (add(ba("multipurposechair"), 1002.3, 0.0, 48.3, 90), 3))
    # the promo spot on three TVs: both side walls and the partition, a clock, water, plants
    add(ba("wallmountedtv"), 996.58, 2.6, 45.35, 90)
    add(ba("wallmountedtv"), 1004.42, 2.6, 44.3, 270)
    add(ba("wallmountedtv"), 1001.6, 2.7, 51.42, 180)
    add(ba("largewallclock"), 996.58, 2.5, 49.6, 90)
    add(ba("watercooler"), 1004.1, 0.0, 46.2, 270)
    add(ba("groundplant02"), 996.9, 0.0, 47.85, 0)
    add(ba("pottedplant1"), 999.25, 0.0, 41.9, 0)
    add(ba("pottedplant1"), 1001.75, 0.0, 41.9, 0)
    add(ba("exitsign"), 1000.5, 4.25, 41.58, 0)
    add(ba("signstaffonly"), 999.5, 2.75, 51.42, 180)
    add(ba("bathroomsignwallmounted"), 1003.0, 2.75, 51.42, 180)
    # behind the partition: the devices' stock, the cleaning station, lockers, the delivery spot; the toilets
    # two storage shelves (2.97 x 0.5 each) for the devices: along the back wall and the left wall of the stock room
    add(ba("storageshelf"), 998.6, 0.0, 56.15, 180)
    add(ba("storageshelf"), 996.85, 0.0, 54.9, 90)   # GO-BLUEPRINT-3: was 54.6 - its end stood in the delivery spot
    add(ba("cleaningstation"), 998.55, 0.0, 51.99, 0)
    add(ba("uniformlocker"), 1004.25, 0.0, 51.75, 270)
    add(ba("handtruckspawner"), 1001.0, 0.0, 52.0, 270)
    add(ba("deliveryspot"), 997.32, 0.0, 52.45, 0)
    for x in (1002.0, 1003.0):
        add(ba("toiletstall"), x, 0.0, 56.0, 180)
    add(ba("sinkwallmodern"), 1004.42, 0.5, 53.0, 270)
    add(ba("sinksoapwallmounted"), 1004.42, 1.5, 52.75, 270)
    add(ba("mirrorwallmounted"), 1004.42, 1.5, 53.5, 270)
    add(ba("handdryer"), 1001.58, 1.5, 53.0, 90)
    # light, sound, cameras
    for x in (998.5, 999.5, 1000.5, 1001.5, 1002.5):
        add(ba("ceilinglamptube"), x, 4.5, 43.0, 0)
        add(ba("ceilinglamptube"), x, 4.5, 50.0, 0)
    for z in (44.5, 45.5, 46.5, 47.5, 48.5):
        add(ba("ceilinglamptube"), 998.0, 4.5, z, 90)
        add(ba("ceilinglamptube"), 1003.0, 4.5, z, 90)
    for x, z, yaw in ((999.5, 45.0, 45), (1001.5, 45.0, 135), (999.5, 48.0, 45), (1001.5, 48.0, 135),
                      (998.0, 53.0, 45), (1000.0, 53.0, 45), (1003.0, 53.0, 45), (998.0, 55.0, 135), (1000.0, 55.0, 45), (1003.0, 55.0, 45)):
        add(ba("ceilinglampsquare"), x, 4.5, z, yaw)
    for x, z, yaw in ((996.58, 42.5, 90), (1004.42, 42.5, 270), (996.58, 50.5, 90), (1004.42, 50.5, 270)):
        add(ba("classicloudspeakermedium"), x, 4.0, z, yaw)
    for x, z in ((1000.5, 44.5), (1000.5, 50.5), (999.0, 54.5)):
        add(ba("securitycameraroof"), x, 4.5, z, 0)
    return t, "ba:buildingsize_a", 2, out, {"seats": 15, "desks": 3, "tables": 2, "tvs": 3}


# ── the cable office in an office building J, version 1 ("J1"): 23.8 x 14.8 m ─────────────────────────────────────────
# made from the game's own web development agency of J1 (BusinessLayouts/WebDevelopmentAgency/J1): its ten computer
# workplaces become Going Online's (four technical, two control room, four office workplaces), the meeting room becomes the
# headend (the racks, 0.7 x 1.0 each, a row along its back wall, UPS and generator facing it), the lounge corner the devices'
# stock (two storage shelves); toilets, sinks, kitchen and the rest stay as the game has them
RACKS_START = ["gocmts30", "gosat", "goups"]
RACKS_FULL = ["gocmts30", "goolt", "gosat", "gocas", "govoice", "goiptv", "goups", "gogenerator"]


def office_j1(full):
    t = "GO Kabel-Büro J1 (" + ("Voll" if full else "Start") + ")"
    # the meeting room (table, its seven chairs, the standing TV) and the lounge corner go
    gone = [("largemeetingtable", 1092.27, -103.47), ("expensivetv", 1092.5, -107.0), ("cornersofa02right", 1101.0, -107.0),
            ("coffeetable1", 1101.0, -105.5), ("modernshelfhorizontal", 1098.5, -106.25), ("armchair1", 1104.5, -102.5),
            ("armchair1", 1104.62, -101.06), ("officechair", 1104.26, -106.63), ("officechair", 1104.43, -105.24),
            ("floorlamp", 1090.0, -107.0), ("groundplant03", 1094.5, -107.0)]   # GO-BLUEPRINT-3: where the racks stand
    for x, z in ((1091.17, -105.47), (1093.37, -105.47), (1091.02, -103.47), (1093.52, -103.47), (1091.17, -101.47),
                 (1092.27, -100.72), (1093.37, -101.47)):
        gone.append(("officechair", x, z))
    # which workplace each of the game's computers becomes
    seats = {(1100.0, -96.5): "gotechdesk", (1103.5, -96.5): "gotechdesk", (1107.5, -96.5): "gocontroldesk",
             (1100.0, -94.0): "gotechdesk", (1103.5, -94.0): "gotechdesk", (1107.5, -94.0): "gocontroldesk",
             (1103.98, -99.44): "godesk", (1106.89, -99.43): "godesk", (1106.0, -106.0): "godesk", (1105.97, -102.04): "godesk"}
    def kind(n, x, z):
        if n != "computer":
            return None
        k = next((k for (sx, sz), k in seats.items() if abs(x - sx) < 0.3 and abs(z - sz) < 0.3), "godesk")
        return "goingonline:itemname_" + k
    out = from_game(t, "WebDevelopmentAgency/J1/WebDevelopmentAgency", drop=gone, rename=kind)   # GO-BLUEPRINT-6: keeps the desks' links
    add = lambda n, x, y, z, yaw: (out.append(item(t, n, x, y, z, yaw)), out[-1])[1]
    racks = RACKS_FULL if full else RACKS_START
    row1 = [r for r in racks if r not in ("goups", "gogenerator")]
    for i, r in enumerate(row1):
        add("goingonline:itemname_" + r, 1090.3 + 0.7 * i, 0.0, -106.85, 0)
    for i, r in enumerate([r for r in racks if r in ("goups", "gogenerator")]):
        add("goingonline:itemname_" + r, 1090.3 + 0.7 * i, 0.0, -104.0, 180)
    for x in (1100.0, 1103.0):
        add(ba("storageshelf"), x, 0.0, -107.15, 0)
    desks = sum(1 for i in out if i["itemName"] in ("goingonline:itemname_gotechdesk", "goingonline:itemname_gocontroldesk", "goingonline:itemname_godesk"))
    return t, "ba:buildingsize_j", 1, out, {"desks": desks, "racks": len(racks)}


# ── GO-BLUEPRINT-3 (user 2026-11-11: "Mehr Vorlagen" - other building sizes): two more customer centers and two more
# offices. Where the game has a layout of that size that fits, ours is made from it (its pieces stand where the game puts
# them - nothing in walls): the A1 bank becomes a customer center, the web development agencies of A3 and C1 offices. The
# C1 customer center is planned on the room's shape (from all the game's C1 layouts: walls at x 942.6 / 957.4 and z -7.4 /
# 7.4, the entrance at x 948.5 - 951.5 in front, toilets behind at the right), its back rooms taken from the C1 gift shop.
def from_game(t, spec, drop=(), rename=None, keep=None):
    """A game layout's pieces, copied with new ids and no custom colours: drop = names (all of them) or (name, x, z)
    (that one); rename(name, x, z) -> another item's full name or None; keep(name, x, z) -> only those."""
    global N
    src = json.load(open(os.path.join(LAYOUTS, *spec.split("/")) + ".json", encoding="utf-8-sig"))
    out, seen, newid = [], set(), {}
    for it in src["Items"]:
        n = it["itemName"].split("_", 1)[1]
        x, z = it["position"]["x"], it["position"]["z"]
        twin = (n, round(x, 2), round(it["position"]["y"], 2), round(z, 2))
        if twin in seen:
            continue   # the game's layout has it twice at the same place
        seen.add(twin)
        if abs(x) < 1 and abs(z) < 1:
            continue   # a lamp the game left at the origin
        if keep and not keep(n, x, z):
            continue
        if any((d == n) if isinstance(d, str) else (d[0] == n and abs(d[1] - x) < 0.3 and abs(d[2] - z) < 0.3) for d in drop):
            continue
        c = json.loads(json.dumps(it))
        if rename:
            r = rename(n, x, z)
            if r:
                c["itemName"] = r
        N += 1
        newid[it["id"]] = c["id"] = ident(t, N)
        c["customColors"] = []
        out.append(c)
    # GO-BLUEPRINT-6: the desks' links to their computers and chairs follow the new ids (and the new names) - before, they
    # pointed at the game's ids, and every workplace missed its desk and chair
    byid = {c["id"]: c for c in out}
    for c in out:
        links = []
        for sl in c.get("stackedItems") or []:
            child = byid.get(newid.get(sl.get("childId"), ""))
            if child is not None:
                links.append({"childId": child["id"], "childItemName": child["itemName"], "attachmentIndex": sl.get("attachmentIndex", 0)})
        c["stackedItems"] = links
        if c.get("parentId"):
            c["parentId"] = newid.get(c["parentId"])
    return out


def center_a1():
    """The A1 bank (4.8 x 14.6 m: the hall in front, staff room and toilet behind the partition at z 5.4) as a customer
    center: its consultation desk stays (our workplace on it), the cashier counter becomes a second desk, a device
    table by the entrance, the office lockers of the back room make way for two storage shelves."""
    t = "GO Kundencenter A1"
    out = from_game(t, "Bank/A1/GarmentDistrictBank",
                    drop=["securitypanel", "securitysign", "counter1", "countercorner", "computermonitor", "printer",
                          "officelockers", "officechair", ("deskplant01", 1000.8, -1.76), ("cleaningstation", 997.0, 10.0)],
                    rename=lambda n, x, z: "goingonline:itemname_godesk" if n == "computer" else None)
    add = lambda n, x, y, z, yaw: (out.append(item(t, n, x, y, z, yaw)), out[-1])[1]
    # the second desk where the counter stood, at the right wall: staff at the wall, the customer in front
    d = add(ba("specialemployeedesksmall"), 1000.25, 0.0, -0.9, 90)
    link(d, (add("goingonline:itemname_godesk", 1000.25, 0.74, -0.9, 90), 1), (add(ba("armchair1"), 1001.0, 0.0, -0.9, 270), 2),
         (add(ba("multipurposechair"), 999.25, 0.0, -0.9, 90), 3))
    # the devices on show right of the entrance (it is at x 998 - 1000 in front)
    add("goingonline:itemname_godevicetable", 1001.13, 0.0, -3.0, 270)
    # the back room: two storage shelves for the devices (along the toilet's wall and the left wall), the cleaning
    # station to the back wall between them
    add(ba("storageshelf"), 999.24, 0.0, 8.08, 90)
    add(ba("storageshelf"), 996.85, 0.0, 8.48, 90)
    add(ba("cleaningstation"), 998.2, 0.0, 10.1, 180)
    return t, "ba:buildingsize_a", 1, out, {"seats": 6, "desks": 2, "tables": 1}


def center_c1():
    """A customer center in C1 (14.8 x 14.8 m): four device tables either side of the entrance, 24 waiting seats in two
    blocks with the aisle from the door free, five customer desks in a row, the gift shop's back: staff corner, storage
    shelves for the devices, the delivery spot and three toilets."""
    t = "GO Kundencenter C1"
    back = {"toiletstall", "sink", "mirrorwallmounted", "handdryer", "bathroomsignwallmounted", "storageshelf", "deliveryspot",
            "handtruckspawner", "cleaningstation", "uniformlocker", "counter1", "cheapcoffeemachine", "officechair",
            "loudspeaker2", "wallmountedtv", "securitycamera", "drinksfridge", "groundplant01", "groundplant02", "jobboard"}
    out = from_game(t, "GiftShop/C1/GiftShopWholesalers",
                    keep=lambda n, x, z: n.startswith("ceiling") or (n in back and not (n == "counter1" and z < 5)))
    add = lambda n, x, y, z, yaw: (out.append(item(t, n, x, y, z, yaw)), out[-1])[1]
    for x in (944.0, 946.4, 953.6, 956.0):           # the devices by the shop window, the door (948.5 - 951.5) free
        add("goingonline:itemname_godevicetable", x, 0.0, -6.9, 0)
    add(ba("groundplant02"), 947.9, 0.0, -6.9, 0)
    add(ba("groundplant02"), 952.1, 0.0, -6.9, 0)
    for z in (-2.0, -3.4):                            # 8 x 3 waiting seats facing the desks
        for x in (945.0, 947.2, 952.8, 955.0):
            add(ba("waitingroomchairs"), x, 0.0, z, 0)
    for x in (943.9, 946.4, 948.8, 951.2, 953.6):     # the desks: staff behind, the customer in front
        d = add(ba("specialemployeedesksmall"), x, 0.0, 1.4, 0)
        link(d, (add("goingonline:itemname_godesk", x, 0.74, 1.4, 0), 1), (add(ba("armchair1"), x, 0.0, 2.15, 180), 2),
             (add(ba("multipurposechair"), x, 0.0, 0.4, 0), 3))
    add(ba("largewallclock"), 942.58, 2.5, 1.0, 90)
    add(ba("watercooler"), 943.0, 0.0, -0.6, 90)
    add(ba("exitsign"), 950.0, 4.25, -7.28, 0)
    return t, "ba:buildingsize_c", 1, out, {"seats": 24, "desks": 5, "tables": 4}


def office_a3(full):
    """The A3 web development agency (4.8 x 15 m) as a small cable office: its four computers become Going Online's
    (two technical, two office workplaces), the racks stand along the walls between the reception and the desks - three
    at the right wall, the full set four at each wall."""
    t = "GO Kabel-Büro A3 (" + ("Voll" if full else "Start") + ")"
    kinds = {(1000.5, 105.0): "gotechdesk", (997.5, 105.0): "gotechdesk", (1000.5, 104.0): "godesk", (997.5, 104.0): "godesk"}
    out = from_game(t, "WebDevelopmentAgency/A3/WebDevelopmentAgency",
                    drop=[("multipurposechair", 1000.5, 99.0), ("groundplant02", 1000.13, 101.87), ("groundplant02", 997.0, 101.0)],
                    rename=lambda n, x, z: "goingonline:itemname_" + kinds[(x, z)] if n in ("computer", "desktopcomputer", "laptop") and (x, z) in kinds else None)
    add = lambda n, x, y, z, yaw: (out.append(item(t, n, x, y, z, yaw)), out[-1])[1]
    racks = RACKS_FULL if full else RACKS_START
    spots = [(1000.9, z, 270) for z in (99.9, 100.6, 101.3, 102.0)] + [(997.1, z, 90) for z in (99.9, 100.6, 101.3, 102.0)]
    for r, (x, z, yaw) in zip(racks, spots):
        add("goingonline:itemname_" + r, x, 0.0, z, yaw)
    return t, "ba:buildingsize_a", 3, out, {"desks": 4, "racks": len(racks)}


def office_c1(full):
    """The C1 web development agency (14.8 x 14.8 m) as a cable office: its eight computers become Going Online's
    workplaces (four technical, two control room, two office), the meeting room behind its glass walls becomes the
    headend (the racks in a row along its wall), the corner sofa a storage shelf for the devices."""
    t = "GO Kabel-Büro C1 (" + ("Voll" if full else "Start") + ")"
    kinds = {(953.5, -3.0): "gotechdesk", (956.0, -3.0): "gotechdesk", (953.5, -5.5): "gotechdesk", (956.0, -5.5): "gotechdesk",
             (953.5, -0.5): "gocontroldesk", (956.0, -0.5): "gocontroldesk", (945.5, 5.0): "godesk", (946.5, 5.0): "godesk"}
    out = from_game(t, "WebDevelopmentAgency/C1/WebDevelopmentAgency",
                    drop=["largemeetingtable", "carpetlongbigmodern", "cornersofa01",
                          ("multipurposechair", 943.5, -0.75), ("multipurposechair", 943.5, -6.25), ("multipurposechair", 944.6, -1.5),
                          ("multipurposechair", 944.6, -5.5), ("multipurposechair", 944.7, -2.5), ("multipurposechair", 944.7, -4.5),
                          ("multipurposechair", 944.75, -3.5)],
                    rename=lambda n, x, z: "goingonline:itemname_" + kinds[(x, z)] if n == "desktopcomputer" and (x, z) in kinds else None)
    add = lambda n, x, y, z, yaw: (out.append(item(t, n, x, y, z, yaw)), out[-1])[1]
    racks = RACKS_FULL if full else RACKS_START
    for i, r in enumerate(racks):                      # along the meeting room's wall, facing into it
        add("goingonline:itemname_" + r, 943.1, 0.0, -6.2 + 0.7 * i, 90)
    add(ba("storageshelf"), 949.25, 0.0, 5.5, 90)
    return t, "ba:buildingsize_c", 1, out, {"desks": 8, "racks": len(racks)}


def office_d2(full):
    """GO-BLUEPRINT-4 (user 2026-11-11: "Büro Vorlage D2"): the D2 web development agency (22.8 x 14.8 m) as a cable
    office: its ten computers become Going Online's workplaces (four technical, two control room, four office), the
    meeting room behind its glass walls becomes the headend (the racks in a row along its wall at x 1008.4, facing
    into it), the corner sofa a storage shelf for the devices. The user then: "mit mehr Arbeitsplätzen" - eight more
    (18 in all; the entrance is at x 995.5 - 998.5 in front, far from them): two technical ones completing the middle
    row (x 1002), two control room desks in the headend room against its glass walls (its door, z -102.5 .. -98 on
    the left, and the racks stay free), and two desks each along the top and bottom wall right of the room where
    the planters stood."""
    t = "GO Kabel-Büro D2 (" + ("Voll" if full else "Start") + ")"
    kinds = {(1000.0, -94.0): "gotechdesk", (1000.0, -97.0): "gotechdesk", (1000.0, -103.0): "gotechdesk", (1000.0, -106.0): "gotechdesk",
             (1002.0, -94.0): "gocontroldesk", (1002.0, -106.0): "gocontroldesk",
             (992.5, -94.5): "godesk", (992.5, -99.0): "godesk", (992.5, -101.5): "godesk", (992.5, -106.0): "godesk"}
    out = from_game(t, "WebDevelopmentAgency/D2/WebDevelopmentAgency",
                    drop=["largemeetingtable", "cornersofa01",
                          ("officechair", 1006.25, -100.5), ("officechair", 1006.3, -101.5), ("officechair", 1006.3, -99.5),
                          ("officechair", 1006.4, -102.5), ("officechair", 1006.4, -98.5), ("officechair", 1007.5, -103.25),
                          ("officechair", 1007.5, -97.75),
                          ("colorfulcarpet", 1005.5, -98.0), ("colorfulcarpet", 1005.5, -100.0), ("colorfulcarpet", 1005.5, -102.0),
                          ("colorfulcarpet", 1005.5, -103.0),
                          ("decorativebookshelf", 1005.5, -104.0), ("decorativebookshelf", 1005.5, -96.5),
                          ("groundplant04", 1006.5, -93.0), ("groundplant04", 1006.5, -107.0)],
                    rename=lambda n, x, z: "goingonline:itemname_" + kinds[(x, z)] if n == "desktopcomputer" and (x, z) in kinds else None)
    add = lambda n, x, y, z, yaw: (out.append(item(t, n, x, y, z, yaw)), out[-1])[1]
    racks = RACKS_FULL if full else RACKS_START
    for i, r in enumerate(racks):                      # along the meeting room's wall, facing into it
        add("goingonline:itemname_" + r, 1007.9, 0.0, -103.9 + 0.7 * i, 270)
    add(ba("storageshelf"), 995.5, 0.0, -92.86, 180)
    # the eight more workplaces: (desk x, z, yaw, the workplace); the person's chair 0.75 m behind the desk
    for x, z, yaw, kind in ((1002.0, -97.0, 90, "gotechdesk"), (1002.0, -103.0, 90, "gotechdesk"),
                            (1006.0, -96.55, 180, "gocontroldesk"), (1006.0, -103.95, 0, "gocontroldesk"),
                            (1005.38, -93.15, 180, "godesk"), (1007.38, -93.15, 180, "godesk"),
                            (1005.38, -106.85, 0, "gotechdesk"), (1007.38, -106.85, 0, "gotechdesk")):
        a = math.radians(yaw)
        cx, cz = x + 0.75 * math.sin(a), z + 0.75 * math.cos(a)
        d = add(ba("officedesk1"), x, 0.0, z, yaw)
        link(d, (add("goingonline:itemname_" + kind, x, 0.74, z, yaw), 1), (add(ba("officechair2"), cx, 0.0, cz, (yaw + 180) % 360), 2))
    return t, "ba:buildingsize_d", 2, out, {"desks": 18, "racks": len(racks)}


def png(path, w, h, rgb_rows):
    """A tiny PNG writer (no Pillow needed)."""
    raw = b"".join(b"\x00" + bytes(row) for row in rgb_rows)
    def chunk(k, d):
        c = k + d
        return struct.pack(">I", len(d)) + c + struct.pack(">I", zlib.crc32(c) & 0xFFFFFFFF)
    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    open(path, "wb").write(data)


def thumbnail(path, items, box):
    """A floor plan from above: the room, seats blue, desks teal, device tables green, the rest grey."""
    x0, z0, x1, z1 = box
    W, H = 512, 288
    across = (z1 - z0) >= (x1 - x0)   # the longer side across the picture
    s = min((W - 20) / (z1 - z0), (H - 20) / (x1 - x0)) if across else min((W - 20) / (x1 - x0), (H - 20) / (z1 - z0))
    img = [[(236, 239, 242)] * W for _ in range(H)]
    def rect(ax, az, bx, bz, col):
        if across:   # z across (the entrance on the left), x down
            r0, r1, c0, c1 = (ax - x0) * s, (bx - x0) * s, (az - z0) * s, (bz - z0) * s
        else:        # x across, z up
            r0, r1, c0, c1 = (z1 - bz) * s, (z1 - az) * s, (ax - x0) * s, (bx - x0) * s
        for py in range(max(0, int(10 + r0)), min(H, int(10 + r1) + 1)):
            for px in range(max(0, int(10 + c0)), min(W, int(10 + c1) + 1)):
                img[py][px] = col
    rect(x0, z0, x1, z1, (255, 255, 255))
    sizes = {"waitingroomchairs": (1.81, 0.69, (59, 140, 240)), "specialemployeedesksmall": (2.0, 1.0, (26, 158, 191)),
             "godevicetable": (2.0, 0.5, (46, 160, 90)), "armchair1": (0.66, 0.74, (120, 120, 130)),
             "multipurposechair": (0.59, 0.58, (150, 150, 160)), "storageshelf": (2.97, 0.5, (170, 140, 100)),
             "toiletstall": (1.0, 1.18, (190, 190, 200)), "bigcabinet": (0.7, 1.0, (60, 70, 90)),
             "gocmts30": (0.7, 1.0, (60, 70, 90)), "goolt": (0.7, 1.0, (60, 110, 80)), "gosat": (0.7, 1.0, (130, 70, 60)),
             "gocas": (0.7, 1.0, (100, 75, 130)), "govoice": (0.7, 1.0, (130, 110, 60)), "goiptv": (0.7, 1.0, (70, 90, 180)),
             "goups": (0.7, 1.0, (150, 150, 75)), "gogenerator": (0.7, 1.0, (140, 55, 45)), "officedesk1": (2.0, 1.0, (26, 158, 191)),
             "officedesk2left": (2.0, 1.0, (26, 158, 191)), "officedesk2right": (2.0, 1.0, (26, 158, 191)),
             "officechair2": (0.6, 0.6, (120, 120, 130)), "officechair": (0.6, 0.6, (120, 120, 130)),
             "moderncounter": (1.0, 0.6, (160, 160, 170)), "windowedroomdivider": (2.0, 0.1, (90, 90, 100)),
             "sink": (0.5, 0.45, (190, 190, 200)), "regularchair": (0.5, 0.5, (150, 150, 160)), "roundtable": (1.0, 1.0, (170, 150, 120)), "watercooler": (0.5, 0.5, (120, 180, 220)),
             "groundplant02": (0.5, 0.5, (80, 160, 80)), "pottedplant1": (0.4, 0.4, (80, 160, 80))}
    for it in items:
        n = it["itemName"].split("_", 1)[1]
        if n not in sizes or it["position"]["y"] > 0.1:
            continue
        w, d, col = sizes[n]
        q = it["rotation"]
        yaw = math.degrees(2 * math.atan2(q["y"], q["w"])) % 180
        if 45 < yaw < 135:
            w, d = d, w
        x, z = it["position"]["x"], it["position"]["z"]
        rect(x - w / 2, z - d / 2, x + w / 2, z + d / 2, col)
    png(path, W, H, [[c for p in row for c in p] for row in img])


def write(tpl, size, version, items, info, business):
    empty = json.load(open(os.path.join(LAYOUTS, "Empty", size.split("_")[-1].upper() + str(version), "default.json"), encoding="utf-8"))
    folder = os.path.join(OUT, tpl)
    os.makedirs(folder, exist_ok=True)
    layout = {"Items": items, "interiorDesigns": empty["interiorDesigns"], "BusinessType": business, "BuildingSize": size,
              "BuildingVersion": version, "LayoutName": tpl, "buildNumber": BUILD, "requiredModIds": []}
    json.dump(layout, open(os.path.join(folder, "Layout.json"), "w", encoding="utf-8"), ensure_ascii=False)
    price = float(sum(PRICE.get(i["itemName"], 0) for i in items))
    meta = {"blueprintType": 1, "blueprintVersion": 2, "buildNumber": BUILD, "itemId": 0, "price": price,
            "buildingType": "ba:buildingtype_retail" if business.endswith("cablecenter") else "ba:buildingtype_office",
            "buildingSizeInfo": {"buildingSize": size, "buildingVersion": version}, "requiredModIds": [],
            "otherData": [{"dataElement": 8, "value": business}, {"dataElement": 3, "value": "100"},
                          {"dataElement": 4, "value": str(info.get("desks", 0))},
                          {"dataElement": 5, "value": str(sum(1 for i in items if i["itemName"] == "ba:itemname_storageshelf"))}],
            "isWorkshopReviewPending": False}
    json.dump(meta, open(os.path.join(folder, "Metadata.json"), "w", encoding="utf-8"), indent=4)
    xs = [i["position"]["x"] for i in items]; zs = [i["position"]["z"] for i in items]
    thumbnail(os.path.join(folder, "Thumbnail.png"), items, (min(xs) - 0.3, min(zs) - 0.5, max(xs) + 0.3, max(zs) + 0.6))
    print(f"{tpl}: {len(items)} pieces, ${price:,.0f} - {info}")


for make in (center_a2, center_a1, center_c1):
    tpl, size, version, items, info = make()
    write(tpl, size, version, items, info, "goingonline:businesstype_cablecenter")
for make in (office_j1, office_a3, office_c1, office_d2):
    for full in (False, True):
        tpl, size, version, items, info = make(full)
        write(tpl, size, version, items, info, "goingonline:businesstype_cableprovider")
