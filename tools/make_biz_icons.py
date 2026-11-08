#!/usr/bin/env python3
"""Draws the item icons of the new businesses (BUSINESS-PACK-1, src/Business/BizPack.cs) into assets/BP_*.png:
256 x 256 with a transparent background, like the game's own item icons. Needs Pillow. The lettering uses Poppins
Bold from AMP's web folder (SIL Open Font License), else DejaVu Sans Bold.

Run: python3 tools/make_biz_icons.py
"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
S = 4
FONTS = [os.path.expanduser("~/.ampdata/instances/ADS01/WebRoot/Fonts/Poppins-Bold.ttf"),
         "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"]


def font(size):
    for f in FONTS:
        if os.path.exists(f):
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()


def fit(text, w, h):
    lo, hi = 6, 900
    while hi - lo > 1:
        mid = (lo + hi) // 2
        l, t, r, b = font(mid).getbbox(text)
        if r - l <= w and b - t <= h:
            lo = mid
        else:
            hi = mid
    return font(lo)


def label(d, text, cx, cy, w, h, fill):
    f = fit(text, w * S, h * S)
    l, t, r, b = d.textbbox((0, 0), text, font=f)
    d.text((cx * S - (l + r) / 2, cy * S - (t + b) / 2), text, font=f, fill=fill)


def new():
    img = Image.new("RGBA", (256 * S, 256 * S), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def R(d, x0, y0, x1, y1, fill, r=10):
    d.rounded_rectangle((x0 * S, y0 * S, x1 * S, y1 * S), radius=r * S, fill=fill)


def E(d, x0, y0, x1, y1, fill, outline=None, width=0):
    d.ellipse((x0 * S, y0 * S, x1 * S, y1 * S), fill=fill, outline=outline, width=width * S)


GREEN, BLUE, AMBER = (120, 230, 120, 255), (90, 190, 255, 255), (255, 190, 60, 255)
DARK, PANEL, WHITE = (36, 40, 48, 255), (24, 27, 32, 255), (245, 248, 250, 255)


def leds(d, x, y, n=4):
    for j in range(n):
        E(d, x + j * 9, y, x + j * 9 + 5, y + 5, GREEN if j % 2 == 0 else BLUE)


def rack(name, units, text, w=136, body=(70, 78, 90, 255)):
    img, d = new()
    x0 = 128 - w // 2
    R(d, x0, 18, x0 + w, 238, body, 14)
    h = 150 / units
    for i in range(units):
        y = 32 + i * h
        R(d, x0 + 12, y, x0 + w - 12, y + h - 8, PANEL, 4)
        leds(d, x0 + w - 52, y + (h - 8) / 2 - 2)
    R(d, x0 + 10, 196, x0 + w - 10, 228, WHITE, 6)
    label(d, text, 128, 212, w - 30, 20, (30, 34, 40, 255))
    return name, img


def blade():
    img, d = new()
    R(d, 30, 70, 226, 186, (70, 78, 90, 255), 14)
    for i in range(3):
        y = 86 + i * 30
        R(d, 44, y, 212, y + 22, PANEL, 4)
        leds(d, 168, y + 8)
    label(d, "BLADE", 128, 214, 150, 26, (230, 236, 242, 255))
    return "BP_Blade.png", img


def mainframe():
    img, d = new()
    R(d, 26, 18, 230, 238, (44, 52, 64, 255), 14)
    for col in range(2):
        x = 40 + col * 98
        for i in range(7):
            y = 32 + i * 22
            R(d, x, y, x + 82, y + 15, PANEL, 3)
            leds(d, x + 44, y + 5, 3)
    R(d, 40, 196, 216, 228, WHITE, 6)
    label(d, "MAINFRAME", 128, 212, 160, 20, (30, 34, 40, 255))
    return "BP_Mainframe.png", img


def washer(name, door, text, body=(236, 240, 244, 255)):
    img, d = new()
    R(d, 40, 24, 216, 232, body, 18)
    R(d, 52, 36, 204, 66, (200, 206, 214, 255), 8)
    E(d, 170, 44, 186, 60, BLUE)
    E(d, 66, 84, 190, 208, (120, 128, 140, 255))
    E(d, 78, 96, 178, 196, door)
    label(d, text, 108, 51, 90, 18, (60, 66, 76, 255))
    return name, img


def dryer():
    name, img = washer("BP_Dryer.png", (70, 78, 92, 255), "DRY")
    d = ImageDraw.Draw(img)
    for i in range(3):
        y = 130 + i * 14
        d.arc(((96) * S, (y - 6) * S, (160) * S, (y + 6) * S), 200, 340, fill=(255, 210, 120, 255), width=3 * S)
    return name, img


def scrubber():
    img, d = new()
    R(d, 118, 30, 132, 150, (60, 66, 76, 255), 6)
    R(d, 92, 26, 158, 42, (60, 66, 76, 255), 6)
    R(d, 70, 140, 186, 196, (240, 180, 60, 255), 16)
    E(d, 60, 186, 196, 230, (80, 86, 96, 255))
    E(d, 74, 194, 182, 222, (150, 156, 166, 255))
    return "BP_Scrubber.png", img


def extractor():
    img, d = new()
    R(d, 64, 90, 170, 220, (110, 190, 120, 255), 18)
    R(d, 82, 70, 152, 96, (80, 150, 90, 255), 10)
    d.arc((150 * S, 60 * S, 236 * S, 170 * S), 260, 80, fill=(60, 66, 76, 255), width=10 * S)
    R(d, 196, 160, 236, 176, (60, 66, 76, 255), 6)
    E(d, 76, 212, 100, 236, (60, 66, 76, 255)); E(d, 134, 212, 158, 236, (60, 66, 76, 255))
    return "BP_Extractor.png", img


def window():
    img, d = new()
    R(d, 60, 150, 196, 230, (90, 160, 230, 255), 14)
    R(d, 70, 140, 186, 156, (60, 120, 200, 255), 6)
    R(d, 120, 30, 132, 140, (70, 76, 86, 255), 5)
    R(d, 70, 22, 182, 40, (70, 76, 86, 255), 6)
    R(d, 64, 40, 188, 48, (40, 44, 52, 255), 3)
    return "BP_Window.png", img


def fold():
    img, d = new()
    R(d, 28, 150, 228, 170, (220, 214, 200, 255), 6)
    R(d, 44, 170, 56, 232, (150, 140, 120, 255), 3); R(d, 200, 170, 212, 232, (150, 140, 120, 255), 3)
    for i, c in enumerate([(120, 170, 230, 255), (240, 140, 140, 255), (140, 210, 150, 255), (250, 220, 120, 255)]):
        y = 132 - i * 22
        R(d, 70, y, 186, y + 20, c, 6)
    return "BP_Fold.png", img


def changer():
    img, d = new()
    R(d, 66, 24, 190, 232, (70, 150, 96, 255), 16)
    R(d, 84, 44, 172, 96, (20, 40, 28, 255), 8)
    label(d, "$ 1 5 10", 128, 70, 76, 22, (140, 255, 170, 255))
    R(d, 96, 118, 160, 128, (20, 30, 24, 255), 4)
    R(d, 104, 150, 152, 176, (230, 236, 240, 255), 6)
    E(d, 118, 190, 138, 210, (240, 200, 80, 255))
    return "BP_Changer.png", img


def disc(name, colour, draw):
    img, d = new()
    E(d, 16, 16, 240, 240, colour)
    draw(d)
    return name, img


def it_support(d):
    d.arc((70 * S, 64 * S, 186 * S, 180 * S), 180, 360, fill=WHITE, width=14 * S)
    R(d, 60, 118, 90, 170, WHITE, 10); R(d, 166, 118, 196, 170, WHITE, 10)
    d.line((178 * S, 170 * S, 150 * S, 196 * S), fill=WHITE, width=8 * S)
    E(d, 132, 186, 154, 206, WHITE)


def software(d):
    label(d, "</>", 128, 128, 150, 90, WHITE)


def cloud(d):
    for box in [(62, 112, 132, 182), (100, 82, 180, 162), (148, 112, 200, 172)]:
        E(d, *box, fill=WHITE)
    R(d, 62, 140, 200, 182, WHITE, 20)


def clean(d):
    R(d, 104, 108, 160, 200, WHITE, 12)
    R(d, 116, 84, 150, 112, WHITE, 6)
    R(d, 82, 84, 122, 96, WHITE, 4)
    for x, y in [(70, 70), (58, 100), (78, 120)]:
        E(d, x, y, x + 10, y + 10, WHITE)


def main():
    out = [
        blade(), rack("BP_Rack.png", 6, "RACK"), mainframe(),
        scrubber(), extractor(), window(),
        washer("BP_Washer.png", (150, 200, 240, 255), "8-12 kg"),
        washer("BP_WasherBig.png", (150, 200, 240, 255), "27 kg", body=(200, 206, 214, 255)),
        dryer(), fold(), changer(),
        disc("BP_ItSupport.png", (40, 110, 220, 255), it_support),
        disc("BP_Software.png", (70, 80, 200, 255), software),
        disc("BP_Cloud.png", (30, 150, 200, 255), cloud),
        disc("BP_Clean.png", (40, 160, 170, 255), clean),
    ]
    os.makedirs(os.path.join(ROOT, "assets"), exist_ok=True)
    for name, img in out:
        img.resize((256, 256), Image.LANCZOS).save(os.path.join(ROOT, "assets", name), optimize=True)
    print(", ".join(n for n, _ in out))


if __name__ == "__main__":
    main()
