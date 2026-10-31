## Going Public Server 1.0.13

**Server and players must both run 1.0.13.** Update the Workshop mod and the server together.

### Fixed: the Linux/Proton test switch turned multiplayer off
- With the test file `skip-tiny-patches.txt` next to `BigAmbitionsMP.dll`, 41 groups of game hooks failed, multiplayer was switched off and a banner claimed that "another installed mod" was incompatible. The file now only does what it should: the smallest game methods are left alone and multiplayer stays on (the features on those methods are off while the file is there).
- The banner no longer calls the mod's own folder in `ModsLocal` "another installed mod".

### New: load status on the main menu
- When the game starts, a small console panel at the bottom of the main menu shows what the mod loaded, line by line: `[ OK ]`, `[WARN]` or `[FAIL]` for the folder, Harmony, the game hooks and multiplayer.
- If everything loaded, it closes by itself after a few seconds. If something failed, it stays until you click it.
- The main menu's version line now also shows the mod's version, e.g. `Going Public Server 1.0.13`.

### New: the mod's news in the game's News panel
- The News panel on the main menu shows the mod's news first, with the mod's picture; the game's own news follow behind the arrows. A click on a card opens it on GitHub.
- The news come from the README of this repository: section **News** (German: **Neuigkeiten**), one card per heading below it. New news need no new mod version.

### Removed: the bug report of the original Going Public
- **Report a Bug** in the Multiplayer menu, the purple button in the top bar and the chat command `/bug` are gone. Their reports went to the original Going Public's Discord, not to this server's admin. Nothing is uploaded any more.

### Already in the last 1.0.12 build, missing from its notes
- The server no longer saves when a player joins or a join breaks off; the automatic save stays (as do the saves when a player leaves the game or the server stops).
- Save first, then join: before every join the server saves the world, then lets the player in. A save waits while a player is loading into the world.
- Joining with a save made inside a building failed (the lobby kept waiting, "save is damaged"). The player now starts in front of the building.
- On joining, the server always uses its own copy of the save, never the player's local one.
- **New world: name** in AMP creates a new world (Easy, Normal, Hard or Custom). The Custom values have their own tab **Custom**.
- Navigation: **Walk there** no longer sends the character to the spot under the button - nor does any button that closes a mod window.
- **A restart loads the newest save**: the first try at start always failed and the server loaded the second newest save - every restart (each scheduled backup too) turned the world back a bit. "Load a save once" and the console command `load` now work as well.

### Updating
1. Players: update the Workshop subscription to 1.0.13.
2. Server: update the instance in AMP (it downloads this release), then restart it.
