## Going Public Server 1.0.16.2

**Server und Spieler brauchen beide 1.0.16.2.** Workshop-Mod und Server bitte zusammen aktualisieren - der Server lässt nur dieselbe Version herein.

Korrekturen nach dem Test von 1.0.16.1:

### Radio
- **Internetsender spielen jetzt**: In 1.0.16.1 blieb jeder Stream stumm, egal welcher Sender, auch im Auto. Die Spiel-Engine kann einen endlosen MP3-Stream nicht abspielen. Die Mod lädt und dekodiert die Streams jetzt selbst (mit NLayer, MIT-Lizenz, im Ordner `Dependencies/`).
- **http und https**: Beide Arten von Senderadressen spielen. Die Einschränkung „nur https“ aus 1.0.16.1 fällt weg.
- **Titelanzeige**: Unter dem Sendernamen steht das laufende Lied, wenn der Sender es mitschickt (im Radio-Tab, im Radio des Handys und am Lautsprecher).
- **Radio zu Fuß**: Im Radio-Tab gibt es den Schalter „Radio zu Fuß“. Ist er an, spielt das Radio auch außerhalb des Autos weiter, auch ohne Kopfhörer. In Gebäuden ist es still, dort spielen die Lautsprecher. Der Schalter gilt nur auf dem eigenen Rechner.
- **Laden-Lautsprecher**: Die Lautsprecher eines Ladens spielen auch Internetsender.
  - Im eigenen Laden zeigt der Radio-Tab oben „Mein Radio“ und „Laden-Lautsprecher“; „Laden-Lautsprecher“ ist vorgewählt. Einen Sender antippen, dann spielen ihn die Lautsprecher.
  - Am Lautsprecher selbst (Maus darauf) geht „Nächster Sender“ nach den Sendern des Spiels durch die Internetsender.
  - Alle Spieler im Laden hören denselben Sender, und der Server speichert ihn mit der Welt.
  - Wählen dürfen der Ladenbesitzer, seine Fusionspartner und wer seinen Business-Schlüssel hat.
- **Mitfahrer** hören den Sender des Fahrers, auch einen Internetsender. Nach dem Aussteigen läuft wieder ihr eigener Sender, auch ein Internetsender. Mit „Radio zu Fuß“ bleibt er zu hören.
- Die englische Senderliste hat TruckersFM dazubekommen.

### Server-Konsole
- **Serverschlüssel und Version** stehen wieder in AMP. Die Zeile kam bisher nur vor dem Laden der Welt, und AMP leerte die Anzeige, sobald die Welt fertig war. Jetzt kommt sie noch einmal, wenn die Welt läuft, und danach alle 10 Minuten.

### Sonst
- Die Versionszeile unten links heißt „GPS-MM v. 1.0.16.2“.
- Alles aus 1.0.16.1 ist enthalten (Autopilot, Mitfahrer, Radio-Tab, Sitzen, Fusion, ruhigere Konsole).
