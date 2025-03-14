using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2ScreenMenuAPI.Enums;
using CS2ScreenMenuAPI.Config;
using CS2ScreenMenuAPI.Interfaces;
using CS2ScreenMenuAPI.Extensions;
using CS2ScreenMenuAPI.Extensions.PlayerSettings;
using System.Collections.Concurrent;
using CounterStrikeSharp.API.Modules.Commands;

namespace CS2ScreenMenuAPI.Internal
{
    internal class ScreenMenuInstance : IMenuInstance
    {
        private WorldTextManager.MenuTextEntities? _menuEntities;
        private static BasePlugin _plugin = null!;
        private readonly CCSPlayerController _player;
        private ScreenMenu _menu;
        private bool _isClosed = false;
        private const int NUM_PER_PAGE = 6;

        private int CurrentSelection = 0;
        private int CurrentPage = 0;
        private PlayerButtons _oldButtons;
        private readonly MenuConfig _config;
        private bool _useHandled = false;
        private bool _menuJustOpened = true;
        private static readonly ConcurrentDictionary<int, ScreenMenuInstance> _activeMenus = new();

        private static readonly ConcurrentDictionary<BasePlugin, HashSet<string>> _registeredCommands = new();
        private static readonly object _keyRegistrationLock = new object();
        private readonly Dictionary<string, CommandInfo.CommandCallback> _registeredKeyCommands = new();

        private string basePath = Path.Combine(Server.GameDirectory, "csgo", "addons", "counterstrikesharp", "shared", "CS2ScreenMenuAPI");

        private uint _currentDisabledOptionsIndex = 0;

        private readonly StringBuilder _menuTextBuilder = new(2048);

        public ScreenMenuInstance(BasePlugin plugin, CCSPlayerController player, ScreenMenu menu)
        {
            _plugin = plugin;
            _player = player;
            _config = new MenuConfig();
            _config.Initialize();
            _menu = menu;
            PlayerSettings.Initialize(basePath);

            Reset();
            _oldButtons = player.Buttons;
            _useHandled = true;
            _menuJustOpened = true;

            _activeMenus[player.Slot] = this;

            if (_activeMenus.TryGetValue(player.Slot, out var existingMenu) && existingMenu != this)
            {
                existingMenu.Close();
            }

            RegisterOnKeyPress();
            RegisterListenersNEvents();
        }
        private void RegisterOnKeyPress()
        {
            for (int i = 0; i <= 9; i++)
            {
                string commandName = $"css_{i}";
                int key = i;

                CommandInfo.CommandCallback callback = (player, info) =>
                {
                    if (player == null || player.IsBot || !player.IsValid)
                        return;

                    if (!_isClosed && player.Slot == _player.Slot && MenuAPI.GetActiveMenu(player) == this)
                    {
                        OnKeyPress(player, key);
                    }
                };

                _plugin.AddCommand(commandName, "Uses OnKeyPress", callback);

                _registeredKeyCommands[commandName] = callback;
            }
        }
        private void RegisterListenersNEvents()
        {
            _plugin.RegisterListener<Listeners.OnTick>(Update);
            _plugin.RegisterListener<Listeners.CheckTransmit>(CheckTransmitListener);
            _plugin.RegisterListener<Listeners.OnEntityDeleted>(OnEntityDeleted);
            _plugin.RegisterEventHandler<EventRoundStart>(OnRoundStart, HookMode.Pre);
            _plugin.RegisterEventHandler<EventRoundEnd>(OnRoundEnd, HookMode.Pre);
        }
        private void OnEntityDeleted(CEntityInstance entity)
        {
            uint entityIndex = entity.Index;
            if (WorldTextManager.WorldTextOwners.ContainsKey(entityIndex))
            {
                WorldTextManager.WorldTextOwners.Remove(entityIndex);
            }

            if (entityIndex == _currentDisabledOptionsIndex)
            {
                _currentDisabledOptionsIndex = 0;
            }

            if (WorldTextManager.EntityTransforms.ContainsKey(entityIndex))
            {
                WorldTextManager.EntityTransforms.Remove(entityIndex);
            }
        }

        private void CheckTransmitListener(CCheckTransmitInfoList infoList)
        {
            foreach ((CCheckTransmitInfo info, CCSPlayerController? client) in infoList)
            {
                if (!CCSPlayer.IsValidPlayer(client))
                    continue;

                foreach (var kvp in WorldTextManager.WorldTextOwners)
                {
                    uint worldTextIndex = kvp.Key;
                    CCSPlayerController owner = kvp.Value;

                    if (client?.Slot != owner.Slot)
                    {
                        info.TransmitEntities.Remove((int)worldTextIndex);
                    }
                }
            }
        }
        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            if (MenuAPI.GetActiveMenu(_player) != this)
                return HookResult.Continue;

            _plugin.AddTimer(0.1f, () =>
            {
                ScreenMenu menu = _menu;
                Close();
                if (menu.IsSubMenu)
                    MenuAPI.OpenSubMenu(_plugin, _player, menu);
                else
                    MenuAPI.OpenMenu(_plugin, _player, menu);
            });
            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            if (MenuAPI.GetActiveMenu(_player) == this)
            {
                _plugin.AddTimer(0.1f, () =>
                {
                    ScreenMenu menu = _menu;
                    Close();
                    if (menu.IsSubMenu)
                        MenuAPI.OpenSubMenu(_plugin, _player, menu);
                    else
                        MenuAPI.OpenMenu(_plugin, _player, menu);
                });
            }
            return HookResult.Continue;
        }

        private IntPtr _hudObserverId = IntPtr.Zero;
        private void Update()
        {
            if (!CCSPlayer.IsValidPlayer(_player))
            {
                Close();
                return;
            }

            if (MenuAPI.GetActiveMenu(_player) != this)
                return;

            var observerServices = _player.Pawn.Value?.ObserverServices;
            if (observerServices != null)
            {
                var currentObserverPawn = observerServices.ObserverTarget?.Value?.As<CCSPlayerPawn>();
                var currentObserverId = currentObserverPawn?.Handle ?? IntPtr.Zero;

                if (currentObserverId != _hudObserverId)
                {
                    if (_menuEntities?.MainEntity != null && _menuEntities.MainEntity.IsValid)
                    {
                        _menuEntities.MainEntity.Enabled = false;
                        _menuEntities.MainEntity.AcceptInput("Kill", _menuEntities.MainEntity);
                    }

                    _menuEntities = null;
                    _hudObserverId = currentObserverId;

                    Display();
                }
            }

            var currentButtons = _player.Buttons;

            if (_menuJustOpened)
            {
                _menuJustOpened = false;
                _oldButtons = currentButtons;
                return;
            }

            HandleButtons(currentButtons);
            _oldButtons = currentButtons;
        }

        private void MoveSelection(int direction)
        {
            // Cache values to avoid recalculation
            int totalLines = GetTotalLines();
            int currentPageOffset = CurrentPage * NUM_PER_PAGE;
            int selectableCount = Math.Min(NUM_PER_PAGE, _menu.MenuOptions.Count - currentPageOffset);

            bool hasResOption = _menu.AddResolutionOption;

            int newSelection = CurrentSelection;
            int attempts = 0;

            do
            {
                newSelection = (newSelection + direction + totalLines) % totalLines;

                if (newSelection < selectableCount)
                {
                    if (!_menu.MenuOptions[currentPageOffset + newSelection].Disabled)
                        break;
                }
                else if (newSelection >= selectableCount)
                {
                    if (hasResOption && newSelection == totalLines - 1)
                        break;
                    else if (newSelection < totalLines - (hasResOption ? 1 : 0))
                        break;
                }

                attempts++;
            }
            while (attempts < totalLines);

            CurrentSelection = newSelection;
        }

        private void HandleButtons(PlayerButtons currentButtons)
        {
            if (_menu.MenuType != MenuType.KeyPress)
            {
                if (Buttons.Buttons.ButtonMapping.TryGetValue(_config.Buttons.ScrollUpButton, out var scrollUpButton))
                {
                    if (((_oldButtons & scrollUpButton) == 0) && ((currentButtons & scrollUpButton) != 0))
                    {
                        if (!string.IsNullOrEmpty(_config.Sounds.ScrollUp))
                        {
                            _player.ExecuteClientCommand($"play {_config.Sounds.ScrollUp}");
                        }
                        MoveSelection(-1);
                        Display();
                    }
                }

                if (Buttons.Buttons.ButtonMapping.TryGetValue(_config.Buttons.ScrollDownButton, out var scrollDownButton))
                {
                    if (((_oldButtons & scrollDownButton) == 0) && ((currentButtons & scrollDownButton) != 0))
                    {
                        if (!string.IsNullOrEmpty(_config.Sounds.ScrollDown))
                        {
                            _player.ExecuteClientCommand($"play {_config.Sounds.ScrollDown}");
                        }
                        MoveSelection(1);
                        Display();
                    }
                }

                if (Buttons.Buttons.ButtonMapping.TryGetValue(_config.Buttons.SelectButton, out var selectButton))
                {
                    if (((_oldButtons & selectButton) == 0) && ((currentButtons & selectButton) != 0))
                    {
                        if (!_useHandled)
                        {
                            HandleSelection();
                            Display();
                            _useHandled = true;
                        }
                    }
                    else if ((currentButtons & selectButton) == 0)
                    {
                        _useHandled = false;
                    }
                }
            }
        }

        public void Display()
        {
            var settings = PlayerSettings.GetPlayerSettings(_player.SteamID.ToString());

            if (string.IsNullOrEmpty(settings.Resolution))
            {
                var previousMenu = _menu;
                var resolutionMenu = new ScreenMenu(_config.Translations.MenuResolutionTitle, _plugin) // Open the resolution menu
                {
                    PostSelectAction = PostSelectAction.Close,
                    PositionX = -5.5f,
                    HasExitOption = false,
                };
                foreach (var resolution in _config.Resolution.MenuResoltions.Keys)
                {
                    resolutionMenu.AddOption(resolution, (player, option) =>
                    {
                        var playerSettings = PlayerSettings.GetPlayerSettings(player.SteamID.ToString());
                        playerSettings.Resolution = resolution;
                        PlayerSettings.SetSettings(player.SteamID.ToString(), playerSettings);
                        player.PrintToChat(_config.Translations.ResolutionSet.Replace("{res}", resolution));

                        _menu = previousMenu;
                        Display();
                    });
                }

                _menu = resolutionMenu;
            }
            if (_config.Resolution.MenuResoltions.TryGetValue(settings.Resolution, out var menuRes))
            {
                _menu.PositionX = menuRes.PositionX;
            }

            // Adjust for FOV
            float posX = _menu.PositionX;
            float posY = _menu.PositionY;
            float menuSize = _menu.Size;

            // Get the player's FOV and adjust the menu parameters
            PlayerExtensions.AdjustMenuForFOV(_player, ref posX, ref posY, ref menuSize);

            // Build the menu text
            _menuTextBuilder.Clear();
            BuildMenuText(_menuTextBuilder);

            if (_menuEntities == null || _menuEntities?.MainEntity == null || !_menuEntities.MainEntity.IsValid)
            {
                if (_menuEntities?.MainEntity != null && _menuEntities.MainEntity.IsValid)
                {
                    _menuEntities.MainEntity.Enabled = false;
                    _menuEntities.MainEntity.AcceptInput("Kill", _menuEntities.MainEntity);
                }

                _menuEntities = WorldTextManager.Create(
                    _player,
                    _menuTextBuilder.ToString(),
                    menuSize,
                    _menu.TextColor,
                    _menu.FontName,
                    posX,
                    posY,
                    _menu.Background,
                    _menu.BackgroundHeight,
                    _menu.BackgroundWidth
                );
            }
            else
            {
                _menuEntities.MainEntity.AcceptInput("SetMessage", _menuEntities.MainEntity,
                    _menuEntities.MainEntity, _menuTextBuilder.ToString());

                if (Math.Abs(menuSize - _menu.Size) > 0.5f ||
                    Math.Abs(posX - _menu.PositionX) > 0.5f ||
                    Math.Abs(posY - _menu.PositionY) > 0.5f)
                {
                    _menuEntities.MainEntity.Enabled = false;
                    _menuEntities.MainEntity.AcceptInput("Kill", _menuEntities.MainEntity);

                    _menuEntities = WorldTextManager.Create(
                        _player,
                        _menuTextBuilder.ToString(),
                        menuSize,
                        _menu.TextColor,
                        _menu.FontName,
                        posX,
                        posY,
                        _menu.Background,
                        _menu.BackgroundHeight,
                        _menu.BackgroundWidth
                    );
                }
            }
        }

        private void OpenResolutionMenu(ScreenMenu? returnMenu = null)
        {
            var resolutionMenu = new ScreenMenu(_config.Translations.MenuResolutionTitle, _plugin)
            {
                PostSelectAction = PostSelectAction.Close,
                PositionX = -5.5f,
                HasExitOption = true,
                IsSubMenu = true,
                AddResolutionOption = false,
                ParentMenu = returnMenu
            };

            foreach (var resolution in _config.Resolution.MenuResoltions.Keys)
            {
                resolutionMenu.AddOption(resolution, (player, option) =>
                {
                    var playerSettings = PlayerSettings.GetPlayerSettings(player.SteamID.ToString());
                    playerSettings.Resolution = resolution;
                    PlayerSettings.SetSettings(player.SteamID.ToString(), playerSettings);
                    player.PrintToChat(_config.Translations.ResolutionSet.Replace("{res}", resolution));

                    if (returnMenu != null)
                    {
                        SmoothTransitionToMenu(returnMenu);
                    }
                    else
                    {
                        Close();
                    }
                });
            }

            if (returnMenu != null)
            {
                SmoothTransitionToMenu(resolutionMenu);
            }
            else
            {
                _menu = resolutionMenu;
                Display();
            }
        }

        private void BuildMenuText(StringBuilder builder)
        {
            int currentOffset = CurrentPage * NUM_PER_PAGE;
            int selectable = Math.Min(NUM_PER_PAGE, _menu.MenuOptions.Count - currentOffset);
            if (CurrentSelection == 0 && selectable > 0 && _menu.MenuOptions[currentOffset].Disabled)
            {
                for (int i = 1; i < selectable; i++)
                {
                    if (!_menu.MenuOptions[currentOffset + i].Disabled)
                    {
                        CurrentSelection = i;
                        break;
                    }
                }
            }

            int maxLength = _menu.Title.Length;

            for (int i = currentOffset; i < currentOffset + selectable; i++)
            {
                var option = _menu.MenuOptions[i];
                string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == i % NUM_PER_PAGE)
                    ? _config.Translations.SelectPrefix : "";

                bool shouldShowNumber = option.Disabled
                    ? _config.DefaultSettings.EnableDisabledOptionsCount
                    : _config.DefaultSettings.EnableOptionsCount;

                string numberPart = shouldShowNumber ? $"{i + 1}. " : "";
                string optionText = prefix + numberPart + option.Text;

                maxLength = Math.Max(maxLength, optionText.Length);
            }

            maxLength = Math.Max(maxLength, $"7. {_config.Translations.BackButton}".Length);
            maxLength = Math.Max(maxLength, $"8. {_config.Translations.NextButton}".Length);
            maxLength = Math.Max(maxLength, $"9. {_config.Translations.ExitButton}".Length);

            if (_menu.AddResolutionOption)
            {
                maxLength = Math.Max(maxLength, $"0. {_config.Translations.ResolutionOption}".Length);
            }

            string titlePadding = new string('\u2800', maxLength - _menu.Title.Length + 4);

            if (_config.DefaultSettings.Spacing)
            {
                builder.AppendLine("\u2800");
            }

            builder.AppendLine(_menu.Title + titlePadding);
            builder.AppendLine("");

            int globalOptionCount = 0;
            for (int i = 0; i < selectable; i++)
            {
                var option = _menu.MenuOptions[currentOffset + i];
                string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == i)
                    ? _config.Translations.SelectPrefix : "";

                bool shouldNumber = !option.Disabled
                    ? _config.DefaultSettings.EnableOptionsCount
                    : _config.DefaultSettings.EnableDisabledOptionsCount;

                string numberPart = "";
                if (shouldNumber)
                {
                    globalOptionCount++;
                    numberPart = $"{globalOptionCount}. ";
                }

                builder.AppendLine(prefix + numberPart + option.Text);
            }

            builder.AppendLine("");

            // Navigation buttons section rewritten for better readability and performance
            if (CurrentPage == 0)
            {
                if (_menu.IsSubMenu)
                {
                    string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable)
                        ? _config.Translations.SelectPrefix : "";
                    builder.AppendLine($"{prefix}7. {_config.Translations.BackButton}");

                    if (_menu.MenuOptions.Count > NUM_PER_PAGE)
                    {
                        prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + 1)
                            ? _config.Translations.SelectPrefix : "";
                        builder.AppendLine($"{prefix}8. {_config.Translations.NextButton}");
                    }

                    if (_menu.HasExitOption)
                    {
                        prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + (_menu.MenuOptions.Count > NUM_PER_PAGE ? 2 : 1))
                            ? _config.Translations.SelectPrefix : "";
                        builder.AppendLine($"{prefix}9. {_config.Translations.ExitButton}");
                    }
                }
                else
                {
                    if (_menu.MenuOptions.Count > NUM_PER_PAGE)
                    {
                        string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable)
                            ? _config.Translations.SelectPrefix : "";
                        builder.AppendLine($"{prefix}8. {_config.Translations.NextButton}");
                    }

                    if (_menu.HasExitOption)
                    {
                        string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + (_menu.MenuOptions.Count > NUM_PER_PAGE ? 1 : 0))
                            ? _config.Translations.SelectPrefix : "";
                        builder.AppendLine($"{prefix}9. {_config.Translations.ExitButton}");
                    }
                }
            }
            else
            {
                string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable)
                    ? _config.Translations.SelectPrefix : "";
                builder.AppendLine($"{prefix}7. {_config.Translations.BackButton}");

                if ((_menu.MenuOptions.Count - CurrentPage * NUM_PER_PAGE) > NUM_PER_PAGE)
                {
                    prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + 1)
                        ? _config.Translations.SelectPrefix : "";
                    builder.AppendLine($"{prefix}8. {_config.Translations.NextButton}");
                }

                if (_menu.HasExitOption)
                {
                    int navOffset = (_menu.MenuOptions.Count - CurrentPage * NUM_PER_PAGE) > NUM_PER_PAGE ? 2 : 1;
                    prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + navOffset)
                        ? _config.Translations.SelectPrefix : "";
                    builder.AppendLine($"{prefix}9. {_config.Translations.ExitButton}");
                }
            }

            if (_menu.AddResolutionOption)
            {
                int navCount = 0;
                if (CurrentPage == 0)
                {
                    if (_menu.IsSubMenu)
                    {
                        navCount = 1; // Back button
                        if (_menu.MenuOptions.Count > NUM_PER_PAGE)
                            navCount++; // Next exists
                        if (_menu.HasExitOption)
                            navCount++; // Exit exists
                    }
                    else
                    {
                        if (_menu.MenuOptions.Count > NUM_PER_PAGE)
                            navCount++; // Next exists
                        if (_menu.HasExitOption)
                            navCount++; // Exit exists
                    }
                }
                else
                {
                    navCount = 1; // Back button
                    if ((_menu.MenuOptions.Count - CurrentPage * NUM_PER_PAGE) > NUM_PER_PAGE)
                        navCount++; // Next exists
                    if (_menu.HasExitOption)
                        navCount++; // Exit exists
                }

                int resolutionSelectionIndex = selectable + navCount;

                builder.AppendLine("");
                string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == resolutionSelectionIndex)
                                    ? _config.Translations.SelectPrefix : "";
                builder.AppendLine($"{prefix}0. {_config.Translations.ResolutionOption}");
            }

            // Control info
            if (_menu.MenuType == MenuType.Both || _menu.MenuType == MenuType.Scrollable)
            {
                builder.AppendLine("");
                builder.AppendLine(_config.Translations.ScrollInfo);
                builder.AppendLine(_config.Translations.SelectInfo);
            }

            if (_config.DefaultSettings.Spacing)
            {
                builder.AppendLine("\u2800");
            }
        }

        private int GetTotalLines()
        {
            int currentOffset = CurrentPage * NUM_PER_PAGE;
            int selectable = Math.Min(NUM_PER_PAGE, _menu.MenuOptions.Count - currentOffset);
            int navCount = 0;

            if (CurrentPage == 0)
            {
                if (_menu.IsSubMenu)
                {
                    navCount = 1; // Back
                    if (_menu.MenuOptions.Count > NUM_PER_PAGE)
                        navCount++; // Next exists
                    if (_menu.HasExitOption)
                        navCount++; // Close exists
                }
                else
                {
                    if (_menu.MenuOptions.Count > NUM_PER_PAGE)
                        navCount++; // Next exists
                    if (_menu.HasExitOption)
                        navCount++; // Close exists
                }
            }
            else
            {
                navCount = 1; // Back
                if ((_menu.MenuOptions.Count - currentOffset) > NUM_PER_PAGE)
                    navCount++; // Next exists
                if (_menu.HasExitOption)
                    navCount++; // Close exists
            }
            int resolutionOption = _menu.AddResolutionOption ? 1 : 0;

            return selectable + navCount + resolutionOption;
        }

        private void HandleSelection()
        {
            int currentOffset = CurrentPage * NUM_PER_PAGE;
            int selectable = Math.Min(NUM_PER_PAGE, _menu.MenuOptions.Count - currentOffset);
            int totalLines = GetTotalLines();

            if (_menu.AddResolutionOption && CurrentSelection == totalLines - 1)
            {
                if (!string.IsNullOrEmpty(_config.Sounds.Select))
                {
                    _player.ExecuteClientCommand($"play {_config.Sounds.Select}");
                }
                OpenResolutionMenu(_menu);
                return;
            }

            if (CurrentSelection < selectable)
            {
                int optionIndex = currentOffset + CurrentSelection;
                if (optionIndex < _menu.MenuOptions.Count)
                {
                    var option = _menu.MenuOptions[optionIndex];
                    if (!option.Disabled)
                    {
                        ScreenMenu? submenu = null;
                        if (option is MenuOption menuOption)
                        {
                            submenu = menuOption.SubMenu;
                        }

                        if (submenu != null)
                        {
                            if (!string.IsNullOrEmpty(_config.Sounds.Select))
                            {
                                _player.ExecuteClientCommand($"play {_config.Sounds.Select}");
                            }
                            SmoothTransitionToMenu(submenu);
                        }
                        else
                        {
                            option.OnSelect(_player, option);
                            if (!string.IsNullOrEmpty(_config.Sounds.Select))
                            {
                                _player.ExecuteClientCommand($"play {_config.Sounds.Select}");
                            }

                            switch (_menu.PostSelectAction)
                            {
                                case PostSelectAction.Close:
                                    Close();
                                    break;
                                case PostSelectAction.Reset:
                                    Reset();
                                    break;
                                case PostSelectAction.Nothing:
                                    break;
                                default:
                                    throw new NotImplementedException("The specified Select Action is not supported!");
                            }
                        }
                    }
                }
            }
            else
            {
                HandleNavigationSelection(selectable);
            }
        }

        private void HandleNavigationSelection(int selectable)
        {
            int navIndex = CurrentSelection - selectable;

            if (CurrentPage == 0)
            {
                if (_menu.IsSubMenu)
                {
                    if (navIndex == 0)
                    {

                        if (_menu.ParentMenu == null)
                            return;

                        if (!string.IsNullOrEmpty(_config.Sounds.Exit))
                        {
                            _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                        }
                        SmoothTransitionToMenu(_menu.ParentMenu);
                    }
                    else if (navIndex == 1)
                    {
                        if (_menu.MenuOptions.Count > NUM_PER_PAGE)
                        {
                            int newPage = CurrentPage + 1;
                            int newSelectable = Math.Min(NUM_PER_PAGE, _menu.MenuOptions.Count - (newPage * NUM_PER_PAGE));
                            int desiredSelection = newSelectable + 1;
                            NextPage(desiredSelection);
                        }
                        else if (_menu.HasExitOption)
                        {
                            if (!string.IsNullOrEmpty(_config.Sounds.Exit))
                            {
                                _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                            }
                            Close();
                        }
                        else
                        {
                            return;
                        }
                    }
                    else if (navIndex == 2)
                    {
                        if (!(_menu.MenuOptions.Count > NUM_PER_PAGE && _menu.HasExitOption))
                            return;

                        if (!string.IsNullOrEmpty(_config.Sounds.Exit))
                        {
                            _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                        }
                        Close();
                    }
                }
                else
                {
                    if (_menu.MenuOptions.Count > NUM_PER_PAGE)
                    {
                        if (navIndex == 0)
                        {
                            int newPage = CurrentPage + 1;
                            int newSelectable = Math.Min(NUM_PER_PAGE, _menu.MenuOptions.Count - (newPage * NUM_PER_PAGE));
                            int desiredSelection = newSelectable + 1;
                            NextPage(desiredSelection);
                        }
                        else if (navIndex == 1)
                        {
                            if (!_menu.HasExitOption)
                                return;

                            if (!string.IsNullOrEmpty(_config.Sounds.Exit))
                            {
                                _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                            }
                            Close();
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        if (_menu.HasExitOption && navIndex == 0)
                        {
                            if (!string.IsNullOrEmpty(_config.Sounds.Exit))
                            {
                                _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                            }
                            Close();
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            }
            else
            {
                if (navIndex == 0)
                {
                    int newPage = CurrentPage - 1;
                    int newSelectable = Math.Min(NUM_PER_PAGE, _menu.MenuOptions.Count - (newPage * NUM_PER_PAGE));
                    int desiredSelection = (_menu.IsSubMenu || newPage > 0) ? newSelectable : 0;
                    PrevPage(desiredSelection);
                }
                else if (navIndex == 1)
                {
                    if ((_menu.MenuOptions.Count - CurrentPage * NUM_PER_PAGE) > NUM_PER_PAGE)
                    {
                        int newPage = CurrentPage + 1;
                        int newSelectable = Math.Min(NUM_PER_PAGE, _menu.MenuOptions.Count - (newPage * NUM_PER_PAGE));
                        int desiredSelection = newSelectable + 1;
                        NextPage(desiredSelection);
                    }
                    else if (_menu.HasExitOption)
                    {
                        if (!string.IsNullOrEmpty(_config.Sounds.Exit))
                        {
                            _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                        }
                        Close();
                    }
                    else
                    {
                        return;
                    }
                }
                else if (navIndex == 2)
                {
                    if (!((_menu.MenuOptions.Count - CurrentPage * NUM_PER_PAGE) > NUM_PER_PAGE && _menu.HasExitOption))
                        return;

                    if (!string.IsNullOrEmpty(_config.Sounds.Exit))
                    {
                        _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                    }
                    Close();
                }
            }
        }


        public void NextPage(int nextSelectionIndex = -1)
        {
            if ((CurrentPage + 1) * NUM_PER_PAGE < _menu.MenuOptions.Count)
            {
                CurrentPage++;
                CurrentSelection = (nextSelectionIndex >= 0) ? nextSelectionIndex : 0;
                Display();
                if (!string.IsNullOrEmpty(_config.Sounds.Next))
                {
                    _player.ExecuteClientCommand($"play {_config.Sounds.Next}");
                }
            }
        }

        public void PrevPage(int prevSelectionIndex = -1)
        {
            if (CurrentPage > 0)
            {
                CurrentPage--;
                CurrentSelection = (prevSelectionIndex >= 0) ? prevSelectionIndex : 0;
                Display();
                if (!string.IsNullOrEmpty(_config.Sounds.Back))
                {
                    _player.ExecuteClientCommand($"play {_config.Sounds.Back}");
                }
            }
        }

        public void OnKeyPress(CCSPlayerController player, int key)
        {
            if (_isClosed || player.Handle != _player.Handle || MenuAPI.GetActiveMenu(player) != this)
                return;

            if (_menu.MenuType == MenuType.Scrollable)
                return;

            if (key == 0 && _menu.AddResolutionOption)
            {
                _player.ExecuteClientCommand($"play {_config.Sounds.Select}");
                OpenResolutionMenu(_menu);
                return;
            }

            if (key == 9)
            {
                if (_menu.HasExitOption)
                    Close();
                _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                return;
            }

            if (key == 7)
            {
                if (CurrentPage == 0 && _menu.IsSubMenu)
                {
                    if (_menu.ParentMenu != null)
                    {
                        _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                        SmoothTransitionToMenu(_menu.ParentMenu);
                    }
                    else
                    {
                        Close();
                        _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                    }
                    return;
                }
                else if (CurrentPage > 0)
                {
                    PrevPage();
                    return;
                }
            }

            if (key == 8)
            {
                if ((_menu.MenuOptions.Count - (CurrentPage * NUM_PER_PAGE)) > NUM_PER_PAGE)
                {
                    NextPage();
                }
                return;
            }

            int currentOffset = CurrentPage * NUM_PER_PAGE;
            int totalDisplayed = Math.Min(NUM_PER_PAGE, _menu.MenuOptions.Count - currentOffset);

            var keyToOptionMap = new Dictionary<int, int>();
            int globalOptionCount = 0;

            for (int i = 0; i < totalDisplayed; i++)
            {
                var option = _menu.MenuOptions[currentOffset + i];
                bool shouldNumber = !option.Disabled
                    ? _config.DefaultSettings.EnableOptionsCount
                    : _config.DefaultSettings.EnableDisabledOptionsCount;
                if (shouldNumber)
                {
                    globalOptionCount++;
                    keyToOptionMap[globalOptionCount] = i;
                }
            }


            if (!_config.DefaultSettings.EnableOptionsCount && key >= 1 && key <= totalDisplayed)
            {
                int enabledFound = 0;
                for (int i = 0; i < totalDisplayed; i++)
                {
                    var option = _menu.MenuOptions[currentOffset + i];
                    if (!option.Disabled)
                    {
                        enabledFound++;
                        if (enabledFound == key)
                        {
                            keyToOptionMap[key] = i;
                            break;
                        }
                    }
                }
            }

            if (keyToOptionMap.ContainsKey(key))
            {
                int optionIndex = keyToOptionMap[key];
                var option = _menu.MenuOptions[currentOffset + optionIndex];

                if (option.Disabled)
                    return;

                ScreenMenu? submenu = null;
                if (option is MenuOption menuOption)
                {
                    submenu = menuOption.SubMenu;
                }

                if (submenu != null)
                {
                    if (!string.IsNullOrEmpty(_config.Sounds.Select))
                    {
                        _player.ExecuteClientCommand($"play {_config.Sounds.Select}");
                    }
                    SmoothTransitionToMenu(submenu);
                }
                else
                {
                    if (!string.IsNullOrEmpty(_config.Sounds.Select))
                    {
                        _player.ExecuteClientCommand($"play {_config.Sounds.Select}");
                    }
                    option.OnSelect(_player, option);

                    switch (_menu.PostSelectAction)
                    {
                        case PostSelectAction.Close:
                            Close();
                            break;
                        case PostSelectAction.Reset:
                            Reset();
                            break;
                        case PostSelectAction.Nothing:
                            break;
                        default:
                            throw new NotImplementedException("The specified Select Action is not supported!");
                    }
                }
            }

        }

        public void SmoothTransitionToMenu(ScreenMenu newMenu)
        {
            var currentEntity = _menuEntities?.MainEntity;
            bool freezeStatus = _menu.FreezePlayer;

            _menu = newMenu;
            CurrentPage = 0;
            CurrentSelection = 0;

            if (freezeStatus != _menu.FreezePlayer)
            {
                if (_menu.FreezePlayer)
                    _player.Freeze();
                else
                    _player.Unfreeze();
            }

            // Reuse the StringBuilder
            _menuTextBuilder.Clear();
            BuildMenuText(_menuTextBuilder);

            bool entityValid = currentEntity != null && currentEntity.IsValid;

            if (!entityValid)
            {
                if (_menuEntities?.MainEntity != null && _menuEntities.MainEntity.IsValid)
                {
                    _menuEntities.MainEntity.Enabled = false;
                    _menuEntities.MainEntity.AcceptInput("Kill", _menuEntities.MainEntity);
                }

                _menuEntities = null;
                Display();
            }
            else
            {
                currentEntity?.AcceptInput("SetMessage", currentEntity,
                    currentEntity, _menuTextBuilder.ToString());
            }

            MenuAPI.UpdateActiveMenu(_player, this);
        }

        public void Close()
        {
            if (_isClosed)
                return;

            _isClosed = true;

            foreach (var commandEntry in _registeredKeyCommands)
            {
                _plugin.RemoveCommand(commandEntry.Key, commandEntry.Value);
            }
            _registeredKeyCommands.Clear();

            if (_menuEntities != null)
            {
                if (_menu.FreezePlayer)
                {
                    _player.Unfreeze();
                }

                if (_menuEntities.MainEntity != null && _menuEntities.MainEntity.IsValid)
                {
                    _menuEntities.MainEntity.Enabled = false;
                    _menuEntities.MainEntity.AcceptInput("Kill", _menuEntities.MainEntity);
                }
            }

            if (_activeMenus.TryGetValue(_player.Slot, out var activeMenu) && activeMenu == this)
            {
                _activeMenus.TryRemove(_player.Slot, out _);
                MenuAPI.RemoveActiveMenu(_player);
            }

            UnregisterListeners();
        }

        public void Reset()
        {
            CurrentPage = 0;
            CurrentSelection = 0;
            int currentOffset = CurrentPage * NUM_PER_PAGE;
            if (_menu.MenuOptions.Count > currentOffset && _menu.MenuOptions[currentOffset].Disabled)
            {
                MoveSelection(1);
            }
            Display();
        }

        private void UnregisterListeners()
        {
            _plugin.RemoveListener<Listeners.OnTick>(Update);
            _plugin.RemoveListener<Listeners.CheckTransmit>(CheckTransmitListener);
            _plugin.RemoveListener<Listeners.OnEntityDeleted>(OnEntityDeleted);
        }
    }
}