using LgymApi.Application.Pagination;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Identity.Approval;

internal interface ITrainerApplicationRepository
{
    Task<Pagination<User>> ListAsync(int page, int pageSize, bool includeReviewed, CancellationToken cancellationToken);
    Task<User?> FindAsync(Id<User> id, bool forUpdate, CancellationToken cancellationToken);
}
