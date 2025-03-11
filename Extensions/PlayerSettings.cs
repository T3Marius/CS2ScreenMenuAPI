using Newtonsoft.Json;

namespace CS2ScreenMenuAPI.Extensions.PlayerSettings
{
    public static class PlayerSettings
    {
        private static string? cookiePath;
        private static Dictionary<string, PlayerSettings_Config> playerSettings { get; set; } = new Dictionary<string, PlayerSettings_Config>();
        private static bool isInitialized = false;

        public static void Initialize(string basePath)
        {
            if (isInitialized)
                return;

            cookiePath = Path.Combine(basePath, "players_settings.json");
            isInitialized = true;
            LoadSettings();
        }
        private static void LoadSettings()
        {
            if (string.IsNullOrEmpty(cookiePath))
                throw new InvalidOperationException("Error tryin' to Load the settings!");

            if (File.Exists(cookiePath))
            {
                string json = File.ReadAllText(cookiePath);
                playerSettings = JsonConvert.DeserializeObject<Dictionary<string, PlayerSettings_Config>>(json)
                    ?? new Dictionary<string, PlayerSettings_Config>();
            }
            else
            {
                SaveSettings();
            }
        }
        private static void SaveSettings()
        {
            if (string.IsNullOrEmpty(cookiePath))
                throw new InvalidOperationException("Failed to SaveSettings!");

            string json = JsonConvert.SerializeObject(playerSettings, Formatting.Indented);
            File.WriteAllText(cookiePath, json);
        }
        public static PlayerSettings_Config GetPlayerSettings(string steamId)
        {
            if (!isInitialized)
                throw new InvalidOperationException("Failed to get player settings!");

            if (!playerSettings.TryGetValue(steamId, out var settings))
            {
                settings = new PlayerSettings_Config
                {
                    SteamID = steamId,
                    Resolution = string.Empty
                };
            }
            return settings;
        }
        public static void SetSettings(string steamId, PlayerSettings_Config settings)
        {
            if (!isInitialized)
                throw new InvalidOperationException("Failed to SetSettings!");

            playerSettings[steamId] = settings;
            SaveSettings();
        }
    }
    public class PlayerSettings_Config
    {
        public string SteamID { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
    }
}