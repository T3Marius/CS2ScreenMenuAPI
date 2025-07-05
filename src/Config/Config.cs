using System.Reflection;
using CounterStrikeSharp.API;
using static CS2ScreenMenuAPI.PlayerRes;
using Tomlyn;
using Tomlyn.Model;

namespace CS2ScreenMenuAPI
{
    public class Config
    {
        public Database_Config Database { get; set; } = new();
        public Menu_Settings Settings { get; set; } = new();
        public Menu_Controls Controls { get; set; } = new();
        public Sounds_Settings Sounds { get; set; } = new();
        public Dictionary<string, Dictionary<string, string>> Lang { get; set; } = new();
    }
    public class Menu_Settings
    {
        public string FontName { get; set; } = "Tahoma Bold";
        public string MenuType { get; set; } = "KeyPress";
        public int Size { get; set; } = 25;
        public float PositionX { get; set; } = 0;
        public float PositionY { get; set; } = 0;
        public bool HasExitOption { get; set; } = true;
        public bool ShowResolutionOption { get; set; } = true;
        public bool ShowPageCount { get; set; } = true;
        public bool ShowDisabledOptionNum { get; set; } = true;
        public bool FreezePlayer { get; set; } = true;
        public bool ShowControlsInfo { get; set; } = true;
        public Dictionary<string, Resolution> Resolutions { get; set; } = [];
    }
    public class Database_Config
    {
        public string Host { get; set; } = "host";
        public string Name { get; set; } = "name";
        public string User { get; set; } = "user";
        public string Password { get; set; } = "pass";
        public uint Port { get; set; } = 3306;
    }
    public class Menu_Controls
    {
        public string ScrollUp { get; set; } = "W";
        public string ScrollDown { get; set; } = "S";
        public string Select { get; set; } = "E";
        public string Exit { get; set; } = "Tab";
    }
    public class Sounds_Settings
    {
        public string Select { get; set; } = "menu.Select";
        public string Next { get; set; } = "menu.Select";
        public string Prev { get; set; } = "menu.Close";
        public string Close { get; set; } = "menu.Close";
        public string ScrollUp { get; set; } = "menu.ScrollUp";
        public string ScrollDown { get; set; } = "menu.ScrollDown";
        public float Volume { get; set; } = 1.0f;
    }
    public static class ConfigLoader
    {
        private static readonly string ConfigPath;
        private static DateTime _lastLoadTime = DateTime.MinValue;

        static ConfigLoader()
        {
            string assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;

            ConfigPath = Path.Combine(
                Server.GameDirectory,
                "csgo",
                "addons",
                "counterstrikesharp",
                "shared",
                assemblyName,
                "config.toml"
                );
        }

        public static Config Load()
        {
            if (!File.Exists(ConfigPath))
            {
                CreateDefaultConfig();
            }
            return LoadConfigFromFile();
        }

        private static Config LoadConfigFromFile()
        {
            string configText = File.ReadAllText(ConfigPath);
            TomlTable model = Toml.ToModel(configText);

            var config = new Config
            {
                Database = LoadDatabase((TomlTable)model["Database"]),
                Settings = LoadSettings((TomlTable)model["Settings"]),
                Controls = LoadControls((TomlTable)model["Controls"]),
                Sounds = LoadSounds((TomlTable)model["Sounds"]),

            };
            if (model.ContainsKey("Lang"))
            {
                TomlTable langTable = (TomlTable)model["Lang"];
                foreach (var kv in langTable)
                {
                    if (kv.Value is TomlTable innerTable)
                    {
                        Dictionary<string, string> innerDict = new Dictionary<string, string>();
                        foreach (var innerKv in innerTable)
                        {
                            innerDict[innerKv.Key] = innerKv.Value?.ToString() ?? string.Empty;
                        }
                        config.Lang[kv.Key] = innerDict;
                    }
                }
            }

            return config;
        }
        private static Database_Config LoadDatabase(TomlTable databaseTable)
        {
            var database = new Database_Config
            {
                Host = databaseTable["Host"].ToString()!,
                Name = databaseTable["Name"].ToString()!,
                User = databaseTable["User"].ToString()!,
                Password = databaseTable["Password"].ToString()!,
                Port = uint.Parse(databaseTable["Port"].ToString()!),
            };
            return database;
        }
        private static Menu_Settings LoadSettings(TomlTable settingsTable)
        {
            var settings = new Menu_Settings
            {
                FontName = settingsTable["FontName"].ToString()!,
                MenuType = settingsTable["MenuType"].ToString()!,
                Size = int.Parse(settingsTable["Size"].ToString()!),
                PositionX = float.Parse(settingsTable["PositionX"].ToString()!),
                PositionY = float.Parse(settingsTable["PositionY"].ToString()!),
                HasExitOption = bool.Parse(settingsTable["HasExitOption"].ToString()!),
                ShowResolutionOption = bool.Parse(settingsTable["ShowResolutionOption"].ToString()!),
                ShowDisabledOptionNum = bool.Parse(settingsTable["ShowDisabledOptionNum"].ToString()!),
                ShowPageCount = bool.Parse(settingsTable["ShowPageCount"].ToString()!),
                FreezePlayer = bool.Parse(settingsTable["FreezePlayer"].ToString()!),
                ShowControlsInfo = bool.Parse(settingsTable["ShowControlsInfo"].ToString()!)
            };

            if (settingsTable.ContainsKey("Resolutions"))
            {
                TomlTable resolutionsTable = (TomlTable)settingsTable["Resolutions"];
                foreach (var kv in resolutionsTable)
                {
                    if (kv.Value is TomlTable resolutionTable)
                    {
                        float posX = float.Parse(resolutionTable["PositionX"].ToString()!);
                        float posY = float.Parse(resolutionTable["PositionY"].ToString()!);

                        settings.Resolutions[kv.Key] = new Resolution(posX, posY);
                    }
                }
            }

            return settings;
        }
        private static Menu_Controls LoadControls(TomlTable controlsTable)
        {
            var controls = new Menu_Controls
            {
                ScrollUp = controlsTable["ScrollUp"].ToString()!,
                ScrollDown = controlsTable["ScrollDown"].ToString()!,
                Select = controlsTable["Select"].ToString()!,
                Exit = controlsTable["Exit"].ToString()!
            };
            return controls;
        }
        private static Sounds_Settings LoadSounds(TomlTable soundsTable)
        {
            var sounds = new Sounds_Settings
            {
                Select = soundsTable["Select"].ToString()!,
                Next = soundsTable["Next"].ToString()!,
                Prev = soundsTable["Prev"].ToString()!,
                Close = soundsTable["Close"].ToString()!,
                ScrollUp = soundsTable["ScrollUp"].ToString()!,
                ScrollDown = soundsTable["ScrollDown"].ToString()!,
                Volume = float.Parse(soundsTable["Volume"].ToString()!)
            };
            return sounds;
        }
        private static void CreateDefaultConfig()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            string defaultConfig = @"
# Screen Menu Configuration

[Database]
Host = ""host""
Name = ""name""
User = ""user""
Password = ""pass""
Port = 3306

[Settings]
FontName = ""Tahoma Bold""
MenuType = ""KeyPress""
Size = 25
PositionX = 0
PositionY = 0
HasExitOption = true
ShowResolutionOption = true
ShowDisabledOptionNum = true
ShowPageCount = true
FreezePlayer = true
ShowControlsInfo = true
ScrollPrefix = ""\u2023""

[Settings.Resolutions.""1920x1080""]
PositionX = -9.0
PositionY = 0.0

[Settings.Resolutions.""1440x1080""]
PositionX = -7.0
PositionY = 0.0

[Controls]
ScrollUp = ""W""
ScrollDown = ""S""
Select = ""E""
Exit = ""Tab""

[Sounds]
Select = ""menu.Select""
Next = ""menu.Select""
Prev = ""menu.Close""
Close = ""menu.Close""
ScrollUp = ""menu.ScrollUp""
ScrollDown = ""menu.ScrollDown""
Volume = 1.0

[Lang.en]
Prev = ""Back""
Next = ""Next""
Close = ""Close""
ScrollKeys = ""[{0}/{1}] Scroll""
SelectKey = ""[{0}] Select""
ExitKey = ""[{0}] Exit""
SelectRes = ""Select Your Game Resolution""
ChangeRes = ""Change Resolution""

";

            File.WriteAllText(ConfigPath, defaultConfig);
        }
    }

}