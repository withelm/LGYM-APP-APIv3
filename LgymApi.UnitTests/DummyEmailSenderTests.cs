using LgymApi.Application.Notifications.Models;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Services;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class DummyEmailSenderTests
{
    private string _tempDirectory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"lgym-dummy-email-{Guid.NewGuid():N}");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public async Task SendAsync_WritesEmailToConfiguredDirectory()
    {
        var sender = new DummyEmailSender(new EmailOptions
        {
            Enabled = true,
            DeliveryMode = EmailDeliveryMode.Dummy,
            DummyOutputDirectory = _tempDirectory,
            FromAddress = "coach@example.com",
            FromName = "Coach"
        });

        var result = await sender.SendAsync(new EmailMessage
        {
            To = "trainee@example.com",
            Subject = "Invitation",
            Body = "Body content"
        });

        Assert.That(result, Is.True);
        var files = Directory.GetFiles(_tempDirectory, "*.email.txt");
        Assert.That(files, Has.Length.EqualTo(1));

        var content = await File.ReadAllTextAsync(files[0]);
        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("To: trainee@example.com"));
            Assert.That(content, Does.Contain("Subject: Invitation"));
            Assert.That(content, Does.Contain("Body content"));
        });
    }

    [Test]
    public async Task SendAsync_ReturnsFalse_WhenEmailFeatureDisabled()
    {
        var sender = new DummyEmailSender(new EmailOptions
        {
            Enabled = false,
            DeliveryMode = EmailDeliveryMode.Dummy,
            DummyOutputDirectory = _tempDirectory
        });

        var result = await sender.SendAsync(new EmailMessage
        {
            To = "trainee@example.com",
            Subject = "x",
            Body = "y"
        });

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(Directory.Exists(_tempDirectory), Is.False);
        });
    }
}
