#!/usr/bin/env bash
# AMP start wrapper of older templates (App.ExecutableLinux=bamp-start.sh). The template for CubeCoders' AMPTemplates
# starts the game itself (xvfb-run + Proton) and runs bamp-tools/bamp-prestart.sh in its pre-start stages (PRESTART-1,
# user 2026-11-11). This wrapper does the same for instances still on an older template, then REPLACES itself with the
# game (exec), so AMP's console and its "stop" command go straight to the game. The status page line ("[Server] Info:
# Port=... | Schluessel=... | Version=...") comes from the mod now (AMP-INFO-2); a game of the last run that hangs is
# ended by the next start (bamp-prestart.sh leftovers) instead of a watch here (SHUTDOWN-HANG-1).
#
# AMP calls:  bamp-start.sh <path to proton> <proton arguments...>
set -u
ROOT="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"
GAME_CMD=("$@")
CONF=""
for ((i = 0; i < ${#GAME_CMD[@]}; i++)); do
    if [ "${GAME_CMD[$i]}" = "-bamp-config" ] && [ $((i + 1)) -lt ${#GAME_CMD[@]} ]; then
        CONF="${GAME_CMD[$((i + 1))]#Z:}"      # the game gets a Wine path (Z:/...); we need the Linux one
    fi
done

if [ -f "$ROOT/bamp-tools/bamp-prestart.sh" ]; then
    bash "$ROOT/bamp-tools/bamp-prestart.sh" all "$ROOT" "$CONF"
else
    echo "[BAMP-Start] bamp-tools/bamp-prestart.sh fehlt - bitte in AMP \"Update\" ausführen."
fi

# Server-only preloader (installed by install-into-amp.sh): the game loads mods only after Steam has started, and there
# is no Steam client here. Doorstop's winhttp.dll must win over Wine's builtin one to load it.
if [ -f "$ROOT/1331550/winhttp.dll" ] && [ -f "$ROOT/1331550/doorstop_config.ini" ]; then
    export WINEDLLOVERRIDES="winhttp=n,b${WINEDLLOVERRIDES:+;$WINEDLLOVERRIDES}"
fi
# Unity creates a (hidden) window even with -batchmode -nographics; under Proton without an X display that fails
# ("Failed to create batch mode window") and the game exits. AMP containers have no display, so give it a virtual one.
if [ -z "${DISPLAY:-}" ] && command -v xvfb-run >/dev/null; then
    exec xvfb-run -a "${GAME_CMD[@]}"
fi
exec "${GAME_CMD[@]}"
