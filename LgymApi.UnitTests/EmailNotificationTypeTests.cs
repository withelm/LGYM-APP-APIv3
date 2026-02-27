using System.Text.Json;
using LgymApi.Domain.Notifications;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class EmailNotificationTypeTests
{
    [Test]
    public void Define_WithEmptyValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => EmailNotificationType.Define(""));
    }

    [Test]
    public void Parse_WithKnownValue_ReturnsExpectedType()
    {
        var parsed = EmailNotificationType.Parse("training.completed");

        Assert.That(parsed, Is.EqualTo(EmailNotificationTypes.TrainingCompleted));
        Assert.That(parsed.ToString(), Is.EqualTo("training.completed"));
    }

    [Test]
    public void Parse_WithUnknownValue_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EmailNotificationType.Parse("unknown.notification.type"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void TryFromValue_WithEmptyInput_ReturnsFalse(string? input)
    {
        var found = EmailNotificationTypes.TryFromValue(input, out var parsed);

        Assert.That(found, Is.False);
        Assert.That(parsed, Is.EqualTo(default(EmailNotificationType)));
    }

    [Test]
    public void TryFromValue_WithUnknownValue_ReturnsFalse()
    {
        var found = EmailNotificationTypes.TryFromValue("not.known", out var parsed);

        Assert.That(found, Is.False);
        Assert.That(parsed, Is.EqualTo(default(EmailNotificationType)));
    }

    [Test]
    public void JsonSerialize_WritesNotificationTypeAsString()
    {
        var json = JsonSerializer.Serialize(EmailNotificationTypes.Welcome);

        Assert.That(json, Is.EqualTo("\"user.registration.welcome\""));
    }

    [Test]
    public void JsonDeserialize_WithKnownString_ReturnsTypedValue()
    {
        var parsed = JsonSerializer.Deserialize<EmailNotificationType>("\"trainer.invitation.created\"");

        Assert.That(parsed, Is.EqualTo(EmailNotificationTypes.TrainerInvitation));
    }

    [TestCase("\"\"")]
    [TestCase("\" \"")]
    public void JsonDeserialize_WithEmptyString_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EmailNotificationType>(json));
    }
}
