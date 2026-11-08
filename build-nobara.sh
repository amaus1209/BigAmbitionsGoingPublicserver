#!/usr/bin/env bash
# Build Going Public (with the built-in Telemetry API) on Nobara / Fedora / any Linux and install it into the
# game's Proton prefix. Also moves an old separate "BA Telemetry Bridge" mod out of ModsLocal (into a backup
# folder next to it - nothing is deleted).
#
#   ./build-nobara.sh                  build Release + install + move the old bridge away
#   ./build-nobara.sh --dev            build the Dev configuration (diagnostics on)
#   ./build-nobara.sh --game-dir DIR   game folder, if it is not found automatically
#   ./build-nobara.sh --no-build       only look for the game and the old bridge (nothing is built)
#   ./build-nobara.sh --dry-run        show what would happen, change nothing
#
# Needs the .NET SDK once:  sudo dnf install dotnet-sdk-8.0
set -euo pipefail

APP_ID=1331550
HERE="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"
CONFIG=Release
GAME_DIR=""
BUILD=1
DRY=0

while [ $# -gt 0 ]; do
    case "$1" in
        --dev) CONFIG=Dev ;;
        --game-dir) GAME_DIR="${2:?--game-dir needs a folder}"; shift ;;
        --no-build) BUILD=0 ;;
        --dry-run) DRY=1; BUILD=0 ;;
        -h|--help) sed -n '2,13p' "$0"; exit 0 ;;
        *) echo "Unknown option: $1 (see --help)" >&2; exit 2 ;;
    esac
    shift
done

say()  { printf '==> %s\n' "$*"; }
warn() { printf 'WARNUNG: %s\n' "$*" >&2; }
run()  { if [ "$DRY" = 1 ]; then printf '   (dry-run) %s\n' "$*"; else "$@"; fi; }

# ── Steam libraries (native, Flatpak, Snap; every library from libraryfolders.vdf) ──────────────────────────
steam_libraries() {
    local roots=(
        "$HOME/.steam/steam" "$HOME/.steam/root" "$HOME/.local/share/Steam"
        "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam"
        "$HOME/.var/app/com.valvesoftware.Steam/data/Steam"
        "$HOME/snap/steam/common/.local/share/Steam"
    )
    local r p
    {
        for r in "${roots[@]}"; do
            [ -d "$r/steamapps" ] || continue
            readlink -f "$r"
            if [ -f "$r/steamapps/libraryfolders.vdf" ]; then
                grep -oE '"path"[[:space:]]+"[^"]+"' "$r/steamapps/libraryfolders.vdf" \
                    | sed -E 's/"path"[[:space:]]+"([^"]+)"/\1/' \
                    | while read -r p; do [ -d "$p/steamapps" ] && readlink -f "$p"; done
            fi
        done
    } | awk '!seen[$0]++'
}

mapfile -t LIBS < <(steam_libraries)

if [ -z "$GAME_DIR" ]; then
    for lib in "${LIBS[@]}"; do
        if [ -d "$lib/steamapps/common/Big Ambitions/Big Ambitions_Data/Managed" ]; then
            GAME_DIR="$lib/steamapps/common/Big Ambitions"; break
        fi
    done
fi
if [ -z "$GAME_DIR" ] || [ ! -d "$GAME_DIR/Big Ambitions_Data/Managed" ]; then
    echo "Big Ambitions nicht gefunden (gesucht in: ${LIBS[*]:-keine Steam-Bibliothek})." >&2
    echo "Mit --game-dir \"/pfad/zu/steamapps/common/Big Ambitions\" angeben." >&2
    exit 1
fi
GAME_DIR="${GAME_DIR%/}"
LIB_ROOT="$(readlink -f "$GAME_DIR/../../..")"
say "Spiel:   $GAME_DIR"

# The game's Proton prefix lives in the library that holds the game (Steam keeps compatdata next to it).
PREFIX=""
for lib in "$LIB_ROOT" "${LIBS[@]}"; do
    cand="$lib/steamapps/compatdata/$APP_ID/pfx"
    if [ -d "$cand/drive_c" ]; then PREFIX="$cand"; break; fi
done
if [ -z "$PREFIX" ]; then
    echo "Proton-Präfix für Big Ambitions fehlt (steamapps/compatdata/$APP_ID). Das Spiel einmal über Steam starten, dann erneut." >&2
    exit 1
fi
GAME_DATA="$PREFIX/drive_c/users/steamuser/AppData/LocalLow/Hovgaard Games/Big Ambitions"
MODS="$GAME_DATA/ModsLocal"
say "Präfix:  $PREFIX"
say "Mods:    $MODS"

# ── Old separate Telemetry Bridge: move it out of ModsLocal ────────────────────────────────────────────────
move_old_bridge() {
    [ -d "$MODS" ] || return 0
    local stamp backup d found=0
    stamp="$(date +%Y%m%d-%H%M%S)"
    backup="$GAME_DATA/ModsLocal-Backup"
    for d in "$MODS"/*/; do
        d="${d%/}"
        [ -d "$d" ] || continue
        [ "$(basename "$d")" = "GoingPublicServer" ] && continue
        # BigAmbitionsMP = the mod's folder before the rename: two copies would be refused as a duplicate install
        if [ "$(basename "$d")" = "BigAmbitionsMP" ] || [[ "$(basename "$d")" == *Telemetry*Bridge* ]] || [[ "$(basename "$d")" == *TelemetryBridge* ]] \
           || find "$d" -maxdepth 3 -iname '*Telemetry*Bridge*.dll' -print -quit | grep -q .; then
            found=1
            say "Alte Mod-Kopie (BigAmbitionsMP / Telemetry Bridge) gefunden: $d"
            run mkdir -p "$backup"
            run mv "$d" "$backup/$(basename "$d")-$stamp"
            [ "$DRY" = 1 ] || say "  -> verschoben nach $backup/$(basename "$d")-$stamp (zum Wiederherstellen zurück nach ModsLocal schieben)"
        fi
    done
    [ "$found" = 1 ] || say "Keine separate Telemetry Bridge in ModsLocal gefunden - gut."
}
move_old_bridge

# ── Build + install ────────────────────────────────────────────────────────────────────────────────────────
if [ "$BUILD" = 1 ]; then
    if ! command -v dotnet >/dev/null 2>&1; then
        echo ".NET SDK fehlt. Nobara/Fedora:  sudo dnf install dotnet-sdk-8.0" >&2
        exit 1
    fi
    DEPLOY="$MODS/GoingPublicServer/"
    say "Baue Going Public Server ($CONFIG) ..."
    # GameDir: where the game DLLs are. ModDeployDir: the csproj copies the finished mod there after the build.
    dotnet build "$HERE/BigAmbitionsMP.csproj" -c "$CONFIG" -nologo \
        "-p:GameDir=$GAME_DIR/" "-p:ModDeployDir=$DEPLOY"
    if [ ! -f "$DEPLOY/BigAmbitionsMP.dll" ]; then
        echo "Build lief durch, aber $DEPLOY/BigAmbitionsMP.dll fehlt. Ausgabe oben prüfen." >&2
        exit 1
    fi
    say "Installiert: $DEPLOY"
    ls -la "$DEPLOY" "$DEPLOY/Dependencies" 2>/dev/null | sed 's/^/   /'
    cat <<TXT

Fertig. Jetzt:
  1. Big Ambitions über Steam starten, im Mod-Menü "Going Public" aktiv lassen.
  2. Im Spiel-Log nach "Going Public Server" suchen:
     $GAME_DATA/Player.log
TXT
fi
