using System.Net.Sockets;
using FluentAssertions;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Services;
using MailKit.Net.Smtp;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class SmtpEmailSenderTests
{
    [Test]
    public async Task SendAsync_Should_Return_False_When_Email_Disabled()
    {
        var options = CreateOptions(enabled: false);
        var sender = new SmtpEmailSender(options);

        var message = new EmailMessage { To = "user@lgym.app", Subject = "Subject", Body = "Body" };

        var result = await sender.SendAsync(message, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Test]
    public void SendAsync_Should_Throw_When_Email_Is_Invalid()
    {
        var options = CreateOptions(enabled: true);
        var sender = new SmtpEmailSender(options);

        var message = new EmailMessage { To = "invalid-address", Subject = "Subject", Body = "Body" };

        var act = new Func<Task>(async () =>
            await sender.SendAsync(message, CancellationToken.None));
        var exception = act.Should().ThrowAsync<InvalidOperationException>().GetAwaiter().GetResult().And;

        exception.InnerException.Should().BeOfType<FormatException>();
    }

    [Test]
    public async Task SendAsync_Should_Throw_When_Smtp_Connection_Fails()
    {
        var options = CreateOptions(enabled: true, smtpHost: "localhost", smtpPort: 1, useSsl: true);
        var sender = new SmtpEmailSender(options);

        var message = new EmailMessage { To = "user@lgym.app", Subject = "Subject", Body = "Body" };

        var act = new Func<Task>(() =>
            sender.SendAsync(message, CancellationToken.None));
        await act.Should().ThrowAsync<SocketException>();
    }

    private static EmailOptions CreateOptions(
        bool enabled,
        string smtpHost = "localhost",
        int smtpPort = 25,
        bool useSsl = false)
    {
        return new EmailOptions
        {
            Enabled = enabled,
            DeliveryMode = EmailDeliveryMode.Smtp,
            FromAddress = "no-reply@lgym.app",
            FromName = "LGYM",
            SmtpHost = smtpHost,
            SmtpPort = smtpPort,
            UseSsl = useSsl,
            Username = string.Empty,
            Password = string.Empty
        };
    }
}
