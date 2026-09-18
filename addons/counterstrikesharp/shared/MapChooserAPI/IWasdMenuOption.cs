using CounterStrikeSharp.API.Core;

namespace MapChooserAPI;

public interface IWasdMenuOption
{
    public IWasdMenu? Parent { get; set; }
    public string? OptionDisplay { get; set; }
    public Action<CCSPlayerController, IWasdMenuOption>? OnChoose { get; set; }
    public int Index { get; set; }
    public int Count { get; set; }
    public string? Key { get; set; }
    public DisableOption DisableOption { get; set; }
    public PostSelectAction PostSelectAction { get; set; }
}