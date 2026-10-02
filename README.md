# AutoSyncMap

Shares every player's explored map and pins on a Valheim dedicated server automatically, without
anyone visiting a cartography table.

- A new tick-box on the large map, **Auto sync map**, next to "Visible to other players".
- While it is on, your map is shared when you join and once every in-game day.
- You receive everyone else's explored areas and pins the same way.
- It works between players who are never online together: the server keeps the shared map.

## Who installs it

| Where | What it does there |
| --- | --- |
| **The dedicated server** | Keeps the one shared map and hands it to players who sync. |
| **Each player who wants it** | Adds the tick-box and does the syncing. |

It is the same DLL in both places. Players without it are not affected: they keep using
cartography tables as usual, and can play alongside players who have it.

## How it works

A sync is exactly what a cartography table does, done for you:

1. Your game sends its shared map data to the server (the same data "record discoveries" writes to
   a table).
2. The server merges it into its one shared map and sends that back.
3. Your game merges it in (the same as "read map" at a table).

The merge on the server follows two rules:

- **Explored areas are only ever added.** Nothing anyone explored is lost.
- **Your pins are yours.** Each sync replaces your pins on the shared map with the set you have now,
  so deleting one of your own pins removes it for everyone at their next sync. Other players' pins
  are never changed by your sync.

As with a table, death markers are not shared, and other players' areas and pins show as shared map
data (the game's "shared map" display toggle applies to them).

## When it syncs

- About 20 seconds after you spawn into the world.
- Each time a new in-game day begins.
- Straight away when you switch the tick-box on.

In-game time on a dedicated server only moves while someone is online, so "once every in-game day"
is about every 20 minutes of play.

## Installation

Requires [BepInEx 5](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).

- **Server:** put `AutoSyncMap.dll` in `BepInEx/plugins/` and restart.
- **Player:** put `AutoSyncMap.dll` in `BepInEx/plugins/` in your Valheim folder, or install it with
  a mod manager.

It is meant for dedicated servers. In single player, or when hosting a game from your own client,
it does nothing.

## Settings

`BepInEx/config/liekos47.autosyncmap.cfg`, written on first run. These are per player.

| Setting | Default | What it does |
| --- | --- | --- |
| `AutoSync` | `true` | The tick-box on the map. Off: nothing is sent or received. |
| `ShowMessage` | `true` | Show a short top-left message after each sync. |
| `Label` | `Auto sync map` | The text beside the tick-box. |
| `OffsetX` | `0` | Moves the tick-box sideways. Rejoin to see a change. |
| `OffsetY` | `0` | Moves the tick-box up or down. 0 puts it one row above "Visible to other players", or above the game's "Cartography Table" row when that is showing; a negative number moves it down. |

## What the server stores

One file beside the world save: `worlds_local/<world>.autosyncmap.dat`, the shared map in the
game's own format, compressed. Keep it with the world when you back it up or move it. Deleting it
resets the shared map; players' own maps are not affected, and it fills up again as they sync.

## Licence

MIT.
