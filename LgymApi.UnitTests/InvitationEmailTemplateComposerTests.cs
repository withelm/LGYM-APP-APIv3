using LgymApi.Application.Notifications.Models;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Services;
using System.Globalization;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class InvitationEmailTemplateComposerTests
{
    private string _templateRootPath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _templateRootPath = Path.Combine(Path.GetTempPath(), $"lgym-email-templates-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_templateRootPath, "TrainerInvitation"));
        File.WriteAllText(
            Path.Combine(_templateRootPath, "TrainerInvitation", "en.email"),
            "Subject: Trainer invitation from {{TrainerName}}\n---\nInvitation {{InvitationCode}}\nAccept: {{AcceptUrl}}\nReject: {{RejectUrl}}\nExpires: {{ExpiresAt}}");
        File.WriteAllText(
            Path.Combine(_templateRootPath, "TrainerInvitation", "pl.email"),
            "Subject: Zaproszenie od {{TrainerName}}\n---\nKod: {{InvitationCode}}\nAkceptuj: {{AcceptUrl}}\nOdrzuc: {{RejectUrl}}\nWygasa: {{ExpiresAt}}");
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrWhiteSpace(_templateRootPath) && Directory.Exists(_templateRootPath))
        {
            Directory.Delete(_templateRootPath, recursive: true);
        }
    }

    [Test]
    public void ComposeTrainerInvitation_ShouldUsePolishTemplate_WhenLanguageIsPl()
    {
        var composer = CreateComposer();

        var invitationId = Guid.NewGuid();
        var payload = new InvitationEmailPayload
        {
            InvitationId = invitationId,
            InvitationCode = "ABC123XYZ789",
            ExpiresAt = DateTimeOffset.Parse("2026-03-01T10:00:00+00:00"),
            TrainerName = "Coach Mike",
            RecipientEmail = "trainee@example.com",
            CultureName = "pl-PL"
        };

        var message = composer.ComposeTrainerInvitation(payload);

        Assert.Multiple(() =>
        {
            Assert.That(message.To, Is.EqualTo("trainee@example.com"));
            Assert.That(message.Subject, Is.EqualTo("Zaproszenie od Coach Mike"));
            Assert.That(message.Body, Does.Contain("Kod: ABC123XYZ789"));
            Assert.That(message.Body, Does.Contain("Akceptuj:"));
            Assert.That(message.Body, Does.Contain("Wygasa:"));
            Assert.That(message.Body, Does.Contain($"/accept/{invitationId}"));
            Assert.That(message.Body, Does.Contain($"/reject/{invitationId}"));
        });
    }

    [Test]
    public void ComposeTrainerInvitation_ShouldFallbackToDefaultLanguage_WhenTemplateMissing()
    {
        var composer = CreateComposer();

        var invitationId = Guid.NewGuid();
        var payload = new InvitationEmailPayload
        {
            InvitationId = invitationId,
            InvitationCode = "ZZZ999YYY888",
            ExpiresAt = DateTimeOffset.Parse("2026-03-01T10:00:00+00:00"),
            TrainerName = "Coach Jane",
            RecipientEmail = "trainee@example.com",
            CultureName = "de-DE"
        };

        var message = composer.ComposeTrainerInvitation(payload);

        Assert.Multiple(() =>
        {
            Assert.That(message.Subject, Is.EqualTo("Trainer invitation from Coach Jane"));
            Assert.That(message.Body, Does.Contain("Invitation ZZZ999YYY888"));
            Assert.That(message.Body, Does.Contain("Accept:"));
        });
    }

    private TrainerInvitationEmailTemplateComposer CreateComposer()
    {
        return new TrainerInvitationEmailTemplateComposer(new EmailOptions
        {
            InvitationBaseUrl = "https://app.example.com/invitations",
            TemplateRootPath = _templateRootPath,
            DefaultCulture = CultureInfo.GetCultureInfo("en-US")
        });
    }
}
