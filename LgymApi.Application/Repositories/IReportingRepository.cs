using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface IReportingRepository
{
    Task AddTemplateAsync(ReportTemplate template, CancellationToken cancellationToken = default);
    Task<ReportTemplate?> FindTemplateByIdAsync(Id<ReportTemplate> templateId, CancellationToken cancellationToken = default);
    Task<List<ReportTemplate>> GetTemplatesByTrainerIdAsync(Id<User> trainerId, CancellationToken cancellationToken = default);
    Task AddRequestAsync(ReportRequest request, CancellationToken cancellationToken = default);
    Task<ReportRequest?> FindRequestByIdAsync(Id<ReportRequest> requestId, CancellationToken cancellationToken = default);
    Task<List<ReportRequest>> GetPendingRequestsByTraineeIdAsync(Id<User> traineeId, CancellationToken cancellationToken = default);
    Task AddSubmissionAsync(ReportSubmission submission, CancellationToken cancellationToken = default);
    Task<List<ReportSubmission>> GetSubmissionsByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default);
}
