using System;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using CS2ScreenMenuAPI.Internal;

namespace CS2ScreenMenuAPI.Extensions
{
    public class ColorJsonConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? colorString = reader.GetString();
            if (!string.IsNullOrEmpty(colorString))
            {
                // Try parsing by name first.
                Color color = Color.FromName(colorString);
                if (color.IsKnownColor || color.IsNamedColor)
                {
                    return color;
                }
                try
                {
                    return ColorTranslator.FromHtml(colorString);
                }
                catch
                {
                    return Color.Empty;
                }
            }
            return Color.Empty;
        }

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        {
            string colorString = value.IsKnownColor || value.IsNamedColor
                ? value.Name
                : ColorTranslator.ToHtml(value);
            writer.WriteStringValue(colorString);
        }
    }
    public static class PlayerExtensions
    {
        public static void Freeze(this CCSPlayerController player)
        {
            CCSPlayerPawn? pawn = player.PlayerPawn.Value;
            if (pawn != null)
            {
                pawn.ChangeMoveType(MoveType_t.MOVETYPE_OBSOLETE);
            }
        }
        public static void AdjustMenuForFOV(CCSPlayerController player, ref float positionX, ref float positionY, ref float menuSize)
        {
            var fov = player.DesiredFOV == 0 ? 90 : player.DesiredFOV;
            if (fov == 90)
                return;

            float fovRatio = fov / 90.0f;
            menuSize = 32.0f * fovRatio;

            float baseX = positionX;

            if (Math.Abs(fov - 100f) < 0.01f)
            {
                positionX = baseX * (fovRatio * 1.1f) - (fovRatio - 1.0f) * 0.15f;
            }

            else if (fov > 90 && fov < 110)
            {
                positionX = baseX * (fovRatio * 1.1f) - (fovRatio - 1.0f) * 0.75f;
            }
            else
            {
                positionX = baseX * (fovRatio * 1.1f) - (fovRatio - 1.0f) * 1.5f;
            }

            float baseY = positionY;
            if (fov > 90)
            {
                positionY = baseY - ((fov - 90) * 0.015f);
            }
            else if (fov < 90)
            {
                positionY = baseY + ((90 - fov) * 0.015f);
            }

            if (fov > 120)
            {
                positionX -= (fov - 120) * 0.08f;
            }
            else if (fov < 50)
            {
                positionX += (50 - fov) * 0.03f;
            }

            menuSize = Math.Max(16.0f, Math.Min(menuSize, 48.0f));
        }


        public static void Unfreeze(this CCSPlayerController player)
        {
            CCSPlayerPawn? pawn = player.PlayerPawn.Value;
            if (pawn != null)
            {
                pawn.ChangeMoveType(MoveType_t.MOVETYPE_WALK);
            }
        }
        public static void ChangeMoveType(this CBasePlayerPawn pawn, MoveType_t movetype)
        {
            if (pawn.Handle == IntPtr.Zero)
            {
                return;
            }

            pawn.MoveType = movetype;
            Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", movetype);
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
        }

    }
    public static class WorldTextExtensions
    {
        public static Vector GetPosition(this CPointWorldText entity)
        {
            if (WorldTextManager.EntityTransforms.TryGetValue(entity.Index, out var transform))
            {
                return transform.Position;
            }
            return new Vector();
        }

        public static QAngle GetAngles(this CPointWorldText entity)
        {
            if (WorldTextManager.EntityTransforms.TryGetValue(entity.Index, out var transform))
            {
                return transform.Angles;
            }
            return new QAngle();
        }
        public static void SetColor(this CPointWorldText worldText, string color)
        {
            worldText.AcceptInput("SetColor", worldText, worldText, $"{color}");
            Utilities.SetStateChanged(worldText, "CPointWorldText", "m_Color");
        }
    }
}
