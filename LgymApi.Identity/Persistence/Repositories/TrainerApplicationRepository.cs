using LgymApi.Application.Pagination;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Approval;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Identity.Persistence.Repositories;

internal sealed class TrainerApplicationRepository(IIdentityPersistenceContext context) : ITrainerApplicationRepository
{
    public async Task<Pagination<TrainerApplicationAttempt>> ListAsync(
        int page, int pageSize, bool includeReviewed, CancellationToken cancellationToken)
    {
        var query = ActiveApplications().AsNoTracking();
        if (!includeReviewed)
        {
            query = query.Where(application => application.Status == TrainerApprovalStatus.PendingApproval);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Include(application => application.Applicant)
            .OrderBy(application => application.CreatedAt).ThenBy(application => application.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new Pagination<TrainerApplicationAttempt> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public Task<TrainerApplicationAttempt?> FindLatestAsync(Id<User> id, bool forUpdate, CancellationToken cancellationToken)
    {
        var query = ActiveApplications().Where(application => application.ApplicantId == id)
            .Include(application => application.Applicant);
        return (forUpdate ? query : query.AsNoTracking())
            .OrderByDescending(application => application.Attempt).FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(TrainerApplicationAttempt application) => context.TrainerApplications.Add(application);

    public Task<TrainerApprovalStatus?> GetLatestStatusAsync(Id<User> id, CancellationToken cancellationToken) =>
        context.TrainerApplications.AsNoTracking().Where(application => application.ApplicantId == id)
            .OrderByDescending(application => application.Attempt)
            .Select(application => (TrainerApprovalStatus?)application.Status)
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<TrainerApplicationAttempt> ActiveApplications() =>
        context.TrainerApplications.Where(application => !application.Applicant.IsDeleted);
}
