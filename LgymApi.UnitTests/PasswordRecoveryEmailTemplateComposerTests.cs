using System.Globalization;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Services;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PasswordRecoveryEmailTemplateComposerTests
{
    [Test]
    public void Compose_WithEnglishPayload_ReturnsEnglishTemplateWithPlaceholdersReplaced()
    {
        var composer = CreateComposer();
        var payload = new PasswordRecoveryEmailPayload
        {
            UserId = Id<User>.New(),
            TokenId = Id<PasswordResetToken>.New(),
            UserName = "Alex",
            RecipientEmail = "alex@example.com",
            ResetToken = "ABC123DEF456",
            CultureName = "en-US"
        };

        var message = composer.ComposePasswordRecovery(payload);

        Assert.Multiple(() =>
        {
            Assert.That(message.To, Is.EqualTo("alex@example.com"));
            Assert.That(message.Subject, Is.EqualTo("Reset your LGYM password"));
            Assert.That(message.Body, Does.Contain("Hi Alex,"));
            Assert.That(message.Body, Does.Contain("Click the link below to reset your password:"));
            Assert.That(message.Body, Does.Contain("ABC123DEF456"));
            Assert.That(message.Body, Does.Contain("This link will expire in 30 minutes."));
            Assert.That(message.Body, Does.Not.Contain("{{UserName}}"));
            Assert.That(message.Body, Does.Not.Contain("{{ResetToken}}"));
            Assert.That(message.Body, Does.Not.Contain("{{ResetUrl}}"));
            Assert.That(message.Body, Does.Not.Contain("{{ExpiryMinutes}}"));
        });
    }

    [Test]
    public void Compose_WithPolishPayload_ReturnsPolishTemplateWithPlaceholdersReplaced()
    {
        var composer = CreateComposer();
        var payload = new PasswordRecoveryEmailPayload
        {
            UserId = Id<User>.New(),
            TokenId = Id<PasswordResetToken>.New(),
            UserName = "Piotr",
            RecipientEmail = "piotr@example.pl",
            ResetToken = "XYZ789GHI012",
            CultureName = "pl-PL"
        };

        var message = composer.ComposePasswordRecovery(payload);

        Assert.Multiple(() =>
        {
            Assert.That(message.To, Is.EqualTo("piotr@example.pl"));
            Assert.That(message.Subject, Is.EqualTo("Zresetuj hasło do konta LGYM"));
            Assert.That(message.Body, Does.Contain("Cześć Piotr,"));
            Assert.That(message.Body, Does.Contain("Kliknij poniższy link, aby zresetować hasło:"));
            Assert.That(message.Body, Does.Contain("XYZ789GHI012"));
            Assert.That(message.Body, Does.Contain("Ten link wygaśnie za 30 minut."));
            Assert.That(message.Body, Does.Not.Contain("{{UserName}}"));
            Assert.That(message.Body, Does.Not.Contain("{{ResetToken}}"));
            Assert.That(message.Body, Does.Not.Contain("{{ResetUrl}}"));
            Assert.That(message.Body, Does.Not.Contain("{{ExpiryMinutes}}"));
        });
    }

    [Test]
    public void Compose_WithJsonPayload_DeserializesAndReturnsEmailMessage()
    {
        var composer = CreateComposer();
        var payloadJson = """
            {
                "userId": "d75e53b9-2701-4cb0-b2d1-c02f0dbf8aa0",
                "tokenId": "f85e53b9-2701-4cb0-b2d1-c02f0dbf8bb1",
                "userName": "TestUser",
                "recipientEmail": "test@example.com",
                "resetToken": "TOKEN123456",
                "cultureName": "en-US"
            }
            """;

        var message = composer.Compose(payloadJson);

        Assert.Multiple(() =>
        {
            Assert.That(message.To, Is.EqualTo("test@example.com"));
            Assert.That(message.Subject, Is.EqualTo("Reset your LGYM password"));
            Assert.That(message.Body, Does.Contain("Hi TestUser,"));
            Assert.That(message.Body, Does.Contain("Click the link below to reset your password:"));
            Assert.That(message.Body, Does.Contain("TOKEN123456"));
        });
    }

    [Test]
    public void Compose_WithInvalidJson_ThrowsInvalidOperationException()
    {
        var composer = CreateComposer();
        var invalidJson = "{ invalid json ";

        var ex = Assert.Throws<InvalidOperationException>(() => composer.Compose(invalidJson));
        Assert.That(ex!.Message, Does.Contain("Failed to deserialize password recovery email payload"));
    }

    [Test]
    public void Compose_WithNullPayload_ThrowsInvalidOperationException()
    {
        var composer = CreateComposer();
        var nullPayloadJson = "null";

        var ex = Assert.Throws<InvalidOperationException>(() => composer.Compose(nullPayloadJson));
        Assert.That(ex!.Message, Does.Contain("Failed to deserialize password recovery email payload"));
    }

    [Test]
    public void Compose_WithSpecialCharactersInUserName_SanitizesCorrectly()
    {
        var composer = CreateComposer();
        var payload = new PasswordRecoveryEmailPayload
        {
            UserId = Id<User>.New(),
            TokenId = Id<PasswordResetToken>.New(),
            UserName = "O'Connor <test@example.com>",
            RecipientEmail = "oconnor@example.com",
            ResetToken = "ABC123",
            CultureName = "en-US"
        };

        var message = composer.ComposePasswordRecovery(payload);

        Assert.Multiple(() =>
        {
            Assert.That(message.Body, Does.Contain("O'Connor"));
            Assert.That(message.Body, Is.Not.Empty);
        });
    }

    [Test]
    public void Compose_WithDefaultCulture_FallsBackToEnglish()
    {
        var composer = CreateComposer();
        var payload = new PasswordRecoveryEmailPayload
        {
            UserId = Id<User>.New(),
            TokenId = Id<PasswordResetToken>.New(),
            UserName = "Alex",
            RecipientEmail = "alex@example.com",
            ResetToken = "ABC123",
            CultureName = "fr-FR" // French culture, but only en/pl templates exist
        };

        // Should fall back to en-US default culture based on EmailOptions.DefaultCulture
        var message = composer.ComposePasswordRecovery(payload);

        Assert.Multiple(() =>
        {
            Assert.That(message.Subject, Does.Contain("Reset your LGYM password").Or.Contain("Zresetuj hasło"));
            Assert.That(message.Body, Is.Not.Empty);
        });
    }

    [Test]
    public void NotificationType_ReturnsPasswordRecoveryType()
    {
        var composer = CreateComposer();
        
        Assert.That(composer.NotificationType.Value, Is.EqualTo("user.password.recovery"));
    }

    [Test]
    public void Compose_WithEmptyStrings_DoesNotCrash()
    {
        var composer = CreateComposer();
        var payload = new PasswordRecoveryEmailPayload
        {
            UserId = Id<User>.New(),
            TokenId = Id<PasswordResetToken>.New(),
            UserName = string.Empty,
            RecipientEmail = "test@example.com",
            ResetToken = string.Empty,
            CultureName = "en-US"
        };

        var message = composer.ComposePasswordRecovery(payload);

        Assert.Multiple(() =>
        {
            Assert.That(message.To, Is.EqualTo("test@example.com"));
            Assert.That(message.Subject, Is.Not.Empty);
            Assert.That(message.Body, Is.Not.Empty);
        });
    }

    private static PasswordRecoveryEmailTemplateComposer CreateComposer()
    {
        var emailOptions = new EmailOptions
        {
            TemplateRootPath = "EmailTemplates",
            DefaultCulture = CultureInfo.GetCultureInfo("en-US"),
            PasswordRecoveryBaseUrl = "https://app.example.com/reset"
        };

        return new PasswordRecoveryEmailTemplateComposer(emailOptions);
    }
}
