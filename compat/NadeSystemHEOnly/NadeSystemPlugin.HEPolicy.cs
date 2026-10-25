using System;
using CounterStrikeSharp.API.Modules.Cvars;

namespace NadeSystem;

internal static class GrenadePolicy
{
    internal static bool Blocks(string? type) =>
        string.Equals(type, "he", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "hegrenade", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "weapon_hegrenade", StringComparison.OrdinalIgnoreCase);
}

public partial class NadeSystemPlugin
{
    private ConVar? _nativeAllowGrenades;
    private static bool IsBlockedHE(string? type) => GrenadePolicy.Blocks(type);

    // The upstream package uses plugin-generated grenades and disables native
    // throws. Keep that split so native AI cannot bypass the HE-only filter.
    private void EnforceNativeGrenadesOff()
    {
        _nativeAllowGrenades ??= ConVar.Find("bot_allow_grenades");
        if (_nativeAllowGrenades != null && _nativeAllowGrenades.GetPrimitiveValue<bool>())
            _nativeAllowGrenades.SetValue(false);
    }
}
