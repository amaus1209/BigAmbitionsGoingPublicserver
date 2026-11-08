#!/usr/bin/env bash
# AMP start wrapper for the Going Public server: makes sure the server has its key, prints it for AMP's status page,
# then REPLACES itself with the game (exec), so AMP's console and its "stop" command go straight to the game.
#
# AMP calls:  bamp-start.sh <path to proton> <proton arguments...>
# Settings (bamp-server.json, written by AMP's settings page):
#   TelemetryKey            ""           the server key players type into "Server key"; empty = created once
#                                        (telemetry-data/key.txt), shown on the status page
set -u
ROOT="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"
START="$(date +%s)"
GAME_CMD=("$@")
CONF=""
for ((i = 0; i < ${#GAME_CMD[@]}; i++)); do
    if [ "${GAME_CMD[$i]}" = "-bamp-config" ] && [ $((i + 1)) -lt ${#GAME_CMD[@]} ]; then
        CONF="${GAME_CMD[$((i + 1))]#Z:}"      # the game gets a Wine path (Z:/...); we need the Linux one
    fi
done

# SERVER-RULES-1 (1.0.12): AMP writes a setting only into a key that is already in bamp-server.json. A setting the
# template gained in an update (the module switches, the game rules) gets its default here; AMP then fills in the value
# set on its settings page at the next start.
settings_keys() {
    local manifest="$ROOT/bamp-tools/bigambitionsconfig.json"
    [ -n "$CONF" ] && [ -f "$CONF" ] && [ -f "$manifest" ] && [ -f "$ROOT/bamp-tools/seed-settings.py" ] && command -v python3 >/dev/null || return 0
    python3 "$ROOT/bamp-tools/seed-settings.py" "$CONF" "$manifest" || true   # SETTINGS-SEED-1
}
settings_keys

# SERVER-KEY-1 (1.0.11, replaces the telemetry collector): the key players need. The AMP setting wins; otherwise the
# one in telemetry-data/key.txt, created once (12 hex characters). A background loop repeats it with the mod version for
# AMP's status page every 30 s (AMP only pushes server info to pages open when the line arrives), watches the game's quit
# (quit_watch) and ends with the game command, which runs under this script's PID (exec at the end).
server_key() {
    local key="" kfile="$ROOT/telemetry-data/key.txt"
    mkdir -p "$ROOT/telemetry-data"
    rm -rf "${ROOT:?}/bamp-collector"   # left over from 1.0.10 and older (an Update unpacks over the old files)
    # the collector's settings of 1.0.10 and older are gone: drop them from the server file, so the mod does not warn
    if [ -n "$CONF" ] && [ -f "$CONF" ] && grep -q '"TelemetryCollector' "$CONF" && command -v python3 >/dev/null; then
        python3 - "$CONF" <<'PY' || true
import json, sys
p = sys.argv[1]
c = json.load(open(p, encoding="utf-8-sig"))
for k in ("TelemetryCollector", "TelemetryCollectorPort"):
    c.pop(k, None)
json.dump(c, open(p, "w", encoding="utf-8"), indent=2)
PY
    fi
    if [ -n "$CONF" ] && command -v python3 >/dev/null; then
        key="$(python3 -c 'import json,sys; c=json.load(open(sys.argv[1], encoding="utf-8-sig")); print("".join(ch for ch in str(c.get("TelemetryKey","") or "") if ch.isalnum() or ch in "-_")[:64])' "$CONF" 2>/dev/null || true)"
    fi
    if [ -z "$key" ]; then
        [ -s "$kfile" ] && key="$(tr -cd 'A-Za-z0-9_-' < "$kfile")"
        if [ -z "$key" ]; then
            key="$(od -An -N6 -tx1 /dev/urandom | tr -d ' \n')"
            printf '%s\n' "$key" > "$kfile" && chmod 600 "$kfile"
            echo "[Server] Neuer Server-Schlüssel erstellt."
        fi
    fi
    # INFO-30S-2 (user 2026-11-02: "build it in again, only the sending must stop when the server is ended - that kept
    # the server from restarting, it hung"; INFO-ONCE-1 had made it once at the start and on changes): the line goes out
    # every 30 s. AMP keeps the last line's values until the game ends, but a page opened later asks for them
    # (GenericModule.RequestServerInfo), and with Websockets on AMP 2.8's page asks before its socket is up and misses
    # the answer - the next line brings them back. It stops for good once the game stops (stopping/stopped) and while a
    # stop or restart is announced (AMP's "stop warn" with players on, "restart N"); the hang watch goes on.
    last_info="$(info_line)"
    printf '%s\n' "$last_info"
    (
        quit_at=0
        admin_gone=0
        quiet=0
        while true; do
            for _ in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15; do
                sleep 2
                kill -0 "$$" 2>/dev/null || exit 0   # the game command has ended
            done
            st="$(game_state)"
            quit_watch "$st"
            case "$st" in stopping|stopped) quiet=1 ;; esac
            [ "$quiet" = 1 ] && continue
            countdown_on && continue
            line="$(info_line)"
            # a file AMP is just rewriting (GenericModule.kvp on a settings change) reads as "no admin page" for a moment -
            # no change unless it stays so for a minute (user 2026-11-02: "Admin Seite" stood there empty)
            case "$last_info" in
                *" | Admin="*)
                    if [ "$line" = "${last_info%% | Admin=*}" ] && [ "$admin_gone" -lt 2 ]; then
                        admin_gone=$((admin_gone + 1)); line="$last_info"
                    else
                        admin_gone=0
                    fi ;;
            esac
            printf '%s\n' "$line"
            last_info="$line"
        done
    ) </dev/null &
}

# The game's state in this run: running, idle, stopping, stopped - "" before its first status (headless/status.json,
# rewritten every 5 s). The mod up to 1.0.11 wrote "stopping" only at a later tick that a hung quit never reached: its
# STOPPING line in events.log ("yyyy-MM-dd HH:mm:ssZ [BAMP-HEADLESS] STOPPING ...", UTC) counts as well.
game_state() {
    local f="$ROOT/headless/status.json" st="" line
    if [ -f "$f" ] && [ "$(stat -c %Y "$f" 2>/dev/null || echo 0)" -ge "$START" ]; then   # not the last run's file
        st="$(sed -n 's/.*"state": *"\([^"]*\)".*/\1/p' "$f" 2>/dev/null | head -n1)"
    fi
    if [ "$st" != stopping ] && [ "$st" != stopped ] && [ -f "$ROOT/headless/events.log" ]; then
        line="$(tail -n 50 "$ROOT/headless/events.log" 2>/dev/null | grep -a '\] STOPPING ' | tail -n1)"
        [ -n "$line" ] && [ "$(date -u -d "${line:0:19}" +%s 2>/dev/null || echo 0)" -ge "$START" ] && st=stopping
    fi
    echo "$st"
}

# INFO-30S-2: a stop or restart announced in this run (the game's status: "countdown").
countdown_on() {
    local f="$ROOT/headless/status.json"
    [ -f "$f" ] && [ "$(stat -c %Y "$f" 2>/dev/null || echo 0)" -ge "$START" ] \
        && grep -q '"countdown": *"\(stop\|restart\)"' "$f" 2>/dev/null
}

# The status page lines (AMP's Console.ServerInfoRegex makes one entry per named group): the game port, the server key
# and the running mod version (headless/status.json, written by the game every 5 s; the port from the settings before
# the first status). AMP-INFO-1 (user 2026-10-31): "Spiel Port", "Server Password" (= the server key), "Server Version".
info_line() {
    local ver port build
    ver="$(sed -n 's/.*"modVersion": *"\([^"]*\)".*/\1/p' "$ROOT/headless/status.json" 2>/dev/null | head -n1)"
    # BUILD-ID-1: the build next to the version, as the Workshop zip names it (1.0.16.2-fd6c3de9; no space: the regex takes \S+)
    build="$(sed -n 's/.*"modBuild": *"\([0-9a-f]*\)".*/\1/p' "$ROOT/headless/status.json" 2>/dev/null | head -n1)"
    [ -n "$ver" ] && [ -n "$build" ] && ver="$ver-$build"
    port="$(sed -n 's/.*"port": *\([0-9][0-9]*\).*/\1/p' "$ROOT/headless/status.json" 2>/dev/null | head -n1)"
    [ -n "$port" ] || port="$(sed -n 's/.*"HostPort": *\([0-9][0-9]*\).*/\1/p' "$CONF" 2>/dev/null | head -n1)"
    # ADMIN-LINK-1: the admin page (the ranking page's admin part) - under the Connection Link on AMP's status page. AMP's
    # address comes from bamp-web-sync.sh (amp.json, ADS's Network.BaseURL), else from the setting PlayerWebUrl.
    local base admin=""
    base="$(sed -n 's/.*"base": *"\(https\{0,1\}:\/\/[A-Za-z0-9.:-]*\)".*/\1/p' "$ROOT/telemetry-data/web/amp.json" 2>/dev/null | head -n1)"
    [ -n "$base" ] || base="$(sed -n 's/.*"PlayerWebUrl": *"\(https\{0,1\}:\/\/[A-Za-z0-9.:-]*\)\/*".*/\1/p' "$CONF" 2>/dev/null | head -n1)"
    # only where the instance's template reads it (an older Console.ServerInfoRegex would not match the longer line, and
    # the status page would lose key and version)
    if [ -n "$base" ] && [ -n "$port" ] && grep -q 'Admin_Seite' "$ROOT/../GenericModule.kvp" 2>/dev/null; then
        admin=" | Admin=$base/monitor/$port/index.html#admin"
    fi
    echo "[Server] Info: Port=${port:--} | Schluessel=$key | Version=${ver:--}$admin"
}

# SHUTDOWN-HANG-1 (1.0.12): on "stop" the game saves, writes "stopping" and at the end "stopped" into headless/status.json
# and then ends its own process. Should it still be there afterwards (the teardown hung under Proton, and AMP 2.8 waits
# 600 s before it kills), end it here: 30 s after "stopped", or 180 s after "stopping" (saving takes seconds).
quit_watch() {
    local st="${1-$(game_state)}" now d pids
    now="$(date +%s)"
    case "$st" in
        stopping) [ "$quit_at" = 0 ] && quit_at=$((now + 180)) ;;
        stopped) d=$((now + 30)); { [ "$quit_at" = 0 ] || [ "$quit_at" -gt "$d" ]; } && quit_at=$d ;;
        *) quit_at=0; return 0 ;;
    esac
    [ "$now" -ge "$quit_at" ] || return 0
    pids="$(game_pids)"
    [ -n "$pids" ] || return 0
    echo "[BAMP-Start] Das Spiel beendet sich nach dem Stopp nicht (Zustand '$st') - Prozess wird beendet."
    kill -TERM $pids 2>/dev/null
    sleep 5
    pids="$(game_pids)"
    [ -n "$pids" ] && kill -KILL $pids 2>/dev/null
    return 0
}

# The game's processes under Wine: their command line names the exe - not this script, xvfb-run or Proton's python.
game_pids() {
    local f p
    for f in /proc/[0-9]*/cmdline; do
        grep -qa "Big Ambitions.exe" "$f" 2>/dev/null || continue
        p="${f#/proc/}"
        p="${p%/cmdline}"
        case "$(cat "/proc/$p/comm" 2>/dev/null)" in ""|bash|sh|dash|xvfb-run|python*|sleep|grep|sed) continue ;; esac
        echo "$p"
    done
}
server_key
# The built mod in bamp-mod/ goes into the Proton prefix on every start too (not only in AMP's update stage), so a new
# build from install-into-amp.sh needs nothing but a restart.
install_mod() {
    local src="$ROOT/bamp-mod/GoingPublicServer"
    local dst="$ROOT/.proton/compatdata/pfx/drive_c/users/steamuser/AppData/LocalLow/Hovgaard Games/Big Ambitions/ModsLocal"
    [ -f "$src/BigAmbitionsMP.dll" ] && [ -d "$ROOT/.proton/compatdata/pfx" ] || return 0
    # the folder before the rename to GoingPublicServer: a second copy would be refused as a duplicate install
    [ -d "$dst/BigAmbitionsMP" ] && rm -rf "$dst/BigAmbitionsMP" && echo "[BAMP-Start] alter Mod-Ordner BigAmbitionsMP entfernt."
    if ! cmp -s "$src/BigAmbitionsMP.dll" "$dst/GoingPublicServer/BigAmbitionsMP.dll"; then
        mkdir -p "$dst" && rm -rf "$dst/GoingPublicServer" && cp -r "$src" "$dst/" && echo "[BAMP-Start] Going Public Server aus bamp-mod/ ins Proton-Präfix kopiert."
    fi
}
install_mod

# SERVER-MODS-1: extra mods. A mod in bamp-mods-extra/ (AMP's Steam Workshop store, or the file manager) goes into the
# prefix's ModsLocal; then the LIST of every ModsLocal folder but Going Public Server's own is published:
# telemetry-data/web/<key folder>/mods/mods.json. SEC-MODS-1: no mod files - a player refused for missing mods opens
# the Workshop pages from that list (a Workshop mod's folder is its item id) and subscribes in Steam. Nothing is
# published without a server key.
publish_mods() {
    local mods="$ROOT/.proton/compatdata/pfx/drive_c/users/steamuser/AppData/LocalLow/Hovgaard Games/Big Ambitions/ModsLocal"
    mkdir -p "$ROOT/bamp-mods-extra"
    if [ -d "$mods" ]; then
        for d in "$ROOT/bamp-mods-extra"/*/; do
            [ -d "$d" ] || continue
            local n; n="$(basename "$d")"
            [ "$n" = "GoingPublicServer" ] && continue
            # blueprints (Layout.json, no DLL) stay with the players - never installed on or handed out by the server
            if [ -f "$d/Layout.json" ] && ! ls "$d"/*.dll >/dev/null 2>&1; then echo "[BAMP-Start] '$n' ist ein Blueprint - bleibt beim Spieler, nicht installiert."; continue; fi
            if ! diff -rq "$d" "$mods/$n" >/dev/null 2>&1; then
                rm -rf "$mods/$n" && cp -rL "$d" "$mods/$n" && echo "[BAMP-Start] Mod '$n' aus bamp-mods-extra/ installiert."
            fi
        done
        # SERVER-MODS-2: AMP links a Workshop item into bamp-mods-extra/ but never removes the link when the item
        # leaves "Steam Workshop Items", so a removed mod stayed installed and players were still asked for it.
        # A Workshop item (folder name = item id) that is no longer in that list leaves bamp-mods-extra/ and
        # ModsLocal. Mods added by hand (not a number) are left alone; nothing is removed if the list is unreadable.
        local wanted
        if wanted="$(python3 -c '
import json, sys
for line in open(sys.argv[1], encoding="utf-8-sig"):
    if line.startswith("SteamWorkshop.WorkshopItemIDs="):
        print(" ".join(str(i).strip() for i in json.loads(line.split("=", 1)[1]))); break
else:
    sys.exit(1)' "$ROOT/../steamcmdplugin.kvp" 2>/dev/null)"; then
            for d in "$ROOT/bamp-mods-extra"/* "$mods"/*; do
                local n; n="$(basename "$d")"
                [[ "$n" =~ ^[0-9]+$ ]] || continue
                [[ " $wanted " == *" $n "* ]] && continue
                rm -rf "$d" && echo "[BAMP-Start] Workshop-Mod '$n' ist nicht mehr unter \"Steam Workshop Items\" - entfernt ($(basename "$(dirname "$d")")/)."
            done
        fi
    fi
    local key=""
    for _ in $(seq 1 20); do
        key="$(python3 -c 'import json,sys; print(str(json.load(open(sys.argv[1], encoding="utf-8-sig")).get("TelemetryKey","")).strip())' "$CONF" 2>/dev/null || true)"
        [ -z "$key" ] && [ -s "$ROOT/telemetry-data/key.txt" ] && key="$(tr -d '[:space:]' < "$ROOT/telemetry-data/key.txt")"
        [ -n "$key" ] && break
        sleep 0.5
    done
    [ -n "$key" ] || return 0
    # the folder is named after SHA-256(key), never the key itself (DedicatedHost.KeyFolder)
    local kdir; kdir="$ROOT/telemetry-data/web/$(printf '%s' "$key" | tr 'A-Z' 'a-z' | sha256sum | cut -c1-16)"
    mkdir -p "$kdir"
    [ -f "$kdir/ranking.json" ] || printf '{"updatedUnix": %s, "players": []}\n' "$(date +%s)" > "$kdir/ranking.json"
    [ -d "$mods" ] || return 0
    python3 - "$mods" "$kdir/mods" <<'PY'
import json, os, sys
mods, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
entries = []
for name in sorted(os.listdir(mods)):
    src = os.path.join(mods, name)
    if name == "GoingPublicServer" or not os.path.isdir(src):
        continue
    if os.path.isfile(os.path.join(src, "Layout.json")) and not any(f.lower().endswith(".dll") for f in os.listdir(src)):
        continue   # a blueprint: blueprints stay with the players
    entries.append({"folder": name, "workshop": name.isdigit()})
for f in os.listdir(out):   # mod zips of earlier versions: the server hands out no mod files any more
    if f != "mods.json":
        os.remove(os.path.join(out, f))
json.dump({"mods": entries}, open(os.path.join(out, "mods.json"), "w", encoding="utf-8"), indent=1)
ws = [e["folder"] for e in entries if e["workshop"]]
other = [e["folder"] for e in entries if not e["workshop"]]
print(f"[BAMP-Start] {len(entries)} Server-Mod(s) in der Liste für Spieler ({len(ws)} aus dem Workshop)" + (": " + ", ".join(ws + other) if entries else "."))
if other:
    print(f"[BAMP-Start] nicht im Workshop, Spieler brauchen sie von dir: {', '.join(other)}")
PY
}
publish_mods
# MOD-TRANSLATIONS-3: which of the server's mods lack which game languages (console lines "[ModTr] ...", templates for
# the missing ones in modtr-templates/ - AMP file manager). Reads only; changes nothing.
scan_translations() {
    local mods="$ROOT/.proton/compatdata/pfx/drive_c/users/steamuser/AppData/LocalLow/Hovgaard Games/Big Ambitions/ModsLocal"
    [ -f "$ROOT/bamp-tools/modtr-scan.py" ] && [ -d "$mods" ] && command -v python3 >/dev/null || return 0
    rm -rf "$ROOT/modtr-templates"
    local cache="$ROOT/.proton/compatdata/pfx/drive_c/users/steamuser/AppData/LocalLow/Hovgaard Games/Big Ambitions/GoingPublicServer/ModTranslationsCache"
    python3 "$ROOT/bamp-tools/modtr-scan.py" "$mods" "$ROOT/bamp-mod/GoingPublicServer/ModTranslations:$cache" "$ROOT/modtr-templates" || true
}
scan_translations
# CHAT-TRANSLATE-1 (user 2026-11-08: a live translation of the chat, German <-> English): the translator runs next to the
# game, on this server only - no account, no service on the internet, the chat never leaves the server. The first start
# sets it up in bamp-translate/ (a Python venv with CTranslate2 + SentencePiece and two Argos Translate models, ~550 MB;
# outside the game folder 1331550, so not in AMP's backups). All in the background: the game starts at once, and the mod
# switches the translation on as soon as headless/translate.json says where the translator listens (127.0.0.1 only).
# It ends with the game (its PID is this script's, exec below). AMP switch ChatTranslate, on by default.
chat_translate() {
    local state="$ROOT/headless/translate.json" dir="$ROOT/bamp-translate" tool="$ROOT/bamp-tools/bamp-translate.py" on="true"
    rm -f "$state"
    [ -f "$tool" ] || return 0
    if ! command -v python3 >/dev/null; then echo "[BAMP-Start] Chat-Übersetzung: python3 fehlt auf diesem Server - bleibt aus."; return 0; fi
    if [ -n "$CONF" ] && [ -f "$CONF" ]; then
        on="$(python3 -c 'import json,sys; print(str(json.load(open(sys.argv[1], encoding="utf-8-sig")).get("ChatTranslate", "true")).strip().lower())' "$CONF" 2>/dev/null || echo true)"
    fi
    [ "$on" = "false" ] && return 0
    mkdir -p "$ROOT/headless"
    local parent=$$
    (
        exec 9>"$dir.lock"
        flock -w 3600 9 || exit 0             # a setup of the run before is still downloading
        nice -n 10 python3 -I "$tool" setup "$dir" || exit 0
        exec 9>&-
        exec nice -n 10 "$dir/venv/bin/python" -I "$tool" serve "$dir" "$state" "$parent"
    ) </dev/null &
}
chat_translate
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
