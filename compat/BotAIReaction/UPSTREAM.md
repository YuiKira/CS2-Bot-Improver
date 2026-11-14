# Runtime reaction gate

Based on ed0ard/CS2-BotAI release v1.8.16, including the bombsite-call correction and lower reload-ammo threshold. Both platform patch definitions match that release; the custom notice hook and startup guards remain in place.

All native patches remain unchanged except IsNoticable_AlwaysTrue. Instead of overwriting that function with an unconditional true return, a pre-hook provides the same return after an additional per-bot/per-enemy wait: Pro +100ms, Rank +40ms. The gate applies at all difficulties (Low, Medium and High), without a selected-VPK restriction. The original profile ReactionTime values are restored so the old configuration increment does not stack with the new gate.

The timing starts when native visibility asks whether a visible candidate should be noticed. After the wait, upstream behavior resumes. This does not insert a delay between every shot. Sight gaps over 0.5 seconds and round/map resets start a new encounter. The gate does not affect human players' command processing.

bot_reaction_status reports hook availability, all-difficulty scope, actual profile API values and observed notice holds. This is a notice delay, not a claim that every shot lands exactly 100ms/40ms later. Native update cadence can round waits upward.

Build with CounterStrikeSharp.API 1.0.380 and pass BotControllerApiPath pointing to the 0.9.0 API DLL from the validated package.

Revision reaction.2 fixes startup validation: CSS's BaseMemoryFunction.Handle is
a pointer to its ValveFunction wrapper, not the server code address returned by
FindSignature. Validate the engine signature separately and require a nonzero
wrapper. Entity/profile enumeration starts on game ticks, after engine globals
exist, rather than in OnAllPluginsLoaded. Only successfully installed hooks are
unhooked. These correct log-confirmed startup failures, not the unresolved low-FPS
report that also occurs with CSS disabled.
