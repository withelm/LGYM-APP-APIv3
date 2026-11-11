using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LgymApi.Api.Features.Common.Serialization;

public sealed class ExplicitOffsetDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected an ISO 8601 timestamp string with an explicit offset.");
        }

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value)
            || !HasExplicitOffset(value)
            || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new JsonException("Expected an ISO 8601 timestamp string with an explicit offset.");
        }

        return parsed;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value);
            return;
        }

        writer.WriteNullValue();
    }

    private static bool HasExplicitOffset(string value)
    {
        if (value.EndsWith('Z'))
        {
            return true;
        }

        var timeSeparator = value.IndexOf('T');
        var offsetSign = Math.Max(value.LastIndexOf('+'), value.LastIndexOf('-'));
        return timeSeparator >= 0
            && offsetSign > timeSeparator
            && value.Length - offsetSign == 6
            && value[offsetSign + 3] == ':';
    }
}
