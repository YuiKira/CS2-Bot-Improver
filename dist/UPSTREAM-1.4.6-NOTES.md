# CS2 Bot Improver 1.4.6 - Custom Skins And Maps

Based on the official Windows v1.4.6 package published October 11, 2026.

## Changes

- Adopt the complete official v1.4.6 Bot and grenade stack, updated signatures/offsets, blinded-state fix and situational-awareness improvements.
- Preserve official behavior modules, native dependencies, game configs, grenade lineups and Panel v1.4.6.exe byte-for-byte. No custom Bot or grenade strategies are introduced.
- Retain our equipment panel, custom skin selections, five sticker slots and map voting/switching. Use the official packaged cosmetic catalog.
- Refresh the official gameinfo baseline and retain the startup fix: add only the two Bot/MetaMod search paths. The equipment panel patches the current configuration instead of restoring an obsolete layered configuration.

## Installation

Back up your installation and custom settings, then copy the package into `game/csgo`. Use `CS2 Bot Tools.exe` for equipment settings and `Panel v1.4.6.exe` for original controls. Stop using old Panel executables and old gameinfo backups. Existing custom settings are not included in or overwritten by this ZIP.

The custom sticker selector retains its existing translated catalog. Stickers added upstream since that catalog are available to runtime randomization but are not individually listed in the selector.

## Validation

Custom plugins and desktop panel compile successfully. Gameinfo generation, idempotent mode switching, five-slot sticker configuration, managed Bot API references, official-file checksums and ZIP integrity checks pass. The uploaded asset checksum is verified against the local package.

This is a regular release following the previously validated integration approach. This specific 1.4.6 custom package has not been tested in-game in the build environment; previous user validation applied to 1.4.5.

## Rebuilding

Build BotRandomizer, GG1MapChooser and DesktopPanel in Release mode, then run:

```sh
CS2_DOTNET=/path/to/dotnet node scripts/package-upstream.js /path/to/official-v1.4.6.zip /path/to/previous-custom.zip /path/to/output.zip
```

The official ZIP SHA-256 is pinned. The previous custom package contributes only map voting files. `release-components.json` records unchanged official files. Official third-party binaries are used directly; legacy vendored Bot source snapshots are not rebuilt.
