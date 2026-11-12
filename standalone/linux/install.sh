#!/usr/bin/env bash
# Going Public Server - installation on Linux (Debian / Ubuntu with systemd).
#
#   sudo ./install.sh [--dir /opt/goingpublicserver] [--data /var/lib/goingpublicserver] [--user goingpublic]
#                     [--port 8080] [--lang de|en] [--no-start]
#   sudo ./install.sh --uninstall [--purge]
#
# Installs what the server needs (the libraries Proton GE runs the Windows game with, Xvfb, python3, SteamCMD's 32-bit
# libraries), a system user, the program and the systemd service "goingpublicserver", and shows the access data of the
# web interface HERE in the terminal (once). Run it again later = update: program and server files are replaced, the
# data (game, saves, settings, access) stay.
set -euo pipefail
HERE="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"
DIR=/opt/goingpublicserver
DATA=/var/lib/goingpublicserver
USR=goingpublic
PORT=""
LANGSEL=""
START=1
UNINSTALL=0
PURGE=0
SERVICE=goingpublicserver
while [ $# -gt 0 ]; do
    case "$1" in
        --dir) DIR="${2:?}"; shift 2 ;;
        --data) DATA="${2:?}"; shift 2 ;;
        --user) USR="${2:?}"; shift 2 ;;
        --port) PORT="${2:?}"; shift 2 ;;
        --lang) LANGSEL="${2:?}"; shift 2 ;;
        --no-start) START=0; shift ;;
        --uninstall) UNINSTALL=1; shift ;;
        --purge) PURGE=1; shift ;;
        -h|--help) sed -n '2,12p' "$0"; exit 0 ;;
        *) echo "unknown option: $1" >&2; exit 2 ;;
    esac
done
case "${LANGSEL:-${LC_ALL:-${LC_MESSAGES:-${LANG:-}}}}" in de*) DE=1; LANGSEL=de ;; *) DE=0; LANGSEL="${LANGSEL:-en}" ;; esac
say() { if [ "$DE" = 1 ]; then printf '%s\n' "$1"; else printf '%s\n' "$2"; fi; }
[ "$(id -u)" = 0 ] || { say "Bitte mit sudo starten: sudo $0" "Please run it with sudo: sudo $0"; exit 1; }

if [ "$UNINSTALL" = 1 ]; then
    systemctl disable --now "$SERVICE" 2>/dev/null || true   # the manager stops the game first (it saves)
    rm -f "/etc/systemd/system/$SERVICE.service" /usr/local/bin/goingpublicserver
    systemctl daemon-reload 2>/dev/null || true
    rm -rf "$DIR"
    say "Programm entfernt. Die Daten bleiben in $DATA." "Program removed. The data stay in $DATA."
    if [ "$PURGE" = 1 ]; then
        read -r -p "$(say "Auch $DATA löschen (Spiel, Spielstände, Backups)? Tippe JA: " "Also delete $DATA (game, saves, backups)? Type YES: ")" a
        if [ "$a" = JA ] || [ "$a" = YES ]; then
            rm -rf "$DATA"
            userdel "$USR" 2>/dev/null || true
            say "Daten gelöscht." "Data deleted."
        fi
    fi
    exit 0
fi

[ -x "$HERE/GoingPublicServer" ] && [ -d "$HERE/payload" ] || { say "GoingPublicServer und payload/ fehlen neben install.sh." "GoingPublicServer and payload/ are missing next to install.sh."; exit 1; }
command -v apt-get >/dev/null && command -v systemctl >/dev/null || {
    say "Dieses Skript braucht apt (Debian/Ubuntu) und systemd. Andere Systeme: die Docker-Vorlage nutzen, oder ./GoingPublicServer --data <Ordner> direkt starten." \
        "This script needs apt (Debian/Ubuntu) and systemd. Other systems: use the Docker template, or start ./GoingPublicServer --data <folder> directly."
    exit 1
}
[ "$(uname -m)" = x86_64 ] || { say "Nur x86_64 (64-Bit-PC/Server) wird unterstützt." "Only x86_64 is supported."; exit 1; }

say "== 1/5 Pakete ==" "== 1/5 packages =="
dpkg --add-architecture i386
apt-get update -qq
pick() { for p in "$@"; do if apt-cache show "$p" >/dev/null 2>&1; then echo "$p"; return; fi; done; }   # renamed packages (t64)
PKGS=(ca-certificates curl tar gzip xz-utils python3 procps util-linux xvfb xauth
      libc6:i386 libstdc++6:i386 libgcc-s1:i386
      libfreetype6 libfreetype6:i386 libfontconfig1 libfontconfig1:i386 fonts-dejavu-core
      libx11-6 libx11-6:i386 libxext6 libxrender1 libxcomposite1 libxcursor1 libxi6 libxrandr2 libxinerama1 libxkbcommon0
      libgl1 libgbm1 libvulkan1 libdbus-1-3 libudev1 libpulse0 libsdl2-2.0-0
      "$(pick libasound2t64 libasound2)" "$(pick libgnutls30t64 libgnutls30)")
DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends "${PKGS[@]}"

say "== 2/5 Benutzer $USR ==" "== 2/5 user $USR =="
id "$USR" >/dev/null 2>&1 || useradd --system --home-dir "$DATA" --shell /usr/sbin/nologin "$USR"
GRP="$(id -gn "$USR")"
mkdir -p "$DATA"
chown "$USR:$GRP" "$DATA"
chmod 750 "$DATA"

say "== 3/5 Programm nach $DIR ==" "== 3/5 program into $DIR =="
if systemctl is-active --quiet "$SERVICE"; then
    say "Dienst läuft - wird gestoppt (der Server speichert vorher) ..." "Service running - stopping it (the server saves first) ..."
    systemctl stop "$SERVICE"
fi
mkdir -p "$DIR"
install -m 755 "$HERE/GoingPublicServer" "$DIR/GoingPublicServer"
rm -rf "$DIR/payload"
cp -r "$HERE/payload" "$DIR/payload"
[ -f "$HERE/LIESMICH.txt" ] && install -m 644 "$HERE/LIESMICH.txt" "$DIR/"
chmod -R a+rX "$DIR"
RESET="sudo goingpublicserver reset-password"
cat > /usr/local/bin/goingpublicserver <<EOF
#!/bin/sh
# Going Public Server as its user - e.g.  sudo goingpublicserver reset-password
if [ "\$(id -u)" = 0 ]; then
    exec runuser -u $USR -- env GPS_RESET_HINT="$RESET" $DIR/GoingPublicServer --data $DATA "\$@"
fi
exec sudo -u $USR env GPS_RESET_HINT="$RESET" $DIR/GoingPublicServer --data $DATA "\$@"
EOF
chmod 755 /usr/local/bin/goingpublicserver

say "== 4/5 Zugang zur Weboberfläche ==" "== 4/5 web interface access =="
runuser -u "$USR" -- env HOME="$DATA" GPS_RESET_HINT="$RESET" "$DIR/GoingPublicServer" setup --data "$DATA" --lang "$LANGSEL" ${PORT:+--port "$PORT"}

say "== 5/5 Dienst $SERVICE ==" "== 5/5 service $SERVICE =="
cat > "/etc/systemd/system/$SERVICE.service" <<EOF
[Unit]
Description=Going Public Server (Big Ambitions multiplayer server and web interface)
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=$USR
Group=$GRP
WorkingDirectory=$DATA
Environment=HOME=$DATA
Environment=GPS_RESET_HINT=$RESET
ExecStart=$DIR/GoingPublicServer run --data $DATA
Restart=on-failure
RestartSec=10
# the manager stops the game first (it saves) - give it the time; Wine leftovers go after it
KillMode=mixed
TimeoutStopSec=300
LimitNOFILE=524288

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable "$SERVICE" >/dev/null 2>&1
[ "$START" = 1 ] && systemctl restart "$SERVICE"

WPORT="$(sed -n 's/.*"WebPort": *\([0-9]*\).*/\1/p' "$DATA/manager/config.json" 2>/dev/null | head -n1)"
WPORT="${WPORT:-8080}"
IP="$(hostname -I 2>/dev/null | awk '{print $1}')"
echo
say "Fertig. Weboberfläche: http://${IP:-<Server-IP>}:$WPORT  (Zugangsdaten siehe oben)" "Done. Web interface: http://${IP:-<server IP>}:$WPORT  (access data above)"
say "  Weiter: anmelden, unter „Installation“ das Spiel mit dem Steam-Konto laden, das Big Ambitions besitzt, und Proton GE installieren." \
    "  Next: log in, download the game under \"Installation\" with the Steam account that owns Big Ambitions, and install Proton GE."
say "  Dienst: systemctl status $SERVICE · Log: journalctl -u $SERVICE -f · Passwort vergessen: $RESET" \
    "  Service: systemctl status $SERVICE · log: journalctl -u $SERVICE -f · forgot the password: $RESET"
say "  Ports: UDP 7777 (Spiel) freigeben; TCP $WPORT (Weboberfläche) nur, wenn du sie von außen brauchst - dann besser hinter HTTPS." \
    "  Ports: open UDP 7777 (game); TCP $WPORT (web interface) only if you need it from outside - better behind HTTPS then."
if command -v ufw >/dev/null && ufw status 2>/dev/null | grep -q "Status: active"; then
    say "  ufw ist aktiv:  sudo ufw allow 7777/udp   (und ggf. sudo ufw allow $WPORT/tcp)" "  ufw is active:  sudo ufw allow 7777/udp   (and maybe sudo ufw allow $WPORT/tcp)"
fi
