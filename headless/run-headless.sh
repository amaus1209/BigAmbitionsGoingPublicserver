#!/usr/bin/env bash
# Going Public server mode - start Big Ambitions headless on Linux without AMP (manual test, systemd, screen/tmux).
# UNTESTED DRAFT. Needs: the Windows game files (steamcmd +@sSteamCmdForcePlatformType windows +app_update 1331550),
# Proton (GE) or Wine, and the built mod in the prefix's ModsLocal folder (see HEADLESS.md).
#
#   GAME_DIR=/srv/ba/game PROTON=/srv/ba/proton/proton ./run-headless.sh            # Proton
#   GAME_DIR=/srv/ba/game WINE=wine64 WINEPREFIX=/srv/ba/pfx ./run-headless.sh      # plain Wine
#
# Console: type commands (status, players, save, say <text>, ban <name>, stop). Ctrl+C = save and stop.
# Exit codes of the game: 0 stop, 10 restart wanted, 2/3/4/5 fatal (see HEADLESS.md). Anything but 0 restarts
# after a pause, at most MAX_RESTARTS times in a row.
set -u

GAME_DIR="${GAME_DIR:?set GAME_DIR to the folder with 'Big Ambitions.exe'}"
BASE="${BASE:-$(cd "$(dirname "$0")" && pwd)}"
HEADLESS_DIR="${HEADLESS_DIR:-$BASE/headless-data}"
SERVER_JSON="${SERVER_JSON:-$GAME_DIR/bamp-server.json}"
MAX_RESTARTS="${MAX_RESTARTS:-5}"
EXTRA_ARGS="${EXTRA_ARGS:-}"          # e.g. "-logFile -" to see the full game log in this console

mkdir -p "$HEADLESS_DIR"
if [ ! -f "$SERVER_JSON" ]; then
  cp "$BASE/bamp-server.example.json" "$SERVER_JSON" 2>/dev/null \
    || printf '%s\n' '{ "Headless": true, "DedicatedHost": true, "DedicatedSession": "", "HostPort": 7777 }' > "$SERVER_JSON"
  echo "Created $SERVER_JSON - set DedicatedSession to your multiplayer save, then start again."
  exit 1
fi

winpath() { printf 'Z:%s' "$1"; }    # Wine/Proton map Z: to /
ARGS=(-batchmode -nographics -bamp-config "$(winpath "$SERVER_JSON")" -bamp-headless-dir "$(winpath "$HEADLESS_DIR")")
# shellcheck disable=SC2206
ARGS+=($EXTRA_ARGS)

export SteamAppId=1331550 SteamGameId=1331550
if [ -n "${PROTON:-}" ]; then
  export STEAM_COMPAT_DATA_PATH="${STEAM_COMPAT_DATA_PATH:-$BASE/compatdata}"
  export STEAM_COMPAT_CLIENT_INSTALL_PATH="${STEAM_COMPAT_CLIENT_INSTALL_PATH:-$HOME/.steam/steam}"
  mkdir -p "$STEAM_COMPAT_DATA_PATH"
  RUN=("$PROTON" runinprefix "$GAME_DIR/Big Ambitions.exe")
else
  export WINEDEBUG="${WINEDEBUG:--all}"
  RUN=("${WINE:-wine64}" "$GAME_DIR/Big Ambitions.exe")
fi

exec 3<&0                            # keep the console's stdin for the command forwarder below
child=0
stop_game() {                        # SIGTERM/SIGINT from systemd or Ctrl+C: ask the game to save and quit
  echo "stop" >> "$HEADLESS_DIR/command.txt"
  STOP_REQUESTED=1
}
STOP_REQUESTED=0
trap stop_game TERM INT

restarts=0
while :; do
  echo "[run-headless] starting: ${RUN[*]} ${ARGS[*]}"
  # commands go through the command file only (stdin is not handed to the game, so nothing runs twice)
  "${RUN[@]}" "${ARGS[@]}" </dev/null &
  child=$!
  ( while IFS= read -r line; do printf '%s\n' "$line" >> "$HEADLESS_DIR/command.txt"; done ) <&3 &
  fwd=$!
  wait "$child"; code=$?
  # a trapped signal interrupts 'wait' - wait again for the real exit
  while kill -0 "$child" 2>/dev/null; do wait "$child"; code=$?; done
  kill "$fwd" 2>/dev/null
  echo "[run-headless] game exited with code $code"
  [ "$STOP_REQUESTED" = 1 ] && exit 0
  [ "$code" = 0 ] && exit 0
  if [ "$code" != 10 ]; then
    restarts=$((restarts + 1))
    if [ "$restarts" -gt "$MAX_RESTARTS" ]; then echo "[run-headless] $MAX_RESTARTS restarts in a row - giving up."; exit "$code"; fi
    sleep $((15 * restarts))
  fi
done
