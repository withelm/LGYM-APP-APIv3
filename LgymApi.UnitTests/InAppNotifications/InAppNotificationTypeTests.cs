using FluentAssertions;
using LgymApi.Domain.Notifications;
using System.Text.Json;
using NUnit.Framework;

namespace LgymApi.UnitTests.InAppNotifications;

[TestFixture]
public sealed class InAppNotificationTypeTests
{
    [Test]
    public void Define_WithValidValue_ReturnsValueObject()
    {
        var type = InAppNotificationType.Define("custom.type");

        type.Value.Should().Be("custom.type");
    }

    [Test]
    public void Define_WithEmptyValue_Throws()
    {
        var act = () => InAppNotificationType.Define(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Parse_WithKnownValue_ReturnsRegisteredType()
    {
        var type = InAppNotificationType.Parse("trainer.invitation.sent");

        type.Should().Be(InAppNotificationTypes.InvitationSent);
    }

    [Test]
    public void Parse_WithUnknownValue_Throws()
    {
        var act = () => InAppNotificationType.Parse("unknown.type");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void TryFromValue_WithKnownValue_ReturnsTrue()
    {
        var result = InAppNotificationTypes.TryFromValue("trainer.invitation.accepted", out var type);

        result.Should().BeTrue();
        type.Should().Be(InAppNotificationTypes.InvitationAccepted);
    }

    [Test]
    public void TryFromValue_WithUnknownValue_ReturnsFalse()
    {
        var result = InAppNotificationTypes.TryFromValue("unknown.type", out var type);

        result.Should().BeFalse();
        type.Should().Be(default(InAppNotificationType));
    }

    [Test]
    public void TryFromValue_WithNull_ReturnsFalse()
    {
        var result = InAppNotificationTypes.TryFromValue(null, out var type);

        result.Should().BeFalse();
        type.Should().Be(default(InAppNotificationType));
    }

    [Test]
    public void JsonConverter_SerializesAndDeserializesRegisteredType()
    {
        var json = JsonSerializer.Serialize(InAppNotificationTypes.InvitationRejected);
        var type = JsonSerializer.Deserialize<InAppNotificationType>(json);

        json.Should().Be("\"trainer.invitation.rejected\"");
        type.Should().Be(InAppNotificationTypes.InvitationRejected);
    }

    [Test]
    public void JsonConverter_WithEmptyString_ThrowsJsonException()
    {
        var act = () => JsonSerializer.Deserialize<InAppNotificationType>("\"\"");

        act.Should().Throw<JsonException>();
    }
}
