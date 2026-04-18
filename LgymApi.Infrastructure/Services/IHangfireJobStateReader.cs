namespace LgymApi.Infrastructure.Services;

public interface IHangfireJobStateReader
{
    Task<HangfireJobStateSnapshot> ReadAsync(string schedulerJobId, CancellationToken cancellationToken = default);
}
