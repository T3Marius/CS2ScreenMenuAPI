using CounterStrikeSharp.API.Core;

namespace CS2ScreenMenuAPI
{
    public class MenuOption : IMenuOption
    {
        public string Text { get; set; } = string.Empty;
        public bool IsDisabled { get; set; } = false;
        public Menu? SubMenu { get; set; }
        public Action<CCSPlayerController, IMenuOption> Callback { get; set; } = (_, _) => { };
    }
}