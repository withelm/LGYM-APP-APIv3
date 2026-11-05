using LgymApi.Application.Pagination;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Approval;

namespace LgymApi.UnitTests.Fakes;

internal sealed class TrainerApplicationRepositoryStub : ITrainerApplicationRepository
{
    public TrainerApprovalStatus? Status { get; set; }

    public Task<TrainerApprovalStatus?> GetLatestStatusAsync(Id<User> id, CancellationToken cancellationToken) =>
        Task.FromResult(Status);

    public Task<Pagination<TrainerApplicationAttempt>> ListAsync(
        int page, int pageSize, bool includeReviewed, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<TrainerApplicationAttempt?> FindLatestAsync(Id<User> id, bool forUpdate, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public void Add(TrainerApplicationAttempt application) => throw new NotSupportedException();
}
