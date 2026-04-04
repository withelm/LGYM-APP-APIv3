using System.Text.Json;
using System.Text.Json.Serialization;

namespace LgymApi.Domain.Notifications;

[JsonConverter(typeof(InAppNotificationTypeJsonConverter))]
public readonly record struct InAppNotificationType
{
    public string Value { get; }

    private InAppNotificationType(string value)
    {
        Value = value;
    }

    public static InAppNotificationType Define(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("In-app notification type cannot be empty.", nameof(value));
        }

        return new InAppNotificationType(value);
    }

    public static InAppNotificationType Parse(string value)
    {
        if (InAppNotificationTypes.TryFromValue(value, out var notificationType))
        {
            return notificationType;
        }

        throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown in-app notification type.");
    }

    public override string ToString() => Value;
}

internal sealed class InAppNotificationTypeJsonConverter : JsonConverter<InAppNotificationType>
{
    public override InAppNotificationType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("In-app notification type cannot be empty.");
        }

        return InAppNotificationType.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, InAppNotificationType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
