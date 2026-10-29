# Big Ambitions – Going Public Server


![Going Public Server](logo.jpg)


<img src="Screenshot 2026-10-29 101200.png" alt="" width="300">
<img src="Screenshot 2026-10-29 101231.png" alt="" width="300">


> The repository name has no hyphens on purpose: AMP cannot upgrade instances from template repositories whose name contains one.
>
> 
Dedicated server for **Big Ambitions** multiplayer, as a template for the
[AMP](https://cubecoders.com/AMP) game server panel. The game runs headless under Proton with the
**Going Public Server** mod; players join with the server's address and server key.

*Deutsch weiter unten.*

## Installing on an AMP server (Linux)

1. In AMP (ADS): **Configuration → Instance Deployment → Configuration Repositories**, add
   `amaus1209/BigAmbitionsGoingPublicServer:main`, then **Fetch latest**.
2. Create an instance from the template **Big Ambitions (Going Public server)**.
3. In the instance: **Update**. AMP downloads the game with SteamCMD (it asks for a Steam login that owns
   Big Ambitions, and the Steam Guard code if the account has one), Proton GE, and the newest
   **Going Public Server** release from this repository.
4. **Start**. The console shows `[BAMP-HEADLESS] READY`, the status page shows the **server key**.
5. Open UDP port 7777 (the game port) in your firewall.

Settings (world, save to load, max players, autosave, frame rate, language …) are on the instance's
configuration page. Choose the server language there and run **Update** once more to see the settings in it.

### Workshop mods on the server

Configuration → Updates → **Steam Workshop Items**: one Workshop item ID per line, then **Update**. AMP
downloads them with the same Steam login; the server installs them at the next start. Players who lack a
mod get a button that opens its Workshop page in Steam.

### Optional on the host: ranking page and save backups

Instances run in containers and cannot reach AMP's web folder, so two small jobs run on the host, as the user AMP
runs as (`crontab -e`; the scripts come with every instance in `bigambitions/bamp-tools/`):

```
* * * * *  ~/.ampdata/instances/<instance>/bigambitions/bamp-tools/bamp-web-sync.sh
17 4 * * * ~/.ampdata/instances/<instance>/bigambitions/bamp-tools/backup-saves.sh --all >> ~/backups/saves/backup.log 2>&1
```

- `bamp-web-sync.sh` serves the ranking of **all** Going Public Server instances at
  `https://<your AMP address>/monitor/index.html` (the instance's Connection Link); each player opens the
  ranking of their server with its server key.
- `backup-saves.sh --all` saves every instance's multiplayer saves to `~/backups/saves/` (14 per instance).

## Players

1. Subscribe to **Going Public Server** in the Steam Workshop of Big Ambitions. It must be the same version as
   the server (the server refuses other versions; Steam keeps the subscription up to date).
2. Start Big Ambitions, enable the mod, then **Multiplayer → Join a Game**: host address = the server's
   address, **Server key** = the key from the server's status page.

---

## Deutsch

**Installation in AMP:** Konfiguration → Instance Deployment → Configuration Repositories:
`amaus1209/BigAmbitionsGoingPublicServer:main` eintragen, **Fetch latest**, eine Instanz aus der Vorlage
**Big Ambitions (Going Public server)** anlegen, **Update** (lädt das Spiel per SteamCMD mit einem Steam-Konto,
das Big Ambitions besitzt – bei Steam Guard fragt AMP nach dem Code –, Proton GE und das neueste Release von
hier), dann **Start**. Der Server-Schlüssel steht auf der Statusseite. UDP-Port 7777 freigeben.

Die Server-Sprache in den Einstellungen wählen und noch einmal **Update** – danach erscheinen die
Einstellungen in dieser Sprache.

**Workshop-Mods:** Konfiguration → Updates → **Steam Workshop Items**: eine ID pro Zeile, dann **Update**.

**Spieler:** **Going Public Server** im Steam Workshop von Big Ambitions abonnieren (gleiche Version wie der
Server), Mod aktivieren, **Multiplayer → Join a Game** mit der Server-Adresse und dem **Server key**.
