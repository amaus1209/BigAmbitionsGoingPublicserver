## Going Public Server 1.0.13

**Server und Spieler brauchen beide 1.0.13.** Workshop-Mod und Server bitte zusammen aktualisieren.

### Behoben: Der Linux/Proton-Testschalter schaltete den Mehrspieler ab
- Mit der Testdatei `skip-tiny-patches.txt` neben `BigAmbitionsMP.dll` schlugen 41 Gruppen von Spiel-Hooks fehl. Der Mehrspieler wurde abgeschaltet, und ein Hinweis behauptete, „ein anderer installierter Mod“ sei inkompatibel. Jetzt tut die Datei nur noch, was sie soll: Die kleinsten Spielmethoden bleiben unangetastet, und der Mehrspieler bleibt an. Die Funktionen auf diesen Methoden sind aus, solange die Datei da ist.
- Der Hinweis nennt den eigenen Ordner des Mods in `ModsLocal` nicht mehr „einen anderen installierten Mod“.

### Neu: Ladestatus im Hauptmenü
- Beim Spielstart zeigt ein kleines Konsolenfenster unten im Hauptmenü Zeile für Zeile, was der Mod geladen hat. Für den Ordner, Harmony, die Spiel-Hooks und den Mehrspieler steht jeweils `[ OK ]`, `[WARN]` oder `[FAIL]`.
- Ist alles geladen, schließt es sich nach ein paar Sekunden selbst. Ist etwas fehlgeschlagen, bleibt es stehen, bis du draufklickst.
- Die Versionszeile im Hauptmenü zeigt jetzt auch die Version des Mods, z. B. `Going Public Server 1.0.13`.

### Neu: Neuigkeiten des Mods in der Neuigkeiten-Anzeige des Spiels
- Die Neuigkeiten-Anzeige im Hauptmenü zeigt zuerst die Neuigkeiten des Mods mit dem Mod-Bild. Die Neuigkeiten des Spiels folgen hinter den Pfeilen. Ein Klick auf eine Karte öffnet sie auf GitHub.
- Die Neuigkeiten kommen aus der README dieses Repositorys: Im Abschnitt **Neuigkeiten** (englisch: **News**) wird jede Überschrift darunter zu einer Karte. Für neue Neuigkeiten braucht es keine neue Mod-Version.

### Entfernt: Fehlermeldung des ursprünglichen Going Public
- **Report a Bug** im Mehrspieler-Menü, der lila Knopf in der oberen Leiste und der Chat-Befehl `/bug` sind weg. Ihre Berichte gingen an den Discord des ursprünglichen Going Public, nicht an den Admin dieses Servers. Es wird nichts mehr hochgeladen.

### Schon im letzten 1.0.12-Build, fehlte in dessen Notizen
- Der Server speichert nicht mehr, wenn ein Spieler beitritt oder ein Beitritt abbricht. Die automatische Speicherung bleibt, ebenso das Speichern, wenn ein Spieler das Spiel verlässt oder der Server stoppt.
- Erst speichern, dann beitreten: Vor jedem Beitritt speichert der Server die Welt und lässt den Spieler dann herein. Eine Speicherung wartet, solange ein Spieler in die Welt lädt.
- Beitreten mit einem Spielstand, der in einem Gebäude gespeichert wurde, schlug fehl: Die Lobby wartete, oder es hieß „Spielstand beschädigt“. Der Spieler startet jetzt vor dem Gebäude.
- Beim Beitritt nimmt der Server immer seine eigene Kopie des Spielstands, nie die lokale des Spielers.
- **Neue Welt: Name** in AMP legt eine neue Welt an (Einfach, Normal, Schwer oder Benutzerdefiniert). Die benutzerdefinierten Werte haben einen eigenen Reiter **Benutzerdefiniert**.
- Navigation: **Hinlaufen** schickt die Figur nicht mehr an die Stelle unter dem Knopf. Das gilt auch für jeden Knopf, der ein Mod-Fenster schließt.
- **Ein Neustart lädt den neuesten Spielstand**: Der erste Versuch beim Start schlug immer fehl, und der Server lud den zweitneuesten Spielstand. Jeder Neustart, auch jede geplante Sicherung, drehte die Welt so ein Stück zurück. „Spielstand einmal laden“ und der Konsolenbefehl `load` funktionieren jetzt ebenfalls.

### Aktualisieren
1. Spieler: das Workshop-Abo auf 1.0.13 aktualisieren.
2. Server: die Instanz in AMP **aktualisieren** (lädt dieses Release), danach neu starten.
