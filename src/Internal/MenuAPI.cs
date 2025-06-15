using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace CS2ScreenMenuAPI
{
    public static class MenuAPI
    {
        private static readonly Dictionary<CCSPlayerController, Menu> _activeMenus = new();

        public static void OpenResolutionMenu(CCSPlayerController player, BasePlugin plugin)
        {
            PlayerRes.CreateResolutionMenu(player, plugin);
        }
        public static Menu? GetActiveMenu(CCSPlayerController player)
        {
            _activeMenus.TryGetValue(player, out var menu);
            return menu;
        }
        public static void CloseActiveMenu(CCSPlayerController player)
        {
            GetActiveMenu(player)?.Close(player);
        }
        public static void CloseAllMenus()
        {
            foreach (var menu in _activeMenus.Values)
            {
                foreach (var p in Utilities.GetPlayers())
                {
                    GetActiveMenu(p)?.Close(p);
                }

            }
            _activeMenus.Clear();
        }
        public static void SetActiveMenu(CCSPlayerController player, Menu? menu)
        {
            if (menu == null)
            {
                if (_activeMenus.ContainsKey(player))
                {
                    _activeMenus[player].Close(player);
                    _activeMenus.Remove(player);
                }
            }
            else
            {
                if (_activeMenus.TryGetValue(player, out var activeMenu))
                {
                    activeMenu.Close(player);
                }
                _activeMenus[player] = menu;
            }
        }
    }
}