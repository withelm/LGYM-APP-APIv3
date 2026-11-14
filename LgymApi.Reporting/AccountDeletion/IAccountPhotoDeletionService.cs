using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Reporting.AccountDeletion;

public interface IAccountPhotoDeletionService
{
    Task StageAsync(Id<AccountReference> accountId, Id<CorrelationScope> generation, CancellationToken cancellationToken = default);
    Task ProcessPendingAsync(CancellationToken cancellationToken = default);
}
