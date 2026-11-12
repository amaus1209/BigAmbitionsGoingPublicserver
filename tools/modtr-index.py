#!/usr/bin/env python3
"""MOD-TRANSLATIONS-2/4: checks the translation tables and writes ModTranslations/index.json (version + sha256 per table)
- for the mod's shipped copy and, as a GitHub Action, for the public repository after every upload. The players' games
fetch the repository's set when its version is higher than theirs, so the version goes up by one on any change.

Checks, per table: valid JSON object of texts; placeholders ({0}, {name}) and tags (<b>) as in the English text - for
Texts/ the English text is the key, for Locales/ it is _english/Locales/<Mod>/en.json when that reference exists. A
table that fails stops the index (exit 1) and names the line, so a broken translation never reaches the players.
    python3 tools/modtr-index.py [ModTranslations]"""
import hashlib, json, os, re, sys

root = sys.argv[1] if len(sys.argv) > 1 else "ModTranslations"
rel_ok = re.compile(r"^(Locales|Texts)/[A-Za-z0-9._-]+/[a-z]{2}(-[a-z]{2})?\.json$")
ph = lambda s: sorted(re.findall(r"\{[A-Za-z0-9_]+\}", s))
tags = lambda s: sorted(re.findall(r"<[^<>]+>", s))
files, errors = {}, []
for kind in ("Locales", "Texts"):
    for base, _, names in os.walk(os.path.join(root, kind)):
        for n in sorted(names):
            rel = os.path.relpath(os.path.join(base, n), root).replace(os.sep, "/")
            if not n.endswith(".json"):
                continue
            if not rel_ok.match(rel):
                errors.append(f"{rel}: unerwarteter Name (erwartet <Mod>/<Sprache>.json, z. B. Locales/SmartSuite/de.json)")
                continue
            try:
                data = json.load(open(os.path.join(root, rel), encoding="utf-8-sig"))
            except Exception as ex:
                errors.append(f"{rel}: kein gültiges JSON ({ex})")
                continue
            if not isinstance(data, dict) or not all(isinstance(v, str) for v in data.values()):
                errors.append(f"{rel}: muss \"Schlüssel\": \"Text\"-Paare enthalten")
                continue
            mod = rel.split("/")[1]
            ref = {}
            if kind == "Locales":
                p = os.path.join(root, "_english", "Locales", mod, "en.json")
                if os.path.isfile(p):
                    ref = json.load(open(p, encoding="utf-8-sig"))
            for k, v in data.items():
                en = k if kind == "Texts" else ref.get(k)
                if en is None or k.endswith("__comment"):
                    continue
                if ph(en) != ph(v) or tags(en) != tags(v):
                    errors.append(f"{rel}: \"{k[:60]}\": Platzhalter/Tags passen nicht - englisch {ph(en) + tags(en)}, übersetzt {ph(v) + tags(v)}")
            files[rel] = hashlib.sha256(open(os.path.join(root, rel), "rb").read()).hexdigest()
if errors:
    print("Übersetzungen NICHT übernommen - bitte korrigieren:")
    for e in errors:
        print("  " + e)
    sys.exit(1)
p = os.path.join(root, "index.json")
old = json.load(open(p, encoding="utf-8")) if os.path.exists(p) else {"version": 0, "files": {}}
version = old.get("version", 0) + (1 if old.get("files") != files else 0)
json.dump({"version": version, "files": dict(sorted(files.items()))}, open(p, "w", encoding="utf-8"), indent=1)
print(f"index v{version}: {len(files)} table(s)")
