using LgymApi.Domain.Notifications;
using System.Text.Json;

namespace LgymApi.UnitTests.InAppNotifications;

[TestFixture]
public sealed class InAppNotificationTypeTests
{
    [Test]
    public void Define_WithValidValue_ReturnsValueObject()
    {
        var type = InAppNotificationType.Define("custom.type");

        Assert.That(type.Value, Is.EqualTo("custom.type"));
    }

    [Test]
    public void Define_WithEmptyValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => InAppNotificationType.Define(string.Empty));
    }

    [Test]
    public void Parse_WithKnownValue_ReturnsRegisteredType()
    {
        var type = InAppNotificationType.Parse("trainer.invitation.sent");

        Assert.That(type, Is.EqualTo(InAppNotificationTypes.InvitationSent));
    }

    [Test]
    public void Parse_WithUnknownValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InAppNotificationType.Parse("unknown.type"));
    }

    [Test]
    public void TryFromValue_WithKnownValue_ReturnsTrue()
    {
        var result = InAppNotificationTypes.TryFromValue("trainer.invitation.accepted", out var type);

        Assert.That(result, Is.True);
        Assert.That(type, Is.EqualTo(InAppNotificationTypes.InvitationAccepted));
    }

    [Test]
    public void TryFromValue_WithUnknownValue_ReturnsFalse()
    {
        var result = InAppNotificationTypes.TryFromValue("unknown.type", out var type);

        Assert.That(result, Is.False);
        Assert.That(type, Is.EqualTo(default(InAppNotificationType)));
    }

    [Test]
    public void TryFromValue_WithNull_ReturnsFalse()
    {
        var result = InAppNotificationTypes.TryFromValue(null, out var type);

        Assert.That(result, Is.False);
        Assert.That(type, Is.EqualTo(default(InAppNotificationType)));
    }

    [Test]
    public void JsonConverter_SerializesAndDeserializesRegisteredType()
    {
        var json = JsonSerializer.Serialize(InAppNotificationTypes.InvitationRejected);
        var type = JsonSerializer.Deserialize<InAppNotificationType>(json);

        Assert.That(json, Is.EqualTo("\"trainer.invitation.rejected\""));
        Assert.That(type, Is.EqualTo(InAppNotificationTypes.InvitationRejected));
    }

    [Test]
    public void JsonConverter_WithEmptyString_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<InAppNotificationType>("\"\""));
    }
}
