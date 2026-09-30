# CS2 September 24 Update Compatibility - Windows Beta 1

This is an experimental compatibility package, not a confirmed in-game fix.
Back up your existing installation and settings before replacing the package.

## Changes

- Update MetaMod to 2.0.0.1472 and CounterStrikeSharp (with runtime) to 1.0.376.
- Update the complete BotController native/managed stack to 0.7.0 (ABI 22), BotHider to 0.5.1, and BotVision to 0.3.0 using official release binaries.
- Apply the upstream September 25 economic attribute signature fix to our custom skins plugin (1.6.2-beta.1).
- Rebuild skins and map voting against CounterStrikeSharp 1.0.376; retain the equipment panel and map-switching features.
- Keep the original BotAI, BotState, BotAimImprover, BotBuy and NadeSystem binaries and behavior unchanged.
- Remove the stale duplicate desktop publish directory from the package.

## Validation And Limitations

The custom plugins and desktop panel compile successfully. The five-slot custom sticker configuration smoke test passes. All 11 direct managed member references to BotControllerApi/BotHiderApi in the packaged plugins resolve against the updated APIs, including the seven calls in the unchanged BotState binary; no native calls were executed by this check. Package integrity, dependency checksums and unchanged upstream behavior binaries are checked during packaging.

No CS2 client/server is available in the build environment, so plugin loading, skins, map transitions, Bot movement/aim and grenade behavior have NOT been tested in-game. Remaining native signatures in legacy behavior plugins may still require upstream fixes. Do not treat this Beta as production-ready.

BotController 0.7.0 changes its recording format; older BotController recordings are not compatible. This is not a claim that NadeSystem's own data format changed.

When reporting a failure, include the CS2 version, `meta list`, `css_plugins list`, the CounterStrikeSharp error log, and reproduction steps. For crashes, include the crash dump if available. Remove secrets and personal data first.

## Rebuilding

Use the previous `cs2-v1.4.4-skin-map-r1` release ZIP as the base. Build BotRandomizer, GG1MapChooser and DesktopPanel in Release mode, download the exact assets listed in `scripts/package-cs2-beta.js`, then run:

```sh
node scripts/package-cs2-beta.js /path/to/previous-release.zip /path/to/downloads /path/to/new-release.zip
```

The script verifies all downloaded checksums and embeds `beta-components.json`. The updated third-party providers are packaged from official binaries; the repository's older third-party source snapshots are not used to rebuild those providers.
