using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CS2ScreenMenuAPI.Config;

namespace CS2ScreenMenuAPI.Internal
{
    internal static class WorldTextManager
    {
        internal static Dictionary<uint, CCSPlayerController> WorldTextOwners = new();
        internal static Dictionary<uint, (Vector Position, QAngle Angles)> EntityTransforms = new();
        internal static MenuConfig _config = new MenuConfig();

        public class MenuTextEntities
        {
            public CPointWorldText? EnabledOptions { get; set; }
            public CPointWorldText? DisabledOptions { get; set; }
        }

        internal static MenuTextEntities Create(
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
                return new MenuTextEntities();

            if (pawnBase.LifeState != (byte)LifeState_t.LIFE_DEAD)
            {
                return CreateForAlive(player, text, size, color, font, shiftX, shiftY, drawBackground, backgroundHeight, backgroundWidth);
            }
            else
            {
                return CreateForDead(player, text, size, color, font, shiftX, shiftY, drawBackground, backgroundHeight, backgroundWidth);
            }
        }

        internal static MenuTextEntities CreateForAlive(
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
                return new MenuTextEntities();

            var pawnBase = player.Pawn.Value;
            if (pawnBase == null)
                return new MenuTextEntities();

            CCSPlayerPawn? pawn = pawnBase.As<CCSPlayerPawn>();
            if (pawn == null)
                return new MenuTextEntities();

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

        internal static MenuTextEntities CreateForDead(
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
                return new MenuTextEntities();

            var pawnBase = player.Pawn.Value;
            if (pawnBase == null)
                return new MenuTextEntities();

            if (player.ControllingBot)
                return new MenuTextEntities();

            var observerServices = pawnBase.ObserverServices;
            if (observerServices == null)
                return new MenuTextEntities();

            CCSPlayerPawn? observerPawn = observerServices.ObserverTarget?.Value?.As<CCSPlayerPawn>();
            if (observerPawn == null || !observerPawn.IsValid)
                return new MenuTextEntities();

            CCSPlayerPawn pawn = observerPawn;
            viewmodel = player.EnsureCustomView(0);
            if (viewmodel == null)
                return new MenuTextEntities();

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

        private static MenuTextEntities CreateWorldText(
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
            var entities = new MenuTextEntities();

            QAngle eyeAngles = pawn.EyeAngles;
            Vector forward = new(), right = new(), up = new();
            NativeAPI.AngleVectors(eyeAngles.Handle, forward.Handle, right.Handle, up.Handle);

            Vector offset = forward * 7 + right * shiftX + up * shiftY;

            QAngle angles = new()
            {
                Y = eyeAngles.Y + 270,
                Z = 90 - eyeAngles.X,
                X = 0
            };

            var finalPos = pawn.AbsOrigin! + offset + new Vector(0, 0, pawn.ViewOffset.Z);

            entities.EnabledOptions = CreateEntity(
                effectiveOwner,
                viewmodel,
                finalPos,
                angles,
                text,
                size,
                color ?? Color.Aquamarine,
                font,
                drawBackground,
                backgroundHeight,
                backgroundWidth
            );

            if (entities.EnabledOptions != null)
            {
                Vector disabledPos = finalPos - forward * 0.5f;

                entities.DisabledOptions = CreateEntity(
                    effectiveOwner,
                    viewmodel,
                    disabledPos,
                    angles,
                    "",
                    size,
                    _config.DefaultSettings.DisabledOptionsColor,
                    font,
                    false,
                    0,
                    0
                );
            }

            return entities;
        }

        private static CPointWorldText? CreateEntity(
            CCSPlayerController effectiveOwner,
            CCSGOViewModel viewmodel,
            Vector position,
            QAngle angles,
            string text,
            float size,
            Color color,
            string font,
            bool drawBackground,
            float backgroundHeight,
            float backgroundWidth)
        {
            CPointWorldText? entity = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
            if (entity == null)
                return null;

            entity.MessageText = text;
            entity.Enabled = true;
            entity.FontSize = size;
            entity.Fullbright = true;
            entity.Color = color;
            entity.WorldUnitsPerPx = (0.25f / 1050) * size;
            entity.FontName = font;
            entity.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_LEFT;
            entity.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;
            entity.ReorientMode = PointWorldTextReorientMode_t.POINT_WORLD_TEXT_REORIENT_NONE;

            if (drawBackground)
            {
                entity.DrawBackground = true;
                entity.BackgroundBorderHeight = backgroundHeight;
                entity.BackgroundBorderWidth = backgroundWidth;
            }

            entity.DispatchSpawn();
            entity.Teleport(position, angles, null);
            entity.AcceptInput("ClearParent");
            entity.AcceptInput("SetParent", viewmodel, null, "!activator");

            WorldTextOwners[entity.Index] = effectiveOwner;
            EntityTransforms[entity.Index] = (position, angles);

            return entity;
        }
    }
}
