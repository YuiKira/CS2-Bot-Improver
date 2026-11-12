# R3 Corrected: Runtime Reaction Delay, Smoke Tuning and HE Only Disabled

This replaces the original R3 asset under the same release tag. No additional R release is introduced.

## November 12 game configuration synchronization

- Refresh all three bundled gameinfo.gi files against the game-file snapshot at SteamTracking/GameTracking-CS2 commit `82e2b9d62cc97f1358c68a220a4a392c9e41c509`, from November 12 in China time. The previous online backup exactly matched the pre-update game file.
- Include `ForceUseBuildVersion 1`, the new network receive limits and `cl_interp_parallel 1`. Generate BOT mode by adding only the existing profile and Metamod SearchPaths entries to that updated baseline; preserve all plugin binaries, profiles, skins, maps and tuning.
- This corrects an omitted game configuration update. Its effect on low FPS and map loading has not been verified in-game; do not treat it as a confirmed performance fix. The user reports that disabling CSS and the three native BOT plugins did not improve performance.
- Restore temporarily renamed .vdf files before testing this package. Fully exit CS2, install the complete package and test once with the same settings. For a clean baseline if symptoms remain, back up the current gameinfo.gi and user settings, then use Steam file verification to obtain the current Valve gameinfo.gi and launch CS2 directly without the BOT panel re-enabling mod paths. Do not copy an older online backup over the verified file.

## Log-confirmed startup fixes

- Fix BotAI's reaction hook initialization. CounterStrikeSharp returns a native function-wrapper handle, which must not be compared with the engine function's code address. That comparison previously rejected a valid hook, failed BotAI loading and rolled back its patches. Validate the resolved signature and nonzero wrapper separately, and only unhook a successfully installed hook.
- Defer reaction-profile entity enumeration until game ticks. Plugin capability discovery can happen in the main menu before engine globals exist.
- Fix the map menu reading player buttons before a pawn or movement services exist, both at player activation and while handling an open menu.
- BotAI is now `1.8.15-reaction.2`; the map chooser is `1.8.0-startup.1`. Timing amounts, HE filtering, smoke behavior and skin settings are unchanged.
- Both modules compile with zero warnings/errors. The updated package passes 697 managed API-reference checks, 19 reaction-gate checks, 253 cosmetic checks and 10 grenade-policy checks. These checks do not execute native game hooks.
- These fix errors observed in supplied logs. They are not a verified fix for the reported low FPS or slow map loading: the user reports those symptoms also occur with CounterStrikeSharp disabled. Native-plugin isolation and an actual game test remain necessary. The extra RayTraceImpl initialization error is outside the packaged managed modules and remains unresolved.

## November 12 compatibility repair for CS2 1.41.9.7

- Replace CounterStrikeSharp 1.0.379 with the official successful CI build 1.0.380-6, from commit `2eb9a57d800b662e1d728188f7441fa0ede8bf57` (run `43213602663`). The game changed its network message interface; the old native runtime can crash while loading a map even without third-party plugins.
- Use the matching official Windows native DLL, managed API publish output and gamedata. Preserve the installed runtime, user configuration, skins, map UI and all custom BOT tuning.
- Keep BotAI's original-byte validation enabled. The optional bombsite-search redirection no longer matches this game build and is safely skipped; no unverified relative-call displacement is installed. Other patch signatures match the pinned November 11 signature report. Signature checks alone do not prove complete runtime compatibility.
- This is an official CI compatibility build, not a new formal CounterStrikeSharp release. Its build and managed tests passed. This package still needs an actual CS2 map/join test; no game is available in the build environment.
- Package checks passed: 694 direct managed API references, 253 cosmetic cases, 19 reaction-gate cases and 10 compiled grenade-policy cases. A full archive comparison preserves 591 existing files byte-for-byte; changed files are limited to the CSS overlay and release metadata/notes.

Primary references: [official crash report](https://github.com/roflmuffin/CounterStrikeSharp/issues/1491), [official compatibility fix](https://github.com/roflmuffin/CounterStrikeSharp/commit/2eb9a57d800b662e1d728188f7441fa0ede8bf57), [successful official CI run](https://github.com/roflmuffin/CounterStrikeSharp/actions/runs/43213602663), and [pinned BOT signature reports](https://github.com/XBribo/cs2-bot-signatures/tree/194bade8093a8bf83dd8efdcbb8ef19cab071db4).

## Official 1.4.8 integration

- Use the official 1.4.8 Windows package as the base, with the CounterStrikeSharp compatibility overlay above, BotController 0.9.0, BotHider 0.7.0, and Panel.exe.
- Include BotAI 1.8.15's bombsite patch correction while preserving all-difficulty reaction holds.
- Include the published Smarter-Bot 1.10.6 combat-crouch fix, excluding knives and tasers, while preserving the ammo-selection fix and disabling damage-triggered smoke reveal.
- Update the cosmetic catalog with the new charm from BotRandomizer 1.3.5. Custom skin/knife choices, seeds, wear, StatTrak, stickers and ground-knife behavior remain available.
- Keep map voting and the custom equipment UI; the tools launcher now opens Panel.exe.
- Merge original retake settings, including 5-second freeze time and 3-second round restart delay.
- Continue replacing the existing R3 asset and tag; no additional R release is introduced.

## Bot reaction at all difficulties

- Replace BotAI's unconditional visible-enemy notice patch with a native notice pre-hook.
- Pro templates wait an additional 100 ms before a new visible enemy can be noticed; Rank templates wait an additional 40 ms. Bot profiles are read through the packaged BotController API.
- The timer acts before newly noticed targets enter normal aim/attack behavior. It does not add a pause between every shot. Continuous visibility preserves the encounter; losing sight for more than 0.5 seconds starts a new one.
- Restore original profile ReactionTime values so the earlier configuration-only increment does not stack with the runtime wait.
- Low, Medium and High all receive the same template-specific extra wait. Native scheduling may round waits upward. This is an awareness delay, not a promise of an exact end-to-end shot timing or a change to accuracy.

## Smoke visibility

- Disable the damage-triggered global smoke reveal. Hitting a bot no longer makes the attacker visible through smoke to all bots for 0.8 seconds.
- Extend bullet smoke tunnel duration from 1 to 2 seconds; tunnel radius and smoke density threshold are unchanged.
- The existing bomb-defuse reveal cycle is preserved.

## Grenades

- Only bot high-explosive (HE) grenades are blocked, at database, replay and projectile entry points.
- Flashbangs, smoke, molotovs/incendiaries and decoys retain original NadeSystem v1.2.3 behavior in normal mode. Human grenades are unaffected.
- Other native compatibility binaries, skins, maps, aim priorities and AttackDelay remain unchanged.

## Install and verify

1. Fully close CS2 before overwriting the installation with the new release-windows.zip from this R3 release. The filename is unchanged; download it again.
2. Select any difficulty (Low, Medium or High) in the panel, then start CS2. The delay applies to all three.
3. Original panel is Panel.exe; BotVision should be 0.4.0; the custom skin plugin is 1.6.2-upstream148.1. Plugin versions should be Bot AI 1.8.15-reaction.2, map chooser 1.8.0-startup.1 and NadeSystem 1.2.3-nohe.1; Smarter-Bot should be 1.10.6-nohurtreveal.1.
4. Run bot_reaction_status after an encounter. It reports hook=active, difficulty=all, profileAPI=ready, held/released counts and observed notice hold in milliseconds. If inactive/pending, or counts remain zero after combat, report that output; a loaded configuration alone is not proof of active timing.
5. bot_nades should report normal. If an older console/config command set it to off, use bot_nades normal. HE stays blocked in every mode.

Verification: 253 cosmetic merge checks, 19 timing/state checks, 10 compiled HE/non-HE policy checks, packaged managed API references, gameinfo mode round-trip/layer validation, and full package hash/CRC checks. Original files outside the explicit custom overrides and the existing custom UI/map files are checked byte-for-byte. This environment cannot run a CS2 match; runtime status is provided for in-game verification.
