## Going Public Server 1.0.16

**Server und Spieler brauchen beide 1.0.16.** Workshop-Mod und Server bitte zusammen aktualisieren.

### Autos bleiben, wenn ihr Besitzer geht
- Auf einem Dedicated Server **bleiben die Autos eines Spielers in der Welt**, wenn er die Verbindung trennt. Alle sehen sie weiter, auch Spieler, die später beitreten, und auch nach einem Server-Neustart (eine Datei je Welt, `absent-cars.json` neben ihren Spielständen).
- Ein Auto, das sein Besitzer **auf der Fahrbahn** (er fuhr gerade, als er ging) oder **im Halteverbot** stehen ließ, wird in die nächste freie Parkbucht am Straßenrand gestellt. Es sind dieselben Plätze, die das Spiel Spielerautos gibt (nie ein Behindertenparkplatz).
- **Fährt** ein Freund gerade eines dieser Autos, wenn der Besitzer geht, fährt er zu Ende. Das Auto **bleibt** dort stehen, wo er aussteigt, statt zu verschwinden, und wird nach kurzer Zeit legal geparkt, falls es im Halteverbot steht.
- Kommt der Besitzer zurück, stellt sein eigenes Spiel seine echten Autos dorthin, wo die anderen sie gesehen haben, bevor seine Autos wieder gezeigt werden. Eine Meldung sagt ihm, wie viele umgeparkt wurden.
- Ein Bann entfernt die behaltenen Autos; eine neue Welt startet ohne sie.

### Navigation
- Ein **kurzer Rechtsklick in die Welt** löscht die Route (die der Navigation und das Ziel des Spiels). Das geht nur bei geschlossener Stadtkarte und nicht auf einem Gebäude oder Objekt (dort öffnet der Rechtsklick weiter dessen Optionen). In Menüs und im Inneneinrichter passiert nichts, und Gedrückthalten zum Drehen der Kamera zählt nicht.

### Verkehr
- Die **Räder der vorbeifahrenden Autos** drehen sich, die Vorderräder lenken in Kurven mit.

### Autopilot
- Im eigenen Auto mit gesetztem Ziel fährt **„Hinfahren“** oben im Bild (oder **„Ziel automatisch hinfahren“** in der Gebäude-Info der Karte) das Auto selbst zum Ziel: über die Straßenroute, etwa 40 km/h, in Kurven langsamer. Er setzt vor dem Abbiegen den **Blinker**, hält an **roten Ampeln** und bremst vor Autos, Fußgängern und Spielern.
- Am Ziel **parkt** er ein: mit dem Einparken des Spiels, wenn es dort einen Platz anbietet, sonst in der nächsten freien Parkbucht am Straßenrand.
- **Lenken, Gas oder Bremse übernimmt** sofort; „Autofahrt stoppen“ oben im Bild beendet ihn ebenfalls. Die Lenkhilfe des Spiels ist aus, solange er fährt.

### Navigation
- Fußwege zeigen, wo sie über die **Straße** führen (orange: Überwege, Stücke entlang der Fahrbahn) und wo über den **Fußweg** (blau); die Zeile oben zeigt die Legende, solange noch ein Straßenstück vor dir liegt.

### Steuerung
- Die Mod-Tasten - RPG-Kamera an/aus (F6), ihre Einstellungen (F8), Ladenschilder (F7), Planer (Umschalt+F9), Auswertung (Umschalt+F10), Navigation (Umschalt+F11) - lassen sich unter **Optionen, Steuerung** ändern (mit oder ohne Umschalt; Esc bricht ab; „Auf Standard zurücksetzen“ setzt sie mit zurück). Die Fenster zeigen die gewählte Taste.

### Verkehr
- **Flüssiger auf deinem Bildschirm**: Jedes Auto wird jetzt aus einem kurzen Verlauf der Server-Positionen abgespielt (mindestens 120 ms dahinter, egal welcher Netzwerk-Takt) statt genau einen Sendetakt dahinter. Bei „Maximal“ waren das 33 ms, schon kleine Netzschwankungen ließen Autos vorfahren und zurückspringen (ein Test-Log zählte etwa 2.500 solcher Sprünge bis 2 m) - das sah aus, als führen Autos ineinander.
- **Keine Auffahrunfälle mehr auf dem Server**: Die Verkehrs-KI bremste in Schlangen ab und zu zu spät (224 Berührungen in den Server-Logs, meist in Schlangen vor einer Kreuzung). Ein Verkehrsauto, das in weniger als 1,2 s auf den Vordermann in gleicher Richtung auffahren würde, wird jetzt genau so weit gebremst, dass es dahinter hält.

### Andere Spieler
- **Flüssigere Figuren**: Andere Spieler werden aus den zuletzt empfangenen Positionen gezeichnet, etwa 100 ms dahinter, zwischen den beiden um diesen Moment. Vorher lief die Figur der neuesten Position hinterher und rechnete 0,3 s voraus - nach einem verlorenen Paket (schwacher Router oder WLAN) stand sie kurz still und sprang dann.

### Verbindung
- **Keine Welt-Pause mehr bei Verbindungsabbruch** - neue AMP-Einstellung „Pause bei Verbindungsabbruch“: Aus (Standard; die anderen spielen weiter, die Autos des Abwesenden bleiben), Bis zu 60 Sekunden oder Bis der Spieler zurück ist (wie bisher).
- **Selbst wieder beitreten**: Ging der Server durch das Netz oder einen Neustart verloren, zählt das Hauptmenü herunter und tritt demselben Server wieder bei - bis zu fünf Versuche; „Abbrechen“ beendet es. Nie nach einem Kick oder Bann.
- **Netzwerk-Takt pro Spieler**: Der Server beobachtet die Verbindung jedes Spielers (Ping, Paketverlust) und schickt einer schwachen Verbindung weniger Verkehrs- und Positionsdaten; die anderen behalten den vollen Takt.
- **Server-Werte auf der Ranglisten-Seite** statt auf AMPs Statusseite (die wieder aufgeräumt ist): Netzverkehr rein und raus in einem Diagramm mit zwei Farben, Nachrichten pro Sekunde, Server-FPS, der Netzwerk-Takt, wie viele Spieler gedrosselt sind - und je Spieler Ping, Paketverlust und der Takt, den er bekommt. Die Maus (oder die Pfeiltasten) zeigt die Werte zu jedem Zeitpunkt der letzten Stunde; „Als Tabelle zeigen“ listet sie je Minute.
- **Versionshinweis**: Ein Spieler mit anderer Mod-Version bekommt im Chat den Hinweis zu aktualisieren; mit der neuen AMP-Einstellung „Nur dieselbe Mod-Version“ wird er mit einer Meldung abgewiesen, die beide Versionen nennt.
- **Behoben: Die AMP-Einstellung „Netzwerk-Takt“ wurde ignoriert** - seit 1.0.14 (das Server-Log meldete „key 'NetRate' is not a server setting“); jetzt greift sie.

### Navigation und Autopilot
- **Kürzeste Route**: Die Autoroute zielte auf den einen Fahrspurpunkt, der dem Ziel am nächsten liegt - oft auf der anderen Straßenseite, dann ging es einmal um den Block. Jetzt zählen alle Fahrspuren um Start und Ziel, und der kürzeste Gesamtweg gewinnt.
- **Abbiegehinweise** in der Zeile oben: „Rechts abbiegen in 80 m“, „Jetzt links abbiegen“.
- Der Autopilot hält das **Tempolimit** jeder Straße (aus den Verkehrsdaten), fährt **in ein Parkhaus** (Navigationsfenster, Meine Orte, „Parkhaus …“) und kann **einem Freund folgen** („… folgen“ unter Meine Orte: Das Ziel wandert mit ihm, das Auto bleibt etwa 10 m dahinter).

### Außerdem
- Autos abwesender Spieler: Schild „… (offline)“ und ein graues Autosymbol auf der Stadtkarte.
- **Kürzeres Warten beim Beitreten**: Der erste Gebäude-Abgleich zeichnete alle 885 Ladenschilder in einem einzigen Frame neu (1,6 s); die Schilder kommen jetzt über die nächsten Frames, die nahen zuerst. Ein Hänger von 120-160 ms bei jedem Rivalen-Update (eine Suche durch die ganze Stadt nach der Rangliste) ist weg.
- **AMP-Einstellung „Log-Umfang“**: Normal lässt die häufigen Statuszeilen aus der Konsole (eine Stunde mit einem Spieler waren 15.000 Zeilen); Ausführlich zeigt alles.
- Eine **Testliste** für diese Version: TESTLISTE-1.0.16.md im Repository.

### Aktualisieren
1. Spieler: das Workshop-Abo auf 1.0.16 aktualisieren.
2. AMP (ADS): **Configuration → Instance Deployment → Fetch latest**, dann die Instanz upgraden - für die neuen Einstellungen, und die Messwert-Kästen verschwinden von der Statusseite.
3. Server: die Instanz in AMP **aktualisieren** (lädt dieses Release) und neu starten.
