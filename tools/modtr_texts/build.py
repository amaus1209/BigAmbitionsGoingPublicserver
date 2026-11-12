#!/usr/bin/env python3
"""Builds ModTranslations/Texts/{Immersion,BrandSystem}/<lang>.json from tools/modtr_texts/part*.py (lists in the order of
the de.json tables). Checks count, {placeholders}, leading/trailing spaces and line breaks per text.
    python3 tools/modtr_texts/build.py"""
import glob, importlib.util, json, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MODS = {"imm": "Immersion", "brand": "BrandSystem"}
en = {k: list(json.load(open(os.path.join(ROOT, "ModTranslations", "Texts", m, "de.json"), encoding="utf-8")).keys()) for k, m in MODS.items()}
ph = lambda s: sorted(re.findall(r"\{\d\}", s))
ok = True
for part in sorted(glob.glob(os.path.join(os.path.dirname(__file__), "part*.py"))):
    spec = importlib.util.spec_from_file_location("p", part); mod = importlib.util.module_from_spec(spec); spec.loader.exec_module(mod)
    for lang, tabs in mod.L.items():
        for k, m in MODS.items():
            vals = tabs[k]
            if len(vals) != len(en[k]):
                print(f"{lang}/{m}: {len(vals)} statt {len(en[k])}"); ok = False; continue
            for e, v in zip(en[k], vals):
                # Chinese and Japanese separate with full-width punctuation instead of spaces
                cjk = lang in ("ja", "zh-cn", "zh-tw")
                edge_ok = (e.startswith(" ") == v.startswith(" ") or (cjk and v[:1] in "（：")) and \
                          (e.endswith(" ") == v.endswith(" ") or (cjk and v[-1:] in "）："))
                if ph(e) != ph(v) or not edge_ok or e.count("\n") != v.count("\n"):
                    print(f"{lang}/{m}: Form weicht ab: {e!r} -> {v!r}"); ok = False
            json.dump(dict(zip(en[k], vals)), open(os.path.join(ROOT, "ModTranslations", "Texts", m, f"{lang}.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=4)
        print(lang, "ok")
sys.exit(0 if ok else 1)
