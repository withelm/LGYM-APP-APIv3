using LgymApi.Application.Pagination;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Identity.Approval;

internal interface ITrainerApplicationRepository
{
    Task<Pagination<TrainerApplicationAttempt>> ListAsync(int page, int pageSize, bool includeReviewed, CancellationToken cancellationToken);
    Task<TrainerApplicationAttempt?> FindLatestAsync(Id<User> id, bool forUpdate, CancellationToken cancellationToken);
    Task<TrainerApprovalStatus?> GetLatestStatusAsync(Id<User> id, CancellationToken cancellationToken);
    void Add(TrainerApplicationAttempt application);
}
