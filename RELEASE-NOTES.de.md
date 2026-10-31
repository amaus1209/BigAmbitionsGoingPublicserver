## Going Public Server 1.0.15

**Server und Spieler brauchen beide 1.0.15.** Workshop-Mod und Server bitte zusammen aktualisieren.

### Linux / Proton
- **Behoben: „In BizMan öffnen“ zeigte die leere BizMan-Liste** statt der Gebäudeseite (Wohnung mieten, Gebäude kaufen …). Unter Wine/Proton schrieb Harmony die meisten Spiel-Umleitungen als kurzen Sprung über eine Zeigerzelle neben dem Spielcode, und diese Zellen blieben dort nicht heil. Der Aufruf einer umgeleiteten Methode scheiterte dann schon an ihrem ersten Befehl (`BizManBusiness.SetInitialTab`, `MyEmployeesMassActionsUI.UpdateMassActionDropdown`, `SaveGamePathHelper.CurrentVersionFolderPath`; der Crash-Dump zeigte einen Sprung in Daten). Unter Wine ist jetzt jede Umleitung ein eigenständiger Sprung; unter Windows bleibt alles gleich. Das Ladefenster zeigt die Zeile „Proton/Wine: Spiel-Umleitungen als eigenständige Sprünge geschrieben“.
- Absicherung: Scheitert `SetInitialTab` beim Öffnen eines Gebäudes in BizMan trotzdem, wählt der Mod den Start-Reiter nach der Regel des Spiels und öffnet die Gebäudeseite trotzdem.
- Die Umleitungs-Prüfung im Log (`[DetourCheck]`) nennt jetzt auch die Sprungarten und überwacht jede Zeigerzelle.

### Navigation
- Ein Ziel heißt jetzt wie seine Adresse („44 Second Street“) statt wie der Straßenschlüssel (`ba:street_secondstreet 44`).

### Aktualisieren
1. Spieler: das Workshop-Abo auf 1.0.15 aktualisieren.
2. Server: die Instanz in AMP **aktualisieren** (lädt dieses Release) und neu starten.
