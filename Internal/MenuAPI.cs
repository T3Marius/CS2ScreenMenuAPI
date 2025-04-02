using CounterStrikeSharp.API.Core;

namespace CS2ScreenMenuAPI
{
    public static class MenuAPI
    {
        private static readonly Dictionary<CCSPlayerController, Menu> _activeMenus = new();

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
                menu.Dispose();
            }
            _activeMenus.Clear();
        }
        public static void SetActiveMenu(CCSPlayerController player, Menu? menu)
        {
            if (menu == null)
            {
                if (_activeMenus.ContainsKey(player))
                {
                    _activeMenus[player].Dispose();
                    _activeMenus.Remove(player);
                }
            }
            else
            {
                if (_activeMenus.TryGetValue(player, out var activeMenu))
                {
                    activeMenu.Dispose();
                }
                _activeMenus[player] = menu;
            }
        }
    }
}