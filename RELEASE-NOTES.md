## Going Public Server 1.0.16.1

**Server and players both need 1.0.16.1.** Please update the Workshop mod and the server together - the server only lets in the same version.

Fixes from the first test of 1.0.16:

### Self-driving
- Stops at **red lights** - the intersection's own light colour, at the stop line, until green.
- Steers into turns **sharply and at the right moment** instead of cutting them, and slows down for curves.
- No longer drives into **hydrants** and other things standing still; when stuck, it backs off.
- **Blinks** before a turn, with the game's own blinkers.

### Passengers
- See the car's **wheels** turn and steer and hear its **engine**.
- Hear the **driver's radio station** - the driver's choice comes first; after getting out, your own station plays again.

### Radio
- **A Radio tab in the phone**: the phone's full menu now has "Radio" with every station to pick: the game's stations, your own streams and the server's station list. The station that plays is marked. You hear the radio as before - in a car, or with headphones.
- **The server's station list from GitHub**: in AMP the server owner picks the **Radio station list** under *Modules* - the language of the list (default: German, "Off" = none). Every player on the server gets the same list from the template repository (`radio/<language>.txt`): 250 German stations for German (SWR3, Deutschlandfunk, Bayern 3, 1LIVE, NDR 2 ...), the country's most popular stations for 22 more languages. It is loaded at game start and kept for a start without the internet. Editing the file on GitHub changes the list for everybody - no new mod version needed.
- **Your own stations**: `streams.txt` in the game's Radio folder (Windows: `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions\Radio\`), one line per station "Name = address"; .m3u and .pls files in the folder work too. They come before the server's list.
- **https streams only**: the game loads nothing over plain http; an http address is tried as https.
- In the car, "next station" goes through the game's stations, then the streams.

### World with energy/hunger off
- Stays that way after a server restart or a reconnect. The needs dial had switched it on again; existing worlds are repaired at their next start.

### Sitting
- Another player **sitting** on a bench or chair is shown **sitting** to the others too (they saw him stand on his seat).
- If an NPC sits on exactly that seat in your game, the NPC is hidden while the player sits there. In buildings no player owns, every game has its own NPCs.

### Merger
- **Staff list**: after a lost connection or a server restart a merged member did not get the partner's unassigned staff back - the list stayed empty and nobody could be assigned. The server now sends them at every join and every change of the merger.
- **Dissolving only together**: with two members the button under *Business* now reads **"Dissolve company"**. The other member gets a notice and the button **"Agree to dissolve"**; the company ends only when every member agreed and is online. **"Withdraw dissolve vote"** takes one's own vote back.
- **Dissolving sells everything**: every shop (furniture, business cars and the deposit back, plus a goodwill of 30 days' average profit), every bought building at market value, every private car and every home; all staff are let go. The money goes into the company account, which is then shared equally.
- In a company of three or more, "Leave merger" stays a plain leave - the company carries on.

### Server console
- **A quieter console in AMP**: with "Log detail: Normal" the periodic diagnostics are left out too (network tallies, consistency audits, single wallet bookings, per-file save lines, frame measurements). The "[Server] Info" line comes only when port, key or version change (was every 30 seconds), and AMP drops the engine's harmless graphics notices. Together about 57 % fewer lines; "Detailed" still shows everything.
- **No false stutter warnings**: while idle (no players, or everybody in the pause menu) the server slows down on purpose to its "Frame cap while empty". The frame measurement took that for stutter and wrote "[Rhythm] OSCILLATING" every 10 seconds (1.0.15: 215,000 lines in one night). With players on, the server runs at its full frame rate.

### Other
- The version line at the bottom left reads "GPS-MM v. 1.0.16.1".
- When a member of a merged company opens "My Employees", a `[MergerDiag]` line goes into Player.log (for troubleshooting).
