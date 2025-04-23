using CounterStrikeSharp.API.Core;
using System.Reflection;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Entities;

namespace CS2ScreenMenuAPI
{
    public static class PlayerRes
    {
        public class Resolution
        {
            public float PositionX { get; set; }
            public float PositionY { get; set; }

            public Resolution()
            {
                PositionX = 0;
                PositionY = 0;
            }
            public Resolution(float posX, float posY)
            {
                PositionX = posX;
                PositionY = posY;
            }
        }
        public static void CreateResolutionMenu(CCSPlayerController player, BasePlugin plugin)
        {
            var config = ConfigLoader.Load();

            CounterStrikeSharp.API.Modules.Menu.CenterHtmlMenu resolutionMenu = new CounterStrikeSharp.API.Modules.Menu.CenterHtmlMenu($"{player.Localizer("SelectRes")}", plugin);
            resolutionMenu.ExitButton = false;

            foreach (KeyValuePair<string, Resolution> resolution in config.Settings.Resolutions)
            {
                string resName = resolution.Key;
                Resolution resValue = resolution.Value;

                resolutionMenu.AddMenuOption(resName, (p, o) =>
                {
                    ResolutionDatabase.SetPlayerResolution(p, resValue);
                    CounterStrikeSharp.API.Modules.Menu.MenuManager.CloseActiveMenu(player);
                });
            }

            resolutionMenu.AddMenuOption("<font color='red'>Close</font>", (p, option) =>
            {
                CounterStrikeSharp.API.Modules.Menu.MenuManager.CloseActiveMenu(player);
            });

            CounterStrikeSharp.API.Modules.Menu.MenuManager.OpenCenterHtmlMenu(plugin, player, resolutionMenu);
        }
        public static void CreateResolutionMenu(CCSPlayerController player, BasePlugin plugin, Action afterSelectionCallback)
        {
            var config = ConfigLoader.Load();

            CounterStrikeSharp.API.Modules.Menu.CenterHtmlMenu resolutionMenu = new CounterStrikeSharp.API.Modules.Menu.CenterHtmlMenu($"{player.Localizer("SelectRes")}", plugin);
            resolutionMenu.ExitButton = false;

            foreach (KeyValuePair<string, Resolution> resolution in config.Settings.Resolutions)
            {
                string resName = resolution.Key;
                Resolution resValue = resolution.Value;

                resolutionMenu.AddMenuOption(resName, (p, o) =>
                {
                    ResolutionDatabase.SetPlayerResolution(p, resValue);
                    CounterStrikeSharp.API.Modules.Menu.MenuManager.CloseActiveMenu(player);
                    afterSelectionCallback.Invoke();
                });
            }

            resolutionMenu.AddMenuOption("<font color='red'>Close</font>", (p, option) =>
            {
                CounterStrikeSharp.API.Modules.Menu.MenuManager.CloseActiveMenu(player);
                afterSelectionCallback.Invoke();
            });

            CounterStrikeSharp.API.Modules.Menu.MenuManager.OpenCenterHtmlMenu(plugin, player, resolutionMenu);
        }
    }
}