// ScreenMenu.cs - Update the AddSubMenu method
using System.Drawing;
using CounterStrikeSharp.API.Core;
using CS2ScreenMenuAPI.Config;
using CS2ScreenMenuAPI.Enums;

namespace CS2ScreenMenuAPI.Internal
{
    public class ScreenMenu : BaseMenu
    {
        private readonly BasePlugin _plugin;
        private readonly MenuConfig _config;
        public bool IsSubMenu { get; set; } = false;
        public bool HasExitOption { get; set; } = true;
        public ScreenMenu? ParentMenu { get; set; } = null;
        public PostSelectAction PostSelectAction { get; set; } = PostSelectAction.Nothing;
        public int OptionsCount => MenuOptions.Count;
        public Color TextColor
        {
            get => _config.DefaultSettings.TextColor;
            set => _config.DefaultSettings.TextColor = value;
        }
        public string FontName
        {
            get => _config.DefaultSettings.Font;
            set => _config.DefaultSettings.Font = value;
        }
        public MenuType MenuType
        {
            get => _config.DefaultSettings.MenuType;
            set => _config.DefaultSettings.MenuType = value;
        }
        public float PositionX
        {
            get => _config.DefaultSettings.PositionX;
            set => _config.DefaultSettings.PositionX = value;
        }
        public float PositionY
        {
            get => _config.DefaultSettings.PositionY;
            set => _config.DefaultSettings.PositionY = value;
        }
        public bool Background
        {
            get => _config.DefaultSettings.Background;
            set => _config.DefaultSettings.Background = value;
        }
        public float BackgroundHeight
        {
            get => _config.DefaultSettings.BackgroundHeight;
            set => _config.DefaultSettings.BackgroundHeight = value;
        }
        public float BackgroundWidth
        {
            get => _config.DefaultSettings.BackgroundWidth;
            set => _config.DefaultSettings.BackgroundWidth = value;
        }
        public float Size
        {
            get => _config.DefaultSettings.Size;
            set => _config.DefaultSettings.Size = value;
        }
        public bool EnableOptionsCount
        {
            get => _config.DefaultSettings.EnableOptionsCount;
            set => _config.DefaultSettings.EnableOptionsCount = value;
        }
        public bool FreezePlayer
        {
            get => _config.DefaultSettings.FreezePlayer;
            set => _config.DefaultSettings.FreezePlayer = value;
        }
        public bool AddResolutionOption
        {
            get => _config.DefaultSettings.AddResolutionOption;
            set => _config.DefaultSettings.AddResolutionOption = value;
        }

        public ScreenMenu(string title, BasePlugin plugin) : base(title)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _config = new MenuConfig();
            _config.Initialize();
        }

        public void Open(CCSPlayerController player)
        {
            if (IsSubMenu)
            {
                MenuAPI.OpenSubMenu(_plugin, player, this);
            }
            else
            {
                MenuAPI.OpenMenu(_plugin, player, this);
            }
        }
    }
}