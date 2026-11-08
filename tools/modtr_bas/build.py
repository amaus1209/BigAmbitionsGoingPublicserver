#!/usr/bin/env python3
"""Builds ModTranslations/Locales/BAScheduler/<lang>.json from tools/modtr_bas/<lang>.py (V = the 399 texts in the order
of en.json, the "bascheduler__comment" key left out). en.json is BAScheduler's own English table, the reference for the
keys and {placeholders}. Checks count, placeholders, rich-text tags and line breaks.
    python3 tools/modtr_bas/build.py [lang ...]"""
import glob, importlib.util, json, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
en = json.load(open(os.path.join(HERE, "en.json"), encoding="utf-8-sig"))
keys = [k for k in en if k != "bascheduler__comment"]
ph = lambda s: sorted(re.findall(r"\{[A-Za-z]+\}", s))
tags = lambda s: sorted(re.findall(r"<[^>]+>", s))
langs = sys.argv[1:] or sorted(os.path.splitext(os.path.basename(p))[0] for p in glob.glob(os.path.join(HERE, "*.py")) if not p.endswith("build.py"))
ok = True
for lang in langs:
    spec = importlib.util.spec_from_file_location(lang, os.path.join(HERE, lang + ".py")); mod = importlib.util.module_from_spec(spec); spec.loader.exec_module(mod)
    if len(mod.V) != len(keys):
        print(f"{lang}: {len(mod.V)} statt {len(keys)} Texte"); ok = False; continue
    bad = [k for k, v in zip(keys, mod.V) if ph(en[k]) != ph(v) or tags(en[k]) != tags(v) or en[k].count("\n") != v.count("\n")]
    for k in bad: print(f"{lang}: {k}: {en[k]!r} -> {mod.V[keys.index(k)]!r}")
    ok &= not bad
    out = {"bascheduler__comment": f"BAScheduler in '{lang}', shipped by Going Public Server (machine translation - review welcome). If BAScheduler ships its own {lang}.json, that one is used."}
    out.update(zip(keys, mod.V))
    json.dump(out, open(os.path.join(ROOT, "ModTranslations", "Locales", "BAScheduler", f"{lang}.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=4)
    print(lang, "ok" if not bad else "MIT FEHLERN")
sys.exit(0 if ok else 1)
