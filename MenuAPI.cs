using CounterStrikeSharp.API.Core;
using CS2ScreenMenuAPI.Interfaces;
using CS2ScreenMenuAPI.Internal;
using CS2ScreenMenuAPI.Enums;
using CS2ScreenMenuAPI.Extensions;
using CounterStrikeSharp.API;

namespace CS2ScreenMenuAPI
{
    public static class MenuAPI
    {
        private static readonly Dictionary<IntPtr, IMenuInstance> ActiveMenus = [];
        public static void OpenMenu(BasePlugin plugin, CCSPlayerController player, ScreenMenu menu)
        {
            if (!CCSPlayer.IsValidPlayer(player))
                throw new ArgumentException("Player is null or invalid", nameof(player));

            CloseActiveMenu(player);

            WorldTextManager.Create(player, "       ", drawBackground: false); // fix the bug where first menu open didn't create the entity
            Server.NextFrame(() =>
            {
                ActiveMenus[player.Handle] = new ScreenMenuInstance(plugin, player, menu);
                ActiveMenus[player.Handle].Display();

                if (menu.MenuType == MenuType.Scrollable || menu.MenuType == MenuType.Both)
                {
                    if (menu.FreezePlayer)
                    {
                        player.Freeze();
                    }
                }
            });
        }

        public static void OpenSubMenu(BasePlugin plugin, CCSPlayerController player, ScreenMenu menu)
        {
            if (!CCSPlayer.IsValidPlayer(player))
                throw new ArgumentException("Player is null or invalid", nameof(player));

            if (ActiveMenus.TryGetValue(player.Handle, out var activeMenu))
            {
                if (activeMenu is ScreenMenuInstance screenMenuInstance)
                {
                    screenMenuInstance.SmoothTransitionToMenu(menu);
                    return;
                }
                activeMenu.Close();
            }

            ActiveMenus[player.Handle] = new ScreenMenuInstance(plugin, player, menu);
            ActiveMenus[player.Handle].Display();

            if (menu.MenuType == MenuType.Scrollable || menu.MenuType == MenuType.Both)
            {
                if (menu.FreezePlayer)
                {
                    player.Freeze();
                }
            }
        }

        public static void CloseActiveMenu(CCSPlayerController player)
        {
            if (!CCSPlayer.IsValidPlayer(player)) return;

            if (ActiveMenus.TryGetValue(player.Handle, out var menu))
            {
                menu.Close();
                ActiveMenus.Remove(player.Handle);
            }
        }

        public static void RemoveActiveMenu(CCSPlayerController player)
        {
            if (!CCSPlayer.IsValidPlayer(player))
                return;

            ActiveMenus.Remove(player.Handle);
        }
        public static void ClearAllActiveMenus()
        {
            foreach (var menu in ActiveMenus.Values)
            {
                menu?.Close();
            }
            ActiveMenus.Clear();
        }
        public static void UpdateActiveMenu(CCSPlayerController player, IMenuInstance menu)
        {
            if (!CCSPlayer.IsValidPlayer(player))
                return;

            ActiveMenus[player.Handle] = menu;
        }
        public static IMenuInstance? GetActiveMenu(CCSPlayerController player)
        {
            return CCSPlayer.IsValidPlayer(player) && ActiveMenus.TryGetValue(player.Handle, out var menu) ? menu : null;
        }

    }
}