"""BIZ-LOC-1: the Business page's texts for the language files.

The page translates every fixed text through BL() (MPBusinessPage.cs): key = "gps_biz_" + slug(english), the slug
made exactly like BSlug there. This script lists the page's English texts (--list) and writes the keys into
Locales/<lang>.json from a translation table (--apply table.py), English into en.json.
"""
import json, re, sys, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "src", "MPBusinessPage.cs")

def slug(s):
    out = []
    for ch in s.lower():
        if ("a" <= ch <= "z") or ("0" <= ch <= "9"):
            out.append(ch)
        elif out and out[-1] != "_":
            out.append("_")
        if len(out) >= 40:
            break
    r = "".join(out).strip("_")
    if len(s) > 40:
        h = 2166136261
        for b in s.encode("utf-8"):
            h ^= b
            h = (h * 16777619) & 0xFFFFFFFF
        r += "_" + format(h, "08x")[:6]
    return r

HELPERS = ("BText(", "BCell(", "BButton(", "BRowButtons", "BNameCell(", "BpText(", "BpButton(", "BpCard(", "BMeasure(",
           "BpMeasure(", "BL(", "empty = ", "label = ", "B_TABS", "new[] {", "B_UNKNOWN =", "BP_RELEASE =", "BizKind(", "BizStatus(")
NOT_UI = re.compile(r"^(BAMP_\w+|[A-Z][a-z]+[A-Z]\w*|Row|Tab|Hit|Fill|Line2?|Box|Bar|Body|Card|Check|Cols|Content|Dim|Divider|Escape|"
                    r"Handle|Low|Notice|Page|Shadow|Splitter|Tick|Avatar|Badge|LeftPane|TableHeader|HeaderCard)$")

def texts():
    src = open(SRC, encoding="utf-8").read()
    found = []
    for line in src.split("\n"):
        t = line.strip()
        if t.startswith("//") or "Logger" in t or "BizErr" in t or "\"ERR " in t or "\"OK " in t:
            continue
        if not any(h in t for h in HELPERS):
            continue
        for m in re.finditer(r'(?<![@\w])\$?"((?:[^"\\\n]|\\.)*)"', t):
            if m.group(0).startswith("$"):
                continue   # interpolated: made into BL("..{0}..") where it is UI text
            s = m.group(1).replace('\\"', '"')
            if len(s) < 2 or not re.search(r"[A-Za-z]{2}", s) or NOT_UI.match(s) or s.startswith(("gps_", "F0", "N0")):
                continue
            if s not in found:
                found.append(s)
    return found

if __name__ == "__main__":
    if sys.argv[1:2] == ["--list"]:
        for s in texts():
            print(slug(s) + "\t" + s)
    elif sys.argv[1:2] == ["--apply"]:
        table = {}
        exec(open(sys.argv[2], encoding="utf-8").read(), table)
        T = table["T"]   # lang -> {english: translation}
        found = texts()
        listed = table["E"]
        missing = [x for x in listed if x not in found]
        if missing:
            sys.exit("not on the page any more (fix the table): " + " | ".join(missing))
        eng = [x for x in found if x in listed]   # internal words (ids, kinds) the page compares stay out
        for lang in sorted(set(T) | {"en"}):
            p = os.path.join(ROOT, "Locales", lang + ".json")
            d = json.load(open(p, encoding="utf-8"))
            tr = T.get(lang, {})
            n = 0
            for s in eng:
                v = s if lang == "en" else tr.get(s)
                if v is None:
                    continue
                d["gps_biz_" + slug(s)] = v
                n += 1
            open(p, "w", encoding="utf-8").write(json.dumps(d, indent=4, ensure_ascii=False) + "\n")
            print(lang, n)
