using CounterStrikeSharp.API.Core;
namespace MapChooserAPI;

public interface IWasdMenu
{
    public string Title { get; set; }
    public LinkedList<IWasdMenuOption>? Options { get; set; }
    // previous option node
    public LinkedListNode<IWasdMenuOption>? Prev { get; set; }
    public LinkedListNode<IWasdMenuOption> Add(string display, Action<CCSPlayerController, IWasdMenuOption> onChoice, string? key = null, DisableOption disableOption = DisableOption.None, PostSelectAction postSelectAction = PostSelectAction.Nothing);
    public LinkedListNode<IWasdMenuOption> AddItem(string display, Action<CCSPlayerController, IWasdMenuOption> onChoice, string? key = null, DisableOption disableOption = DisableOption.None, PostSelectAction postSelectAction = PostSelectAction.Nothing);
    public LinkedListNode<IWasdMenuOption> AddItem(string display, string? key = null, DisableOption disableOption = DisableOption.None, PostSelectAction postSelectAction = PostSelectAction.Nothing);
    public bool RetractVote(int? index = null, string? key = null);
}
public enum DisableOption
{
    /// <summary>
    /// No disabling option is applied.
    /// </summary>
    None,

    /// <summary>
    /// Disables the item and displays it's number.
    /// </summary>
    DisableShowNumber,

    /// <summary>
    /// Disables the item and hides it's number.
    /// </summary>
    DisableHideNumber
}