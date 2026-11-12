# Going Public als Server (Headless, AMP auf Ubuntu)

Big Ambitions läuft ohne Fenster und ohne Grafikkarte (`-batchmode -nographics`) unter Proton auf dem Server. Going Public
hostet dabei selbst die Welt: Der Server lädt den neuesten Spielstand, nimmt Spieler an, pausiert die Welt, wenn niemand
online ist, und speichert beim Beenden. Gesteuert wird er über die AMP-Konsole.

> **Status: geschrieben, nicht kompiliert, nicht getestet.** Ob Big Ambitions unter Proton ohne Grafik überhaupt startet, ist
> **noch nicht bewiesen**. Das ist der erste Test unten. Erst wenn er klappt, lohnt sich alles Weitere.

## Was der Server-Modus macht

| Funktion | Wo |
|---|---|
| Erkennt `-batchmode` / `-bamp-headless` / `Headless=true` und schaltet den Dedicated-Host an | `src/HeadlessHost.cs`, `src/MPConfig.cs` |
| Lädt den **neuesten** Stand des eingestellten Spiels (manuell, Autosave, Beenden-Checkpoint, Mitternacht). Wenn das fehlschlägt, nimmt er den nächstälteren Stand | `src/DedicatedHost.cs` |
| Wird ein Ladevorgang abgelehnt, beendet er das Hosting und versucht es erneut, statt in der Lobby hängen zu bleiben | `src/MPCanvasUI.cs` |
| Konsolenzeilen `[BAMP-HEADLESS] READY / JOIN / LEAVE / STATE / HEARTBEAT / FATAL`, an denen AMP „bereit“ und die Spielerliste erkennt | `src/HeadlessHost.cs` |
| Befehle: `status`, `players` (mit Ping und Spielzeit), `save` (Hauptspielstand), `autosave`, `saves` (Liste), `load <#N|Name>` (lädt, Neustart), `say <Text>`, `notice <Text>` (Meldung auf dem Bildschirm aller Spieler), `msg <Spieler> <Text>` (privat), `kick <Spieler> [Grund]`, `ban <Spieler> [Grund]`, `unban <Name\|Key\|all>`, `banlist`, `chat [N]`, `stop` (sofort), `stop warn` (so stoppt AMP – `App.ExitString` –: sind Spieler da, werden sie `StopWarnSeconds` vorher gewarnt), `restart` (sofort), `restart <Minuten>` (angekündigt: Warnungen bei 5 min, 1 min, 30 s, 10 s), `restart cancel`; ein Name mit Leerzeichen in Anführungszeichen. Für die Admin-Seite (ADMIN-1): `admin players\|bans\|chat\|status <id>` → eine Zeile `DATA <id> <json>` | stdin oder `headless/command.txt` |
| `status.json` (alle 5 s) und `events.log` im Headless-Ordner | `src/HeadlessHost.cs` |
| Frame-Limit 30 fps (läuft), 10 fps (leer), ohne Limit beim Laden, Ton aus | `src/HeadlessHost.cs` |
| Keine Autosaves, solange die Welt leer pausiert (der letzte Abgang hat schon gespeichert) | `src/MPCanvasUI.cs` |
| Wächter: Laden > 15 min, Auto-Start unmöglich, Hauptschleife > 5 min tot oder Mod defekt → Prozess endet mit Fehlercode, AMP startet neu | `src/HeadlessHost.cs` |
| Einstellungen aus `bamp-server.json` neben dem Spiel (schreibt AMP aus seiner Einstellungsseite) | `src/MPConfig.cs` |

Exit-Codes: `0` stop · `10` restart · `2` Laden hängt · `3` kein Spielstand / Auto-Start gescheitert · `4` Hauptschleife steht ·
`5` Mod konnte nicht starten.

## Machbarkeitstest (zuerst, ohne AMP)

Auf dem Ubuntu-Server, als normaler Benutzer:

1. **Spiel laden** (Windows-Version, dein Steam-Konto):
   `steamcmd +@sSteamCmdForcePlatformType windows +force_install_dir /srv/ba/game +login DEIN_NAME +app_update 1331550 validate +quit`
2. **Proton GE** nach `/srv/ba/proton` entpacken (GitHub-Release von GloriousEggroll).
3. **Ohne Mod starten**, nur um zu sehen, ob das Spiel lebt:
   `STEAM_COMPAT_DATA_PATH=/srv/ba/compatdata STEAM_COMPAT_CLIENT_INSTALL_PATH=$HOME/.steam/steam /srv/ba/proton/proton runinprefix "/srv/ba/game/Big Ambitions.exe" -batchmode -nographics -logFile -`
   Gut: Logzeilen laufen weiter, das Hauptmenü wird erreicht, kein Absturz. Schlecht: Absturz, Meldungen über Steam oder Grafik.
4. **Mod einspielen**: den gebauten Ordner (als `GoingPublicServer`) nach
   `/srv/ba/compatdata/pfx/drive_c/users/steamuser/AppData/LocalLow/Hovgaard Games/Big Ambitions/ModsLocal/` kopieren.
5. **Spielstand hinlegen**: Den Multiplayer-Stand, den der Server hosten soll, einmal auf dem Windows-PC **mit dem Steam-Konto des
   Servers** anlegen und speichern. Den Ordner `BigAmbitionsMP` aus `%LocalAppData%Low\Hovgaard Games\Big Ambitions\` an dieselbe
   Stelle im Prefix kopieren. Die Server-Figur braucht einen eigenen Platz im Spielstand.
6. `cp headless/bamp-server.example.json /srv/ba/game/bamp-server.json`, `DedicatedSession` eintragen, dann
   `GAME_DIR=/srv/ba/game PROTON=/srv/ba/proton/proton ./headless/run-headless.sh`
7. Warten auf `[BAMP-HEADLESS] READY`. Dann vom Windows-PC per IP auf Port 7777 (UDP) beitreten, `status` eintippen, `stop`.

Was du mir von dem Test zurückschickst: die Konsolenausgabe von Schritt 3 und 6, `headless-data/events.log`, `status.json` und
das Game-Log (`.../Big Ambitions/Player.log` im Prefix).

### Das größte offene Risiko: Steam

Das Spiel nutzt die Steam-Client-Schnittstelle (Facepunch.Steamworks), nicht die Dedicated-Server-Schnittstelle. Ohne laufenden
und angemeldeten Steam-Client kann `SteamClient` ungültig sein. Ob Big Ambitions dann trotzdem startet, zeigt Schritt 3. Going
Public selbst kommt ohne Steam aus, wenn Spieler **per IP** beitreten. Steam-Einladungen und Workshop-Downloads funktionieren
dann aber nicht. Falls das Spiel ohne Steam nicht startet, ist Plan B ein Steam-Client unter Xvfb im selben Prefix. Das ist
aufwendiger und kommt erst nach diesem Test.

**Ergebnis unter AMP:** Das Spiel startet ohne Steam (`SteamApi_Init failed` wird nur geloggt), lädt aber **keine Mods**: Der
Mod-Loader (`HGModLoader`) wartet auf `SteamAPI.OnSteamInit`. Deshalb installiert `install-into-amp.sh --build` einen
**Server-Vorlader** (`headless/preloader/`, Unity Doorstop 4.3.0 als `winhttp.dll` neben der exe): Er startet den Mod-Loader
ohne Steam, nur bei Start mit `-bamp-config`. Spieler brauchen ihn nicht. Im Log: `[BAMP-Preloader] ...`, Details in
`1331550/bamp-preloader/bamp-preloader.log`. Ohne Steam kennt der Server keinen Steam-Namen: `PlayerId` setzen.

## AMP

Vorlage in `headless/amp/` (Aufbau wie die gepflegte Enshrouded-Vorlage aus CubeCoders/AMPTemplates: SteamCMD Windows-Download,
Proton GE, Start über `proton runinprefix`):

* `bigambitions.kvp`, `bigambitionsconfig.json`, `bigambitionsmetaconfig.json`, `bigambitionsports.json`, `bigambitionsupdates.json`
* Als eigenes Generic-Template einbinden: ADS liest Vorlagen **nur** aus eingetragenen Repositories (Configuration → Instance
  Deployment → Configuration Repositories, Format `owner/repo:branch`, Dateien im Wurzelverzeichnis, per `git clone` von
  github.com). Dateien, die man in einen fremden Repo-Ordner unter `DeploymentTemplates/` kopiert, löscht „Fetch latest“
  (`git clean`), `LOCAL…`-Ordner werden nicht eingelesen. Ist dieses Repo privat: die 5 Dateien in ein lokales Git-Repo legen
  und per `git config --global url.<pfad>.insteadOf https://github.com/<owner>/<name>.git` umleiten (ADS läuft als `amp`).
* Im Container gibt es kein Display: `bamp-start.sh` startet das Spiel deshalb unter `xvfb-run` (sonst „Failed to create
  batch mode window“).
* Steam-Login: Die Vorlage fragt nach deinem Konto, weil das Spiel gekauft sein muss (kein anonymer Download).
* Mod: Den gebauten Ordner nach `bigambitions/bamp-mod/GoingPublicServer/` hochladen und „Update“ ausführen. Die Vorlage kopiert ihn
  dann in den Prefix.
* Einstellungen (Spielstand, Server-Name, Port, Pausenzeit, fps) landen in `bamp-server.json`.
* Start über `bamp-start.sh`: legt beim ersten Start den Server-Schlüssel an (`telemetry-data/key.txt`, falls in den
  Einstellungen keiner steht), zeigt ihn auf der Statusseite und ersetzt sich dann durch das Spiel. AMP behält diese
  Werte, bis das Spiel endet. Eine später geöffnete Seite fragt sie neu an. Mit Websockets verpasst AMP 2.8 die Antwort,
  und die Verbindungsinfo bleibt leer. Abhilfe: In der Konfiguration der Instanz „Websockets aktivieren“ ausschalten.
  `headless/amp/install-into-amp.sh --build <Instanz>` baut die Mod auf dem Server und kopiert den Wrapper hinein.
* Stoppen: AMP schickt `stop` → Server speichert, meldet den Spielern das Ende und beendet sich. Unitys eigener Abbau
  hängt unter Proton (letzte Zeile „Input System module state changed to: Shutdown.“), und AMP 2.8 wartet dann 600 s
  (`App.ExitTimeout` gibt es dort nicht mehr). Deshalb beendet der Server seinen Prozess selbst, sobald gespeichert und die
  Mods entladen sind (Konsole: `[BAMP-HEADLESS] EXIT …`, `status.json`: `"state": "stopped"`). Bleibt der Prozess trotzdem,
  beendet `bamp-start.sh` ihn 30 s nach „stopped“ bzw. 180 s nach „stopping“.
* Erreicht AMP nie „Ready“, kommen die Konsolenzeilen nicht durch Proton an. Dann `-logFile -` an die Startargumente hängen. Der
  Zustand steht in jedem Fall in `bigambitions/headless/status.json`.

### AMP auf Deutsch

AMP hat eine eingebaute, nicht beworbene Übersetzung (`WebRoot/Scripts/Locale.js`): Sie lädt `WebRoot/Locale/<Sprache>.json`,
sobald ein Browser AMP einmal mit `?lang=<Sprache>` öffnet, und merkt sich die Wahl in diesem Browser.

* Datei: `headless/amp/amp-locale/de.json`. Grundlage ist die deutsche Datei von
  [CubeCoders/AMPTranslations](https://github.com/CubeCoders/AMPTranslations) (MIT, siehe `LICENSE-AMPTranslations.txt`),
  korrigiert und für AMP 2.8 ergänzt, dazu die Einstellungen dieser Vorlage (aus `i18n/de.json`).
* Einspielen: `bamp-web-sync.sh` (Cron, jede Minute) hält `<ADS>/WebRoot/Locale/de.json` aktuell, auch nach AMP-Updates.
* Einschalten: `https://<AMP-Adresse>/?lang=de` einmal öffnen, zurück mit `?lang=en`.
* Übersetzt werden Texte der Oberfläche, Menüs, Dialoge, Knöpfe und Namen und Beschreibungen der Einstellungen. Die
  Kategorie-Reiter der Einstellungen und die Einträge von Auswahllisten übersetzt AMP nicht, die bleiben englisch.
* AMP lässt Browser die Datei ein Jahr lang zwischenspeichern. Nach einer neuen Fassung den Browser-Cache leeren.
* Nach einem AMP-Update: `python3 tools/amp-locale-check.py` listet neue Texte ohne Übersetzung und prüft die Datei.

## Zusätzliche Mods auf dem Server

Workshop-Mods im **Steam-Workshop-Reiter der Instanz** in AMP suchen und installieren (AMP lädt sie per SteamCMD mit seinem
Steam-Login nach `bamp-mods-extra/`), oder Mod-Ordner per AMP-Dateimanager nach `bigambitions/bamp-mods-extra/` hochladen (ein Ordner je Mod, so wie er in `ModsLocal`
liegt). Beim Start kopiert `bamp-start.sh` sie in das `ModsLocal` des Servers und veröffentlicht alle zusätzlichen Mods als ZIP
unter `https://<AMP-Adresse>/monitor/<Server-Schlüssel>/mods/` (`mods.json` mit SHA-256). Ein Spieler, dem Mods fehlen, wird
abgewiesen, lädt sie dabei automatisch herunter (nur vom Server, dem er beitritt, nur mit dessen Schlüssel, über HTTPS – also
per Domain beitreten), und sieht, welche seiner Mods der Server nicht hat und deaktiviert werden müssen. Danach: Spiel neu
starten, erneut beitreten. Eigene Ordner gleichen Namens beim Spieler werden nie überschrieben.

Abgleich über die Workshop-Nummer: AMP legt jede Workshop-Mod als Ordner `<Workshop-Nummer>` ab. Mods mit DLL werden über
Assembly-Name und -Version verglichen, Mods ohne DLL über die Nummer (`ws:<Nummer>`) – egal ob sie beim Spieler aus dem
Workshop-Abo oder vom Server stammen. Wer eine Mod schon abonniert hat, bekommt sie nicht zusätzlich vom Server.

Blueprints bleiben beim Spieler: Das Spiel speichert eigene Blueprints in `…\Big Ambitions\Blueprints\` auf dem jeweiligen
PC; die Mod schickt sie nirgends hin, und beim Beitritt werden sie nicht verglichen. Workshop-Blueprints (`Layout.json`, keine
DLL) installiert der Server aus `bamp-mods-extra/` nicht und verteilt sie nicht.

## Wichtig für Mods

Der Server erzwingt **gleiche Mods für alle**. Jede zusätzliche Mod auf dem Server müssen alle Spieler ebenfalls haben, sonst
werden sie abgewiesen. Eine alte separate „BA Telemetry Bridge“ sollte nicht installiert sein (sie wäre eine weitere Mod, die
alle bräuchten). `build-nobara.sh` räumt sie automatisch weg.

## Einstellungen (`bamp-server.json`)

| Schlüssel | Standard | Bedeutung |
|---|---|---|
| `ServerLanguage` | en | Sprache der AMP-Felder und der Texte des Servers (Spieler sehen ihre eigene Spielsprache). AMP-Texte: `headless/amp/i18n/<sprache>.json`, erzeugt mit `install-into-amp.sh --lang xx` bzw. `localize-template.py` |
| `DedicatedSession` | leer = zuletzt gespielt | Name der Multiplayer-Welt; geladen wird ihr neuester Stand |
| `DedicatedLoadSave` | leer = neuester | Einmal diesen Stand laden statt des neuesten: Name wie `saves` ihn zeigt (`Test-auto-2`) oder `#N`. Gilt einmal pro Wert |
| `DedicatedNewWorld` | leer | „Neue Welt: Name“: Gibt es diese Welt noch nicht, legt der Server sie beim nächsten Start mit `DedicatedNewGame` an und spielt sie; danach ist sie eine normale Welt (AMP-Reiter New world) |
| `DedicatedNewGame` | leer = aus | `Easy`/`Normal`/`Hard`/`Custom`: Schwierigkeit einer neuen Welt (`DedicatedNewWorld`, oder `DedicatedSession`, solange es sie noch nicht gibt); Charakter = `PlayerId` |
| `NewGameDaysPerYear` … `NewGameResaleValue` | Normal-Werte | nur bei `DedicatedNewGame` = `Custom` (AMP-Reiter Custom): die Optionen von „Benutzerdefiniertes Spiel“ (Tage pro Jahr, Startkapital, Rivalenangriffe %, Startalter, Altern/Zufriedenheit/Energie aus, alle Kurse/Kontakte, Fahrzeugschäden/Kraftstoff aus, Steuersatz, Lohn %, öffentliche Preise %, Bankzinsen %, Kundenwerbung %, Dringlichkeitsgebühr Großhandel/Importeur %, Limits aus, alle Importprodukte, Exportpreise %, Wiederverkaufswert %); Prozent wie die Regler im Spiel, auf deren Bereiche begrenzt (`DedicatedHost.NewGameSettings`) |
| `PlayerId` | Steam-Name | Name der Server-Figur |
| `HostPort` | 7777 | UDP-Port |
| `DedicatedIdleGraceSeconds` | 10 | Sekunden ohne Spieler bis zur Pause |
| `DedicatedAfkMinutes` | 5 | Welt pausiert, solange kein verbundener Spieler so viele Minuten Maus/Tastatur benutzt hat (0 = aus); läuft nie während einer Zeitbeschleunigung |
| `DedicatedPauseInMenu` | true | Welt pausiert, solange jeder verbundene Spieler das Pausenmenü (Esc) offen hat |
| `DedicatedRequireKey` | true | Beitritt nur mit dem Server-Schlüssel (= `TelemetryKey` bzw. `telemetry-data/key.txt`); Spieler tragen ihn unter „Spiel beitreten“ im Feld „Server-Schlüssel“ ein |
| `StopWarnSeconds` | 60 | Stoppt, startet oder aktualisiert AMP den Server, während Spieler da sind, werden sie so viele Sekunden vorher gewarnt (bei 60, 30, 10 s), dann wird gespeichert; 0 = sofort, höchstens 90 (AMPs `App.ExitTimeout` ist 150) |
| `MergerAutoDissolveDays` | 0 | Fusion automatisch auflösen (MERGER-AUTO-DISSOLVE-1): Ein Mitglied, das so viele Tage nicht auf dem Server war (gezählt nur, solange der Server läuft), scheidet aus - ohne Abstimmung und Verkauf; alle behalten ihr Eigentum, das Geld der Firma wird gleichmäßig geteilt (sein Anteil wartet, bis es zurück ist), eine Fusion aus zwei endet. Nur solange ein anderes Mitglied da war. Die Mitglieder auf dem Server bekommen eine Meldung, das abwesende Mitglied bei seiner Rückkehr. `0` = nie |
| `SteamOwnerCheck` | true | Nur Spieler, deren Steam-Konto Big Ambitions besitzt (STEAM-OWNER-1): das Spiel schickt beim Beitreten ein Ticket von Steam (GetAuthTicketForWebApi, an den Server-Schlüssel gebunden), der Server lässt es von Steam prüfen (ISteamUserAuth/AuthenticateUserTicket); ein Steam-Emulator kann keines ausstellen, Familienbibliothek zählt. Wirkt nur mit `SteamWebApiKey`; antwortet Steam nicht, kommt der Spieler herein (Log-Warnung) |
| `SteamWebApiKey` | leer | Steam-Web-API-Schlüssel für die Besitzprüfung (steamcommunity.com/dev/apikey); bleibt auf dem Server, steht in keinem Log und keinem Fehlerbericht |
| `SteamFriendJoin` | true | Spieler melden Steam, auf welchem Server sie spielen (Rich Presence `connect` = `bamp:<Adresse>/<Port>[/<Schlüssel>]`, STEAM-FRIEND-JOIN-1): ihre Steam-Freunde sehen „Spiel beitreten“/„Zum Spiel einladen“ und landen mit einem Klick auf dem Server, mit Schlüssel. Nicht bei LAN-/localhost-Adressen |
| `DedicatedAutoAccept` | true | Beitritte ohne Bestätigung |
| `AutosaveMinutes` | 0 = wie Einzelspieler | Autosave-Takt, solange Spieler da sind |
| `HeadlessFps` / `HeadlessIdleFps` | 30 / 5 | Frame-Limit läuft / leer |
| `HeadlessLoadTimeoutSeconds` | 900 | Laden länger → Neustart (0 = aus) |
| `HeadlessStallSeconds` | 300 | Hauptschleife steht → Prozess beenden (0 = aus) |
| `HeadlessDisableCameras` | false | alle Kameras aus (nur für Xvfb ohne `-nographics`, riskant) |
| `ModuleIrsPhone` / `ModuleShopSigns` / `ModuleCamera` / `ModulePlanner` / `ModuleInsights` / `ModuleNavigation` / `ModulePhonePages` / `ModuleGoingOnline` | true | Module 1–8 (Steuern per Telefon, Ladenschilder F7, RPG-Kamera F6/F8, Planer Umschalt+F9, Auswertung Umschalt+F10, Navigation Umschalt+F11, Handy-Seiten, Kabelanbieter „Going Online“ Umschalt+F12) für alle Spieler an/aus |
| `CamFootPitch` … `CamIndoors`, `NavRouteLine` | wie im Spiel | Einstellungen der RPG-Kamera (Winkel/Abstand zu Fuß und im Auto, Zielhöhe, Schulter, Empfindlichkeit, Sichtfeld, Sichtweite, Totzone, Zurückschwenken, in Gebäuden) und der Routenlinie – auf dem Server für alle Spieler gleich; das F8-Fenster zeigt sie dort nur an |
| `NavPhone` | `true` | Navi im Handy (NAV-PHONE-1/2): ist ein Ziel gesetzt, füllt das Navi den Bildschirm des BizPhones (zugeklappt kein Navi, mit der Maus darüber scheinen die Apps durch); `false` = es steht über dem Handy – für alle Spieler des Servers |
| `RuleRentReturn` | 1 | SmartRent: Faktor auf die Miete aus eigenen Gebäuden (`RealEstate.DailyIncome`): 1, 1.5, 3, 6, 10, 18 |
| `RuleBuildingPrice` | 100 | SmartRent: Marktwert aller Gebäude in % (`Building.GetMarketValue`): 100, 75, 50, 35 – Kauf, Verkauf und Anzeige folgen ihm |
| `RuleBusinessSale` | false | SmartRent: Geschäftsverkauf in der Auswertung (Mietende wie in BizMan + Firmenwert von 30 Tagen Durchschnittsgewinn) |
| `RuleBusinessSaleMinDays` / `RuleBusinessSaleProfitDays` | 25 / 30 | Mindestalter in Tagen / Tage mit Gewinn vor einem Verkauf |
| `RuleLoanLimit` | 1 | SmartRent: Faktor auf den Vermögens- und Einkommensanteil des Kreditrahmens (`BankDialog.GetEconomyMaxLoanAmount`): 1, 2, 3, 5; das Limit jeder Bank bleibt |

Die Modul-Schalter und Regeln schickt der Server jedem Spieler beim Beitritt (`WorldSnapshotPayload.Rules`, `ServerRules.cs`);
im Einzelspieler sind alle Module an und die Regeln die des Spiels. Fehlende Schlüssel ergänzt `bamp-start.sh` beim Start mit dem
Standardwert aus `bamp-tools/bigambitionsconfig.json` – AMP trägt seine Werte nur in vorhandene Schlüssel ein, sie gelten also ab
dem nächsten Start.

Nur diese Schlüssel (und `DedicatedHost`, `TelemetryKey` (der Server-Schlüssel), `Headless`, `Port`, `AutoDownloadHostMods`, `RefuseModMismatch`, `HeadlessDir`) werden
aus der Datei gelesen. Andere ignoriert der Server mit Warnung. Die Werte werden nie in die normale Config geschrieben.

## Bekannte Grenzen

* Nichts davon ist kompiliert oder im Spiel gelaufen. Beim Bauen auf deinem Windows-PC können Tippfehler-Fehler kommen, die schicke mir.
* Die Server-Figur hat normale Bedürfnisse und steht sichtbar in der Welt (siehe `DEDICATED.md`).
* `kick` wirft nur hinaus – der Spieler darf sofort wieder beitreten, sein Spiel zeigt den Grund. `ban` sperrt ihn bis `unban` (der Grund steht in der Bannliste und wird ihm beim Beitreten gezeigt).
* Jede öffentliche Chat-Zeile kommt als `[BAMP-HEADLESS] CHAT Name: Text` in die Konsole (AMP erkennt sie über `Console.UserChatRegex`); AMPs Spielerliste hat die Knöpfe Kick und Ban (`Console.UserActions`).
* Ob die Konsoleneingabe unter Proton beim Spiel ankommt, ist offen. `command.txt` im Headless-Ordner funktioniert immer.
