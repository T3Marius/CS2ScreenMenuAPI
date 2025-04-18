﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace CS2ScreenMenuAPI
{
    public interface IMenu
    {
        public void NextPage();
        public void PrevPage();
        public void Close(CCSPlayerController player);
        public void OnKeyPress(CCSPlayerController player, int key);
        void Refresh();
        void Display();
    }
    public enum PostSelect
    {
        Nothing,
        Close,
        Reset
    }
    public enum MenuType
    {
        KeyPress,
        Scrollable,
        Both
    }
}