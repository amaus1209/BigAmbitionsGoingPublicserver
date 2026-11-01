## Going Public Server 1.0.16.1

**Server und Spieler brauchen beide 1.0.16.1.** Workshop-Mod und Server bitte zusammen aktualisieren - der Server lässt nur dieselbe Version herein.

Korrekturen aus dem ersten Test von 1.0.16:

### Autopilot
- Hält an **roten Ampeln** - die Ampelfarbe der Kreuzung selbst, an der Haltelinie, bis Grün.
- Lenkt in Kurven **kräftig und im richtigen Moment** ein, statt sie zu schneiden, und wird vor Kurven langsamer.
- Fährt nicht mehr in **Hydranten** und andere stehende Dinge; steckt er fest, setzt er zurück.
- **Blinkt** vor dem Abbiegen, mit den Blinkern des Spiels.

### Mitfahrer
- Sehen die **Räder** des Autos drehen und einlenken und hören den **Motor**.
- Hören den **Radiosender des Fahrers** - der Fahrer hat Vorrang; nach dem Aussteigen läuft wieder der eigene Sender.

### Radio
- **Eigener Radio-Tab im Handy**: Im großen Handy-Menü gibt es jetzt „Radio“ mit allen Sendern zum Antippen: die Sender des Spiels, deine eigenen Streams und die Senderliste des Servers. Der laufende Sender ist markiert. Zu hören ist das Radio wie bisher im Auto oder mit Kopfhörern.
- **Senderliste des Servers von GitHub**: In AMP wählt der Server-Betreiber unter *Module* die **Radio-Senderliste** - die Sprache der Liste (Standard: Deutsch, „Off“ = keine). Alle Spieler des Servers bekommen dieselbe Liste aus dem Vorlagen-Repo (`radio/<Sprache>.txt`): für Deutsch 250 deutsche Sender (SWR3, Deutschlandfunk, Bayern 3, 1LIVE, NDR 2 ...), für 22 weitere Sprachen die beliebtesten Sender des jeweiligen Landes. Sie wird beim Spielstart geladen und für einen Start ohne Internet gespeichert. Wer die Datei auf GitHub ändert, ändert die Liste für alle - ohne neue Mod-Version.
- **Eigene Sender**: `streams.txt` im Radio-Ordner des Spiels (Windows: `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions\Radio\`), eine Zeile pro Sender „Name = Adresse“; .m3u- und .pls-Dateien im Ordner gehen auch. Sie stehen vor der Liste des Servers.
- **Nur https-Streams**: Das Spiel lädt nichts über unverschlüsseltes http; eine http-Adresse wird als https versucht.
- Im Auto geht „Nächster Sender“ durch die Sender des Spiels, dann durch die Streams.

### Welt mit Energie/Hunger aus
- Bleibt so, auch nach einem Neustart des Servers oder Wiederverbinden. Der Bedürfnis-Regler hatte sie wieder eingeschaltet; bestehende Welten werden beim nächsten Start repariert.

### Sitzen
- Ein **sitzender** Mitspieler auf Bank oder Stuhl wird bei den anderen auch **sitzend** gezeigt (sie sahen ihn auf seinem Platz stehen).
- Sitzt in deinem Spiel ein NPC auf genau diesem Platz, wird der NPC ausgeblendet, solange der Mitspieler dort sitzt. In Gebäuden, die keinem Spieler gehören, hat jedes Spiel seine eigenen NPCs.

### Fusion
- **Mitarbeiterliste**: Nach einem Verbindungsabbruch oder Server-Neustart bekam ein Fusions-Mitglied die nicht zugewiesenen Mitarbeiter seines Partners nicht wieder - die Liste blieb leer, Zuweisen ging nicht. Der Server schickt sie jetzt bei jedem Beitritt und jeder Änderung der Fusion mit.
- **Auflösen nur gemeinsam**: Bei zwei Mitgliedern heißt der Knopf unter *Business* jetzt **„Firma auflösen“**. Der andere bekommt einen Hinweis und den Knopf **„Auflösung zustimmen“**; aufgelöst wird erst, wenn alle zugestimmt haben und online sind. **„Antrag zurückziehen“** nimmt die eigene Stimme zurück.
- **Beim Auflösen wird alles verkauft**: jeder Laden (Einrichtung, Firmenautos und Kaution zurück, dazu ein Firmenwert von 30 Tagen Durchschnittsgewinn), jedes gekaufte Gebäude zum Marktwert, alle privaten Autos und alle Wohnungen; alle Mitarbeiter werden entlassen. Der Erlös geht aufs Firmenkonto, das danach zu gleichen Teilen geteilt wird.
- In einer Firma mit drei oder mehr Mitgliedern bleibt „Fusion verlassen“ ein normaler Austritt - die Firma besteht weiter.

### Server-Konsole
- **Ruhigere Konsole in AMP**: Bei „Log-Umfang: Normal“ fallen jetzt auch die regelmäßigen Diagnosezeilen weg (Netzwerk-Zählungen, Abgleich-Prüfungen, einzelne Kontobuchungen, Dateizeilen beim Speichern, Frame-Messungen). Die Zeile „[Server] Info“ kommt nur noch, wenn sich Port, Schlüssel oder Version ändern (vorher alle 30 Sekunden), und harmlose Grafik-Meldungen der Engine filtert AMP aus. Zusammen etwa 57 % weniger Zeilen; „Ausführlich“ zeigt weiter alles.
- **Keine falschen Ruckel-Warnungen mehr**: Im Leerlauf (keine Spieler oder alle im Pausenmenü) bremst der Server absichtlich auf „Bildrate im Leerlauf“. Die Frame-Messung hielt das für Ruckeln und schrieb alle 10 Sekunden „[Rhythm] OSCILLATING“ (mit 1.0.15 waren es 215.000 Zeilen in einer Nacht). Wenn Spieler da sind, läuft der Server mit voller Bildrate.

### Sonst
- Die Versionszeile unten links heißt „GPS-MM v. 1.0.16.1“.
- Öffnet ein Fusions-Mitglied „Meine Mitarbeiter“, steht eine Zeile `[MergerDiag]` im Player.log (für die Fehlersuche).
