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

            // If the pawn is dead, try to get the observer's pawn.
            if (pPawnBase.LifeState == (byte)LifeState_t.LIFE_DEAD)
            {
                if (player.ControllingBot)
                {
                    return null;
                }
                else
                {
                    var observerServices = player.PlayerPawn.Value?.ObserverServices;
                    if (observerServices == null)
                    {
                        return null;
                    }

                    var observerPawn = observerServices.ObserverTarget;
                    if (observerPawn == null || !observerPawn.IsValid)
                    {
                        return null;
                    }

                    // Try to cast the observer pawn to a CCSPlayerPawn.
                    var obsPawn = observerPawn.Value as CCSPlayerPawn;
                    if (obsPawn == null)
                    {
                        return null;
                    }

                    var observerController = obsPawn.OriginalController;
                    if (observerController == null || !observerController.IsValid)
                    {
                        return null;
                    }

                    // Use the observer controller's index to find the observer.
                    uint origIndex = observerController.Value!.Index;
                    if (origIndex == 0)
                    {
                        return null;
                    }
                    uint observerIndex = origIndex - 1;

                    // Assume Utilities.GetPlayers() returns all CCSPlayerController instances.
                    var allPlayers = Utilities.GetPlayers();
                    var observer = allPlayers.FirstOrDefault(p => p.Index == observerIndex);
                    if (observer == null)
                    {
                        return null;
                    }

                    pPawnBase = observer.PlayerPawn.Value;
                    if (pPawnBase == null)
                    {
                        return null;
                    }
                }
            }

            if (pPawnBase.ViewModelServices == null)
            {
                return null;
            }

            var handle = new CHandle<CCSGOViewModel>(
                (IntPtr)(pPawnBase.ViewModelServices.Handle +
                         Schema.GetSchemaOffset("CCSPlayer_ViewModelServices", "m_hViewModel") + 4));

            if (!handle.IsValid)
            {
                CCSGOViewModel? viewmodel = Utilities.CreateEntityByName<CCSGOViewModel>("predicted_viewmodel");
                if (viewmodel == null)
                {
                    return null;
                }

                viewmodel.DispatchSpawn();
                handle.Raw = viewmodel.EntityHandle.Raw;
            }

            return handle.Value;
        }
    }
}