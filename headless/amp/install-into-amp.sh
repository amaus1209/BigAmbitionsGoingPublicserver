#!/usr/bin/env bash
# Copy the server-side parts of this repo into an AMP "Big Ambitions (Going Public server)" instance:
#   - bamp-start.sh            start wrapper (server key + game)
#   - bamp-mod/GoingPublicServer  the built mod, if a build is found (AMP's update stage installs it into the prefix)
#   - 1331550/winhttp.dll, doorstop_config.ini, bamp-preloader/   server-only preloader so mods load without Steam
#
#   ./install-into-amp.sh /home/amp/.ampdata/instances/BigAmbitions01/bigambitions
#   ./install-into-amp.sh --build /home/amp/.ampdata/instances/BigAmbitions01/bigambitions
#       --build: first compile Going Public from this repo against the game files AMP downloaded into
#                <instance>/1331550 (needs the .NET SDK: sudo apt install dotnet-sdk-8.0)
#
# The argument is the instance's App.RootDir (the folder that holds .proton/, 1331550/, headless/).
# Run it as the user AMP runs as (usually "amp"), then in AMP: Update, then Start.
set -euo pipefail
REPO="$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)"
BUILD=0
LANG_OPT=""
while [ $# -gt 0 ]; do
    case "$1" in
        --build) BUILD=1; shift ;;
        --lang) LANG_OPT="$(echo "${2:-}" | tr 'A-Z' 'a-z')"; shift 2 ;;
        *) break ;;
    esac
done
DEST="${1:?Pfad zum App.RootDir der AMP-Instanz angeben (Ordner mit .proton/ und 1331550/)}"
DEST="${DEST%/}"
[ -d "$DEST" ] || { echo "Ordner fehlt: $DEST" >&2; exit 1; }
[ -d "$DEST/1331550" ] || [ -d "$DEST/.proton" ] || echo "Hinweis: $DEST hat (noch) weder 1331550/ noch .proton/ - erst in AMP 'Update' ausführen?" >&2

install -m 755 "$REPO/headless/amp/bamp-start.sh" "$DEST/bamp-start.sh"
mkdir -p "$DEST/telemetry-data"
rm -rf "${DEST:?}/bamp-collector"   # 1.0.11: the telemetry collector is gone
echo "Start-Wrapper installiert: $DEST/bamp-start.sh"
# The settings translation for AMP's update stage "Settings in the server language" (servers without this repo;
# the stage passes the ServerLanguage setting itself - bamp-server.json only gets it at the next start).
mkdir -p "$DEST/bamp-tools/i18n"
install -m 755 "$REPO/headless/amp/localize-template.py" "$REPO/headless/amp/bamp-web-sync.sh" "$REPO/headless/amp/backup-saves.sh" \
    "$REPO/headless/amp/modtr-scan.py" "$REPO/headless/amp/seed-settings.py" "$REPO/headless/amp/bamp-translate.py" "$DEST/bamp-tools/"
install -m 644 "$REPO/headless/amp/bigambitionsconfig.json" "$REPO/headless/amp/bigambitionsports.json" "$DEST/bamp-tools/"
install -m 644 "$REPO/headless/amp/i18n/"*.json "$DEST/bamp-tools/i18n/"
# AMP's own interface in German (bamp-web-sync.sh copies it into the ADS WebRoot/Locale; open AMP once with ?lang=de)
mkdir -p "$DEST/bamp-tools/amp-locale"
install -m 644 "$REPO/headless/amp/amp-locale/"* "$DEST/bamp-tools/amp-locale/"

# Player pages over AMP's own web server (and so over its HTTPS proxy, no extra port): the ranking page and the
# instance's key folder (ranking.json, mods.json) in telemetry-data/web; bamp-web-sync.sh (host, cron) gathers the
# key folders of all instances under <ADS WebRoot>/monitor -> https://<AMP address>/monitor/. BAMP_AMP_WEBROOT
# overrides the ADS WebRoot.
mkdir -p "$DEST/telemetry-data/web"
install -m 644 "$REPO/headless/amp/ranking.html" "$DEST/telemetry-data/web/index.html"
install -m 644 "$REPO/media/logo-640.jpg" "$DEST/telemetry-data/web/"
# key-protected files live in a folder named after SHA-256(lower-case key) (DedicatedHost.KeyFolder); the old folder
# named after the key itself (telemetry monitor export, first ranking) goes. An empty ranking until the first player.
K="$(python3 -c 'import json,sys; print(str(json.load(open(sys.argv[1], encoding="utf-8-sig")).get("TelemetryKey","")).strip())' "$DEST/1331550/bamp-server.json" 2>/dev/null || true)"
[ -z "$K" ] && [ -f "$DEST/telemetry-data/key.txt" ] && K="$(tr -d '[:space:]' < "$DEST/telemetry-data/key.txt")"
if [ -n "$K" ]; then
    rm -rf "${DEST:?}/telemetry-data/web/$K"
    KDIR="$DEST/telemetry-data/web/$(printf '%s' "$K" | tr 'A-Z' 'a-z' | sha256sum | cut -c1-16)"
    mkdir -p "$KDIR"
    [ -f "$KDIR/ranking.json" ] || printf '{"updatedUnix": %s, "players": []}\n' "$(date +%s)" > "$KDIR/ranking.json"
fi
rm -rf "$DEST/telemetry-data/web/ranking.html" "$DEST/telemetry-data/web/join.html" "$DEST/telemetry-data/web/download"
if [ -n "${BAMP_AMP_WEBROOT:-}" ] || ls "$(dirname "$(dirname "$DEST")")"/*/ADSModule.kvp >/dev/null 2>&1; then
    "$REPO/headless/amp/bamp-web-sync.sh" ${BAMP_AMP_WEBROOT:+"$BAMP_AMP_WEBROOT"} \
        && echo "Spieler-Seiten über AMP: https://<AMP-Adresse>/monitor/<Spiel-Port>/index.html je Instanz (per Cron: bamp-web-sync.sh)"
else
    echo "Hinweis: keine ADS-Instanz mit WebRoot gefunden - keine Spieler-Seiten über AMP." >&2
fi

if [ "$BUILD" = 1 ]; then
    command -v dotnet >/dev/null || { echo ".NET SDK fehlt: sudo apt install dotnet-sdk-8.0" >&2; exit 1; }
    [ -d "$DEST/1331550/Big Ambitions_Data/Managed" ] || { echo "Spieldateien fehlen in $DEST/1331550 - erst in AMP 'Update' ausführen." >&2; exit 1; }
    echo "Baue Going Public gegen $DEST/1331550 ..."
    # ModDeployDir: a scratch folder - the csproj's copy-to-ModsLocal step must not write into a random place here.
    dotnet build "$REPO/BigAmbitionsMP.csproj" -c Release -nologo "-p:GameDir=$DEST/1331550/" "-p:ModDeployDir=$REPO/bin/amp-deploy/"
    echo "Baue den Server-Vorlader (Mods ohne Steam) ..."
    dotnet build "$REPO/headless/preloader/BampSteamless.csproj" -c Release -nologo
fi

# Server-only preloader: the game loads mods only after Steam has started, and there is no Steam client under AMP.
# Unity Doorstop (winhttp.dll) loads bamp-preloader/BampSteamless.dll before the game, which lets the mod loader
# start without Steam. bamp-start.sh enables it (WINEDLLOVERRIDES=winhttp=n,b). Players never need this.
PRE="$REPO/headless/preloader/bin/Release/net48"
if [ -f "$PRE/BampSteamless.dll" ] && [ -d "$DEST/1331550" ]; then
    DOORSTOP_VER=4.3.0
    DOORSTOP_SHA=e23fec7a6adfa07ba26a3e209b617584d0319a1145506ae4e1b892c44e86f679
    CACHE="$REPO/bin/doorstop-$DOORSTOP_VER"
    if [ ! -f "$CACHE/x64/winhttp.dll" ]; then
        mkdir -p "$CACHE"
        curl -fsSL -o "$CACHE/doorstop.zip" "https://github.com/NeighTools/UnityDoorstop/releases/download/v$DOORSTOP_VER/doorstop_win_release_$DOORSTOP_VER.zip"
        echo "$DOORSTOP_SHA  $CACHE/doorstop.zip" | sha256sum -c --quiet - || { echo "Doorstop-Download: Prüfsumme falsch" >&2; rm -f "$CACHE/doorstop.zip"; exit 1; }
        python3 -I -m zipfile -e "$CACHE/doorstop.zip" "$CACHE"
    fi
    install -m 644 "$CACHE/x64/winhttp.dll" "$DEST/1331550/winhttp.dll"
    printf '%s\n' '[General]' 'enabled=true' 'target_assembly=bamp-preloader\BampSteamless.dll' 'redirect_output_log=false' \
        'boot_config_override=' 'ignore_disable_switch=false' '' '[UnityMono]' 'dll_search_path_override=' 'debug_enabled=false' \
        > "$DEST/1331550/doorstop_config.ini"
    mkdir -p "$DEST/1331550/bamp-preloader"
    install -m 644 "$PRE/BampSteamless.dll" "$PRE/0Harmony.dll" "$DEST/1331550/bamp-preloader/"
    echo "Server-Vorlader installiert: $DEST/1331550/winhttp.dll, doorstop_config.ini, bamp-preloader/"
fi

# A finished build (from build-nobara.sh / dotnet build) goes to bamp-mod/, which AMP's update stage copies into the prefix.
BUILT=""
for c in "$REPO/bin/Release/net48" "$REPO/bin/Dev/net48" "$REPO/bin/Debug/net48"; do
    [ -f "$c/BigAmbitionsMP.dll" ] && { BUILT="$c"; break; }
done
if [ -n "$BUILT" ]; then
    M="$DEST/bamp-mod/GoingPublicServer"
    rm -rf "$DEST/bamp-mod/BigAmbitionsMP"   # the folder name before the rename
    rm -rf "$M"; mkdir -p "$M/Dependencies" "$M/assets"
    cp "$BUILT/BigAmbitionsMP.dll" "$M/"
    for d in 0Harmony.dll LiteNetLib.dll NLayer.dll; do [ -f "$BUILT/$d" ] && cp "$BUILT/$d" "$M/Dependencies/"; done
    for a in BAMP_ChatIcon.png BAMP_HubIcon.png BAMP_LoiterIcon.png BAMP_RadioIcon.png BAMP_NavIcon.png BAMP_PlannerIcon.png BAMP_InsightsIcon.png BAMP_CameraIcon.png BAMP_SignsIcon.png GO_AppIcon.png; do [ -f "$REPO/assets/$a" ] && cp "$REPO/assets/$a" "$M/assets/"; done
    for a in "$REPO"/goingonline/assets/GO_*.png "$REPO"/assets/BP_*.png; do [ -f "$a" ] && cp "$a" "$M/assets/"; done   # Going Online (module 8): its app icon and the icons of its items (GAME-TYPES-1)
    if ls "$REPO"/goingonline/assets/models/*.gom >/dev/null 2>&1; then mkdir -p "$M/assets/models" && cp "$REPO"/goingonline/assets/models/*.gom "$M/assets/models/"; fi   # its own 3D models (GO-MODELS-1)
    [ -f "$REPO/assets/preview.png" ] && cp "$REPO/assets/preview.png" "$M/"
    cp "$REPO/LICENSE" "$REPO/THIRD-PARTY-NOTICES.txt" "$M/" 2>/dev/null || true
    [ -d "$REPO/Locales" ] && cp -r "$REPO/Locales" "$M/"   # translations (the game registers <mod>/Locales)
    [ -d "$REPO/ModTranslations" ] && cp -r "$REPO/ModTranslations" "$M/"   # other mods in the player's language (ModTranslations.cs)
    echo "Going Public ($BUILT) nach $M kopiert - beim nächsten Start kommt es ins Proton-Präfix."
    # The same build as a zip for the players (the server refuses other builds) - for the Steam Workshop upload, in
    # the instance folder (AMP file manager: bigambitions/client/). The hash in the name tells builds apart.
    DL="$DEST/client"
    mkdir -p "$DL"
    ZIP="$(python3 - "$M" "$DL" "$REPO/src/MyPluginInfo.cs" <<'PY'
import hashlib, pathlib, re, sys, zipfile
mod, dl, info = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]), pathlib.Path(sys.argv[3])
ver = (re.search(r'PLUGIN_VERSION\s*=\s*"([^"]+)"', info.read_text(encoding="utf-8")) or [None, "x"])[1]
h = hashlib.sha256((mod / "BigAmbitionsMP.dll").read_bytes()).hexdigest()[:8]
name = f"GoingPublicServer-{ver}-{h}.zip"
for old in list(dl.glob("GoingPublicServer-*.zip")) + list(dl.glob("BigAmbitionsMP-*.zip")):
    if old.name != name:
        old.unlink()
with zipfile.ZipFile(dl / name, "w", zipfile.ZIP_DEFLATED) as z:
    for f in sorted(mod.rglob("*")):
        if f.is_file():
            z.write(f, "GoingPublicServer/" + f.relative_to(mod).as_posix())
print(name)
PY
)"
    echo "Spieler-Paket (für den Steam Workshop) in AMP: Dateimanager > client/$ZIP"
else
    echo "Kein Build gefunden (bin/Release/net48/BigAmbitionsMP.dll). Erst bauen (siehe headless/HEADLESS.md) oder den Mod-Ordner per AMP-Dateimanager nach bamp-mod/BigAmbitionsMP hochladen."
fi

# Settings file: AMP's start only MERGES its settings into keys already in bamp-server.json - a key missing from the
# file never gets the value set on AMP's settings page. Add every setting of the template with its default.
CONF="$DEST/1331550/bamp-server.json"
if [ -f "$CONF" ] && command -v python3 >/dev/null; then
    python3 "$REPO/headless/amp/seed-settings.py" "$CONF" "$REPO/headless/amp/bigambitionsconfig.json" || true
fi
# MULTILANG-1: AMP shows template fields exactly as the template words them. Write the instance's settings manifest
# in the server language (--lang xx, else ServerLanguage from bamp-server.json; texts in headless/amp/i18n/<lang>.json).
# AMP reads it when the instance starts.
INSTANCE="$(dirname "$DEST")"
if [ -f "$INSTANCE/configmanifest.json" ] && command -v python3 >/dev/null; then
    L="$LANG_OPT"
    if [ -z "$L" ] && [ -f "$CONF" ]; then
        L="$(python3 -c 'import json,sys; print(str(json.load(open(sys.argv[1], encoding="utf-8-sig")).get("ServerLanguage", "")).strip().lower())' "$CONF" 2>/dev/null || true)"
    fi
    if [ -n "$L" ]; then
        python3 "$REPO/headless/amp/localize-template.py" "$L" --manifest "$INSTANCE/configmanifest.json" \
            && echo "AMP-Einstellungen in Sprache '$L' (wirksam nach einem Neustart der Instanz)."
    fi
fi
command -v python3 >/dev/null || echo "WARNUNG: python3 fehlt auf diesem Server - Server-Schlüssel aus den Einstellungen, Mod-Liste und Übersetzungsprüfung gehen nicht (sudo apt install python3)." >&2
cat <<TXT

Fertig. In AMP:
  1. Instanz neu laden (die Vorlage ruft jetzt bamp-start.sh auf), "Update", dann "Start".
  2. In der Konsole erscheint [BAMP-HEADLESS] READY; der Server-Schlüssel steht auf der Statusseite.
  3. Firewall: UDP 7777 für das Spiel öffnen.
  4. Spieler: Mod im Steam Workshop abonnieren; im Spiel Join a Game -> Host address = AMP-Adresse,
     Server key = der Schlüssel. Rangliste: Connection Link der Instanz (https://<AMP-Adresse>/monitor/<Spiel-Port>/index.html).
TXT
