namespace LgymApi.Infrastructure.Services;

public sealed record HangfireJobStateSnapshot(string? StateName, bool Exists, bool IsBroken)
{
    public static HangfireJobStateSnapshot Missing()
    {
        return new HangfireJobStateSnapshot(null, false, true);
    }

    public static HangfireJobStateSnapshot Active(string? stateName)
    {
        return new HangfireJobStateSnapshot(stateName, true, false);
    }

    public static HangfireJobStateSnapshot Broken(string? stateName)
    {
        return new HangfireJobStateSnapshot(stateName, true, true);
    }
}
