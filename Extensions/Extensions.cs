using System;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

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
    public static class Helper
    {

        public static void SetBinds(CCSPlayerController player)
        {
            player.ExecuteClientCommand("echo test | bind 1 \"slot1;css_1\"");
            player.ExecuteClientCommand("echo test | bind 2 \"slot2;css_2\"");
            player.ExecuteClientCommand("echo test | bind 3 \"slot3;css_3\"");
            player.ExecuteClientCommand("echo test | bind 4 \"slot4;css_4\"");
            player.ExecuteClientCommand("echo test | bind 5 \"slot5;css_5\"");
            player.ExecuteClientCommand("echo test | bind 6 \"slot6;css_6\"");
            player.ExecuteClientCommand("echo test | bind 7 \"slot7;css_7\"");
            player.ExecuteClientCommand("echo test | bind 8 \"slot8;css_8\"");
            player.ExecuteClientCommand("echo test | bind 9 \"slot9;css_9\"");
        }
        public static void RemoveBinds(CCSPlayerController player)
        {

            player.ExecuteClientCommand("echo test | unbind 1 \"slot1;\"");
            player.ExecuteClientCommand("echo test | unbind 2 \"slot2;\"");
            player.ExecuteClientCommand("echo test | unbind 3 \"slot3;\"");
            player.ExecuteClientCommand("echo test | unbind 4 \"slot4;\"");
            player.ExecuteClientCommand("echo test | unbind 5 \"slot5;\"");
            player.ExecuteClientCommand("echo test | unbind 6 \"slot6;\"");
            player.ExecuteClientCommand("echo test | unbind 7 \"slot7;\"");
            player.ExecuteClientCommand("echo test | unbind 8 \"slot8;\"");
            player.ExecuteClientCommand("echo test | unbind 9 \"slot9;\"");

            player.ExecuteClientCommand("echo test | bind 1 \"css_1\"");
            player.ExecuteClientCommand("echo test | bind 2 \"css_2\"");
            player.ExecuteClientCommand("echo test | bind 3 \"css_3\"");
            player.ExecuteClientCommand("echo test | bind 4 \"css_4\"");
            player.ExecuteClientCommand("echo test | bind 5 \"css_5\"");
            player.ExecuteClientCommand("echo test | bind 6 \"css_6\"");
            player.ExecuteClientCommand("echo test | bind 7 \"css_7\"");
            player.ExecuteClientCommand("echo test | bind 8 \"css_8\"");
            player.ExecuteClientCommand("echo test | bind 9 \"css_9\"");
        }
    }
}
