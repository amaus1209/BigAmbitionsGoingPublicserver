#!/usr/bin/env python3
"""Draws Going Online's pictures (needs Pillow):
  assets/GO_AppIcon.png  512x512, the app's icon in the style of Going Public Server's app icons (a coloured rounded
                         square with a soft shadow): "Going Online" large in two lines, below it a row of four white
                         tiles, the four products as small logos: TV, Internet, Telefon, IPTV (user 2026-11-06:
                         "Kabelnetz weg und dafür TV, Internet, Telefon, IPTV als Logo im Logo", "mit Schriftzug
                         Going Online", then "Logo V2" out of four layouts)
  preview.png            the Workshop thumbnail (the logo large on a dark background, the four products below)

The lettering uses Poppins ExtraBold, the line below the Workshop picture Poppins Bold (SIL Open Font License, both
from AMP's web folder, or the fonts in $GO_FONT_HEAVY / $GO_FONT); without them it falls back to DejaVu Sans Bold.

Run: python3 goingonline/tools/make_icons.py
"""
import math
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
S = 4                      # drawn four times larger, scaled down for smooth edges
W = 512 * S
WHITE = (255, 255, 255, 255)
TEAL = (0, 137, 123, 255)
U = 8                      # a glyph is drawn on 100 x 100 units of U pixels each
AMP_FONTS = os.path.expanduser("~/.ampdata/instances/ADS01/WebRoot/Fonts/")
FALLBACK = ["/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"]
FONTS = [os.environ.get("GO_FONT", ""), AMP_FONTS + "Poppins-Bold.ttf"] + FALLBACK
HEAVY = [os.environ.get("GO_FONT_HEAVY", ""), AMP_FONTS + "Poppins-ExtraBold.ttf", AMP_FONTS + "Poppins-Bold.ttf"] + FALLBACK


def P(x, y):
    return (x * S, y * S)


def u(*v):
    return [x * U for x in v]


def canvas():
    return Image.new("L", (100 * U, 100 * U), 0)


def rounded(mask, r):
    """Rounds every corner of a mask by about r units: blur it, then cut at half."""
    return mask.filter(ImageFilter.GaussianBlur(r * U)).point(lambda v: 255 if v >= 128 else 0)


def fit(mask, size):
    """The glyph cut to what it covers and centred on the canvas, its longer side `size` units."""
    box = mask.getbbox()
    g = mask.crop(box)
    k = size * U / max(g.size)
    g = g.resize((max(1, round(g.width * k)), max(1, round(g.height * k))), Image.LANCZOS)
    out = canvas()
    out.paste(g, ((out.width - g.width) // 2, (out.height - g.height) // 2))
    return out


# ── the four products ────────────────────────────────────────────────────────────────────────────────────────────────

def glyph_tv():
    """A television with a rabbit-ear antenna and two feet."""
    m = canvas()
    d = ImageDraw.Draw(m)
    for x in (31, 69):
        d.line(u(50, 30, x, 10), fill=255, width=6 * U)
        d.ellipse(u(x - 5, 5, x + 5, 15), fill=255)
    d.rounded_rectangle(u(8, 28, 92, 82), 12 * U, fill=255)     # cabinet
    d.rounded_rectangle(u(18, 38, 82, 72), 7 * U, fill=0)       # screen
    d.rounded_rectangle(u(22, 80, 33, 92), 3 * U, fill=255)     # feet
    d.rounded_rectangle(u(67, 80, 78, 92), 3 * U, fill=255)
    return fit(m, 82)


def glyph_internet():
    """A globe: the outline, one meridian, the equator and two parallels."""
    m = canvas()
    d = ImageDraw.Draw(m)
    c, r = 50, 44
    d.ellipse(u(c - r, c - r, c + r, c + r), outline=255, width=8 * U)
    d.ellipse(u(c - 19, c - r, c + 19, c + r), outline=255, width=7 * U)
    d.line(u(c - r, c, c + r, c), fill=255, width=7 * U)
    for dy in (-21, 21):
        hw = math.sqrt(r * r - dy * dy) - 3
        d.line(u(c - hw, c + dy, c + hw, c + dy), fill=255, width=6 * U)
    return fit(m, 82)


def glyph_phone():
    """A telephone handset: one arc around a point up and to the right, thin in the middle (the grip), thick at both
    ends (earpiece and mouthpiece)."""
    m = canvas()
    d = ImageDraw.Draw(m)
    cx, cy, ro = 74, 26, 62

    def inner(a):
        # the ends reach far in towards the face, the grip in between is narrow
        if a <= 100 or a >= 168:
            return 30.0
        if a <= 110:
            return 30.0 + (a - 100) / 10 * 19
        if a >= 158:
            return 30.0 + (168 - a) / 10 * 19
        return 49.0

    angles = [70 + i * 0.5 for i in range(int((200 - 70) / 0.5) + 1)]
    pt = lambda r, a: (U * (cx + r * math.cos(math.radians(a))), U * (cy + r * math.sin(math.radians(a))))
    d.polygon([pt(ro, a) for a in angles] + [pt(inner(a), a) for a in reversed(angles)], fill=255)
    return fit(rounded(m, 3.5), 80)


def glyph_iptv():
    """A flat screen on a stand with a play button: television over the internet."""
    m = canvas()
    d = ImageDraw.Draw(m)
    d.rounded_rectangle(u(6, 12, 94, 72), 10 * U, fill=255)     # screen
    d.rounded_rectangle(u(15, 21, 85, 63), 5 * U, fill=0)
    d.polygon(u(41, 30, 41, 54, 62, 42), fill=255)              # play
    d.rectangle(u(44, 70, 56, 82), fill=255)                    # stand
    d.rounded_rectangle(u(26, 80, 74, 89), 4 * U, fill=255)
    return fit(rounded(m, 1.2), 84)


GLYPHS = (("TV", glyph_tv), ("Internet", glyph_internet), ("Telefon", glyph_phone), ("IPTV", glyph_iptv))


def font(size, heavy=False):
    for f in (HEAVY if heavy else FONTS):
        if f and os.path.exists(f):
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()


def font_fit(text, width):
    """The largest size at which the text is at most `width` pixels wide."""
    lo, hi = 8, 2000
    while hi - lo > 1:
        mid = (lo + hi) // 2
        if font(mid).getlength(text) <= width:
            lo = mid
        else:
            hi = mid
    return font(lo)


def logo():
    """The icon at W x W: the teal square, "Going Online" in two lines as large as fits, below it TV, Internet, Telefon
    and IPTV in a row. The same space above the lettering, between it and the row, and below the row."""
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    box = (72 * S, 62 * S, 440 * S, 430 * S)
    sh = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle((box[0], box[1] + 14 * S, box[2], box[3] + 14 * S), 62 * S, fill=(0, 0, 0, 120))
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(14 * S)))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(box, 62 * S, fill=TEAL)
    pad, tile, tgap = 28, 74, 10
    lines = ("Going", "Online")

    def measure(size):
        f = font(size, heavy=True)
        boxes = [d.textbbox((0, 0), t, font=f) for t in lines]
        lead = round(size * 0.05)                 # ink gap between the lines (the g's tail and the O)
        height = sum(b[3] - b[1] for b in boxes) + lead
        width = max(b[2] - b[0] for b in boxes)
        return f, boxes, lead, height, width

    room_h, room_w = (368 - 3 * pad - tile) * S, 312 * S
    lo, hi = 8, 2000
    while hi - lo > 1:
        mid = (lo + hi) // 2
        _, _, _, hh, ww = measure(mid)
        if hh <= room_h and ww <= room_w:
            lo = mid
        else:
            hi = mid
    f, boxes, lead, height, _ = measure(lo)
    space = pad * S + (room_h - height) / 3        # what the lettering leaves over goes evenly into the three gaps
    y = 62 * S + space
    for text, (l, t, r, btm) in zip(lines, boxes):
        d.text((W / 2 - (l + r) / 2, y - t), text, font=f, fill=WHITE)
        y += (btm - t) + lead
    row = 4 * tile + 3 * tgap
    x0, ty = 256 - row / 2, (62 * S + 2 * space + height) / S
    teal = Image.new("RGBA", (W, W), TEAL)
    for i, (_, draw) in enumerate(GLYPHS):
        tx = x0 + i * (tile + tgap)
        d.rounded_rectangle((P(tx, ty), P(tx + tile, ty + tile)), 15 * S, fill=WHITE)
        g = round(tile * 0.68 * S)
        glyph = draw().resize((g, g), Image.LANCZOS)
        img.paste(teal.crop((0, 0, g, g)), (round((tx + tile / 2) * S - g / 2), round((ty + tile / 2) * S - g / 2)), glyph)
    return img


def icon(big=None):
    return (big or logo()).resize((512, 512), Image.LANCZOS)


def preview(big):
    img = Image.new("RGBA", (1024, 1024), (18, 24, 32, 255))
    d = ImageDraw.Draw(img)
    for i in range(1024):   # a soft vertical gradient
        a = int(40 * i / 1024)
        d.line([(0, i), (1024, i)], fill=(18 + a // 3, 30 + a, 40 + a, 255))
    # the logo's square 640 px wide, near the top (the picture around it is transparent margin and shadow)
    k = 640 / 368
    size = round(512 * k)
    img.alpha_composite(big.resize((size, size), Image.LANCZOS), (round(512 - 256 * k), round(100 - 62 * k)))
    text = " · ".join(name for name, _ in GLYPHS)
    f = font_fit(text, 760)
    l, t, r, b = d.textbbox((0, 0), text, font=f)
    d.text((512 - (l + r) / 2, 858 - (t + b) / 2), text, font=f, fill=(190, 230, 225, 255))
    return img.convert("RGB")


# ── the items of the game (GAME-TYPES-1): 256 x 256, drawn 4x, transparent like the game's own item icons ─────────────

def _fit_box(text, width, height):
    """The largest bold size at which the text fits `width` x `height` pixels."""
    lo, hi = 8, 2000
    while hi - lo > 1:
        mid = (lo + hi) // 2
        f = font(mid, heavy=True)
        l, t, r, b = f.getbbox(text)
        if r - l <= width and b - t <= height:
            lo = mid
        else:
            hi = mid
    return font(lo, heavy=True)


def _rack(d, body, label, accent):
    """A 19-inch rack: dark cabinet, rows of units with lights, a label plate."""
    k = S
    d.rounded_rectangle((60 * k, 18 * k, 196 * k, 238 * k), radius=14 * k, fill=body)
    for i in range(6):
        y = 34 + i * 26
        d.rounded_rectangle((74 * k, y * k, 182 * k, (y + 18) * k), radius=4 * k, fill=(24, 27, 32, 255))
        for j in range(4):
            d.ellipse(((150 + j * 8) * k, (y + 6) * k, (155 + j * 8) * k, (y + 11) * k), fill=accent if (i + j) % 3 else (120, 230, 120, 255))
    d.rounded_rectangle((70 * k, 196 * k, 186 * k, 228 * k), radius=6 * k, fill=(240, 244, 248, 255))
    f = _fit_box(label, 100 * k, 20 * k)
    l, t, r, b = d.textbbox((0, 0), label, font=f)
    d.text((128 * k - (l + r) / 2, 212 * k - (t + b) / 2), label, font=f, fill=(30, 34, 40, 255))


def _box(d, body, label, leds, antenna=False):
    """A small device: a box with front lights and a label."""
    k = S
    if antenna:
        d.rounded_rectangle((84 * k, 40 * k, 94 * k, 110 * k), radius=5 * k, fill=(40, 44, 50, 255))
        d.rounded_rectangle((162 * k, 40 * k, 172 * k, 110 * k), radius=5 * k, fill=(40, 44, 50, 255))
    d.rounded_rectangle((36 * k, 100 * k, 220 * k, 196 * k), radius=18 * k, fill=body)
    for j in range(leds):
        d.ellipse(((56 + j * 16) * k, 170 * k, (64 + j * 16) * k, 178 * k), fill=(120, 230, 120, 255) if j % 2 == 0 else (90, 190, 255, 255))
    f = _fit_box(label, 120 * k, 40 * k)
    l, t, r, b = d.textbbox((0, 0), label, font=f)
    d.text((128 * k - (l + r) / 2, 136 * k - (t + b) / 2), label, font=f, fill=(245, 248, 250, 255))


def _card(d):
    k = S
    d.rounded_rectangle((36 * k, 64 * k, 220 * k, 192 * k), radius=16 * k, fill=(196, 158, 60, 255))
    d.rounded_rectangle((60 * k, 100 * k, 108 * k, 140 * k), radius=6 * k, fill=(230, 210, 140, 255))
    for x in (72, 84, 96):
        d.line(((x * k, 100 * k), (x * k, 140 * k)), fill=(170, 140, 60, 255), width=2 * k)
    f = _fit_box("PAY-TV", 90 * k, 26 * k)
    l, t, r, b = d.textbbox((0, 0), "PAY-TV", font=f)
    d.text((168 * k - (l + r) / 2, 160 * k - (t + b) / 2), "PAY-TV", font=f, fill=(60, 44, 10, 255))


def _service(glyph):
    """A service line: the product's glyph white on the app's teal disc."""
    img = Image.new("RGBA", (256 * S, 256 * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((16 * S, 16 * S, 240 * S, 240 * S), fill=TEAL)
    g = fit(glyph(), 62).resize((150 * S, 150 * S), Image.LANCZOS)
    img.paste(Image.new("RGBA", g.size, WHITE), (53 * S, 53 * S), g)
    return img


def items():
    """The item icons: equipment as racks, devices as boxes, the smartcard, three service lines."""
    racks = [("GO_Item_Cmts30.png", "CMTS 3.0", (70, 78, 90, 255)), ("GO_Item_Cmts31.png", "CMTS 3.1", (40, 92, 104, 255)),
             ("GO_Item_Olt.png", "OLT", (44, 98, 66, 255)), ("GO_Item_Sat.png", "SAT", (118, 60, 52, 255)),
             ("GO_Item_Cas.png", "CAS", (90, 66, 118, 255)), ("GO_Item_Voice.png", "VoIP", (118, 98, 52, 255)),
             ("GO_Item_Iptv.png", "IPTV", (36, 40, 48, 255))]
    out = []
    for name, label, body in racks:
        img = Image.new("RGBA", (256 * S, 256 * S), (0, 0, 0, 0))
        _rack(ImageDraw.Draw(img), body, label, (90, 190, 255, 255))
        out.append((name, img))
    boxes = [("GO_Item_Modem30.png", "3.0", (62, 70, 82, 255), 5, True), ("GO_Item_Modem31.png", "3.1", (40, 92, 104, 255), 5, True),
             ("GO_Item_Ont.png", "ONT", (44, 98, 66, 255), 4, False), ("GO_Item_Stb.png", "IPTV", (36, 40, 48, 255), 3, False)]
    for name, label, body, leds, ant in boxes:
        img = Image.new("RGBA", (256 * S, 256 * S), (0, 0, 0, 0))
        _box(ImageDraw.Draw(img), body, label, leds, ant)
        out.append((name, img))
    img = Image.new("RGBA", (256 * S, 256 * S), (0, 0, 0, 0))
    _card(ImageDraw.Draw(img))
    out.append(("GO_Item_Card.png", img))
    out += [("GO_Item_Internet.png", _service(glyph_internet)), ("GO_Item_Tv.png", _service(glyph_tv)), ]
    for name, img in out:
        img.resize((256, 256), Image.LANCZOS).save(os.path.join(ROOT, "assets", name), optimize=True)
    return [n for n, _ in out]


def main():
    os.makedirs(os.path.join(ROOT, "assets"), exist_ok=True)
    big = logo()
    icon(big).save(os.path.join(ROOT, "assets", "GO_AppIcon.png"), optimize=True)
    preview(big).save(os.path.join(ROOT, "preview.png"), optimize=True)
    print("assets/GO_AppIcon.png, preview.png, " + ", ".join(items()))


if __name__ == "__main__":
    main()
