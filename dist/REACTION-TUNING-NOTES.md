# R3 Corrected: Runtime Reaction Delay, Smoke Tuning and HE Only Disabled

This replaces the original R3 asset under the same release tag. No additional R release is introduced.

## Official 1.4.8 integration

- Use the official 1.4.8 Windows package as the base, including CounterStrikeSharp 1.0.379, BotController 0.9.0, BotHider 0.7.0, current native signatures, and Panel.exe.
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
3. Original panel is Panel.exe; BotVision should be 0.4.0; the custom skin plugin is 1.6.2-upstream148.1. Plugin versions should be Bot AI 1.8.15-reaction.1 and NadeSystem 1.2.3-nohe.1; Smarter-Bot should be 1.10.6-nohurtreveal.1.
4. Run bot_reaction_status after an encounter. It reports hook=active, difficulty=all, profileAPI=ready, held/released counts and observed notice hold in milliseconds. If inactive/pending, or counts remain zero after combat, report that output; a loaded configuration alone is not proof of active timing.
5. bot_nades should report normal. If an older console/config command set it to off, use bot_nades normal. HE stays blocked in every mode.

Verification: 253 cosmetic merge checks, 19 timing/state checks, 10 compiled HE/non-HE policy checks, packaged managed API references, gameinfo mode round-trip/layer validation, and full package hash/CRC checks. Original files outside the explicit custom overrides and the existing custom UI/map files are checked byte-for-byte. This environment cannot run a CS2 match; runtime status is provided for in-game verification.
