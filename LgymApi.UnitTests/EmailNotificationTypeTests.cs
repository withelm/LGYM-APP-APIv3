using FluentAssertions;
using System.Text.Json;
using LgymApi.Domain.Notifications;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class EmailNotificationTypeTests
{
    [Test]
    public void Define_WithEmptyValue_ThrowsArgumentException()
    {
        var action = () => EmailNotificationType.Define("");
        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Parse_WithKnownValue_ReturnsExpectedType()
    {
        var parsed = EmailNotificationType.Parse("training.completed");

        parsed.Should().Be(EmailNotificationTypes.TrainingCompleted);
        parsed.ToString().Should().Be("training.completed");
    }

    [Test]
    public void Parse_WithUnknownValue_ThrowsArgumentOutOfRangeException()
    {
        var action = () => EmailNotificationType.Parse("unknown.notification.type");
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void TryFromValue_WithEmptyInput_ReturnsFalse(string? input)
    {
        var found = EmailNotificationTypes.TryFromValue(input, out var parsed);

        found.Should().BeFalse();
        parsed.Should().Be(default(EmailNotificationType));
    }

    [Test]
    public void TryFromValue_WithUnknownValue_ReturnsFalse()
    {
        var found = EmailNotificationTypes.TryFromValue("not.known", out var parsed);

        found.Should().BeFalse();
        parsed.Should().Be(default(EmailNotificationType));
    }

    [Test]
    public void JsonSerialize_WritesNotificationTypeAsString()
    {
        var json = JsonSerializer.Serialize(EmailNotificationTypes.Welcome);

        json.Should().Be("\"user.registration.welcome\"");
    }

    [Test]
    public void JsonDeserialize_WithKnownString_ReturnsTypedValue()
    {
        var parsed = JsonSerializer.Deserialize<EmailNotificationType>("\"trainer.invitation.created\"");

        parsed.Should().Be(EmailNotificationTypes.TrainerInvitation);
    }

    [TestCase("\"\"")]
    [TestCase("\" \"")]
    public void JsonDeserialize_WithEmptyString_ThrowsJsonException(string json)
    {
        var action = () => JsonSerializer.Deserialize<EmailNotificationType>(json);
        action.Should().Throw<JsonException>();
    }
}
