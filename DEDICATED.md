# Dedicated Host für Going Public (BigAmbitionsMP)

Ein Dauer-Host: Auf einem Rechner läuft ein normales Big-Ambitions-Spiel mit dieser Mod. Dessen Spielfigur ist der
"Server-NPC". Die Welt läuft nur, solange mindestens ein anderer Spieler da ist, und pausiert (mit Speicherstand), wenn
der letzte geht. Alle Spieler müssen dieselben Mods haben.

> Status: geschrieben, aber **nicht kompiliert und nicht im Spiel getestet**. Beim Schreiben standen weder die
> Spiel-DLLs noch ein C#-Compiler zur Verfügung. Geprüft wurde nur, dass jede benutzte Mod-Funktion existiert und die
> Klammern in allen geänderten Dateien ausgeglichen sind.

## Was neu ist

| Funktion | Datei |
|---|---|
| Idle-Pause (kein Spieler da → Welt pausiert, sobald einer kommt → läuft weiter) | `src/DedicatedHost.cs`, `src/MPServer.cs` |
| Beitritte werden automatisch angenommen (kein Popup nötig) | `src/DedicatedHost.cs` |
| Pause nach Verbindungsabbruch blockiert die übrigen Spieler nicht mehr | `src/DedicatedHost.cs` |
| Auto-Start: lädt einen gespeicherten Spielstand selbst aus dem Hauptmenü | `src/DedicatedHost.cs`, `src/MPCanvasUI.cs` |
| Alle Spieler mit gleichen Mods: "Different mods: Refuse" ist erzwungen | `src/MPConfig.cs` |
| Client lädt fehlende Workshop-Mods des Hosts herunter (Opt-in) | `src/ModAutoDownload.cs`, `src/MPClient.cs`, `src/MPContentFingerprint.cs` |

## Einrichtung (Host)

In der JSON-Config im Ordner `%LocalAppData%Low\Hovgaard Games\Big Ambitions\BigAmbitionsMP\`
(Datei `BigAmbitionsMP.cfg.<...>.json`), bei geschlossenem Spiel:

```json
{
  "DedicatedHost": "true",
  "DedicatedSession": "NameDesSpielstands",
  "DedicatedIdleGraceSeconds": "10",
  "DedicatedAutoAccept": "true",
  "HostPort": "7777"
}
```

* `DedicatedSession` ist der Name des Multiplayer-Spielstands. Einmal von Hand hosten und speichern, dann den Namen eintragen.
* Port `HostPort` (UDP) am Router/Firewall freigeben, wenn Spieler per IP beitreten sollen. Über Steam geht es ohne Freigabe.
* Der Rechner braucht das vollständige Spiel mit Steam-Login, die Mod und die Workshop-Mods der Gruppe.

## Einrichtung (Client)

Beim Beitritt zu einem Host mit abweichenden Mods wird man abgewiesen. Der Host nennt dabei seine Workshop-Mods. Damit
der Client sie automatisch abonniert und lädt, in **seiner** Config:

```json
{ "AutoDownloadHostMods": "true" }
```

Danach: Big Ambitions neu starten, die Mods unter "Mods" aktivieren, erneut beitreten.
**Ein laufendes Spiel kann Mods nicht nachladen**, das lässt sich per Code nicht umgehen.

Sicherheit: Workshop-Mods sind Code, der auf dem Rechner des Spielers läuft. Deshalb ist der Download **aus**, solange der
Spieler ihn nicht selbst einschaltet. Er ist auf 12 Items pro Abweisung begrenzt und abonniert nur, er aktiviert nichts.

## Bekannte Grenzen

* **Kein echter Server ohne Grafik.** Der Host ist ein vollständiger Spiel-Client und braucht Grafikfunktionen. Ob er unter
  AMP (Linux/Proton, virtuelles Display) läuft, ist ungetestet. Die Release-Notes der Mod erwähnen zwar korrigiertes Hosting
  unter Linux/Proton (Steam Deck), das ist aber kein Betrieb ohne Bildschirm.
* **Die Host-Figur hat normale Bedürfnisse.** Hunger und Energie sind eine Welt-Einstellung ("Needs drain"), keine
  Einstellung pro Figur. Mit Wert 0 sind sie für alle aus, das betrifft dann auch die Spieler. Die Figur bleibt, wo sie steht,
  und ist für die anderen sichtbar. Dass sie über sehr lange Laufzeiten altert, ist nicht abgefangen.
* **Der Mods-Abgleich** verweigert nur, wenn die Mod-Liste des Hosts als verlässlich gilt (nach abgeschlossenem Scan). Sonst
  lässt der Host den Spieler wie bisher mit Warnung herein. Das ist das vorhandene Verhalten der Mod.
* **Die Facepunch-Steamworks-Aufrufe** in `ModAutoDownload.cs` (`Item.GetAsync`, `Subscribe`, `Download`, `IsInstalled`, ...)
  sind nach der bekannten API geschrieben und ungeprüft gegen die Version in deinem Spielordner.

## Bauen

Wie die Original-Mod (`BigAmbitionsMP.csproj`, net48): `dotnet build -c Release -p:GameDir="<Pfad zu Big Ambitions>"`,
dann den Ordner nach `...\Hovgaard Games\Big Ambitions\ModsLocal\GoingPublicServer\` kopieren. Die Mod steht unter MIT-Lizenz
(Copyright 2026 Melaus); der Lizenztext muss bei Weitergabe erhalten bleiben. Für eine Veröffentlichung lohnt sich ein
Pull Request an den Autor, damit Host und Clients dieselbe Version nutzen.
