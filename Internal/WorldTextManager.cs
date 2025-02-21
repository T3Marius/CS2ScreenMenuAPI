using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace CS2ScreenMenuAPI.Internal
{
    internal static class WorldTextManager
    {
        internal static Dictionary<uint, CCSPlayerController> WorldTextOwners = [];

        internal static CPointWorldText? Create(
            CCSPlayerController player,
            string text,
            float size = 35,
            Color? color = null,
            string font = "",
            float shiftX = 0f,
            float shiftY = 0f,
            bool drawBackground = true,
            float backgroundHeight = 0.2f,
            float backgroundWidth = 0.15f
        )
        {

            CCSGOViewModel? viewmodel = player.EnsureCustomView(0);
            if (viewmodel == null)
            {
                return null;
            }

            var pawn = player.Pawn.Value!;
            if (pawn == null)
            {
                return null;
            }

            bool isSpectating = false;
            CCSPlayerController effectiveOwner = player;

            if (pawn.LifeState == (byte)LifeState_t.LIFE_DEAD)
            {
                isSpectating = true;

                if (player.ControllingBot)
                {
                    return null;
                }

                var observerServices = pawn.ObserverServices;
                if (observerServices == null)
                {
                    return null;
                }

                var observerPawn = observerServices.ObserverTarget?.Value?.As<CCSPlayerPawn>();
                if (observerPawn == null || !observerPawn.IsValid)
                {
                    return null;
                }

                effectiveOwner = player;

                pawn = observerPawn;

                viewmodel = effectiveOwner.EnsureCustomView(0);
                if (viewmodel == null)
                {
                    return null;
                }
            }

            Console.WriteLine("[Create] Creating point_worldtext entity");
            CPointWorldText worldText = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext")!;
            if (worldText == null)
            {
                Console.WriteLine("[Create] Failed: couldn't create point_worldtext entity");
                return null;
            }
            worldText.MessageText = text;
            worldText.Enabled = true;
            worldText.FontSize = size;
            worldText.Fullbright = true;
            worldText.Color = color ?? Color.Aquamarine;
            worldText.WorldUnitsPerPx = (0.25f / 1050) * size;
            worldText.FontName = font;
            worldText.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_LEFT;
            worldText.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;
            worldText.ReorientMode = PointWorldTextReorientMode_t.POINT_WORLD_TEXT_REORIENT_NONE;

            if (drawBackground)
            {
                worldText.DrawBackground = true;
                worldText.BackgroundBorderHeight = backgroundHeight;
                worldText.BackgroundBorderWidth = backgroundWidth;
            }

            QAngle eyeAngles = pawn.As<CCSPlayerPawn>().EyeAngles;
            Vector forward = new(), right = new(), up = new();
            NativeAPI.AngleVectors(eyeAngles.Handle, forward.Handle, right.Handle, up.Handle);

            Vector offset = new();
            if (isSpectating)
            {
                offset += forward * 7;
                offset += right * shiftX;
                offset += up * shiftY;
            }
            else
            {
                offset += forward * 7;
                offset += right * shiftX;
                offset += up * shiftY;
            }

            QAngle angles = new()
            {
                Y = eyeAngles.Y + 270,
                Z = 90 - eyeAngles.X,
                X = 0
            };

            worldText.DispatchSpawn();

            var finalPos = pawn.AbsOrigin! + offset + new Vector(0, 0, pawn.ViewOffset.Z);

            worldText.Teleport(finalPos, angles, null);
            worldText.AcceptInput("ClearParent");
            worldText.AcceptInput("SetParent", viewmodel, null, "!activator");

            WorldTextOwners[worldText.Index] = effectiveOwner;

            return worldText;
        }
    }
}