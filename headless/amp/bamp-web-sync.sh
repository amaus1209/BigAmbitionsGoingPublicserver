#!/usr/bin/env bash
# Player pages of ALL Going Public Server instances: https://<AMP address>/monitor/<game port>/index.html per instance
# (and the older shared https://<AMP address>/monitor/index.html, where the key picks the server)
# AMP's web server (ADS) serves <ADS>/WebRoot; instances run in containers and cannot reach it, so this runs on the
# host (as the AMP user), from cron every minute:
#     * * * * *  /path/to/bamp-web-sync.sh
# It keeps <ADS>/WebRoot/monitor as a real folder holding
#   - the ranking page (index.html) and the logo, from the instance updated last,
#   - one link per server key: monitor/<key folder> -> <instance>/telemetry-data/web/<key folder> (the folder name is
#     SHA-256 of the key, so a player's key opens the ranking of exactly that server),
# and drops links of deleted instances or changed keys. It also keeps AMP's German interface file in <WebRoot>/Locale.
# Changes nothing when nothing changed.
#     bamp-web-sync.sh [<ADS WebRoot>]     (default: the WebRoot of the ADS instance found under ~/.ampdata/instances)
set -uo pipefail
INST="$HOME/.ampdata/instances"
WEBROOT="${1:-${BAMP_AMP_WEBROOT:-}}"
if [ -z "$WEBROOT" ]; then
    for k in "$INST"/*/ADSModule.kvp; do
        [ -f "$k" ] && [ -d "$(dirname "$k")/WebRoot" ] && { WEBROOT="$(dirname "$k")/WebRoot"; break; }
    done
fi
[ -n "$WEBROOT" ] && [ -d "$WEBROOT" ] || { echo "bamp-web-sync: keine ADS-WebRoot gefunden" >&2; exit 1; }
M="$WEBROOT/monitor"
[ -L "$M" ] && rm -f "$M"   # the single-instance link of earlier versions
mkdir -p "$M"

newest=""
for web in "$INST"/*/*/telemetry-data/web; do
    [ -d "$web" ] || continue
    for d in "$web"/*/; do
        d="${d%/}"; h="$(basename "$d")"
        [[ "$h" =~ ^[0-9a-f]{16}$ ]] || continue
        [ "$(readlink "$M/$h" 2>/dev/null)" = "$d" ] || ln -sfn "$d" "$M/$h"
    done
    [ -f "$web/index.html" ] && { [ -z "$newest" ] || [ "$web/index.html" -nt "$newest/index.html" ]; } && newest="$web"
done
# RANK-URL-1 (user 2026-10-31: every instance needs its own ranking address - with several instances it was always the
# same): monitor/<game port> -> that instance's web folder (its page, logo and key folders), so
# https://<AMP address>/monitor/<port>/index.html is that server's ranking - the instance's Connection Link. Only that
# instance's key opens it there. The game port tells the instances apart: on one host each has its own.
ports=" "
for web in "$INST"/*/*/telemetry-data/web; do
    [ -d "$web" ] || continue
    port="$(sed -n 's/.*"HostPort": *\([0-9][0-9]*\).*/\1/p' "$(dirname "$(dirname "$web")")/1331550/bamp-server.json" 2>/dev/null | head -n1)"
    [[ "$port" =~ ^[0-9]{2,5}$ ]] || continue
    ports="$ports$port "
    [ "$(readlink "$M/$port" 2>/dev/null)" = "$web" ] || ln -sfn "$web" "$M/$port"
done
# ADMIN-1 (user 2026-11-02: an admin page "through the ranking login"): the admin part of each ranking page signs in at
# its own AMP instance (AMP's login and rights, through ADS at /API/ADSModule/Servers/<id>) - amp.json gives it the id
BASE="$(sed -n 's/^Network\.BaseURL=\(https\{0,1\}:\/\/[A-Za-z0-9.:-]*\)\/*\r\{0,1\}$/\1/p' "$(dirname "$WEBROOT")/ADSModule.kvp" 2>/dev/null | head -n1)"
IDS="$(python3 -I -c 'import json, sys
try:
    for e in json.load(open(sys.argv[1])): print(e.get("InstanceName", ""), e.get("InstanceID", ""))
except Exception: pass' "$(dirname "$INST")/instances.json" 2>/dev/null)"
for web in "$INST"/*/*/telemetry-data/web; do
    [ -d "$web" ] || continue
    name="${web#"$INST"/}"; name="${name%%/*}"
    id="$(printf '%s\n' "$IDS" | awk -v n="$name" '$1 == n { print $2; exit }')"
    [[ "$id" =~ ^[0-9a-f-]{36}$ ]] || continue
    j="{\"instance\":\"$id\""
    # ADMIN-LINK-1 (user 2026-11-02: "in AMP, under the ranking, the admin link too"): AMP's public address (ADS
    # Network.BaseURL, what its Connection Link is made of) - bamp-start.sh puts the admin page's address into the line
    # AMP's status page shows under the Connection Link
    [ -n "$BASE" ] && j="$j,\"base\":\"$BASE\""
    j="$j}"
    [ "$(cat "$web/amp.json" 2>/dev/null)" = "$j" ] || printf '%s' "$j" > "$web/amp.json"
done
for l in "$M"/*; do   # links of deleted instances / old keys / changed ports
    [ -L "$l" ] || continue
    n="$(basename "$l")"
    if [ ! -e "$l" ] || { [[ "$n" =~ ^[0-9]{2,5}$ ]] && [[ "$ports" != *" $n "* ]]; }; then rm -f "$l"; fi
done
if [ -n "$newest" ]; then
    for f in index.html logo-640.jpg; do
        [ -f "$newest/$f" ] && ! cmp -s "$newest/$f" "$M/$f" && cp "$newest/$f" "$M/$f"
    done
fi
# the download page of earlier versions goes (players use the Steam Workshop); its address forwards to the ranking
rm -rf "${M:?}/download"
J='<!doctype html><meta charset="utf-8"><meta http-equiv="refresh" content="0; url=index.html"><title>Going Public Server</title><a href="index.html">Going Public Server</a>'
[ "$(cat "$M/join.html" 2>/dev/null)" = "$J" ] || printf '%s' "$J" > "$M/join.html"
# AMP-LOCALE-1: AMP's own interface in German. AMP loads <WebRoot>/Locale/<lang>.json once a browser opened the panel
# with ?lang=<lang> (it remembers the choice). The files ship next to this script; an AMP update may rewrite the
# WebRoot, the next run puts them back.
LOC="$(dirname "$(readlink -f "$0")")/amp-locale"
if [ -d "$LOC" ]; then
    mkdir -p "$WEBROOT/Locale"
    for f in "$LOC"/*.json; do
        [ -f "$f" ] && ! cmp -s "$f" "$WEBROOT/Locale/$(basename "$f")" && cp "$f" "$WEBROOT/Locale/"
    done
fi
exit 0
