using LgymApi.Application.Pagination;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Approval;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Identity.Persistence.Repositories;

internal sealed class TrainerApplicationRepository(IIdentityPersistenceContext context) : ITrainerApplicationRepository
{
    public async Task<Pagination<User>> ListAsync(int page, int pageSize, bool includeReviewed, CancellationToken cancellationToken)
    {
        var query = context.Users.AsNoTracking().Where(user => user.TrainerApprovalStatus != null);
        if (!includeReviewed)
        {
            query = query.Where(user => user.TrainerApprovalStatus == TrainerApprovalStatus.PendingApproval);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(user => user.TrainerApplicationCreatedAt).ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new Pagination<User> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public Task<User?> FindAsync(Id<User> id, bool forUpdate, CancellationToken cancellationToken)
    {
        var query = context.Users.Where(user => user.Id == id && user.TrainerApprovalStatus != null);
        return (forUpdate ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
    }
}
