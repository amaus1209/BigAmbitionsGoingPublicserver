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

### Going Public Server 1.0.16.3
- **Steam: only owners of the game** (AMP switch, with a Steam Web API key): the server lets in only players whose Steam account owns Big Ambitions - it has Steam confirm a ticket each player's game sends when joining, and a Steam emulator cannot make one. Family Sharing counts.
- **Steam: friends can join**: while you play on a server, your Steam friends see "Join Game" on you (and you can invite them) and land on the same server with one click - address and server key included. The admin can switch it off in AMP.
- **AMP switch "Navigation: sat-nav in the phone"**: whether the sat-nav fills the BizPhone while the self-driving drives (off = it stays on top of the phone).
- **Admin page** on the ranking site (sign-in with the AMP account): players online with message, kick and ban (with a reason), the chat to read and write, the ban list with unban, announcements on every player's screen, save, and a restart now or announced in a few minutes (the players are warned). `kick` now only puts a player out; the chat shows in AMP's console, and AMP's player list has Kick and Ban.

### Going Public Server 1.0.16.2
- **Internet radio plays**: in 1.0.16.1 every stream stayed silent (the game engine cannot play an endless MP3 stream) - the mod now decodes the streams itself. http and https stations; the song that plays shows under the station's name; an "Alphabetical" switch sorts the stations by name.
- **Radio on foot**: a switch in the phone's Radio tab - the radio keeps playing out of the car, also without headphones; inside buildings it is silent (the loudspeakers play there).
- **Shop loudspeakers** play internet stations too: in your shop, the Radio tab picks for "Shop loudspeakers", and a loudspeaker's "Next station" goes on into the internet stations. Everybody in the shop hears the same station; the server keeps it with the world.
- **Radio tab**: favourites with a heart (they stand at the top, and "next station" goes through them), a search field, a clear word when a station sends AAC or HLS instead of MP3.
- **Passengers** hear the driver's station (internet stations too); after getting out their own radio plays again.
- **Navigation device**: with a destination set, a small sat-nav in the game's own look on top of the BizPhone (filling its screen while the self-driving drives or you walk there automatically) shows the next turn, the streets around you with the route and the distance left (the map shows the whole route). Routes change lanes where the game's lanes are not linked (no more driving to the end of the road before turning, as at the harbour), turn from the outer lane and keep right. A drive shows in blue, a walk in orange - on the map and in the sat-nav.
- AMP shows the server key and version again (they vanished from the console once the world was up).

### Going Public Server 1.0.16.1
- **Self-driving** stops at red lights, steers properly into turns, no longer drives into hydrants and blinks.
- **Passengers** see the wheels turn and steer and hear the engine and the driver's radio.
- **Radio**: a Radio tab in the phone's menu to pick the station. The server's station list comes from GitHub in the language chosen in AMP (250 German stations; the most popular stations of the country for more languages); https streams only; your own stations in `streams.txt`.
- A world with energy/hunger off stays that way after a restart or a reconnect.
- A sitting player is shown sitting to the others; an NPC on his seat is hidden meanwhile.
- **Merger**: dissolving needs every member's agreement and sells everything first (shops with goodwill, bought buildings at market value, cars and homes; the staff are let go) - then the money is shared equally. A merged member gets the partner's staff list back after a reconnect.
- The version line at the bottom left reads "GPS-MM v. 1.0.16.1".

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
- **Warning before AMP stops the server** (on the Server tab): when AMP stops, restarts or updates the server while
  players are on, they are warned first (default 60 s, at 60, 30 and 10 s), then the world is saved. Announcements and a
  longer announced restart from AMP itself: in the console `notice <text>` and `restart <minutes>` (`restart cancel`), or
  as a task "Send console message" with those commands in the instance's **Schedule** (e.g. every day at 4:00
  `restart 5`).
- **Steam** (on the Server tab): **only players who own the game** - needs a **Steam Web API key** from
  steamcommunity.com/dev/apikey (any Steam account that has bought something can make one; it stays on the server) -
  and **friends can join** (the players' Steam friends see Join Game).
- **New world**: name and difficulty (Easy, Normal, Hard or Custom - the Custom values have their own tab). They only
  count when the server creates that world (it does not exist yet).
- **Modules**: switch each built-in module on or off for everybody. **RPG camera**, the navigation's route line and
  whether the sat-nav goes into the phone (while self-driving or walking there by itself) are the same for every player
  on the server. **Radio station list**: the language of the radio stations every player gets
  (the files in `radio/` of this repository - edit one to change that list for everybody; Off = none).
- **Game rules**: rent from owned buildings, building prices, bank loan limit and business sale.

### On the host: ranking page, save backups, AMP in German

Instances run in containers and cannot reach AMP's web folder, so small jobs run on the host, as the user AMP runs as
(`crontab -e`; the scripts come with every instance in `bigambitions/bamp-tools/`):

```
* * * * *  ~/.ampdata/instances/<instance>/bigambitions/bamp-tools/bamp-web-sync.sh
17 4 * * * ~/.ampdata/instances/<instance>/bigambitions/bamp-tools/backup-saves.sh --all >> ~/backups/saves/backup.log 2>&1
```

- **Admin page**: `https://<your AMP address>/monitor/<game port>/index.html#admin` - in AMP on the instance's status
  page as "Admin Seite" under the Connection Link, on the ranking page as *Admin* at the top right and below the table -
  sign in with your AMP account; in AMP it needs the right to use that instance's console. Players online
  (message, kick, ban with a reason), the chat (read along, write as the server or to one player), the ban list
  (unban, unban everyone), save and restart. In AMP itself the chat shows in the console, and the player list has Kick
  and Ban.
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

#### Going Public Server 1.0.16.3
- **Steam: nur mit gekauftem Spiel** (AMP-Schalter, mit Steam-Web-API-Schlüssel): Der Server lässt nur Spieler herein, deren Steam-Konto Big Ambitions besitzt – er lässt sich von Steam ein Ticket bestätigen, das das Spiel jedes Spielers beim Beitreten mitschickt; ein Steam-Emulator kann keines ausstellen. Die Familienbibliothek zählt.
- **Steam: Freunde können beitreten**: Spielst du auf einem Server, sehen deine Steam-Freunde bei dir „Spiel beitreten“ (und du kannst sie einladen) und landen mit einem Klick auf demselben Server – mit Adresse und Server-Schlüssel. Der Admin kann es in AMP abschalten.
- **AMP-Schalter „Navigation: Navi im Handy“**: ob das Navi beim Autopiloten den BizPhone-Bildschirm füllt (aus = es bleibt über dem Handy).
- **Admin-Seite** auf der Rangliste (Anmeldung mit dem AMP-Konto): Spieler online mit Nachricht, Kicken und Bannen (mit Grund), der Chat zum Mitlesen und Schreiben, die Bannliste mit Entbannen, Ankündigungen auf dem Bildschirm aller Spieler, Speichern und ein Neustart sofort oder in ein paar Minuten angekündigt (die Spieler werden gewarnt). `kick` wirft jetzt nur noch hinaus; der Chat steht in der AMP-Konsole, und AMPs Spielerliste hat die Knöpfe Kick und Ban.

#### Going Public Server 1.0.16.2
- **Internetradio spielt**: In 1.0.16.1 blieb jeder Stream stumm (die Spiel-Engine kann keinen endlosen MP3-Stream abspielen) - die Mod dekodiert die Streams jetzt selbst. http- und https-Sender; unter dem Sendernamen steht das laufende Lied; der Schalter „Alphabetisch“ sortiert die Sender nach Namen.
- **Radio zu Fuß**: ein Schalter im Radio-Tab des Handys - das Radio spielt auch außerhalb des Autos weiter, auch ohne Kopfhörer; in Gebäuden ist es still (dort spielen die Lautsprecher).
- **Laden-Lautsprecher** spielen auch Internetsender: Im eigenen Laden wählt der Radio-Tab für „Laden-Lautsprecher“, und „Nächster Sender“ am Lautsprecher geht nach den Sendern des Spiels durch die Internetsender. Alle im Laden hören denselben Sender; der Server merkt ihn sich mit der Welt.
- **Radio-Tab**: Favoriten mit Herz (stehen oben, „Nächster Sender“ geht durch sie), ein Suchfeld, eine klare Meldung, wenn ein Sender AAC oder HLS statt MP3 sendet.
- **Mitfahrer** hören den Sender des Fahrers (auch Internetsender); nach dem Aussteigen läuft wieder ihr eigenes Radio.
- **Navigationsgerät**: Ist ein Ziel gesetzt, zeigt ein kleines Navi im Stil des Spiels über dem BizPhone (beim Autopiloten und beim automatischen Hinlaufen füllt es den BizPhone-Bildschirm) die nächste Abbiegung, die Straßen um dich herum mit der Route und die Restentfernung (die Karte zeigt die ganze Route). Die Route wechselt die Spur auch dort, wo die Spuren im Spiel nicht verbunden sind (kein Fahren mehr bis ans Straßenende vor dem Abbiegen, wie am Hafen), biegt von der äußeren Spur ab und hält sich rechts. Mit dem Auto ist die Route blau, zu Fuß orange - auf der Karte und im Navi.
- AMP zeigt Serverschlüssel und Version wieder (sie verschwanden aus der Konsole, sobald die Welt geladen war).

#### Going Public Server 1.0.16.1
- Der **Autopilot** hält an Rot, lenkt sauber in Kurven ein, fährt nicht mehr in Hydranten und blinkt.
- **Mitfahrer** sehen die Räder drehen und einlenken und hören den Motor und das Radio des Fahrers.
- **Radio**: eigener Radio-Tab im Handy-Menü zur Senderwahl. Die Senderliste des Servers kommt von GitHub in der Sprache, die in AMP gewählt ist (250 deutsche Sender; für weitere Sprachen die beliebtesten Sender des Landes); nur https-Streams; eigene Sender in `streams.txt`.
- Eine Welt mit Energie/Hunger aus bleibt so, auch nach einem Neustart oder Wiederverbinden.
- Ein sitzender Mitspieler wird bei den anderen sitzend gezeigt; ein NPC auf seinem Platz wird solange ausgeblendet.
- **Fusion**: Auflösen geht nur, wenn alle Mitglieder zustimmen, und verkauft vorher alles (Läden mit Firmenwert, gekaufte Gebäude zum Marktwert, Autos und Wohnungen; die Mitarbeiter werden entlassen) - danach wird das Geld zu gleichen Teilen geteilt. Ein Fusions-Mitglied bekommt nach einem Verbindungsabbruch die Mitarbeiterliste des Partners wieder.
- Die Versionszeile unten links heißt „GPS-MM v. 1.0.16.1“.

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
- **Vorwarnung, bevor AMP den Server stoppt** (Reiter Server): Stoppt, startet oder aktualisiert AMP den Server, während
  Spieler da sind, werden sie erst gewarnt (Standard 60 s, bei 60, 30 und 10 s), dann wird gespeichert. Ankündigungen und
  einen länger angekündigten Neustart aus AMP selbst: in der Konsole `notice <Text>` und `restart <Minuten>`
  (`restart cancel`), oder im **Zeitplan** der Instanz als Aufgabe „Konsolennachricht senden“ mit diesen Befehlen (z. B.
  täglich um 4:00 `restart 5`).
- **Steam** (Reiter Server): **nur mit gekauftem Spiel** – braucht einen **Steam-Web-API-Schlüssel** von
  steamcommunity.com/dev/apikey (jedes Steam-Konto mit einem Kauf kann einen erstellen; er bleibt auf dem Server) –
  und **Freunde können beitreten** (die Steam-Freunde der Spieler sehen „Spiel beitreten“).
- **Neue Welt**: Name und Schwierigkeit (Einfach, Normal, Schwer oder Benutzerdefiniert – die benutzerdefinierten Werte
  haben einen eigenen Reiter). Sie zählen nur, wenn der Server diese Welt anlegt (es gibt sie noch nicht).
- **Module**: jedes eingebaute Modul für alle an- oder ausschalten. **RPG-Kamera**, die Routenlinie der Navigation und
  ob das Navi beim Autopiloten oder automatischen Hinlaufen ins Handy geht, sind auf dem Server für alle Spieler gleich. **Radio-Senderliste**: die Sprache der Radiosender, die alle Spieler
  bekommen (die Dateien in `radio/` dieses Repos - wer eine ändert, ändert die Liste für alle; Off = keine).
- **Spielregeln**: Miete aus eigenen Gebäuden, Gebäudepreise, Kreditrahmen der Bank und Geschäftsverkauf.

**Admin-Seite:** `https://<AMP-Adresse>/monitor/<Spiel-Port>/index.html#admin` – in AMP auf der Statusseite der Instanz
als „Admin-Seite“ unter dem Verbindungslink, auf der Rangliste oben rechts („Admin“) und unter der Tabelle – Anmeldung mit dem AMP-Konto; es braucht in AMP das Recht, die Konsole dieser Instanz zu benutzen. Spieler
online (Nachricht, Kicken, Bannen mit Grund), der Chat (mitlesen, als Server oder an einen Spieler schreiben), die
Bannliste (Entbannen, alle entbannen), Speichern und Neustart. In AMP selbst steht der Chat in der Konsole, und die
Spielerliste hat die Knöpfe Kick und Ban.

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




Going Public Server - Windows
=============================
Big-Ambitions-Mehrspieler-Server mit Weboberfläche, ohne AMP. (English below.)

START
  1. Den Ordner GoingPublicServer an einen festen Ort entpacken, z. B. C:\GoingPublicServer (etwa 10 GB frei).
  2. GoingPublicServer.exe starten. Windows-SmartScreen: "Weitere Informationen" -> "Trotzdem ausführen".
     Die Windows-Firewall fragt nach dem Netzwerkzugriff: zulassen.
  3. Beim ersten Start erscheint ein FENSTER mit den Zugangsdaten zur Weboberfläche (Adresse, Benutzer, Passwort).
     Das Passwort wird nur dieses eine Mal angezeigt.
  4. Im Browser http://localhost:8080 öffnen und anmelden.
  5. Installation: das Spiel laden - mit dem Steam-Konto, das Big Ambitions besitzt (bei Steam Guard den Code eingeben).
  6. Einstellungen: Welt wählen oder unter "New world" eine neue anlegen. Übersicht: Starten.
  7. Im Router UDP 7777 (Spiel) zu diesem PC weiterleiten. Spieler treten mit deiner Adresse, Port 7777 und dem
     Server-Schlüssel bei. TCP 8080 (Weboberfläche) nur freigeben, wenn du sie von außen brauchst.

GUT ZU WISSEN
  - Das schwarze Fenster ist die Verwaltung. Schließen stoppt den Server (er speichert und beendet sich).
  - Passwort vergessen: Reset-Password.bat starten - das neue Passwort erscheint wieder in einem Fenster.
  - Der Server läuft ohne Steam-Client; ein Steam, das auf diesem PC läuft, bleibt dein eigenes.
  - Mods des Servers: server\mods (nur Going Public Server und was du dazulegst - getrennt von deinen eigenen Mods).
  - Spielstände: Windows legt sie für jedes Big Ambitions dieses Windows-Kontos in
    %USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions ab, die Server-Welten unter SaveGames\_BAMP_MP.
    Spielst du selbst auf demselben Windows-Konto, teilen sich Spiel und Server diesen Ordner - am saubersten läuft
    der Server auf einem eigenen PC oder unter einem eigenen Windows-Konto.
  - Update: das neue Paket über diesen Ordner entpacken (GoingPublicServer.exe vorher beenden). Server-Dateien allein:
    Weboberfläche -> Installation -> "Von GitHub aktualisieren".

-------------------------------------------------------------------------------------------------------------------------

START: unpack the folder (e.g. C:\GoingPublicServer, about 10 GB free), run GoingPublicServer.exe (SmartScreen: More
info -> Run anyway; allow the firewall). The ACCESS DATA appear in a WINDOW at the first start - only once. Open
http://localhost:8080, log in; Installation: download the game with the Steam account that owns Big Ambitions;
Settings: choose or create a world; Overview: Start. Forward UDP 7777 to this PC.
Closing the black window stops the server (it saves). Forgot the password: run Reset-Password.bat.


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

Going Public Server - Linux
===========================
Big-Ambitions-Mehrspieler-Server mit Weboberfläche, ohne AMP. (English below.)

INSTALLATION (Debian / Ubuntu, 64 Bit, systemd)
  tar -xzf GoingPublicServer-*-Linux.tar.gz
  cd GoingPublicServer-*-Linux
  sudo ./install.sh                 Optionen: --port 8080  --lang de|en  --data /var/lib/goingpublicserver

  Das Skript installiert die Pakete (Bibliotheken für Proton GE, Xvfb, python3, 32-Bit-Teile für SteamCMD), den
  Benutzer "goingpublic", das Programm nach /opt/goingpublicserver und den Dienst "goingpublicserver".
  Am Ende stehen die ZUGANGSDATEN zur Weboberfläche im Terminal - nur dieses eine Mal.

DANACH (im Browser, http://<Server-IP>:8080)
  1. Anmelden.
  2. Installation: Spiel laden - mit dem Steam-Konto, das Big Ambitions besitzt (bei Steam Guard den Code eingeben).
  3. Installation: Proton GE installieren (führt das Windows-Spiel aus).
  4. Einstellungen: Welt wählen oder unter "New world" eine neue anlegen.
  5. Übersicht: Starten. Spieler treten mit Adresse, Port 7777 und dem Server-Schlüssel bei.
  Freigeben: UDP 7777 (Spiel). TCP 8080 (Weboberfläche) nur, wenn nötig - von außen besser hinter HTTPS/VPN.

BEFEHLE
  systemctl status goingpublicserver        Zustand        journalctl -u goingpublicserver -f   Log
  sudo goingpublicserver reset-password     neues Passwort (erscheint im Terminal)
  sudo ./install.sh                         Update (Programm und Server-Dateien; Daten bleiben)
  sudo ./install.sh --uninstall [--purge]   entfernen (--purge: auch Spiel, Spielstände, Backups)

ORDNER
  /opt/goingpublicserver          Programm
  /var/lib/goingpublicserver      Daten: server/1331550 (Spiel), server/mods (Mods), server/prefix (Spielstände unter
                                  ...LocalLow/Hovgaard Games/Big Ambitions/SaveGames/_BAMP_MP), backups/, manager/

-------------------------------------------------------------------------------------------------------------------------

INSTALL (Debian / Ubuntu, 64 bit, systemd)
  sudo ./install.sh  [--port 8080] [--lang de|en] [--data DIR]
  The ACCESS DATA of the web interface appear at the end, in this terminal - only once.
THEN, in the browser (http://<server IP>:8080): log in; Installation: download the game with the Steam account that
  owns Big Ambitions, install Proton GE; Settings: choose or create a world; Overview: Start. Open UDP 7777.
COMMANDS: systemctl status goingpublicserver · journalctl -u goingpublicserver -f ·
  sudo goingpublicserver reset-password · sudo ./install.sh (update) · sudo ./install.sh --uninstall [--purge]


