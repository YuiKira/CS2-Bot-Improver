using System;
using System.Collections.Generic;
using CounterStrikeSharp.API.Core;

namespace MapChooserAPI 
{
    // Minimal surface to match the other plugin's expectations.
    public interface BaseMenu : IMenu
    {
/*        string Title { get; set; }
        List<ItemOption> ItemOptions { get; }
        bool ExitButton { get; set; }
        int MenuTime { get; set; }
        IMenu? PrevMenu { get; set; }
        BasePlugin Plugin { get; }

        ItemOption AddItem(string display, Action<CCSPlayerController, ItemOption> onSelect, DisableOption disableOption = DisableOption.None);
        ItemOption AddItem(string display, DisableOption disableOption);
        void Display(CCSPlayerController player, int time);
        void DisplayAt(CCSPlayerController player, int firstItem, int time);
        void DisplayToAll(int time);
        void DisplayAtToAll(int firstItem, int time); */
    }
    public interface IMenu
    {
        string Title { get; set; }
        List<ItemOption> ItemOptions { get; }
        bool ExitButton { get; set; }
        int MenuTime { get; set; }
        IMenu? PrevMenu { get; set; }
        BasePlugin Plugin { get; }
        ItemOption AddItem(string display, Action<CCSPlayerController, ItemOption> onSelect, DisableOption disableOption = DisableOption.None, PostSelectAction postSelectAction = PostSelectAction.Nothing);
        ItemOption AddItem(string display, DisableOption disableOption = DisableOption.None, PostSelectAction postSelectAction = PostSelectAction.Nothing);
        void Display(CCSPlayerController player, int time);
        void DisplayAt(CCSPlayerController player, int firstItem, int time);
        void DisplayToAll(int time);
        void DisplayAtToAll(int firstItem, int time);
    }

    public enum PostSelectAction
    {
        Close = 0,
        Reset = 1,
        Nothing = 2
    }

    public interface ItemOption
    {
        public string Text { get; set; }
        public DisableOption DisableOption { get; set; }
        public PostSelectAction PostSelectAction { get; set; }
        public Action<CCSPlayerController, ItemOption>? OnSelect { get; set; }
    }
}
