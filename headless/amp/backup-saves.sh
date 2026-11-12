#!/usr/bin/env bash
# Daily backup of the dedicated servers' multiplayer saves (all worlds, all players' slots).
#
#   backup-saves.sh [<instance App.RootDir>] [backup dir] [keep]
#   without a RootDir (or with "--all"): every AMP instance that has Going Public multiplayer saves
#   (~/.ampdata/instances/*/*/.proton/.../_BAMP_MP), so new and deleted instances need no change here.
#
# Running servers are asked to save first (headless/command.txt, the same channel as the console) and given a minute.
# Archives: <backup dir>/saves-<instance>-YYYYmmdd-HHMM.tar.gz; per instance only the newest <keep> (default 14) stay.
# Cron (as the AMP user):  17 4 * * *  /path/to/headless/amp/backup-saves.sh >> ~/backups/saves/backup.log 2>&1
set -uo pipefail
SAVEDIR=".proton/compatdata/pfx/drive_c/users/steamuser/AppData/LocalLow/Hovgaard Games/Big Ambitions/SaveGames/_BAMP_MP"
ARG="${1:---all}"
OUT="${2:-$HOME/backups/saves}"
KEEP="${3:-14}"
mkdir -p "$OUT"

ROOTS=()
if [ "$ARG" = "--all" ]; then
    for s in "$HOME"/.ampdata/instances/*/*/"$SAVEDIR"; do
        [ -d "$s" ] && ROOTS+=("${s%/"$SAVEDIR"}")
    done
else
    ROOTS+=("${ARG%/}")
fi
[ ${#ROOTS[@]} -gt 0 ] || { echo "$(date '+%F %T') keine Instanz mit Mehrspieler-Spielständen gefunden"; exit 1; }

# running servers (status.json written in the last 2 minutes) save first
asked=0
for ROOT in "${ROOTS[@]}"; do
    STATUS="$ROOT/headless/status.json"
    if [ -f "$STATUS" ] && [ $(( $(date +%s) - $(stat -c %Y "$STATUS") )) -lt 120 ]; then
        echo "save" >> "$ROOT/headless/command.txt"
        asked=1
    fi
done
[ "$asked" = 1 ] && sleep 60

rc=0
for ROOT in "${ROOTS[@]}"; do
    SAVES="$ROOT/$SAVEDIR"
    if [ ! -d "$SAVES" ]; then echo "$(date '+%F %T') keine Mehrspieler-Spielstände unter $SAVES"; rc=1; continue; fi
    NAME="$(basename "$(dirname "$ROOT")")"
    ARCHIVE="$OUT/saves-$NAME-$(date +%Y%m%d-%H%M).tar.gz"
    tar -czf "$ARCHIVE" -C "$(dirname "$SAVES")" "$(basename "$SAVES")" -C "$ROOT/1331550" bamp-server.json 2>/dev/null \
        || tar -czf "$ARCHIVE" -C "$(dirname "$SAVES")" "$(basename "$SAVES")" || { rc=1; continue; }
    echo "$(date '+%F %T') Backup $NAME: $ARCHIVE ($(du -h "$ARCHIVE" | cut -f1))"
    ls -1t "$OUT"/saves-"$NAME"-*.tar.gz 2>/dev/null | tail -n +"$((KEEP + 1))" | xargs -r rm -f
done
exit $rc
