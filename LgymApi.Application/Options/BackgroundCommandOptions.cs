namespace LgymApi.Application.Options;

public sealed class BackgroundCommandOptions
{
    public int ProcessingLeaseTimeoutMinutes { get; set; } = 15;

    /// <summary>
    /// Duration a notification may remain in the Sending state before it is considered
    /// stuck and eligible for recovery. Defaults to 30 seconds. Must be less than 60
    /// seconds so recovery can intervene before downstream SMTP/timeouts mask the failure.
    /// </summary>
    public int EmailSendLeaseSeconds { get; set; } = 30;

    public void Validate()
    {
        if (EmailSendLeaseSeconds <= 0)
        {
            throw new InvalidOperationException("BackgroundCommands:EmailSendLeaseSeconds must be greater than 0.");
        }

        if (EmailSendLeaseSeconds >= 60)
        {
            throw new InvalidOperationException("BackgroundCommands:EmailSendLeaseSeconds must be less than 60 seconds.");
        }
    }
}
