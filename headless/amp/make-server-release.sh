#!/usr/bin/env bash
# Build the two files of a GitHub release for AMP servers that do not build the mod themselves:
#   GoingPublicServer-Server.zip   everything install-into-amp.sh puts into an instance's App.RootDir (the built mod,
#                                  the server-only preloader with Unity Doorstop, bamp-start.sh, the
#                                  player pages). The template's update stage "Going Public Server download" fetches
#                                  the newest release's copy and unpacks it into the RootDir.
#   GoingPublicServer-<ver>.zip    the mod for the players (the same build - the server refuses other builds).
#
#   headless/amp/make-server-release.sh [--build] <out-dir>
#       --build: compile the mod and the preloader first; needs the .NET SDK and the game's Managed folder
#                (BAMP_GAME_DIR, default: the first AMP instance found with 1331550/Big Ambitions_Data/Managed).
set -euo pipefail
REPO="$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)"
BUILD=0
[ "${1:-}" = "--build" ] && { BUILD=1; shift; }
OUT="${1:?Zielordner angeben}"
mkdir -p "$OUT"
OUT="$(cd "$OUT" && pwd)"

if [ "$BUILD" = 1 ]; then
    GAME="${BAMP_GAME_DIR:-}"
    if [ -z "$GAME" ]; then
        for d in "$HOME"/.ampdata/instances/*/bigambitions/1331550; do
            [ -d "$d/Big Ambitions_Data/Managed" ] && { GAME="$d"; break; }
        done
    fi
    [ -n "$GAME" ] || { echo "Spieldateien nicht gefunden - BAMP_GAME_DIR=<.../1331550> setzen." >&2; exit 1; }
    dotnet build "$REPO/BigAmbitionsMP.csproj" -c Release -nologo "-p:GameDir=$GAME/" "-p:ModDeployDir=$REPO/bin/amp-deploy/"
    dotnet build "$REPO/headless/preloader/BampSteamless.csproj" -c Release -nologo
fi

# Stage = a fresh RootDir: install-into-amp.sh fills it exactly as it fills an instance. No settings file and no key
# in it, so nothing instance-specific gets in; its own web-root link goes into a throwaway folder.
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
mkdir -p "$STAGE/root/1331550" "$STAGE/webroot"
BAMP_AMP_WEBROOT="$STAGE/webroot" "$REPO/headless/amp/install-into-amp.sh" "$STAGE/root" >/dev/null
R="$STAGE/root"
[ -f "$R/bamp-mod/GoingPublicServer/BigAmbitionsMP.dll" ] || { echo "Kein Build - mit --build aufrufen." >&2; exit 1; }
[ -f "$R/1331550/winhttp.dll" ] || { echo "Server-Vorlader fehlt - mit --build aufrufen." >&2; exit 1; }
printf '%s\n' "Unity Doorstop 4.3.0 (winhttp.dll), unmodified - LGPL-2.1 - source: https://github.com/NeighTools/UnityDoorstop" \
    "Harmony (bamp-preloader/0Harmony.dll) - MIT - source: https://github.com/pardeike/Harmony" \
    > "$R/1331550/bamp-preloader/THIRD-PARTY.txt"

rm -f "$OUT"/GoingPublicServer-*.zip
python3 - "$R" "$OUT/GoingPublicServer-Server.zip" <<'PY'
import os, sys, zipfile
root, out = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for base, dirs, files in os.walk(root):
        dirs.sort()
        for f in sorted(files):
            full = os.path.join(base, f)
            z.write(full, os.path.relpath(full, root).replace(os.sep, "/"))
PY
PLAYER="$(ls "$R/client"/GoingPublicServer-*.zip)"
VER="$(basename "$PLAYER" | sed -E 's/^GoingPublicServer-([^-]+)-.*/\1/')"
cp "$PLAYER" "$OUT/GoingPublicServer-$VER.zip"
echo "Release $VER:"
ls -l "$OUT"/GoingPublicServer-*.zip
