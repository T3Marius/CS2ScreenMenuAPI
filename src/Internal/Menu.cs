using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using static CS2ScreenMenuAPI.Buttons;
using static CS2ScreenMenuAPI.PlayerRes;
using static CounterStrikeSharp.API.Core.Listeners;
using Microsoft.Extensions.Logging;

namespace CS2ScreenMenuAPI
{
    public class Menu : IMenu, IDisposable
    {
        internal readonly CCSPlayerController _player;
        private readonly BasePlugin _plugin;
        private readonly Dictionary<string, CommandInfo.CommandCallback> _registeredKeyCommands = new();
        private bool _isClosed;
        private bool _isResolutionMenuShown = false;
        internal bool _isFlashing = false;
        internal int _flashKey = -1;
        private PlayerButtons g_OldButtons;

        private readonly MenuRenderer _renderer;

        public Menu? PrevMenu { get; set; } = null;
        public Menu? ParentMenu { get => PrevMenu; set => PrevMenu = value; }
        public Config _config { get; set; } = new Config();
        public string Title { get; set; } = string.Empty;
        public List<IMenuOption> Options { get; } = new();
        public int CurrentPage { get; set; }
        public bool ShowResolutionOption { get; set; }
        public int ItemsPerPage { get; } = 6;
        public PostSelect PostSelect = PostSelect.Nothing;
        public MenuType MenuType = MenuType.KeyPress;
        public bool HasExitButon { get; set; }
        public bool ShowPageCount { get; set; }
        public bool ShowControlsInfo { get; set; }
        public bool ShowDisabledOptionNum { get; set; }
        public bool IsSubMenu { get; set; } = false;
        public float MenuPositionX { get; set; }
        internal int _currentSelectionIndex = 0;
        internal string ScrollUpKey = "W";
        internal string ScrollDownKey = "S";
        internal string SelectKey = "E";

        public Menu(CCSPlayerController player, BasePlugin plugin)
        {
            _player = player;
            _plugin = plugin;
            _config = ConfigLoader.Load();

            _renderer = new MenuRenderer(this, _player);

            ConfigureSettings();
            RegisterEvents();

            if (!ResolutionDatabase._initialized)
            {
                try { ResolutionDatabase.InitializeAsync(_plugin.Logger, _config.Database).GetAwaiter().GetResult(); }
                catch (Exception ex) { _plugin.Logger.LogError($"Resolution DB init failed: {ex}"); }
            }

            if (ResolutionDatabase.HasPlayerResolution(player))
            {
                Resolution playerRes = ResolutionDatabase.GetPlayerResolution(player);
                MenuPositionX = playerRes.PositionX;
                _renderer.MenuPosition = _renderer.MenuPosition with { X = playerRes.PositionX };
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
                    Resolution playerRes = ResolutionDatabase.GetPlayerResolution(_player);
                    MenuPositionX = playerRes.PositionX;
                    _renderer.MenuPosition = _renderer.MenuPosition with { X = playerRes.PositionX };
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
                _renderer.MenuPosition = _renderer.MenuPosition with { X = _config.Settings.PositionX };
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
            Options.Add(new MenuOption { Text = text, Callback = callback, IsDisabled = disabled });
            _renderer.ForceRefresh = true;
        }
        public void Refresh()
        {
            int maxPage = GetMaxPage();
            if (CurrentPage > maxPage)
            {
                CurrentPage = maxPage;
            }
            if (CurrentPage < 0)
            {
                CurrentPage = 0;
            }

            int totalSelectableItems = GetEnabledOptionsCountOnCurrentPage() + GetNavigationButtonCount();
            if (_currentSelectionIndex >= totalSelectableItems && totalSelectableItems > 0)
            {
                _currentSelectionIndex = totalSelectableItems - 1;
            }
            else if (totalSelectableItems == 0)
            {
                _currentSelectionIndex = 0;
            }

            _renderer.ForceRefresh = true;
        }

        public void Display()
        {
            _player.CreateFakeWorldText(this);
            Server.NextFrame(() => _renderer.Draw());
        }

        private void TransitionToPrevMenu()
        {
            if (PrevMenu == null) return;
            PlayPrevSound();
            Menu prevMenu = PrevMenu;

            _isClosed = true;
            UnregisterKeyCommands();

            Server.NextFrame(() =>
            {
                if (!_player.IsValid || !_player.Pawn.IsValid) return;

                _renderer.DestroyEntities();

                MenuAPI.SetActiveMenu(_player, prevMenu);

                prevMenu._isClosed = false;
                prevMenu.RegisterKeyCommands();
                prevMenu.Display();
            });
        }
        public void OnKeyPress(CCSPlayerController player, int key)
        {
            if (_isClosed || _isFlashing) return;

            if (MenuType == MenuType.Scrollable)
            {
                return;
            }

            HandleInstantFlash(key, () =>
            {
                switch (key)
                {
                    case 0:
                        if (ShowResolutionOption)
                        {
                            Menu currentMenu = this;
                            Menu? prevMenu = PrevMenu;
                            bool isSubMenu = IsSubMenu;

                            Close(player);
                            PlaySelectSound();

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
                        if (IsSubMenu && CurrentPage == 0 && PrevMenu != null) { TransitionToPrevMenu(); }
                        else { PrevPage(); }
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
                        if (key >= 1 && key <= 6) { HandleOptionSelection(player, key); }
                        break;
                }
            });
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
                            var subMenu = (option is MenuOption menuOption) ? menuOption.SubMenu : null;

                            if (subMenu != null)
                            {
                                PlaySelectSound();
                                subMenu.PrevMenu = this;
                                subMenu.IsSubMenu = true;

                                Close(this._player);
                                Server.NextFrame(() =>
                                {
                                    MenuAPI.SetActiveMenu(this._player, subMenu);
                                    subMenu._isClosed = false;
                                    subMenu.RegisterKeyCommands();
                                    subMenu.Display();
                                });
                            }
                            else
                            {
                                option.Callback(_player, option);
                                if (_isResolutionMenuShown) return;
                                switch (PostSelect)
                                {
                                    case PostSelect.Close: Close(_player); PlayCloseSound(); break;
                                    case PostSelect.Reset: Refresh(); PlaySelectSound(); break;
                                    case PostSelect.Nothing: PlaySelectSound(); break;
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

                if (showBackButton && navCount++ == navIndex)
                {
                    if (IsSubMenu && CurrentPage == 0 && PrevMenu != null) { TransitionToPrevMenu(); }
                    else { PrevPage(); }
                    return;
                }
                if (showNextButton && navCount++ == navIndex) { NextPage(); return; }
                if (HasExitButon && navCount++ == navIndex) { Close(_player); PlayCloseSound(); return; }

                if (ShowResolutionOption && navCount++ == navIndex)
                {
                    PlaySelectSound();

                    Menu currentMenu = this;
                    Menu? prevMenu = PrevMenu;
                    bool isSubMenu = IsSubMenu;

                    Close(_player);

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
                    return;
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

                    Close(this._player);
                    Server.NextFrame(() =>
                    {
                        MenuAPI.SetActiveMenu(this._player, subMenu);
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
            if (totalSelectableItems == 0) return;
            _currentSelectionIndex = (_currentSelectionIndex + 1) % totalSelectableItems;
            Refresh();
        }

        private void ScrollUp()
        {
            int enabledOptionsCount = GetEnabledOptionsCountOnCurrentPage();
            int totalSelectableItems = enabledOptionsCount + GetNavigationButtonCount();
            if (totalSelectableItems == 0) return;
            _currentSelectionIndex = (_currentSelectionIndex - 1 + totalSelectableItems) % totalSelectableItems;
            Refresh();
        }

        public void NextPage()
        {
            int maxPage = GetMaxPage();
            if (CurrentPage < maxPage)
            {
                CurrentPage++;
                _currentSelectionIndex = 0;
                Refresh();
                PlayNextSound();
            }
        }

        public void PrevPage()
        {
            if (CurrentPage > 0)
            {
                CurrentPage--;
                _currentSelectionIndex = 0;
                Refresh();
                PlayPrevSound();
            }
        }

        public void Close(CCSPlayerController player)
        {
            if (_isClosed) return;
            _isClosed = true;
            _renderer.DestroyEntities();
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

        private void RegisterEvents()
        {
            _plugin.RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
            _plugin.RegisterEventHandler<EventRoundStart>(OnRoundStart);
            _plugin.RegisterListener<OnTick>(OnTick);
            _plugin.RegisterListener<CheckTransmit>(OnCheckTransmit);
        }
        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            if (MenuAPI.GetActiveMenu(_player) != this)
                return HookResult.Continue;

            if (MenuType != MenuType.KeyPress && _config.Settings.FreezePlayer)
            {
                _player.Freeze();
            }
            Server.NextFrame(() =>
            {
                _renderer.ForceRefresh = true;
                _player.CreateFakeWorldText(this);
                Server.NextFrame(() => Display());
            });

            return HookResult.Continue;
        }
        private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            if (@event.Userid == _player) Close(_player);
            return HookResult.Continue;
        }

        private void OnTick()
        {
            if (MenuAPI.GetActiveMenu(_player) != this || _player.Pawn.Value == null || !_player.IsValid)
            {
                if (!_isClosed) Close(_player);
                return;
            }
            if (_isClosed) { Close(_player); return; }

            _renderer.Tick();

            if (MenuType != MenuType.KeyPress)
            {
                PlayerButtons button = _player.Buttons;
                if (ButtonMapping.TryGetValue(ScrollUpKey, out PlayerButtons scrollUpButton) && (button & scrollUpButton) == 0 && (g_OldButtons & scrollUpButton) != 0) { ScrollUp(); }
                else if (ButtonMapping.TryGetValue(ScrollDownKey, out PlayerButtons scrollDownButton) && (button & scrollDownButton) == 0 && (g_OldButtons & scrollDownButton) != 0) { ScrollDown(); }
                else if (ButtonMapping.TryGetValue(SelectKey, out PlayerButtons selectButton) && (button & selectButton) == 0 && (g_OldButtons & selectButton) != 0) { SelectCurrentOption(); }
                g_OldButtons = button;
            }
        }

        private void OnCheckTransmit(CCheckTransmitInfoList infoList)
        {
            _renderer.CheckTransmit(infoList);
        }
        private void ConfigureSettings()
        {
            MenuType = _config.Settings.MenuType switch
            {
                "Scrollable" => MenuType.Scrollable,
                "Both" => MenuType.Both,
                _ => MenuType.KeyPress,
            };
            HasExitButon = _config.Settings.HasExitOption;
            ShowResolutionOption = _config.Settings.ShowResolutionOption;
            ShowPageCount = _config.Settings.ShowPageCount;
            ShowDisabledOptionNum = _config.Settings.ShowDisabledOptionNum;
            ShowControlsInfo = _config.Settings.ShowControlsInfo;
            if (MenuType != MenuType.KeyPress)
            {
                ScrollUpKey = _config.Controls.ScrollUp;
                ScrollDownKey = _config.Controls.ScrollDown;
                SelectKey = _config.Controls.Select;
            }
        }

        internal int GetEnabledOptionsCountOnCurrentPage()
        {
            int startIndex = CurrentPage * ItemsPerPage;
            int endIndex = Math.Min(Options.Count, startIndex + ItemsPerPage);
            int count = 0;
            for (int i = startIndex; i < endIndex; i++)
            {
                if (!Options[i].IsDisabled) count++;
            }
            return count;
        }

        internal int GetMaxPage() => (int)Math.Ceiling(Options.Count / (double)ItemsPerPage) - 1;
        private void HandleInstantFlash(int key, Action onFlashed)
        {

            if (MenuType == MenuType.Scrollable)
            {
                onFlashed.Invoke();
                return;
            }

            _isFlashing = true;
            _flashKey = key;
            _renderer.ForceRefresh = true;
            Server.NextFrame(() =>
            {
                if (_isClosed || MenuAPI.GetActiveMenu(_player) != this) return;

                _isFlashing = false;
                _flashKey = -1;
                _renderer.ForceRefresh = true;

                onFlashed.Invoke();
            });
        }
        internal int GetNavigationButtonCount()
        {
            int count = 0;
            if (CurrentPage > 0 || (IsSubMenu && PrevMenu != null)) count++;
            if (CurrentPage < GetMaxPage()) count++;
            if (HasExitButon) count++;
            if (ShowResolutionOption) count++;
            return count;
        }
        private void PlaySelectSound() { if (!string.IsNullOrEmpty(_config.Sounds.Select)) _player.EmitSound(_config.Sounds.Select); }
        private void PlayCloseSound() { if (!string.IsNullOrEmpty(_config.Sounds.Close)) _player.EmitSound(_config.Sounds.Close); }
        private void PlayNextSound() { if (!string.IsNullOrEmpty(_config.Sounds.Next)) _player.EmitSound(_config.Sounds.Next); }
        private void PlayPrevSound() { if (!string.IsNullOrEmpty(_config.Sounds.Prev)) _player.EmitSound(_config.Sounds.Prev); }
    }
}