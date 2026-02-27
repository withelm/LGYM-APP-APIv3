using System.Text.Json;
using System.Text.Json.Serialization;

namespace LgymApi.Domain.Notifications;

[JsonConverter(typeof(EmailNotificationTypeJsonConverter))]
public readonly record struct EmailNotificationType
{
    public string Value { get; }

    private EmailNotificationType(string value)
    {
        Value = value;
    }

    public static EmailNotificationType Define(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Email notification type cannot be empty.", nameof(value));
        }

        return new EmailNotificationType(value);
    }

    public static EmailNotificationType Parse(string value)
    {
        if (EmailNotificationTypes.TryFromValue(value, out var notificationType))
        {
            return notificationType;
        }

        throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown email notification type.");
    }

    public override string ToString() => Value;
}

internal sealed class EmailNotificationTypeJsonConverter : JsonConverter<EmailNotificationType>
{
    public override EmailNotificationType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("Email notification type cannot be empty.");
        }

        return EmailNotificationType.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, EmailNotificationType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
