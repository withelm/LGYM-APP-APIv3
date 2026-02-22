using System.Net;
using LgymApi.Application.Notifications.Models;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Services;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class SmtpEmailSenderTests
{
    [Test]
    public async Task SendAsync_ReturnsFalse_WhenEmailFeatureDisabled()
    {
        var sender = new SmtpEmailSender(new EmailOptions
        {
            Enabled = false
        });

        var result = await sender.SendAsync(new EmailMessage
        {
            To = "trainee@example.com",
            Subject = "x",
            Body = "y"
        });

        Assert.That(result, Is.False);
    }

    [Test]
    public void SendAsync_ThrowsInvalidOperationException_WhenRecipientAddressInvalid()
    {
        var sender = new SmtpEmailSender(new EmailOptions
        {
            Enabled = true,
            FromAddress = "coach@example.com",
            FromName = "Coach",
            SmtpHost = "localhost",
            SmtpPort = 2525
        });

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sender.SendAsync(new EmailMessage
            {
                To = "not-an-email",
                Subject = "x",
                Body = "y"
            }));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.InnerException, Is.TypeOf<FormatException>());
            Assert.That(exception.Message, Is.EqualTo("Recipient email address is invalid."));
        });
    }

    [Test]
    public void SendAsync_ThrowsOperationCanceledException_WhenTokenAlreadyCanceled()
    {
        var sender = new SmtpEmailSender(new EmailOptions
        {
            Enabled = true
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await sender.SendAsync(new EmailMessage
            {
                To = "trainee@example.com",
                Subject = "x",
                Body = "y"
            }, cts.Token));
    }
}
