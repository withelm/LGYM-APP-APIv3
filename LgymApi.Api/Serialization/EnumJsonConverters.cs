using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Serialization;

public sealed class StringEnumJsonConverterFactory : JsonConverterFactory
{
    private static readonly Dictionary<Type, IReadOnlyDictionary<string, string>> AliasMap =
        new()
        {
            [typeof(WeightUnits)] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["kg"] = nameof(WeightUnits.Kilograms),
                ["kilogram"] = nameof(WeightUnits.Kilograms),
                ["kilograms"] = nameof(WeightUnits.Kilograms),
                ["lb"] = nameof(WeightUnits.Pounds),
                ["lbs"] = nameof(WeightUnits.Pounds),
                ["pound"] = nameof(WeightUnits.Pounds),
                ["pounds"] = nameof(WeightUnits.Pounds)
            },
            [typeof(HeightUnits)] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["m"] = nameof(HeightUnits.Meters),
                ["meter"] = nameof(HeightUnits.Meters),
                ["meters"] = nameof(HeightUnits.Meters),
                ["cm"] = nameof(HeightUnits.Centimeters),
                ["centimeter"] = nameof(HeightUnits.Centimeters),
                ["centimeters"] = nameof(HeightUnits.Centimeters),
                ["mm"] = nameof(HeightUnits.Millimeters),
                ["millimeter"] = nameof(HeightUnits.Millimeters),
                ["millimeters"] = nameof(HeightUnits.Millimeters)
            }
        };

    public override bool CanConvert(Type typeToConvert)
    {
        var enumType = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
        return enumType.IsEnum;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var enumType = Nullable.GetUnderlyingType(typeToConvert);
        if (enumType is null)
        {
            var converterType = typeof(StringEnumJsonConverter<>).MakeGenericType(typeToConvert);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }

        var nullableConverterType = typeof(NullableStringEnumJsonConverter<>).MakeGenericType(enumType);
        return (JsonConverter)Activator.CreateInstance(nullableConverterType)!;
    }

    private static bool TryResolveAlias<TEnum>(string raw, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        if (!AliasMap.TryGetValue(typeof(TEnum), out var aliases))
        {
            return false;
        }

        if (!aliases.TryGetValue(raw.Trim(), out var enumName))
        {
            return false;
        }

        return Enum.TryParse(enumName, out value);
    }

    private sealed class StringEnumJsonConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"{typeof(TEnum).Name} must be provided as string.");
            }

            var raw = reader.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return default;
            }

            if (Enum.TryParse(raw, true, out TEnum parsed))
            {
                return parsed;
            }

            if (TryResolveAlias(raw, out TEnum aliased))
            {
                return aliased;
            }

            throw new JsonException($"Invalid value '{raw}' for enum {typeof(TEnum).Name}.");
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    private sealed class NullableStringEnumJsonConverter<TEnum> : JsonConverter<TEnum?>
        where TEnum : struct, Enum
    {
        private readonly StringEnumJsonConverter<TEnum> _inner = new();

        public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            return _inner.Read(ref reader, typeof(TEnum), options);
        }

        public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value.Value.ToString());
        }
    }
}
