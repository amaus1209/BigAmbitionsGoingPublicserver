# Big Ambitions – Going Public Server


![Going Public Server](logo.jpg)


<img src="Screenshot/Bildschirmfoto_20261031_200853.png" alt="Driving through the city at night, a friend's car with its owner's name" width="600">
<img src="Screenshot/Bildschirmfoto_20261031_200920.png" alt="Two players in the street, the friend's name in green" width="600">
<img src="Screenshot/Bildschirmfoto_20261031_200953.png" alt="The phone's Business page: two players merged into one company" width="600">
<img src="Screenshot/Screenshot 2026-10-29 101200.png" alt="" width="600">
<img src="Screenshot/Screenshot 2026-10-29 101231.png" alt="" width="600">
<img src="Screenshot/Screenshot 2026-10-29 101245.png" alt="" width="600">
<img src="Screenshot/Screenshot 2026-10-29 101259.png" alt="" width="600">
<img src="Screenshot/Screenshot 2026-10-29 101550.png" alt="" width="600">

> The repository name has no hyphens on purpose: AMP cannot upgrade instances from template repositories whose name contains one.

**Always-on multiplayer server for Big Ambitions.** Play together on a dedicated server that keeps running: the world
stays when you log off, it pauses while nobody is online, and you join with the server's address and server key. The
server is a template for the [AMP](https://cubecoders.com/AMP) game server panel; the game runs headless under Proton
with the **Going Public Server** mod
([Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3829681182)).

*Deutsch weiter unten.*

## Important

- **The server runs on AMP only** (Linux, headless, under Proton). There is no other server download yet. Players
  can no longer host a game themselves: multiplayer means joining a dedicated server.
- **Server and players need the same version.** Steam keeps the players' subscription up to date; update the AMP
  instance when a new release is out (the server refuses other versions).
- **No other mods on the server.** The server runs Going Public Server and nothing else: leave AMP's
  *Steam Workshop Items* empty and put nothing into `bamp-mods-extra/`. The built-in modules replace Immersion,
  BAScheduler, SmartSuite and VoogleRoute - players should not run those mods at the same time either.
- **Linux / Steam Deck (Proton):** play with **GE-Proton** (tested: GE-Proton11-11, the same as the server; install it
  with ProtonUp-Qt, then Properties → Compatibility). With Proton Experimental and Proton 11.0-2 the game crashed now
  and then while loading the world. Windows is not affected.

## News

<!-- Going Public Server 1.0.13+ shows every heading of this section as a card in the game's News panel on the
     main menu, newest first. German: the section "Neuigkeiten" in the German part. -->

### Going Public Server 1.0.16
- A player's cars stay in the world when he leaves; a car left in the road or parked illegally is parked at the roadside. Back on the server, his cars stand where the others saw them.
- A short right click into the world (not on a building or object, map closed) deletes the route.
- The wheels of the cars driving past turn and steer.
- **Self-driving**: in your car, "Drive there" (top of the screen, or "Drive there automatically" on the map's building card) drives you to the destination - blinkers in turns, stops at red lights and for cars and people ahead - and parks there. Steering, throttle or brake take over.
- Walking routes show **road** (orange) and **footpath** (blue).
- The mod keys (F6, F7, F8, Shift+F9/F10/F11) can be changed under **Options, Controls**.
- Smoother traffic: cars no longer jerk back on your screen, and on the server they brake in time behind the car ahead. Other players move smoothly too, even when packets get lost.
- A lost connection no longer pauses the world (AMP: "Pause when a connection drops"); the game joins the server again by itself. Weak links get a lower network rate automatically.
- Navigation: the shortest route, turn hints ("Turn right in 80 m"); self-driving follows the road's speed limit, drives into car parks and can follow a friend.
- Absent players' cars show "(offline)" and a grey pin on the map; a note (or, if set, a refusal) for an older mod version; less waiting when joining.
- **Server numbers on the ranking page**: network in and out in one chart (two colours), messages, server FPS, throttled players and each player's ping and loss; AMP's status page is tidy again.

### Going Public Server 1.0.15
- Linux/Proton: game hooks are written as self-contained jumps - fixes "Open in BizMan" showing the empty list instead of the building (rent, buy) and other rare failures there.
- The navigation names a destination by its address ("44 Second Street") instead of the street key.

### Going Public Server 1.0.14
- The load status shows while the game loads the mod; the server's own character is gone everywhere.
- Passengers see car parks, friends show their buildings, "Walk there automatically" on the map's building card.
- AMP: 60/120/200 FPS, a network rate setting; new worlds take the Custom values from the first start.

### Going Public Server 1.0.13
- Linux/Proton: the test file skip-tiny-patches.txt no longer turns multiplayer off.
- The main menu shows the mod's load status and these news; the mod's version is next to the game's.
- The bug report of the original Going Public is gone.

### 1.0.12: Joining a server
The server saves first, then lets you in, and always loads its own copy of your save. A save made inside a
building now starts you in front of it.

### 1.0.12: Built-in modules
F6 RPG camera (F8 settings), F7 shop signs, Shift+F9 planner, Shift+F10 insights, Shift+F11 navigation. The
server admin switches each one on or off in AMP.

### 1.0.12: Steam names and friends
Steam names above the players, friends in green and on the city map, a Friends tab on the phone's Business
page. Taxes by phone: Contacts -> Internal Revenue Service.

## Players: joining a server

1. Subscribe to **Going Public Server** in the Steam Workshop of Big Ambitions and enable it in the game's mod list.
2. **Multiplayer → Join Game**: **Host address** = the server's address, **Server key** = the key from the server
   admin.
3. Your progress stays on the server. **Escape → Leave server** saves it and takes you to the main menu.

## Built in

- **Steam names and friends**: the Steam name above every player, friends in green and on the city map, friend
  requests on the phone (Business → Friends); a friend's rented and bought buildings show under it there.
- **Pay taxes by phone**: Contacts → Internal Revenue Service → Call.
- **F7 Shop signs**: your own pictures on signs, storefront signs and billboards.
- **F6 RPG camera** (settings F8): third-person camera on foot and in the car.
- **Shift+F9 Planner**: checks schedules, fills them with the game's own auto-fill and suggests opening hours from
  customer demand.
- **Shift+F10 Insights**: profit per business, sales, staff, real estate and alerts.
- **Shift+F11 Navigation**: search a destination or click it on the city map, route line on foot and by car, and your
  character can walk there by itself, in your car it drives there and parks. A short right click into the world (map
  closed) deletes the route.
- On the main menu: the mod's load status, these news, and the mod's version next to the game's.
- 23 languages.

## Server owners: AMP

### Installing

1. In AMP (ADS): **Configuration → Instance Deployment → Configuration Repositories**, add
   `amaus1209/BigAmbitionsGoingPublicServer:main`, then **Fetch latest**.
2. Create an instance from the template **Big Ambitions (Going Public server)**.
3. In the instance: **Update**. AMP downloads the game with SteamCMD (it asks for a Steam login that owns
   Big Ambitions, and the Steam Guard code if the account has one), Proton GE, and the newest
   **Going Public Server** release from this repository.
4. **Start**. The console shows `[BAMP-HEADLESS] READY`; the status page shows the game port, the **server password** (the server key players join with), and the server version. Network traffic, server FPS and each player's connection are on the ranking page (see below).
5. Open the game port (UDP, 7777 by default) in your firewall.

### Updating

- **Update** downloads the newest release; restart the instance afterwards. Players get the matching version from the
  Steam Workshop.
- After an update, start the server once: new settings get their default then, and values set in AMP apply from the
  next start.
- **Stop** saves the world first and then ends the server (a few seconds).

### Settings (the instance's Configuration page)

- **Server language**: choose it, then run **Update** once more to see the settings in that language.
- **Server**: world, load a save once, the server's character name, max players, pausing (empty, idle, everyone in
  the pause menu), server key, autosave, **Frame cap while running** (60, 120 or 200 FPS) and while empty, and
  **Network rate** (Normal 10 / 5 Hz, High 20 / 10 Hz, Very high 30 / 15 Hz, Maximum 60 / 30 Hz - how often
  positions, cars and traffic go over the network; higher drives more smoothly, and since every player's stream goes
  to every other player, the data grows quickly with more players).
- **New world**: name and difficulty (Easy, Normal, Hard or Custom - the Custom values have their own tab). They only
  count when the server creates that world (it does not exist yet).
- **Modules**: switch each built-in module on or off for everybody. **RPG camera** and the navigation's route line are
  the same for every player on the server.
- **Game rules**: rent from owned buildings, building prices, bank loan limit and business sale.

### On the host: ranking page, save backups, AMP in German

Instances run in containers and cannot reach AMP's web folder, so small jobs run on the host, as the user AMP runs as
(`crontab -e`; the scripts come with every instance in `bigambitions/bamp-tools/`):

```
* * * * *  ~/.ampdata/instances/<instance>/bigambitions/bamp-tools/bamp-web-sync.sh
17 4 * * * ~/.ampdata/instances/<instance>/bigambitions/bamp-tools/backup-saves.sh --all >> ~/backups/saves/backup.log 2>&1
```

- `bamp-web-sync.sh` serves each instance's ranking at `https://<your AMP address>/monitor/<game port>/index.html`
  (the instance's Connection Link); players open it with their server's key. Merged companies show as "merged with ...". Below the ranking, **Server**
  shows the last hour: network traffic in and out, messages, server FPS, the network rate, throttled players and each
  player's ping, packet loss and rate (the server writes it every 15 s). It also copies a German
  translation of AMP's own interface into AMP's web folder: open AMP once with `https://<your AMP address>/?lang=de`
  (per browser).
- `backup-saves.sh --all` saves every instance's multiplayer saves to `~/backups/saves/` (14 per instance).

## Translations of other mods

Going Public Server adds the game languages other mods are missing (for the mods players run themselves - the server
runs none). The tables are in `ModTranslations/`; the game fetches them at every start, so a new translation needs no
mod update.

**Translating yourself:** at every start the server writes one ready file per mod and language to
`bigambitions/modtr-templates/ModTranslations/` (AMP file manager) - laid out like `ModTranslations/` here, with the
current translation or, where there is none, the English text. Translate only the text after the colon; keep keys,
placeholders (`{0}`, `{name}`) and tags (`<b>`). Upload the file to the same path here (Add file → Upload files). The
*Translations* action checks it and renews `index.json`; a red X under *Actions* names a line to fix.

## Credits

Unofficial fork of [Going Public](https://github.com/Melaus123/BigAmbitionMulti) by Melaus (MIT license).

The built-in modules are our own implementations, modelled on these Workshop mods. Many thanks to their authors:

- RPG camera: [Immersion - RPG Camera](https://steamcommunity.com/sharedfiles/filedetails/?id=3791985529) by Chamiks
- Planner: [BAScheduler - Schedule, Staff & Opening Hours Planner](https://steamcommunity.com/sharedfiles/filedetails/?id=3795741870) by Shanpapa
- Insights and game rules: [SmartSuite Empire Management](https://steamcommunity.com/sharedfiles/filedetails/?id=3793254452) (SmartInsights, SmartRent) by ᗪEEᒍ Gᗩᑌᗪᗩᖇᗪ
- Navigation: [Voogle Route](https://steamcommunity.com/sharedfiles/filedetails/?id=3740623471) with
  [LIB BA Player Location](https://steamcommunity.com/sharedfiles/filedetails/?id=3741773276) and
  [LIB BA Unified UI](https://steamcommunity.com/sharedfiles/filedetails/?id=3790426259) by - Capitaine -

Big Ambitions belongs to Hovgaard Games; every player and the server need their own copy of the game.

---

## Deutsch

### Neuigkeiten

#### Going Public Server 1.0.16
- Die Autos eines Spielers bleiben in der Welt, wenn er geht; ein Auto auf der Fahrbahn oder im Halteverbot wird am Straßenrand geparkt. Kommt er zurück, stehen seine Autos dort, wo die anderen sie gesehen haben.
- Ein kurzer Rechtsklick in die Welt (nicht auf ein Gebäude oder Objekt, Karte zu) löscht die Route.
- Die Räder der vorbeifahrenden Autos drehen sich und lenken mit.
- **Autopilot**: Im eigenen Auto fährt „Hinfahren“ (oben im Bild, oder „Ziel automatisch hinfahren“ in der Gebäude-Info der Karte) zum Ziel - mit Blinker beim Abbiegen, Halt an roten Ampeln und vor Autos und Personen - und parkt dort ein. Lenken, Gas oder Bremse übernimmt.
- Fußwege zeigen **Straße** (orange) und **Fußweg** (blau).
- Die Mod-Tasten (F6, F7, F8, Umschalt+F9/F10/F11) lassen sich unter **Optionen, Steuerung** ändern.
- Flüssigerer Verkehr: Autos springen auf deinem Bildschirm nicht mehr zurück, und auf dem Server bremsen sie rechtzeitig hinter dem Vordermann. Auch andere Spieler bewegen sich flüssig, selbst wenn Pakete verloren gehen.
- Ein Verbindungsabbruch pausiert die Welt nicht mehr (AMP: „Pause bei Verbindungsabbruch“); das Spiel tritt dem Server selbst wieder bei. Schwache Verbindungen bekommen automatisch einen niedrigeren Netzwerk-Takt.
- Navigation: kürzeste Route, Abbiegehinweise („Rechts abbiegen in 80 m“); der Autopilot hält das Tempolimit der Straße, fährt in Parkhäuser und kann einem Freund folgen.
- Autos abwesender Spieler zeigen „(offline)“ und ein graues Symbol auf der Karte; Hinweis (oder auf Wunsch Abweisung) bei älterer Mod-Version; kürzeres Warten beim Beitreten.
- **Server-Werte auf der Ranglisten-Seite**: Netzverkehr rein und raus in einem Diagramm (zwei Farben), Nachrichten, Server-FPS, gedrosselte Spieler sowie Ping und Verlust je Spieler; AMPs Statusseite ist wieder aufgeräumt.

#### Going Public Server 1.0.15
- Linux/Proton: Spiel-Umleitungen werden als eigenständige Sprünge geschrieben - behebt „In BizMan öffnen“, das statt des Gebäudes (mieten, kaufen) die leere Liste zeigte, und andere seltene Fehler dort.
- Die Navigation nennt ein Ziel mit seiner Adresse („44 Second Street“) statt mit dem Straßenschlüssel.

#### Going Public Server 1.0.14
- Der Ladestatus erscheint schon, während das Spiel den Mod lädt; die Server-Figur ist überall verschwunden.
- Mitfahrer sehen Parkhäuser, Freunde zeigen ihre Gebäude, „Ziel automatisch hinlaufen“ in der Gebäude-Info der Karte.
- AMP: 60/120/200 FPS, Netzwerk-Takt; neue Welten übernehmen die benutzerdefinierten Werte ab dem ersten Start.

#### Going Public Server 1.0.13
- Linux/Proton: Die Testdatei skip-tiny-patches.txt schaltet den Mehrspieler nicht mehr ab.
- Das Hauptmenü zeigt den Ladestatus des Mods und diese Neuigkeiten; die Mod-Version steht neben der des Spiels.
- Die Fehlermeldung des ursprünglichen Going Public ist entfernt.

#### 1.0.12: Beitreten
Der Server speichert zuerst und lässt dich dann herein; er lädt immer seine eigene Kopie deines Spielstands.
Wer in einem Gebäude gespeichert hat, startet jetzt davor.

#### 1.0.12: Eingebaute Module
F6 RPG-Kamera (F8 Einstellungen), F7 Ladenschilder, Umschalt+F9 Planer, Umschalt+F10 Auswertung,
Umschalt+F11 Navigation. Der Server-Admin schaltet jedes in AMP an oder aus.

#### 1.0.12: Steam-Namen und Freunde
Steam-Namen über den Spielern, Freunde grün und auf der Stadtkarte, der Tab Friends auf der Business-Seite im
Handy. Steuern per Telefon: Kontakte -> Internal Revenue Service.

### Wichtig

- **Der Server läuft nur unter AMP** (Linux, ohne Grafik, unter Proton). Einen anderen Server-Download gibt es noch
  nicht. Spieler können selbst kein Spiel mehr hosten: Mehrspieler heißt, einem dedizierten Server beizutreten.
- **Server und Spieler brauchen dieselbe Version.** Steam hält das Abo der Spieler aktuell; die AMP-Instanz nach einem
  neuen Release aktualisieren (der Server lehnt andere Versionen ab).
- **Keine anderen Mods auf dem Server.** Der Server läuft nur mit Going Public Server: in AMP die
  *Steam Workshop Items* leer lassen und nichts nach `bamp-mods-extra/` legen. Die eingebauten Module ersetzen
  Immersion, BAScheduler, SmartSuite und VoogleRoute - auch Spieler sollen diese Mods nicht gleichzeitig nutzen.
- **Linux / Steam Deck (Proton):** mit **GE-Proton** spielen (getestet: GE-Proton11-11, wie der Server; mit
  ProtonUp-Qt installieren, dann Eigenschaften → Kompatibilität). Mit Proton Experimental und Proton 11.0-2 stürzte
  das Spiel gelegentlich beim Laden der Welt ab. Unter Windows tritt das nicht auf.

### Spieler: einem Server beitreten

1. **Going Public Server** im Steam Workshop von Big Ambitions abonnieren und in der Mod-Liste des Spiels aktivieren.
2. **Mehrspieler → Spiel beitreten**: **Host-Adresse** = Adresse des Servers, **Server-Schlüssel** = der Schlüssel vom
   Server-Admin.
3. Dein Fortschritt bleibt auf dem Server. **Esc → Server verlassen** speichert ihn und bringt dich ins Hauptmenü.

### Eingebaut

- **Steam-Namen und Freunde**: Steam-Name über jedem Spieler, Freunde in Grün und auf der Stadtkarte,
  Freundschaftsanfragen im Handy (Business → Friends); dort stehen unter jedem Freund seine gemieteten und gekauften
  Gebäude.
- **Steuern per Telefon**: Kontakte → Internal Revenue Service → Anrufen.
- **F7 Ladenschilder**: eigene Bilder auf Schildern, Ladenschildern und Werbetafeln.
- **F6 RPG-Kamera** (Einstellungen F8): Kamera hinter der Figur, zu Fuß und im Auto.
- **Umschalt+F9 Planer**: prüft Schichtpläne, füllt sie mit der Funktion des Spiels und schlägt Öffnungszeiten nach
  Kundennachfrage vor.
- **Umschalt+F10 Auswertung**: Gewinn je Geschäft, Verkäufe, Personal, Immobilien und Warnungen.
- **Umschalt+F11 Navigation**: Ziel suchen oder auf der Stadtkarte anklicken, Routenlinie zu Fuß und mit dem Auto, auf
  Wunsch läuft die Figur selbst hin, im Auto fährt es hin und parkt. Ein kurzer Rechtsklick in die Welt (Karte zu)
  löscht die Route.
- Im Hauptmenü: der Ladestatus des Mods, diese Neuigkeiten und die Mod-Version neben der des Spiels.
- 23 Sprachen.

### Server-Betreiber: AMP

**Installation:** Konfiguration → Instance Deployment → Configuration Repositories:
`amaus1209/BigAmbitionsGoingPublicServer:main` eintragen, **Fetch latest**, eine Instanz aus der Vorlage
**Big Ambitions (Going Public server)** anlegen, **Update** (lädt das Spiel per SteamCMD mit einem Steam-Konto,
das Big Ambitions besitzt – bei Steam Guard fragt AMP nach dem Code –, Proton GE und das neueste Release von
hier), dann **Start**. Die Konsole zeigt `[BAMP-HEADLESS] READY`; die Statusseite zeigt Spiel-Port, Server-Passwort (den Server-Schlüssel) und Server-Version. Netzverkehr, Server-FPS und die Verbindung jedes Spielers stehen auf der Ranglisten-Seite (siehe unten).
Den Spiel-Port freigeben (UDP, standardmäßig 7777).

**Aktualisieren:** **Update** lädt das neueste Release, danach die Instanz neu starten. Die Spieler bekommen die passende
Version aus dem Steam Workshop. Nach einem Update den Server einmal starten: Neue Einstellungen bekommen dann ihren
Standardwert, in AMP gesetzte Werte gelten ab dem nächsten Start. **Stop** speichert zuerst die Welt und beendet den
Server dann (wenige Sekunden).

**Einstellungen** (Konfigurationsseite der Instanz):
- **Server-Sprache**: wählen, dann noch einmal **Update** – danach erscheinen die Einstellungen in dieser Sprache.
- **Server**: Welt, Spielstand einmal laden, Name der Server-Figur, Spieleranzahl, Pausieren (leer, untätig, alle im
  Pausenmenü), Server-Schlüssel, automatisches Speichern, **Bildrate im Betrieb** (60, 120 oder 200 FPS) und ohne
  Spieler sowie **Netzwerk-Takt** (Normal 10 / 5 Hz, Hoch 20 / 10 Hz, Sehr hoch 30 / 15 Hz, Maximal 60 / 30 Hz – wie
  oft Positionen, Autos und Verkehr über das Netz gehen; höher fährt sich flüssiger, und weil der Strom jedes Spielers
  an jeden anderen geht, wächst die Datenmenge mit mehr Spielern schnell).
- **Neue Welt**: Name und Schwierigkeit (Einfach, Normal, Schwer oder Benutzerdefiniert – die benutzerdefinierten Werte
  haben einen eigenen Reiter). Sie zählen nur, wenn der Server diese Welt anlegt (es gibt sie noch nicht).
- **Module**: jedes eingebaute Modul für alle an- oder ausschalten. **RPG-Kamera** und die Routenlinie der Navigation
  sind auf dem Server für alle Spieler gleich.
- **Spielregeln**: Miete aus eigenen Gebäuden, Gebäudepreise, Kreditrahmen der Bank und Geschäftsverkauf.

**Auf dem Host (Cron, siehe oben):** `bamp-web-sync.sh` stellt die Rangliste jeder Instanz unter
`https://<AMP-Adresse>/monitor/<Spiel-Port>/index.html` bereit (der Verbindungslink der Instanz) (fusionierte Firmen stehen als „fusioniert mit …“ dabei) und legt
die deutsche Übersetzung der AMP-Oberfläche ab: AMP einmal mit `https://<AMP-Adresse>/?lang=de` öffnen (pro Browser).
Unter der Rangliste zeigt **Server** die letzte Stunde: Netzverkehr rein und raus, Nachrichten, Server-FPS, Netzwerk-Takt,
gedrosselte Spieler sowie Ping, Paketverlust und Takt je Spieler (der Server schreibt das alle 15 s).
`backup-saves.sh --all` sichert die Mehrspieler-Spielstände aller Instanzen nach `~/backups/saves/` (14 pro Instanz).

**Übersetzungen anderer Mods selbst bearbeiten** (für die Mods, die Spieler selbst nutzen – der Server nutzt keine):
Der Server legt bei jedem Start unter `bigambitions/modtr-templates/ModTranslations/` (AMP-Dateimanager) für jede Mod
und Sprache eine fertige Datei an - aktuelle Übersetzung oder, wo es keine gibt, der englische Text. Nur den Text
rechts vom Doppelpunkt übersetzen, Schlüssel, Platzhalter (`{0}`, `{name}`) und Tags (`<b>`) unverändert lassen, dann
in denselben Pfad unter `ModTranslations/` hier hochladen. Die Action *Translations* prüft die Datei und erneuert
`index.json`; ein rotes X unter *Actions* nennt die Zeile mit dem Fehler.

### Herkunft

Inoffizieller Fork von [Going Public](https://github.com/Melaus123/BigAmbitionMulti) von Melaus (MIT-Lizenz). Die
eingebauten Module sind eigene Umsetzungen nach dem Vorbild dieser Workshop-Mods (Links oben unter *Credits*): Immersion -
RPG Camera von Chamiks, BAScheduler von Shanpapa, SmartSuite Empire Management (SmartInsights, SmartRent) von
ᗪEEᒍ Gᗩᑌᗪᗩᖇᗪ und Voogle Route mit LIB BA Player Location und LIB BA Unified UI von - Capitaine -. Vielen Dank an ihre
Autoren. Big Ambitions gehört Hovgaard Games; jeder Spieler und auch der Server brauchen eine eigene Lizenz des Spiels.
