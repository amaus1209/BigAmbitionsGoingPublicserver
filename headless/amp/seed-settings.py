#!/usr/bin/env python3
"""SETTINGS-SEED-1: every setting of the AMP template gets its key in bamp-server.json, with its default.

AMP's start only MERGES its settings into keys already in bamp-server.json - a key missing from the file never gets the
value set on AMP's settings page. Run at three moments, the same code each time:
  * AMP's update stage "Settings for AMP" (user report 2026-10-31: on a NEW instance the custom world values were lost -
    the keys only came in with the first start, after AMP had already written the file, and that first start created
    the world with the defaults); with the keys there after the Update, the first start already carries AMP's values;
  * bamp-start.sh at every start (a setting the template gained in an update);
  * install-into-amp.sh (servers set up from this repository).

    seed-settings.py <bamp-server.json> <bigambitionsconfig.json>
"""
import json
import sys


def seed(conf_path, manifest_path):
    c = json.load(open(conf_path, encoding="utf-8-sig"))
    added = []
    for s in json.load(open(manifest_path, encoding="utf-8")):
        key = s.get("ParamFieldName", "")
        if not key.startswith("$.") or key[2:] in c:
            continue
        v = s.get("DefaultValue", "")
        if s.get("InputType") == "checkbox":
            v = str(v).lower() == "true"
        elif s.get("InputType") == "number":
            try:
                v = int(v)
            except ValueError:
                v = float(v)   # e.g. the camera's target height 1.5
        c[key[2:]] = v
        added.append(key[2:])
    if added:
        json.dump(c, open(conf_path, "w", encoding="utf-8"), indent=2, ensure_ascii=False)
    return added


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    try:
        added = seed(sys.argv[1], sys.argv[2])
    except (OSError, ValueError) as e:
        sys.exit("[BAMP] seed-settings: " + str(e))
    if added:
        print("[BAMP] bamp-server.json ergänzt um: " + ", ".join(added) + " (AMP trägt seine Werte beim nächsten Start ein).")
