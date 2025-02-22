using System;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

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
}
