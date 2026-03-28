using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Reporting;

public interface IReportingService
{
    Task<ReportTemplateResult> CreateTemplateAsync(UserEntity currentTrainer, CreateReportTemplateCommand command, CancellationToken cancellationToken = default);
    Task<List<ReportTemplateResult>> GetTrainerTemplatesAsync(UserEntity currentTrainer, CancellationToken cancellationToken = default);
    Task<ReportTemplateResult> GetTrainerTemplateAsync(UserEntity currentTrainer, Id<ReportTemplate> templateId, CancellationToken cancellationToken = default);
    Task<ReportTemplateResult> UpdateTemplateAsync(UserEntity currentTrainer, Id<ReportTemplate> templateId, CreateReportTemplateCommand command, CancellationToken cancellationToken = default);
    Task DeleteTemplateAsync(UserEntity currentTrainer, Id<ReportTemplate> templateId, CancellationToken cancellationToken = default);

    Task<ReportRequestResult> CreateReportRequestAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CreateReportRequestCommand command, CancellationToken cancellationToken = default);
    Task<List<ReportRequestResult>> GetPendingRequestsForTraineeAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default);
    Task<ReportSubmissionResult> SubmitReportRequestAsync(UserEntity currentTrainee, Id<ReportRequest> requestId, SubmitReportRequestCommand command, CancellationToken cancellationToken = default);
    Task<List<ReportSubmissionResult>> GetTraineeSubmissionsAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
}
