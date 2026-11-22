# Going Online

Ein Mod für Big Ambitions: Du wirst Kabelnetzbetreiber und verkaufst Internet, Fernsehen (Kabel und IPTV) und Telefon
an die Häuser der Stadt, gegen den Konkurrenten Skyline Cable. Dafür stellst du Leute in sechs Rollen ein, vom
Technikteam bis zum Kundenservice im Kundencenter. Gegründet wird in BizMan – wie jedes Bürounternehmen mit dem Kurs „Bürounternehmen“: Bei einem leeren Büro steht dort „Kabelanbieter“ neben den anderen Unternehmen – Kopfstelle und Kundencenter unter einem Dach. Das Konzept mit allen Zahlen und Quellen steht in [KONZEPT.md](KONZEPT.md), die Testschritte in
[TESTLISTE.md](TESTLISTE.md).

## Zwei Formen, ein Code

- **Eigener Mod (Einzelspiel):** `GoingOnline.csproj` baut `GoingOnline.dll` aus `src/` (Einstieg, Einstellungen,
  Handy-Kachel) und `core/` (der Anbieter). Er kommt ohne Harmony aus und nutzt nur Ereignisse und Objekte des Spiels.
  Ist Going Public Server geladen, hält er sich raus.
- **Modul 8 in Going Public Server:** `BigAmbitionsMP.csproj` kompiliert `core/` mit ein (`src/GoingOnlineModule.cs`
  dort). Den Schalter dafür setzt AMP mit `ModuleGoingOnline`. Die App liegt auf den Handy-Seiten und im großen Menü.
  Jede Person rechnet ihren Anbieter auf dem eigenen Rechner, und gespeichert wird er im eigenen Spielstand.

## Bauen

```bash
python3 -I goingonline/tools/locales.py            # Locales/en.json + de.json aus dem Code und den deutschen Texten
python3 -I goingonline/tools/merge_into_server.py  # dieselben Texte in Going Public Servers Locales
goingonline/tools/package.sh [<Spielordner>] [<Ziel>]   # Build + GoingOnline-<Version>-<Build>.zip für den Workshop
```

Die Bilder (`assets/GO_AppIcon.png`, `preview.png`) entstehen mit `tools/make_icons.py`, dafür braucht es Pillow. Das
Logo zeigt den Schriftzug „Going Online“ groß in zwei Zeilen (Poppins ExtraBold aus AMPs Web-Ordner oder
`$GO_FONT_HEAVY`, sonst DejaVu Sans Bold) und darunter die vier Produkte als kleine Logos: TV, Internet, Telefon, IPTV.

## Selbsttest

Auf einem dedizierten Going-Public-Server mit der Datei `goingonline-selftest.txt` im Mod-Ordner rechnet
`core/SelfTest.cs` nach dem Start einen erfundenen Anbieter in der echten Stadt durch, so viele Tage, wie in der Datei
steht (zum Beispiel `60`). Ändert sich die Datei, läuft der Test noch einmal. Der Testspieler stellt so viele Leute
ein, wie die App als Bedarf zeigt. Jeder Tag landet mit zwei Zeilen im Log (`[GoingOnline] selftest ...`), am Ende
folgt eine Zusammenfassung mit Tarifen, Rollen, Vierteln und Geräten. Dabei bewegt sich kein Geld, und nichts kommt in
den Spielstand.

---

# Going Online (English)

A Big Ambitions mod: become a cable network operator and sell internet, TV (cable and IPTV) and phone lines to the
city's buildings, with staff in six roles, from technicians to a customer center.
- **Its own mod (single player):** `GoingOnline.csproj` (`src/` + `core/`), no Harmony. It stays idle when Going
  Public Server is loaded.
- **Module 8 of Going Public Server:** `core/` is compiled into it. The AMP switch is `ModuleGoingOnline`.

Licence: MIT (see [LICENSE](LICENSE)). Developed by amaus1209 together with Claude Code (Anthropic). Big Ambitions
belongs to Hovgaard Games.
