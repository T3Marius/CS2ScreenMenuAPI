using CounterStrikeSharp.API.Core;
using CS2ScreenMenuAPI.Interfaces;

namespace CS2ScreenMenuAPI.Internal
{
    internal class MenuOption : IMenuOption
    {
        public string Text { get; set; }
        public bool Disabled { get; set; }
        public Action<CCSPlayerController, IMenuOption> OnSelect { get; set; }
        public ScreenMenu? SubMenu { get; set; }

        public MenuOption(string text, Action<CCSPlayerController, IMenuOption> onSelect, bool disabled = false)
        {
            Text = text;
            OnSelect = onSelect;
            Disabled = disabled;
            SubMenu = null;
        }
    }
}