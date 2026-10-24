# CS2 1.41.9.2 Compatibility Update - Skins And Maps r2

October 24, 2026 compatibility refresh for the custom Windows 1.4.6 package. This is not an official upstream 1.4.7 release.

## Why This Update

CounterStrikeSharp issue #1466 reports reproducible Windows crashes when players/Bots are authorized on CS2 1.41.9.2 with CSS 1.0.376, even without Bot plugins. CSS 1.0.378 addresses the game update. This is a strong candidate for the reported join crash, not a confirmed diagnosis from this user's crash dump. Bot patches and projectile signatures have also been updated upstream.

## Included Repairs

- MetaMod 2.0.0 build 1483 and CounterStrikeSharp 1.0.378 with runtime.
- Official BotController 0.7.2 (native, managed provider and shared API), BotHider 0.5.2 and BotVision 0.3.1. These require MetaMod 1481 or later.
- Official BotAI 1.8.14, BotAimImprover 2.1.8, BotState 1.10.4 and NadeSystem 1.2.3. Original logic is used without custom strategy edits.
- Backport native gamedata from pinned upstream commits for BotController and BotVision; required keys are checked before replacement. No invented signatures or offsets. Newer unreleased native code is NOT included.
- Rebuild our skin plugin, map chooser and map API against CSS 1.0.378. Preserve equipment UI, custom skin settings support, map list, grenade lineups, game rules and the previous gameinfo startup fix.

## Installation And Validation

Close CS2 and both panels. Back up the installation and BotRandomizer.custom.json. Extract the entire package into game/csgo, replacing files, especially addons/metamod, the CSS core/runtime and all included plugin modules. Do not copy only the Bot DLLs. Use Panel v1.4.6.exe or CS2 Bot Tools.exe; do not restore older gameinfo backups.

The ZIP does not contain user cosmetic settings. Checksums, official asset digests, protected content, 648 direct managed CSS/Bot/map API references, five-slot sticker configuration, gameinfo mode switching and builds pass verification. Native calls were not executed in these checks. Native signatures have not been scanned against this user's game binaries, and this package has not been tested in-game here. This is a compatibility attempt, not a guarantee that every crash is fixed. Older RayTrace binaries and the existing gameinfo baseline remain unchanged; additional native failures may need separate diagnosis.

If it still crashes, provide the CS2 version output, whether it crashes at menu/map load/Bot spawn, meta list, css_plugins list, CSS logs and a crash dump if available. Remove secrets/personal information. Keep 1.4.6-r1 for rollback, but it is not expected to fix CS2 1.41.9.2.

## Sources

- https://github.com/roflmuffin/CounterStrikeSharp/issues/1466
- https://github.com/roflmuffin/CounterStrikeSharp/releases/tag/v1.0.378
- https://github.com/roflmuffin/CounterStrikeSharp/pull/1470
- https://github.com/alliedmodders/metamod-source/releases/tag/2.0.0.1483
- https://github.com/XBribo/CS2-Bot-Controller/releases/tag/v0.7.2
- https://github.com/XBribo/CS2-Bot-Hider/releases/tag/v0.5.2
- https://github.com/XBribo/CS2-Bot-Vision/releases/tag/v0.3.1
- https://github.com/ed0ard/CS2-BotAI/releases/tag/v1.8.14
- https://github.com/ed0ard/CS2-Bullseye-Bot/releases/tag/v2.1.8
- https://github.com/ed0ard/CS2-Smarter-Bot/releases/tag/v1.10.4
- https://github.com/ed0ard/CS2-Bot-NadeSystem/releases/tag/v1.2.3

Pinned commit IDs and checksums are in release-components.json and scripts/package-oct24.js. Legacy vendored third-party source snapshots are not used to rebuild official DLLs.

## Rebuilding

Build BotRandomizer and GG1MapChooser in Release mode, then run node scripts/package-oct24.js with the 1.4.6-r1 custom ZIP, the official download directory and a new output ZIP path. Set CS2_DOTNET if dotnet is not on PATH. The build rejects wrong source ZIPs, signature snapshots and official downloads, and runs scripts/ApiSmoke against staged assemblies.
