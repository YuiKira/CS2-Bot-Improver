# Smarter-Bot without damage-triggered smoke reveal

Based on ed0ard/CS2-Smarter-Bot v1.10.6, commit ae672d36ab3ba0717bc921187d0aa44786b8a864.

Remove only the player_hurt handler registration and its damage-triggered smoke reveal code. Damaging a bot no longer adds or refreshes the attacker's global smoke reveal. Bomb-defuse reveal cycles and other bot behavior are preserved.

Build against CounterStrikeSharp.API 1.0.378 with BotControllerApiPath pointing to the packaged BotController 0.9.0 API DLL.
