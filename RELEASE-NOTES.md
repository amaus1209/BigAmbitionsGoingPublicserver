## Going Public Server 1.0.15

**Server and players both need 1.0.15.** Please update the Workshop mod and the server together.

### Linux / Proton
- **Fixed: "Open in BizMan" showed the empty BizMan list** instead of the building page (rent an apartment, buy a building, ...). Under Wine/Proton, Harmony wrote most game hooks as a short jump through a pointer cell next to the game's code, and those cells did not stay intact there. Calling a hooked method then failed at its first instruction (`BizManBusiness.SetInitialTab`, `MyEmployeesMassActionsUI.UpdateMassActionDropdown`, `SaveGamePathHelper.CurrentVersionFolderPath`; the crash dump showed a jump to data). Under Wine every hook is now a self-contained jump; Windows is unchanged. The load console shows the line "Proton/Wine: game hooks written as self-contained jumps".
- Backstop: if `SetInitialTab` still fails when a building is opened in BizMan, the mod picks the start tab with the game's own rule and opens the building page anyway.
- The hook check in the log (`[DetourCheck]`) now also lists the jump kinds and watches every pointer cell.

### Navigation
- A destination is named by its address ("44 Second Street") instead of the street key (`ba:street_secondstreet 44`).

### Updating
1. Players: update the Workshop subscription to 1.0.15.
2. Server: **Update** the instance in AMP (it downloads this release), then restart it.
