# R3 Corrected: Runtime Reaction Delay, Smoke Tuning and HE Only Disabled

This replaces the original R3 asset under the same release tag. No additional R release is introduced.

## Official 1.4.7 integration

- Use the official 1.4.7 Windows package as the base, including BotVision 0.4.0, current native signatures, BotHider and the original Panel 1.4.7 with version detection.
- Include Smarter-Bot 1.10.5's loaded-weapon selection fix while keeping damage-triggered smoke reveal disabled.
- Port BotRandomizer 1.3.4's item-view lifetime fixes, cosmetic pool caching, and matching intro agents into the custom skin module. Custom skin/knife choices, seeds, wear, StatTrak, stickers and ground-knife behavior are retained.
- Keep map voting and the custom equipment UI. The tools launcher now opens Panel 1.4.7.
- Merge original mode/rule configurations and gameinfo updates. This remains R3 under the existing tag; it is not an additional R release.

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
3. Original panel should be 1.4.7; BotVision should be 0.4.0; the custom skin plugin is 1.6.2-upstream147.1. Plugin versions should be Bot AI 1.8.14-reaction.2 and NadeSystem 1.2.3-nohe.1; Smarter-Bot should be 1.10.5-nohurtreveal.1.
4. Run bot_reaction_status after an encounter. It reports hook=active, difficulty=all, profileAPI=ready, held/released counts and observed notice hold in milliseconds. If inactive/pending, or counts remain zero after combat, report that output; a loaded configuration alone is not proof of active timing.
5. bot_nades should report normal. If an older console/config command set it to off, use bot_nades normal. HE stays blocked in every mode.

Verification: 253 cosmetic merge checks, 19 timing/state checks, 10 compiled HE/non-HE policy checks, 679 packaged managed API references, gameinfo mode round-trip/layer validation, and full package hash/CRC checks. 548 original files and 29 custom UI/map files are preserved byte-for-byte. This environment cannot run a CS2 match; runtime status is provided for in-game verification.
