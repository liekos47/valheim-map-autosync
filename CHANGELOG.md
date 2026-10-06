## 0.3.0

- Renamed from AutoSyncMap to MapAutoSync. The DLL, the settings file
  (`liekos47.mapautosync.cfg`) and the server's shared map file (`<world>.mapautosync.dat`) carry
  the new name.
- "Sync now" tells you what it is doing: its label and large text at the top of the map show
  "Syncing", then "Sync complete", or that the server did not answer. It also sends straight away
  instead of up to two seconds later.
- "Map synced" after an automatic sync is shown in the same large text on the map while the map is
  open, instead of the small top-left message that was easy to miss beside the map.
- The tick-box and the button now have the same dark shade behind their labels as the game's own
  rows.
- A server on 0.3.0 still syncs players on older versions (AutoSyncMap 0.1.0 to 0.2.0) and tells
  them on screen to update. It will do the same for versions older than its own in future.
- A server's shared map from before 0.3.0 is carried over to the new file name by itself.
- Players on 0.3.0 need the server on 0.3.0: update the server first.

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
