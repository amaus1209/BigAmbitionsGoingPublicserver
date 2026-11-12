#!/usr/bin/env python3
"""AMP-LOCALE-1: lists the texts of the installed AMP that headless/amp/amp-locale/<lang>.json does not translate yet,
and checks that file (placeholders, links, leading/trailing spaces). Run it after an AMP update.

AMP's Locale.js (WebRoot/Scripts) swaps whole one-line text nodes of the page and its plugin tabs once when they load,
plus texts passed through Locale.l() - menu entries, dialog titles and buttons, the names and descriptions of settings.
Those come from the panel's HTML/JS and from the setting attributes in AMP's assemblies (AMP.dll and ModuleShared.dll
inside the single-file AMP_Linux_x86_64 bundle, the plugin DLLs). Category tabs and drop-down entries never pass
through it and stay as AMP or the template words them.
    python3 tools/amp-locale-check.py [--ads <ADS instance dir>] [<lang>.json]"""
import json
import mmap
import re
import struct
import sys
from html.parser import HTMLParser
from pathlib import Path

PLUGINS = ["ADSModule", "FileManagerPlugin", "LocalFileBackupPlugin", "AnalyticsPlugin", "StorePlugin",
           "GenericModule", "steamcmdplugin", "RCONPlugin", "EmailSenderPlugin", "WebhookPlugin",
           "WebRequestPlugin", "InstanceManagerPlugin", "SystemUserManagerPlugin"]
SKIP_PAGES = {"FirstTimeSetup.html"}          # only shown while ADS is set up the first time
NODE = re.compile(r"^(\s*)([a-zA-Z ][^\n\r  ]+?)(\s*)\Z")   # Locale.ApplyLocale's rule


class Page(HTMLParser):
    def __init__(self, whole_doc):
        super().__init__(convert_charrefs=True)
        self.body, self.skip, self.stack, self.texts, self.icons = not whole_doc, 0, [], [], set()

    def handle_starttag(self, tag, attrs):
        self.body |= tag == "body"
        if tag in ("area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "wbr"):
            return
        self.stack.append((tag, "mat-icon" in (dict(attrs).get("class") or "")))
        self.skip += tag in ("script", "style", "head", "title")

    def handle_endtag(self, tag):
        while self.stack:
            t = self.stack.pop()[0]
            self.skip -= t in ("script", "style", "head", "title")
            if t == tag:
                break

    def handle_data(self, data):
        if self.body and not self.skip and self.stack and re.search(r"[a-zA-Z]\w", data) and "<!-- ko" not in data:
            m = NODE.match(data)
            if m:   # Material Symbols ligatures (<span class="mat-icon">add_circle</span>) must stay untouched
                (self.icons.add if self.stack[-1][1] else self.texts.append)(m.group(2))


JS_TEXTS = [re.compile(r"""Locale\.(?:l|GetLocaleMessage)\(\s*(["'])((?:\\.|(?!\1).)+?)\1\s*[,)]"""),
            re.compile(r"""(?:SideMenuEntryVM|ShowModalAsync|ModalAction|AddSideMenuItem)\(\s*(["'])((?:\\.|(?!\1).)+?)\1""")]


def serstrings(m, i):
    """The UTF-8 SerStrings of the custom-attribute blob whose prolog (01 00) ends at i."""
    out = []
    while i < len(m):
        b = m[i]
        if b < 0x80:
            n, j = b, i + 1
        elif b & 0xC0 == 0x80 and i + 1 < len(m):
            n, j = ((b & 0x3F) << 8) | m[i + 1], i + 2
        else:
            break
        raw = m[j:j + n]
        if n == 0 or len(raw) < n or not re.fullmatch(rb"[\t\n\r\x20-\x7e\xc2-\xf4][\t\n\r\x20-\x7e\x80-\xbf\xc2-\xf4]*", raw):
            break
        try:
            out.append(raw.decode("utf-8"))
        except UnicodeDecodeError:
            break
        i = j + n
    return out


def setting_texts(data):
    """[Name, Description] pairs of the setting attributes in one assembly."""
    found, i = [], data.find(b"\x01\x00")
    while i != -1:
        s = serstrings(data, i + 2)
        if len(s) >= 2 and 0 < len(s[0]) <= 60 and not s[0].endswith(".") and len(s[1].split()) >= 3:
            found += s[:2]
        i = data.find(b"\x01\x00", i + 1)
    return found


def bundled(path, names):
    """Files from a .NET single-file bundle (uncompressed entries), by name."""
    with open(path, "rb") as f:
        m = mmap.mmap(f.fileno(), 0, access=mmap.ACCESS_READ)
        sig = bytes.fromhex("8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae")
        at = m.find(sig)
        if at < 8:
            return {}
        hdr = struct.unpack_from("<q", m, at - 8)[0]
        major, _, count = struct.unpack_from("<IIi", m, hdr)

        def text(o):
            n = shift = 0
            while True:
                b = m[o]
                o += 1
                n |= (b & 0x7F) << shift
                shift += 7
                if b < 0x80:
                    return m[o:o + n].decode("utf-8"), o + n
        _, o = text(hdr + 12)
        o += 40 if major >= 2 else 0
        out = {}
        for _ in range(count):
            off, size = struct.unpack_from("<qq", m, o)
            o += 16
            comp = struct.unpack_from("<q", m, o)[0] if major >= 6 else 0
            o += 9 if major >= 6 else 1
            name, o = text(o)
            if name in names and not comp:
                out[name] = m[off:off + size]
        return out


def main():
    args = sys.argv[1:]
    ads = None
    if args[:1] == ["--ads"]:
        ads, args = Path(args[1]), args[2:]
    if ads is None:
        ads = next((k.parent for k in sorted(Path.home().glob(".ampdata/instances/*/ADSModule.kvp"))), None)
    if ads is None or not (ads / "WebRoot/Scripts/Locale.js").exists():
        sys.exit("Keine ADS-Instanz mit WebRoot/Scripts/Locale.js gefunden (--ads <Ordner> angeben).")
    loc = Path(args[0]) if args else Path(__file__).resolve().parent.parent / "headless/amp/amp-locale/de.json"
    tr = json.load(open(loc, encoding="utf-8-sig"))["Strings"]

    texts, icons = {}, set()

    def add(src, items):
        for t in items:
            texts.setdefault(t, src)

    pages = [(ads / "WebRoot/AMP.html", True)]
    scripts = [ads / "WebRoot/Scripts" / n for n in ("AMP.js", "UI.js", "PluginHandler.js")]
    for p in PLUGINS:
        web = ads / "Plugins" / p / "WebRoot"
        pages += [(f, False) for f in sorted(web.glob("*.html")) if f.name not in SKIP_PAGES]
        scripts += sorted(web.glob("*.js"))
    for f, whole in pages:
        page = Page(whole)
        page.feed(f.read_text(encoding="utf-8-sig", errors="replace"))
        add(f.name, page.texts)
        icons |= page.icons
    for f in scripts:
        src = f.read_text(encoding="utf-8-sig", errors="replace")
        add(f.name, [m.group(2).split(":")[0] for rx in JS_TEXTS for m in rx.finditer(src) if "${" not in m.group(2)])
        add(f.name, [n for n in re.findall(r'\bName:\s*"([^"]{2,60})"', src) if " " in n or n[0].isupper()])
    states = re.search(r"const applicationState = \{(.*?)\};", (ads / "WebRoot/Scripts/AMP.js").read_text(encoding="utf-8-sig"), re.S)
    if states:
        add("AMP.js", re.findall(r":\s*'([^']+)'", states.group(1)))
    for name, data in bundled(ads / "AMP_Linux_x86_64", {"AMP.dll", "ModuleShared.dll"}).items():
        add(name, setting_texts(data))
    for p in PLUGINS:
        dll = ads / "Plugins" / p / f"{p}.dll"
        if dll.exists():
            add(dll.name, setting_texts(dll.read_bytes()))

    problems = []
    for k, v in tr.items():
        if not v.strip():
            problems.append(f"leer: {k!r}")
        if k[:1].isspace() != v[:1].isspace() or k[-1:].isspace() != v[-1:].isspace():
            problems.append(f"Leerzeichen am Rand anders: {k!r}")
        for rx in (r"\{\{\w+\}\}", r"\{\w+\}", r"\]\([^)]+\)", r"`[^`]+`"):
            if sorted(re.findall(rx, k)) != sorted(re.findall(rx, v)):
                problems.append(f"Platzhalter/Link anders als im Englischen: {k!r}")
        if k.strip() in icons:
            problems.append(f"Symbolname übersetzt (zerstört das Symbol): {k!r}")
    missing = {}
    for t, src in texts.items():
        if t not in tr and t.strip() not in icons and re.search(r"[a-z]{2}", t):
            missing.setdefault(src, []).append(t)
    print(f"{loc.name}: {len(tr)} Übersetzungen; AMP {(ads / 'AMPVersion').read_text().strip() if (ads / 'AMPVersion').exists() else '?'}"
          f" zeigt {len(texts)} übersetzbare Texte, davon {sum(map(len, missing.values()))} ohne Übersetzung.")
    for src in sorted(missing):
        print(f"\n[{src}]")
        for t in missing[src]:
            print("   ", json.dumps(t, ensure_ascii=False))
    for p in problems:
        print("FEHLER:", p)
    sys.exit(1 if problems else 0)


if __name__ == "__main__":
    main()
