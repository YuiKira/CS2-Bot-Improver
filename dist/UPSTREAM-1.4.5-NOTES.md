# CS2 Bot Improver 1.4.5 - Custom Skins And Maps Beta 1

Based on the official Windows v1.4.5 package published October 3, 2026.

## Changes

- Adopt the complete official v1.4.5 Bot, grenade, native dependency and configuration stack, including Rush mode, behavior trees, FOV/radio/plant-defense improvements and the new Rules panel.
- Preserve official behavior modules, dependencies, configurations, lineup data and Panel v1.4.5.exe byte-for-byte. No custom Bot or grenade strategies are introduced.
- Retain our equipment panel, custom skin selections, five sticker slots and map voting/switching.
- Import the official updated cosmetic catalog, including new stickers and music kits. The custom sticker selector retains its existing translated catalog; new stickers are available to runtime randomization but are not yet individually listed in that selector.
- Retain the startup fix: use the current official gameinfo baseline and add only two Bot/MetaMod search paths. The equipment panel patches current SearchPaths instead of restoring an obsolete layered configuration.

## Installation And Validation

Back up your installation and custom settings, then copy the package to `game/csgo`. Use `CS2 Bot Tools.exe` for equipment and `Panel v1.4.5.exe` for the original controls. Remove obsolete `Panel v1.4.4.exe` and duplicate `publish` directories left by older packages. Do not reapply old gameinfo backups.

Package integrity, unchanged official file checksums, managed Bot API reference resolution, custom sticker configuration and gameinfo round-trip checks pass. Custom plugins and desktop panel compile successfully.

No CS2 client/server is available here. This custom build has NOT been tested in-game and remains a Beta even though its base is an official release.

Report failures with the CS2 version, `meta list`, `css_plugins list`, error logs and reproduction steps. Remove secrets and personal data first.

## Rebuilding

Build BotRandomizer, GG1MapChooser and DesktopPanel in Release mode, then run:

```sh
CS2_DOTNET=/path/to/dotnet node scripts/package-upstream.js /path/to/official-v1.4.5.zip /path/to/previous-custom.zip /path/to/output.zip
```

The official ZIP checksum is pinned. The previous custom package contributes only map voting files. Official third-party binaries are used directly; legacy vendored Bot sources are not rebuilt. `beta-components.json` records all unchanged official files.
