using CounterStrikeSharp.API.Core;
using CS2ScreenMenuAPI.Internal;

namespace CS2ScreenMenuAPI
{
    public interface IMenuOption
    {
        string Text { get; set; }
        bool Disabled { get; set; }
        Action<CCSPlayerController, IMenuOption> OnSelect { get; set; }
    }
}