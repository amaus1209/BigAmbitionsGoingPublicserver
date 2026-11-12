#!/usr/bin/env bash
# Builds Going Online and packs it for the Steam Workshop (and for ModsLocal):
#   <out>/GoingOnline-<version>-<build>.zip   with GoingOnline/ = GoingOnline.dll, Dependencies/0Harmony.dll, Locales/,
#   assets/, preview.png, LICENSE, THIRD-PARTY-NOTICES.txt (Harmony's MIT licence, GO-PATCHES-1)
# <build> = the first 8 hex digits of the DLL's SHA-256 (as Going Public Server names its builds).
#
#   goingonline/tools/package.sh [<Big Ambitions folder>] [<out folder>]
# The game folder holds "Big Ambitions_Data/Managed" (default: the AMP test instance's copy).
set -euo pipefail
HERE="$(cd "$(dirname "$(readlink -f "$0")")/.." && pwd)"
GAME="${1:-$HOME/.ampdata/instances/BigAmbitionsGoingPublicserver03/bigambitions/1331550/}"
OUT="${2:-$HERE/dist}"
mkdir -p "$OUT"
OUT="$(cd "$OUT" && pwd)"

python3 -I "$HERE/tools/locales.py" >/dev/null
dotnet build "$HERE/GoingOnline.csproj" -c Release -p:GameDir="$GAME" -nologo -v q
DLL="$HERE/bin/Release/net48/GoingOnline.dll"
[ -f "$DLL" ] || { echo "build missing: $DLL" >&2; exit 1; }

python3 -I - "$HERE" "$OUT" <<'PY'
import hashlib, pathlib, re, sys, zipfile
here, out = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
dll = here / "bin/Release/net48/GoingOnline.dll"
ver = re.search(r"<Version>([^<]+)</Version>", (here / "GoingOnline.csproj").read_text(encoding="utf-8")).group(1)
build = hashlib.sha256(dll.read_bytes()).hexdigest()[:8]
name = f"GoingOnline-{ver}-{build}.zip"
files = [(dll, "GoingOnline.dll"), (here / "preview.png", "preview.png"), (here / "LICENSE", "LICENSE"),
         (here / "THIRD-PARTY-NOTICES.txt", "THIRD-PARTY-NOTICES.txt"),
         (dll.parent / "0Harmony.dll", "Dependencies/0Harmony.dll"),   # GO-PATCHES-1: the game loads a mod's Dependencies/ first
         ]
for src, rel in files:
    if not src.exists(): sys.exit(f"missing for the package: {src}")
files += [(f, "assets/" + f.name) for f in sorted((here / "assets").glob("GO_*.png"))]   # the app icon and the items' icons
files += [(f, "assets/" + f.name) for f in sorted((here / "assets").glob("GO_*.txt"))]   # the van livery's font and pictures (GO-LIVERY-1)
files += [(f, "assets/models/" + f.name) for f in sorted((here / "assets" / "models").glob("*.gom"))]   # own 3D models (GO-MODELS-1)
files += [(f, "assets/models/" + f.name) for f in sorted((here / "assets" / "models").glob("*.txt"))]   # the van's texture fields (GO-LIVERY-1)
files += [(f, "assets/blueprints/" + f.parent.name + "/" + f.name) for f in sorted((here / "assets" / "blueprints").glob("*/*"))]   # templates for the interior installation firms (GO-BLUEPRINT-2)
files += [(f, "Locales/" + f.name) for f in sorted((here / "Locales").glob("*.json"))]
for old in out.glob("GoingOnline-*.zip"):
    if old.name != name:
        old.unlink()
with zipfile.ZipFile(out / name, "w", zipfile.ZIP_DEFLATED) as z:
    for src, rel in files:
        z.write(src, "GoingOnline/" + rel)
print(out / name)
PY
