# Testliste Going Online 0.8.1

Abhaken im Spiel. Bei Fehlern bitte die `Player.log` mitschicken (Zeilen mit `[GoingOnline]`).

## 0. Neu in 0.8.1: der Einzel-Mod in den Seiten des Spiels (GO-PATCHES-1)

Nur mit dem eigenen Mod, Going Public Server dabei abgeschaltet (sonst hält sich Going Online raus). Unter Linux mit
Proton testen, wenn möglich – dort braucht Harmony die Korrektur WINE-JUMPS-1.
- [ ] Spielstand laden. In der `Player.log` steht `[GoingOnline] Harmony v2.4.2.0 (this mod's copy): 17 patch class(es) in,
      0 failed`, unter Proton dazu `Wine/Proton: the game hooks are written as self-contained jumps (WINE-JUMPS-1)`.
- [ ] BizMan → Unternehmen: Der Kabelanbieter steht in der Liste, als Typ „Kabelanbieter (… Anschlüsse)“, mit seiner
      Zufriedenheit; bei den Lagerhäusern steht er nicht.
- [ ] Seine Seite öffnet auf „Einblicke“ und hat die Reiter Lieferungen und Fuhrpark; „Inventar & Preise“ zeigt die Geräte
      mit Bestand, ihr Preisfeld zeigt „-“ und lässt sich nicht ändern.
- [ ] Fuhrpark: die Fahrzeuge am Büro und die geleasten, mit wer gerade fährt; ein Fahrzeug bekommt per Auswahl eine Person.
- [ ] Großhändler anrufen → Liefervertrag: Der Kabelanbieter ist auswählbar; in BizMan → Lieferungen stehen die Geräte; um
      8 Uhr liegen sie im Lagerregal des Büros.
- [ ] Möbelhaus (Factory Supply Depot) anrufen, CMTS oder Arbeitsplatz bestellen: Das Büro des Kabelanbieters steht als
      Lieferadresse zur Auswahl.
- [ ] Personalvermittlung: Den Kabelanbieter wählen – die Berufe seiner Arbeitsplätze stehen zur Auswahl; ohne passenden
      Arbeitsplatz kommt ein Hinweis, was fehlt.
- [ ] Die Geräte-Karte im Reiter Technik nennt jetzt auch den Liefervertrag beim Großhändler.
- [ ] Eigene Modelle für die neuen Geräte: vCMTS (Server und Switches mit Glasfasern), Streaming-Cache (lila Blenden),
      USV (Gerät mit Anzeige neben dem Akkuschrank) und Notstromaggregat (Gehäuse mit Bedienfeld, Not-Aus und Auspuff) -
      im Laden mit eigenem Bild, aufgestellt mit eigenem Modell; die Lämpchen blinken.
- [ ] Handy (GO-PHONE-2): Mit der Kachel „Going Online“ wird das Handy eine Reihe höher, die Namen unter allen Symbolen
      sind ganz zu sehen. Eingeklappt zeigt es nicht mehr als vorher; im Auto mit Radio bleibt die zusätzliche Reihe.
- [ ] Personal: Ohne Arbeitsplatz bietet die Vermittlung nur „Reinigung“ und sagt, was fehlt. Mit einem
      Technik-Arbeitsplatz im Büro bietet Anderson Recruitment Corp. Technik und Schaltdienst an, City Workforce Inc. den
      Netzmeister; mit einem Kundencenter-Arbeitsplatz City Workforce Inc. Außendienst und Technische Entstörung.


- [ ] Den Ordner `GoingOnline` aus dem Zip nach `%LocalAppData%Low\Hovgaard Games\Big Ambitions\ModsLocal\` legen, das
      Spiel starten und den Mod in der Mod-Liste aktivieren.
- [ ] Optionen → Mods: „Going Online“ zeigt vier Regler (Störungen, Neukundschaft, DOCSIS 3.1 ab, Glasfaser ab), jeweils
      mit „100 %“.
- [ ] Einen Spielstand laden. Im Handy steht die Kachel „Going Online“, die Kacheln rücken etwas zusammen, und das Handy
      wird nicht höher.
- [ ] Im großen Menü steht oben „Going Online“ neben den Apps des Spiels. Umschalt+F12 öffnet und schließt die Seite.
- [ ] Ein Klick auf eine App des Spiels (BizMan, Kontakte …) bringt deren Seite, unsere tritt zur Seite.

## 2. Gründung (0.7: ein Büro für alles)

- [ ] **Im Büro gründen (0.7)**: Ein leeres Büro mieten, in BizMan „Neues Unternehmen gründen“: Dort steht
      „Kabelanbieter“ mit dem Bild von Going Online, aber kein „Kabel-Kundencenter“ mehr. Gründen: Das Unternehmen heißt
      wie eingegeben und ist geöffnet (Öffnungszeiten 0–24 Uhr an allen Tagen); die App zeigt den Kabelanbieter,
      Kopfstelle und Kundencenter sind dieses Büro (Reiter Personal: kein eigener Abschnitt „Kundencenter“).
- [ ] Bei einem Lagerhaus und bei einem Laden steht unter „Neues Unternehmen gründen“ weder „Kabelanbieter“ noch
      „Kabel-Kundencenter“.
- [ ] **Kurs „Bürounternehmen“ (0.7)**: Ohne diesen Kurs der Manhattan Business School zeigt BizMan beim Gründen die
      Meldung des Spiels, dass der Kurs fehlt; mit dem Kurs lässt sich der Kabelanbieter gründen (wie jedes Bürounternehmen).
- [ ] **Einrichtung wie im Büro (0.7)**: Nach dem Gründen stehen in BizMan unter Einblicke die Anforderungen „CMTS DOCSIS
      3.0“, Computer-Arbeitsplätze, Toilettenkabinen und Waschbecken (Anzahl nach Größe), und es gibt dazu Aufgaben. Die
      App-Übersicht sagt „Das Büro ist noch nicht eingerichtet … : …“ mit dem, was fehlt.
- [ ] **Tisch und Stuhl (0.7)**: Einen Technik-Arbeitsplatz ohne Tisch oder Stuhl aufstellen, jemandem dort eine Schicht
      geben: Im Reiter Personal steht „Arbeitsplatz unvollständig – der Computer braucht einen Tisch und einen Stuhl“, die
      Person arbeitet nicht. Mit Tisch und Stuhl arbeitet sie. Fehlen Toiletten oder Waschbecken: „Büro noch nicht
      eingerichtet – siehe BizMan, Anforderungen“.
- [ ] Die App ohne Kabelanbieter: die drei Schritte (Büro, BizMan, CMTS und Technik-Arbeitsplatz) und der Hinweis zu
      Transportern und Lagerregal.
- [ ] **Berufe nach Arbeitsplätzen (0.7)**: Ohne Arbeitsplatz im Büro bei Anderson Recruitment Corp. den Kabelanbieter
      wählen: gelbe Meldung „… hat noch keinen Arbeitsplatz …“, die Berufsliste bleibt gesperrt (nicht die Meldung des
      Spiels „bietet keine Kandidaten …“). Einen Technik-Arbeitsplatz aufstellen: Anderson bietet Kabeltechnik und
      Schaltdienst, City Workforce Inc. Netzmeister. Dazu einen Kundencenter-Arbeitsplatz: Anderson auch Kundenservice,
      City Workforce auch Außendienst und Technische Entstörung. In der App unter Personal steht „Die Agenturen bieten an:
      …“ mit genau diesen Berufen. Im Log: „recruitment at … workplaces for … (GO-HIRE-1)“.
- [ ] Nur Kundencenter-Arbeitsplätze, Agentur Anderson: Sie bietet Kundenservice an. Nur Technik-Arbeitsplätze, Agentur
      City Workforce: Netzmeister.
- [ ] BizMan → Kabelanbieter → Zeitplan: Die aufgestellten Arbeitsplätze stehen als Arbeitsstationen da; eine Person je
      Arbeitsplatz und Schicht.
- [ ] **Umzug aus dem Lagerhaus (0.7)**: Ein Spielstand aus 0.6 mit der Kopfstelle im Lagerhaus: Er lädt, das Netz läuft
      weiter, die Übersicht zeigt den gelben Hinweis „Die Kopfstelle steht noch in einem Lagerhaus …“. In einem Büro
      „Kabelanbieter“ gründen (oder ins Büro des alten Kundencenters gehen) und dort ein CMTS aufstellen: Ereignis „Kopfstelle und Kundencenter sind jetzt im Büro …“,
      Meldung oben rechts; das alte Lagerhaus ist wieder ein gewöhnliches Lagerhaus (BizMan, Lagerhäuser) und seine
      Geräte zählen weiter zum Bestand. Das Personal in MyEmployees dem Büro zuweisen: Es steht wieder unter Personal.
- [ ] **Geräte im Büro (0.7)**: Ein Lagerregal (z. B. von Square Appliances oder Ika Bohag) ins Büro stellen, Kartons vom
      Großhändler darauf: Der Reiter Geräte zählt sie „auf Lager“.
- [ ] **Technik**: Ein Büromöbelgeschäft (das den Computerarbeitsplatz führt) verkauft CMTS 3.0/3.1, OLT, Sat-Kopfstelle,
      CA-System, Telefonie- und IPTV-Plattform und die zwei Arbeitsplätze – im Laden und per Telefon, mit eigenem Symbol.
      Aufgestellt sehen sie aus wie ein umgefärbter großer Schrank bzw. ein Computerarbeitsplatz.
- [ ] Ohne CMTS in der Kopfstelle: rote Zeile in der Übersicht, das Netz steht. Ein CMTS aufstellen: Das Netz läuft
      (Ereignis „steht wieder ein CMTS“). Im Reiter Netz steht bei jedem Teil „steht in der Kopfstelle“ oder „kaufen …“.
- [ ] Unter Finanzen: „Strom und Wartung der Technik“ (z. B. 25 $ am Tag für ein CMTS 3.0).
- [ ] **Geräte (0.6.1)**: Jeder Großhändler (z. B. Hudson Wholesale, Stockco, NY Distro Inc) hat Regale mit
      Kabelmodem 3.0/3.1, Glasfaser-Modem, IPTV-Box und Smartcard; die übrigen Waren sind weiter da. Der Reiter Geräte
      nennt den Großhändler nahe der Kopfstelle. Kartons kaufen, in die Kopfstelle (Regal) bringen: „auf Lager“ zählt
      sie mit; neue Kundschaft nimmt sie heraus.
- [ ] **Import (0.6.1)**: Mit Hauptquartier, Einkaufsagent und einem Lager bietet BlueStone Imports die Geräte im
      Importplan an; was im Lager ankommt, zählt im Reiter Geräte zum Bestand.
- [ ] **Preise eintippen (0.6.1)**: Tarife → „Preis/Tag“, Geräte → Miete und Verkaufspreis, Gebäude → „Straße suchen“:
      Tippen geht, ohne dass das Spiel dabei Tastenkürzel auslöst; Enter oder Klick daneben übernimmt den Wert.
- [ ] **Breite (0.6.1)**: Die Going-Online-Seite nutzt die ganze Breite; in Gebäude ist rechts nichts abgeschnitten
      (mit Glasfaser stehen die Knöpfe in einer zweiten Zeile).
- [ ] **Kartenfilter (0.6.2)**: Voogle Maps → Kartenfilter, ganz unten bei den Unternehmen: „Kabelanbieter“ mit dem
      Going-Online-Bild („Kabel-Kundencenter“ nur bei einem alten Kundencenter aus 0.6). Anhaken: Das Büro leuchtet auf der
      Karte. Der Knopf daneben fährt die Karte zum eigenen Gebäude. Nach dem Neuladen ist der Haken noch gesetzt.
- [ ] **Agenturen (0.6.2, mit Going Public Server)**: Mit beiden Arbeitsplätzen im Büro: City Workforce Inc. →
      Kabel-Guru wählen: Die Liste zeigt Netzmeister, Außendienst und Technische Entstörung; Anderson Recruitment Corp.:
      Kabeltechnik, Schaltdienst und Kundenservice. Im Log steht je Agentur eine Zeile „recruitment at … (GO-HIRE-1)“.
- [ ] **Netzmeister am Hauptsitz (0.6.2)**: City Workforce Inc. → den Hauptsitz wählen: In der Liste steht neben den
      Berufen des Hauptsitzes „Netzmeister“. Einstellen, im Zeitplan des Hauptsitzes eine Schicht an einem Computer geben:
      Die Person steht in der App unter Personal als Netzmeister und nimmt in der Schicht ein Fahrzeug der Kopfstelle.
- [ ] **BizMan-Liste (0.6.2, mit Going Public Server)**: Der Kabelanbieter steht unter „Unternehmen & Immobilien“ als
      „Kabelanbieter (n Anschlüsse)“ mit der Zufriedenheit aus der App, nicht mehr unter „Lagerhäuser“. Ein Klick öffnet
      „Einblicke“; Reiter: Einblicke, Inventar & Preise, Zeitplan, Lieferungen, Fuhrpark, Inventar, Marketing,
      Einstellungen.
- [ ] **Fuhrpark (0.6.2, mit Going Public Server)**: Zwei Leasing-Transporter in der App: Der Reiter „Fuhrpark“ zeigt
      „Fahrzeug 1“ und „Fahrzeug 2“ (Leasing-Transporter – jetzt: … oder gerade frei). „Fest zugeordnet“ bietet
      „Automatisch (jede Schicht neu)“ und die Leute mit Fahrzeug (Kabeltechnik, Schaltdienst, Netzmeister,
      Außendienst). Eine Person wählen: In ihrer Schicht fährt sie dieses Fahrzeug; dieselbe Person bei Fahrzeug 2 wählen
      nimmt sie aus Fahrzeug 1. Ohne Fahrzeuge steht dort, wo man eins herbekommt.
- [ ] **Liefervertrag (0.6.2, mit Going Public Server)**: Im Büro ein Lagerregal aufstellen. Einen
      Großhändler anrufen → „Vertrag starten“: Kabel-Guru steht in der Liste. Annehmen → „In BizMan öffnen“: Unter
      Lieferungen stehen die Geräte. Mengen setzen, Vertrag aktivieren: Am Liefertag um 8 Uhr liegen die Kartons auf dem
      Lagerregal, die App zählt sie unter Geräte „auf Lager“, die Lieferung ist unter Finanzen gebucht.
- [ ] **Personal**: Bei Anderson Recruitment Corp. gibt es „Kabeltechnik“ und „Schaltdienst“, bei City Workforce Inc.
      „Netzmeister“, „Außendienst“, „Technische Entstörung“ (sobald die Arbeitsplätze dafür im Büro stehen). Einstellen, in
      BizMan eine Schicht am Arbeitsplatz geben: Die Person steht im Reiter Personal („Schicht in BizMan“), arbeitet in ihrer Schicht Aufträge ab;
      den Lohn zahlt das Spiel (keine Löhne bei Going Online unter Finanzen).
- [ ] **Berichte**: Am nächsten Tag zeigt BizMan beim Kabelanbieter Umsätze „Kabelanschluss“, „Pay-TV-Paket“, „Gerätemiete“;
      EconoView verbucht sie als Umsatz des Unternehmens (nicht doppelt als Abo-Einnahmen von Going Online).
- [ ] **F1**: Kapitel „Going Online – Kabelanbieter“ mit sieben Seiten.
- [ ] **Sicher entfernen** (Finanzen): Nach „Ja, vorbereiten“ ist der Kabelanbieter ein leeres Büro, die Technik sind große
      Schränke, das Personal hat Berufe des Spiels; speichern, Mod aus, laden: Der Spielstand lädt.
- [ ] Links erscheinen die zehn Reiter (Übersicht, Netz, Gebäude, Tarife, Geräte, Fernsehen, Aufträge, Personal,
      Finanzen, Konkurrenz). Die Übersicht zeigt die Kopfstelle, Ruf 60 und „Erste Schritte“.
- [ ] Die Schritte werden nacheinander „erledigt“: Häuser anschließen, eine Fachkraft, ein Transporter, erste Kundschaft.
- [ ] Ohne Kundenservice und Schaltdienst steht bei Bestellungen und Anschalten „dir“ als Bearbeiter (8–20 Uhr).
- [ ] Der erste Knoten im Viertel der Kopfstelle kostet 6.000 $, in einem anderen Viertel 12.000 $.

## 3. Netz und Gebäude

- [ ] Reiter „Gebäude“: Viertel-Filter, Art-Filter, Stand-Filter, Suche und Seiten funktionieren. Die Einheiten
      passen grob zur Größe der Häuser (große Wohnhäuser mit vielen Haushalten).
- [ ] „Anschließen“ beim ersten Haus eines Viertels kostet den ersten Knoten mit („+1 Knoten“). Der Stand zeigt
      „Koax in 24 h“ und läuft herunter.
- [ ] Nach dem Bau gibt es eine Meldung „… ist angeschlossen“, und Kundschaft kommt (am nächsten Spieltag).
- [ ] Reiter „Netz“: Das Viertel zeigt Knoten, Einheiten, Anschlüsse und das Balkendiagramm der Last pro Stunde. Abends
      ist es höher als nachts.
- [ ] „+ Knoten“ funktioniert. Mit „+“ und „-“ ändert sich die gebuchte Bandbreite samt Preis pro Tag.
- [ ] „Route“ setzt das Navi auf das Haus.

## 4. Tarife und Geräte

- [ ] Name, Tempo (− / +), TV, Preis, Laufzeit und „im Verkauf“ lassen sich ändern. Der Marktpreis färbt sich
      grün, orange oder rot.
- [ ] Ein Tarif mit Kundschaft lässt sich nicht löschen.
- [ ] „Anschlussgebühr“ mit − und + zwischen 0 und 150 $ einstellen. Neue Anschlüsse zahlen sie einmal („Anschlussgebühren“
      in den Finanzen und in EconoView).
- [ ] Reiter „Geräte“: Einkauf (+10/+50/+200) zieht Geld ab („<Firma>: Geräte-Einkauf“). Miete und Kaufpreis lassen
      sich ändern. Leert sich das Lager, kommt eine Warnung, und neue Kundschaft wartet.
- [ ] **Geräte im Regal (0.7.1, GO-DEVICE-VISUALS-1)**: Modems, ONT, Set-Top-Box und TV-Karte zeigen dort, wo das Spiel
      Waren zeigt (z. B. im Regal eines Großhändlers), ihr eigenes Modell statt Smartphone oder Karton. In der Player.log
      steht keine Zeile „game types: no model for ba:itemname_smartphone1“ mehr.

- [ ] **Firma auflösen (0.7.1, GO-DISSOLVE-1)**: Das Büro des Kabelanbieters (und eine alte Lagerhalle) in BizMan kündigen:
      Die App zeigt „Von … ist kein Büro mehr übrig“ und den Knopf „Firma auflösen“; „Ja, auflösen“ → Meldung
      „… ist aufgelöst“, die App zeigt wieder „Firma gründen“, nach Speichern und Neuladen ebenso. Solange noch ein Büro
      mit dem Kabelanbieter da ist, erscheint der Knopf nicht.

## 5. Abrechnung (Mitternacht)

- [ ] In der Finanz-App stehen die Posten „<Firma>: Abo-Einnahmen“, „Gerätemiete“, „Bandbreite“, „Personal“ …,
      auch im Filter als „Going Online: …“.
- [ ] Mit Telefon, Fahrzeugen und Fremdfirmen kommen „Zusammenschaltung und Portierung“, „Sprit und Arbeitsplätze“ und
      „Fremdfirmen“ dazu, und das Geld geht wirklich ab.
- [ ] Reiter „Finanzen“: Tag, Einnahmen, laufende Kosten, Abgaben (10 % vom Gewinn), Ergebnis und investiert.

## 6. Personal, Fahrzeuge und Kundencenter

- [ ] Reiter „Personal“: Je Rolle stehen Bewerbungen bereit (Technik, Schaltdienst, Netzmeister, Außendienst,
      Kundenservice, Technische Entstörung). Einstellen, Schicht durchschalten, entlassen.
- [ ] Ohne Fahrzeug an der Kopfstelle steht bei Technik und Schaltdienst „kein Transporter an der Kopfstelle“.
- [ ] „Leasing-Transporter +“: Er steht sofort unter „Fahrzeuge an der Kopfstelle“, die Fachkraft fährt los, und in der
      Abrechnung stehen 45 $ am Tag unter „Leasing“ (in EconoView „Fahrzeug-Leasing“). „Leasing-Pkw“ genauso mit 30 $.
- [ ] Einen Transporter (Mersaidi Dash, UMC Desert, UMC Nunavut oder Vord Courier D500) kaufen und an der Kopfstelle
      parken (bis 80 m): Er steht unter „Fahrzeuge an der Kopfstelle“, und eine Fachkraft fährt los. Das Fahrzeug, in
      dem man selbst sitzt, zählt nicht.
- [ ] Ein normales Auto an der Kopfstelle zählt als „Pkw“ und geht an Netzmeister oder Außendienst.
- [ ] Arbeitsplätze in der Kopfstelle kaufen (+1/+5): Kundenservice und Entstörung zeigen dann „am Arbeitsplatz“. Die
      Höchstzahl passt zur Größe des Gebäudes.
- [ ] Probelauf (ohne die Typen des Spiels): ein Büro mieten und dort das Kundencenter einrichten. „Platz für …“ passt zur
      Größe (ein Arbeitsplatz je 10 m²). Umzug in ein anderes Büro kostet 200 $ je Arbeitsplatz.
- [ ] Netzmeister: „Viertel“ durchschalten, arbeitet 8–16 Uhr. Ohne Netzmeister dauert ein Bau im Viertel doppelt so
      lange.
- [ ] Der Bedarf je Rolle („Bedarf etwa …, im Team …“) steigt, wenn viel Arbeit wartet, und fällt danach wieder.

## 7. Aufträge, Störungen und Zufriedenheit

- [ ] Reiter „Aufträge“: Neue Kundschaft läuft durch Bestellung, Portierung (bei Telefon), Anschalten und Termin. Ohne
      Kundenservice nimmt man selbst nebenbei ein paar Bestellungen auf (8–20 Uhr).
- [ ] Wer bei Koax kündigt, steht unter „noch Signal ohne Vertrag“, bis der Schaltdienst abschaltet.
- [ ] Störungen tauchen auf (große mit einer Meldung oben rechts). Freie Fachkräfte fahren hin („unterwegs“) und
      arbeiten („arbeitet x %“). Mit Technischer Entstörung werden WLAN-Probleme per Fernzugriff gelöst.
- [ ] „Selbst erledigen“ setzt die Route. Vor Ort läuft der Fortschritt, danach kommt „Repariert“.
- [ ] Eine Störung, die länger als 2 Tage offen ist, kostet Entschädigung („<Firma>: Entschädigungen“).
- [ ] Übersicht: „Warum die Kundschaft zufrieden ist – oder nicht“ zeigt fünf Balken. Mit zu wenig Personal sinkt der
      passende Balken, und daneben steht, was hilft.
- [ ] „Fremdfirma: an“ bei einer Rolle: Aufträge, die über 8 Stunden warten, erledigt „einer Fremdfirma“, und die Kosten
      stehen unter „Fremdfirmen“.
- [ ] „Überstunden: an“: mehr geschafft, höhere Löhne.

## 8. Gestattungsverträge und Außendienst

- [ ] Reiter „Gebäude“ und „Aufträge“: Jedes angeschlossene Haus zeigt „Vertrag bis Tag …“ (zwei Spieljahre).
- [ ] 10 Tage vor dem Ende kommt ein Hinweis, „Verlängern“ verlängert um zwei Spieljahre. Mit Außendienst ist es 30 %
      billiger.
- [ ] „Selbst verlängern: an“ verlängert in den letzten fünf Tagen von allein, auch mit leerer Kasse. Die Kosten stehen
      am nächsten Tag unter „Gestattungsverträge“.
- [ ] Ohne Verlängerung: Ausschreibung. Gewonnen kostet sie 30 % mehr; verloren steht „anderer Anbieter bis Tag …“, und
      die Kundschaft des Hauses ist weg.
- [ ] Der Außendienst ist „unterwegs in …“, und dort kommt an dem Tag mehr Kundschaft.

## 9. Alter, Wartung, Reparaturen, Mengenrabatt

- [ ] Reiter „Finanzen“: „Wartung (nach Alter der Anschlüsse)“ ist am Anfang klein und wächst mit den Spieljahren.
      „Reparaturmaterial“ kommt mit behobenen Störungen (ein Faserbruch kostet 1.800 $).
- [ ] Ab 500 Anschlüssen steht unter dem Tag „Mengenrabatt …“, und die Einkaufspreise im Reiter „Geräte“ sinken.
- [ ] Reiter „Gebäude“: Ein Koax-Haus ab einem Jahr zeigt sein Alter („Koax 1,2 J.“), ab drei Jahren in Orange. Der
      Knopf heißt dann „Erneuern …“; danach ist das Alter weg.

## 10. Umzüge, Entstörungspauschale, Beschwerden

- [ ] Reiter „Tarife“: „Umzugsgebühr“ (0–100 $) und „Entstörungspauschale“ (0–150 $) mit − und + einstellen.
- [ ] Reiter „Aufträge“: Unter „Umzüge“ stehen Mitgezogene, Gebühr und Kündigungen. In der Liste erscheinen „Umzug: alte
      Adresse abklemmen“ und „Umzug: neue Adresse aufschalten“; der Schaltdienst erledigt beides.
- [ ] Eine Störung „Kunde meldet Störung (Ursache unklar)“ zeigt nach der Prüfung die Ursache, zum Beispiel
      „Kundenseitig: TV verstellt“. Am Telefon gelöst kostet sie nichts, nach einem Besuch die Pauschale
      („Entstörungspauschalen“ in den Finanzen).
- [ ] „Beim Kunden“ zählt Besuche, Fehler der Kundschaft und „niemand zu Hause“. Bei „niemand zu Hause“ steht der Auftrag
      danach mit „neuer Besuch in … h“ in der Liste.
- [ ] Reiter „Personal“: Bei der Technik stehen „Umgang x/5“ und die Beschwerden; „Schulung 300 $“ hebt den Umgang um eins
      und ist danach 30 Tage gesperrt. Bewerbungen zeigen den Umgang.
- [ ] Beschwerden erscheinen unter „Beschwerden“ (zum Beispiel „Beschwerde: Techniker unfreundlich (Name)“), der
      Kundenservice arbeitet sie ab. Die Übersicht hat den sechsten Balken „Wenig Beschwerden, rechtzeitig beantwortet“.

## 11. Konkurrenz (Skyline Cable)

- [ ] Reiter „Konkurrenz“: sein Ruf neben deinem, wie hart er kämpft, seine Preise. Je Viertel: Marktanteil, sein Netz,
      seine Preise, Exklusivverträge.
- [ ] Mit wachsendem Anteil in einem Viertel kommen Meldungen: Preissenkung, Rückholaktion, Netzausbau. Sie stehen unter
      „Was er getan hat“, und in der Übersicht steht „wehrt sich in …“.
- [ ] Im Reiter „Tarife“ sinkt der Marktpreis während seiner Preissenkung.
- [ ] Reiter „Gebäude“: Ein Haus mit seinem Exklusivvertrag zeigt „Skyline Cable bis Tag …“ und lässt sich bis dahin
      nicht anschließen.
- [ ] Eine verlorene Ausschreibung: Das Haus zeigt „Skyline Cable bis Tag …“.
- [ ] Optionen → Mods → „Konkurrenz (wie hart sie kämpft)“ auf 0 %: Er macht keine Züge mehr.

## 12. Telefon und IPTV

- [ ] Reiter „Netz“: Telefonie-Plattform kaufen, Rufnummernblock kaufen („+ Block $300“).
- [ ] Reiter „Tarife“: „Telefon“ an einem Internettarif einschalten. Neue Kundschaft bekommt Nummern, viele portieren
      (zwei Tage). Ohne freie Nummern kommt ein Hinweis.
- [ ] IPTV-Plattform kaufen, Set-Top-Boxen einkaufen, bei einem Tarif TV auf „IPTV“ schalten: Kundschaft kommt, die
      Boxen gehen vom Lager, und die Last an den Knoten steigt zur Fernsehzeit.

## 13. Fernsehen und Pay-TV

- [ ] Satellitenanlage kaufen, Transponder hoch: Bei jedem zweiten kostet ein Transmodulator 800 $. Im Reiter „Netz“
      sinkt der Platz für DOCSIS.
- [ ] Ein Nur-TV-Tarif und ein Tarif mit TV bekommen Kundschaft.
- [ ] CA-System kaufen, Smartcards einkaufen, einen Pay-TV-Vertrag abschließen: Abos kommen dazu, und Einkauf samt
      Mindestabnahme erscheinen in der Abrechnung.

## 14. DOCSIS 3.1 und Glasfaser

- [ ] Ab 300 Anschlüssen (oder mit dem Regler früher) kommt eine Meldung, und das CMTS 3.1 lässt sich kaufen. Danach
      „DOCSIS 3.1“ je Viertel (12 h). Tarife über 500 Mbit/s bekommen Kundschaft. „3.0-Modems tauschen“ funktioniert.
- [ ] Ab 1500 Anschlüssen und mit 3.1: OLT kaufen, Splitter 1:32/1:64, „Glasfaser“ an einem Haus (1–4 Tage),
      Glasfaser-Tarif anlegen.
- [ ] „x umziehen“ legt Termine an. Danach laufen die Anschlüsse über Glasfaser, und „Koax abschalten“ ist möglich.

## 15. Speichern

- [ ] Speichern, das Spiel beenden, neu laden: Firma, Häuser, Tarife, Lager, Personal, Arbeitsplätze, Kundencenter,
      Rufnummern, Verträge, Aufträge, Störungen, Leasing-Fahrzeuge und alles von Skyline Cable sind noch da.
- [ ] Ein anderer Spielstand ohne Anbieter zeigt wieder die Gründung.
- [ ] Ohne den Mod lässt sich der Spielstand normal laden.

## 16. Mehrspieler (Going Public Server)

- [ ] Mit dem neuen Server-Mod auf Instanz 3: Die Kachel „Going Online“ steht auf der dritten Handy-Seite, und das
      große Menü hat den Eintrag.
- [ ] Der eigene Mod ist zusätzlich aktiv: Er meldet im Log, dass er sich raushält. Es gibt keine doppelte Kachel.
- [ ] AMP-Schalter „Going Online (Kabelanbieter)“ aus: Die App ist weg. Ein Tipp auf die Kachel sagt, dass das Modul
      aus ist.
- [ ] Gründen, Server verlassen, wieder beitreten: Der Anbieter ist noch da (er reist mit dem Spielstand zum Server).
- [ ] Rangliste des Servers: Nach dem Gründen steht innerhalb von zwei Minuten unter deinem Namen „Kabelanbieter: …“ mit
      „neu“, und der Abschnitt „Kabelanbieter (Going Online)“ zeigt Firma, Anschlüsse, Häuser, Viertel, Ruf und Gründung.
- [ ] Going Online: CMTS, OLT, Sat, CAS, Voice und IPTV kaufen und aufstellen - eigene Schränke mit Namensschild in
      ihrer Farbe, LEDs blinken; Platzieren, Verschieben und Verkaufen wie vorher; Modems/ONT/Set-Top-Box/TV-Karte zeigen
      die neuen Symbole (Lager, BizMan). Player.log: „own models: 12 loaded ... (GO-MODELS-1)“.
- [ ] Going Online: Techniker auf einen Einsatz schicken - im Stadtverkehr fahren bis zu drei weiße Servicewagen mit dem
      Firmenlogo (Seiten und Heck), Leiter und Rundumleuchte; ohne Einsatz werden es wieder normale Autos.
      Player.log: „service vans: N in the traffic ... (GO-VAN-1)“.
- [ ] **Leute zuweisen ohne Arbeitsplatz (0.7.1, GO-ASSIGN-1)**: Mitarbeiter-App, eine Person mit Kabel-Beruf: Unter „Aktuelles
      Unternehmen“ steht der Kabelanbieter, auch bevor im Büro ein Arbeitsplatz steht. Die Personalagenturen bieten weiter nur
      die Berufe der aufgestellten Arbeitsplätze an.
- [ ] **Leitstand-Arbeitsplatz (0.7.1, GO-WORK-3)**: im Büromöbelgeschäft; bringt Schaltdienst und Netzmeister. Die App
      (Personal) zählt Technik-, Leitstand- und Kundencenter-Arbeitsplätze.
- [ ] **LEDs bei Tag (0.7.1, GO-LIGHTS-1)**: Die Lichter der Schränke sind auch in einem hellen Büro deutlich zu sehen.
- [ ] **Grenzen je Gerät (0.7.1, GO-CAP-1)**: Netz-Seite zeigt die Auslastung von CMTS, OLT, Telefonie, IPTV, CA-System; voll =
      neue Kundschaft wartet, bis ein weiteres Gerät steht.
- [ ] **Ein Sat-Gerät, ein Transponder (0.7.1, GO-TV-2)**: Transponder nur so viele wie Sat-Geräte in der Kopfstelle.
- [ ] **Mehr Störungen und Beschwerden (0.7.1, GO-FAULTS-2)**: doppelte Störungsraten, neue Beschwerdegründe.
- [ ] **CMTS-Ports (0.7.1, GO-PORTS-1)**: Netz-Seite „Auslastung CMTS-Ports“ (8 je CMTS). Alle belegt = kein neuer Knoten, kein
      Gebäude, das einen braucht (Ereignis). Mehr Knoten als Ports: „je Knoten“ zeigt weniger, abends langsamer.
- [ ] **Knoten entfernen (0.7.1, GO-NODES-1)**: „− Knoten“ geht nur, solange die übrigen Knoten alle angeschlossenen Einheiten
      abdecken; ein Knoten im Bau wird zuerst abgebrochen.
- [ ] **0.8 Büros und Kundencenter (GO-HUBS-1, GO-CENTERS-1)**: Neuer Anbieter: erst Büro → „Kabelanbieter“ (Kopfstelle), dann
      Laden → „Kabel-Kundencenter“; vorher gibt es keine Kundschaft und keine Gebäude („kein Büro/Center dort“). Ein Kundencenter
      zuerst gründet nichts (Hinweis). Reiter Netz → „Büros und Kundencenter“: jedes Viertel mit Büro, Kundencenter, Zuführung, Ring,
      Personal.
- [ ] **Hub**: zweites Büro in einem anderen Viertel, „Kabelanbieter“, CMTS aufstellen, „Zuführung bauen“ (48 h) → Viertel
      „betreut“, Viertelkarte zeigt „Hub …: CMTS … | Ports …“. „Ring schließen“ → Baggerschaden ohne Ausfall (Ereignis).
- [ ] **Alter Spielstand**: Ereignis „Going Online 0.8 …“, 10 Tage alte Regeln, tägliche Erinnerung; danach in Vierteln ohne
      Büro/Center keine Neukundschaft, Kündigungen steigen leicht.
- [ ] **Personal je Viertel (GO-STAFF-1)**: Reiter Personal: Regeltext und je Gruppe „Anschlüsse … | Technik n (für …) | …“; zu
      wenig Personal → Ereignis „Zu wenig Leute für …“; mehr als 10 Leute je Netzmeister → Netzmeister-Teil gelb.
- [ ] **Kundenservice nur im Kundencenter**: Mitarbeiter-App bietet für das Kundencenter nur Kundenservice an, für Büros die übrigen
      Berufe; die Agentur bietet im Kundencenter Kundenservice, sobald dort ein Kundencenter-Arbeitsplatz steht.
- [ ] **Leben im Kundencenter (GO-LOBBY-1)**: Gerätetisch im Möbelgeschäft (1.800 $) – Theke mit Geräten, Firmenlogo vorne.
      Im geöffneten Kundencenter: Leute kommen herein, schauen am Gerätetisch, setzen sich auf Wartezimmerstühle/Sofas, gehen
      zum Kundenplatz (mit Schicht) und wieder hinaus. Fernseher (z. B. Wandfernseher) zeigen den Spot mit Logo, Name, Tarif,
      Sendern. Player.log: keine Zeile „customer center: … (GO-LOBBY-1)“.
- [ ] **Netzüberwachung (GO-PNM-1)**: Reiter Netz → „Netzüberwachung: an“ → „läuft“ nur mit Netzmeister in der Schicht und
      Leitstand-Arbeitsplatz, sonst „ruht: …“; Finanzen: „Netzüberwachung“ täglich.
- [ ] **Strom und Notstrom (GO-POWER-1)**: USV-Anlage und Notstromaggregat im Möbelgeschäft; Viertelkarte „Akkus für die Knoten“;
      Ereignis „Stromausfall in … für etwa … h: …“, Übersicht „Stromausfälle“, danach „Der Strom ist in … wieder da“.
- [ ] **Upload und High-Split (GO-UP-1)**: Tarife: Spalte „Upload“ (Koax: auto, 10 … 200 Mbit/s; über 50 mit *, nur mit High-Split);
      Viertelkarte „Upload: Spitze heute …“, „High-Split …“ (erst DOCSIS 3.1) → Hausverstärker-Aufträge → „High-Split läuft“.
- [ ] **Fiber Deep (GO-DEEP-1)**: Viertelkarte „Fiber Deep … (n Knoten)“ (DOCSIS 3.1, freie Ports) → nach 72 h mehr Knoten, 128
      Einheiten je Knoten, keine Verstärkerstörungen mehr.
- [ ] **Remote-PHY (GO-RPHY-1)**: vCMTS-Server im Möbelgeschäft; Netz „Auslastung vCMTS-Ports“; Viertelkarte „Remote-PHY …“ →
      Knoten auf den vCMTS-Ports, mehr Kapazität je Knoten.
- [ ] **Streaming-Cache (GO-CACHE-1)**: Möbelgeschäft „Streaming-Cache-Server“ in die Kopfstelle; ab 1.000 Internetkunden „steht“
      grün; Bandbreite: Spitze sinkt.
- [ ] **Netz vermieten (GO-WHOLESALE-1)**: ab 2.000 Anschlüssen Angebot von Funkwelle Mobil (Ereignis); Reiter Netz → „Angebot
      annehmen“, Preis −/+; Leitungen wachsen täglich; Finanzen „Vermietete Leitungen“.
- [ ] **Firmen-SLA (GO-SLA-1)**: Tarif-Schalter „SLA 8 h“; nur Firmen schließen ab; Störungen dort zuerst.
- [ ] **Sammelvertrag (GO-BULK-1)**: Gebäude ab 30 Wohnungen: „Sammelvertrag anbieten“ → am nächsten Tag Ja/Nein; Finanzen
      „Sammelverträge“.
- [ ] **Monitore (GO-SCREENS-1)**: In Büro/Kundencenter zeigen die Going-Online-Arbeitsplätze die App mit wechselnden Seiten.

## 17. Eingabefelder und Servicewagen (0.8.1, 2026-11-09)
- [ ] **Preise eintippen (GO-INPUT-3)**: Tarife → Preis anklicken (Wert blau markiert), Ziffern tippen, Enter. Der neue Preis bleibt
      (auch nach dem Wechsel des Reiters). Ebenso Name, Gerätepreise, Pay-TV-Preis, Straßensuche, Firmenname beim Gründen.
- [ ] **Servicewagen kaufen (GO-VAN-2)**: Autohaus mit Transportern → „Servicewagen“ 26.500 $; mit Logo, fahrbar, zählt im
      Fuhrpark als Transporter (am Büro parken, bis 80 m).
- [ ] **Leasing (GO-VAN-3)**: Personal → Leasing-Transporter „+“ → ein echter Servicewagen parkt vor dem Büro; „-“ nimmt ihn weg.
      Verkaufen bringt 0 $.
- [ ] **Zum Kunden (GO-VAN-4)**: Arbeitet jemand aus der Technik bei der Kundschaft, steht der Wagen dort, danach wieder am Büro; kein Strafzettel.
- [ ] **Verkehr und Rundumleuchte (GO-VAN-5)**: Mit Technik-Leuten im Dienst fahren Servicewagen im Stadtverkehr; Leuchte blinkt.
- [ ] **Form (GO-VAN-6)**: Servicewagen rund statt eckig, Räder mit Radkappen; Räder drehen beim Fahren und lenken mit.


## 18. Beklebung des Servicewagens (0.8.1, GO-LIVERY-1, 2026-11-10)

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
- [ ] **Mehrspieler** (nur mit Going Public Server): Die Servicewagen der anderen zeigen deren Beklebung.
- [ ] Beim ersten Anzeigen einer Beklebung ein kurzer Ruckler (ein Bild wird gemalt) ist normal. Im Spieler-Fehlerbericht stehen
      Zeilen „van livery: …“.
- [ ] **Nur mit Going Public Server** (CAR-LIVERY-1): Im Reiter steht der Hinweis, dass jeder Wagen eine eigene Beklebung bekommen
      kann („Beklebung“ neben „Fahrzeug parken“, jede Seite eigene Gestaltung und Farbe, neue Lackfarbe). Ein so eingestellter Wagen
      behält seine Beklebung, auch wenn du hier etwas änderst; die anderen Wagen folgen diesem Reiter. Im Einzel-Mod Going Online
      fehlt der Hinweis.

## 19. Ereignisse, Netz in der Stadt, Leitungen an Mitspieler, mehr Vorlagen (2026-11-11)

- [ ] **Ereignisse** (GO-EVENTS-1): Nach einigen Spieltagen kommt um 9 Uhr ab und zu eine Meldung (Unwetter, Fußballspiel,
      Bagger, Zeitungsbericht, Testsieger, Glasfaser-Förderung). In der Übersicht steht oben „Ereignisse“ mit dem Text,
      zwei Knöpfen und der Frist. Ein Knopf entscheidet; ohne Entscheidung gilt nach der Frist die kostenlose Wahl.
      Kosten und Erstattungen stehen am nächsten Tag unter Finanzen („Ereignisse (Sondermaßnahmen)“, „Erstattungen und
      Förderung“). Player.log: „event … (GO-EVENTS-1)“.
- [ ] Unwetter mit Bereitschaft: während des Sturms viele Störungen im Viertel, die Technik arbeitet sie schneller ab.
- [ ] Fußballspiel: abends mehr Last; mit Sport-Aktion mehr TV-Abschlüsse; danach eine Zeile, ob das Netz gehalten hat.
- [ ] **Netz in der Stadt** (GO-STREET-1): Neben der Tür jedes angeschlossenen Hauses steht auf dem Gehweg ein grauer
      Verteilerkasten mit einem Band in der Farbe der Wagen-Beklebung, nicht vor der Tür, nicht auf der Straße, nicht in
      Wänden. Während ein Haus angeschlossen wird: Absperrschranken (Leuchte blinkt) und Kegel davor. Arbeitet jemand der
      Technik dort: zwei Kegel. Kein Ruckeln beim Fahren durch angeschlossene Viertel. Player.log: „street: N cabinets …
      (GO-STREET-1/2)“.
- [ ] Verteilerkästen mit Schild (GO-STREET-2): vorne Logo und Firmenname, lesbar von vorn. Mit zwei Spielern auf dem
      Server sieht jeder auch die Kästen des anderen (mit dessen Logo, Name und Farbe), auch nachdem der andere offline
      gegangen ist; ein Haus mit beiden Firmen hat zwei Kästen nebeneinander.
- [ ] **Leitungen an Mitspieler** (GO-WHOLESALE-2, nur Going Public Server, zwei Spieler mit Kabelanbieter): Spieler A
      klickt im Reiter „Netz“ unter „Leitungen mit Mitspielern“ auf „Mein Netz anbieten“ (Preis mit −/+). Spieler B sieht
      das Angebot binnen einer Minute unter „Netze anderer Spieler“ und klickt „Leitungen mieten“. Ab dem nächsten Tag hat
      B dort Leitungen („… Leitungen von dir, … am Tag“), B zahlt „Leitungen von Mitspielern“ und bekommt „Abos auf Netzen
      von Mitspielern“, A bekommt „Leitungen an Mitspieler“ (auch, wenn A zwischendurch offline war – dann beim nächsten
      Verbinden). „Kündigen“ beendet es. Server-Log: „lines: a bill of … (GO-WHOLESALE-2)“.
- [ ] **Mehr Vorlagen** (GO-BLUEPRINT-3): Kristian Bahood bietet die Kundencenter für A1, A2 und C1 an, Kingdom Office die
      Kabel-Büros für A3, C1, D2 und J1 (Start und Voll). Kein Arbeitsplatz meldet „Fehlende Anforderungen: Stuhl,
      Schreibtisch“ (GO-BLUEPRINT-6). Nach dem Einbau steht nichts in Wänden oder vor Türen, die
      Arbeitsplätze funktionieren.
