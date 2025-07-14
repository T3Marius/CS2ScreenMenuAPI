using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API;
using static CounterStrikeSharp.API.Core.Listeners;

namespace CS2ScreenMenuAPI
{
    public static class PlayerRes
    {
        private static readonly Dictionary<CCSPlayerController, ResolutionMenuState> _activeResolutionMenus = new();

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

        private class ResolutionMenuState
        {
            public float CurrentPositionX { get; set; }
            public PlayerButtons PreviousButtons { get; set; }
            public BasePlugin Plugin { get; set; }
            public Action? AfterSelectionCallback { get; set; }
            public bool IsActive { get; set; } = true;
            public PlayerButtons HoldingButton { get; set; } = 0;
            public DateTime ButtonHoldStartTime { get; set; } = DateTime.MinValue;
            public int RepeatCount { get; set; } = 0;

            public ResolutionMenuState(float initialX, BasePlugin plugin, Action? callback = null)
            {
                CurrentPositionX = initialX;
                Plugin = plugin;
                AfterSelectionCallback = callback;
            }
        }

        public static void CreateResolutionMenu(CCSPlayerController player, BasePlugin plugin)
        {
            CreateResolutionMenu(player, plugin, null);
        }

        public static void CreateResolutionMenu(CCSPlayerController player, BasePlugin plugin, Action? afterSelectionCallback)
        {
            var config = ConfigLoader.Load();

            float currentPosX = ResolutionDatabase.HasPlayerResolution(player)
                ? ResolutionDatabase.GetPlayerResolution(player).PositionX
                : config.Settings.PositionX;

            var menuState = new ResolutionMenuState(currentPosX, plugin, afterSelectionCallback);
            _activeResolutionMenus[player] = menuState;

            if (_activeResolutionMenus.Count == 1)
            {
                plugin.RegisterListener<OnTick>(OnResolutionMenuTick);
            }
            player.FreezeInResolutionMenu();

            ShowResolutionMenu(player, menuState);
        }

        private static void OnResolutionMenuTick()
        {
            var playersToRemove = new List<CCSPlayerController>();
            DateTime now = DateTime.Now;
            const float InitialDelay = 0.3f;
            const float RepeatDelay = 0.05f;

            foreach (var kvp in _activeResolutionMenus.ToList())
            {
                var player = kvp.Key;
                var menuState = kvp.Value;

                if (!player.IsValid || !player.Pawn.IsValid || !menuState.IsActive)
                {
                    playersToRemove.Add(player);
                    continue;
                }

                var currentButtons = player.Buttons;
                var previousButtons = menuState.PreviousButtons;

                PlayerButtons movementButtons = currentButtons & (PlayerButtons.Moveleft | PlayerButtons.Moveright);
                bool buttonHandled = false;

                if (movementButtons != 0)
                {
                    if (menuState.HoldingButton != movementButtons)
                    {
                        menuState.HoldingButton = movementButtons;
                        menuState.ButtonHoldStartTime = now;
                        menuState.RepeatCount = 0;
                        buttonHandled = HandleMovementButton(player, menuState, movementButtons);
                    }
                    else
                    {
                        double totalSeconds = (now - menuState.ButtonHoldStartTime).TotalSeconds;
                        if (totalSeconds >= InitialDelay)
                        {
                            int repeatCount = (int)((totalSeconds - InitialDelay) / RepeatDelay);
                            if (repeatCount > menuState.RepeatCount)
                            {
                                buttonHandled = HandleMovementButton(player, menuState, movementButtons);
                                menuState.RepeatCount = repeatCount;
                            }
                        }
                    }
                }
                else
                {
                    menuState.HoldingButton = 0;
                    menuState.ButtonHoldStartTime = DateTime.MinValue;
                    menuState.RepeatCount = 0;
                }

                if (!buttonHandled)
                {
                    if ((currentButtons & PlayerButtons.Use) == 0 && (previousButtons & PlayerButtons.Use) != 0)
                    {
                        SaveAndCloseResolutionMenu(player, menuState);
                        playersToRemove.Add(player);
                        continue;
                    }
                    else if ((currentButtons & PlayerButtons.Reload) == 0 && (previousButtons & PlayerButtons.Reload) != 0)
                    {
                        CloseResolutionMenu(player, menuState, false);
                        playersToRemove.Add(player);
                        continue;
                    }
                }

                ShowResolutionMenu(player, menuState);
                menuState.PreviousButtons = currentButtons;
            }

            foreach (var player in playersToRemove)
            {
                _activeResolutionMenus.Remove(player);
            }

            if (_activeResolutionMenus.Count == 0)
            {

            }
        }

        private static bool HandleMovementButton(CCSPlayerController player, ResolutionMenuState menuState, PlayerButtons button)
        {
            if ((button & PlayerButtons.Moveleft) != 0)
            {
                menuState.CurrentPositionX -= 0.02f;
                UpdateMenuPositionRealTime(player, menuState);
                return true;
            }
            else if ((button & PlayerButtons.Moveright) != 0)
            {
                menuState.CurrentPositionX += 0.02f;
                UpdateMenuPositionRealTime(player, menuState);
                return true;
            }
            return false;
        }


        private static void ShowResolutionMenu(CCSPlayerController player, ResolutionMenuState menuState)
        {
            var sb = new System.Text.StringBuilder();

            sb.Append("<font color='#00FF00' class='fontSize-m'><b>🎯 Menu Position Adjustment</b></font>");
            sb.Append(" <font color='#FFD700' class='fontSize-sm'>Current X Position: </font>");
            sb.Append($"<font color='#00FFFF' class='fontSize-m'><b>{menuState.CurrentPositionX:F3}</b></font><br>");

            sb.Append("<font color='#FFFFFF' class='fontSize-sm'>");
            sb.Append("📍 <b>A</b> - Move menu left");
            sb.Append("<br>");
            sb.Append("📍 <b>D</b> - Move menu right");
            sb.Append("<br>");
            sb.Append("✅ <b>E</b> - Save position");
            sb.Append("<br>");
            sb.Append("❌ <b>R</b> - Cancel");
            sb.Append("</font><br>");

            sb.Append("<font color='#FFA500' class='fontSize-s'>Adjust your menu position by your liking.</font>");

            player.PrintToCenterHtml(sb.ToString());
        }

        private static void UpdateMenuPositionRealTime(CCSPlayerController player, ResolutionMenuState menuState)
        {
            var tempResolution = new Resolution(menuState.CurrentPositionX, 0);
            ResolutionDatabase.SetPlayerResolution(player, tempResolution);

            if (menuState.AfterSelectionCallback != null)
            {
                Server.NextFrame(() =>
                {
                    if (player.IsValid)
                    {
                        menuState.AfterSelectionCallback.Invoke();
                    }
                });
            }
        }
        private static void SaveAndCloseResolutionMenu(CCSPlayerController player, ResolutionMenuState menuState)
        {
            var resolution = new Resolution(menuState.CurrentPositionX, 0);
            ResolutionDatabase.SetPlayerResolution(player, resolution);

            CloseResolutionMenu(player, menuState, true);
        }

        private static void CloseResolutionMenu(CCSPlayerController player, ResolutionMenuState menuState, bool saved)
        {
            menuState.IsActive = false;

            // Unfreeze player from resolution menu when closing
            player.UnfreezeFromResolutionMenu();

            Server.NextFrame(() =>
            {
                if (player.IsValid && saved && menuState.AfterSelectionCallback != null)
                {
                    menuState.AfterSelectionCallback.Invoke();
                }
            });
        }

        public static void CleanupPlayerResolutionMenu(CCSPlayerController player)
        {
            if (_activeResolutionMenus.ContainsKey(player))
            {
                // Unfreeze player from resolution menu
                player.UnfreezeFromResolutionMenu();

                _activeResolutionMenus.Remove(player);
            }
        }


    }
}