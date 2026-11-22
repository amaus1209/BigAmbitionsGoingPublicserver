# Going Public Server ohne AMP (STANDALONE-1)

Der Dedicated Server als eigenes Programm für **Windows**, **Linux** (mit Installationsskript) und **Docker** – jeweils mit
Weboberfläche und Anmeldung. Die Zugangsdaten erscheinen einmal: unter Windows in einem eigenen Fenster, unter Linux im
Terminal (Docker: Ausgabe des Containers).

## Bauen

```sh
standalone/build-packages.sh [--build] <Zielordner>
```

Ergebnis: `GoingPublicServer-<v>-Windows.zip`, `-Linux.tar.gz`, `-Docker.zip`, dazu `GoingPublicServer-Server.zip` (AMP) und die
Workshop-Datei `GoingPublicServer-<v>-<build>.zip`. Ohne `--build` nimmt es den letzten Mod-Build – Server-Zip und
Workshop-Zip bleiben derselbe Build. Die Verwaltung (`manager/`) wird immer gebaut: .NET 8, self-contained, eine Datei
(`dotnet publish -r linux-x64` bzw. `-r win-x64`), auch die Windows-Fassung auf Linux.

## Aufbau

| Teil | Was |
|---|---|
| `manager/` | Die Verwaltung `GoingPublicServer(.exe)`: ASP.NET Core (Kestrel) + Weboberfläche (`wwwroot/`, eingebettet), startet und überwacht das Spiel, SteamCMD, Proton GE (Linux), Einstellungen, Backups, Updates |
| `linux/install.sh` | Debian/Ubuntu: Pakete (Proton-Bibliotheken, Xvfb, python3, 32-Bit für SteamCMD), Benutzer `goingpublic`, `/opt/goingpublicserver`, Daten `/var/lib/goingpublicserver`, Dienst `goingpublicserver`, Befehl `goingpublicserver` |
| `windows/` | `Reset-Password.bat`, `LIESMICH.txt` |
| `docker/` | `Dockerfile` (Debian 13), `docker-compose.yml`, `entrypoint.sh` (PUID/PGID), `LIESMICH.md` |
| `payload/` (im Paket) | die Server-Dateien aus dem Server-Zip: Vorlader (`1331550/`), Mod, Einstellungsbeschreibung + Übersetzungen, Ranglisten-Seite, `VERSION` |

Datenordner (Windows: der Programmordner; Linux: `/var/lib/goingpublicserver`; Docker: `/data`):
`manager/` (config.json, users.json, manager.log), `steamcmd/`, `backups/`, `configmanifest.json` (die Mod trägt Welten und
Spielstände ein, AMP-PICK-1) und `server/` – aufgebaut wie AMPs `bigambitions/`: `1331550/` (Spiel + `bamp-server.json`),
`headless/`, `telemetry-data/` (Schlüssel, Ranglisten-Seite), `mods/` (die Mods des Servers), `logs/console.log`, unter Linux
`proton/`, `prefix/`, `home/`.

Start des Spiels wie unter AMP (`-batchmode -nographics -bamp-config … -bamp-headless-dir …`), dazu:
- `-bamp-nosteam` und `-bamp-mods-dir <server/mods>` – der Vorlader (`headless/preloader`) überspringt `SteamAPI.InitSteam`
  und biegt `ModDiscoveryRegistry.ModsLocalPath` um: kein Steam-Client, eigene Mods (wichtig unter Windows neben einem
  eigenen Spiel). AMP übergibt beides nicht und läuft unverändert.
- `-disable-force-steam`, `-logFile -` (Unitys Log auf stdout; mit einer Log-DATEI leitet Unity stdout in die Datei um und
  die `[BAMP-HEADLESS]`-Zeilen kommen nicht an).
- Linux: `xvfb-run -a <proton> runinprefix …`; das Präfix richtet ein Proton-Aufruf außer `runinprefix` ein (`getcompatpath`).

Befehle an den Server: Linux über stdin (wie AMPs Konsole), Windows über `headless/command.txt`. Stopp: `stop`, danach
SHUTDOWN-HANG-1 (30 s nach „stopped“ / 180 s nach „stopping“ hart beenden), Exit-Codes wie in `headless/HEADLESS.md`.

## Getestet (2026-11-01)

- Docker (Debian 13): Proton-GE-Download über die Oberfläche (x86_64-Archiv), Präfix, neue Welt, READY nach 80 s,
  `status` aus der Web-Konsole, Stopp über die Oberfläche und `docker stop` (gespeichert, Exit 0), Autostart, Passwort-Reset.
- `install.sh` in Ubuntu 24.04 und Debian 12 (systemctl ersetzt): Pakete, Benutzer, Dienstdatei, Zugangsdaten im Terminal,
  zweiter Lauf als Update; danach das Spiel unter Ubuntu bis READY, SIGTERM → gespeichert, keine Wine-Reste.
- Windows-Build: kompiliert; das Zugangsdaten-Fenster unter Wine (Proton) geprüft. **Nicht** auf echtem Windows getestet,
  ebenso wenig SteamCMD mit einem echten Steam-Login.
