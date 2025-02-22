using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace CS2ScreenMenuAPI.Internal
{
    internal static class WorldTextManager
    {
        internal static Dictionary<uint, CCSPlayerController> WorldTextOwners = new();

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
            var pawnBase = player.Pawn.Value;
            if (pawnBase == null)
                return null;

            if (pawnBase.LifeState != (byte)LifeState_t.LIFE_DEAD)
            {
                return CreateForAlive(player, text, size, color, font, shiftX, shiftY, drawBackground, backgroundHeight, backgroundWidth);
            }
            else
            {
                return CreateForDead(player, text, size, color, font, shiftX, shiftY, drawBackground, backgroundHeight, backgroundWidth);
            }
        }

        internal static CPointWorldText? CreateForAlive(
            CCSPlayerController player,
            string text,
            float size,
            Color? color,
            string font,
            float shiftX,
            float shiftY,
            bool drawBackground,
            float backgroundHeight,
            float backgroundWidth
        )
        {
            CCSGOViewModel? viewmodel = player.EnsureCustomView(0);
            if (viewmodel == null)
                return null;

            var pawnBase = player.Pawn.Value;
            if (pawnBase == null)
                return null;

            CCSPlayerPawn? pawn = pawnBase.As<CCSPlayerPawn>();
            if (pawn == null)
                return null;

            return CreateWorldText(
                effectiveOwner: player,
                pawn: pawn,
                viewmodel: viewmodel,
                text: text,
                size: size,
                color: color,
                font: font,
                shiftX: shiftX,
                shiftY: shiftY,
                drawBackground: drawBackground,
                backgroundHeight: backgroundHeight,
                backgroundWidth: backgroundWidth,
                isSpectating: false
            );
        }

        internal static CPointWorldText? CreateForDead(
            CCSPlayerController player,
            string text,
            float size,
            Color? color,
            string font,
            float shiftX,
            float shiftY,
            bool drawBackground,
            float backgroundHeight,
            float backgroundWidth
        )
        {
            CCSGOViewModel? viewmodel = player.EnsureCustomView(0);
            if (viewmodel == null)
                return null;

            var pawnBase = player.Pawn.Value;
            if (pawnBase == null)
                return null;

            if (player.ControllingBot)
                return null;

            var observerServices = pawnBase.ObserverServices;
            if (observerServices == null)
                return null;

            CCSPlayerPawn? observerPawn = observerServices.ObserverTarget?.Value?.As<CCSPlayerPawn>();
            if (observerPawn == null || !observerPawn.IsValid)
                return null;

            CCSPlayerPawn pawn = observerPawn;
            viewmodel = player.EnsureCustomView(0);
            if (viewmodel == null)
                return null;

            return CreateWorldText(
                effectiveOwner: player,
                pawn: pawn,
                viewmodel: viewmodel,
                text: text,
                size: size,
                color: color,
                font: font,
                shiftX: shiftX,
                shiftY: shiftY,
                drawBackground: drawBackground,
                backgroundHeight: backgroundHeight,
                backgroundWidth: backgroundWidth,
                isSpectating: true
            );
        }
        private static CPointWorldText? CreateWorldText(
            CCSPlayerController effectiveOwner,
            CCSPlayerPawn pawn,
            CCSGOViewModel viewmodel,
            string text,
            float size,
            Color? color,
            string font,
            float shiftX,
            float shiftY,
            bool drawBackground,
            float backgroundHeight,
            float backgroundWidth,
            bool isSpectating
        )
        {
            CPointWorldText? worldText = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
            if (worldText == null)
                return null;

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

            QAngle eyeAngles = pawn.EyeAngles;
            Vector forward = new(), right = new(), up = new();
            NativeAPI.AngleVectors(eyeAngles.Handle, forward.Handle, right.Handle, up.Handle);

            Vector offset = new();
            offset += forward * 7;
            offset += right * shiftX;
            offset += up * shiftY;

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
