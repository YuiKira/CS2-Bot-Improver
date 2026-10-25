# R3 Corrected: Runtime Reaction Delay and HE Only Disabled

This replaces the original R3 asset under the same release tag. No additional R release is introduced.

## Medium bot reaction

- Replace BotAI's unconditional visible-enemy notice patch with a native notice pre-hook.
- Pro templates wait an additional 100 ms before a new visible enemy can be noticed; Rank templates wait an additional 40 ms. Bot profiles are read through the packaged BotController API.
- The timer acts before newly noticed targets enter normal aim/attack behavior. It does not add a pause between every shot. Continuous visibility preserves the encounter; losing sight for more than 0.5 seconds starts a new one.
- Restore original profile ReactionTime values so the earlier configuration-only increment does not stack with the runtime wait.
- Low and High keep upstream behavior. Native scheduling may round waits upward. This is an awareness delay, not a promise of an exact end-to-end shot timing or a change to accuracy.

## Grenades

- Only bot high-explosive (HE) grenades are blocked, at database, replay and projectile entry points.
- Flashbangs, smoke, molotovs/incendiaries and decoys retain original NadeSystem v1.2.3 behavior in normal mode. Human grenades are unaffected.
- Other native compatibility binaries, skins, maps, aim priorities and AttackDelay remain unchanged.

## Install and verify

1. Fully close CS2 before overwriting the installation with the new release-windows.zip from this R3 release. The filename is unchanged; download it again.
2. Select Medium in the panel, then start CS2. Existing Low/High selections intentionally do not receive the extra delay.
3. Plugin versions should be Bot AI 1.8.14-reaction.1 and NadeSystem 1.2.3-nohe.1.
4. Run bot_reaction_status after an encounter. It reports hook=active, Medium=True, profileAPI=ready, held/released counts and observed notice hold in milliseconds. If inactive/pending, or counts remain zero after combat, report that output; a loaded configuration alone is not proof of active timing.
5. bot_nades should report normal. If an older console/config command set it to off, use bot_nades normal. HE stays blocked in every mode.

Verification: 19 timing/state checks, compiled HE/non-HE policy checks, packaged managed API compatibility and full package difference/CRC checks. This environment cannot run a CS2 match; runtime status is provided for in-game verification.
