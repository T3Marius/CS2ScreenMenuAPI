using CounterStrikeSharp.API.Core;

namespace CS2ScreenMenuAPI
{
    public interface IMenuOption
    {
        string Text { get; set;  }
        bool IsDisabled { get; set; }
        Action<CCSPlayerController, IMenuOption> Callback { get; set; }
    }

}
