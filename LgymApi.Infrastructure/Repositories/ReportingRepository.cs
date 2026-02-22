using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class ReportingRepository : IReportingRepository
{
    private readonly AppDbContext _dbContext;

    public ReportingRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddTemplateAsync(ReportTemplate template, CancellationToken cancellationToken = default)
    {
        await _dbContext.ReportTemplates.AddAsync(template, cancellationToken);
    }

    public Task<ReportTemplate?> FindTemplateByIdAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ReportTemplates
            .Include(x => x.Fields.OrderBy(f => f.Order).ThenBy(f => f.CreatedAt))
            .FirstOrDefaultAsync(x => x.Id == templateId, cancellationToken);
    }

    public Task<List<ReportTemplate>> GetTemplatesByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ReportTemplates
            .AsNoTracking()
            .Where(x => x.TrainerId == trainerId && !x.IsDeleted)
            .Include(x => x.Fields.OrderBy(f => f.Order).ThenBy(f => f.CreatedAt))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRequestAsync(ReportRequest request, CancellationToken cancellationToken = default)
    {
        await _dbContext.ReportRequests.AddAsync(request, cancellationToken);
    }

    public Task<ReportRequest?> FindRequestByIdAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ReportRequests
            .Include(x => x.Template)
            .ThenInclude(x => x.Fields.OrderBy(f => f.Order).ThenBy(f => f.CreatedAt))
            .Include(x => x.Submission)
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);
    }

    public Task<List<ReportRequest>> GetPendingRequestsByTraineeIdAsync(Guid traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ReportRequests
            .Where(x => x.TraineeId == traineeId && x.Status == ReportRequestStatus.Pending)
            .Include(x => x.Template)
            .ThenInclude(x => x.Fields.OrderBy(f => f.Order).ThenBy(f => f.CreatedAt))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddSubmissionAsync(ReportSubmission submission, CancellationToken cancellationToken = default)
    {
        await _dbContext.ReportSubmissions.AddAsync(submission, cancellationToken);
    }

    public Task<List<ReportSubmission>> GetSubmissionsByTrainerAndTraineeAsync(Guid trainerId, Guid traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ReportSubmissions
            .AsNoTracking()
            .Where(x => x.ReportRequest.TrainerId == trainerId && x.TraineeId == traineeId)
            .Include(x => x.ReportRequest)
            .ThenInclude(x => x.Template)
            .ThenInclude(x => x.Fields.OrderBy(f => f.Order).ThenBy(f => f.CreatedAt))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
