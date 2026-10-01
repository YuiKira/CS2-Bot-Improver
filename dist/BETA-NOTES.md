# CS2 September 24 Update Compatibility - Windows Beta 1

This is an experimental compatibility package, not a confirmed in-game fix.
Back up your existing installation and settings before replacing the package.

Updated in place on October 1, 2026. Download the asset again if you downloaded the September 30 build.

Also refreshed on October 1 to fix the fatal `csgo_imported/gameinfo.gi` loading error. The package now uses the official configuration supplied from the affected installation, with only the two Bot/MetaMod search paths added. All three bundled gameinfo files are updated. The equipment panel patches the current game's SearchPaths instead of restoring a stale complete configuration; missing layered-mod dependencies are rejected with a Steam verification prompt. Other game builds may still require restoring their own official gameinfo first.

## Changes

- Update MetaMod to 2.0.0.1472 and CounterStrikeSharp (with runtime) to 1.0.376.
- Update the complete BotController native/managed stack to 0.7.1 (ABI 22), BotHider to 0.5.1, and BotVision to 0.3.0 using official release binaries. BotController 0.7.1 primarily fixes Linux loading; it does not claim a new Windows Bot behavior fix.
- Update NadeSystem to the official 1.2.2 build, which fixes smoke, HE and Molotov projectile creation signatures for the CS2 update. Preserve grenade lineup data and upstream strategy.
- Apply the upstream September 25 economic attribute signature fix to our custom skins plugin (1.6.2-beta.1).
- Rebuild skins and map voting against CounterStrikeSharp 1.0.376; retain the equipment panel and map-switching features.
- Keep the original BotAI, BotState, BotAimImprover and BotBuy binaries unchanged. No custom Bot or grenade strategy changes are introduced.
- Remove the stale duplicate desktop publish directory from the package.

## Validation And Limitations

The custom plugins and desktop panel compile successfully. The five-slot custom sticker configuration smoke test passes. All 11 direct managed member references to BotControllerApi/BotHiderApi in the packaged plugins resolve against the updated APIs, including the seven calls in the unchanged BotState binary; no native calls were executed by this check. Package integrity, dependency checksums and unchanged upstream behavior binaries are checked during packaging.

No CS2 client/server is available in the build environment, so plugin loading, skins, map transitions, Bot movement/aim and grenade behavior have NOT been tested in-game. Remaining native signatures in legacy behavior plugins may still require upstream fixes. Do not treat this Beta as production-ready.

BotController 0.7.x changes its recording format compared with 0.6.x; older BotController recordings are not compatible. This is not a claim that NadeSystem's own data format changed.

When reporting a failure, include the CS2 version, `meta list`, `css_plugins list`, the CounterStrikeSharp error log, and reproduction steps. For crashes, include the crash dump if available. Remove secrets and personal data first.

## Rebuilding

Use the previous `cs2-v1.4.4-skin-map-r1` release ZIP as the base. Build BotRandomizer, GG1MapChooser and DesktopPanel in Release mode, download the exact assets listed in `scripts/package-cs2-beta.js`, then run:

```sh
node scripts/package-cs2-beta.js /path/to/previous-release.zip /path/to/downloads /path/to/new-release.zip
```

The script verifies all downloaded checksums and embeds `beta-components.json`. It runs the .NET GameInfoTool to generate mode configurations from `dist/gameinfo-online.gi`; set `CS2_DOTNET` if dotnet is not on PATH. The updated third-party providers are packaged from official binaries; the repository's older third-party source snapshots are not used to rebuild those providers.
