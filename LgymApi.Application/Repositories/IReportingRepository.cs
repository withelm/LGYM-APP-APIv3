using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IReportingRepository
{
    Task AddTemplateAsync(ReportTemplate template, CancellationToken cancellationToken = default);
    Task<ReportTemplate?> FindTemplateByIdAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task<List<ReportTemplate>> GetTemplatesByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default);
    Task AddRequestAsync(ReportRequest request, CancellationToken cancellationToken = default);
    Task<ReportRequest?> FindRequestByIdAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<List<ReportRequest>> GetPendingRequestsByTraineeIdAsync(Guid traineeId, CancellationToken cancellationToken = default);
    Task AddSubmissionAsync(ReportSubmission submission, CancellationToken cancellationToken = default);
    Task<List<ReportSubmission>> GetSubmissionsByTrainerAndTraineeAsync(Guid trainerId, Guid traineeId, CancellationToken cancellationToken = default);
}
