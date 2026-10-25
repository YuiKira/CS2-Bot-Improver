# Bot Grenades Disabled at Module Level - r4

Fixes r3, which only executed bot_nades off from mode configuration files. That did not guarantee that the command ran after NadeSystem loaded, or prevent a later command from re-enabling it.

- Replace the original NadeSystem.dll at the same installation path with an inactive module. None of the original grenade event handlers, timers, replay logic or projectile spawning code is registered.
- Enforce bot_allow_grenades=false from the plugin on every tick, including after configuration changes and map switches.
- bot_nades always reports off in this build; normal/more/max do not re-enable grenade throwing.
- Preserve r3 Medium reaction tuning: Pro templates +100 ms; Rank templates +40 ms.
- Preserve all other plugin binaries, native compatibility fixes, skins, maps and aim behavior.
- Human players' grenades are not blocked or modified.

Completely close CS2 before overwriting the old installation with release-windows.zip, then restart. Closing the game is necessary so the existing NadeSystem.dll can be replaced. The plugin list should show NadeSystem (Disabled), version 1.2.3-disabled.1. The bot_nades command should report off (disabled in this build).

This package is built and checked against the shipped CounterStrikeSharp API. Package comparison verifies that only the grenade module, its build metadata and release notes changed from r3. In-game behavior has not been play-tested here.
