using LgymApi.Application.Features.Reporting.Models;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Reporting;

public interface IReportingService
{
    Task<ReportTemplateResult> CreateTemplateAsync(UserEntity currentTrainer, CreateReportTemplateCommand command, CancellationToken cancellationToken = default);
    Task<List<ReportTemplateResult>> GetTrainerTemplatesAsync(UserEntity currentTrainer, CancellationToken cancellationToken = default);
    Task<ReportTemplateResult> GetTrainerTemplateAsync(UserEntity currentTrainer, Guid templateId, CancellationToken cancellationToken = default);
    Task<ReportTemplateResult> UpdateTemplateAsync(UserEntity currentTrainer, Guid templateId, CreateReportTemplateCommand command, CancellationToken cancellationToken = default);
    Task DeleteTemplateAsync(UserEntity currentTrainer, Guid templateId, CancellationToken cancellationToken = default);

    Task<ReportRequestResult> CreateReportRequestAsync(UserEntity currentTrainer, Guid traineeId, CreateReportRequestCommand command, CancellationToken cancellationToken = default);
    Task<List<ReportRequestResult>> GetPendingRequestsForTraineeAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default);
    Task<ReportSubmissionResult> SubmitReportRequestAsync(UserEntity currentTrainee, Guid requestId, SubmitReportRequestCommand command, CancellationToken cancellationToken = default);
    Task<List<ReportSubmissionResult>> GetTraineeSubmissionsAsync(UserEntity currentTrainer, Guid traineeId, CancellationToken cancellationToken = default);
}
