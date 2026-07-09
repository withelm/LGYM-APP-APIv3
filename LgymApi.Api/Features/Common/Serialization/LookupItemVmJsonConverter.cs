using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Api.Features.Common.Contracts;

namespace LgymApi.Api.Features.Common.Serialization;

internal sealed class LookupItemVmJsonConverter : JsonConverter<LookupItemVm>
{
    public override LookupItemVm? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return new LookupItemVm
            {
                Id = value,
                DisplayName = value
            };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Lookup item must be a string or an object.");
        }

        var id = string.Empty;
        var displayName = string.Empty;

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (root.TryGetProperty("id", out var idProperty))
        {
            id = idProperty.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("displayName", out var displayNameProperty))
        {
            displayName = displayNameProperty.GetString() ?? string.Empty;
        }

        return new LookupItemVm
        {
            Id = id,
            DisplayName = displayName
        };
    }

    public override void Write(Utf8JsonWriter writer, LookupItemVm value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("displayName", value.DisplayName);
        writer.WriteEndObject();
    }
}
