using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;

namespace CS2ScreenMenuAPI
{
    public static class CCSPlayer
    {
        // Dictionary to store original velocity modifiers for frozen players
        private static readonly Dictionary<CCSPlayerController, float> _originalVelocityModifiers = new();
        private static readonly HashSet<CCSPlayerController> _frozenPlayers = new();
        private static readonly HashSet<CCSPlayerController> _frozenInResolutionMenu = new();
        public static string Localizer(this CCSPlayerController player, string key, params string[] args)
        {
            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            Config config = ConfigLoader.Load();

            if (config.Lang.TryGetValue(cultureInfo.Name, out var lang) && lang.TryGetValue(key, out var text))
            {
                return string.Format(text, args);
            }

            string shortName = cultureInfo.TwoLetterISOLanguageName.ToLower();
            if (config.Lang.TryGetValue(shortName, out lang) && lang.TryGetValue(key, out text))
            {
                return string.Format(text, args);
            }

            if (config.Lang.TryGetValue("en", out lang) && lang.TryGetValue(key, out text))
            {
                return string.Format(text, args);
            }
            return key;
        }
        public static CCSPlayerPawn? GetPlayerPawn(this CCSPlayerController player)
        {
            return player.PlayerPawn.Value;
        }
        public static CCSPlayerPawnBase? GetPlayerPawnBase(this CCSPlayerController player)
        {
            return player.GetPlayerPawn() as CCSPlayerPawnBase;
        }

        public static CCSGOViewModel? EnsureCustomView(this CCSPlayerController player, int index)
        {

            CCSPlayerPawnBase? pPawnBase = player.GetPlayerPawnBase();
            if (pPawnBase == null)
            {
                return null;
            }
            ;

            if (pPawnBase.LifeState == (byte)LifeState_t.LIFE_DEAD)
            {

                var playerPawn = player.Pawn.Value;
                if (playerPawn == null || !playerPawn.IsValid)
                {
                    return null;
                }

                if (player.ControllingBot)
                {
                    return null;
                }

                var observerServices = playerPawn.ObserverServices;
                if (observerServices == null)
                {
                    return null;
                }

                var observerPawn = observerServices.ObserverTarget?.Value?.As<CCSPlayerPawn>();
                if (observerPawn == null || !observerPawn.IsValid)
                {
                    return null;
                }

                var observerController = observerPawn.OriginalController.Value;
                if (observerController == null || !observerController.IsValid)
                {
                    return null;
                }

                pPawnBase = observerController.GetPlayerPawnBase();
                if (pPawnBase == null)
                {
                    return null;
                }
            }

            var pawn = pPawnBase as CCSPlayerPawn;
            if (pawn == null)
            {
                return null;
            }

            if (pawn.ViewModelServices == null)
            {
                return null;
            }

            int offset = Schema.GetSchemaOffset("CCSPlayer_ViewModelServices", "m_hViewModel");
            IntPtr viewModelHandleAddress = (IntPtr)(pawn.ViewModelServices.Handle + offset + 4);

            var handle = new CHandle<CCSGOViewModel>(viewModelHandleAddress);
            if (!handle.IsValid)
            {
                CCSGOViewModel viewmodel = Utilities.CreateEntityByName<CCSGOViewModel>("predicted_viewmodel")!;
                viewmodel.DispatchSpawn();
                handle.Raw = viewmodel.EntityHandle.Raw;
                Utilities.SetStateChanged(pawn, "CCSPlayerPawnBase", "m_pViewModelServices");
            }

            return handle.Value;
        }

        public static void Freeze(this CCSPlayerController player)
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn == null) return;

            if (!_originalVelocityModifiers.ContainsKey(player))
            {
                _originalVelocityModifiers[player] = pawn.VelocityModifier;
            }

            _frozenPlayers.Add(player);
        }

        public static void Unfreeze(this CCSPlayerController player)
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn == null) return;

            _frozenPlayers.Remove(player);

            if (_originalVelocityModifiers.TryGetValue(player, out float originalVelocity))
            {
                pawn.VelocityModifier = originalVelocity;
                _originalVelocityModifiers.Remove(player);
            }
        }

        public static void FreezeInResolutionMenu(this CCSPlayerController player)
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn == null) return;

            Config config = ConfigLoader.Load();
            if (!config.Settings.FreezePlayerInResolutionMenu) return;

            if (!_originalVelocityModifiers.ContainsKey(player))
            {
                _originalVelocityModifiers[player] = pawn.VelocityModifier;
            }

            _frozenInResolutionMenu.Add(player);
        }

        public static void UnfreezeFromResolutionMenu(this CCSPlayerController player)
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn == null) return;

            _frozenInResolutionMenu.Remove(player);

            if (!_frozenPlayers.Contains(player) && _originalVelocityModifiers.TryGetValue(player, out float originalVelocity))
            {
                pawn.VelocityModifier = originalVelocity;
                _originalVelocityModifiers.Remove(player);
            }
        }

        public static void UpdateFrozenPlayers()
        {
            foreach (var player in _frozenPlayers.Concat(_frozenInResolutionMenu))
            {
                var pawn = player.PlayerPawn.Value;
                if (pawn != null)
                {
                    pawn.VelocityModifier = 0f;
                }
            }
            foreach (var player in Utilities.GetPlayers())
            {
                if (MenuAPI.GetActiveMenu(player)!.MenuType == MenuType.Scrollable && MenuAPI.GetActiveMenu(player)!._config.Settings.FreezePlayer)
                {
                    var pawn = player.PlayerPawn.Value;
                    if (pawn != null)
                    {
                        pawn.VelocityModifier = 0f;
                    }
                }
            }
        }

        public static void CleanupFrozenPlayer(CCSPlayerController player)
        {
            _frozenPlayers.Remove(player);
            _frozenInResolutionMenu.Remove(player);
            _originalVelocityModifiers.Remove(player);
        }
        public static void ChangeMoveType(this CBasePlayerPawn pawn, MoveType_t movetype)
        {
            if (pawn.Handle == IntPtr.Zero)
                return;

            pawn.MoveType = movetype;
            Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", movetype);
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
        }
    }
}