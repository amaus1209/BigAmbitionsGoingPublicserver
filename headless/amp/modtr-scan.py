#!/usr/bin/env python3
"""MOD-TRANSLATIONS-3: which of the server's mods are missing which game languages - run by bamp-start.sh at every start.

    modtr-scan.py <ModsLocal> <ModTranslations folders, separated by ":"> <out folder>
    (the copy shipped with Going Public Server and the cache of the repository's tables the game downloaded)

For every mod (except Going Public Server and blueprints):
  - with its own Locales/en.json: the game languages neither the mod nor ModTranslations/Locales/<DLL>/ has.
  - without one: if ModTranslations/Texts/<DLL>/ covers it, the languages still missing there; otherwise the mod's
    text candidates (every string constant of its DLL that looks like words, read from the #US metadata heap).
MOD-TRANSLATIONS-4: <out>/ModTranslations/ gets one ready-to-edit file per mod and game language, laid out like the
repository's ModTranslations/ (current translation, else English), plus _english/ references and LIESMICH.txt - to
translate by hand and upload to the repository.
One console line per mod ("[ModTr] ..."), a summary in <out>/report.json. Changes nothing in the game or the mods.
"""
import json, os, re, struct, sys

GAME_LANGS = ["en", "de", "fr", "es", "it", "pt", "nl", "da", "fi", "pl", "cs", "hu", "ro", "lt", "el", "tr", "ru",
              "uk", "ja", "ko", "zh-cn", "zh-tw"]


def user_strings(dll):
    """The #US heap of a .NET assembly: every string literal of its code (what ldstr loads)."""
    b = open(dll, "rb").read()
    pe = struct.unpack_from("<I", b, 0x3C)[0]
    if b[pe:pe + 4] != b"PE\0\0":
        return []
    nsec = struct.unpack_from("<H", b, pe + 6)[0]
    optsz = struct.unpack_from("<H", b, pe + 20)[0]
    opt = pe + 24
    magic = struct.unpack_from("<H", b, opt)[0]
    dd = opt + (96 if magic == 0x10B else 112)
    cli_rva = struct.unpack_from("<I", b, dd + 14 * 8)[0]
    secs = [struct.unpack_from("<8sIIII", b, opt + optsz + i * 40) for i in range(nsec)]

    def off(rva):
        for _, vsize, va, rawsize, rawptr in secs:
            if va <= rva < va + max(vsize, rawsize):
                return rva - va + rawptr
        raise ValueError("rva")
    cli = off(cli_rva)
    md_rva = struct.unpack_from("<I", b, cli + 8)[0]
    md = off(md_rva)
    vlen = struct.unpack_from("<I", b, md + 12)[0]
    p = md + 16 + vlen + 2
    nstreams = struct.unpack_from("<H", b, p)[0]
    p += 2
    us = None
    for _ in range(nstreams):
        so, ssz = struct.unpack_from("<II", b, p)
        p += 8
        e = b.index(b"\0", p)
        name = b[p:e].decode("ascii")
        p = (e + 4) & ~3
        if name == "#US":
            us = (md + so, ssz)
    if not us:
        return []
    start, size = us
    out, i = [], 1
    while i < size:
        c = b[start + i]
        if c & 0x80 == 0:
            n, i = c, i + 1
        elif c & 0xC0 == 0x80:
            n, i = ((c & 0x3F) << 8) | b[start + i + 1], i + 2
        else:
            n, i = ((c & 0x1F) << 24) | (b[start + i + 1] << 16) | (b[start + i + 2] << 8) | b[start + i + 3], i + 4
        if n:
            out.append(b[start + i:start + i + n - 1].decode("utf-16-le", "replace"))
        i += n
    return out


LOGGY = re.compile(r"(exception|failed to|initiali[sz]ed|loaded|loading|unloading|\bnull\b|invoked|resolved|"
                   r"called but|^\[|\bdebug\b|prerequisites|diagnostic)", re.I)


def looks_visible(s):
    """A guess only - the candidates are reviewed before anything is translated."""
    t = s.strip()
    if len(t) < 2 or not re.search(r"[A-Za-z]{2}", t) or LOGGY.search(t):
        return False
    if re.fullmatch(r"[0-9A-Fa-f]{6,8}", t) or re.search(r"[\\/]\w|\.(dll|json|png|jpg|txt|cs)\b", t):
        return False
    if " " not in t:   # a single word: an identifier unless it is a capitalized word or ALL CAPS
        return bool(re.fullmatch(r"[A-Z][a-z]{2,}|[A-Z]{3,}", t))
    return not re.fullmatch(r"[\w.:-]+", t)


def tables(roots, kind, mod):
    """Languages of ModTranslations/<kind>/<mod>/ over all given roots."""
    langs = set()
    for r in roots:
        d = os.path.join(r, kind, mod)
        if os.path.isdir(d):
            langs |= {os.path.splitext(f)[0].lower() for f in os.listdir(d) if f.endswith(".json")}
    return langs


def table(roots, kind, mod, lang):
    """The current table of one language (a later root - the downloaded cache - wins)."""
    t = {}
    for r in roots:
        p = os.path.join(r, kind, mod, lang + ".json")
        if os.path.isfile(p):
            try:
                t.update(json.load(open(p, encoding="utf-8-sig")))
            except Exception:
                pass
    return t


def write(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    json.dump(data, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=4)


def editable(out, roots, kind, mod, english, langs):
    """MOD-TRANSLATIONS-4: one ready file per language in <out>/ModTranslations/<kind>/<mod>/ - the current translation
    where there is one, the English text where not (an untranslated line simply stays English in the game) - and the
    English reference in <out>/ModTranslations/_english/ (the repository's index check compares placeholders with it)."""
    if not langs:
        return
    base = os.path.join(out, "ModTranslations")
    write(os.path.join(base, "_english", kind, mod, "en.json"), english)
    for lang in langs:
        cur = table(roots, kind, mod, lang)
        write(os.path.join(base, kind, mod, lang + ".json"), {k: cur.get(k, v) for k, v in english.items()})


README = """Übersetzungen selbst bearbeiten (Going Public Server)
=====================================================

ModTranslations/ hat genau den Aufbau des Ordners ModTranslations/ im GitHub-Repository:
  Locales/<Mod>/<Sprache>.json  - Mods mit eigenen Übersetzungsdateien (Schlüssel: Text)
  Texts/<Mod>/<Sprache>.json    - Mods mit festem englischem Text (englischer Text: Übersetzung)
  _english/...                  - das englische Original zum Vergleich (nicht ändern)

Jede Datei enthält schon alle Zeilen: übersetzte, wo es eine Übersetzung gibt, sonst den englischen Text.

So geht's:
 1. Die Datei der gewünschten Sprache herunterladen (de = Deutsch, fr = Französisch, ... zh-tw = Chinesisch traditionell).
 2. NUR den Text rechts vom Doppelpunkt übersetzen. Links (der Schlüssel bzw. der englische Text) bleibt genau so.
    Platzhalter wie {0}, {name} oder {businessName} und Zeichen wie <b> ... </b> und \n unverändert übernehmen.
 3. Bei Texts/: nur Zeilen übersetzen, die im Spiel wirklich sichtbar sind. Interne Namen nicht anfassen - eine Zeile,
    die gleich bleibt, ändert nichts.
 4. Auf GitHub im Repository in den gleichen Ordner hochladen (Add file -> Upload files), z. B.
    ModTranslations/Locales/SmartSuite/de.json, und committen. Den Ordner _english/ beim ersten Mal mit hochladen.
 5. GitHub prüft die Platzhalter und erneuert index.json von selbst (Reiter "Actions", grüner Haken). Beim nächsten
    Spielstart haben alle Spieler die Übersetzung. Ein rotes X nennt die Zeile mit dem Fehler.
"""


def main(mods, gps_tr, out):
    roots = [r for r in gps_tr.split(":") if r]
    os.makedirs(out, exist_ok=True)
    report = []
    for name in sorted(os.listdir(mods)):
        d = os.path.join(mods, name)
        if name == "GoingPublicServer" or not os.path.isdir(d):
            continue
        dlls = [f for f in os.listdir(d) if f.lower().endswith(".dll")]
        if not dlls:
            continue   # a blueprint or a data-only mod
        dll = sorted(dlls, key=lambda f: -os.path.getsize(os.path.join(d, f)))[0]
        mod = os.path.splitext(dll)[0]
        loc = os.path.join(d, "Locales")
        entry = {"folder": name, "dll": mod}
        if os.path.isfile(os.path.join(loc, "en.json")):
            own = {os.path.splitext(f)[0].lower() for f in os.listdir(loc) if f.endswith(".json")}
            ours = tables(roots, "Locales", mod)
            # a regional file ("pt-BR") for a game language the mod lacks is used by Going Public Server (MOD-TRANSLATIONS-3)
            regional = {l for l in GAME_LANGS if l not in own and any(o.startswith(l + "-") for o in own)}
            missing = [l for l in GAME_LANGS if l not in own and l not in ours and l not in regional]
            entry.update(kind="locales", own=sorted(own), ours=sorted(ours), missing=missing)
            try:
                english = json.load(open(os.path.join(loc, "en.json"), encoding="utf-8-sig"))
                editable(out, roots, "Locales", mod, english, [l for l in GAME_LANGS if l not in own and l not in regional])
            except Exception as ex:
                print(f"[ModTr] {mod}: en.json nicht lesbar ({ex})")
            print(f"[ModTr] {mod} ({name}): eigene Sprachen {len(own)}, von Going Public Server {len(ours)}"
                  + (f" (+ {', '.join(sorted(regional))} aus der regionalen Datei)" if regional else "")
                  + (f", fehlen: {', '.join(missing)}" if missing else ", alle Spielsprachen vorhanden"))
        else:
            ours = tables(roots, "Texts", mod)
            if ours:
                missing = [l for l in GAME_LANGS if l != "en" and l not in ours]
                entry.update(kind="texts", ours=sorted(ours), missing=missing)
                keys = {}
                for lang in sorted(ours):
                    for k in table(roots, "Texts", mod, lang):
                        keys.setdefault(k, k)
                editable(out, roots, "Texts", mod, keys, [l for l in GAME_LANGS if l != "en"])
                print(f"[ModTr] {mod} ({name}): feste Texte, übersetzt in {len(ours)} Sprache(n)"
                      + (f", fehlen: {', '.join(missing)}" if missing else ", alle Spielsprachen vorhanden"))
            else:
                try:
                    cands = sorted({s for s in user_strings(os.path.join(d, dll)) if looks_visible(s)})
                except Exception as ex:
                    cands = []
                    print(f"[ModTr] {mod} ({name}): DLL nicht lesbar ({ex})")
                entry.update(kind="untranslated", candidates=len(cands))
                if cands:
                    editable(out, roots, "Texts", mod, {c: c for c in cands}, [l for l in GAME_LANGS if l != "en"])
                print(f"[ModTr] {mod} ({name}): keine Übersetzungen - {len(cands)} mögliche Anzeigetexte"
                      + (f" in modtr-templates/ModTranslations/Texts/{mod}/" if cands else ""))
        report.append(entry)
    json.dump(report, open(os.path.join(out, "report.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    open(os.path.join(out, "LIESMICH.txt"), "w", encoding="utf-8").write(README)


if __name__ == "__main__":
    if len(sys.argv) != 4:
        print(__doc__)
        sys.exit(2)
    main(*sys.argv[1:])
