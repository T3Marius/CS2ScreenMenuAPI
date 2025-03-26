using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CS2ScreenMenuAPI.Extensions;
using CS2ScreenMenuAPI.Enums;

namespace CS2ScreenMenuAPI.Config
{
    public class MenuConfig
    {
        private const string CONFIG_FILE = "config.jsonc";
        private string _configPath = string.Empty;
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
        };

        public Buttons_Config Buttons { get; set; } = new Buttons_Config();
        public Menu_Translations Translations { get; set; } = new Menu_Translations();
        public Default_Settings DefaultSettings { get; set; } = new Default_Settings();
        public Sounds_Config Sounds { get; set; } = new Sounds_Config();
        public Resolution_Settings Resolution { get; set; } = new Resolution_Settings();
        public MenuConfig() { }

        public void Initialize()
        {
            _configPath = Path.Combine(
                Server.GameDirectory,
                "csgo",
                "addons",
                "counterstrikesharp",
                "shared",
                "CS2ScreenMenuAPI",
                CONFIG_FILE);

            string directory = Path.GetDirectoryName(_configPath)!;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            LoadConfig();
        }

        private void LoadConfig()
        {
            if (!File.Exists(_configPath))
            {
                CreateDefaultConfig();
                return;
            }

            try
            {
                MergeConfigFileWithDefaults();

                var jsonContent = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<MenuConfig>(jsonContent, _jsonOptions);
                if (config != null)
                {
                    Buttons = config.Buttons;
                    Translations = config.Translations;
                    DefaultSettings = config.DefaultSettings;
                    Sounds = config.Sounds;
                    Resolution = config.Resolution;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading menu configuration: {ex.Message}");
                CreateDefaultConfig();
            }
        }

        private void CreateDefaultConfig()
        {
            var configContent = @"{
    /* 
        Menu configuration file.
        Adjust your button settings and display texts as needed.
    */
    ""Buttons"": {
        ""ScrollUpButton"": ""W"",
        ""ScrollDownButton"": ""S"",
        ""SelectButton"": ""E""
    },
    ""DefaultSettings"": {
        ""MenuType"": ""Both"",
        ""TextColor"": ""Orange"",
        ""PositionX"": -5.5,
        ""PositionY"": 0,
        ""Background"": true,
        ""BackgroundHeight"": 0,
        ""BackgroundWidth"": 0.2,
        ""Font"": ""Arial Bold"",
        ""Size"": 32,
        ""AddResolutionOption"": true,
        ""Spacing"": true,
        ""FreezePlayer"": true,
        ""EnableOptionsCount"": true,
        ""EnableDisabledOptionsCount"": true
    },
    ""Translations"": {
        ""NextButton"": ""Next"",
        ""BackButton"": ""Back"",
        ""ExitButton"": ""Exit"",
        ""ScrollInfo"": ""[W/S] Scroll"",
        ""SelectInfo"": ""[E] Select"",
        ""SelectPrefix"": ""‣ "",
        ""ResolutionOption"": ""Change Resolution"",
        ""MenuResolutionTitle"": ""Resolution Setup"",
        ""ResolutionSet"": ""Menu resolution set to: {res}""
    },
    ""Sounds"": {
        ""MenuSoundsVolume"": 0.7,
        ""SoundEventFile"": ""soundevents/menu_sounds.vsndevts"",
        ""Select"": ""menu.Select"",
        ""Next"": ""menu.Select"",
        ""Back"": ""menu.Select"",
        ""Exit"": ""menu.Close"",
        ""ScrollUp"": ""UI.ButtonRolloverLarge"",
        ""ScrollDown"": ""UI.ButtonRolloverLarge""
    },
    ""Resolutions"": {
        ""MenuResoltions"": {
            ""1920x1080"": { ""PositionX"": -9.0, ""PositionY"": 0 },
            ""1680x1050"": { ""PositionX"": -8.2, ""PositionY"": 0 },
            ""1600x900"": { ""PositionX"": -9.0, ""PositionY"": 0 },
            ""1440x1080"": { ""PositionX"": -6.8, ""PositionY"": 0 },
            ""1280x1080"": { ""PositionX"": -6.0, ""PositionY"": 0 },
            ""1280x720"": { ""PositionX"": -9.0, ""PositionY"": 0 },
            ""1280x1024"": { ""PositionX"": -6.3, ""PositionY"": 0 },
            ""1024x768"": { ""PositionX"": -6.8, ""PositionY"": 0 },
            ""800x600"": { ""PositionX"": -6.8, ""PositionY"": 0 }
        }
    }
    /* 
        Buttons mapping:
        
        Alt1       - Alt1
        Alt2       - Alt2
        Attack     - Attack
        Attack2    - Attack2
        Attack3    - Attack3
        Bullrush   - Bullrush
        Cancel     - Cancel
        Duck       - Duck
        Grenade1   - Grenade1
        Grenade2   - Grenade2
        Space      - Jump
        Left       - Left
        W          - Forward
        A          - Moveleft
        S          - Back
        D          - Moveright
        E          - Use
        R          - Reload
        F          - (Custom) 0x800000000
        Shift      - Speed
        Right      - Right
        Run        - Run
        Walk       - Walk
        Weapon1    - Weapon1
        Weapon2   - Weapon2
        Zoom       - Zoom
        Tab        - (Custom) 8589934592
    */
}";
            try
            {
                string directory = Path.GetDirectoryName(_configPath)!;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_configPath, configContent);
                LoadConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating default configuration: {ex.Message}");
            }
        }

        public void SaveConfig()
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(this, _jsonOptions);
                File.WriteAllText(_configPath, jsonContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving menu configuration: {ex.Message}");
            }
        }

        private void MergeConfigFileWithDefaults()
        {
            string defaultConfigContent = @"{
    /* 
        Menu configuration file.
        Adjust your button settings and display texts as needed.
    */
    ""Buttons"": {
        ""ScrollUpButton"": ""W"",
        ""ScrollDownButton"": ""S"",
        ""SelectButton"": ""E""
    },
    ""DefaultSettings"": {
        ""MenuType"": ""Both"",
        ""TextColor"": ""Orange"",
        ""PositionX"": -5.5,
        ""PositionY"": 0,
        ""Background"": true,
        ""BackgroundHeight"": 0,
        ""BackgroundWidth"": 0.2,
        ""Font"": ""Arial Bold"",
        ""Size"": 32,
        ""AddResolutionOption"": true,
        ""Spacing"": true,
        ""FreezePlayer"": true,
        ""EnableOptionsCount"": true,
        ""EnableDisabledOptionsCount"": true
    },
    ""Translations"": {
        ""NextButton"": ""Next"",
        ""BackButton"": ""Back"",
        ""ExitButton"": ""Exit"",
        ""ScrollInfo"": ""[W/S] Scroll"",
        ""SelectInfo"": ""[E] Select"",
        ""SelectPrefix"": ""‣ "",
        ""ResolutionOption"": ""Change Resolution"",
        ""MenuResolutionTitle"": ""Resolution Setup"",
        ""ResolutionSet"": ""Menu resolution set to: {res}""
    },
    ""Sounds"": {
        ""MenuSoundsVolume"": 0.7,
        ""SoundEventFile"": ""soundevents/menu_sounds.vsndevts"",
        ""Select"": ""menu.Select"",
        ""Next"": ""menu.Select"",
        ""Back"": ""menu.Select"",
        ""Exit"": ""menu.Close"",
        ""ScrollUp"": ""UI.ButtonRolloverLarge"",
        ""ScrollDown"": ""UI.ButtonRolloverLarge""
    },
    ""Resolutions"": {
        ""MenuResoltions"": {
            ""1920x1080"": { ""PositionX"": -9.0, ""PositionY"": 0 },
            ""1680x1050"": { ""PositionX"": -8.2, ""PositionY"": 0 },
            ""1600x900"": { ""PositionX"": -9.0, ""PositionY"": 0 },
            ""1440x1080"": { ""PositionX"": -6.8, ""PositionY"": 0 },
            ""1280x1080"": { ""PositionX"": -6.0, ""PositionY"": 0 },
            ""1280x720"": { ""PositionX"": -9.0, ""PositionY"": 0 },
            ""1280x1024"": { ""PositionX"": -6.3, ""PositionY"": 0 },
            ""1024x768"": { ""PositionX"": -6.8, ""PositionY"": 0 },
            ""800x600"": { ""PositionX"": -6.8, ""PositionY"": 0 }
        }
    }
    /* 
    Buttons mapping:
        
        Alt1       - Alt1
        Alt2       - Alt2
        Attack     - Attack
        Attack2    - Attack2
        Attack3    - Attack3
        Bullrush   - Bullrush
        Cancel     - Cancel
        Duck       - Duck
        Grenade1   - Grenade1
        Grenade2   - Grenade2
        Space      - Jump
        Left       - Left
        W          - Forward
        A          - Moveleft
        S          - Back
        D          - Moveright
        E          - Use
        R          - Reload
        F          - (Custom) 0x800000000
        Shift      - Speed
        Right      - Right
        Run        - Run
        Walk       - Walk
        Weapon1    - Weapon1
        Weapon2   - Weapon2
        Zoom       - Zoom
        Tab        - (Custom) 8589934592
    */
}";
            try
            {
                string cleanedDefault = RemoveJsonComments(defaultConfigContent);
                string userConfigContent = File.ReadAllText(_configPath);
                string cleanedUserConfig = RemoveJsonComments(userConfigContent);

                var defaultJson = JsonNode.Parse(cleanedDefault) as JsonObject;
                var userJson = JsonNode.Parse(cleanedUserConfig) as JsonObject;

                if (defaultJson == null || userJson == null)
                    return;

                MergeJsonObjects(userJson, defaultJson);

                File.WriteAllText(_configPath, userJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error merging configuration: {ex.Message}");
            }
        }

        private string RemoveJsonComments(string json)
        {
            return Regex.Replace(json, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        }

        private void MergeJsonObjects(JsonObject target, JsonObject defaults)
        {
            foreach (var kvp in defaults)
            {
                if (!target.ContainsKey(kvp.Key))
                {
                    target[kvp.Key] = CloneNode(kvp.Value);
                }
                else if (kvp.Value is JsonObject defaultChild && target[kvp.Key] is JsonObject targetChild)
                {
                    MergeJsonObjects(targetChild, defaultChild);
                }
            }
        }

        private JsonNode CloneNode(JsonNode? node)
        {
            var json = node?.ToJsonString()!;
            return JsonNode.Parse(json)!;
        }
    }

    public class Buttons_Config
    {
        public string ScrollUpButton { get; set; } = "W";
        public string ScrollDownButton { get; set; } = "S";
        public string SelectButton { get; set; } = "E";
    }

    public class Sounds_Config
    {
        public float MenuSoundsVolume { get; set; } = 0.7f;
        public string SoundEventFile { get; set; } = "soundevents/menu_sounds.vsndevts";
        public string Select { get; set; } = "menu.Select";
        public string Next { get; set; } = "menu.Select";
        public string Back { get; set; } = "menu.Select";
        public string Exit { get; set; } = "menu.Close";

        public string ScrollUp { get; set; } = "UI.ButtonRolloverLarge";
        public string ScrollDown { get; set; } = "UI.ButtonRolloverLarge";
    }

    public class Menu_Translations
    {
        public string NextButton { get; set; } = "Next";
        public string BackButton { get; set; } = "Back";
        public string ExitButton { get; set; } = "Exit";
        public string ScrollInfo { get; set; } = "[W/S] Scroll";
        public string SelectInfo { get; set; } = "[E] Select";
        public string SelectPrefix { get; set; } = "‣ ";
        public string ResolutionOption { get; set; } = "Change Resolution";
        public string MenuResolutionTitle { get; set; } = "Resolution Setup";
        public string ResolutionSet { get; set; } = "Menu resolution set to: {res}";
    }

    public class Default_Settings
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MenuType MenuType { get; set; } = MenuType.Both;

        [JsonConverter(typeof(ColorJsonConverter))]
        public Color TextColor { get; set; } = Color.Orange;
        public float PositionX { get; set; } = -5.5f;
        public float PositionY { get; set; } = 0f;
        public bool Background { get; set; } = true;
        public float BackgroundHeight { get; set; } = 0f;
        public float BackgroundWidth { get; set; } = 0.2f;
        public string Font { get; set; } = "Arial Bold";
        public float Size { get; set; } = 32;
        public bool AddResolutionOption { get; set; } = true;
        public bool Spacing { get; set; } = true;
        public bool FreezePlayer { get; set; } = true;
        public bool EnableOptionsCount { get; set; } = true;
        public bool EnableDisabledOptionsCount { get; set; } = true;
    }

    public class Resolution_Settings
    {
        public Dictionary<string, MenuResolution> MenuResoltions { get; set; } = new Dictionary<string, MenuResolution>
        {
            { "1920x1080", new MenuResolution { PositionX = -9.0f, PositionY = 0f } },
            { "1680x1050", new MenuResolution { PositionX = -8.2f, PositionY = 0f } },
            { "1600x900", new MenuResolution { PositionX = -9.0f, PositionY = 0f } },
            { "1440x1080", new MenuResolution { PositionX = -6.8f, PositionY = 0f } },
            { "1280x1080", new MenuResolution { PositionX = -6.0f, PositionY = 0f } },
            { "1280x720", new MenuResolution { PositionX = -9.0f, PositionY = 0f } },
            { "1280x1024", new MenuResolution { PositionX = -6.3f, PositionY = 0f } },
            { "1024x768", new MenuResolution { PositionX = -6.8f, PositionY = 0f } },
            { "800x600", new MenuResolution { PositionX = -6.8f, PositionY = 0f } },
        };
    }

    public class MenuResolution
    {
        public float PositionX { get; set; }
        public float PositionY { get; set; }
    }
}
