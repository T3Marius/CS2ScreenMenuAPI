using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;

namespace CS2ScreenMenuAPI.Internal
{
    public static class CCSPlayer
    {
        public static bool IsValidPlayer(CCSPlayerController? p)
        {
            return p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected;
        }

        public static CCSPlayerPawn? GetPlayerPawn(this CCSPlayerController player)
        {
            return player.PlayerPawn.Value;
        }
        public static CCSPlayerPawnBase? GetPlayerPawnBase(this CCSPlayerController player)
        {
            return player.GetPlayerPawn() as CCSPlayerPawnBase;
        }

        public static void InitializePlayerWorldText(CCSPlayerController player)
        {
            if (player == null) return;

            WorldTextManager.Create(player, "");
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
    }
}