using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Services;
using System.Globalization;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class WelcomeEmailTemplateComposerTests
{
    private string _templateRootPath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _templateRootPath = Path.Combine(Path.GetTempPath(), $"lgym-welcome-email-templates-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_templateRootPath, "Welcome"));
        File.WriteAllText(
            Path.Combine(_templateRootPath, "Welcome", "en.email"),
            "Subject: Welcome {{UserName}}\n---\nHi {{UserName}}!");
        File.WriteAllText(
            Path.Combine(_templateRootPath, "Welcome", "pl.email"),
            "Subject: Witaj {{UserName}}\n---\nCześć {{UserName}}!");
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
    public void ComposeWelcome_ShouldUsePolishTemplate_WhenLanguageIsPl()
    {
        var composer = CreateComposer();
        var payload = new WelcomeEmailPayload
        {
            UserId = Guid.NewGuid(),
            UserName = "Alicja",
            RecipientEmail = "alicja@example.com",
            CultureName = "pl-PL"
        };

        var message = composer.ComposeWelcome(payload);

        Assert.Multiple(() =>
        {
            Assert.That(message.To, Is.EqualTo("alicja@example.com"));
            Assert.That(message.Subject, Is.EqualTo("Witaj Alicja"));
            Assert.That(message.Body, Is.EqualTo("Cześć Alicja!"));
        });
    }

    [Test]
    public void ComposeWelcome_ShouldFallbackToDefaultLanguage_WhenTemplateMissing()
    {
        var composer = CreateComposer();
        var payload = new WelcomeEmailPayload
        {
            UserId = Guid.NewGuid(),
            UserName = "Alex",
            RecipientEmail = "alex@example.com",
            CultureName = "de-DE"
        };

        var message = composer.ComposeWelcome(payload);

        Assert.Multiple(() =>
        {
            Assert.That(message.Subject, Is.EqualTo("Welcome Alex"));
            Assert.That(message.Body, Is.EqualTo("Hi Alex!"));
        });
    }

    private WelcomeEmailTemplateComposer CreateComposer()
    {
        return new WelcomeEmailTemplateComposer(new EmailOptions
        {
            InvitationBaseUrl = "https://app.example.com/invitations",
            TemplateRootPath = _templateRootPath,
            DefaultCulture = CultureInfo.GetCultureInfo("en-US")
        });
    }
}
