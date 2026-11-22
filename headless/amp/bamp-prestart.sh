#!/usr/bin/env bash
# PRESTART-1 (user 2026-11-11: the AMP template goes to CubeCoders' AMPTemplates, whose rules allow scripts in update and
# pre-start stages only, not as the program AMP starts): everything that has to happen before each start of the game.
# The template runs one step per pre-start stage, then starts the game itself (xvfb-run + Proton). bamp-start.sh
# (instances still on an older template) runs "all" and then starts the game as before. The status page line now comes
# from the mod (AMP-INFO-2).
#
#   bamp-prestart.sh <step> <root folder> [<bamp-server.json>]
#     leftovers     end a game of the last run that is still there (it would keep the port and the save)
#     settings      settings the template gained get their defaults in bamp-server.json (SERVER-RULES-1)
#     key           the key players type into "Server key" (SERVER-KEY-1)
#     mod           the built mod from bamp-mod/ into the Proton prefix
#     mods          extra mods from bamp-mods-extra/ into the prefix, and their list for players (SERVER-MODS-1/2)
#     translations  which mods lack which game languages (MOD-TRANSLATIONS-3)
#     translate     the chat translator, in the background (CHAT-TRANSLATE-1)
#     all           all of them, in this order
#     steam <name>  update stage: the game through SteamCMD with its remembered login (STEAM-LOGIN-1), not part of "all"
set -u
STEP="${1:-all}"
ROOT="${2:-}"
ROOT="${ROOT%/}"
CONF="${3:-}"
if [ -z "$ROOT" ] || [ ! -d "$ROOT" ]; then
    echo "[BAMP-Start] bamp-prestart.sh: no server folder given - nothing to do."
    exit 0
fi
[ -n "$CONF" ] || CONF="$ROOT/1331550/bamp-server.json"
# LANG-1 (PR #2053 review 2026-11-22): the messages follow the setting "Server language" - German for "de", English for
# every other language. BAMP_LANG tells the Python helpers (seed-settings.py, modtr-scan.py, the mod list below).
BAMP_LANG=en
[ -f "$CONF" ] && grep -Eq '"ServerLanguage"[[:space:]]*:[[:space:]]*"[Dd][Ee]"' "$CONF" 2>/dev/null && BAMP_LANG=de
export BAMP_LANG
# say "<English>" "<Deutsch>"
say() { if [ "$BAMP_LANG" = de ]; then printf '%s\n' "$2"; else printf '%s\n' "$1"; fi; }
TOOLS="$ROOT/bamp-tools"
PREFIX_MODS="$ROOT/.proton/compatdata/pfx/drive_c/users/steamuser/AppData/LocalLow/Hovgaard Games/Big Ambitions/ModsLocal"

has_python() { command -v python3 >/dev/null; }

# The key in the AMP setting TelemetryKey, as players may type it: letters, digits, '-' and '_', at most 64.
conf_key() {
    [ -f "$CONF" ] && has_python || return 0
    python3 -c 'import json,sys; c=json.load(open(sys.argv[1], encoding="utf-8-sig")); print("".join(ch for ch in str(c.get("TelemetryKey","") or "") if ch.isalnum() or ch in "-_")[:64])' "$CONF" 2>/dev/null || true
}

# A game of the last run that is still there: its teardown hung under Proton, and AMP's stop ended only the program it
# had started (bamp-start.sh used to end such a game, SHUTDOWN-HANG-1). It would keep the port and the save, so it ends
# now. Only this instance's game counts - its folder is in the command line, as a Linux or as a Wine path - so other
# instances on the same machine are left alone; shells, xvfb-run and Proton's python are not the game.
leftovers() {
    local win="Z:${ROOT//\//\\}\\" pids="" f p
    for f in /proc/[0-9]*/cmdline; do
        grep -qa "Big Ambitions.exe" "$f" 2>/dev/null || continue
        grep -qaF -e "$ROOT/" -e "$win" "$f" 2>/dev/null || continue
        p="${f#/proc/}"
        p="${p%/cmdline}"
        [ "$p" = "$$" ] && continue
        case "$(cat "/proc/$p/comm" 2>/dev/null)" in ""|bash|sh|dash|xvfb-run|Xvfb|python*|sleep|grep|sed) continue ;; esac
        pids="$pids $p"
    done
    [ -n "$pids" ] || return 0
    say "[BAMP-Start] A game from the last run is still running (process$pids) - ending it." \
        "[BAMP-Start] Ein Spiel vom letzten Lauf läuft noch (Prozess$pids) - es wird beendet."
    kill -TERM $pids 2>/dev/null
    sleep 5
    for p in $pids; do kill -0 "$p" 2>/dev/null && kill -KILL "$p" 2>/dev/null; done
    return 0
}

# SERVER-RULES-1 (1.0.12): AMP writes a setting only into a key that is already in bamp-server.json. A setting the template
# gained in an update gets its default here; AMP fills in the value from its settings page at the next start.
settings() {
    local manifest="$TOOLS/bigambitionsconfig.json"
    [ -f "$CONF" ] && [ -f "$manifest" ] && [ -f "$TOOLS/seed-settings.py" ] && has_python || return 0
    python3 "$TOOLS/seed-settings.py" "$CONF" "$manifest" || true   # SETTINGS-SEED-1
}

# SERVER-KEY-1 (1.0.11): the key players need. The AMP setting wins; otherwise the one in telemetry-data/key.txt, created
# once (12 hex characters). The mod reads the same (DedicatedHost.ServerJoinKey) and shows it on AMP's status page.
key() {
    local key="" kfile="$ROOT/telemetry-data/key.txt"
    mkdir -p "$ROOT/telemetry-data"
    rm -rf "${ROOT:?}/bamp-collector"   # left over from 1.0.10 and older (an update unpacks over the old files)
    # the collector's settings of 1.0.10 and older are gone: drop them from the server file, so the mod does not warn
    if [ -f "$CONF" ] && grep -q '"TelemetryCollector' "$CONF" && has_python; then
        python3 - "$CONF" <<'PY' || true
import json, sys
p = sys.argv[1]
c = json.load(open(p, encoding="utf-8-sig"))
for k in ("TelemetryCollector", "TelemetryCollectorPort"):
    c.pop(k, None)
json.dump(c, open(p, "w", encoding="utf-8"), indent=2)
PY
    fi
    [ -n "$(conf_key)" ] && return 0
    [ -s "$kfile" ] && key="$(tr -cd 'A-Za-z0-9_-' < "$kfile")"
    if [ -z "$key" ]; then
        key="$(od -An -N6 -tx1 /dev/urandom | tr -d ' \n')"
        printf '%s\n' "$key" > "$kfile" && chmod 600 "$kfile"
        say "[Server] New server key created." "[Server] Neuer Server-Schlüssel erstellt."
    fi
    return 0
}

# The built mod in bamp-mod/ goes into the Proton prefix before every start (not only in AMP's update stage), so a new
# build from install-into-amp.sh needs nothing but a restart.
mod() {
    local src="$ROOT/bamp-mod/GoingPublicServer"
    [ -f "$src/BigAmbitionsMP.dll" ] && [ -d "$ROOT/.proton/compatdata/pfx" ] || return 0
    # the folder before the rename to GoingPublicServer: a second copy would be refused as a duplicate install
    [ -d "$PREFIX_MODS/BigAmbitionsMP" ] && rm -rf "$PREFIX_MODS/BigAmbitionsMP" && say "[BAMP-Start] Old mod folder BigAmbitionsMP removed." "[BAMP-Start] alter Mod-Ordner BigAmbitionsMP entfernt."
    if ! cmp -s "$src/BigAmbitionsMP.dll" "$PREFIX_MODS/GoingPublicServer/BigAmbitionsMP.dll"; then
        mkdir -p "$PREFIX_MODS" && rm -rf "$PREFIX_MODS/GoingPublicServer" && cp -r "$src" "$PREFIX_MODS/" && say "[BAMP-Start] Going Public Server copied from bamp-mod/ into the Proton prefix." "[BAMP-Start] Going Public Server aus bamp-mod/ ins Proton-Präfix kopiert."
    fi
    return 0
}

# SERVER-MODS-1: extra mods. A mod in bamp-mods-extra/ (AMP's Steam Workshop store, or the file manager) goes into the
# prefix's ModsLocal; then the LIST of every ModsLocal folder but Going Public Server's own is published:
# telemetry-data/web/<key folder>/mods/mods.json. SEC-MODS-1: no mod files - a player refused for missing mods opens the
# Workshop pages from that list (a Workshop mod's folder is its item id) and subscribes in Steam. Nothing is published
# without a server key.
mods() {
    mkdir -p "$ROOT/bamp-mods-extra"
    if [ -d "$PREFIX_MODS" ]; then
        local d n
        for d in "$ROOT/bamp-mods-extra"/*/; do
            [ -d "$d" ] || continue
            n="$(basename "$d")"
            [ "$n" = "GoingPublicServer" ] && continue
            # blueprints (Layout.json, no DLL) stay with the players - never installed on or handed out by the server
            if [ -f "$d/Layout.json" ] && ! ls "$d"/*.dll >/dev/null 2>&1; then say "[BAMP-Start] '$n' is a blueprint - it stays with the players, not installed." "[BAMP-Start] '$n' ist ein Blueprint - bleibt beim Spieler, nicht installiert."; continue; fi
            if ! diff -rq "$d" "$PREFIX_MODS/$n" >/dev/null 2>&1; then
                rm -rf "${PREFIX_MODS:?}/$n" && cp -rL "$d" "$PREFIX_MODS/$n" && say "[BAMP-Start] Mod '$n' installed from bamp-mods-extra/." "[BAMP-Start] Mod '$n' aus bamp-mods-extra/ installiert."
            fi
        done
        # SERVER-MODS-2: AMP links a Workshop item into bamp-mods-extra/ but never removes the link when the item leaves
        # "Steam Workshop Items", so a removed mod stayed installed and players were still asked for it. A Workshop item
        # (folder name = item id) that is no longer in that list leaves bamp-mods-extra/ and ModsLocal. Mods added by hand
        # (not a number) are left alone; nothing is removed if the list is unreadable.
        local wanted
        if wanted="$(python3 -c '
import json, sys
for line in open(sys.argv[1], encoding="utf-8-sig"):
    if line.startswith("SteamWorkshop.WorkshopItemIDs="):
        print(" ".join(str(i).strip() for i in json.loads(line.split("=", 1)[1]))); break
else:
    sys.exit(1)' "$ROOT/../steamcmdplugin.kvp" 2>/dev/null)"; then
            for d in "$ROOT/bamp-mods-extra"/* "$PREFIX_MODS"/*; do
                n="$(basename "$d")"
                [[ "$n" =~ ^[0-9]+$ ]] || continue
                [[ " $wanted " == *" $n "* ]] && continue
                rm -rf "$d" && say "[BAMP-Start] Workshop mod '$n' is no longer under \"Steam Workshop Items\" - removed ($(basename "$(dirname "$d")")/)." \
                    "[BAMP-Start] Workshop-Mod '$n' ist nicht mehr unter \"Steam Workshop Items\" - entfernt ($(basename "$(dirname "$d")")/)."
            done
        fi
    fi
    local key
    key="$(conf_key)"
    [ -z "$key" ] && [ -s "$ROOT/telemetry-data/key.txt" ] && key="$(tr -cd 'A-Za-z0-9_-' < "$ROOT/telemetry-data/key.txt")"
    [ -n "$key" ] || return 0
    # the folder is named after SHA-256(key), never the key itself (DedicatedHost.KeyFolder)
    local kdir
    kdir="$ROOT/telemetry-data/web/$(printf '%s' "$key" | tr 'A-Z' 'a-z' | sha256sum | cut -c1-16)"
    mkdir -p "$kdir"
    [ -f "$kdir/ranking.json" ] || printf '{"updatedUnix": %s, "players": []}\n' "$(date +%s)" > "$kdir/ranking.json"
    [ -d "$PREFIX_MODS" ] && has_python || return 0
    python3 - "$PREFIX_MODS" "$kdir/mods" <<'PY'
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
names = (": " + ", ".join(ws + other) if entries else ".")
if os.environ.get("BAMP_LANG") == "de":
    print(f"[BAMP-Start] {len(entries)} Server-Mod(s) in der Liste für Spieler ({len(ws)} aus dem Workshop)" + names)
    if other:
        print(f"[BAMP-Start] nicht im Workshop, Spieler brauchen sie von dir: {', '.join(other)}")
else:
    print(f"[BAMP-Start] {len(entries)} server mod(s) in the list for players ({len(ws)} from the Workshop)" + names)
    if other:
        print(f"[BAMP-Start] not in the Workshop, players need them from you: {', '.join(other)}")
PY
    return 0
}

# MOD-TRANSLATIONS-3: which of the server's mods lack which game languages (lines "[ModTr] ...", templates for the
# missing ones in modtr-templates/ - AMP file manager). Reads only; changes nothing.
translations() {
    local cache="$ROOT/.proton/compatdata/pfx/drive_c/users/steamuser/AppData/LocalLow/Hovgaard Games/Big Ambitions/GoingPublicServer/ModTranslationsCache"
    [ -f "$TOOLS/modtr-scan.py" ] && [ -d "$PREFIX_MODS" ] && has_python || return 0
    rm -rf "$ROOT/modtr-templates"
    python3 "$TOOLS/modtr-scan.py" "$PREFIX_MODS" "$ROOT/bamp-mod/GoingPublicServer/ModTranslations:$cache" "$ROOT/modtr-templates" || true
    return 0
}

# CHAT-TRANSLATE-1 (user 2026-11-08: a live translation of the chat, German <-> English): the translator runs next to the
# game, on this server only - no account, no service on the internet, the chat never leaves the server. The first start
# sets it up in bamp-translate/ (a Python venv with CTranslate2 + SentencePiece and two Argos Translate models, ~550 MB;
# outside the game folder 1331550, so not in AMP's backups). It runs detached: the stage ends at once and the game starts,
# the mod switches the translation on as soon as headless/translate.json says where the translator listens (127.0.0.1
# only), and the translator ends with the game (it watches headless/status.json). Its lines go to headless/translate.log.
# AMP switch ChatTranslate, on by default.
translate() {
    local state="$ROOT/headless/translate.json" dir="$ROOT/bamp-translate" tool="$TOOLS/bamp-translate.py" log="$ROOT/headless/translate.log" on="true" old=""
    mkdir -p "$ROOT/headless"
    # a translator of an earlier run ends with its game by itself; one still there would answer this run and remove
    # this run's translate.json when it ends - so it ends now
    if [ -f "$state" ] && has_python; then
        old="$(python3 -c 'import json,sys; print(int(json.load(open(sys.argv[1])).get("pid", 0)))' "$state" 2>/dev/null || true)"
        if [ -n "$old" ] && [ "$old" != 0 ] && grep -qa "bamp-translate.py" "/proc/$old/cmdline" 2>/dev/null; then
            kill -TERM "$old" 2>/dev/null
            sleep 1
        fi
    fi
    rm -f "$state"
    [ -f "$tool" ] || return 0
    if ! has_python; then say "[BAMP-Start] Chat translation: python3 is missing on this server - it stays off." "[BAMP-Start] Chat-Übersetzung: python3 fehlt auf diesem Server - bleibt aus."; return 0; fi
    if [ -f "$CONF" ]; then
        on="$(python3 -c 'import json,sys; print(str(json.load(open(sys.argv[1], encoding="utf-8-sig")).get("ChatTranslate", "true")).strip().lower())' "$CONF" 2>/dev/null || echo true)"
    fi
    [ "$on" = "false" ] && return 0
    [ -f "$log" ] && [ "$(stat -c %s "$log" 2>/dev/null || echo 0)" -gt 1000000 ] && mv -f "$log" "$log.1"
    local detach=()
    command -v setsid >/dev/null && detach=(setsid)
    "${detach[@]}" bash -c '
        exec 9>"$1.lock"
        flock -w 3600 9 || exit 0             # a setup of the run before is still downloading
        nice -n 10 python3 -I "$2" setup "$1" || exit 0
        exec 9>&-
        exec nice -n 10 "$1/venv/bin/python" -I "$2" serve "$1" "$3" 0
    ' bamp-translate "$dir" "$tool" "$state" </dev/null >>"$log" 2>&1 &
    say "[BAMP-Start] Chat translation starts in the background (messages in headless/translate.log)." \
        "[BAMP-Start] Chat-Übersetzung startet im Hintergrund (Meldungen in headless/translate.log)."
    return 0
}

# STEAM-LOGIN-1 (user 2026-11-13: "möchte die zugangsdaten in der instanz unter einstellungen bigambitions eintragen", then
# "Nur Kontoname"): the update stage "SteamCMD Download (remembered login)" when the AMP setting SteamUpdateLogin is
# "remembered" - the game's files through SteamCMD with the login it remembered on this server after one login through
# AMP's window (Steam Guard is asked only then). Only the account name (4th argument, AMP setting SteamUpdateUser) - no
# password exists anywhere; SteamCMD's token lies in the instance's .virtualhome/Steam (where AMP's own SteamCMD stage
# keeps it). +@NoPromptForPassword and a closed stdin: a login SteamCMD no longer remembers fails at once with a message
# instead of waiting for a password at night. The account name never reaches AMP's console or log (shown as <Konto>).
steam() {
    local user="${1:-}" inst exe out ok=0 why=""
    user="$(printf '%s' "$user" | tr -d '[:space:]')"
    if [ -z "$user" ]; then
        say "[BAMP-Start] Steam login 'Remembered login' is chosen, but no Steam account name is set (Settings > Big Ambitions > Steam updates)." \
            "[BAMP-Start] Steam-Anmeldung 'Gemerkte Anmeldung' ist gewählt, aber kein Steam-Kontoname eingetragen (Einstellungen > Big Ambitions > Steam-Updates)."
        exit 1
    fi
    if ! [[ "$user" =~ ^[A-Za-z0-9_.@-]{1,64}$ ]]; then
        say "[BAMP-Start] The Steam account name has characters that do not work here (allowed: letters, digits, _ . @ -)." \
            "[BAMP-Start] Der Steam-Kontoname enthält Zeichen, die hier nicht gehen (erlaubt: Buchstaben, Ziffern, _ . @ -)."
        exit 1
    fi
    exe="$ROOT/steamcmd.sh"
    if [ ! -x "$exe" ] && [ ! -f "$exe" ]; then
        say "[BAMP-Start] SteamCMD is missing in this instance - update once with 'Steam login for game updates: Login window'." \
            "[BAMP-Start] SteamCMD fehlt in dieser Instanz - einmal mit 'Steam-Anmeldung: Login-Fenster' aktualisieren."
        exit 1
    fi
    inst="$(dirname "$ROOT")"
    [ -d "$inst/.virtualhome" ] && export HOME="$inst/.virtualhome"   # where AMP's SteamCMD keeps the remembered login
    out="$(mktemp)"
    say "[BAMP-Start] Game update through SteamCMD with the remembered login ..." "[BAMP-Start] Spiel-Update über SteamCMD mit der gemerkten Anmeldung ..."
    local acc="<account>"
    [ "$BAMP_LANG" = de ] && acc="<Konto>"
    timeout 7200 bash "$exe" +@ShutdownOnFailedCommand 1 +@NoPromptForPassword 1 +@sSteamCmdForcePlatformType windows \
        +force_install_dir "$ROOT/1331550" +login "$user" +app_update 1331550 +quit </dev/null 2>&1 \
        | while IFS= read -r line; do
              printf '%s\n' "$line" >> "$out"
              [ -n "${line// /}" ] && printf '[SteamCMD] %s\n' "${line//$user/$acc}"
          done
    grep -qE "Success! App '1331550' (fully installed|already up to date)" "$out" && ok=1
    why="$(grep -aiE "Cached credentials not found|password|Login Failure|Invalid Password|Steam Guard|two-factor|Rate Limit|Expired|FAILED" "$out" | tail -n 1)"
    rm -f "$out"
    if [ "$ok" = 1 ]; then say "[BAMP-Start] Game update with the remembered Steam login done." "[BAMP-Start] Spiel-Update mit der gemerkten Steam-Anmeldung fertig."; return 0; fi
    why="${why//$user/$acc}"
    say "[BAMP-Start] Game update with the remembered Steam login failed${why:+ (${why})}. Set the Steam login to 'Login window' once, press Update and log in, then back to 'Remembered login'." \
        "[BAMP-Start] Spiel-Update mit der gemerkten Steam-Anmeldung ging nicht${why:+ (${why})}. Steam-Anmeldung einmal auf 'Login-Fenster' stellen, Update drücken und anmelden, danach wieder 'Gemerkte Anmeldung'."
    exit 1
}

case "$STEP" in
    steam) steam "${4:-}" ;;
    leftovers|settings|key|mod|mods|translations|translate) "$STEP" ;;
    all) leftovers; settings; key; mod; mods; translations; translate ;;
    *) say "[BAMP-Start] bamp-prestart.sh: unknown step '$STEP'." "[BAMP-Start] bamp-prestart.sh: unbekannter Schritt '$STEP'."; exit 0 ;;
esac
exit 0
