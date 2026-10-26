# Smarter-Bot without damage-triggered smoke reveal

Based on ed0ard/CS2-Smarter-Bot v1.10.4, commit e8c74af3bd2465478d6d07742f25ce53f4302bc7.

Remove only the player_hurt handler registration and its damage-triggered smoke reveal code. Damaging a bot no longer adds or refreshes the attacker's global smoke reveal. Bomb-defuse reveal cycles and other bot behavior are preserved.

Build against CounterStrikeSharp.API 1.0.378 with BotControllerApiPath pointing to the packaged BotController 0.7.2 API DLL.
