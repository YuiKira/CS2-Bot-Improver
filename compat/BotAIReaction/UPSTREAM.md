# Runtime reaction gate

Based on ed0ard/CS2-BotAI v1.8.15, commit 320a8fd3cd6bca69fd1ec511fe0c3e45b4d42ccc.

All native patches remain unchanged except IsNoticable_AlwaysTrue. Instead of overwriting that function with an unconditional true return, a pre-hook provides the same return after an additional per-bot/per-enemy wait: Pro +100ms, Rank +40ms. The gate applies at all difficulties (Low, Medium and High), without a selected-VPK restriction. The original profile ReactionTime values are restored so the old configuration increment does not stack with the new gate.

The timing starts when native visibility asks whether a visible candidate should be noticed. After the wait, upstream behavior resumes. This does not insert a delay between every shot. Sight gaps over 0.5 seconds and round/map resets start a new encounter. The gate does not affect human players' command processing.

bot_reaction_status reports hook availability, all-difficulty scope, actual profile API values and observed notice holds. This is a notice delay, not a claim that every shot lands exactly 100ms/40ms later. Native update cadence can round waits upward.

Build with CounterStrikeSharp.API 1.0.378 and pass BotControllerApiPath pointing to the 0.9.0 API DLL from the validated package.
