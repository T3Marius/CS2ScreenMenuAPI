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
    public static class Player
    {
        public static void Freeze(this CCSPlayerController player)
        {
            CCSPlayerPawn? pawn = player.PlayerPawn.Value;
            if (pawn != null)
            {
                pawn.ChangeMoveType(MoveType_t.MOVETYPE_OBSOLETE);
            }
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
    }
}
