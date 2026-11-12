# Testliste 1.0.17.3

Was seit 1.0.17 (veröffentlicht am 6. November) dazugekommen ist - zuerst das Neue von 1.0.17.3, darunter 1.0.17.2 und
1.0.17.1. Die Liste für 1.0.17 selbst (Module 9-11) ist `TESTLISTE-1.0.17.md`; Going Online (Modul 8) hat seine eigene
Liste: `goingonline/TESTLISTE.md` im Mod-Repo.

## Neu in 1.0.17.3: Ereignisse, Netz in der Stadt, Leitungen an Mitspieler, mehr Vorlagen
- [ ] Going Online, Ereignisse: ab und zu um 9 Uhr eine Meldung (Unwetter, Fußballspiel, Bagger, Zeitungsbericht, Testsieger,
      Glasfaser-Förderung); in der Übersicht oben „Ereignisse“ mit zwei Knöpfen und Frist; Kosten und Erstattungen unter
      Finanzen. Player.log „event … (GO-EVENTS-1)“.
- [ ] Netz in der Stadt: graue Verteilerkästen neben den Türen angeschlossener Häuser (Gehweg, nicht vor der Tür) mit einem
      Schild (Logo und Firmenname), Baustellen mit Schranken und Kegeln beim Anschließen, Kegel beim Technik-Einsatz. Jeder
      Spieler sieht die Kästen aller Kabelanbieter, auch von Spielern, die offline sind. Player.log „street: … (GO-STREET-1/2)“.
- [ ] Leitungen an Mitspieler (zwei Spieler mit Kabelanbieter): A bietet im Reiter „Netz“ an, B mietet; am nächsten Tag
      Leitungen, Abrechnung bei beiden unter Finanzen, auch wenn A offline war. Server-Log „lines: … (GO-WHOLESALE-2)“.
- [ ] Vorlagen: Kundencenter A1, A2, C1 bei Kristian Bahood; Kabel-Büros A3, C1, D2, J1 (Start/Voll) bei Kingdom Office;
      nichts in Wänden; kein Arbeitsplatz zeigt „Fehlende Anforderungen: Stuhl, Schreibtisch“, die Leute arbeiten.
- [ ] Kundencenter: Der Fernseh-Spot zeigt das Logo oben links und darunter nacheinander Begrüßung, jeden Tarif, den
      günstigsten Preis, Kundenzufriedenheit, Anschlüsse, Sender, Telefon, Glasfaser, Technik, Anschlussgebühr und
      „Jetzt wechseln“ - kein Text unter dem Logo. Der Gerätetisch zeigt das Firmenlogo (auch nach längerem Spielen).

## Neu in 1.0.17.2: Servicewagen, Kennzeichen, Regler, Personal, Vorlagen
- [ ] Gekaufter Servicewagen: Kennzeichen vorne und hinten „GO1-<Name>“, ein zweiter gekaufter „GO2-<Name>“; die Nummer
      bleibt nach Speichern und Laden. Ein anderer Spieler sieht die Nummer auch (Server ab 1.0.17.2).
      Player.log: „number plates: "GO1-…" on a service van (GO-PLATE-1)“, keine Zeile „number plates: …“ mit Fehler.
- [ ] Im Servicewagen Q und E zugleich drücken: Die Rundumleuchte blitzt links und rechts im Wechsel; noch einmal - aus.
      Die Blinker stehen danach wie vorher. Player.log: „roof beacon switched … (GO-VAN-15)“.
- [ ] Nachts vor dem Servicewagen: Die Lichtkegel kommen aus beiden Scheinwerfern, nicht vom Dach.
- [ ] Reifen von der Seite und von hinten: runde, volle Flanken, Profilrillen; hinten am Stoßfänger keine weißen Flecken.
- [ ] Außenspiegel: groß und rund am kurzen Arm, ohne Blinker. Der Blinker sitzt am Kotflügel direkt unter dem Spiegel (klein,
      runde Ecken, schmaler schwarzer Rand) und blinkt mit Q / E. Die Warnleuchten auf dem Dach haben runde Ecken (GO-VAN-17/18).
- [ ] Geleaste Servicewagen (App, Personal): Sie stehen in Parkbuchten bei der Kopfstelle (bis 60 m) und bei der
      Kundschaft (Rundumleuchte an) und fahren dazwischen die Straßen entlang; einsteigen geht nicht. Auf der Karte je
      ein Punkt, an der Kopfstelle einer für alle („… Servicewagen an der Kopfstelle“); der Knopf der Zeile „Servicewagen
      (Going Online)“ springt von Wagen zu Wagen und nach dem letzten wieder zum ersten. Kein Ruckeln (Player.log ohne
      „[Rhythm] OSCILLATING … period≈0.25s“). Player.log: „… van models in the city … (GO-VAN-16)“.
- [ ] Beklebung (Fenster am Fahrzeug und App-Reiter „Servicewagen“): die Schieberegler „Schriftgröße“ und „Logogröße“
      ziehen - der Wagen zeigt die neue Größe nach kurzer Pause; bei 70 % Schrift passt ein langer Name in eine Zeile.
      Logos füllen ihr weißes Feld.
- [ ] Neu eingestellte Techniker und Kundendienstler stehen unter Personal (Player.log: „crew: N in the team …
      (GO-STAFF-2)“ mit N > 0).
- [ ] Tarife: jeder Tarif in einer Zeile mit allen Spalten, Spalte „Download“; nichts abgeschnitten. Reiter „Gebäude“:
      „Glasfaser | Koax | Ausfälle“ und „Skyline Cable bis Tag …“ ganz lesbar.
- [ ] Vorlagen (die Mod legt sie beim Start in den Blueprints-Ordner, Player.log „blueprints: … (GO-BLUEPRINT-2)“):
      Kingdom Office (43 4th Avenue) bietet die beiden Kabel-Büros für ein J1-Büro an, Kristian Bahood (2 1st Avenue)
      das Kundencenter für einen A2-Laden - jeweils mit Kabelanbieter bzw. Kabel-Kundencenter darin oder leer. Nach dem
      Einbau steht alles frei (nichts in Wänden), die Arbeitsplätze funktionieren (Personal arbeitet), im Kundencenter
      setzen sich Besucher auf die Wartebänke.


## Auto, Server, Anzeige
- [ ] Selbstfahrt über eine Brücke oder eine lange Gerade ohne Ampel: bis etwa 60 km/h, vor der nächsten Ampel,
      Kreuzung oder Kurve sanft zurück auf 40.
- [ ] Beim Fahren stehen Versionszeile und ⏩-Knopf rechts neben „Tastenkürzel fürs Fahren“, nicht darauf.
- [ ] Fußgänger an einer Ampel-Kreuzung beobachten: Sie gehen nur los, wenn die Autos noch lange genug Rot haben, sonst
      warten sie auf die nächste Rotphase; wer auf dem Streifen ist, wenn die Ampel gleich umspringt, beeilt sich. Bekommen
      die Autos Grün, ist niemand mehr auf dem Zebrastreifen. Player.log alle 10 Minuten: „[PedLights] last 10 min: …“.
- [ ] Abbiegende Autos an einer Ampel-Kreuzung: Ist jemand auf dem Zebrastreifen, halten sie davor und fahren weiter, wenn
      er frei ist. Mit einem zweiten Spieler in der Nähe (Autos des Hosts): Die Fußgänger warten, bis kein Auto auf den
      Streifen zufährt.
- [ ] Zeit vorspulen im Auto: Das Fenster öffnet sich über der Fahrzeuganzeige.
- [ ] Am Pier (z. B. BlueStone Imports, 4 Pier) aussteigen, wo kein Parkplatz frei war: Die Figur steht auf dem Gehweg
      und läuft normal (Player.log: „[ExitRescue]“, falls sie versetzt wurde).
- [ ] Server-Log beim Start: „[Json] … message type(s) prepared on the main thread … 0 failed“.
- [ ] Kabelanbieter im Büro gründen (BizMan → Neues Unternehmen gründen bei einem Büro): „Kabelanbieter“ steht zur
      Wahl, „Kabel-Kundencenter“ nicht mehr; bei Lagerhaus und Laden steht keins von beiden. Kopfstelle und Kundencenter
      sind dieses Büro.
- [ ] Personalagentur, Kabel-Guru wählen, noch ohne Arbeitsplatz im Büro: Meldung „… hat noch keinen Arbeitsplatz …“.
      Mit Technik- und Kundencenter-Arbeitsplatz: Anderson Recruitment Corp. zeigt „Kundenservice“, „Kabeltechnik“ und
      „Schaltdienst“, City Workforce Inc. „Netzmeister“, „Außendienst“ und „Technische Entstörung“.
      Player.log: je Agentur „recruitment at … workplaces for … (GO-HIRE-1)“.
- [ ] Kabelanbieter gründen verlangt wie jedes Bürounternehmen den Kurs „Bürounternehmen“; BizMan → Einblicke listet CMTS, Computer-Arbeitsplätze, Toiletten und
      Waschbecken als Anforderungen. Personal arbeitet erst, wenn alles steht und jeder Computer Tisch und Stuhl hat (sonst
      in der App: „Arbeitsplatz unvollständig“ / „Büro noch nicht eingerichtet“).
- [ ] Alter Spielstand mit Kopfstelle im Lagerhaus: In einem Büro „Kabelanbieter“ gründen und ein CMTS aufstellen – die
      Kopfstelle zieht ins Büro, das Lagerhaus ist wieder ein gewöhnliches Lager.
- [ ] BizMan: Kabel-Guru steht unter „Unternehmen & Immobilien“ als „Kabelanbieter (n Anschlüsse)“, nicht mehr unter
      „Lagerhäuser“; die Seite öffnet auf „Einblicke“ und hat „Lieferungen“, „Fuhrpark“ und „Inventar“.
- [ ] BizMan → Fuhrpark: eigene Fahrzeuge am Büro und Leasing-Fahrzeuge je eine Karte, „jetzt: …“ oder
      „gerade frei“; „Fest zugeordnet“ listet die Kabel-Leute, eine Wahl hält bis zur nächsten Änderung.
- [ ] Großhändler anrufen → Vertrag starten: Kabel-Guru ist wählbar (Lagerregal im Büro); unter BizMan →
      Lieferungen stehen die Geräte; am Liefertag um 8 Uhr liegen sie auf dem Lagerregal.
- [ ] Voogle Maps → Kartenfilter: „Kabelanbieter“ unten bei den Unternehmen; anhaken zeigt das Büro, der Knopf daneben
      fährt dorthin.
- [ ] City Workforce Inc. → Hauptsitz wählen: „Netzmeister“ steht zur Wahl; mit Schicht an einem Computer des Hauptsitzes zählt die Person in Going Online als Netzmeister.
- [ ] Kabel-Guru hat statt des schwarzen ein richtiges Logo; BizMan → Einstellungen zeigt den Logo-Editor, Speichern
      ändert das Logo.
- [ ] F7 Ladenschilder: „★ Firmenlogo des Spiels“ wählen, Schildart und Geschäft wählen, Übernehmen: Das Schild zeigt
      wieder das Logo, die anderen Schildarten behalten ihr Bild.
- [ ] Going Online: CMTS, OLT, Sat, CAS, Voice und IPTV kaufen und aufstellen - eigene Schränke mit Namensschild in
      ihrer Farbe, LEDs blinken; Platzieren, Verschieben und Verkaufen wie vorher; Modems/ONT/Set-Top-Box/TV-Karte zeigen
      die neuen Symbole (Lager, BizMan). Player.log: „own models: 12 loaded ... (GO-MODELS-1)“.
- [ ] Going Online: Techniker auf einen Einsatz schicken - im Stadtverkehr fahren bis zu drei weiße Servicewagen mit dem
      Firmenlogo (Seiten und Heck), Leiter und Rundumleuchte; ohne Einsatz werden es wieder normale Autos.
      Player.log: „service vans: N in the traffic ... (GO-VAN-1)“.

## Fehlerberichte an den Server (PLAYER-REPORTS-1)
- [ ] Einem Server beitreten, bei dem in AMP „Fehlerberichte der Spieler“ an ist (ab Werk an): Nach etwa 20 Sekunden in
      der Welt fragt das Spiel „Fehlerberichte an diesen Server?“; „Ja, senden“ zeigt „Danke! …“.
      Player.log: „[Reports] error reports to the server: send (asked) (PLAYER-REPORTS-1)“.
- [ ] Optionen, Reiter Mods: Abschnitt „Multiplayer“ mit „Fehlerberichte an den Server“ (Fragen / Senden / Nicht senden),
      schon im Hauptmenü und auf Deutsch; die Wahl bleibt nach einem Neustart des Spiels.
- [ ] Nach etwa 10 Minuten Spiel: Server-Konsole „[Reports] last 10 min: 1 player(s), … (PLAYER-REPORTS-1)“; der
      Konsolenbefehl `reports` listet die häufigsten Fehler; auf dem Server liegt `bigambitions/player-reports/summary.json`
      mit deinen Zeilen, z. B. „own models: … (GO-MODELS-1)“ als `check`.
- [ ] Konsolenbefehl `playerlog <Name>` (Name aus `players`): Im Spiel kommt „Deine Player.log ging an den Server – er hat
      darum gebeten.“; auf dem Server liegt `player-reports/logs/<Datum>/<Name>_…_request.log.gz`. Darin steht statt deines
      Windows-Namens `<user>` und statt IP-Adressen `<ip>`.
- [ ] Mit „Nicht senden“: Es kommen keine Berichte; `playerlog` meldet in der Konsole „the Player.log was not sent (no
      consent)“.
- [ ] Nach einem Absturz (Spiel hart beenden) beim nächsten Beitreten: Nach etwa 30 Sekunden kommt „Die Player.log deiner
      letzten Sitzung (sie endete nicht sauber) ging an den Server.“, auf dem Server `…_crash.log.gz`.
- [ ] Auf einem Server mit älterem Going Public Server (ohne den Schalter) kommt keine Frage.

## Chat-Übersetzung (CHAT-TRANSLATE-1)
- [ ] Server-Konsole nach dem ersten Start mit dem neuen Server-Paket: „[BAMP-Start] Chat-Übersetzung: erste Einrichtung …“,
      dann „… eingerichtet in N s.“ und „… bereit - Deutsch <-> Englisch“; danach im Log „[ChatTr] chat translation is on:
      the translator answers …“. Ab dem zweiten Start nur noch „bereit“.
- [ ] Konsolenbefehl `translate Hallo, wie geht's?` antwortet mit „translate de -> en: Hello, how are you?“ (o. ä.).
- [ ] Optionen, Reiter Mods, Abschnitt „Multiplayer“: „Chat übersetzen (Deutsch – Englisch)“, ab Werk an.
- [ ] Zwei Personen, eine mit dem Spiel auf Deutsch, eine auf Englisch: Die erste schreibt „Willst du mit mir fusionieren?“ –
      die zweite liest „Want to merge with me?“, darunter klein und grau das Original. Umgekehrt kommt „where did you buy
      your van?“ als „Wo hast du deinen Van gekauft?“ an. Die eigene Zeile sieht jede Person so, wie sie sie geschrieben
      hat.
- [ ] Spiel auf Deutsch, aber englisch geschrieben: Eine andere Person mit deutschem Spiel bekommt es auf Deutsch.
- [ ] „lol“, „gg“, „ok“ und Zeilen mit einem Link bleiben, wie sie sind; private Nachrichten werden auch übersetzt.
- [ ] „Chat übersetzen“ aus: Diese Person bekommt alle Zeilen wie geschrieben (Player.log: „[ChatTr] … translated lines off“).
- [ ] AMP-Schalter „Chat-Übersetzung“ aus und Neustart: Konsole ohne „Chat-Übersetzung“-Zeilen, der Chat wie bisher.
- [ ] Größe der AMP-Sicherungen von Server 2 bleibt gleich (der Ordner `bigambitions/bamp-translate` ist nicht darin).

## Going Online: Kundengeräte (GO-DEVICE-VISUALS-1)
- [ ] Server-Konsole: keine Zeilen „game types: no model for ba:itemname_smartphone1“ mehr (bisher 2,5 je Sekunde).
- [ ] Modems, ONT, Set-Top-Box und TV-Karte, wo das Spiel Waren zeigt (z. B. im Regal eines Großhändlers): eigenes Modell
      statt Smartphone oder Karton.

## Fußgängerampeln (PED-SIGNALS-1/2/3, PED-LIGHTS-2)
- [ ] An den Ampelmasten des Spiels leuchten die beiden kleinen Kästen (einer in Richtung Ausleger, einer zur Seite) jetzt als
      Fußgängerampel – oben rotes stehendes, unten grünes gehendes Männchen; eigene Masten stehen keine mehr auf der Straße.
- [ ] Rot, solange die Autos Grün oder Gelb haben. Bekommen die Autos Rot, zeigt der Kasten gegenüber Grün, und die
      Wartenden gehen los (auch über breite Avenues – wer spät dran ist, beeilt sich, die Autos warten). Danach blinkt Grün,
      dann startet niemand mehr.
- [ ] Nachts leuchten die Männchen. Optionen, Reiter Mods: „Fußgängerampeln an den Übergängen“ aus – die Männchen sind weg.
      Player.log: „[PedSignals] N pedestrian lights in the boxes of P traffic light poles, at K of C crossings … (PED-SIGNALS-3)“ – deutlich weniger „boxes look at no crosswalk“ als die 177 von c275d7af.

## Brücke, Selbstparken (AUTO-DRIVE-5, SKIP-BRIDGE-OFF-1, AUTOPARK-4)
- [ ] Selbstfahrt über eine Brücke: bis etwa 90 km/h (Transporter wie der Nunavut: ihr Höchsttempo 80), vor dem Brückenende
      sanft zurück auf 40. Player.log: „[AutoDrive] on a bridge … up to 90 km/h (AUTO-DRIVE-5)“.
- [ ] Im Mehrspieler-Spiel erscheint auf der Brücke kein „Brücke überspringen“ mehr (Player.log: „[Bridge] … stays hidden
      in multiplayer (SKIP-BRIDGE-OFF-1)“); im Einzelspiel ist der Knopf wie gewohnt da.
- [ ] Selbstfahrt zu einem Ziel, an dem alle Plätze an der Tür besetzt sind: Das Auto parkt nur auf ebenem Grund in einer
      Parkspur; sonst bleibt es an der Straße stehen („… kein freier Parkplatz in der Nähe“) und du parkst selbst.

## Vorspul-Fenster (REST-UI-3, REST-AUTOCLOSE-1)
- [ ] ⏩-Knopf unten links: Das Fenster hat den Stil des Spiels – heller Titelbalken mit „ZEIT VORSPULEN“, rotes Kreuz,
      dunkler Körper, blaue Knöpfe; ziehen am Titel und alle Knöpfe wie vorher.
- [ ] Zeit wählen und „Zeit vorspulen“: Ist die Zeit erreicht, schließt sich das Fenster von selbst (Player.log:
      „[Rest] fast-forward done - the window closes (REST-AUTOCLOSE-1)“). Auf einer Bank oder im Bett steht die Figur wie
      bisher auf, beim Arbeiten an einer Station bleibt sie.

## Going Online: Büro beliefern, Leute zuweisen (GO-FURNDELIVERY-1, GO-ASSIGN-1/2)
- [ ] Kontakte, Factory Supply Depot, Bestellung (CMTS, OLT, Arbeitsplätze): Unter „Adresse“ steht jetzt auch das Büro des
      Kabelanbieters (und die Büros von IT-Support, Softwareentwicklung, Cloud-Dienste, Reinigungsfirma: Server und
      Putzsachen gibt es dort); die Lieferung kommt dorthin. Player.log: „[GoingOnline] the store's phone order offers N
      office(s) of our business types too (GO-FURNDELIVERY-1)“.
- [ ] Mitarbeiter-App, eine Person mit einem Kabel-Beruf (Kabeltechnik, Kundenservice …) auswählen: Unter „Aktuelles
      Unternehmen“ steht der Kabelanbieter – ohne Arbeitsplatz und auch, wenn im Büro nur Arbeitscomputer stehen (die zählen
      als Arbeitsplatz der Netzmeister; GO-ASSIGN-2). Ebenso „Mit den ausgewählten …“ → Unternehmen zuweisen.
- [ ] Personalagentur, Kabelanbieter ohne Arbeitsplatz wählen: weiter niemand im Angebot und die Meldung, welcher
      Arbeitsplatz fehlt (GO-WORK-1 bleibt).

## Logos in Schwarz-Weiß, eigenes Bild (LOGO-SHAPES-1, LOGO-PICKER-1)
- [ ] BizMan, Unternehmen, Einstellungen, Logo anpassen: Ganz vorn in „Logo-Form“ stehen 16 neue Formen (Satellitenschüssel,
      Kabelstecker, Router, Fernseher, Glasfaser, Haus mit WLAN; Headset, Laptop, Code, Terminal; Cloud, Server; Sprühflasche,
      Eimer; Waschmaschine, Hemd). Auswählen, Farben ändern, „Logoänderungen speichern“: Schild und Karte zeigen das Logo.
      Player.log: „[Logo] 16 black-and-white logo shapes of our business types are first in the game's shape list“.
- [ ] Eine neue Firma (Kabelanbieter, IT-Support, Softwareentwicklung, Cloud-Dienste, Reinigungsfirma, Waschsalon) bekommt
      gleich eine ihrer eigenen Formen.
- [ ] „Dateien durchsuchen“: Statt des kleinen Fensters oben links öffnet sich ein großes Fenster in der Mitte im Stil des
      Spiels. Links die Orte (unter Proton die Linux-Ordner: Bilder, Downloads …), rechts Ordner und Bilder mit Vorschau,
      Mausrad oder < > blättert, „Nach oben“ geht einen Ordner höher, Esc oder „Schließen“ schließt.
- [ ] Ein Bild anklicken: Das Fenster schließt, das Bild steht in der Vorschau des Logos und in der Liste der eigenen Formen.
      „Farben behalten“: Ein buntes Bild zeigt seine Farben (die Logofarbe wird Weiß). „Schwarz-weiß“: Das Bild wird zur Form
      und nimmt die Logofarbe an. Erst „Logoänderungen speichern“ übernimmt es.
- [ ] „Ordner im Dateimanager“ öffnet wie früher den Ordner des Spiels für eigene Formen.

## Textfelder in Going Online, Hinlaufen (GO-INPUT-2, NAV-WALK-2, GO-BIZMAN-2)
- [ ] Going Online, Tarife: In den Namen klicken und tippen – auch mit Leerzeichen und Ziffern. Das Spiel pausiert nicht und
      reagiert auf keine Taste; Enter, Tab oder ein Klick daneben übernimmt, Esc bricht ab. Ebenso der Preis (Komma oder Punkt).
      Player.log: „text field: an edit ended by Enter (GO-INPUT-2)“. Steht dort „the selection went to '…' without the player
      - given back“, bitte melden: Dann weiß ich, wer den Fokus wegnimmt.
- [ ] Stadtplan, Ziel wählen, „Hinlaufen“: Die Figur läuft los, auch wenn danach die Karte eines Gebäudes offen ist und auch
      beim zweiten Mal zum selben Ziel. Ist das Spiel pausiert, sagt eine Meldung, dass es losgeht, sobald es weiterläuft.
      Startet es nicht, steht der Grund in der Meldung und im Player.log („… did not start: … (NAV-WALK-2)“).
- [ ] BizMan, Kabelanbieter im Büro: kein Reiter „Inventar“ mehr (dafür „Inventar & Preise“ des Spiels), kein Fehler
      „InvalidCastException“ im Fehlerbericht.

## Wiederverbinden, Ampel-Fenster, LEDs, Leitstand (RECONNECT-2, PED-SIGNALS-5, GO-LIGHTS-1, GO-WORK-3)
- [ ] Server neu starten, während du drauf bist: Das Spiel geht ins Hauptmenü und verbindet sich von selbst wieder – auch
      wenn der Server noch lädt (bis zu acht Versuche über etwa sieben Minuten). Player.log: „[Rejoin] try N: connected -
      waiting …“, am Ende „[Rejoin] back in the world after N tries (RECONNECT-2)“.
- [ ] Fußgängerampeln: Die Figuren sitzen im schwarzen Fenster beider Kästen (nicht mehr an der Seite), rot oben, grün unten.
      Player.log: „[PedSignals] … (… placed mirrored) … (PED-SIGNALS-5)“ – deutlich mehr Ampeln als 174.
- [ ] Bei Tag (helles Büro) blinken die LEDs der Going-Online-Schränke sichtbar, die Bildschirme leuchten; nachts nicht grell.
- [ ] Büromöbelgeschäft: neuer „Leitstand-Arbeitsplatz“ (hellgrün). Aufgestellt bietet die Personalagentur Schaltdienst und
      Netzmeister; die Going-Online-App (Personal) zählt „… Technik-, … Leitstand-, … Kundencenter-Arbeitsplätze“.
- [ ] Die LEDs der Going-Online-Schränke blinken (auch in der Pause). Player.log: „rack lights: the clock runs for N blinking
      LED materials; R racks in the world, S of them show those materials (GO-LIGHTS-2)“ – S sollte gleich R sein.
- [ ] BizMan, Kabelanbieter, „Inventar & Preise“: Die Tabelle zeigt die fünf Geräte (Modems, ONT, IPTV-Box, Smartcard) mit
      Lagerbestand; ihr Preisfeld zeigt „-“ und ist gesperrt (Preise in der Going-Online-App). Player.log: „… lists its 5
      devices with their stock (GO-BIZMAN-3)“.

## Außenschild: Bild, Firmenlogo, Animation (SIGN-FX-1), Fahrzeugnamen (GO-FLEET-2)
- [ ] Schild anklicken, „Aussehen des Schildes“: Unter den Farben stehen „Bild auf dem Schild“ (Eigenes Bild, Firmenlogo) und
      „Animation“ (Pulsieren, Farbwechsel, Blinken). Player.log: „[SignFx] the sign window got the picture and animation
      buttons (SIGN-FX-1)“.
- [ ] „Eigenes Bild“ öffnet das große Bild-Fenster („Bild für das Schild“); ein Klick auf ein Bild setzt es aufs Schild
      (passend ins Breitformat, schwarzer Rand). „Firmenlogo“ setzt das Logo aus BizMan wieder aufs Schild. Andere Spieler
      sehen es nach wenigen Sekunden.
- [ ] Animation nachts ansehen: Pulsieren (Neon wird heller und dunkler, flackert ab und zu), Farbwechsel (Neon und
      Glühbirnen laufen durch die Farben), Blinken (alles an und aus); mehrere gleichzeitig gehen. Nach dem Speichern und
      Neuladen ist die Wahl noch da; andere Spieler sehen dieselbe Animation. Lauflicht folgt später.
- [ ] Going Online, Personal: „Fahrzeuge an der Kopfstelle“ fasst gleiche Namen zusammen („5 × Leasing-Transporter“), die
      Zeile „Arbeitsplätze im Büro“ ist nicht mehr abgeschnitten.

## Going Online: Grenzen je Gerät, Sat-Geräte, Störungen (GO-CAP-1, GO-TV-2, GO-FAULTS-2)
- [ ] Netz-Seite: neue Zeilen „Auslastung CMTS / OLT / Telefonie-Plattform / IPTV-Plattform / CA-System“ – „N von M“, gelb ab
      90 %, rot wenn voll. Je Gerät: CMTS 3.0 500 Modems, CMTS 3.1 1000, OLT 1000, Telefonie 500, IPTV 300, CA-System 500 Abos.
- [ ] Ist ein Gerät voll: Ereignis „… ist voll – neue Kundschaft wartet …“ (einmal am Tag), neue Kundschaft wartet; ein weiteres
      Gerät im Büro aufstellen – dann geht es weiter.
- [ ] Fernsehen: „N Sat-Geräte – je Gerät ein Transponder“; „+“ nur bis zur Zahl der Sat-Geräte. Mehr eingestellte
      Transponder als Geräte: „nur N auf Sendung – stell M weitere Sat-Geräte auf“ (gelb), Sender zählen nur die auf Sendung.
- [ ] Mehr Störungen (doppelt so viele) und neue Beschwerden: „Rechnung unklar“, „Sender fehlt oder Bild gestört“,
      „Preis zu hoch“ (Tarif deutlich über dem Marktpreis), „Ausfall“ (nach Ausfällen) – unter Aufträge.
- [ ] Netz-Seite: „Auslastung CMTS-Ports“ (Knoten von Ports, 8 Ports je CMTS). Sind alle Ports belegt, gibt es keinen neuen
      Knoten und kein neues Gebäude, das einen Knoten braucht (Ereignis „Alle CMTS-Ports sind belegt …“). Mehr Knoten als Ports
      (alter Spielstand): Die Knoten teilen sich die Ports, „je Knoten“ zeigt weniger Leistung, abends wird es langsamer.
- [ ] Viertel-Karte: „− Knoten“ entfernt einen Knoten, solange die übrigen alle angeschlossenen Wohnungen abdecken (500 je
      Knoten); ein Knoten im Bau wird zuerst abgebrochen.
- [ ] **Going Online 0.8 – Büros und Kundencenter je Viertel**: neuer Anbieter: erst Büro als Kopfstelle („Kabelanbieter“), dann
      ein Laden als „Kabel-Kundencenter“ im selben Viertel – erst dann Kundschaft und Gebäude. Reiter Netz → „Büros und
      Kundencenter“ (je Viertel: Büro, Kundencenter, Zuführung, Ring, Personal). Zweites Büro in einem anderen Viertel → CMTS +
      „Zuführung bauen“ → Hub. „Ring schließen“ → Baggerschaden ohne Ausfall. Alter Spielstand: 10 Tage alte Regeln.
- [ ] **Personal je Viertel**: Technik 400, Schaltdienst 800, Kundenservice 500 Anschlüsse je Person; ein Netzmeister führt bis zu
      10 Leute. Reiter Personal zeigt es je Viertel; zu wenig → „Zu wenig Leute für …“, neue Kundschaft wartet.
- [ ] **Leben im Kundencenter**: Gerätetisch (Möbelgeschäft) mit Logo; Leute kommen ins geöffnete Kundencenter, schauen am
      Gerätetisch, warten auf Sitzplätzen, gehen zum Kundenplatz; Fernseher zeigen den Werbespot der Firma.
- [ ] **Netzüberwachung und Notstrom**: Reiter Netz → Netzüberwachung an (läuft mit Netzmeister in der Schicht + Leitstand);
      USV-Anlage und Notstromaggregat (Möbelgeschäft), Akkus je Viertel; Stromausfälle als Ereignis und in der Übersicht.

- [ ] **Lämpchen der Schränke blinken (GO-LIGHTS-3)**: Kopfstelle betreten – die grünen/gelben/blauen LEDs der Schränke (und der
      Modems auf dem Gerätetisch) flackern. Bericht: „rack lights: … N pieces in the world, N of them blink (GO-LIGHTS-4)“ – geschaltet wie die Bremslichter der Autos.
- [ ] **Upload, High-Split, Fiber Deep, Remote-PHY**: Tarife mit Upload-Spalte; Viertelkarte mit Upload-Auslastung und den drei
      Umbauten (High-Split mit Hausverstärker-Aufträgen, Fiber Deep mit kleinen Knoten, Remote-PHY am vCMTS-Server).
- [ ] **Streaming-Cache, Netz vermieten, Firmen-SLA, Sammelvertrag**: siehe Going-Online-Testliste; Reiter Netz, Tarife, Gebäude.
- [ ] **Monitore**: Bildschirme der Going-Online-Arbeitsplätze zeigen die App (Aufträge, Netz, Übersicht, Personal).
- [ ] **Außenschild: Logo neben dem Namen (SIGN-FX-2)**: Schild-Fenster → „Logo neben dem Namen: Links | Rechts | Beide Seiten“;
      noch einmal dieselbe Wahl = wieder nur der Name.
- [ ] **Navi im großen Menü (NAV-CHOICE-2)**: Karte → Gebäude → „In BizMan öffnen“: unten erscheint kein „Ziel: … Hinlaufen | Nur Navi“
      mehr über BizMan; nach dem Schließen wieder.

- [ ] **Tageszusammenfassung zählt den Kabelanbieter (GO-SUMMARY-1)**: „Unternehmen“ in der Zusammenfassung um Mitternacht
      zählt Kabel-Guru (Kopfstelle) mit.
- [ ] **Eigenes Bild neben dem Namen (SIGN-FX-3)**: Schild-Fenster → erst „Links“/„Rechts“/„Beide Seiten“, dann „Eigenes Bild“ →
      das Bild steht als Logo neben dem Namen; ohne gewählte Seite füllt es wie bisher das Schild; „Firmenlogo“ nimmt wieder das
      BizMan-Logo.
- [ ] **Going Online in BizMan nach dem Umbau (GO-PATCHES-1)**: Die Anpassungen für den Kabelanbieter kommen jetzt aus dem
      gemeinsamen Code von Going Online. Alles wie vorher: Kabelanbieter in BizMans Liste mit Reitern Lieferungen und Fuhrpark,
      Liefervertrag beim Großhändler, Telefonbestellung beim Möbelhaus ins Büro (auch für IT-Firma und Reinigungsfirma),
      Hinweise der Personalsuche.
- [ ] **Neue Modelle in Going Online (GO-MODELS-1)**: vCMTS, Streaming-Cache, USV und Notstromaggregat haben eigene 3D-Modelle
      und eigene Bilder im Laden (bisher der große Schrank des Spiels); die Lämpchen blinken.

## Eingabefelder, Servicewagen, Fußgängerampeln (GO-INPUT-3, GO-VAN-2..5, PED-SIGNALS-6)
- [ ] **Preise eintippen (GO-INPUT-3)**: Going Online → Tarife → in den Preis klicken: Der Wert ist blau markiert, die erste Ziffer
      ersetzt ihn, Rücktaste löscht, Enter (oder Tab, oder ein Klick daneben) übernimmt, Esc bricht ab. Ebenso Tarifname,
      Gerätepreise, Pay-TV-Preis, Straßensuche. Das Spiel darf beim Tippen nicht pausieren oder sonst reagieren.
      Player.log: „text box: an edit started (GO-INPUT-3)“ und „… ended by Enter (GO-INPUT-3)“.
- [ ] **Servicewagen beim Händler (GO-VAN-2)**: Ein Autohaus mit Transportern (z. B. General US Trucks), im Laden oder per
      Telefon: „Servicewagen“ für 26.500 $ in der Liste. Kaufen → er steht mit Logo vor dem Händler; fahren, parken.
      Player.log: „service van: vehicle type … registered“, „service van: model ready …“, „service van: offered at a car dealer“.
- [ ] **Geleaste Servicewagen am Büro (GO-VAN-3)**: App → Personal → Leasing-Transporter „+“: Nach ein paar Sekunden steht ein
      Servicewagen mit Logo in einer Parkbucht vor dem Büro; „-“ gibt ihn zurück (nicht, solange du daneben stehst oder ihn fährst).
      BizMan → Fuhrpark zählt ihn.
- [ ] **Wagen beim Kunden (GO-VAN-4)**: Mit Technik-Leuten im Dienst und offenen Aufträgen: Der Wagen steht bei der Kundschaft am
      Straßenrand (Rundumleuchte blinkt), solange jemand aus der Technik dort arbeitet, danach wieder am Büro. Kein Strafzettel. Player.log: „service van: a
      technician's vehicle drove to a customer (GO-VAN-4)“.
- [ ] **Servicewagen im Verkehr (GO-VAN-5)**: Solange Technik-Leute Dienst haben, fahren bis zu drei Servicewagen im Stadtverkehr,
      auch neben anderen Spielern; ihre Rundumleuchte blinkt. Player.log: „service vans: N in the traffic (… on duty …)“.
- [ ] **Fußgängerampeln überall (PED-SIGNALS-6)**: Durch die äußeren Viertel fahren: An den großen Ampelmasten mit langem Ausleger
      leuchten die Kästen jetzt auch; jedes Ende eines Zebrastreifens mit Ampel hat eine Fußgängerampel (im Kasten, außen am Mast
      oder an einem schlanken Pfosten). Player.log: „[PedSignals] 844 pedestrian lights at 247 of 247 crossings …“.
- [ ] **Neue Form des Servicewagens (GO-VAN-6)**: Der Servicewagen (gekauft, geleast, im Verkehr) hat die runde Kastenwagen-Form:
      schräge Haube und Frontscheibe, Radläufe mit schwarzen Leisten, Scheinwerfer mit Grill, schwarze Stoßfänger, Spiegel,
      Türfugen, Schiebetür, Hecktüren mit Scheiben, Rückleuchten in den Ecken, Stahlfelgen mit Lochkranz (drehen und lenken mit).


## Beklebung des Servicewagens (GO-LIVERY-1)

Going Online, neuer Reiter **„Servicewagen“** (ganz unten in der Liste der Reiter).

- [ ] **Vorschau**: Oben zeigt der Reiter die linke Seite und das Heck des Wagens mit deiner Beklebung (Entwurf 1: blaue Fläche
      mit Logo und Firmenname, Glasfaser-Licht, Spruch, Kontaktzeile, Angebote, Kabel, „Techniker im Einsatz“).
- [ ] **Motiv**: „Glasfaser-Licht“, „Techniker im Einsatz“ (Bild mit Technik-Person am Verteilerkasten), „Nur Schrift“ (ohne Fläche),
      „Nur Logo“ (das alte Aussehen: nur das Logo) – Vorschau und Wagen in der Stadt ändern sich nach etwa einer Sekunde.
- [ ] **Farbe der Fläche**: eines der 11 Farbfelder anklicken – Fläche, Schrift und Angebotsbalken nehmen die Farbe an.
- [ ] **Schalter**: Firmenlogo auf der Fläche, „Techniker im Einsatz“ an den Vordertüren, Kabel an den Schiebetüren – jeweils an/aus.
- [ ] **Spruch und Kontaktzeile**: eintippen, Enter – steht auf beiden Seiten und an den Hecktüren. Leer = „#FIRMENNAME“ und
      „Kundenservice rund um die Uhr“. Ein langer Firmenname mit Leerzeichen oder Bindestrich kommt in zwei Zeilen.
- [ ] **Angebote nach Geräten**: Die Balken an der Schiebetür folgen den Geräten in der Kopfstelle (CMTS → Internet, OLT → Glasfaser,
      Sat-Kopfstelle → Fernsehen, IPTV-Plattform → IPTV, Telefonie-Plattform → Telefon, CA-System → Pay-TV). Gerät aufstellen
      oder wegnehmen – der Balken kommt oder geht. Bei 5–6 Angeboten zwei Spalten.
- [ ] **„Standard-Beklebung“** setzt alles zurück.
- [ ] **Autohaus – Ausstellungsstück**: Garment District (günstiges Autohaus): statt des zweiten Honza Mimic steht der Servicewagen
      in deiner Beklebung. Garment District (LKW): hinter den Lieferwagen. Industry City (LKW): im Gang zwischen Liefer- und
      Fracht-LKW. Bitte prüfen: steht er frei (nicht in Möbeln oder anderen Wagen)? Ohne Kabelanbieter zeigt er eine
      Muster-Beklebung „Going Online“.
- [ ] **Farbwahl im Autohaus**: Klick auf den ausgestellten Servicewagen öffnet den Kauf des Spiels. Eine andere Farbe wählen –
      der Wagen färbt sich sofort um, die Beklebung bleibt. Gekauft: Der neue Wagen hat diese Farbe und die Beklebung.
- [ ] **Geleaste Wagen** sind weiß mit Beklebung; Wagen im Stadtverkehr ebenfalls weiß.
- [ ] **Mehrspieler**: Die Servicewagen der anderen zeigen deren Beklebung (deren Name, Logo, Farbe) – nicht deine. Wer kein
      Going Online nutzt, hat einen Wagen ohne Beklebung.
- [ ] Beim ersten Anzeigen einer Beklebung ein kurzer Ruckler (ein Bild wird gemalt) ist normal. Im Spieler-Fehlerbericht stehen
      Zeilen „van livery: …“.

## Beklebung jedes Fahrzeugs, jede Seite für sich (CAR-LIVERY-1)

Im eigenen Fahrzeug sitzen (gekauft oder geleast, nicht Sackkarre, Plattformwagen oder Roller). In der Leiste unten neben
„Fahrzeug parken“ steht **„Beklebung“**.

- [ ] **Fenster öffnen**: Im Stand auf „Beklebung“ – ein großes Fenster im Stil der anderen. Fährt das Auto noch, kommt der Hinweis
      „Halte zuerst an“. Solange das Fenster offen ist, fährt das Auto nicht los (auch nicht beim Tippen von W/A/S/D).
- [ ] **Vorschau**: Links oben zeigt eine Kamera das echte Auto von der gewählten Seite. Die Reiter **Links / Rechts / Hinten / Vorne**
      drehen die Ansicht. Steht eine Wand zwischen Kamera und Auto, wird sie weggeschnitten.
- [ ] **Gestaltung je Seite**: „Keine“, „Logo und Name“, „Farbfläche“, „Farbband“, „Vollfolie“ (beim Servicewagen zusätzlich
      „Glasfaser-Licht“ und „Techniker im Einsatz“). Jede Änderung erscheint nach etwa einer Sekunde am Auto.
- [ ] **Farbe je Seite**: eines der 11 Farbfelder. **Logo-Platz**: Vorne / Mitte / Hinten an den Seiten, Links / Mitte / Rechts
      hinten; auf der Motorhaube immer Mitte.
- [ ] **„Auf alle Seiten übernehmen“** kopiert Gestaltung und Farbe der gewählten Seite auf die anderen.
- [ ] **Lackfarbe**: die Farben des Autohauses – das ganze Auto wird umlackiert (bleibt nach Speichern und Laden).
- [ ] **Logo und Name von**: mit < > zwischen deinen Firmen wechseln (oder „ohne Logo“). **Name, Spruch, Kontaktzeile** eintippen
      (Strg+V geht, Enter oder Klick daneben beendet).
- [ ] **Spielautos** (Honza Mimic, Vord V150, UMC Desert, Mersaidi Dash, Lieferwagen …): Die Aufkleber sitzen auf freien Flächen –
      an Türen bzw. Laderaum unter den Fenstern, nicht auf Scheiben, Lampen oder Reifen. Bitte prüfen: liegt etwas schief, zu klein,
      auf einem Fenster oder auf dem Rad? Steht bei einer Seite „keine freie Fläche“, hat das Modell dort keinen Platz.
- [ ] **Servicewagen**: Jede Seite einzeln (z. B. links Glasfaser-Licht in der Mitte, rechts Vollfolie rot, hinten Farbband).
      „Erster Vorschlag“ = wieder die Beklebung der Firma aus der App Going Online.
- [ ] **„Ohne Beklebung“** nimmt alles ab; „Fertig“ oder Esc schließt.
- [ ] **Speichern und Laden**: Beklebung und Lackfarbe sind nach dem Laden wieder da; ein verkauftes Auto fällt aus der Liste.
- [ ] **Mehrspieler**: Die anderen sehen deine Autos mit deiner Beklebung (mit deinem Firmenlogo), du ihre – auch nach Neuverbinden.
- [ ] Im Spieler-Fehlerbericht stehen Zeilen „[CarLivery] …“ (gefundene Flächen je Autotyp, gemalte Aufkleber, ob der Decal-Shader
      da ist).

## Servicewagen: Lichter, Räder, Spiegel, Lack, Vorschau (GO-VAN-7/8/9, GO-LIVERY-2/3, CAR-LIVERY-2)

Im Servicewagen von Going Online fahren (gekauft oder geleast), am besten einmal bei Tag und einmal bei Nacht.

- [ ] **Bremslicht**: Beim Bremsen leuchten hinten die roten Leuchten hell, mit Fahrlicht (nachts) schwach.
- [ ] **Blinker** (Q / E): Nur die gewählte Seite blinkt – vorne in den Scheinwerfern, an den Kotflügeln unter den
      Außenspiegeln und hinten.
- [ ] **Fahrlicht**: Nachts leuchten die Scheinwerfer; ohne Licht leuchten sie nicht mehr (vorher immer helle Punkte vorne).
- [ ] **Rückfahrlicht**: Im Rückwärtsgang leuchten hinten die weißen Felder.
- [ ] **Räder**: schwarze Reifen mit sichtbarer Flanke, silberne Stahlfelgen mit 14 Löchern, schwarzer Nabe und Radmuttern.
- [ ] **Form**: hohe Front mit großem schwarzem Stoßfänger, steile Frontscheibe, vorderes Seitenfenster als Trapez, Schulterlinie
      entlang der Seite, enge Radläufe, Heck oben leicht geneigt – wirkt der Wagen neben den Spielautos natürlich?
- [ ] **Spiegel**: große, runde Außenspiegel am kurzen Arm, hinten Spiegelglas; der Blinker sitzt darunter am Kotflügel.
- [ ] **Lack der Spielautos**: Glanz und Spiegelung wie bei anderen Autos. Im Autohaus eine andere Farbe wählen – der Wagen hat sie.
      „Lackfarbe“ im Fenster „Beklebung“ färbt ihn sofort. Nach Fahrten wird er schmutzig, Regen macht ihn sauber. Die Aufkleber
      behalten ihre Farben; die Schrift auf dem Lack ist je nach Lackfarbe hell oder dunkel.
- [ ] **Stadtverkehr**: Die Servicewagen im Verkehr bleiben weiß, auch mit Licht und Blinkern.
- [ ] **Going Online, Reiter „Servicewagen“**: Die Vorschau zeigt den Wagen selbst von links und von hinten (Fenster, Türen,
      Lampen, Räder) mit der Beklebung darauf; bei „Nur Logo“ einen weißen Wagen.
- [ ] **Fenster „Beklebung“**: Die Kamera steht über dem Fahrzeug – die Seiten leicht von oben, „Vorne“ schräg von oben auf die
      Haube. Kein Blick mehr von unten durch die Straße.
- [ ] **Server**: Im Server-Log keine Meldung „Prefab wasn't found: …/govan“; geparkte Servicewagen von Leuten, die offline sind,
      stehen weiter da.
- [ ] Im Player.log: „van paint: the game cars' material … (GO-LIVERY-3)“ und „van picture: … (GO-LIVERY-2)“.

## Arbeitsplätze, ältere Einrichtungen, AMP-Start (WORKSTATION-CHAIR-1, GO-DESK-LINK-1, PRESTART-1, AMP-INFO-2, FIRST-WORLD-1)

- [ ] **Aufstehen**: Im Kundencenter an einen Computer setzen und arbeiten, dann ESC – die Spielfigur steht auf. Auch wenn
      währenddessen am selben Schreibtisch ein Stuhl dazukommt. Im Player.log höchstens „[Workstation] … (WORKSTATION-CHAIR-1)“,
      keine NullReferenceException bei UnassignEmployee.
- [ ] **Ältere Einrichtung**: Ein Kundencenter oder Büro, das vor GO-BLUEPRINT-6 aus einer Vorlage eingerichtet wurde, betreten –
      nach wenigen Sekunden sind die Ausrufezeichen an den Arbeitsplätzen weg („Fehlende Anforderungen: Schreibtisch“ nicht mehr).
      Im Player.log „desks: N computer(s) and chair(s) at … linked to the desk they stand on (GO-DESK-LINK-1)“. Nach Verlassen und
      Wiederbetreten bleibt es so, und das Personal arbeitet an diesen Plätzen.
- [ ] **AMP-Statusseite**: Port, Server-Passwort (Schlüssel), Server-Version mit Build und Admin-Link stehen da, auch nach dem
      Neuladen der Seite (die Mod schreibt die Zeile alle 30 s). Während „stop warn“ oder „restart N“ läuft, kommt keine Zeile mehr.
- [ ] **Neue Vorlage** (AMP startet `/usr/bin/xvfb-run` direkt, Vorstart-Schritte aus bigambitionsstart.json): Start, READY,
      Stop, Neustart. In der Konsole die Vorstart-Meldungen „[BAMP-Start] …“; der Chat-Übersetzer läuft (headless/translate.log)
      und beendet sich mit dem Spiel.
- [ ] **Ältere Vorlage** (App.ExecutableLinux=bamp-start.sh): startet genauso, bamp-start.sh ruft bamp-prestart.sh auf.
- [ ] **Hängengebliebenes Spiel**: Läuft vom letzten Lauf noch ein Spiel dieser Instanz, beendet der nächste Start es
      („Ein Spiel vom letzten Lauf läuft noch … wird beendet“); andere Instanzen bleiben unberührt.
- [ ] **Frische Instanz ohne Spielstand** (FIRST-WORLD-1): Neue Instanz aus der Vorlage, Update, Start, ohne etwas
      einzustellen. Im Log „no multiplayer save on this machine yet - the first world 'Server' is created with difficulty
      Normal (FIRST-WORLD-1)“, dann READY und „new world saved as 'Server'“; kein Exit-Code 3. Der nächste Start lädt „Server“.
      Mit „Neue Welt: Schwierigkeit“ = Einfach entsteht sie mit Einfach. Ein Server mit Spielstand lädt wie bisher die zuletzt
      gespielte Welt.

## Personal umbenennen (GO-RENAME-1)

- [ ] **Name ändern**: Going Online → Personal: Bei eingestellten Leuten steht der Name in einem Feld. Anklicken, neuen
      Namen tippen, Enter – der Name bleibt, auch in BizMan (Mitarbeitende) und nach Speichern und Neuladen. Esc bricht ab,
      ein leeres Feld lässt den alten Namen. Im Player.log „staff: '…' is now called '…' (GO-RENAME-1)“.

## Kundencenter: Arbeitsplätze auf dem Service-Tresen (GO-DESK-LINK-2)

- [ ] Kundencenter aus einer GO-Vorlage (A1, A2, C1 – z. B. 19 9th Avenue) betreten: über den Going-Online-Arbeitsplätzen
      auf den Tresen verschwindet das rote „!“, der Hinweis zeigt keine „Fehlende Anforderungen: Schreibtisch“ mehr.
      Im Player.log einmal „desks: workplaces on a service counter count as on a desk (GO-DESK-LINK-2)“.
- [ ] In der Schicht arbeitet das Kundendienst-Personal dort (Going Online → Personal: im Dienst, nicht „Arbeitsplatz unvollständig“).
- [ ] Ein normaler Computer auf einem Tresen (kein Going-Online-Arbeitsplatz) meldet weiter „Schreibtisch“ – nur unsere zählen.

## Personal am Tresen auf der richtigen Seite (GO-COUNTER-SEAT-1)

- [ ] Kundencenter aus einer GO-Vorlage (z. B. 19 9th Avenue) betreten: Going Online dreht die Arbeitsplätze, deren Bildschirm
      zum Sessel zeigt („desks: N workplace(s) … turned so their screen faces the staff's chair (GO-DESK-LINK-3)“).
- [ ] Neue Schicht (oder Gebäude verlassen und wieder betreten): das Personal sitzt auf dem Bürostuhl, der Bildschirm zeigt zu
      ihm; die Kundschaft setzt sich in den Sessel gegenüber. Im Player.log einmal „staff at a service counter take the office
      chair, the armchair is the customers' (GO-COUNTER-SEAT-1)“.
- [ ] Neu eingebaute Kundencenter-Vorlage (GO-BLUEPRINT-7): Bürostuhl hinter dem Tresen auf der Bildschirmseite, Sessel davor.

## Automatische Updates mit Ankündigung (AUTO-UPDATE-1)

- [ ] AMP-Einstellungen (nach dem Update der Instanz und einem Neustart): „Automatische Updates“ (an) und „Vorwarnung (Minuten)“ (5).
- [ ] Konsole `update`: „up to date: build … is the newest on GitHub“, solange der Server den Build des neuesten Releases hat.
- [ ] Neuer Build im Release (mit GoingPublicServer-Update.json): spätestens nach einer Stunde (oder `update`) lädt der Server
      ihn, prüft ihn („is in place“), alle Spieler sehen „Server-Update auf … (Build …). Neu: …“ und die Countdown-Warnungen,
      nach 5 Minuten speichert der Server und startet neu; danach zeigt die Statuszeile den neuen Build.
- [ ] Ohne Spieler: Neustart sofort nach der Meldung. Mit „Automatische Updates“ aus: nichts passiert.
- [ ] Ein Release ohne GoingPublicServer-Update.json: eine Zeile „has no GoingPublicServer-Update.json“, kein Update.

## Kundschaft wartet, bis das Personal frei ist (GO-LOBBY-2)

- [ ] Kundencenter geöffnet, Personal in der Schicht: Kundschaft kommt herein, schaut evtl. am Gerätetisch, setzt sich auf einen
      Wartestuhl (nicht an den Tresen) und geht erst zum Tresen, wenn dort eine Person frei am Platz sitzt – dann setzt sie
      sich in den Sessel gegenüber, bleibt 20-40 s und geht.
- [ ] Ohne Personal am Platz bleibt die Kundschaft sitzen und geht nach 1,5-2,5 Minuten wieder.
- [ ] Neustart-Warnungen bei einem automatischen Update: „Der Server startet in … neu. Update auf Version: 1.0.17.3 (Build …).“
