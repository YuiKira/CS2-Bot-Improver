using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;

namespace NadeSystem;

// Intentionally replaces the original NadeSystem assembly at the same path.
// No original grenade events, timers, replay code or projectile spawning run.
public sealed class NadeSystemDisabled : BasePlugin
{
    public override string ModuleName => "NadeSystem (Disabled)";
    public override string ModuleVersion => "1.2.3-disabled.1";
    public override string ModuleAuthor => "YuiKira";

    private ConVar? _allowGrenades;

    public override void Load(bool hotReload)
    {
        _allowGrenades = ConVar.Find("bot_allow_grenades");
        EnforceNativeGrenadesOff();
        RegisterListener<Listeners.OnTick>(EnforceNativeGrenadesOff);
        AddCommand("bot_nades", "Bot grenade throwing is disabled in this build.", (_, _) =>
        {
            EnforceNativeGrenadesOff();
            Server.PrintToConsole("[NadeSystem] bot_nades = off (disabled in this build)");
        });
        Server.PrintToConsole("[NadeSystem] DISABLED: scripted and native bot grenade throwing are off.");
    }

    private void EnforceNativeGrenadesOff()
    {
        _allowGrenades ??= ConVar.Find("bot_allow_grenades");
        if (_allowGrenades != null && _allowGrenades.GetPrimitiveValue<bool>())
            _allowGrenades.SetValue(false);
    }
}
