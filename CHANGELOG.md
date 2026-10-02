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
