using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;

namespace LgymApi.TestUtils;

public sealed class TestEmailSender : IEmailSender
{
    public List<EmailMessage> SentMessages { get; } = new();
    public int FailuresRemaining { get; set; }
    public bool ReturnFalse { get; set; }

    public void Reset()
    {
        SentMessages.Clear();
        FailuresRemaining = 0;
        ReturnFalse = false;
    }

    public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (FailuresRemaining > 0)
        {
            FailuresRemaining -= 1;
            throw new InvalidOperationException("Simulated SMTP failure");
        }

        SentMessages.Add(message);
        return Task.FromResult(!ReturnFalse);
    }
}
