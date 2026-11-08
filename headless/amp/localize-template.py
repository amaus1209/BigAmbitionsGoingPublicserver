#!/usr/bin/env python3
"""Write the AMP template's settings and ports in another language (AMP itself does not translate template fields).

    localize-template.py <lang> <out-dir>            all template files, localized, into <out-dir>
    localize-template.py <lang> --manifest <file>    only the settings manifest (an instance's configmanifest.json)

Texts come from i18n/<lang>.json next to this script: FieldName -> [DisplayName, Description], plus "_categories",
"_ports" (Ref -> [Name, Description]) and "_enums" (shown value -> translation). Anything missing stays English. "en"
(or a language without a file) writes the English originals. The ServerLanguage default becomes <lang>.
"""
import json
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
FILES = ["bigambitions.kvp", "bigambitionsconfig.json", "bigambitionsmetaconfig.json",
         "bigambitionsports.json", "bigambitionsupdates.json"]


def load_texts(lang):
    f = HERE / "i18n" / f"{lang}.json"
    return json.loads(f.read_text(encoding="utf-8")) if f.exists() else {}


def tr_category(value, cats):
    parts = value.split(":")   # "Name:icon[:order]" - only the name is text
    parts[0] = cats.get(parts[0], parts[0])
    return ":".join(parts)


def localize_config(lang, texts):
    cfg = json.loads((HERE / "bigambitionsconfig.json").read_text(encoding="utf-8"))
    cats, enums = texts.get("_categories", {}), texts.get("_enums", {})
    for f in cfg:
        t = texts.get(f["FieldName"])
        if t:
            f["DisplayName"], f["Description"] = t[0], t[1]
        f["Category"] = tr_category(f["Category"], cats)
        f["Subcategory"] = tr_category(f["Subcategory"], cats)
        if f.get("EnumValues") and f["FieldName"] != "ServerLanguage":
            f["EnumValues"] = {k: enums.get(v, v) for k, v in f["EnumValues"].items()}
        if f["FieldName"] == "ServerLanguage":
            f["DefaultValue"] = lang
    return cfg


KEEP = ("DedicatedSession", "DedicatedLoadSave")   # the lists the server fills in: its worlds and its saves


def keep_lists(cfg, old_path):
    """The worlds and saves the server wrote into an instance's manifest stay when it is written anew (user 2026-11-02:
    after an update AMP listed none - it reads the manifest only when the instance starts, and the server fills them in
    again only once the game runs)."""
    try:
        old = {f.get("FieldName"): f for f in json.loads(Path(old_path).read_text(encoding="utf-8-sig"))}
    except (OSError, ValueError, AttributeError):
        return
    for f in cfg:
        o = old.get(f["FieldName"])
        if f["FieldName"] in KEEP and o and isinstance(o.get("EnumValues"), dict):
            for k, v in o["EnumValues"].items():
                f["EnumValues"].setdefault(k, v)


def localize_ports(texts):
    ports = json.loads((HERE / "bigambitionsports.json").read_text(encoding="utf-8"))
    for p in ports:
        t = texts.get("_ports", {}).get(p.get("Ref", ""))
        if t:
            p["Name"], p["Description"] = t[0], t[1]
    return ports


def dump(obj, path):
    Path(path).write_text(json.dumps(obj, indent=4, ensure_ascii=False) + "\n", encoding="utf-8")


def main(argv):
    if len(argv) < 3:
        print(__doc__)
        return 2
    lang = argv[1].strip().lower()
    texts = load_texts(lang)
    if lang != "en" and not texts:
        print(f"no AMP translation for '{lang}' (headless/amp/i18n/{lang}.json) - English texts", file=sys.stderr)
    if argv[2] == "--manifest":
        cfg = localize_config(lang, texts)
        keep_lists(cfg, argv[3])
        dump(cfg, argv[3])
        print(f"settings manifest in '{lang}': {argv[3]}")
        return 0
    out = Path(argv[2])
    out.mkdir(parents=True, exist_ok=True)
    for name in FILES:
        shutil.copyfile(HERE / name, out / name)
    dump(localize_config(lang, texts), out / "bigambitionsconfig.json")
    dump(localize_ports(texts), out / "bigambitionsports.json")
    print(f"template in '{lang}': {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
