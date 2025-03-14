using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CS2ScreenMenuAPI.Interfaces;
using CS2ScreenMenuAPI.Internal;

namespace CS2ScreenMenuAPI
{
    public interface IScreenMenuApi
    {
        void OpenMenu(BasePlugin plugin, CCSPlayerController player, ScreenMenu menu);
        void OpenSubMenu(BasePlugin plugin, CCSPlayerController player, ScreenMenu menu);
        void CloseActiveMenu(CCSPlayerController player);
        void ClearAllActiveMenus();
        IMenuInstance? GetActiveMenu(CCSPlayerController player);

        public static PluginCapability<IScreenMenuApi> Capability { get; } = new("cs2screenmenu:api");
    }
}