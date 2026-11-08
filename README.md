# Big Ambitions MP Server

Ein Dauer-Server für Big Ambitions: **Going Public Server** 1.0.0 – der Community-Multiplayer-Mod Going Public (v0.3.3) erweitert um einen
**Dedicated-Host** und einen **Headless-Server-Modus** für AMP auf Ubuntu (Proton, ohne Grafik).

> **Status: Entwurf, nicht kompiliert, nicht im Spiel getestet.** Der erste Schritt ist der Machbarkeitstest in
> [`headless/HEADLESS.md`](headless/HEADLESS.md): Startet Big Ambitions unter Proton mit `-batchmode -nographics`?

## Inhalt

| Ordner / Datei | Was |
|---|---|
| `src/` | Quellcode von Going Public plus die Server-Erweiterungen (siehe unten) |
| `headless/HEADLESS.md` | Server-Modus: Machbarkeitstest, AMP, Einstellungen, Befehle |
| `headless/amp/` | AMP-Generic-Template (SteamCMD + Proton GE) |
| `headless/run-headless.sh` | Startskript für Linux ohne AMP (mit Neustart-Schleife) |
| `headless/bamp-server.example.json` | Beispiel-Einstellungen für den Server |
| `DEDICATED.md` | Dedicated-Host: Idle-Pause, Auto-Accept, gleiche Mods, Mod-Download für Clients |
| `build-windows.ps1`, `build-nobara.sh` | Going Public bauen, nach `ModsLocal` installieren und eine alte separate Telemetry Bridge wegräumen |

## Server-Erweiterungen in `src/`

* `DedicatedHost.cs`: Die Welt pausiert ohne Spieler, Beitritte werden automatisch angenommen und der Server lädt den
  **neuesten** Stand des Spiels selbst.
* `HeadlessHost.cs`: Server-Modus ohne Fenster. Er schreibt Konsolenereignisse für AMP, nimmt Befehle an, schreibt `status.json`,
  setzt Frame-Limits und beendet sich bei Fehlern mit Exit-Code, damit AMP neu startet.
* `ModAutoDownload.cs`: Ein Client lädt die Workshop-Mods des Hosts herunter (nur mit Zustimmung).
* Änderungen in `MPConfig.cs` (`bamp-server.json`), `MPServer.cs` (Idle-Pause), `MPCanvasUI.cs`, `MPClient.cs`,
  `MPContentFingerprint.cs`, `Plugin.cs`.

## Bauen

Wie das Original (net48, braucht die Spiel-DLLs):
`dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions"`.
Den Ausgabeordner dann nach `%LocalAppData%Low\Hovgaard Games\Big Ambitions\ModsLocal\GoingPublicServer\` legen (auf dem Server: in
den Proton-Prefix, siehe HEADLESS.md). **Alle Spieler brauchen dieselbe Version und dieselben Mods.**

## Lizenz und Herkunft

Going Public stammt von Melaus ([Melaus123/BigAmbitionMulti](https://github.com/Melaus123/BigAmbitionMulti)) und steht unter der
MIT-Lizenz, Copyright 2026 Melaus. Siehe [`LICENSE`](LICENSE) und [`THIRD-PARTY-NOTICES.txt`](THIRD-PARTY-NOTICES.txt). Diese
Server-Erweiterungen sind ein inoffizieller Fork. Big Ambitions gehört Hovgaard Games. Für das Spiel braucht jeder Spieler und
auch der Server eine eigene Lizenz.
