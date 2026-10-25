# Medium Reaction Tuning and Bot Grenades Off

Based on the working CS2 1.41.9.2 compatibility release r2.

- Medium Pro templates: add 100 ms to their existing ReactionTime.
- Medium Rank templates: add 40 ms to their existing ReactionTime.
- Update both the selected default profile and the Medium preset VPK.
- Disable plugin grenade usage with bot_nades off in every shipped bot mode config.
- Keep native bot grenade usage disabled with bot_allow_grenades 0.
- Keep Low/High profiles, aiming, AttackDelay, weapon behavior, compatibility binaries, skins and maps unchanged.

Install this package over r2 and reselect Medium in the panel, then restart the game. For an already running match, execute bot_nades off in the console to disable plugin grenades immediately. Explicitly selecting another grenade mode later will re-enable that mode.

Package contents, VPK CRCs and profile changes are verified automatically. This tuning has not been play-tested in CS2 here. Configured ReactionTime is not a measured end-to-end response time.
