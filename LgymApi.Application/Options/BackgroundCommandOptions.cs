namespace LgymApi.Application.Options;

public sealed class BackgroundCommandOptions
{
    public int ProcessingLeaseTimeoutMinutes { get; set; } = 15;
}
