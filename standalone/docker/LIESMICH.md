# Going Public Server – Docker-Vorlage

Big-Ambitions-Mehrspieler-Server mit Weboberfläche in einem Container (Debian 13, Proton GE, Xvfb). *(English below.)*

## Start

```sh
docker compose up -d --build
docker compose logs goingpublicserver      # die ZUGANGSDATEN stehen hier - nur beim ersten Start
```

Das Image holt sich das Programm aus `GoingPublicServer-<Version>-Linux.tar.gz` neben dem `Dockerfile` – liegt keine
Datei da, lädt es das Release von GitHub.

Danach im Browser `http://<dieser Rechner>:8080`:

1. anmelden;
2. **Installation**: das Spiel laden, mit dem Steam-Konto, das Big Ambitions besitzt (bei Steam Guard den Code eingeben), und
   **Proton GE** installieren;
3. **Einstellungen**: eine Welt wählen oder unter „New world“ eine neue anlegen;
4. **Übersicht**: Starten. Spieler treten mit der Adresse des Rechners, Port 7777 und dem Server-Schlüssel bei.

## Gut zu wissen

| | |
|---|---|
| Neues Passwort | `docker compose exec goingpublicserver gps reset-password` – erscheint im Terminal |
| Daten | `./data` (Spiel ~7 GB, Proton, Spielstände, Einstellungen, Backups) – gehört `PUID`/`PGID` (Standard 1000) |
| Ports | `8080/tcp` Weboberfläche (von außen nur hinter HTTPS/VPN), `7777/udp` Spiel – muss zur Einstellung „Game Port“ passen |
| Stoppen | `docker compose stop` – der Server speichert vorher (bis zu 5 min Zeit, `stop_grace_period`) |
| Update | neues Paket neben das `Dockerfile`, Version in `docker-compose.yml`, dann `docker compose up -d --build` |
| Sprache | `GPS_LANG: de` oder `en` (Meldungen der Verwaltung); die Weboberfläche schaltet selbst um |

---

**English:** `docker compose up -d --build`, then `docker compose logs goingpublicserver` shows the access data (once).
Open `http://<this machine>:8080`, log in, download the game under Installation (Steam account that owns Big Ambitions)
and install Proton GE, choose or create a world under Settings, then Start. New password:
`docker compose exec goingpublicserver gps reset-password`. Data live in `./data`; ports 8080/tcp (web) and 7777/udp (game).
