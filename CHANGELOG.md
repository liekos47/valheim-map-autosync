## 0.3.0

- Renamed from AutoSyncMap to MapAutoSync. The DLL, the settings file
  (`liekos47.mapautosync.cfg`) and the server's shared map file (`<world>.mapautosync.dat`) carry
  the new name.
- The server and the players must all be on 0.3.0 or later: it does not sync with the earlier
  versions under the old name.

## 0.2.0

- New "Sync now" button on the large map, one row above "Auto sync map". Click it to sync straight
  away instead of waiting for the next in-game day. Its box stays ticked while the sync is under
  way and clears when the server's map has arrived.
- "Sync now" also works while "Auto sync map" is off, for players who prefer to sync by hand.
- Players only. The server does not need this update.

## 0.1.1

- Fixed the "Auto sync map" tick-box sitting on top of the game's "Cartography Table" row. That row
  appears once your map holds shared data, so the tick-box now moves one row further up while it
  is showing.
- First run in a real game: the tick-box appears and a sync with the server works.
- Players only. The server does not need this update.

## 0.1.0

- First version. "Auto sync map" tick-box on the large map; shares explored areas and pins with
  the server on joining and once every in-game day, using the game's own cartography table data.
- The server keeps one merged map beside the world save: explored areas are only added, and each
  player's pins are replaced by their latest set.
- The server's merge is covered by a test with made-up maps from two players. The in-game side
  (the tick-box and the syncing) has not been tried in a running game yet.
- Built against Valheim l-1.0.16.
