#!/usr/bin/env python3
"""Copies Going Online's texts (goingonline/Locales/en.json, de.json) into Going Public Server's Locales, where the
module (GOING-ONLINE-1) finds them. Existing keys keep their place, new ones come at the end; the files keep their
format (json.dumps with indent 4, as they are written). Other languages fall back to English.

Run after goingonline/tools/locales.py:  python3 -I goingonline/tools/merge_into_server.py
"""
import json
import os

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO = os.path.dirname(HERE)

for lang in ("en", "de"):
    src = json.load(open(os.path.join(HERE, "Locales", lang + ".json"), encoding="utf-8"))
    path = os.path.join(REPO, "Locales", lang + ".json")
    text = open(path, encoding="utf-8").read()
    dst = json.loads(text)
    # texts of keys the provider no longer has go out
    for k in [k for k in dst if k.startswith(("go_", "goingonline:", "goingonline_", "help_goingonline")) and k not in src]:
        del dst[k]
    changed = sum(1 for k, v in src.items() if dst.get(k) != v)
    dst.update(src)
    out = json.dumps(dst, ensure_ascii=False, indent=4) + "\n"
    if out != text:
        open(path, "w", encoding="utf-8").write(out)
    print(f"{lang}: {changed} texts added or changed, {len(dst)} in all")
