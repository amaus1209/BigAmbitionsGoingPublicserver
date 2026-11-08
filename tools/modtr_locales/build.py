#!/usr/bin/env python3
"""Language packs for mods with their own Locales/ folder (MOD-TRANSLATIONS-1): tools/modtr_locales/<Mod DLL>/en.json is
the mod's English table (from the server's modtr-templates/Locales/<Mod>/), tools/modtr_locales/<Mod>/langs.py holds
L = {lang: [texts in the order of en.json]}. Writes ModTranslations/Locales/<Mod>/<lang>.json and checks count,
{placeholders}, rich-text tags and line breaks.
    python3 tools/modtr_locales/build.py <Mod DLL> [...]"""
import importlib.util, json, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
ph = lambda s: sorted(re.findall(r"\{[A-Za-z0-9]+\}", s))
tags = lambda s: sorted(re.findall(r"<[^>]+>", s))
ok = True
for mod in sys.argv[1:]:
    en = json.load(open(os.path.join(HERE, mod, "en.json"), encoding="utf-8-sig"))
    keys = list(en)
    spec = importlib.util.spec_from_file_location("langs", os.path.join(HERE, mod, "langs.py")); m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
    out_dir = os.path.join(ROOT, "ModTranslations", "Locales", mod)
    os.makedirs(out_dir, exist_ok=True)
    for lang, vals in m.L.items():
        if len(vals) != len(keys):
            print(f"{mod}/{lang}: {len(vals)} statt {len(keys)}"); ok = False; continue
        bad = [k for k, v in zip(keys, vals) if ph(en[k]) != ph(v) or tags(en[k]) != tags(v) or en[k].count("\n") != v.count("\n")]
        for k in bad: print(f"{mod}/{lang}: {k}: {en[k]!r} -> {vals[keys.index(k)]!r}")
        ok &= not bad
        json.dump(dict(zip(keys, vals)), open(os.path.join(out_dir, f"{lang}.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=4)
    print(mod, len(m.L), "Sprachen", "ok" if ok else "MIT FEHLERN")
sys.exit(0 if ok else 1)
