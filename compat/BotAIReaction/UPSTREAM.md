# Runtime reaction gate

Based on ed0ard/CS2-BotAI v1.8.14, commit 7d6997e11b952abb899d71abefa959c6e49cdfef.

All native patches remain unchanged except IsNoticable_AlwaysTrue. Instead of overwriting that function with an unconditional true return, a pre-hook provides the same return after an additional per-bot/per-enemy wait: Pro +100ms, Rank +40ms. Low/High preserve upstream behavior. The selected Medium VPK is identified by SHA256; the original profile ReactionTime values are restored so the old configuration increment does not stack with the new gate.

The timing starts when native visibility asks whether a visible candidate should be noticed. After the wait, upstream behavior resumes. This does not insert a delay between every shot. Sight gaps over 0.5 seconds and round/map resets start a new encounter. The gate does not affect human players' command processing.

bot_reaction_status reports hook availability, selected profile, actual profile API values and observed notice holds. This is a notice delay, not a claim that every shot lands exactly 100ms/40ms later. Native update cadence can round waits upward.

Build with CounterStrikeSharp.API 1.0.378 and pass BotControllerApiPath pointing to the 0.7.2 API DLL from the validated package.
