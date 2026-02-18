using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Serialization;

public sealed class WeightUnitsJsonConverter : JsonConverter<WeightUnits>
{
    public override WeightUnits Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Weight unit must be a string.");
        }

        var raw = reader.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return WeightUnits.Unknown;
        }

        if (Enum.TryParse(raw, true, out WeightUnits parsed))
        {
            return parsed;
        }

        return raw.ToLowerInvariant() switch
        {
            "kg" or "kilogram" or "kilograms" => WeightUnits.Kilograms,
            "lb" or "lbs" or "pound" or "pounds" => WeightUnits.Pounds,
            _ => throw new JsonException($"Invalid weight unit '{raw}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, WeightUnits value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public sealed class HeightUnitsJsonConverter : JsonConverter<HeightUnits>
{
    public override HeightUnits Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Height unit must be a string.");
        }

        var raw = reader.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return HeightUnits.Unknown;
        }

        if (Enum.TryParse(raw, true, out HeightUnits parsed))
        {
            return parsed;
        }

        return raw.ToLowerInvariant() switch
        {
            "m" or "meter" or "meters" => HeightUnits.Meters,
            "cm" or "centimeter" or "centimeters" => HeightUnits.Centimeters,
            "mm" or "millimeter" or "millimeters" => HeightUnits.Millimeters,
            _ => throw new JsonException($"Invalid height unit '{raw}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, HeightUnits value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
