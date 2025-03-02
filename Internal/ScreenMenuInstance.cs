using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2ScreenMenuAPI.Enums;
using CS2ScreenMenuAPI.Config;
using CS2ScreenMenuAPI.Interfaces;
using CS2ScreenMenuAPI.Extensions;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;

namespace CS2ScreenMenuAPI.Internal
{
    internal class ScreenMenuInstance : IMenuInstance
    {
        private WorldTextManager.MenuTextEntities? _menuEntities;
        private CPointWorldText? _hudText;
        private readonly BasePlugin _plugin;
        private readonly CCSPlayerController _player;
        private readonly ScreenMenu _menu;
        private const int NUM_PER_PAGE = 6;

        private int CurrentSelection = 0;
        private int CurrentPage = 0;
        private PlayerButtons _oldButtons;
        private readonly MenuConfig _config;
        private bool _useHandled = false;
        private bool _menuJustOpened = true;

        private readonly Listeners.OnTick _onTickDelegate;
        private readonly Listeners.CheckTransmit _checkTransmitDelegate;
        private readonly Listeners.OnEntityDeleted _onEntityDeletedDelegate;
        private readonly BasePlugin.GameEventHandler<EventRoundStart> _onRoundStartDelegate;
        private readonly BasePlugin.GameEventHandler<EventRoundEnd> _onRoundEndDelegate;
        private static bool _keyCommandsRegistered = false;
        private Vector _fixedForward = new();
        private bool _fixedForwardSet = false;

        public ScreenMenuInstance(BasePlugin plugin, CCSPlayerController player, ScreenMenu menu)
        {
            _plugin = plugin;
            _player = player;
            _menu = menu;

            _config = new MenuConfig();
            _config.Initialize();

            Reset();
            _oldButtons = player.Buttons;
            _useHandled = true;
            _menuJustOpened = true;

            _onTickDelegate = new Listeners.OnTick(Update);
            _checkTransmitDelegate = new Listeners.CheckTransmit(CheckTransmitListener);
            _onEntityDeletedDelegate = new Listeners.OnEntityDeleted(OnEntityDeleted);
            _onRoundEndDelegate = new BasePlugin.GameEventHandler<EventRoundEnd>(OnRoundEnd);
            _onRoundStartDelegate = new BasePlugin.GameEventHandler<EventRoundStart>(OnRoundStart);

            RegisterOnKeyPress();
            RegisterListenersNEvents();
        }

        private void RegisterOnKeyPress()
        {
            if (!_keyCommandsRegistered)
            {
                for (int i = 1; i <= 9; i++)
                {
                    int key = i;
                    _plugin.AddCommand($"css_{key}", "Uses OnKeyPress", (player, info) =>
                    {
                        if (player == null || player.IsBot || !player.IsValid)
                            return;

                        var menu = MenuAPI.GetActiveMenu(player);
                        menu?.OnKeyPress(player, key);
                    });
                }
                _keyCommandsRegistered = true;
            }
        }

        private void RegisterListenersNEvents()
        {
            _plugin.RegisterListener<Listeners.OnTick>(_onTickDelegate);
            _plugin.RegisterListener<Listeners.CheckTransmit>(_checkTransmitDelegate);
            _plugin.RegisterListener<Listeners.OnEntityDeleted>(_onEntityDeletedDelegate);
            _plugin.RegisterEventHandler<EventRoundStart>(_onRoundStartDelegate, HookMode.Pre);
            _plugin.RegisterEventHandler<EventRoundEnd>(_onRoundEndDelegate, HookMode.Pre);
        }

        private void OnEntityDeleted(CEntityInstance entity)
        {
            uint entityIndex = entity.Index;
            if (WorldTextManager.WorldTextOwners.ContainsKey(entityIndex))
            {
                WorldTextManager.WorldTextOwners.Remove(entityIndex);
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

            _plugin.AddTimer(0.1f, RecreateHud);
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
                    if (_menuEntities != null)
                    {
                        if (_menuEntities.EnabledOptions != null && _menuEntities.EnabledOptions.IsValid)
                        {
                            _menuEntities.EnabledOptions.Enabled = false;
                            _menuEntities.EnabledOptions.AcceptInput("Kill", _menuEntities.EnabledOptions);
                        }
                        if (_menuEntities.DisabledOptions != null && _menuEntities.DisabledOptions.IsValid)
                        {
                            _menuEntities.DisabledOptions.Enabled = false;
                            _menuEntities.DisabledOptions.AcceptInput("Kill", _menuEntities.DisabledOptions);
                        }
                        _menuEntities = null;
                    }
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
            int totalLines = GetTotalLines();
            int selectableCount = Math.Min(NUM_PER_PAGE, _menu.MenuOptions.Count - (CurrentPage * NUM_PER_PAGE));
            int newSelection = CurrentSelection;
            int attempts = 0;

            do
            {
                newSelection = (newSelection + direction + totalLines) % totalLines;
                if (newSelection < selectableCount)
                {
                    if (!_menu.MenuOptions[CurrentPage * NUM_PER_PAGE + newSelection].Disabled)
                        break;
                }
                else
                {
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
        bool DisabledTeleport = false;
        public void Display()
        {
            var enabledBuilder = new StringBuilder();
            var disabledBuilder = new StringBuilder();
            BuildMenuText(enabledBuilder, disabledBuilder);

            if (_menuEntities == null)
            {
                _menuEntities = WorldTextManager.Create(
                    _player,
                    "",
                    _menu.Size,
                    _menu.TextColor,
                    _menu.FontName,
                    _menu.PositionX,
                    _menu.PositionY,
                    _menu.Background,
                    _menu.BackgroundHeight,
                    _menu.BackgroundWidth
                );
            }
            if (_menuEntities?.EnabledOptions != null && _menuEntities.EnabledOptions.IsValid)
            {
                _menuEntities.EnabledOptions.AcceptInput("SetMessage", _menuEntities.EnabledOptions,
                    _menuEntities.EnabledOptions, enabledBuilder.ToString());
            }

            if (_menuEntities?.DisabledOptions != null && _menuEntities.DisabledOptions.IsValid)
            {
                _menuEntities.DisabledOptions.AcceptInput("SetMessage", _menuEntities.DisabledOptions,
                    _menuEntities.DisabledOptions, disabledBuilder.ToString());

                if (_menuEntities.EnabledOptions != null && _menuEntities.EnabledOptions.IsValid)
                {
                    var pawn = _player.PlayerPawn.Value;
                    if (pawn != null)
                    {
                        Vector enabledPos = _menuEntities.EnabledOptions.GetPosition();
                        QAngle enabledAngles = _menuEntities.EnabledOptions.GetAngles();

                        if (!_fixedForwardSet)
                        {
                            QAngle eyeAngles = pawn.EyeAngles;
                            Vector forward = new(), right = new(), up = new();
                            NativeAPI.AngleVectors(eyeAngles.Handle, forward.Handle, right.Handle, up.Handle);
                            _fixedForward = forward;
                            _fixedForwardSet = true;
                        }

                        Vector disabledPos = enabledPos - (_fixedForward * 0.05f);
                        if (!DisabledTeleport)
                        {
                            _menuEntities.DisabledOptions.Teleport(
                                disabledPos,
                                enabledAngles,
                                null
                            );
                            DisabledTeleport = true;
                        }
                    }
                }
            }
        }
        private void BuildMenuText(StringBuilder enabledBuilder, StringBuilder disabledBuilder)
        {
            bool hasDisabledOptions = _menu.MenuOptions.Any(option => option.Disabled);
            int currentOffset = CurrentPage * NUM_PER_PAGE;
            int selectable = Math.Min(NUM_PER_PAGE, _menu.MenuOptions.Count - currentOffset);

            int maxLength = _menu.Title.Length;

            for (int i = 0; i < _menu.MenuOptions.Count; i++)
            {
                var option = _menu.MenuOptions[i];
                string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == i % NUM_PER_PAGE)
                    ? _config.Translations.SelectPrefix : "";
                int currentCount = _config.DefaultSettings.EnableDisabledOptionsCount ? (i + 1) : 0;
                string numberPart = (_config.DefaultSettings.EnableOptionsCount || _config.DefaultSettings.EnableDisabledOptionsCount)
                    ? $"{currentCount}. " : "";

                if (option.Disabled)
                {
                    string disabledText = "  " + prefix + numberPart + option.Text;
                    maxLength = Math.Max(maxLength, disabledText.Length);
                }
                else
                {
                    string text = prefix + numberPart + option.Text;
                    maxLength = Math.Max(maxLength, text.Length);
                }
            }

            maxLength = Math.Max(maxLength, $"7. {_config.Translations.BackButton}".Length);
            maxLength = Math.Max(maxLength, $"8. {_config.Translations.NextButton}".Length);
            maxLength = Math.Max(maxLength, $"9. {_config.Translations.ExitButton}".Length);

            string titlePadding = new string('\u2800', maxLength - _menu.Title.Length + 2);

            if (_config.DefaultSettings.Spacing)
            {
                enabledBuilder.AppendLine("\u2800");
                disabledBuilder.AppendLine("\u2800");
            }

            enabledBuilder.AppendLine(_menu.Title + titlePadding);
            enabledBuilder.AppendLine("");

            disabledBuilder.AppendLine("");
            disabledBuilder.AppendLine("");

            for (int i = 0; i < selectable; i++)
            {
                var option = _menu.MenuOptions[currentOffset + i];
                string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == i)
                    ? _config.Translations.SelectPrefix : "";

                int currentCount = i + 1;
                string numberPart = (_config.DefaultSettings.EnableOptionsCount || _config.DefaultSettings.EnableDisabledOptionsCount)
                    ? $"{currentCount}. " : "";

                if (!option.Disabled)
                {
                    string baseText = prefix + numberPart + option.Text;
                    enabledBuilder.AppendLine(baseText);
                    disabledBuilder.AppendLine("");
                }
                else
                {
                    string baseText = "  " + prefix + numberPart + option.Text;
                    enabledBuilder.AppendLine("");
                    disabledBuilder.AppendLine(baseText);
                }
            }

            enabledBuilder.AppendLine("");
            disabledBuilder.AppendLine("");

            if (CurrentPage == 0)
            {
                if (_menu.IsSubMenu)
                {
                    string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable)
                        ? _config.Translations.SelectPrefix : "";
                    enabledBuilder.AppendLine($"{prefix}7. {_config.Translations.BackButton}");
                    disabledBuilder.AppendLine("");

                    if (_menu.MenuOptions.Count > NUM_PER_PAGE)
                    {
                        prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + 1)
                            ? _config.Translations.SelectPrefix : "";
                        enabledBuilder.AppendLine($"{prefix}8. {_config.Translations.NextButton}");
                        disabledBuilder.AppendLine("");
                    }

                    if (_menu.HasExitOption)
                    {
                        prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + (_menu.MenuOptions.Count > NUM_PER_PAGE ? 2 : 1))
                            ? _config.Translations.SelectPrefix : "";
                        enabledBuilder.AppendLine($"{prefix}9. {_config.Translations.ExitButton}");
                        disabledBuilder.AppendLine("");
                    }
                }
                else
                {
                    if (_menu.MenuOptions.Count > NUM_PER_PAGE)
                    {
                        string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable)
                            ? _config.Translations.SelectPrefix : "";
                        enabledBuilder.AppendLine($"{prefix}8. {_config.Translations.NextButton}");
                        disabledBuilder.AppendLine("");
                    }

                    if (_menu.HasExitOption)
                    {
                        string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + (_menu.MenuOptions.Count > NUM_PER_PAGE ? 1 : 0))
                            ? _config.Translations.SelectPrefix : "";
                        enabledBuilder.AppendLine($"{prefix}9. {_config.Translations.ExitButton}");
                        disabledBuilder.AppendLine("");
                    }
                }
            }
            else
            {
                string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable)
                    ? _config.Translations.SelectPrefix : "";
                enabledBuilder.AppendLine($"{prefix}7. {_config.Translations.BackButton}");
                disabledBuilder.AppendLine("");

                if ((_menu.MenuOptions.Count - CurrentPage * NUM_PER_PAGE) > NUM_PER_PAGE)
                {
                    prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + 1)
                        ? _config.Translations.SelectPrefix : "";
                    enabledBuilder.AppendLine($"{prefix}8. {_config.Translations.NextButton}");
                    disabledBuilder.AppendLine("");
                }

                if (_menu.HasExitOption)
                {
                    int navOffset = (_menu.MenuOptions.Count - CurrentPage * NUM_PER_PAGE) > NUM_PER_PAGE ? 2 : 1;
                    prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + navOffset)
                        ? _config.Translations.SelectPrefix : "";
                    enabledBuilder.AppendLine($"{prefix}9. {_config.Translations.ExitButton}");
                    disabledBuilder.AppendLine("");
                }
            }

            // Control info
            if (_menu.MenuType == MenuType.Both || _menu.MenuType == MenuType.Scrollable)
            {
                enabledBuilder.AppendLine("");
                enabledBuilder.AppendLine(_config.Translations.ScrollInfo);
                enabledBuilder.AppendLine(_config.Translations.SelectInfo);

                disabledBuilder.AppendLine("");
                disabledBuilder.AppendLine("");
                disabledBuilder.AppendLine("");
            }

            if (_config.DefaultSettings.Spacing)
            {
                enabledBuilder.AppendLine("\u2800");
                disabledBuilder.AppendLine("\u2800");
            }
        }
        private void BuildNavigationOptions(StringBuilder enabledBuilder, StringBuilder disabledBuilder, int selectable)
        {
            if (CurrentPage == 0)
            {
                if (_menu.IsSubMenu)
                {
                    BuildSubMenuNavigation(enabledBuilder, disabledBuilder, selectable);
                }
                else
                {
                    BuildMainMenuNavigation(enabledBuilder, disabledBuilder, selectable);
                }
            }
            else
            {
                BuildPaginationNavigation(enabledBuilder, disabledBuilder, selectable);
            }
        }
        private void BuildSubMenuNavigation(StringBuilder enabledBuilder, StringBuilder disabledBuilder, int selectable)
        {
            string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + 0)
                ? _config.Translations.SelectPrefix : "";
            enabledBuilder.AppendLine($"{prefix}7. {_config.Translations.BackButton}");
            disabledBuilder.AppendLine("\u200B");

            if (_menu.MenuOptions.Count > NUM_PER_PAGE)
            {
                prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + 1)
                    ? _config.Translations.SelectPrefix : "";
                enabledBuilder.AppendLine($"{prefix}8. {_config.Translations.NextButton}");
                disabledBuilder.AppendLine("\u200B");
            }

            if (_menu.HasExitOption)
            {
                int expectedIndex = selectable + (_menu.MenuOptions.Count > NUM_PER_PAGE ? 2 : 1);
                prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == expectedIndex)
                    ? _config.Translations.SelectPrefix : "";
                enabledBuilder.AppendLine($"{prefix}9. {_config.Translations.ExitButton}");
                disabledBuilder.AppendLine("\u200B");
            }
        }

        private void BuildMainMenuNavigation(StringBuilder enabledBuilder, StringBuilder disabledBuilder, int selectable)
        {
            if (_menu.MenuOptions.Count > NUM_PER_PAGE)
            {
                string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable)
                    ? _config.Translations.SelectPrefix : "";
                enabledBuilder.AppendLine($"{prefix}8. {_config.Translations.NextButton}");
                disabledBuilder.AppendLine("\u200B");
            }

            if (_menu.HasExitOption)
            {
                string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + (_menu.MenuOptions.Count > NUM_PER_PAGE ? 1 : 0))
                    ? _config.Translations.SelectPrefix : "";
                enabledBuilder.AppendLine($"{prefix}9. {_config.Translations.ExitButton}");
                disabledBuilder.AppendLine("\u200B");
            }
        }

        private void BuildPaginationNavigation(StringBuilder enabledBuilder, StringBuilder disabledBuilder, int selectable)
        {
            string prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + 0)
                ? _config.Translations.SelectPrefix : "";
            enabledBuilder.AppendLine($"{prefix}7. {_config.Translations.BackButton}");
            disabledBuilder.AppendLine("\u200B");

            if ((_menu.MenuOptions.Count - CurrentPage * NUM_PER_PAGE) > NUM_PER_PAGE)
            {
                prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + 1)
                    ? _config.Translations.SelectPrefix : "";
                enabledBuilder.AppendLine($"{prefix}8. {_config.Translations.NextButton}");
                disabledBuilder.AppendLine("\u200B");
            }

            if (_menu.HasExitOption)
            {
                int navOffset = (_menu.MenuOptions.Count - CurrentPage * NUM_PER_PAGE) > NUM_PER_PAGE ? 2 : 1;
                prefix = (_menu.MenuType != MenuType.KeyPress && CurrentSelection == selectable + navOffset)
                    ? _config.Translations.SelectPrefix : "";
                enabledBuilder.AppendLine($"{prefix}9. {_config.Translations.ExitButton}");
                disabledBuilder.AppendLine("\u200B");
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
            return selectable + navCount;
        }

        private void HandleSelection()
        {
            int currentOffset = CurrentPage * NUM_PER_PAGE;
            int selectable = Math.Min(NUM_PER_PAGE, _menu.MenuOptions.Count - currentOffset);

            if (CurrentSelection < selectable)
            {
                int optionIndex = currentOffset + CurrentSelection;
                if (optionIndex < _menu.MenuOptions.Count)
                {
                    var option = _menu.MenuOptions[optionIndex];
                    if (!option.Disabled)
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
                        if (_menu.ParentMenu != null)
                        {
                            Close();
                            if (!string.IsNullOrEmpty(_config.Sounds.Exit))
                            {
                                _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                            }
                            MenuAPI.OpenMenu(_plugin, _player, _menu.ParentMenu);
                        }
                        else
                        {
                            Close();
                            if (!string.IsNullOrEmpty(_config.Sounds.Exit))
                            {
                                _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                            }
                        }
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
                            Close();
                            if (!string.IsNullOrEmpty(_config.Sounds.Exit))
                            {
                                _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                            }
                        }
                    }
                    else if (navIndex == 2 && _menu.MenuOptions.Count > NUM_PER_PAGE && _menu.HasExitOption)
                    {
                        Close();
                        if (!string.IsNullOrEmpty(_config.Sounds.Exit))
                        {
                            _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                        }
                    }
                }
                else
                {
                    int offset = selectable;
                    if (_menu.MenuOptions.Count > NUM_PER_PAGE)
                    {
                        if (navIndex == 0)
                        {
                            int newPage = CurrentPage + 1;
                            int newSelectable = Math.Min(NUM_PER_PAGE, _menu.MenuOptions.Count - (newPage * NUM_PER_PAGE));
                            int desiredSelection = newSelectable + 1;
                            NextPage(desiredSelection);
                        }
                        else if (navIndex == 1 && _menu.HasExitOption)
                        {
                            Close();
                            if (!string.IsNullOrEmpty(_config.Sounds.Exit))
                            {
                                _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                            }
                        }
                    }
                    else
                    {
                        if (_menu.HasExitOption && navIndex == 0)
                        {
                            Close();
                            if (!string.IsNullOrEmpty(_config.Sounds.Exit))
                            {
                                _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                            }
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
                        Close();
                        if (!string.IsNullOrEmpty(_config.Sounds.Exit))
                        {
                            _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                        }
                    }
                }
                else if (navIndex == 2 && (_menu.MenuOptions.Count - CurrentPage * NUM_PER_PAGE) > NUM_PER_PAGE && _menu.HasExitOption)
                {
                    Close();
                    if (!string.IsNullOrEmpty(_config.Sounds.Exit))
                    {
                        _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                    }
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
            if (_menu.MenuType == MenuType.Scrollable)
                return;

            if (player.Handle != _player.Handle)
                return;

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
                        Close();
                        _player.ExecuteClientCommand($"play {_config.Sounds.Exit}");
                        MenuAPI.OpenMenu(_plugin, _player, _menu.ParentMenu);
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
            var displayToOptionMap = new Dictionary<int, int>();
            for (int i = 0, displayNum = 1; i < totalDisplayed; i++, displayNum++)
            {
                var option = _menu.MenuOptions[currentOffset + i];
                if (!option.Disabled)
                {
                    displayToOptionMap[displayNum] = i;
                }
            }

            if (displayToOptionMap.ContainsKey(key))
            {
                int optionIndex = displayToOptionMap[key];
                var option = _menu.MenuOptions[currentOffset + optionIndex];
                _player.ExecuteClientCommand($"play {_config.Sounds.Select}");
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

        public void Close()
        {
            if (_menuEntities != null)
            {
                _player.Unfreeze();
                if (_menuEntities.EnabledOptions != null && _menuEntities.EnabledOptions.IsValid)
                {
                    _menuEntities.EnabledOptions.Enabled = false;
                    _menuEntities.EnabledOptions.AcceptInput("Kill", _menuEntities.EnabledOptions);
                }
                if (_menuEntities.DisabledOptions != null && _menuEntities.DisabledOptions.IsValid)
                {
                    _menuEntities.DisabledOptions.Enabled = false;
                    _menuEntities.DisabledOptions.AcceptInput("Kill", _menuEntities.DisabledOptions);
                }
            }

            MenuAPI.RemoveActiveMenu(_player);
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
            _plugin.RemoveListener<Listeners.OnTick>(_onTickDelegate);
            _plugin.RemoveListener<Listeners.CheckTransmit>(_checkTransmitDelegate);
            _plugin.RemoveListener<Listeners.OnEntityDeleted>(_onEntityDeletedDelegate);
        }

        private void RecreateHud()
        {
            _hudText = null;
            Display();
        }
    }
}