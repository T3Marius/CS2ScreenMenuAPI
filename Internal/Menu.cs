using System.Drawing;
using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using static CS2ScreenMenuAPI.Buttons;
using static CS2ScreenMenuAPI.PlayerRes;
using static CounterStrikeSharp.API.Core.Listeners;

namespace CS2ScreenMenuAPI
{
    public class Menu : IMenu, IDisposable
    {
        private readonly CCSPlayerController _player;
        private readonly BasePlugin _plugin;
        private readonly Dictionary<string, CommandInfo.CommandCallback> _registeredKeyCommands = new();
        private CPointWorldText? _menuText;
        private CPointWorldText? _menuBackgroundText;
        private bool _isClosed;
        private bool _isResolutionMenuShown = false;

        private PlayerButtons g_OldButtons;
        private int _currentSelectionIndex = 0;
        private string ScrollUpKey = "W";
        private string ScrollDownKey = "S";
        private string SelectKey = "E";
        private CCSGOViewModel? _oldViewModel;

        public Menu? PrevMenu { get; set; } = null;
        public Menu? ParentMenu
        {
            get => PrevMenu;
            set => PrevMenu = value;
        }
        public Config _config { get; set; } = new Config();
        public string Title { get; set; } = string.Empty;
        public List<IMenuOption> Options { get; } = new();
        public int CurrentPage { get; private set; }
        public bool ShowResolutionOption { get; set; }
        public int ItemsPerPage { get; } = 6;
        public Color TitleColor { get; set; }
        public Color BackgroundColor { get; set; }
        public string FontName { get; set; } = string.Empty;
        public PostSelect PostSelect = PostSelect.Nothing;
        public MenuType MenuType = MenuType.KeyPress;
        public bool HasExitButon { get; set; }
        public bool ShowPageCount { get; set; }
        public bool ShowControlsInfo { get; set; }
        public bool ShowDisabledOptionNum { get; set; }
        public bool IsSubMenu { get; set; } = false;
        public Color OptionColor { get; set; }
        public Color DisabledColor { get; set; }
        public float MenuPositionX { get; set; }
        public int Size { get; set; }

        public Menu(CCSPlayerController player, BasePlugin plugin)
        {
            _player = player;
            _plugin = plugin;
            _config = ConfigLoader.Load();
            ConfigureSettings();
            RegisterEvents();

            if (HasPlayerResolution(player))
            {
                Resolution playerRes = GetPlayerResolution(player);
                MenuPositionX = playerRes.PositionX;
                MenuAPI.SetActiveMenu(player, this);
                RegisterKeyCommands();
                Display();
            }
            else if (_config.Settings.Resolutions.Count > 0)
            {
                _isClosed = true;
                _isResolutionMenuShown = true;

                CreateResolutionMenu(_player, _plugin, () =>
                {
                    Resolution playerRes = GetPlayerResolution(_player);
                    MenuPositionX = playerRes.PositionX;
                    _isClosed = false;
                    _isResolutionMenuShown = false;
                    RegisterKeyCommands();
                    MenuAPI.SetActiveMenu(_player, this);
                    Display();
                });
            }
            else
            {
                MenuPositionX = _config.Settings.PositionX;
                MenuAPI.SetActiveMenu(player, this);
                RegisterKeyCommands();
                Display();
            }
        }

        private void RegisterKeyCommands()
        {
            for (int i = 0; i <= 9; i++)
            {
                string commandName = $"css_{i}";
                int key = i;

                CommandInfo.CommandCallback callback = (player, info) =>
                {
                    if (player == null || !player.IsValid || player != _player || _isClosed) return;
                    if (MenuAPI.GetActiveMenu(player) != this) return;

                    OnKeyPress(player, key);
                };

                _plugin.AddCommand(commandName, "Handles menu navigation", callback);
                _registeredKeyCommands[commandName] = callback;
            }
        }

        public void AddItem(string text, Action<CCSPlayerController, IMenuOption> callback, bool disabled = false)
        {
            Options.Add(new MenuOption
            {
                Text = text,
                Callback = callback,
                IsDisabled = disabled
            });
        }

        public void Refresh() => UpdateMenuContent();

        public void Display()
        {

            if (MenuType != MenuType.KeyPress && _config.Settings.FreezePlayer)
            {
                _player.Freeze();
            }

            if (_menuText == null || _menuBackgroundText == null)
            {
                ClearDisplay();
                _player.CreateFakeWorldText(this);
                Server.NextFrame(() => CreateDisplay(_player));
            }
            else
            {
                UpdateMenuContent();
            }
        }

        private void UpdateMenuContent()
        {
            if (_menuText == null || _menuBackgroundText == null)
            {
                ClearDisplay();
                _player.CreateFakeWorldText(this);
                Server.NextFrame(() => CreateDisplay(_player));
                return;
            }

            StringBuilder menuContent = new();
            StringBuilder menuBackground = new();
            BuildMenuText(menuContent, menuBackground);

            try
            {
                _menuText.MessageText = menuContent.ToString();
                _menuBackgroundText.MessageText = menuBackground.ToString();

                Utilities.SetStateChanged(_menuText, "CPointWorldText", "m_messageText");
                Utilities.SetStateChanged(_menuBackgroundText, "CPointWorldText", "m_messageText");
            }
            catch (Exception)
            {
                ClearDisplay();
                _player.CreateFakeWorldText(this);
                Server.NextFrame(() => CreateDisplay(_player));
            }
        }

        private void CreateDisplay(CCSPlayerController? player = null)
        {
            player ??= _player;

            StringBuilder menuContent = new();
            StringBuilder menuBackground = new();
            BuildMenuText(menuContent, menuBackground);

            var vectorData = DisplayManager.FindVectorData(player, MenuPositionX);
            if (!vectorData.HasValue) return;

            CCSGOViewModel? viewModel = DisplayManager.EnsureCustomView(player);
            if (viewModel == null) return;

            _oldViewModel = viewModel;

            if (_menuText == null || !_menuText.IsValid)
            {
                _menuText = DisplayManager.CreateWorldText(
                    menuContent.ToString(),
                    Size,
                    OptionColor,
                    FontName,
                    false,
                    BackgroundColor,
                    0,
                    vectorData.Value.Position,
                    vectorData.Value.Angle,
                    viewModel
                );
            }
            else
            {
                _menuText.MessageText = menuContent.ToString();
                _menuText.Teleport(vectorData.Value.Position, vectorData.Value.Angle, null);
                _menuText.AcceptInput("SetParent", viewModel, null, "!activator");
                Utilities.SetStateChanged(_menuText, "CPointWorldText", "m_messageText");
            }

            if (_menuBackgroundText == null || !_menuBackgroundText.IsValid)
            {
                _menuBackgroundText = DisplayManager.CreateWorldText(
                    menuBackground.ToString(),
                    Size,
                    DisabledColor,
                    FontName,
                    true,
                    BackgroundColor,
                    -0.001f,
                    vectorData.Value.Position,
                    vectorData.Value.Angle,
                    viewModel
                );
            }
            else
            {
                _menuBackgroundText.MessageText = menuBackground.ToString();
                _menuBackgroundText.Teleport(vectorData.Value.Position, vectorData.Value.Angle, null);
                _menuBackgroundText.AcceptInput("SetParent", viewModel, null, "!activator");
                Utilities.SetStateChanged(_menuBackgroundText, "CPointWorldText", "m_messageText");
            }
        }
        private void BuildMenuText(StringBuilder menuContent, StringBuilder menuBackground)
        {
            int totalPages = (int)Math.Ceiling(Options.Count / (double)ItemsPerPage);
            bool showBackButton = CurrentPage > 0 || (IsSubMenu && PrevMenu != null);
            bool showNextButton = CurrentPage < totalPages - 1;
            bool hasControlsInfo = MenuType != MenuType.KeyPress && ShowControlsInfo;
            string prefix = _config.Settings.ScrollPrefix;

            string displayTitle = ShowPageCount
                ? $"{Title}:    ({CurrentPage + 1}/{totalPages})"
                : Title + ":";

            menuContent.AppendLine();
            menuBackground.AppendLine(displayTitle);

            int startIndex = CurrentPage * ItemsPerPage;
            int endIndex = Math.Min(Options.Count, startIndex + ItemsPerPage);

            int maxTextLength = displayTitle.Length;

            int enabledOptionCount = 0;
            for (int i = startIndex; i < endIndex; i++)
            {
                if (!Options[i].IsDisabled)
                    enabledOptionCount++;
            }

            int visibleOptionNumber = 1;
            int enabledOptionCounter = 0;
            for (int i = startIndex; i < endIndex; i++)
            {
                var option = Options[i];
                string optionText;
                bool isSelected = false;

                if (!option.IsDisabled)
                {
                    isSelected = (MenuType == MenuType.Scrollable || MenuType == MenuType.Both) &&
                                 enabledOptionCounter == _currentSelectionIndex;

                    if (isSelected)
                        optionText = $"{prefix} {visibleOptionNumber}. {option.Text}";
                    else
                        optionText = $"{visibleOptionNumber}. {option.Text}";

                    enabledOptionCounter++;
                    visibleOptionNumber++;
                }
                else if (ShowDisabledOptionNum)
                {
                    optionText = $"{visibleOptionNumber}. {option.Text}";
                    visibleOptionNumber++;
                }
                else
                {
                    optionText = option.Text;
                }

                maxTextLength = Math.Max(maxTextLength + 1, optionText.Length);

                if (option.IsDisabled)
                {
                    menuContent.AppendLine();
                    menuBackground.AppendLine(optionText);
                }
                else
                {
                    menuContent.AppendLine("  " + optionText);
                    menuBackground.AppendLine();
                }
            }

            menuContent.AppendLine();
            menuBackground.AppendLine();

            int navigationIndex = enabledOptionCounter;

            if (showBackButton)
            {
                bool isSelected = (MenuType == MenuType.Scrollable || MenuType == MenuType.Both) &&
                                 _currentSelectionIndex == navigationIndex;

                string backText = isSelected ?
                    $"{prefix} 7. {_player.Localizer("Prev")}" :
                    $"7. {_player.Localizer("Prev")}";

                menuContent.AppendLine("  " + backText);
                menuBackground.AppendLine();
                navigationIndex++;
            }

            if (showNextButton)
            {
                bool isSelected = (MenuType == MenuType.Scrollable || MenuType == MenuType.Both) &&
                                 _currentSelectionIndex == navigationIndex;

                string nextText = isSelected ?
                    $"{prefix} 8. {_player.Localizer("Next")}" :
                    $"8. {_player.Localizer("Next")}";

                menuContent.AppendLine("  " + nextText);
                menuBackground.AppendLine();
                navigationIndex++;
            }

            if (HasExitButon)
            {
                bool isSelected = (MenuType == MenuType.Scrollable || MenuType == MenuType.Both) &&
                                 _currentSelectionIndex == navigationIndex;

                string closeText = isSelected ?
                    $"{prefix} 9. {_player.Localizer("Close")}" :
                    $"9. {_player.Localizer("Close")}";

                menuContent.AppendLine("  " + closeText);
                menuBackground.AppendLine();
                navigationIndex++;
            }

            if (hasControlsInfo)
            {
                menuContent.AppendLine();
                menuContent.AppendLine();
                menuBackground.AppendLine(_player.Localizer("ScrollKeys", ScrollUpKey, ScrollDownKey));
                menuBackground.AppendLine(_player.Localizer("SelectKey", SelectKey));
            }

            for (int i = 0; i < maxTextLength; i++)
            {
                menuBackground.Append('ᅠ');
            }
        }
        private void TransitionToPrevMenu()
        {
            if (PrevMenu == null) return;

            PlayPrevSound();

            Menu prevMenu = PrevMenu;

            MenuAPI.SetActiveMenu(_player, null);

            Server.NextFrame(() => {
                MenuAPI.SetActiveMenu(_player, prevMenu);
                prevMenu._isClosed = false;
                prevMenu.RegisterKeyCommands();
                prevMenu.Display();

                _isClosed = true;
                ClearDisplay();
            });
        }

        public void OnKeyPress(CCSPlayerController player, int key)
        {
            if (_isClosed) return;

            if (MenuType != MenuType.Scrollable)
            {
                switch (key)
                {
                    case 0:
                        if (ShowResolutionOption)
                        {
                            Menu currentMenu = this;
                            Menu? prevMenu = PrevMenu;
                            bool isSubMenu = IsSubMenu;

                            MenuAPI.SetActiveMenu(_player, null);
                            _isClosed = true;
                            PlayCloseSound();

                            CreateResolutionMenu(player, _plugin, () =>
                            {
                                if (isSubMenu && prevMenu != null)
                                {
                                    prevMenu._isClosed = false;
                                    MenuAPI.SetActiveMenu(_player, prevMenu);
                                    prevMenu.RegisterKeyCommands();
                                    prevMenu.Display();
                                }
                                else
                                {
                                    currentMenu._isClosed = false;
                                    MenuAPI.SetActiveMenu(_player, currentMenu);
                                    currentMenu.RegisterKeyCommands();
                                    currentMenu.Display();
                                }
                            });
                        }
                        break;
                    case 7:
                        if (IsSubMenu && CurrentPage == 0 && PrevMenu != null)
                        {
                            TransitionToPrevMenu();
                        }
                        else
                        {
                            PrevPage();
                        }
                        break;

                    case 8:
                        NextPage();
                        break;

                    case 9:
                        if (HasExitButon)
                        {
                            Close(player);
                            PlayCloseSound();
                        }
                        break;

                    default:
                        if (key >= 1 && key <= 6)
                        {
                            HandleOptionSelection(player, key);
                        }
                        break;
                }
            }
        }

        private void SelectCurrentOption()
        {
            int enabledOptionsCount = GetEnabledOptionsCountOnCurrentPage();

            if (_currentSelectionIndex < enabledOptionsCount)
            {
                int startIndex = CurrentPage * ItemsPerPage;
                int enabledIndex = 0;

                for (int i = startIndex; i < Math.Min(Options.Count, startIndex + ItemsPerPage); i++)
                {
                    if (!Options[i].IsDisabled)
                    {
                        if (enabledIndex == _currentSelectionIndex)
                        {
                            var option = Options[i];

                            Menu? subMenu = null;
                            if (option is MenuOption menuOption)
                            {
                                subMenu = menuOption.SubMenu;
                            }

                            if (subMenu != null)
                            {
                                PlaySelectSound();

                                subMenu.PrevMenu = this;
                                subMenu.IsSubMenu = true;

                                MenuAPI.SetActiveMenu(_player, null);

                                Server.NextFrame(() => {
                                    PlaySelectSound();
                                    MenuAPI.SetActiveMenu(_player, subMenu);
                                    subMenu._isClosed = false;
                                    subMenu.RegisterKeyCommands();
                                    subMenu.Display();
                                });
                            }
                            else
                            {
                                option.Callback(_player, option);

                                if (_isResolutionMenuShown)
                                    return;

                                switch (PostSelect)
                                {
                                    case PostSelect.Close:
                                        Close(_player);
                                        PlayCloseSound();
                                        break;
                                    case PostSelect.Reset:
                                        Refresh();
                                        PlaySelectSound();
                                        break;
                                    case PostSelect.Nothing:
                                        PlaySelectSound();
                                        break;
                                }
                            }

                            return;
                        }
                        enabledIndex++;
                    }
                }
            }
            else
            {
                int navIndex = _currentSelectionIndex - enabledOptionsCount;
                int navCount = 0;

                bool showBackButton = CurrentPage > 0 || (IsSubMenu && PrevMenu != null);
                bool showNextButton = CurrentPage < GetMaxPage();

                if (showBackButton && navCount == navIndex)
                {
                    if (IsSubMenu && CurrentPage == 0 && PrevMenu != null)
                    {
                        TransitionToPrevMenu();
                    }
                    else
                    {
                        PrevPage();
                    }
                    return;
                }
                navCount += showBackButton ? 1 : 0;

                if (showNextButton && navCount == navIndex)
                {
                    NextPage();
                    return;
                }
                navCount += showNextButton ? 1 : 0;

                if (HasExitButon && navCount == navIndex)
                {
                    Close(_player);
                    PlayCloseSound();
                    return;
                }
                navCount += HasExitButon ? 1 : 0;

                if (ShowResolutionOption && navCount == navIndex)
                {
                    Menu currentMenu = this;
                    Menu? prevMenu = PrevMenu;
                    bool isSubMenu = IsSubMenu;

                    MenuAPI.SetActiveMenu(_player, null);
                    _isClosed = true;
                    PlayCloseSound();

                    CreateResolutionMenu(_player, _plugin, () =>
                    {
                        if (isSubMenu && prevMenu != null)
                        {
                            prevMenu._isClosed = false;
                            MenuAPI.SetActiveMenu(_player, prevMenu);
                            prevMenu.RegisterKeyCommands();
                            prevMenu.Display();
                        }
                        else
                        {
                            currentMenu._isClosed = false;
                            MenuAPI.SetActiveMenu(_player, currentMenu);
                            currentMenu.RegisterKeyCommands();
                            currentMenu.Display();
                        }
                    });
                }
            }
        }

        private void HandleOptionSelection(CCSPlayerController player, int key)
        {
            int startIndex = CurrentPage * ItemsPerPage;
            int endIndex = Math.Min(Options.Count, startIndex + ItemsPerPage);
            Dictionary<int, int> displayedNumberToOptionIndex = new Dictionary<int, int>();

            int displayedNumber = 1;

            for (int i = startIndex; i < endIndex; i++)
            {
                var option = Options[i];

                if (!option.IsDisabled)
                {
                    displayedNumberToOptionIndex[displayedNumber++] = i;
                }
                else if (ShowDisabledOptionNum)
                {
                    displayedNumber++;
                }
            }

            if (displayedNumberToOptionIndex.ContainsKey(key))
            {
                int optionIndex = displayedNumberToOptionIndex[key];
                var option = Options[optionIndex];
                Menu? subMenu = null;
                if (option is MenuOption menuOption)
                {
                    subMenu = menuOption.SubMenu;
                }
                if (subMenu != null)
                {
                    PlaySelectSound();
                    subMenu.PrevMenu = this;
                    subMenu.IsSubMenu = true;
                    MenuAPI.SetActiveMenu(_player, null);
                    Server.NextFrame(() => {
                        PlaySelectSound();
                        MenuAPI.SetActiveMenu(_player, subMenu);
                        subMenu._isClosed = false;
                        subMenu.RegisterKeyCommands();
                        subMenu.Display();
                    });
                }
                else
                {
                    option.Callback(player, option);
                    if (_isResolutionMenuShown)
                        return;
                    switch (PostSelect)
                    {
                        case PostSelect.Close:
                            Close(player);
                            PlayCloseSound();
                            break;
                        case PostSelect.Reset:
                            Refresh();
                            PlaySelectSound();
                            break;
                        case PostSelect.Nothing:
                            PlaySelectSound();
                            break;
                    }
                }
            }
        }

        private void ScrollDown()
        {
            int enabledOptionsCount = GetEnabledOptionsCountOnCurrentPage();
            int totalSelectableItems = enabledOptionsCount + GetNavigationButtonCount();

            if (_currentSelectionIndex >= totalSelectableItems - 1)
            {
                _currentSelectionIndex = 0;

                int firstEnabled = FindFirstEnabledOptionIndex();
                if (firstEnabled >= 0)
                {
                    _currentSelectionIndex = firstEnabled;
                }

                UpdateMenuContent();
                return;
            }

            _currentSelectionIndex++;

            if (_currentSelectionIndex < enabledOptionsCount)
            {
                int startIndex = CurrentPage * ItemsPerPage;
                int targetEnabledIndex = _currentSelectionIndex;
                int currentEnabledIndex = 0;
                bool foundEnabled = false;

                for (int i = startIndex; i < Math.Min(Options.Count, startIndex + ItemsPerPage); i++)
                {
                    if (!Options[i].IsDisabled)
                    {
                        if (currentEnabledIndex == targetEnabledIndex)
                        {
                            foundEnabled = true;
                            break;
                        }
                        currentEnabledIndex++;
                    }
                }

                if (!foundEnabled)
                {
                    if (CurrentPage < GetMaxPage())
                    {
                        NextPage();
                        _currentSelectionIndex = 0;

                        int firstEnabled = FindFirstEnabledOptionIndex();
                        if (firstEnabled >= 0)
                        {
                            _currentSelectionIndex = firstEnabled;
                        }
                        return;
                    }
                    else
                    {
                        _currentSelectionIndex = enabledOptionsCount;
                    }
                }
            }

            UpdateMenuContent();
        }

        private void ScrollUp()
        {
            int enabledOptionsCount = GetEnabledOptionsCountOnCurrentPage();
            int totalSelectableItems = enabledOptionsCount + GetNavigationButtonCount();

            if (_currentSelectionIndex == 0)
            {
                _currentSelectionIndex = totalSelectableItems - 1;

                UpdateMenuContent();
                return;
            }

            _currentSelectionIndex--;

            if (_currentSelectionIndex < enabledOptionsCount)
            {
                int startIndex = CurrentPage * ItemsPerPage;
                int targetEnabledIndex = _currentSelectionIndex;
                int currentEnabledIndex = 0;
                bool foundEnabled = false;

                for (int i = startIndex; i < Math.Min(Options.Count, startIndex + ItemsPerPage); i++)
                {
                    if (!Options[i].IsDisabled)
                    {
                        if (currentEnabledIndex == targetEnabledIndex)
                        {
                            foundEnabled = true;
                            break;
                        }
                        currentEnabledIndex++;
                    }
                }

                if (!foundEnabled && CurrentPage > 0)
                {
                    PrevPage();
                    int newEnabledCount = GetEnabledOptionsCountOnCurrentPage();
                    _currentSelectionIndex = newEnabledCount - 1;
                    return;
                }
            }

            UpdateMenuContent();
        }

        public void NextPage()
        {
            int maxPage = GetMaxPage();
            if (CurrentPage < maxPage)
            {
                CurrentPage++;

                if (MenuType == MenuType.Scrollable || MenuType == MenuType.Both)
                {
                    _currentSelectionIndex = 0;

                    int firstEnabled = FindFirstEnabledOptionIndex();
                    if (firstEnabled >= 0)
                    {
                        _currentSelectionIndex = firstEnabled;
                    }
                }

                UpdateMenuContent();
                PlayNextSound();
            }
        }

        public void PrevPage()
        {
            if (CurrentPage > 0)
            {
                CurrentPage--;

                if (MenuType == MenuType.Scrollable || MenuType == MenuType.Both)
                {
                    int firstEnabled = FindFirstEnabledOptionIndex();
                    if (firstEnabled >= 0)
                    {
                        _currentSelectionIndex = firstEnabled;
                    }
                    else
                    {
                        _currentSelectionIndex = 0;
                    }
                }

                UpdateMenuContent();
                PlayPrevSound();
            }
        }

        private void ClearDisplay()
        {
            if (_menuText != null && _menuText.IsValid)
            {
                _menuText.Remove();
                _menuText = null;
            }

            if (_menuBackgroundText != null && _menuBackgroundText.IsValid)
            {
                _menuBackgroundText.Remove();
                _menuBackgroundText = null;
            }
        }

        public void Close(CCSPlayerController player)
        {
            if (_isClosed) return;

            _isClosed = true;
            ClearDisplay();
            UnregisterKeyCommands();
            MenuAPI.SetActiveMenu(player, null);

            if (MenuType != MenuType.KeyPress && _config.Settings.FreezePlayer)
            {
                player.Unfreeze();
            }
        }

        public void Dispose() => Close(_player);

        private void UnregisterKeyCommands()
        {
            foreach (var commandEntry in _registeredKeyCommands)
            {
                _plugin.RemoveCommand(commandEntry.Key, commandEntry.Value);
            }
            _registeredKeyCommands.Clear();
        }

        private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;
            if (player == null)
                return HookResult.Continue;

            if (MenuAPI.GetActiveMenu(player) == this)
            {
                Close(player);
            }

            if (DisplayManager.PlayerFakeTextCreated.ContainsKey(player.SteamID))
            {
                DisplayManager.PlayerFakeTextCreated.Remove(player.SteamID);
            }

            return HookResult.Continue;
        }

        private IntPtr _HudObserverID = IntPtr.Zero;
        private bool _hasStateChanged = false;
        private void OnTick()
        {
            if (MenuAPI.GetActiveMenu(_player) != this || _player.Pawn.Value == null)
            {
                if (!_isClosed)
                {
                    Close(_player);
                }
                return;
            }

            if (_isClosed)
            {
                Close(_player);
                return;
            }

            if (MenuType != MenuType.KeyPress)
            {
                PlayerButtons button = _player.Buttons;
                if (ButtonMapping.TryGetValue(ScrollUpKey, out PlayerButtons scrollUpButton) &&
                (button & scrollUpButton) == 0 && (g_OldButtons & scrollUpButton) != 0)
                {
                    ScrollUp();
                }
                else if (ButtonMapping.TryGetValue(ScrollDownKey, out PlayerButtons scrollDownButton) &&
                (button & scrollDownButton) == 0 && (g_OldButtons & scrollDownButton) != 0)
                {
                    ScrollDown();
                }
                else if (ButtonMapping.TryGetValue(SelectKey, out PlayerButtons selectButton) &&
                (button & selectButton) == 0 && (g_OldButtons & selectButton) != 0)
                {
                    SelectCurrentOption();
                }

                g_OldButtons = button;
            }

            bool isCurrentlySpectating = _player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_DEAD;

            if (_hasStateChanged != isCurrentlySpectating)
            {
                _hasStateChanged = isCurrentlySpectating;

                _oldViewModel = null;
                _HudObserverID = IntPtr.Zero;

                if (DisplayManager.PlayerFakeTextCreated.ContainsKey(_player.SteamID))
                {
                    DisplayManager.PlayerFakeTextCreated[_player.SteamID] = false;
                }
                Server.NextFrame(() => {
                    ClearDisplay();
                    _player.CreateFakeWorldText(this);
                    Server.NextFrame(() => CreateDisplay(_player));
                });

                return;
            }

            if (isCurrentlySpectating)
            {
                var observerServices = _player.Pawn.Value?.ObserverServices;
                if (observerServices != null)
                {
                    var currentObserverPawn = observerServices.ObserverTarget?.Value?.As<CCSPlayerPawn>();
                    var currentObserverId = currentObserverPawn?.Handle ?? IntPtr.Zero;

                    if (currentObserverId != _HudObserverID && currentObserverId != IntPtr.Zero)
                    {
                        _HudObserverID = currentObserverId;
                        Server.NextFrame(() => Display());
                    }
                }
            }
        }

        private void OnCheckTransmit(CCheckTransmitInfoList infoList)
        {
            if (_menuText == null && _menuBackgroundText == null) return;

            foreach ((CCheckTransmitInfo info, CCSPlayerController? player) in infoList)
            {
                if (player == null || player == _player)
                    continue;

                if (_menuText != null && _menuText.IsValid)
                    info.TransmitEntities.Remove(_menuText);

                if (_menuBackgroundText != null && _menuBackgroundText.IsValid)
                    info.TransmitEntities.Remove(_menuBackgroundText);
            }
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            if (MenuAPI.GetActiveMenu(_player) != this)
                return HookResult.Continue;

            if (MenuType != MenuType.KeyPress && _config.Settings.FreezePlayer)
            {
                _player.Freeze();
            }
            
            Server.NextFrame(() => {
                if (_menuText == null || !_menuText.IsValid || _menuBackgroundText == null || !_menuBackgroundText.IsValid)
                {
                    ClearDisplay();
                    _player.CreateFakeWorldText(this);
                    Server.NextFrame(() => CreateDisplay(_player));
                }
                else
                {
                    try
                    {
                        UpdateMenuContent();
                    }
                    catch (Exception)
                    {
                        ClearDisplay();
                        _player.CreateFakeWorldText(this);
                        Server.NextFrame(() => CreateDisplay(_player));
                    }
                }
            });

            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            if (MenuAPI.GetActiveMenu(_player) != this)
                return HookResult.Continue;

            if (MenuType != MenuType.KeyPress && _config.Settings.FreezePlayer)
            {
                _player.Freeze();
            }

            Server.NextFrame(() => {
                if (_menuText == null || _menuBackgroundText == null)
                {
                    ClearDisplay();
                    _player.CreateFakeWorldText(this);
                    Server.NextFrame(() => CreateDisplay(_player));
                }
                else
                {
                    try
                    {
                        UpdateMenuContent();
                    }
                    catch (Exception)
                    {
                        ClearDisplay();
                        _player.CreateFakeWorldText(this);
                        Server.NextFrame(() => CreateDisplay(_player));
                    }
                }
            });

            return HookResult.Continue;
        }

        private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;

            if (player != null)
            {
                player.CreateFakeWorldText(this);
            }
            return HookResult.Continue;
        }
        
        private void RegisterEvents()
        {
            _plugin.RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
            _plugin.RegisterEventHandler<EventRoundStart>(OnRoundStart, HookMode.Post);
            _plugin.RegisterEventHandler<EventRoundEnd>(OnRoundEnd, HookMode.Post);
            _plugin.RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull, HookMode.Post);

            _plugin.RegisterListener<OnTick>(OnTick);
            _plugin.RegisterListener<CheckTransmit>(OnCheckTransmit);
        }

        private void ConfigureSettings()
        {
            TitleColor = Color.FromArgb(231, 210, 177);
            OptionColor = Color.FromArgb(232, 155, 27);
            DisabledColor = Color.FromArgb(231, 210, 177);
            Size = _config.Settings.Size;
            BackgroundColor = Color.FromArgb(120, 60, 55, 45);
            FontName = _config.Settings.FontName;
            HasExitButon = _config.Settings.HasExitOption;
            ShowResolutionOption = _config.Settings.ShowResolutionOption;
            ShowPageCount = _config.Settings.ShowPageCount;
            ShowDisabledOptionNum = _config.Settings.ShowDisabledOptionNum; ;
            ShowControlsInfo = _config.Settings.ShowControlsInfo;

            switch (_config.Settings.MenuType)
            {
                case "KeyPress":
                    MenuType = MenuType.KeyPress;
                    break;
                case "Scrollable":
                    MenuType = MenuType.Scrollable;
                    break;
                case "Both":
                    MenuType = MenuType.Both;
                    break;
                default:
                    MenuType = MenuType.KeyPress;
                    break;
            }

            if (MenuType != MenuType.KeyPress)
            {
                ScrollUpKey = _config.Controls.ScrollUp;
                ScrollDownKey = _config.Controls.ScrollDown;
                SelectKey = _config.Controls.Select;
                _currentSelectionIndex = 0;
            }
        }

        private int GetEnabledOptionsCountOnCurrentPage()
        {
            int startIndex = CurrentPage * ItemsPerPage;
            int endIndex = Math.Min(Options.Count, startIndex + ItemsPerPage);
            int count = 0;

            for (int i = startIndex; i < endIndex; i++)
            {
                if (!Options[i].IsDisabled)
                    count++;
            }

            return count;
        }

        private void PlaySelectSound()
        {
            if (!string.IsNullOrEmpty(_config.Sounds.Select))
            {
                RecipientFilter filter = [_player];
                _player.EmitSound(_config.Sounds.Select, filter, _config.Sounds.Volume);
            }
        }

        private int FindFirstEnabledOptionIndex()
        {
            int startIndex = CurrentPage * ItemsPerPage;
            int endIndex = Math.Min(Options.Count, startIndex + ItemsPerPage);
            int enabledIndex = 0;

            for (int i = startIndex; i < endIndex; i++)
            {
                if (!Options[i].IsDisabled)
                {
                    return enabledIndex;
                }
                enabledIndex++;
            }

            return -1;
        }

        private int GetMaxPage()
        {
            return (int)Math.Ceiling(Options.Count / (double)ItemsPerPage) - 1;
        }

        private int GetNavigationButtonCount()
        {
            int count = 0;

            bool showBackButton = CurrentPage > 0 || (IsSubMenu && PrevMenu != null);
            bool showNextButton = CurrentPage < GetMaxPage();

            if (showBackButton) count++;
            if (showNextButton) count++;
            if (HasExitButon) count++;
            if (ShowResolutionOption) count++;

            return count;
        }

        private void PlayCloseSound()
        {
            if (!string.IsNullOrEmpty(_config.Sounds.Close))
            {
                RecipientFilter filter = [_player];
                _player.EmitSound(_config.Sounds.Close, filter, _config.Sounds.Volume);
            }
        }

        private void PlayNextSound()
        {
            if (!string.IsNullOrEmpty(_config.Sounds.Next))
            {
                RecipientFilter filter = [_player];
                _player.EmitSound(_config.Sounds.Next, filter, _config.Sounds.Volume);
            }
        }

        private void PlayPrevSound()
        {
            if (!string.IsNullOrEmpty(_config.Sounds.Prev))
            {
                RecipientFilter filter = [_player];
                _player.EmitSound(_config.Sounds.Prev, filter, _config.Sounds.Volume);
            }
        }
    }
}