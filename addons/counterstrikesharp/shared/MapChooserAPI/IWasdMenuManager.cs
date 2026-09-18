using CounterStrikeSharp.API.Core;
namespace MapChooserAPI;

public interface IWasdMenuManager
{
    //create new menu object
    public IWasdMenu CreateMenu(string title = "", bool freezePlayer = true, bool displayOptionsCount = false);
    //open the menu as main menu
    public void OpenMainMenu(CCSPlayerController? player, IWasdMenu? menu);
    //close all menus
    public void CloseMenu(CCSPlayerController? player);
    //close current submenu and go to previous menu/submenu
    public void CloseSubMenu(CCSPlayerController? player);
    //close all submenus and go to main menu
    public void CloseAllSubMenus(CCSPlayerController? player);
    //open menu as submenu
    public void OpenSubMenu(CCSPlayerController? player, IWasdMenu? menu);
    public void CloseActiveMenu(IWasdMenu? menu);
    public void CloseActiveMenu(CCSPlayerController? player);
    public BaseMenu MenuByType(string menuType, string title, BasePlugin plugin);
    public BaseMenu MenuByType(Type menuType, string title, BasePlugin plugin);
}