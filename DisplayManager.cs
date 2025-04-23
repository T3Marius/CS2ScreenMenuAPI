using System.Drawing;
using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;

namespace CS2ScreenMenuAPI
{
    public static class DisplayManager
    {
        public readonly record struct VectorData(Vector Position, QAngle Angle);
        public static CCSPlayerPawn? GetPlayerPawn(CCSPlayerController player)
        {
            if (player.Pawn.Value is not CBasePlayerPawn pawn)
                return null;

            if (pawn.LifeState == (byte)LifeState_t.LIFE_DEAD)
            {
                if (pawn.ObserverServices?.ObserverTarget.Value?.As<CBasePlayerPawn>() is not CBasePlayerPawn observer)
                    return null;

                pawn = observer;
            }

            return pawn.As<CCSPlayerPawn>();
        }

        public static VectorData? FindVectorData(this CCSPlayerController player, float? size = null)
        {
            CCSPlayerPawn? playerPawn = GetPlayerPawn(player);
            if (playerPawn == null)
                return null;

            PlayerRes.Resolution resolution = ResolutionDatabase.GetPlayerResolution(player);

            QAngle eyeAngles = playerPawn!.EyeAngles;
            Vector forward = new(), right = new(), up = new();
            NativeAPI.AngleVectors(eyeAngles.Handle, forward.Handle, right.Handle, up.Handle);

            if (size.HasValue)
            {
                (float newX, float newY, float newSize) = GetWorldTextPosition(player, resolution.PositionX, resolution.PositionY, size.Value);

                resolution.PositionX = newX;
                resolution.PositionY = newY;
                size = newSize;
            }

            Vector offset = forward * 7 + right * resolution.PositionX + up * resolution.PositionY;
            QAngle angle = new()
            {
                Y = eyeAngles.Y + 270,
                Z = 90 - eyeAngles.X,
                X = 0
            };

            return new VectorData()
            {
                Position = playerPawn.AbsOrigin! + offset + new Vector(0, 0, playerPawn.ViewOffset.Z),
                Angle = angle,
            };
        }
        private static (float x, float y, float size) GetWorldTextPosition(CCSPlayerController controller, float x, float y, float size)
        {
            float fov = controller.DesiredFOV == 0 ? 90 : controller.DesiredFOV;

            if (fov == 90)
                return (x, y, size);

            float scaleFactor = (float)Math.Tan((fov / 2) * Math.PI / 180) / (float)Math.Tan(45 * Math.PI / 180);

            float newX = x * scaleFactor;
            float newY = y * scaleFactor;
            float newSize = size * scaleFactor;

            return (newX, newY, newSize);
        }
        public static CCSGOViewModel? EnsureCustomView(CCSPlayerController player)
        {
            var pawn = GetPlayerPawn(player);
            if (pawn == null || pawn.ViewModelServices == null)
                return null;

            int offset = Schema.GetSchemaOffset("CCSPlayer_ViewModelServices", "m_hViewModel");
            IntPtr viewModelHandleAddress = pawn.ViewModelServices.Handle + offset + 4;

            CHandle<CCSGOViewModel> handle = new(viewModelHandleAddress);
            if (!handle.IsValid)
            {
                CCSGOViewModel viewmodel = Utilities.CreateEntityByName<CCSGOViewModel>("predicted_viewmodel")!;
                viewmodel.DispatchSpawn();
                handle.Raw = viewmodel.EntityHandle.Raw;
                Utilities.SetStateChanged(pawn, "CCSPlayerPawnBase", "m_pViewModelServices");
            }

            return handle.Value;
        }

        public static CPointWorldText? CreateWorldText(
            string text,
            int size,
            Color color,
            string font,
            bool background,
            Color backgroundColor,
            float offset,
            Vector position,
            QAngle angle,
            CCSGOViewModel viewModel)
        {
            CPointWorldText entity = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext")!;

            if (entity == null || !entity.IsValid)
                return null;

            entity.MessageText = text;
            entity.Enabled = true;
            entity.FontSize = size;
            entity.Fullbright = true;
            entity.Color = color;
            entity.WorldUnitsPerPx = 0.01f;
            entity.FontName = font;
            entity.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_LEFT;
            entity.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;
            entity.ReorientMode = PointWorldTextReorientMode_t.POINT_WORLD_TEXT_REORIENT_NONE;
            entity.RenderMode = RenderMode_t.kRenderNormal;

            if (background)
            {
                entity.DrawBackground = true;
                entity.BackgroundBorderHeight = 0.1f;
                entity.BackgroundBorderWidth = 0.1f;
            }

            entity.DepthOffset = offset;

            entity.DispatchSpawn();
            entity.Teleport(position, angle, null);
            entity.AcceptInput("SetParent", viewModel, null, "!activator");

            return entity;
        }
        public static Dictionary<ulong, bool> PlayerFakeTextCreated = new Dictionary<ulong, bool>();

        public static void CreateFakeWorldText(this CCSPlayerController player, Menu instance)
        {
            ulong playerId = player.SteamID;

            if (PlayerFakeTextCreated.TryGetValue(playerId, out bool created) && created)
                return;

            CCSGOViewModel? viewModel = EnsureCustomView(player);
            if (viewModel == null) { instance.Close(player); return; }

            QAngle angle = new QAngle();
            Vector position = new Vector();

            CPointWorldText? entity = CreateWorldText("       ", 35, Color.Orange, "Arial", false, Color.Transparent, 0.1f, position, angle, viewModel);
            if (entity == null) { instance.Close(player); return; }

            VectorData? vectorData = FindVectorData(player);
            if (vectorData == null) { instance.Close(player); return; }

            entity.Teleport(vectorData.Value.Position, vectorData.Value.Angle, null);
            entity.AcceptInput("SetParent", viewModel, null, "!activator");

            entity.Remove();

            PlayerFakeTextCreated[playerId] = true;
        }
    }
}