using System.Text.Json;
using System.Text.Json.Serialization;

namespace LgymApi.Api.Features.Trainer.Contracts;

public sealed class UniqueReportAnswersConverter : JsonConverter<Dictionary<string, JsonElement>>
{
    public override Dictionary<string, JsonElement> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        var answers = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!answers.TryAdd(property.Name, property.Value.Clone()))
            {
                throw new JsonException();
            }
        }
        return answers;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, JsonElement> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var answer in value)
        {
            writer.WritePropertyName(answer.Key);
            answer.Value.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}
