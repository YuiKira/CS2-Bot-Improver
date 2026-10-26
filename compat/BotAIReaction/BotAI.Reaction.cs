using BotControllerApi;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using Microsoft.Extensions.Logging;

namespace BotAI;

public partial class BotAI
{
    private readonly ReactionGate _reactionGate = new();
    private MemoryFunctionWithReturn<nint, nint, byte, bool>? _noticeFunction;
    private IBotControllerApi? _reactionApi;
    private readonly Dictionary<nint, (int Slot, string Name, int ExtraMs, float ProfileReaction)> _reactionBots = new();
    private string _reactionError = "";
    private long _noticeCalls, _heldCalls, _releasedEncounters;
    private double _lastWaitMs;
    private string _lastBot = "none";

    private void InstallReactionGate(nint address)
    {
        if (address == 0)
            throw new InvalidOperationException("Reaction delay cannot load: IsNoticable signature not found. No reaction delay is claimed.");
        var definition = (_isLinux ? LinuxPatchDefinitions.All : WindowsPatchDefinitions.All)["IsNoticable_AlwaysTrue"];
        _noticeFunction = new MemoryFunctionWithReturn<nint, nint, byte, bool>(definition.signature);
        if (_noticeFunction.Handle != address)
            throw new InvalidOperationException("Reaction notice hook resolved to an unexpected address.");
        _noticeFunction.Hook(OnNoticeEnemy, HookMode.Pre);
        RegisterListener<Listeners.OnTick>(RefreshReactionBots);
        RegisterListener<Listeners.OnMapStart>(_ => ResetReactionState());
        RegisterEventHandler<EventRoundStart>((_, _) => { ResetReactionState(); return HookResult.Continue; });
        AddCommand("bot_reaction_status", "Show the actual notice hook and observed reaction holds.", PrintReactionStatus);
        Logger.LogInformation("[Reaction] r3-all-difficulties: notice hook installed; all difficulties; Pro +100ms, Rank +40ms.");
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        try
        {
            _reactionApi = new PluginCapability<IBotControllerApi>("botcontroller:api").Get();
            if (_reactionApi == null) throw new InvalidOperationException("botcontroller:api unavailable");
            RefreshReactionBots();
        }
        catch (Exception ex)
        {
            _reactionError = "BotController profile API unavailable: " + ex.Message;
            Logger.LogError("[Reaction] {Error}. Notice falls back to +100ms until profiles can be read.", _reactionError);
        }
    }

    private void RefreshReactionBots()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || !player.IsBot || !player.PawnIsAlive || player.HasBeenControlledByPlayerThisRound) continue;
            var bot = player.PlayerPawn.Value?.Bot;
            if (bot == null || bot.Handle == 0) continue;
            int extraMs = 100;
            float reaction = -1;
            if (_reactionApi != null && _reactionApi.GetBotProfile(player.Slot, out var profile))
            {
                extraMs = ReactionGate.ExtraMilliseconds(profile.Skill);
                reaction = profile.ReactionTime;
            }
            _reactionBots[bot.Handle] = (player.Slot, player.PlayerName, extraMs, reaction);
            if (bot.IsEnemyVisible && bot.Enemy.Value is { IsValid: true } enemy)
                _reactionGate.KeepVisible(bot.Handle, enemy.Handle, Server.CurrentTime);
        }
    }

    private HookResult OnNoticeEnemy(DynamicHook hook)
    {
        _noticeCalls++;
        bool allowed = true;
        nint bot = hook.GetParam<nint>(0);
        nint enemy = hook.GetParam<nint>(1);
        if (bot != 0 && enemy != 0)
        {
            if (!_reactionBots.TryGetValue(bot, out var profile))
            {
                RefreshReactionBots();
                if (_reactionBots.TryGetValue(bot, out var current)) profile = current;
                else
                {
                // A newly spawned bot can reach native vision before the tick cache.
                // Fail conservatively with the longer hold rather than firing instantly.
                profile = (-1, "profile pending", 100, -1);
                }
            }
            var decision = _reactionGate.Evaluate(bot, enemy, Server.CurrentTime, profile.ExtraMs);
            allowed = decision.Allowed;
            if (!allowed) _heldCalls++;
            if (decision.Released)
            {
                _releasedEncounters++;
                _lastWaitMs = decision.WaitedSeconds * 1000;
                _lastBot = profile.Name;
                if (_releasedEncounters <= 12)
                    Logger.LogInformation("[Reaction] {Bot}: configured extra={Extra}ms; observed notice hold={Actual:F1}ms; profile ReactionTime={Base:F4}s", profile.Name, profile.ExtraMs, _lastWaitMs, profile.ProfileReaction);
            }
        }
        // Same always-notice behavior as upstream after the additional wait.
        // Returning false during the wait prevents enemy acquisition at its source,
        // before aim and attack can consume a newly noticed target.
        hook.SetReturn(allowed);
        return HookResult.Handled;
    }

    private void ResetReactionState()
    {
        _reactionGate.Clear();
        _reactionBots.Clear();
        _noticeCalls = _heldCalls = _releasedEncounters = 0;
        _lastWaitMs = 0;
        _lastBot = "none";
    }

    private void PrintReactionStatus(CCSPlayerController? player, CommandInfo command)
    {
        command.ReplyToCommand($"[Reaction] r3-all-difficulties; hook={(_noticeFunction != null ? "active" : "FAILED")}; difficulty=all; profileAPI={(_reactionApi != null ? "ready" : "pending/fallback +100ms")}; calls={_noticeCalls}; held={_heldCalls}; released={_releasedEncounters}; last={_lastBot} {_lastWaitMs:F1}ms; Pro=+100ms Rank=+40ms");
        foreach (var bot in _reactionBots.Values)
            command.ReplyToCommand($"[Reaction] slot={bot.Slot} {bot.Name}: extra={bot.ExtraMs}ms; loaded profile ReactionTime={bot.ProfileReaction:F4}s");
        if (_reactionError.Length > 0) command.ReplyToCommand("[Reaction] " + _reactionError);
    }

    private void UninstallReactionGate()
    {
        _noticeFunction?.Unhook(OnNoticeEnemy, HookMode.Pre);
        _noticeFunction = null;
        ResetReactionState();
    }
}
