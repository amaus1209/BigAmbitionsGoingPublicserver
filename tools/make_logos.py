#!/usr/bin/env python3
"""LOGO-SHAPES-1: draws the logo shapes for the mod's businesses in the game's style - a solid white glyph with cut-out
details on transparent ground, 512 x 512, mode LA (as the game's own StreamingAssets/LogoShapes). Drawn at 2048 px and
scaled down for smooth edges. Writes assets/logos/gps_*.png (and with --sheet a preview sheet).
Run: <venv with Pillow>/python -I tools/make_logos.py <repo>/assets/logos [--sheet out.png]"""
import math, os, sys
from PIL import Image, ImageDraw, ImageChops

S = 2048

def canvas():
    return Image.new("L", (S, S), 0)

def rr(d, box, r, fill=255):
    d.rounded_rectangle(box, radius=r, fill=fill)

def circ(d, cx, cy, r, fill=255):
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=fill)

def thick_line(d, pts, w, fill=255):
    d.line(pts, fill=fill, width=w, joint="curve")
    for (x, y) in (pts[0], pts[-1]):
        circ(d, x, y, w / 2, fill)

def arc(d, cx, cy, r, a0, a1, w, fill=255):
    d.arc((cx - r, cy - r, cx + r, cy + r), a0, a1, fill=fill, width=w)
    for a in (a0, a1):   # round caps
        t = math.radians(a)
        circ(d, cx + (r - w / 2) * math.cos(t), cy + (r - w / 2) * math.sin(t), w / 2, fill)

def star4(d, cx, cy, r, fill=255):
    k = r * 0.28
    d.polygon([(cx, cy - r), (cx + k, cy - k), (cx + r, cy), (cx + k, cy + k), (cx, cy + r), (cx - k, cy + k), (cx - r, cy), (cx - k, cy - k)], fill=fill)

def rotate(img, deg, cx=S / 2, cy=S / 2):
    return img.rotate(deg, resample=Image.BICUBIC, center=(cx, cy))

def gear(d, cx, cy, r, teeth, fill=255, hole=0.42):
    pts = []
    for i in range(teeth * 4):
        a = 2 * math.pi * i / (teeth * 4)
        rr_ = r if (i % 4) in (0, 1) else r * 0.80
        pts.append((cx + rr_ * math.cos(a), cy + rr_ * math.sin(a)))
    d.polygon(pts, fill=fill)
    circ(d, cx, cy, r * hole, 0 if fill else 255)

# ── the shapes ───────────────────────────────────────────────────────────────────────────────────────────────────────

def cable_dish():
    cx, cy = 860, 1010
    bowl = canvas(); bd = ImageDraw.Draw(bowl)
    bd.ellipse((cx - 600, cy - 330, cx + 600, cy + 330), fill=255)
    bd.rectangle((0, 0, S, cy), fill=0)                    # keep the lower half: a bowl open to the top
    thick_line(bd, [(cx, cy), (cx, cy - 440)], 70)         # the arm to the focus
    circ(bd, cx, cy - 480, 100)                            # the feed
    bowl = rotate(bowl, -35, cx, cy)                       # facing up-right
    im = canvas(); d = ImageDraw.Draw(im)
    d.polygon([(700, 1150), (860, 1150), (980, 1800), (580, 1800)], fill=255)   # the mount
    rr(d, (420, 1760, 1140, 1880), 40)                     # the foot
    im = ImageChops.lighter(im, bowl); d = ImageDraw.Draw(im)
    fx, fy = cx + 480 * math.sin(math.radians(35)), cy - 480 * math.cos(math.radians(35))
    for r in (250, 410, 570):                              # the signal
        arc(d, fx, fy, r, 275, 355, 76)
    return im

def cable_router():
    im = canvas(); d = ImageDraw.Draw(im)
    rr(d, (220, 1120, 1828, 1640), 130)
    for x in (480, 680, 880):                              # status lights
        circ(d, x, 1380, 62, 0)
    rr(d, (1180, 1340, 1620, 1420), 40, 0)                 # ports
    rr(d, (360, 1640, 560, 1740), 30); rr(d, (1488, 1640, 1688, 1740), 30)   # feet
    thick_line(d, [(470, 1130), (330, 540)], 90)           # antennas
    thick_line(d, [(1578, 1130), (1718, 540)], 90)
    for r in (190, 360, 530):                              # wifi
        arc(d, 1024, 1010, r, 215, 325, 80)
    circ(d, 1024, 960, 70)
    return im

def cable_tv():
    im = canvas(); d = ImageDraw.Draw(im)
    rr(d, (200, 620, 1848, 1640), 110)
    rr(d, (330, 750, 1718, 1510), 50, 0)                   # screen
    d.polygon([(890, 900), (890, 1360), (1250, 1130)], fill=255)   # play
    d.polygon([(760, 1640), (1288, 1640), (1380, 1840), (668, 1840)], fill=255)   # stand
    thick_line(d, [(1024, 600), (700, 200)], 70)           # rabbit ears
    thick_line(d, [(1024, 600), (1380, 230)], 70)
    circ(d, 1024, 610, 120)
    return im

def cable_coax():
    lay = canvas(); d = ImageDraw.Draw(lay)
    rr(d, (990, 140, 1058, 470), 30)                       # centre pin
    cx, cy, r = 1024, 640, 300                             # the nut
    d.polygon([(cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a))) for a in range(30, 390, 60)], fill=255)
    circ(d, cx, cy, 120, 0); circ(d, cx, cy, 52)           # the bore and the pin in it
    rr(d, (874, 900, 1174, 1290), 50)                      # crimp sleeve
    for y in (980, 1080, 1180):
        rr(d, (874, y, 1174, y + 34), 10, 0)
    rr(d, (930, 1260, 1118, 1950), 80)                     # the cable
    return rotate(lay, -32, 1024, 1024)

def it_headset():
    im = canvas(); d = ImageDraw.Draw(im)
    arc(d, 1024, 1060, 720, 190, 350, 150)                 # headband
    rr(d, (190, 980, 520, 1590), 150); rr(d, (1528, 980, 1858, 1590), 150)   # ear cups
    rr(d, (300, 1080, 410, 1490), 50, 0); rr(d, (1638, 1080, 1748, 1490), 50, 0)
    arc(d, 900, 1360, 560, 90, 160, 90)                    # mic boom
    rr(d, (830, 1830, 1130, 1950), 60)                     # microphone
    return im

def it_laptop():
    im = canvas(); d = ImageDraw.Draw(im)
    rr(d, (360, 380, 1688, 1330), 70)
    rr(d, (460, 480, 1588, 1230), 30, 0)                   # screen
    gear(d, 1024, 855, 300, 8)
    d.polygon([(170, 1400), (1878, 1400), (1760, 1620), (288, 1620)], fill=255)   # base
    rr(d, (880, 1400, 1168, 1460), 20, 0)                  # notch
    return im

def soft_code():
    im = canvas(); d = ImageDraw.Draw(im)
    rr(d, (170, 270, 1878, 1778), 130)
    rr(d, (170, 520, 1878, 560), 0, 0)                     # title bar
    for x in (330, 470, 610):
        circ(d, x, 395, 55, 0)
    thick_line(d, [(770, 860), (500, 1150), (770, 1440)], 120, 0)    # <
    thick_line(d, [(1278, 860), (1548, 1150), (1278, 1440)], 120, 0) # >
    thick_line(d, [(1130, 820), (918, 1480)], 110, 0)                # /
    return im

def cloud_arrows():
    im = canvas(); d = ImageDraw.Draw(im)
    circ(d, 680, 1080, 360); circ(d, 1090, 860, 470); circ(d, 1500, 1110, 330)
    rr(d, (330, 1080, 1830, 1460), 320)
    d.polygon([(740, 1110), (900, 920), (1060, 1110)], fill=0); rr(d, (850, 1080, 950, 1360), 30, 0)    # up
    d.polygon([(1110, 1180), (1270, 1370), (1430, 1180)], fill=0); rr(d, (1220, 900, 1320, 1210), 30, 0)  # down
    return im

def cloud_servers():
    im = canvas(); d = ImageDraw.Draw(im)
    for y0 in (250, 750, 1250):
        rr(d, (330, y0, 1718, y0 + 420), 90)
        cy = y0 + 210
        rr(d, (480, cy - 35, 1100, cy + 35), 30, 0)
        circ(d, 1420, cy, 62, 0); circ(d, 1580, cy, 62, 0)
    rr(d, (450, 1690, 650, 1820), 30); rr(d, (1398, 1690, 1598, 1820), 30)
    return im

def clean_spray():
    im = canvas(); d = ImageDraw.Draw(im)
    rr(d, (720, 860, 1420, 1880), 160)                     # bottle
    rr(d, (900, 690, 1240, 900), 30)                       # neck
    rr(d, (780, 430, 1400, 700), 70)                       # head
    rr(d, (520, 480, 800, 600), 40)                        # nozzle
    thick_line(d, [(820, 700), (690, 1010)], 90)           # trigger
    rr(d, (850, 1190, 1290, 1570), 60, 0)                  # label
    star4(d, 1070, 1380, 150)
    for (x, y, r) in ((400, 430, 42), (360, 540, 52), (400, 650, 42), (250, 480, 36), (250, 600, 36), (140, 540, 30)):
        circ(d, x, y, r)
    return im

def clean_bucket():
    im = canvas(); d = ImageDraw.Draw(im)
    for (x, y, r) in ((700, 820, 150), (920, 720, 200), (1180, 750, 175), (1400, 840, 140)):
        circ(d, x, y, r)                                   # foam
    d.ellipse((440, 260, 1608, 1520), outline=255, width=80)   # handle (its lower half hides behind the bucket)
    d.polygon([(470, 960), (1578, 960), (1440, 1860), (608, 1860)], fill=255)   # bucket
    rr(d, (380, 880, 1668, 1010), 50)                      # rim
    rr(d, (560, 1250, 1490, 1300), 25, 0); rr(d, (590, 1520, 1460, 1570), 25, 0)   # hoops
    star4(d, 1740, 420, 170); star4(d, 300, 330, 120); star4(d, 1830, 820, 90)
    return im

def laundry_washer():
    im = canvas(); d = ImageDraw.Draw(im)
    rr(d, (360, 200, 1688, 1850), 120)
    rr(d, (360, 500, 1688, 540), 0, 0)                     # the panel line
    circ(d, 540, 350, 75, 0)                               # the knob
    rr(d, (1260, 320, 1560, 380), 25, 0)                   # the display
    circ(d, 1024, 1190, 500, 0)                            # the gap around the door
    circ(d, 1024, 1190, 430)                               # the door ring
    circ(d, 1024, 1190, 350, 0)                            # the glass
    water = canvas(); wd = ImageDraw.Draw(water)
    top = [(660 + i * 12, 1215 + 50 * math.sin(i * 12 / 105.0)) for i in range(62)]
    wd.polygon(top + [(1400, 1600), (650, 1600)], fill=255)
    mask = canvas(); md = ImageDraw.Draw(mask); circ(md, 1024, 1190, 300)
    return ImageChops.lighter(im, ImageChops.multiply(water, mask))

def cable_fiber():
    im = canvas(); d = ImageDraw.Draw(im)
    thick_line(d, [(330, 1720), (720, 1330)], 230)         # the sleeve the strands come out of
    for t in (0.18, 0.5, 0.82):                            # its rings
        x, y = 330 + (720 - 330) * t, 1720 + (1330 - 1720) * t
        thick_line(d, [(x - 95, y - 95), (x + 95, y + 95)], 30, 0)
    def bez(p0, p1, p2, n=60):
        return [((1 - u) ** 2 * p0[0] + 2 * (1 - u) * u * p1[0] + u * u * p2[0],
                 (1 - u) ** 2 * p0[1] + 2 * (1 - u) * u * p1[1] + u * u * p2[1]) for u in (i / n for i in range(n + 1))]
    for end, ctrl in (((1230, 330), (800, 900)), ((1560, 560), (1000, 1000)), ((1700, 960), (1150, 1200))):
        pts = bez((720, 1330), ctrl, end)
        thick_line(d, pts, 64)
        circ(d, end[0], end[1], 120)                       # the light at the end
        circ(d, end[0], end[1], 62, 0)
        circ(d, end[0], end[1], 34)
    star4(d, 1720, 260, 150); star4(d, 1880, 560, 80)
    return im

def cable_home():
    im = canvas(); d = ImageDraw.Draw(im)
    rr(d, (1360, 360, 1540, 760), 24)                      # the chimney
    d.polygon([(1024, 230), (1850, 990), (1650, 990), (1650, 1820), (398, 1820), (398, 990), (198, 990)], fill=255)
    for r in (150, 290, 430):                              # wifi, cut out of the house
        arc(d, 1024, 1600, r, 228, 312, 78, 0)
    circ(d, 1024, 1600, 70, 0)
    return im

def soft_terminal():
    im = canvas(); d = ImageDraw.Draw(im)
    rr(d, (170, 270, 1878, 1778), 130)
    rr(d, (170, 520, 1878, 560), 0, 0)
    for x in (330, 470, 610):
        circ(d, x, 395, 55, 0)
    thick_line(d, [(470, 880), (800, 1150), (470, 1420)], 120, 0)   # >
    rr(d, (930, 1360, 1480, 1470), 40, 0)                            # _
    return im

def laundry_shirt():
    im = canvas(); d = ImageDraw.Draw(im)
    d.polygon([(700, 330), (1348, 330), (1780, 560), (1630, 930), (1460, 850), (1460, 1780), (588, 1780), (588, 850), (418, 930), (268, 560)], fill=255)
    d.ellipse((800, 190, 1248, 520), fill=0)               # collar
    for (x, y, r) in ((880, 1220, 150), (1160, 1060, 110), (1200, 1420, 90)):
        circ(d, x, y, r, 0); circ(d, x, y, r - 45)         # bubbles as rings
    return im

SHAPES = {
    "gps_cable1": cable_dish, "gps_cable2": cable_coax, "gps_cable3": cable_router, "gps_cable4": cable_tv,
    "gps_cable5": cable_fiber, "gps_cable6": cable_home,
    "gps_itsupport1": it_headset, "gps_itsupport2": it_laptop, "gps_softwaredev1": soft_code, "gps_softwaredev2": soft_terminal,
    "gps_cloud1": cloud_arrows, "gps_cloud2": cloud_servers,
    "gps_cleaning1": clean_spray, "gps_cleaning2": clean_bucket,
    "gps_laundromat1": laundry_washer, "gps_laundromat2": laundry_shirt,
}

def main(argv):
    out = argv[1]
    os.makedirs(out, exist_ok=True)
    tiles = []
    for name, fn in SHAPES.items():
        mask = fn().resize((512, 512), Image.LANCZOS)
        img = Image.merge("LA", (Image.new("L", (512, 512), 255), mask))
        img.save(os.path.join(out, name + ".png"), optimize=True)
        tiles.append((name, img))
    if "--sheet" in argv:
        sheet_path = argv[argv.index("--sheet") + 1]
        cols = 5; rows = (len(tiles) + cols - 1) // cols
        sheet = Image.new("RGBA", (cols * 200, rows * 200), (40, 44, 52, 255))
        for i, (name, img) in enumerate(tiles):
            t = img.convert("RGBA").resize((180, 180), Image.LANCZOS)
            sheet.alpha_composite(t, ((i % cols) * 200 + 10, (i // cols) * 200 + 10))
        sheet.save(sheet_path)
    print(len(tiles), "logo shapes written to", out)

if __name__ == "__main__":
    main(sys.argv)
