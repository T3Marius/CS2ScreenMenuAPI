using CounterStrikeSharp.API.Core;
using System.Reflection;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Entities;

namespace CS2ScreenMenuAPI
{
    public static class PlayerRes
    {
        private static readonly Dictionary<ulong, Resolution> Resolutions = [];
        private static Config? _config = null;
        private static readonly string ResolutionsFilePath;
        private static bool _initialized = false;
        private static Dictionary<ulong, ResolutionData> _savedResolutions = [];

        static PlayerRes()
        {
            string assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;

            ResolutionsFilePath = Path.Combine(
                Server.GameDirectory,
                "csgo",
                "addons",
                "counterstrikesharp",
                "shared",
                assemblyName,
                "player_resolutions.json"
            );

            LoadResolutions();
        }

        private static void LoadResolutions()
        {
            try
            {
                if (File.Exists(ResolutionsFilePath))
                {
                    string jsonContent = File.ReadAllText(ResolutionsFilePath);
                    _savedResolutions = JsonSerializer.Deserialize<Dictionary<ulong, ResolutionData>>(jsonContent)
                        ?? new Dictionary<ulong, ResolutionData>();
                }
            }
            catch (Exception)
            {
                _savedResolutions = new Dictionary<ulong, ResolutionData>();
            }
        }

        private static void SaveResolutions()
        {
            try
            {
                string directory = Path.GetDirectoryName(ResolutionsFilePath)!;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string jsonContent = JsonSerializer.Serialize(_savedResolutions, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(ResolutionsFilePath, jsonContent);
            }
            catch (Exception)
            {
            }
        }

        private static Config GetConfig()
        {
            return _config ??= ConfigLoader.Load();
        }

        public static bool HasPlayerResolution(CCSPlayerController player)
        {
            if (player == null || player.SteamID == 0)
                return false;

            return Resolutions.ContainsKey(player.SteamID) || _savedResolutions.ContainsKey(player.SteamID);
        }
        private class ResolutionData
        {
            public float PositionX { get; set; }
            public float PositionY { get; set; }
        }

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

        public static Resolution GetDefaultResolution()
        {
            var config = GetConfig();
            return new Resolution(config.Settings.PositionX, config.Settings.PositionY);
        }

        public static Resolution GetPlayerResolution(CCSPlayerController player)
        {
            if (player == null || player.SteamID == 0)
                return GetDefaultResolution();

            if (Resolutions.TryGetValue(player.SteamID, out Resolution? resolution) && resolution != null)
                return resolution;

            if (_savedResolutions.TryGetValue(player.SteamID, out ResolutionData? savedResolution))
            {
                resolution = new Resolution(savedResolution.PositionX, savedResolution.PositionY);
                Resolutions[player.SteamID] = resolution;
                return resolution;
            }

            return GetDefaultResolution();
        }

        public static void SetPlayerResolution(CCSPlayerController player, Resolution resolution)
        {
            if (player == null || player.SteamID == 0 || resolution == null)
                return;

            Resolutions[player.SteamID] = resolution;

            _savedResolutions[player.SteamID] = new ResolutionData
            {
                PositionX = resolution.PositionX,
                PositionY = resolution.PositionY
            };

            Task.Run(SaveResolutions);
        }
        public static void CreateResolutionMenu(CCSPlayerController player, BasePlugin plugin)
        {
            var config = GetConfig();

            CounterStrikeSharp.API.Modules.Menu.CenterHtmlMenu resolutionMenu = new CounterStrikeSharp.API.Modules.Menu.CenterHtmlMenu($"{player.Localizer("SelectRes")}", plugin);
            resolutionMenu.ExitButton = false;

            foreach (KeyValuePair<string, Resolution> resolution in config.Settings.Resolutions)
            {
                string resName = resolution.Key;
                Resolution resValue = resolution.Value;

                resolutionMenu.AddMenuOption(resName, (p, o) =>
                {
                    SetPlayerResolution(p, resValue);
                    CounterStrikeSharp.API.Modules.Menu.MenuManager.CloseActiveMenu(player);
                });
            }
            CounterStrikeSharp.API.Modules.Menu.MenuManager.OpenCenterHtmlMenu(plugin, player, resolutionMenu);
        }
        public static void CreateResolutionMenu(CCSPlayerController player, BasePlugin plugin, Action afterSelectionCallback)
        {
            var config = GetConfig();

            CounterStrikeSharp.API.Modules.Menu.CenterHtmlMenu resolutionMenu = new CounterStrikeSharp.API.Modules.Menu.CenterHtmlMenu($"{player.Localizer("SelectRes")}", plugin);
            resolutionMenu.ExitButton = false;
            foreach (KeyValuePair<string, Resolution> resolution in config.Settings.Resolutions)
            {
                string resName = resolution.Key;
                Resolution resValue = resolution.Value;

                resolutionMenu.AddMenuOption(resName, (p, o) =>
                {
                    SetPlayerResolution(p, resValue);
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
        public static void ClearCache(ulong steamID)
        {
            if (Resolutions.ContainsKey(steamID))
                Resolutions.Remove(steamID);

            if (_savedResolutions.ContainsKey(steamID))
            {
                _savedResolutions.Remove(steamID);
                Task.Run(SaveResolutions);
            }
        }
    }
}