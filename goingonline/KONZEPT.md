# Going Online – Konzept

Ein Mod für Big Ambitions: Du gründest einen Kabelnetzbetreiber und verkaufst Internet und Kabelfernsehen an die
Häuser der Stadt. Es gibt ihn als eigenen Mod für das Einzelspiel und als Modul in Going Public Server (AMP-Schalter
`ModuleGoingOnline`). Beide nutzen denselben Code (`core/`).

Vorgaben des Spielers (2026-11-06):
- Internet Service Provider und Kabel-TV-Anbieter.
- Bandbreite einkaufen und an die Häuser weiterverkaufen. Die Größe eines Hauses bestimmt, wie viele Kunden es hat.
- Tarife, Mietmodems oder Kaufmodems.
- Störungen, die Techniker beheben.
- Technik: Start mit DOCSIS 3.0, danach DOCSIS 3.1 und später im Spiel FTTH mit XGS-PON.
- Fernsehen über Satellit, Pay-TV als Wiederverkäufer.
- Wie ein echter Kabelnetzbetreiber (Recherche siehe unten).
- Telefon mit Rufnummerneinkauf und Portierung, dazu IPTV.
- Mehrere Rollen zum Einstellen: Netzmeister je Gebiet (Arbeitsplatz und Auto), Techniker für Störungen und
  Kundentermine (Auto), Schaltdienst für das An- und Abschalten bei DOCSIS (Auto), Kundenservice (Arbeitsplatz),
  Technische Entstörung (Arbeitsplatz).
- Ein Kundencenter, in das so viele Leute passen, wie der Gebäudetyp hergibt.
- Außendienst für neue Kunden. Gestattungsverträge haben eine Laufzeit und müssen danach neu gemacht werden, sonst kann
  ein anderer Anbieter das Haus übernehmen.
- Eine Lösung für schlechte Kundenzufriedenheit, wenn Technik, Schaltdienst oder Kundenservice fehlen.
- Eigene Fahrzeuge (Wunsch: ein VW Caddy) gibt es nicht: Markennamen gehen nicht, und das Spiel hat schon Transporter.
  Der Anbieter nutzt die Fahrzeuge, die der Spieler kauft und an der Kopfstelle parkt, oder geleaste.
- Eine Konkurrenzfirma.
- Mit Startkapital anfangen können (ein neues Spiel beginnt mit 4.200 $).
- In der Rangliste des Servers steht, wer so eine Firma gegründet hat.
- Am Anfang wenig Gewinn, je mehr Kunden, desto mehr Gewinn. Je neuer der Anschluss, desto billiger die Wartung. Bei
  Kabelschäden ist alt (Koax) billig und neu (Glasfaser) teuer.
- Eine Umzugsgebühr, die den Schaltdienst auslöst: an der alten Adresse abklemmen, an der neuen aufschalten.
- Eine Entstörungspauschale, wenn der Fehler bei der Kundschaft liegt: Modem defekt, TV verstellt, Telefon defekt,
  Strom aus, Anschlusskabel defekt, Kunde nicht anwesend.
- Beschwerden, zum Beispiel „Techniker unfreundlich“.
- (0.6) Wie bei „IT Business Expansion“, „Skill Kit“ und „Spotless“: der Kabelanbieter als echte Unternehmensart des
  Spiels, die Technik als Möbel aus den Läden, Geräte von Lieferanten, die Rollen als echte Berufe der Personalagenturen,
  Hilfe unter F1 und „sicher entfernen“ – „alles in einem Rutsch“. Eigener Code, keine Abhängigkeit von diesen Mods.

## Im Spiel verankert (0.6, Büro seit 0.7)

Alles läuft über die Wege, die das Spiel für Mods vorsieht (keine Spieldatei geändert). Seit 0.8.1 setzt der eigene Mod wie
Going Public Server Harmony ein, damit der Kabelanbieter auch in den Seiten des Spiels steht (GO-PATCHES-1, siehe unten):
- **Unternehmensart** (`BAModAPI.ModdingAPI.RegisterModBusinessType`): „Kabelanbieter“ im **Büro** (nachgebildet nach
  dem Hauptsitz) – Kopfstelle und Kundencenter unter einem Dach (GO-OFFICE-1, 0.7, Wunsch des Spielers: „die Kopfstelle
  kann man ja im Kundenbüro errichten, damit würde das Lager entfallen, und Kundenbüro mit Kopfstelle nur in Büroräume“).
  BizMan bietet es unter „Neues Unternehmen gründen“ an. Wird so ein Unternehmen gegründet, gründet Going Online den
  Kabelanbieter damit (`core/GameFound.cs`) und öffnet es rund um die Uhr. „Kabel-Kundencenter“ bleibt für Spielstände
  aus 0.6 eingetragen, wird aber nicht mehr angeboten. Eine Kopfstelle aus 0.6 im Lagerhaus zieht von selbst in ein Büro
  des Kabelanbieters (oder sein Kundencenter) um, sobald dort ein CMTS steht; das Lagerhaus wird wieder ein Lager.
- **Technik als Möbel** (`ItemsGetter.RegisterModItem`, Verkauf über `Services.ContractItemsForSaleService` in den
  Büromöbelgeschäften, die den Computerarbeitsplatz führen): CMTS 3.0 (25.000 $, 25 $ Strom/Tag), CMTS 3.1 (80.000 $,
  60 $), OLT (150.000 $, 90 $), Sat-Kopfstelle (25.000 $, 20 $), CA-System (40.000 $, 30 $), Telefonie-Plattform
  (30.000 $, 30 $), IPTV-Plattform (60.000 $, 50 $). Das Netz läuft mit dem, was in der Kopfstelle steht; ohne CMTS
  steht es still. Dazu zwei Arbeitsplätze (2.500 $): Technik-Arbeitsplatz (Kabeltechnik, Schaltdienst, Netzmeister) und
  Kundencenter-Arbeitsplatz (Kundenservice, Technische Entstörung, Außendienst, Netzmeister).
- **Modelle**: Schränke und Arbeitsplätze des Spiels, umgefärbt, unter eigenem Namen im Modell-Speicher des Spiels
  (`Helpers.PrefabHelper`, so sucht das Spiel das Modell einer Mod-Ware). Eigene Symbole (`tools/make_icons.py`).
- **Geräte als Ware**: Kabelmodem 3.0 (45 $), 3.1 (95 $), Glasfaser-Modem (70 $), IPTV-Box (55 $), Pay-TV-Smartcard
  (12 $) bei den Elektronik-Großhändlern und Importeuren der Stadt. Das Lager ist ein Lagerregal im Büro (`Gear`);
  zurückgegebene Geräte werden aufbereitet und gehen zuerst raus.
- **Berufe** (der Katalog des Spiels, `SkillHelper`): Kabeltechnik und Schaltdienst bei Anderson Recruitment Corp.,
  Netzmeister, Außendienst und Technische Entstörung bei City Workforce Inc., dazu der Kundenservice des Spiels. Das
  Personal sind Angestellte des Spiels mit Arbeitsplatz und Schicht in BizMan; das Spiel zahlt die Löhne (`Crew`).
  Umgangston, Training und Beschwerden bleiben Sache des Kabelanbieters. **Berufe nach Arbeitsplätzen** (GO-WORK-1,
  0.7): Die Personalagenturen bieten genau die Berufe an, deren Arbeitsplätze im Büro stehen (`GameTypes.UpdateJobs`
  setzt die Berufsliste der Unternehmensart danach; ohne Arbeitsplatz sagt eine Meldung, welcher fehlt).
- **Berichte**: Abos, Pay-TV und Gerätemiete gehen als Verkäufe in den Tagesbericht des Unternehmens „Kabelanbieter“;
  das Spiel zahlt sie um Mitternacht aus (im Mehrspieler der Rechner des Besitzers, wie bei jedem Laden). Alle anderen
  Buchungen tragen die Adresse der Kopfstelle.
- **Hilfe (F1)**: ein Kapitel „Going Online – Kabelanbieter“ mit sieben Seiten.
- **Sicher entfernen** (Reiter Finanzen): Kabelanbieter → leeres Büro (eine Kopfstelle aus 0.6 → Lagerhaus), Kundencenter → leeres Büro, Technik und
  Arbeitsplätze → die Möbel, aus denen sie gemacht sind, Geräte im Lager weg, Berufe → die Berufe des Spiels.
- **Alte Spielstände** (vor 0.6): Was in der App gekauft war, zählt weiter (`IspState.AppGear`); in der App
  eingestelltes Personal bleibt, bis es gekündigt wird. Der Selbsttest rechnet wie bisher ohne Spieldinge.

## Zeit und Geld

- Das Spiel rechnet Miete und Löhne pro Tag ab. Der Anbieter rechnet deshalb jede Spielstunde (Netzlast, Störungen,
  Techniker) und jeden Spieltag (Abrechnung, neue Kunden, Kündigungen).
- Preise gelten **pro Tag**.
- Buchungen landen als eigene Posten in der Finanz-App des Spiels (EconoView), etwa „Abo-Einnahmen“ oder „Bandbreite“.
- Investitionen werden sofort bezahlt und brauchen genug Geld. Laufende Kosten werden wie die Miete auch ins Minus gebucht.
- Abgaben: 10 % des Tagesgewinns (der Steuersatz des Spiels), nur wenn der Tag Gewinn bringt. Die Einnahmen tauchen nicht
  in der Steuererklärung des Spiels auf, und die Ausgaben mindern dort auch nichts.

## Die Stadt (aus den Spieldateien, 884 Gebäude, 7 Viertel)

| Art | Anzahl | Fläche (Median) | Kunden im Mod |
|---|---|---|---|
| Wohnhaus | 363 | 1.448 m² | 1 Haushalt je 90 m² (Hamptons: eine Villa = 1 Haushalt) |
| Ladenhaus | 306 | 1.879 m² | Wohnungen darüber (60 % der Fläche, 90 m² je Haushalt) und der Laden als Firmenkunde |
| Bürohaus | 86 | 8.258 m² | 1 Firmenkunde je 450 m² |
| Lagerhaus | 74 | 3.238 m² | 1 Firmenkunde |
| Sonstige (Kino, Theater, Sonderbauten) | 54 | – | keine |

Das ergibt 21.623 Haushalte und 2.814 Firmen (so liest der Mod die Stadt im Spiel). Die meisten Haushalte liegen in
Midtown (7.404) und Murray Hill (4.540). Firmen zahlen bis zum 1,6-Fachen, Villen bis zum Doppelten. Firmen
surfen tagsüber, Haushalte abends.

## Kopfstelle

- Gründung in der App: Firmenname, eine Kopfstelle in einem Lagerhaus oder Büro, das du mietest oder besitzt, und das
  Startkapital (siehe unten).
- Technik: ein kleines CMTS für DOCSIS 3.0 für 25.000 $.
- Endet die Miete, fällt das ganze Netz aus, bis die Kopfstelle umgezogen ist (Umzug 10.000 $).
- Ausbau: CMTS DOCSIS 3.1 (80.000 $), OLT für XGS-PON mit RF-Overlay fürs Fernsehen (150.000 $), Satellitenanlage
  (25.000 $), Transmodulatoren (800 $ je zwei Transponder), CA-System für Pay-TV (40.000 $), Telefonie-Plattform
  (30.000 $), IPTV-Plattform (60.000 $).
- Arbeitsplätze (Schreibtisch, PC, Headset) für 2.500 $ und 4 $ am Tag. Wie viele hineinpassen, hängt vom Gebäude ab
  (siehe Kundencenter).

## Büros, Hubs und Kundencenter (0.8)

Wünsche des Nutzers (2026-11-08): „Sonst kann ein kleines Büro die ganze Stadt verwalten … In jedem Viertel muss mindestens ein
Kundencenter und Büro sein.“ Seine Antworten: Büro und Kundencenter im Viertel. Die Büros haben Personal und Kopfstelle, die
Kundencenter (Einzelhandel) die Geräte. Das Kundencenter ist ein Laden mit Kundschaft. Ohne beides kündigt die Kundschaft nach
und nach. Dazu kamen „Hubs und Glasfaserring“ sowie feste Personalgrenzen. Zur Reihenfolge sagte er: „Erst Büro als Kopfstelle,
dann Kundencenter für Kunden (Einzelhandel) – müssen für den Start vorhanden sein.“

- **Kopfstelle:** das erste Büro („Kabelanbieter“). Dort stehen Sat-Fernsehen, IP-Transit, Telefonie, IPTV und das CA-System
  für die ganze Stadt.
- **Hub:** ein Büro von dir in einem anderen Viertel. Es versorgt sein Viertel, sobald die Glasfaser-Zuführung von der
  Kopfstelle gebaut ist und dort ein CMTS steht. Das kostet 5.000 $ plus 35 $ je Meter, Bauzeit 48 h, ohne Netzmeister doppelt
  so lang.
  - CMTS und OLT des Hubs zählen für Modems, Ports und Glasfaser dieses Viertels.
  - Sein Personal arbeitet dort und nimmt zuerst die Aufträge des eigenen Viertels.
  - Ein Viertel ohne eigenen Hub hängt an der Kopfstelle, wie das ganze Netz vor 0.8.
- **Ring:** ein zweiter Weg für die Glasfaser, zum nächsten anderen Hub oder getrennt zur Kopfstelle (1,3-mal so lang). Kosten
  wie die Zuführung.
  - Ein Bagger kappt eine Zuführung mit 0,15 % je Tag. Ohne Ring ist das ganze Viertel offline, bis ein Spleißtrupp aus zwei
    Leuten 8 Stunden gearbeitet hat (Material 2.500 $).
  - Mit Ring ist niemand offline, aber die Zuführung muss trotzdem repariert werden.
- **Kundencenter:** „Kabel-Kundencenter“ in einem Laden (Einzelhandel). Dort arbeiten Kundenservice und Reinigung, dort liegen die
  Geräte (sie werden zuerst aus den Kundencentern genommen). Ein Kundencenter allein gründet nichts. Ein Kundencenter-Büro aus 0.6
  wird zu einem Büro des Anbieters.
- **Regel:** Neue Kundschaft und neue Gebäude gibt es nur in Vierteln mit eigenem Büro (Kopfstelle oder Hub mit Zuführung und
  CMTS) und Kundencenter. Anderswo kündigen täglich 0,6 % mehr.
  - Ein Anbieter aus der Zeit vor 0.8 behält die alten Regeln noch 10 Tage, mit täglicher Erinnerung.
  - Ein neuer Anbieter startet sofort mit den neuen Regeln.
- **Berufe je Ort:** Im Büro arbeiten Technik, Schaltdienst, Netzmeister, Technische Entstörung und Außendienst. Im Kundencenter
  arbeitet der Kundenservice. Die Reinigung ist der Beruf des Spiels für das Gebäude.

### Leben im Kundencenter (GO-LOBBY-1)

Idee des Nutzers (2026-11-08): „Im Kundencenter ein Tisch mit den Geräten für die Kunden und ein TV mit einem Spot vom Namen
des Kabelanbieters, Warteplätze, bevor ein Kunde zum Kundenplatz hingeht.“

- Solange das Kundencenter geöffnet ist und du drinnen bist, kommen Leute herein. Je 150 Anschlüsse im Viertel kommt eine
  Person mehr, höchstens 6. Es sind die Kundenfiguren des Spiels, aber ohne seinen Ablauf.
- Ihr Weg: zum **Gerätetisch** (eigenes Möbelstück, 1.800 $, Modell godevicetable: eine Theke mit Modems, ONT und
  Set-Top-Box sowie deinem Logo vorne), dann auf einen **Warteplatz** (jeder Sitz des Spiels: Wartezimmerstühle, Sofas,
  Bänke), dann zum freien **Kundenplatz** (Kundencenter-Arbeitsplatz mit jemandem in der Schicht), zum Schluss hinaus. Ist
  niemand am Kundenplatz, gehen sie nach einer Weile wieder.
- Jeder **Fernseher** im Kundencenter zeigt deinen Werbespot: Logo, Firmenname, schnellster Tarif mit Preis, Zahl der
  Sender und „Jetzt wechseln“, alle 5 Sekunden ein neues Bild.
- Wo ein Kundencenter steht, holt neue Kundschaft ihr Gerät dort ab. Der Versand (6 $) entfällt.

### Netzüberwachung (GO-PNM-1)

- Grundlage ist Proactive Network Maintenance: Software liest die Messwerte von CMTS und Modems. Hersteller nennen 10 bis 40 %
  weniger Technikerfahrten (ZCorum, VIAVI, Broadband Library).
- Sie wird in der App eingeschaltet und kostet 40 $ am Tag plus 0,01 $ je Modem. Sie arbeitet, solange ein Netzmeister in der
  Schicht ist und ein Leitstand-Arbeitsplatz in einem Büro steht.
- Wirkung:
  - Netzstörungen und CMTS-Ausfälle sind sofort eingegrenzt: 20 % weniger Arbeit, und nur halb so viele Betroffene rufen an.
  - Verstärker-, Rückweg- und Hausstörungen werden um 25 % seltener.
  - Die Technische Entstörung repariert Geräte 20 Prozentpunkte öfter aus der Ferne.

### Strom und Notstrom (GO-POWER-1)

- Grundlage: Verstärker und Knoten werden über das Koaxkabel aus Netzteilen mit Akkus gespeist. Kopfstellen haben USV und
  Dieselaggregat. Ein Beispiel für eine Vorgabe sind 2 Stunden Pufferzeit (Yolo County).
- Ein Stromausfall der Stadt trifft ein Viertel mit Netz oder Büro mit 1,2 % je Tag und dauert 1 bis 5 Stunden. Die Hälfte der
  Wohnungen dort ist selbst dunkel und zählt nicht als Ausfall.
- **Akkus je Viertel:** 600 $ je Knoten, Unterhalt 0,50 $ je Knoten und Tag. Sie überbrücken 2 Stunden; neue Knoten bekommen
  ihre Akkus mit. Ohne Akkus sind die Knoten sofort aus.
- **Büro:**
  - Ohne Notstrom wird es sofort dunkel.
  - Mit **USV** (12.000 $) läuft es 1 Stunde weiter.
  - Mit **Notstromaggregat** (35.000 $, 15 $ Diesel je Stunde) läuft es den ganzen Ausfall.
  - Ein dunkler Hub legt sein Viertel lahm. Die dunkle Kopfstelle legt Fernsehen, Internetleitung und Telefon der ganzen Stadt
    lahm.
- Danach ist jedes Netzteil eines Knotens mit 5 % kaputt (Störung „Netzteil“).

### Upload und High-Split (GO-UP-1)

- Grundlage: Der Rückweg im Kabel liegt in Europa bei 5 bis 65 MHz. High-Split (204 MHz) bringt bis 1 Gbit/s Upload je Cluster,
  braucht aber neue Verstärker und Muffen, und alte Hausverstärker mit Rückweg nur bis 65 MHz werden getauscht (Vodafone,
  inside-digital, O2-Forum).
- **Tarif-Upload:**
  - Glasfaser ist symmetrisch.
  - Koax hat automatisch ein Zehntel des Downloads (5 bis 50 Mbit/s) oder einen gewählten Wert: 10, 25, 50, 100 oder
    200 Mbit/s, nie mehr als der Download.
  - Über 50 Mbit/s gibt es ihn nur mit High-Split im Viertel.
  - Mehr Upload ist der Kundschaft 0,004 $ je Mbit/s und Tag wert.
- **Last:** Je Koax-Anschluss 12 % der Download-Spitze plus 0,004 × Upload. Firmen nutzen den Upload tagsüber, Haushalte
  gleichmäßiger.
- **Kapazität je Knoten:**
  - DOCSIS 3.0: 120 Mbit/s
  - DOCSIS 3.1: 200 Mbit/s
  - High-Split: 900 Mbit/s
  - Rückwegstörer kosten bis 70 % davon. Ist der Upload voll, gilt die Hälfte der Koax-Kundschaft als gebremst.
- **High-Split je Viertel:**
  - Voraussetzung ist DOCSIS 3.1. Kosten: 7.000 $ je Knoten, 48 h.
  - Danach je Koax-Gebäude ein Auftrag „Hausverstärker tauschen“ (0,75 h, 80 $ Material).
  - Wenn der letzte erledigt ist, läuft High-Split.

### Fiber Deep (GO-DEEP-1)

- Grundlage: Knoten für 50 bis 128 Haushalte ohne Verstärker (NCTA/SCTE 2017).
- Voraussetzung ist DOCSIS 3.1. Jeder Knoten nach dem Umbau kostet 5.500 $; die Zahl richtet sich nach den angeschlossenen
  Einheiten, je 128 ein Knoten. Bauzeit 72 h. Die zusätzlichen Knoten brauchen freie Ports.
- **Danach:**
  - keine Verstärkerstörungen
  - Rückwegstörer halbiert
  - Netzteilstörungen 40 % weniger
  - 128 Einheiten je Knoten, also viel mehr Kapazität je Anschluss

### Remote-PHY (GO-RPHY-1)

- Grundlage: Die PHY-Schicht sitzt im Knoten hinter digitaler Glasfaser, der Rest des CMTS läuft als Software (vCMTS) im Büro
  (Broadband Library).
- **vCMTS-Server** (Möbelstück, 120.000 $, 30 $ am Tag): 64 Remote-PHY-Knoten und 2.000 Modems, kann DOCSIS 3.1. Er zählt auch
  als CMTS eines Büros.
- **Remote-PHY je Viertel:** 3.000 $ je Knoten, 24 h. Voraussetzung sind genug freie vCMTS-Ports in der Gruppe.
- **Danach:**
  - Die Knoten hängen an den vCMTS-Ports statt an den CMTS-Ports.
  - Jeder Kanal trägt 15 % mehr.
  - Rückwegstörer 30 % weniger
  - Zusammen mit Fiber Deep ist das der Weg zu vielen kleinen Knoten.

### Streaming-Cache, Netz vermieten, Firmen-SLA, Sammelvertrag (GO-CACHE-1, GO-WHOLESALE-1, GO-SLA-1, GO-BULK-1)

- **Streaming-Cache:**
  - Grundlage: Streamingdienste stellen Anbietern Cache-Server kostenlos ins Netz; nachts befüllt, versorgen sie den Abend
    von innen (Analysys Mason 2022).
  - Möbelstück für die Kopfstelle: 500 $ Einbau, 8 $ am Tag. Ab 1.000 Internetkunden nimmt jeder ein Fünftel des Verkehrs
    von der Internetleitung, höchstens zwei. Nachts zwischen 3 und 5 Uhr füllen sie sich mit je 400 Mbit/s.
- **Netz vermieten:**
  - Vorbild: O2 verkauft seit 2019 Internet über das Vodafone-Kabelnetz.
  - „Funkwelle Mobil“ (erfunden) bietet ab 2.000 Anschlüssen und Ansehen 50 an, Leitungen zu mieten (1,10 $ je Leitung und
    Tag, einstellbar 0,30 bis 3 $).
  - Die Leitungen wachsen um 3 % des Abstands auf 8 % der versorgten Wohnungen betreuter Viertel, weniger bei hohem Preis
    oder vollen Abenden.
  - Sie belasten Knoten, Rückweg und die Modemplätze der CMTS, nicht die eigene Internetleitung. In Vierteln mit Mietleitungen
    kommt 10 % weniger eigene Neukundschaft.
  - Ausfälle kosten bis 30 % des Mietpreises.
- **Firmen-SLA:** Ein Tarif-Schalter „SLA 8 h“. Nur Firmen nehmen ihn; er ist ihnen 1,6-mal so viel wert. Ihre Störungen
  gehen vor (+12 Dringlichkeit). Mehr als 8 Stunden Ausfall am Tag kosten je solchem Anschluss einen halben Tagespreis.
- **Sammelvertrag:**
  - Für Wohnhäuser ab 30 Wohnungen: Fernsehen für alle Wohnungen zu 0,35 $ je Wohnung und Tag, über zwei Spieljahre.
  - Der Eigentümer antwortet am nächsten Tag; die Zusage-Chance ist 0,3 + 0,6 × Ansehen/100, höchstens 85 %. Nach einem Nein
    kann man frühestens nach 10 Tagen neu anbieten.
  - In solchen Häusern gibt es keine Nur-TV-Tarife mehr, aber 30 % mehr Internet-Neukundschaft.
  - Am Ende wird der Vertrag mit 70 % verlängert.

### Monitore der Arbeitsplätze (GO-SCREENS-1)

Idee des Nutzers: „Auf den Monitoren könnte auch irgendwas ablaufen, so als wenn die Mitarbeiter sich bei Going Online
durchklicken.“

- In deinen Büros und Kundencentern zeigen die Bildschirme der Going-Online-Arbeitsplätze die App mit echten Zahlen: Aufträge,
  Netzauslastung je Viertel, Übersicht, Personal.
- Eine Markierung wandert durch die Zeilen, und die Seite wechselt alle 5 bis 11 Sekunden.
- Der Bildschirm ist das zweite Teil des Spielcomputers (Material „ScreenMaterial“, 61 × 34 cm).

### Wer wie viele betreut (GO-STAFF-1)

Wunsch des Nutzers: „Festlegen, wie viel ein Personal (Technik, Kundendienst, Schalter) bedient und wie viel max. ein Meister
an Personal unter sich hat.“

- Eine Person betreut in ihrer Gruppe (dem Viertel ihres Büros oder Kundencenters):
  - Technik: 400 Anschlüsse
  - Schaltdienst: 800 Koax-Anschlüsse
  - Kundenservice: 500 Anschlüsse
- Du selbst betreust 200 an der Kopfstelle, eine Fremdfirma je Gruppe 300 mehr. Darüber hinaus wartet neue Kundschaft. Zusammen
  sind das etwa 6 bis 7 Leute je 1.000 Anschlüsse; die Branche hat etwa 7 Beschäftigte je 1.000 Kundinnen und Kunden (Benton
  2022).
- Ein Netzmeister führt bis zu 10 Leute aus Technik und Schaltdienst. Wer darüber hinausgeht, arbeitet zu 75 %. Netzmeister
  braucht es also je Viertel und je 10 Leute im Außeneinsatz.

## Start und Startkapital

Ein neues Spiel beginnt mit 4.200 $ (Einstellung „normal“). Eine Bank leiht einem Neuling nur wenig: höchstens das
Vermögen, im Tutorial 15.500 $ oder ein Viertel der Einnahmen über die Laufzeit (so rechnet der Bankdialog des Spiels).
Das reicht für keine Kopfstelle. Den Kabelanbieter gründet man, wenn das erste Geschäft genug abwirft, oder mit einem
Kredit der Bank des Spiels. (Ein eigener Gründerkredit war am 2026-11-06 kurz drin und ist am selben Tag wieder raus:
„warum Kredit – raus damit“.)
- **Gründen in BizMan** (BIZMAN-FOUND-1): Bei einem leeren Lagerhaus, das man mietet oder besitzt, steht im
  Gründungsdialog des Spiels neben „Lagerhaus“ und „Fabrik“ die Karte „Kabelanbieter“. Das Spiel gründet dann ein
  Lagerhaus unter dem eingegebenen Namen (mit seinen eigenen Prüfungen, Öffnungszeiten und Logo), und der Kabelanbieter
  wird mit diesem Lagerhaus als Kopfstelle gegründet. Dabei wird das CMTS bezahlt. Mit weniger als 25.000 $ lässt sich
  die Karte nicht wählen. Für ein Büro geht die Gründung nur in der App: Für Büros hat das Spiel nur Geschäfte mit
  Kundschaft oder den Hauptsitz.
- In der App wählt man ein gemietetes oder eigenes Lagerhaus oder Büro. Hat es schon ein Geschäft, wird dessen Name
  als Firmenname vorgeschlagen.
- Ein Transporter kostet beim Händler 8.000 $ (UMC Desert) bis 72.500 $ (Vord Courier D500). Statt zu kaufen kann man
  ihn **leasen**: 45 $ am Tag (ein Pkw 30 $), ohne Anzahlung; geleaste Fahrzeuge stehen immer an der Kopfstelle.
- Der Start im Kleinen: das CMTS (25.000 $), der erste Knoten im Viertel der Kopfstelle (dort nur 6.000 $, weil die
  Glasfaser kurz ist; in anderen Vierteln 12.000 $), die größten Häuser nahe der Kopfstelle, eine Fachkraft mit
  Leasing-Transporter. Die Übersicht zeigt dazu „Erste Schritte“.
- **Der Gründer packt mit an:** Zwischen 8 und 20 Uhr arbeitet der Spieler selbst mit, die halbe Zeit (6 Stunden am
  Tag): erst die Bestellungen, dann die Anrufe, dann Schaltaufträge in den Häusern (ohne Fahrzeug). Für den Anfang
  braucht es deshalb weder Kundenservice noch Schaltdienst.
- **Anschlussgebühr:** Jeder neue Anschluss zahlt einmal eine Gebühr (voreingestellt 40 $, im Reiter „Tarife“ von 0 bis
  150 $). Sie bezahlt am Anfang im Grunde die Geräte. 40 $ nimmt die Kundschaft hin; je 10 $ mehr unterschreiben gut 3 %
  weniger, ohne Gebühr bis zu 13 % mehr.
- **Größe zahlt sich aus:** Ab 500 Anschlüssen gibt es Mengenrabatt auf Geräte, Set-Top-Boxen, Smartcards, den
  Pay-TV-Einkauf und die Zusammenschaltung, steigend bis 20 % bei 5.000 Anschlüssen. Bandbreite wird mit der Menge
  ohnehin billiger. Am Anfang bleibt deshalb wenig übrig, mit mehr Kunden immer mehr.

## Konkurrenz: Skyline Cable

Der alteingesessene Kabelanbieter der Stadt (der Name ist erfunden). Er hat anfangs alle Haushalte. Wo der Spieler ihm
Kundschaft abnimmt, wehrt er sich. Er entscheidet jeden Tag je Viertel des Spielers:

| Zug | Wann | Wirkung |
|---|---|---|
| Preissenkung | ab 15 % Anteil an den erreichten Einheiten, 6 % Chance am Tag (mehr bei mehr Anteil) | 6–10 % billiger für 12–21 Tage, danach 1 % am Tag zurück; höchstens 20 % unter dem Marktpreis |
| Rückholaktion | ab 8 % Anteil, 4 % am Tag | 6 Tage: 8 % billiger, 20 % weniger Neukunden, 0,3 % mehr Kündigungen am Tag |
| Netzausbau | ab Tag 25 (DOCSIS 3.1) bzw. 60 (Glasfaser) nach der Gründung, 4 % am Tag | nach 10 Tagen bis 1.000 bzw. 2.000 Mbit/s; Power-Nutzer wechseln nur noch für einen Tarif, der mithält |
| Exklusivvertrag | 8 % am Tag | eins der größten noch freien Häuser (ab 10 Einheiten) im Viertel ist ein Spieljahr gesperrt |
| eigene Störung | 0,4 % am Tag | an dem Tag 60 % mehr Neukunden, sein Ruf sinkt um 3 |

- Der Marktpreis ist sein Preis für ein vergleichbares Angebot. Seine Preissenkungen machen deshalb die Tarife des
  Spielers relativ teurer: weniger Neukunden, mehr Kündigungen.
- Bei jeder Ausschreibung bietet er mit. Die Chance des Spielers: 20 % + 60 % × Ruf/100 (+20 Punkte mit Außendienst),
  weniger bei einem harten Konkurrenten und wenn sein Ruf über 60 liegt, zwischen 10 und 95 %. Verliert der Spieler,
  übernimmt Skyline Cable das Haus.
- Sein Ruf (anfangs 60) zählt bei den Neukunden mit: Je schlechter er ist, desto mehr wechseln (bis 40 % mehr).
- Wie hart er kämpft: die Konkurrenz-Einstellung des Spiels (rivalsDifficultyMultiplier) mal die Mod-Einstellung
  „Konkurrenz“ (0–300 %; 0 % = er sitzt nur da).
- Der Reiter „Konkurrenz“ zeigt seine Preise, je Viertel den Marktanteil, seine Technik, seine Aktionen und
  Exklusivverträge, und was er getan hat. Seine Züge kommen auch als Meldung.

## Koaxnetz (DOCSIS)

- Ein Glasfaserknoten versorgt bis zu 500 Haushalte. Der erste Knoten eines Viertels kostet 12.000 $, weil die
  Glasfaser ins Viertel muss. Jeder weitere Knoten kostet 9.000 $ (Knotenteilung). Bauzeit 1 Tag.
- **CMTS-Ports (GO-PORTS-1, 0.7.1):** Jedes CMTS (3.0 oder 3.1) hat 8 Ports, an jedem hängt ein Knoten. Sind alle Ports
  belegt, entsteht kein neuer Knoten und kein Gebäude, das einen braucht, bis ein weiteres CMTS in der Kopfstelle steht.
  Hat ein Spielstand schon mehr Knoten als Ports, teilen sich die Knoten die Ports (in echt: mehrere Knoten auf einem
  Port, eine gemeinsame Servicegruppe). Die Leistung je Knoten sinkt dann im Verhältnis Ports zu Knoten. Vereinfachung:
  Echte CMTS haben je nach Bauart viel mehr Ports; eine Kopfstelle hat 1 bis 12 CMTS.
- **Knoten entfernen (GO-NODES-1, 0.7.1):** „− Knoten“ bricht zuerst einen Knoten im Bau ab, sonst wird ein laufender
  abgebaut, aber nur, solange die übrigen alle angeschlossenen Einheiten abdecken (500 je Knoten). Das spart Wartung und
  Störungen, denn jeder Knoten ist Technik auf der Straße. Dafür wird es abends enger. Geld gibt es nicht zurück.
- Spektrum: 96 Kanalplätze zu 8 MHz.
  - Jeder TV-Transponder belegt einen Platz.
  - DOCSIS 3.0 nutzt bis zu 32 Plätze zu je 40 Mbit/s, das sind 1.280 Mbit/s pro Knoten.
  - DOCSIS 3.1 nutzt den Rest in OFDM-Blöcken: 24 Plätze bringen 1.890 Mbit/s, aber nur für Kunden mit 3.1-Modem.
  - Mehr Sender bedeuten also weniger Internet.
- DOCSIS 3.1 wird ab 300 Kunden freigeschaltet. Dafür muss erst das CMTS umgebaut werden, dann jeder Knoten (8.000 $ je
  Knoten). Tarife über 500 Mbit/s gibt es nur damit. Rund ein Drittel der neuen Kunden bekommt dann ein 3.1-Modem, alle
  mit Tarifen über 500 Mbit/s. Ein Modemtausch per Versand (6 $ je Gerät) holt die bestehenden Mietkunden nach.
- Last: Ein Anschluss braucht zur Spitzenzeit im Schnitt 2 Mbit/s plus 0,6 % seines Tarifs, und der Verkehr wächst mit
  jedem Spieljahr seit der Gründung um ein Viertel (höchstens auf das Dreifache). Haushalte haben ihre Spitze
  zwischen 20 und 23 Uhr, Firmen zwischen 10 und 16 Uhr. Wird der Knoten voll, sinkt das Tempo. Dann sinkt die
  Zufriedenheit, und die Kündigungen steigen.

## Glasfaser (FTTH mit XGS-PON)

- Freigeschaltet ab 1.500 Kunden, nur mit DOCSIS 3.1 in der Kopfstelle.
- Glasfaser je Gebäude kostet 5.000 $ plus 110 $ je Einheit. Die Bauzeit hängt von der Größe ab (1 bis 4 Tage).
- Ein PON-Baum hat 10 Gbit/s in beide Richtungen. Der Splitter wird global gewählt: 1:32 oder 1:64. Jeder PON-Port kostet
  2.500 $ und wird bei Bedarf automatisch eingerichtet.
- Beim Kunden steht ein ONT. Glasfasertarife gibt es bis 8.000 Mbit/s.
- Das Netz ist passiv: Rückwegstörer, Verstärkerausfälle und Hausverstärker fallen weg. Dafür kappt selten ein Bagger
  die Faser.
- Die Koax-Kunden eines Hauses ziehen per Knopf auf Glasfaser um, mit Technikertermin und ONT. Bis dahin laufen sie
  auf Koax weiter, deshalb kostet ein später Umzugstermin nichts. Danach kann das Koax des Hauses abgeschaltet werden.

## Bandbreite

- IP-Transit mit fester Menge. Je größer die Menge, desto billiger: 100 Mbit/s für 6 $ pro Mbit/s und Monat, 1 Gbit/s
  für 2,50 $, bis 100 Gbit/s für 0,40 $ pro Mbit/s und Monat.
- Abrechnung wie beim 95-Prozent-Verfahren: Die höchste Stunde des Tages fällt weg. Liegt die nächsthöhere über der
  gebuchten Menge, kostet der Überschuss das 1,5-Fache.
- Über dem Doppelten der gebuchten Menge ist der Anschluss voll, und alle Kunden werden langsam.
- Peering-Port am Internetknoten: 2.000 $ und 40 $ pro Tag. Er nimmt 35 % des Verkehrs ab, höchstens 10 Gbit/s je Port.

## Häuser anschließen

- Möglich bei Wohn-, Laden-, Büro- und Lagerhäusern.
- Kosten: Gestattung beim Eigentümer und Hausübergabepunkt, zusammen 250 $ plus 8 $ je Einheit. Das Hausnetz liegt
  schon. Bauzeit 1 Tag, bei mehr als 60 Einheiten 2 Tage. Ohne Netzmeister im Viertel dauert jeder Bau doppelt so lange.
- Fehlen im Viertel Knoten, werden sie mitgebaut, und ihre Kosten stehen dabei.

## Alter, Wartung und Erneuern

Jedes angeschlossene Haus hat ein Baujahr (Koax und Glasfaser getrennt):
- **Wartung** pro Einheit und Tag: Koax 0,01 $ plus 0,02 $ je Spieljahr Alter, Glasfaser 0,005 $ plus 0,005 $ je
  Spieljahr. Ein neues Haus mit 100 Einheiten kostet also 1 $ am Tag, nach drei Jahren 7 $.
- **Störungen** werden mit dem Alter häufiger: im Koaxnetz 40 % mehr je Spieljahr (Hausanschluss nach dem Alter des
  Hauses, Knoten nach dem Durchschnitt des Viertels), Glasfaser nur 10 % mehr je Spieljahr.
- **Erneuern:** Ein Koax-Haus ab einem Jahr lässt sich im Reiter „Gebäude“ erneuern (60 % der Kosten von Gestattung
  und Hausübergabepunkt). Danach zählt es wieder als neu. Der Umzug auf Glasfaser verjüngt ebenso.

## Gestattungsverträge

- Der Vertrag mit dem Eigentümer läuft zwei Spieljahre (120 Tage bei 60 Tagen im Jahr, das Spiel kann das ändern).
- 10 Tage vor dem Ende kommt ein Hinweis. Verlängern kostet so viel wie die Gestattung beim Anschließen (250 $ plus 8 $ je
  Einheit, mit Außendienst 30 % weniger) und verlängert um zwei Spieljahre ab dem alten Ende. Bezahlt wird mit der
  Abrechnung um Mitternacht, notfalls ins Minus wie die Miete: Ein Haus geht nie nur deshalb verloren, weil an dem Tag
  das Geld fehlt. Mit „Selbst verlängern“ passiert das in den letzten
  fünf Tagen von allein, wenn das Geld da ist.
- Läuft ein Vertrag aus, gibt es eine Ausschreibung. Die Chance, sie zu gewinnen: 20 % plus 60 % × Ruf/100, mit
  Außendienst 20 Punkte mehr, zwischen 10 und 95 %. Gewonnen kostet die Verlängerung 30 % mehr.
- Verloren: Ein anderer Anbieter übernimmt das Haus samt Kundschaft. Die Geräte kommen zurück auf Lager, die Aufträge
  im Haus fallen weg, und das Haus ist für eine Vertragslaufzeit gesperrt.

## Kunden

- Anfangs hat der alteingesessene Anbieter (Skyline Cable) alle Kunden. Höchstens 85 % der Einheiten eines Hauses
  wechseln.
- Neue Kunden pro Tag: 8 % der offenen Einheiten mal Attraktivität. Die Attraktivität setzt sich zusammen aus Preis
  gegenüber Marktpreis, Tempo am Abend, Störungen, Ruf und Vertrieb.
- Marktpreis pro Tag (US-Niveau, etwa 60 bis 85 $ im Monat): 1,10 $ + 0,55 $ × log10(Mbit/s). Das sind etwa 2,03 $
  für 50 Mbit/s und 2,75 $ für 1.000 Mbit/s. Fernsehen kostet etwa 0,60 $. Die Starttarife: Start 50 für 1,89 $,
  Komfort 250 für 2,29 $ und Turbo 500 für 2,59 $.
- Drei Gruppen: Sparfüchse (40 %, achten auf den Preis), Normale (45 %, auf das Preis-Leistungs-Verhältnis) und
  Power-Nutzer (15 %, auf das Tempo, ein fairer Preis zählt mit). Firmen sind Normale oder Power-Nutzer. Eine Gruppe
  nimmt nicht nur den einen besten Tarif: Ähnlich gute Tarife bekommen einen Anteil, je näher sie an den besten
  herankommen. Sparfüchse sind dabei am wählerischsten, Power-Nutzer verteilen sich am meisten.
- Der Außendienst geht von Tür zu Tür (60 Einheiten je Stunde, mehr Können geht schneller). Wo er an dem Tag war,
  unterschreiben bis zu doppelt so viele.
- 20 % der Haushalte wollen nur Fernsehen, wenn es einen TV-Tarif gibt.
- Kündigungen pro Tag: 0,4 % plus Zuschläge für Überlast, Ausfälle, einen zu hohen Preis und schlechten Ruf.
  Mindestlaufzeiten senken das (30 Tage: halb so viele), schrecken aber ein wenig ab.
- Geräte:
  - 15 % bringen ein eigenes Gerät mit (Routerfreiheit).
  - Die anderen mieten oder kaufen, je nachdem, was über 120 Tage günstiger ist.
  - Fehlt ein Gerät auf Lager, warten Kunden bis zu 3 Tage.
- Der Weg einer Bestellung:
  1. Der Kundenservice nimmt sie auf (5 Minuten je Anschluss). Bleibt sie länger als 48 Stunden liegen, springen jeden
     Tag 20 % ab.
  2. Telefon: 70 % bringen ihre Nummer mit. Die Portierung dauert 2 Tage und kostet 12 $ an den alten Anbieter. Die
     anderen bekommen eine neue Nummer aus einem Block; ohne freie Nummer warten sie.
  3. Koax: Der Schaltdienst schaltet den Anschluss im Haus frei (6 Minuten je Anschluss). Wartet das länger als
     72 Stunden, springen jeden Tag 10 % ab.
  4. Installation: Beim Koaxnetz installieren 85 % selbst (Versand 6 $), 15 % brauchen einen Technikertermin.
     Glasfaser braucht immer einen Termin. Ein verpasster Termin (mehr als 24 Stunden) kostet 10 $ oder 20 % des
     Monatspreises, und 30 % sagen ab.
- Wer beim Koaxnetz kündigt, behält das Signal, bis der Schaltdienst den Anschluss abschaltet. Solche Mitnutzer
  belasten das Netz weiter, zahlen aber nichts.

## Fernsehen

- Satellitenanlage in der Kopfstelle. Transmodulatoren setzen DVB-S2 auf DVB-C um, ein Gerät schafft zwei Transponder,
  ein Transponder bringt rund 8 Sender. Free-TV kostet keine Lizenz pro Kunde.
- Pay-TV als Wiederverkäufer:
  - Großhandelsverträge mit Pay-TV-Anbietern: Einkaufspreis pro Abo, eine Mindestabnahme pro Tag und ein empfohlener
    Verkaufspreis.
  - Den Verkaufspreis legst du fest.
  - Pay-TV braucht das CA-System und eine Smartcard pro Abo (Einkauf 12 $).

| Anbieter (erfunden) | Inhalt | Einkauf/Tag | Mindestabnahme/Tag | Empfohlen/Tag | Nachfrage |
|---|---|---|---|---|---|
| Arena Sport | Sport | 0,60 $ | 30 $ | 1,10 $ | 18 % |
| Silberleinwand | Filme & Serien | 0,42 $ | 20 $ | 0,80 $ | 15 % |
| Kinderplanet | Kinder & Doku | 0,15 $ | 0 $ | 0,35 $ | 10 % |
| Diamant Premium | alles | 1,00 $ | 60 $ | 1,80 $ | 6 % |

## Telefon

- Telefonie-Plattform (Softswitch und Session Border Controller) für 30.000 $. Danach kann jeder Internettarif einen
  Telefonanschluss mit Flatrate haben. Der Kundschaft ist das etwa 0,35 $ am Tag wert.
- Rufnummern gibt es in Blöcken zu 1.000 für 300 $. Gekündigte Nummern kommen zurück in den Block; portierte Nummern
  gehen beim Kündigen an den neuen Anbieter.
- Zusammenschaltung mit den anderen Netzen: 0,05 $ je Telefonanschluss und Tag.

## IPTV

- IPTV-Plattform (Encoder für die Satellitensender, Middleware, Multicast) für 60.000 $: rund 120 Sender und Replay
  über die Internetleitung, ganz ohne Kabelkanäle. Der Kundschaft ist IPTV etwas mehr wert als Kabelfernsehen
  (0,15 $ am Tag).
- Jeder IPTV-Anschluss mietet eine Set-Top-Box (Einkauf 55 $, Miete 0,15 $ am Tag).
- IPTV kostet Bandbreite an den Knoten: Zur Fernsehzeit sehen 55 % fern. Jeder gesehene Sender läuft einmal als
  Multicast (6 Mbit/s, höchstens 80 Sender gleichzeitig), Replay (10 %) läuft einzeln.

## Störungen

| Störung | Betrifft | Häufigkeit | Arbeit |
|---|---|---|---|
| Gerät defekt | 1 Kunde | 0,05 % je Gerät/Tag (ONT 0,02 %) | 0,5 h + Fahrt |
| WLAN-Problem | 1 Kunde | 0,08 % je Kunde/Tag | 0,3 h per Fernwartung |
| Hausanschluss (Wasser, Hausverstärker) | ganzes Haus | 0,15 % je Koax-Haus/Tag | 1,5 h + Fahrt |
| Verstärkerausfall | ein Viertel eines Knotens | 0,4 % je Knoten/Tag | 2 h + Fahrt |
| Rückwegstörer | ein Knoten wird langsam | 0,6 % je Knoten/Tag | 4 h Suche + Fahrt |
| Stromausfall am Knoten | ein Knoten | 0,1 % je Knoten/Tag | 1,5 h + Fahrt |
| Faserbruch (Bagger) | Glasfaser eines Viertels | 0,08 % je Glasfaser-Viertel/Tag | 6 h, 2 Techniker |
| CMTS-Absturz | alle Koax-Kunden | 0,03 %/Tag | 2 h in der Kopfstelle |

- Reparaturmaterial je behobener Störung: Am alten Koaxnetz ist das billig, Glasfaser ist teuer. Hausanschluss 60 $,
  Verstärker 150 $, Rückwegstörer 30 $, Stromversorgung am Knoten 120 $, CMTS 400 $, Faserbruch 1.800 $ (ein
  Spleißtrupp). Defekte Geräte laufen über den Gerätetausch.
- Netzpflege (ein Budget pro Tag) senkt die Störungen im Koaxnetz um bis zu die Hälfte. Ein Netzmeister im Viertel
  senkt sie um weitere 40 %, und Netzstörungen brauchen mit ihm 30 % weniger Arbeit.
- Die Technische Entstörung prüft jede Störung vom Schreibtisch aus (15 Minuten). 40 % der defekten Geräte lassen sich
  dabei aus der Ferne reparieren, der Rest braucht vor Ort 20 % weniger Arbeit. WLAN-Probleme löst sie per Fernzugriff;
  ohne Entstörung muss die Technik hinfahren.
- Bei einem Ausfall rufen 40 % der Betroffenen an.
- Entschädigung nach dem Vorbild von § 58 TKG: Ist eine Störung nach 2 Tagen nicht behoben, gibt es am 3. und 4. Tag
  5 $ oder 10 % des Monatspreises je Kunde und Tag, ab dem 5. Tag 10 $ oder 20 %.
- Du kannst selbst hinfahren (Navi) und vor Ort reparieren. Das dauert so lange wie für einen Techniker mit 3 Sternen.

## Personal

| Rolle | Aufgabe | Braucht | Lohn je Stunde |
|---|---|---|---|
| Technik | Störungen, Termine bei der Kundschaft | Transporter | 14 $ + 3 $ je Stufe |
| Schaltdienst | Koax-Anschlüsse an- und abschalten (DOCSIS) | Transporter | 12 $ + 2,50 $ je Stufe |
| Netzmeister | ein Viertel planen und pflegen, 8–16 Uhr | Arbeitsplatz und Auto | 22 $ + 4 $ je Stufe |
| Außendienst | Tür zu Tür, Gestattungsverträge | Auto | 13 $ + 3 $ je Stufe |
| Kundenservice | Bestellungen, Anrufe | Arbeitsplatz | 11 $ + 2 $ je Stufe |
| Technische Entstörung | Störungen prüfen, Fernzugriff | Arbeitsplatz | 15 $ + 3 $ je Stufe |

- Jeden Tag kommen je Rolle ein bis zwei neue Bewerbungen dazu (höchstens drei je Rolle), mit Name, Können (1 bis 5)
  und Stundenlohn. Mehr Können geht schneller.
- Schichten: Früh (6–14 Uhr), Spät (14–22 Uhr), Nacht (22–6 Uhr, 25 % Zuschlag).
- Fahrzeuge kommen aus dem Spiel: Der Spieler kauft sie beim Autohändler und parkt sie an der Kopfstelle (bis 80 m),
  oder er least sie (siehe Start und Startkapital). Ein Fahrzeug mit einer Ladefläche ab 14 zählt als Transporter (UMC
  Desert, Vord V150, Mersaidi Dash, UMC Nunavut, Vord Courier D500). Das Fahrzeug, in dem der Spieler sitzt, zählt nicht, auch nicht die Autos anderer Spieler. Sprit: 12 $ je
  Fahrzeug und Schicht. Netzmeister und Außendienst nehmen einen Transporter, wenn kein Auto da ist.
- Ein Fahrzeug oder Arbeitsplatz reicht für eine Person je Schicht. Fehlt es, bleibt die Person untätig, und die App
  sagt, was fehlt.
- Reihenfolge der Arbeit: zuerst Ausfälle (je mehr Betroffene, desto dringender), dann Rückwegstörer, Termine, die in
  6 Stunden fällig sind, Bestellungen und Anschalten, Installationen, Abschalten, WLAN-Probleme und zuletzt Umzüge auf
  Glasfaser. Die Wartezeit hebt die Dringlichkeit nur bis zu einer Grenze, damit ein alter Schaltauftrag nie einen
  Ausfall verdrängt.
- Die Fachkräfte fahren von Einsatz zu Einsatz, jede Schicht beginnt an der Kopfstelle. Im selben Haus wird ohne
  Fahrt weitergearbeitet.
- Der Spieler selbst nimmt zwischen 8 und 20 Uhr nebenbei Bestellungen und Anrufe an, so viel wie eine Viertelkraft.
  Für den Anfang reicht das.

## Umzüge

- Jeden Tag zieht etwa 0,08 % der Kundschaft um: die Hälfte im selben Viertel, die anderen irgendwohin in der Stadt (nach
  den Einheiten der Häuser gewichtet).
- Zieht jemand in ein anderes Haus des Anbieters mit freiem Platz, zieht der Vertrag mit: Er zahlt die Umzugsgebühr
  (voreingestellt 30 $, im Reiter „Tarife“ 0–100 $). Der Schaltdienst klemmt die alte Adresse ab (Koax: bis dahin hat sie
  noch Signal) und schaltet die neue auf. Das Modem zieht mit, deshalb braucht es keine Installation; bei Glasfaser gibt
  es einen Termin. Je 10 $ über 30 $ kündigen gut 5 % lieber, höchstens 40 %.
- Zieht jemand woandershin, endet der Vertrag (die Geräte kommen zurück).
- Dauert das Aufschalten länger als drei Tage, beschwert sich die Kundschaft.

## Störungen bei der Kundschaft und Entstörungspauschale

- Etwa 0,06 % der Kundschaft meldet am Tag eine Störung, die bei ihr selbst liegt: eigenes Modem defekt (nur wer ein
  eigenes Gerät hat), TV verstellt (nur Fernsehkundschaft), Telefon defekt (nur mit Telefon), Strom aus, Anschlusskabel in
  der Wohnung defekt. In der App heißt das zuerst „Kunde meldet Störung (Ursache unklar)“; nach der Prüfung der
  Technischen Entstörung steht die Ursache da.
- „TV verstellt“ und „Strom aus“ löst die Technische Entstörung in 60 % der Fälle am Telefon, kostenlos. Sonst fährt die
  Technik hin, und die Kundschaft zahlt die **Entstörungspauschale** (voreingestellt 45 $, 0–150 $). Bei einem toten
  eigenen Modem kauft die Hälfte gleich eins beim Techniker.
- **Kunde nicht anwesend:** Bei jedem Besuch in einer Wohnung (Installation, Gerät, WLAN) ist in 6 % der Fälle niemand zu
  Hause. Die Fehlanfahrt kostet die Pauschale, und der Besuch rückt einen Tag weiter (bei mehreren Terminen im Haus nur
  dieser eine).
- Kundenseitige Störungen bringen keine Entschädigung und zählen nicht als Ausfall des Netzes.
- Je teurer die Pauschale, desto mehr beschweren sich: 4 % plus 0,05 % je Dollar.

## Beschwerden und Umgangston

- Jede Fachkraft hat einen Umgangston von 1 (ruppig) bis 5 (sehr freundlich); Bewerbungen zeigen ihn. Nach jedem Besuch
  bei Kundschaft beschwert sie sich mit 5 % (Umgang 1), 2,5 % (2), 0,8 % (3), 0,4 % (4) oder 0,2 % (5) über einen
  unfreundlichen Techniker.
- Eine **Schulung** (300 $, höchstens alle 30 Tage) hebt den Umgangston um eins. Der Reiter „Personal“ zeigt die
  Beschwerden der letzten 30 Tage je Person.
- Weitere Gründe: die Entstörungspauschale, ein nicht eingehaltener Termin (ein Viertel der Betroffenen), eine Hotline,
  die niemand abnimmt (3 % der verlorenen Anrufe), langsame Abende, ein Umzug, der zu lange dauert.
- Der Kundenservice beantwortet Beschwerden (15 Minuten je Beschwerde, noch vor neuen Bestellungen). Eine Beschwerde, die einen
  Tag liegen bleibt, lässt Kundschaft gehen (3 % je Beschwerde und Tag). Fremdfirmen im Kundenservice nehmen auch
  Beschwerden an.

## Vom Mieten bis zum Öffnen (GO-OFFICE-2, 0.7, im Spielcode nachvollzogen)

Vorbild ist ein Bürounternehmen des Spiels, z. B. die Webentwicklungsagentur:

| Schritt | Webentwicklungsagentur (Spiel) | Kabelanbieter (Going Online) |
|---|---|---|
| Mieten | Büro mieten oder kaufen | genauso |
| Gründen | BizMan „Neues Unternehmen gründen“; Kurs „Bürounternehmen“ nötig, Name eindeutig; danach vorübergehend geschlossen, Standard-Öffnungszeiten, Aufgaben für die Anforderungen | genauso, mit demselben Kurs „Bürounternehmen“; Going Online öffnet es von selbst rund um die Uhr und gründet den Anbieter (Kopfstelle und Kundencenter = dieses Büro) |
| Einrichten | Anforderungen: Computer-Arbeitsplätze, Toiletten (eine je 100 m²), Waschbecken (eines je 200 m²), je höchstens vier; jeder Computer braucht Tisch und Stuhl | dieselben Anforderungen **plus ein CMTS**; die Arbeitsplätze von Going Online sind Computer und brauchen Tisch und Stuhl; dazu ein Lagerregal für die Geräte |
| Einstellen | Personalagentur bietet die Berufe des Typs (Programmierer bei City Workforce) | die Agenturen bieten die Berufe, deren Arbeitsplätze im Büro stehen (GO-WORK-1); Netzmeister auch für den Hauptsitz |
| Zeitplan | BizMan Zeitplan: Schicht an einer Arbeitsstation | genauso (die Arbeitsplätze sind die Stationen) |
| Arbeiten | stündlich arbeitet, wer Schicht hat, an einer vollständigen Station (Tisch + Stuhl), wenn alle Anforderungen erfüllt sind | genauso (GO-WORK-2, `Crew.Duty`): sonst „Arbeitsplatz unvollständig“ oder „Büro noch nicht eingerichtet“ |
| Kundschaft | Kundeneinträge aus der Nachfrage, je arbeitender Person einer pro Stunde | Going Online: Häuser anschließen, Tarife, Bestellungen und Anrufe (Kundenservice), Schalten, Geräte (Modems) aus dem Lager, Verträge |
| Geld | Aufträge, um Mitternacht ausgezahlt (`ProcessDailyOrders`), Löhne zahlt das Spiel | Abos, Pay-TV, Gerätemiete als Aufträge des Büros, um Mitternacht ausgezahlt; Löhne zahlt das Spiel; Strom, Wartung, Leasing bucht Going Online |

## Kundencenter

- Kundenservice und Technische Entstörung arbeiten im Kundencenter – seit 0.7 das Büro des Kabelanbieters selbst
  (GO-OFFICE-1). Netzmeister sitzen zuerst in der Kopfstelle.
- Im Probelauf hängt die Zahl der Plätze vom Gebäude ab: ein Arbeitsplatz je 10 m² im Büro, zwischen 2 und 400;
  Kopfstelle und Kundencenter im selben Büro teilen sich den Platz. Im Spiel zählen die aufgestellten Arbeitsplätze.
- Ein Umzug kostet 200 $ je Arbeitsplatz. Gehört das Gebäude dem Spieler nicht mehr, bleiben die Plätze leer.

## Kundenzufriedenheit

Jeden Abend ergibt sich die Zufriedenheit des Tages aus sechs Teilen. Der Ruf folgt ihr jeden Tag um ein Fünftel des
Abstands.

| Teil | Gewicht | Was fehlt, wenn er sinkt |
|---|---|---|
| Netz (Tempo am Abend, Ausfälle) | 27 % | Knoten, DOCSIS 3.1, Bandbreite, Netzmeister |
| Störungen rechtzeitig behoben | 22 % | Technik, Technische Entstörung |
| Termine eingehalten | 14 % | Technik |
| Anrufe angenommen | 14 % | Kundenservice |
| Bestellungen und Anschalten pünktlich | 13 % | Kundenservice, Schaltdienst |
| Wenig Beschwerden, rechtzeitig beantwortet | 10 % | Kundenservice, freundliche Technik (Schulung), faire Pauschalen |

Zu wenig Personal zeigt sich hier zuerst. Die App zeigt jeden Teil mit einem Balken und sagt, was hilft. Dazu gibt es
drei Auswege:
- **Bedarf je Rolle:** Der Reiter „Personal“ rechnet aus, wie viele Leute eine Rolle braucht: die Arbeit des
  Vortags plus die Hälfte dessen, was gerade wartet, 7,5 Stunden je Person. Bei der Technik mindestens drei, eine je
  Schicht, weil Störungen rund um die Uhr kommen. Netzmeister: einen je Viertel mit Netz.
- **Fremdfirmen** (je Rolle einschaltbar) übernehmen Aufträge, die über 8 Stunden warten: Technik 95 $ je Einsatz (bis
  12 am Tag), Schaltdienst 35 $ (bis 30), Kundenservice 3 $ je Bestellung und 1,50 $ je Anruf (bis 150).
- **Überstunden:** Alle arbeiten 10 statt 8 Stunden, also ein Viertel mehr Arbeit. Die zwei Stunden mehr kosten 50 %
  Zuschlag. Gut für ein paar Tage, auf Dauer teuer.

## Mehrspieler

- Jeder Spieler rechnet seinen eigenen Anbieter. Die Daten stecken im Spielstand und reisen deshalb mit dem Spielstand
  zum Server.
- Bei einer Fusion laufen die Buchungen über die gemeinsame Firmenkasse.
- Jeder Spieler hat seinen eigenen Konkurrenten Skyline Cable, gerechnet auf dem eigenen Rechner.
- Rangliste: Jede Person meldet ihren Anbieter einmal pro Minute an den Server (Firma, Anschlüsse, Häuser, Viertel,
  Ruf, Gründungstag; Nachricht ProviderReport). Die Ranglisten-Seite zeigt unter dem Namen „Kabelanbieter: …“ und
  einen Abschnitt „Kabelanbieter (Going Online)“, die meisten Anschlüsse zuerst. Was in den letzten drei Tagen
  gegründet wurde, ist als „neu“ markiert.
- Später kommt Wettbewerb zwischen Spielern um dieselben Häuser dazu.

## Speichern

Der Zustand steht als komprimierter Eintrag in einer Liste des Spielstands, die das Spiel nur zum Nachschlagen nutzt
(`CollapsedCitymapFilterCategories`). Geschrieben wird er im Ereignis `GlobalEvents.onSaveGame`, das das Spiel kurz vor dem
Schreiben auslöst. Dafür braucht es keinen Eingriff in den Spielcode.

## In den Seiten des Spiels (0.8.1, GO-PATCHES-1)

Wunsch des Spielers (2026-11-08): „Going Online soll jetzt auf aktuellen Stand gebracht werden als Einzel-Mod“, dann „Mit
Harmony“. Die 17 Harmony-Anpassungen, die bis 0.8.0 nur Going Public Server hatte, stehen jetzt in `core/GamePatches.cs`
und gelten in beiden Fassungen:
- BizMan: der Kabelanbieter in der Unternehmensliste (Typ mit Anschlüssen, eigene Zufriedenheit), nicht bei den
  Lagerhäusern; seine Seite öffnet auf „Einblicke“ und bekommt die Reiter Lieferungen, Fuhrpark (der Reiter „Fahrer“ des
  Spiels) und im Lagerhaus Inventar; „Inventar & Preise“ zeigt die Geräte mit Bestand, ihr Preisfeld bleibt gesperrt.
- Lieferungen: Großhändler-Lieferverträge an den Kabelanbieter (auch auf Palettenregale), die Telefonbestellung beim
  Möbelhaus ins Büro.
- Personal: die Personalsuche bietet unsere Berufe an und sagt, welcher Arbeitsplatz fehlt; Kandidaten vom Schwarzen
  Brett nur für Berufe, deren Arbeitsplatz steht.
Der eigene Mod bringt Harmony im Ordner `Dependencies` mit (`src/Patcher.cs`) und setzt die Anpassungen einmal, sobald ein
Spielstand geladen ist – nur ohne Going Public Server, das sie selbst setzt. Das Spiel lädt Harmony nur einmal für alle
Mods (die Kopie des zuerst geladenen Mods); scheitert eine Anpassung daran, steht das im Log, und Going Online läuft ohne
sie weiter – die App kann alles. Unter Wine/Proton schreibt Harmony seine Sprünge ohne Zeigerzellen (WINE-JUMPS-1, wie
in Going Public Server), sonst stürzen die angepassten Methoden dort ab.

## Spielinhalte vom 11. November (GO-EVENTS-1, GO-STREET-1, GO-WHOLESALE-2, GO-BLUEPRINT-3)

Der Nutzer wählte am 11.11. alle vier vorgeschlagenen Inhalte.

**Ereignisse (GO-EVENTS-1, `core/Events.cs`):** höchstens eins am Tag (Wurf um 9 Uhr), jede Art mit eigener Pause, nie
mehr als zwei offene Entscheidungen. Eine Meldung kündigt es an, die Übersicht zeigt es mit zwei Knöpfen bis zur Frist;
ohne Antwort gilt die zweite Wahl, die nichts kostet. Geld läuft über die Tagesabrechnung („Sondermaßnahmen“,
„Erstattungen und Förderung“).
- Unwetter (3,5 % am Tag, Pause 14 Tage): 8–14 h vorher angekündigt, 6–10 h über 1–3 eigenen Vierteln, Netzstörungen
  dort ×4, Glasfaserschnitte ×3. Bereitschaft: 90 $ je Person der Technik, dafür anderthalbfache Arbeit im Sturm und am
  Tag danach.
- Großes Fußballspiel (3 %, Pause 18): in 1–2 Tagen 20–23 Uhr, 35 % mehr Last. Sport-Aktion (800 $ + 0,30 $ je
  TV-Kunde, höchstens 3.000 $): 60 % mehr Abschlüsse für Tarife mit TV bis zur Tagesabrechnung des Spieltags. Danach:
  Stau in einem Viertel −3 Ruf, ruhiger Abend +1.
- Bagger (3 %, Pause 12): Glasfaserschnitt oder Koax-Stammleitung (Kunden eines Knotens) in einem Viertel.
  Schadensersatz fordern: 250 $ Anwalt, nach 5–8 Tagen zahlt die Versicherung in drei von vier Fällen 1.500 $ + 15 $
  je abgeschnittenem Anschluss (höchstens 12.000 $).
- Zeitungsbericht (30 %, wenn zwei Ausfälle mit 25+ Kunden binnen 3 Tagen oder Zufriedenheit unter 50; Pause 20):
  Ruf −6, eine Woche Abschlüsse ×0,8. Gutschrift (1 $ je Kunde): Ruf +4 zurück, die Woche ×0,92.
- Testsieger (4 %, bei Zufriedenheit 85+, Ruf 70+, 300+ Kunden; Pause 45): Ruf +5, zehn Tage Abschlüsse ×1,1, mit
  Werbung (1.500 $) ×1,4.
- Glasfaser-Förderung (2,5 %, Pause 30; ein Viertel mit OLT und mindestens 6 Häusern ohne Glasfaser): 1.500 $ je Haus,
  das binnen 20 Tagen Glasfaser bestellt bekommt (höchstens dreimal das Ziel); wer mitmacht, sagt 2 oder 3 Häuser zu,
  sonst 2.500 $ Vertragsstrafe.

**Netz in der Stadt (GO-STREET-1, `core/Street.cs`, Modelle `gostreetcab`, `gobarrier`, `gocone` aus
`ba-art/goingonline/go_models.py`):** ein grauer Verteilerkasten neben der Tür jedes angeschlossenen Hauses (auf dem
Gehweg, nicht vor der Tür, mit einem Band in der Farbe der Wagen-Beklebung), eine Baustelle mit zwei Absperrschranken
(blinkende Leuchte) und drei Kegeln, solange das Haus angeschlossen wird, zwei Kegel, solange jemand der Technik dort
arbeitet. Nur bis 110 m um die Kamera, ohne Kollision. GO-STREET-2 (Nutzer 11.11.: „Für alle Spieler sichtbar,
Verteilerkästen mit Firmenlogo und Name“): Jeder Kasten trägt ein Schild (64 × 20 cm) mit dem Logo und dem Namen seiner
Firma (`LiveryPaint.ComposeLabel`, das Logo wie bei der Wagen-Beklebung); jeder Rechner schickt sein Netz über den Server
(Nachricht ProviderStreet, `src/GoingOnlineStreet.cs`, auf dem Server `go-street.json` auch für Abwesende), jeder zeigt
die Netze aller. Zwei Firmen in einem Haus bekommen zwei Kästen nebeneinander.

**Leitungen an Mitspieler (GO-WHOLESALE-2, `core/Peers.cs`, im Server `src/GoingOnlineLines.cs`):** wie Funkwelle Mobil,
aber zwischen Spielern. Die Netzfirma bietet ihre versorgten und betreuten Viertel zu einem Preis je Leitung und Tag
an (Standard 1,10 $); das Angebot reist mit dem Anbieter-Bericht zum Server, der alle Angebote verteilt. Wer mietet,
bekommt in Vierteln ohne eigenes Netz Leitungen, die täglich auf 5 % der versorgten Haushalte zuwachsen (mal Ruf, minus
Ausfälle und Stau der Netzfirma); die Kundschaft zahlt den Durchschnittspreis der eigenen Tarife, die Miete geht als
Abrechnung über den Server an die Netzfirma. Der Server hebt Abrechnungen auf (`go-linebills.json`), bis die Netzfirma
online ist und den Empfang bestätigt; jeder Tag wird einmal gebucht. Ist ein Angebot weg, ruht der Vertrag, nach 7 Tagen
endet er. Die gemieteten Leitungen belasten die Knoten der Netzfirma.

**Mehr Vorlagen (GO-BLUEPRINT-3, `tools/blueprints.py`):** Kundencenter A1 (aus der Bank des Spiels), A2, C1 (geplant auf
der Raumform aus allen C1-Layouts, Hinterräume aus dem C1-Geschenkeladen); Kabel-Büros A3, C1 und D2 (aus den
Web-Agenturen des Spiels; D2 auf Wunsch des Nutzers vom 11.11. mit 18 Arbeitsplätzen, GO-BLUEPRINT-4/5) und J1, jeweils Start (3 Racks) und Voll (8 Racks). Geprüft auf überlappende Teile (Maße aus
den Box-Collidern der Prefabs).

## Ideen aus der Recherche (2026-11-08, noch nicht gebaut)

Vorschläge an den Nutzer, aus echten Kabelnetzen abgeleitet:
- ~~Hubs und Glasfaserring~~ – gebaut in 0.8 (siehe „Büros, Hubs und Kundencenter“).
- ~~Rückkanal und High-Split, Fiber Deep, Remote-PHY, Netzüberwachung, Stromversorgung~~ – gebaut in 0.8 (siehe oben).
- **Streaming-Cache** in der Kopfstelle: kostenloser Server eines Streamingdienstes; er füllt sich nachts und nimmt abends
  Transit ab.
- **Vorleistung:** Ein anderer Anbieter (KI oder Mitspieler) mietet Anschlüsse auf dem eigenen Netz, wie O2 auf dem
  Vodafone-Kabelnetz seit 2019.
- **Geschäftskunden mit Entstörzeit (SLA)** und **Sammelverträge** mit Hausverwaltungen.

## Recherche (Quellen)

- Netzebenen, Kopfstelle, Glasfaserknoten, Hausübergabepunkt: dewiki „Netzebene“, Vodafone „Haus-Erschließung“,
  DARC-Vortrag DOCSIS.
- Geteiltes Medium, Knotenteilung, Spitzenlast pro Kunde: NextTV „Split the Node“, CommScope 2021 (3,13 Mbit/s pro Kunde
  zur Spitzenzeit), SCTE Traffic Engineering.
- IP-Transit (95-Prozent-Verfahren, Preisstaffel): Shift Hosting, DrPeering.
- Störungen (Rückwegstörer, Verstärker), Entstörung: Vodafone „Rückwegstörer“, KEVAG Leistungsbeschreibung.
- Entschädigung: § 58 TKG (gesetze-im-internet.de).
- Router (Miete 5–10 € im Monat, Kauf ab 139 €, Routerfreiheit seit 2016): teltarif, Verivox.
- Fernsehen: Einspeiseentgelte (LTO), Programmgebühren (ESPN etwa 9–10 $ pro Kunde und Monat), Sky über Kabelnetze
  (W&V, Horizont), Transmodulatoren (Blankom, Philips TDQ-480 etwa 790 €).
- DOCSIS 3.0/3.1, XGS-PON (10 Gbit/s symmetrisch, Splitter 1:32/1:64), FTTH-Kosten (FBA-Studien).
- Hubs, Glasfaserring, CMTS im Hub, 1 bis 12 CMTS je Kopfstelle: Patente US7068712 und EP1394989, Franchise-Unterlagen des
  Office of Cable Television (Washington D.C.).
- Segmentierung, über 18.000 Glasfaserknoten, bis 7 Gbit/s je Cluster, High-Split mit Verstärkertausch: Vodafone
  Immobilienwirtschaft „Netz-Segmentierung“, inside-digital; Hausverstärker mit Rückweg nur bis 65 MHz: O2-Hilfeforum.
- Fiber Deep/Node+0: NCTA/SCTE 2017 „Fiber Deep Networks“, Light Reading (Videotron), Cisco-Blog; Remote PHY: Broadband
  Library; PNM: ZCorum, VIAVI, Broadband Library; Netzteile mit Akkus: Patente, Yolo County Code (2 Stunden);
  Streaming-Caches: Analysys Mason 2022; Vorleistung: PC-Welt, Telecompaper.
- Beschäftigte je Kundschaft (etwa 7 je 1.000, 2021): Benton Institute, „2022 Telecommunications Benchmarking Study“.
