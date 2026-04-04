using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Reporting;

public interface IReportingService
{
    Task<Result<ReportTemplateResult, AppError>> CreateTemplateAsync(UserEntity currentTrainer, CreateReportTemplateCommand command, CancellationToken cancellationToken = default);
    Task<Result<List<ReportTemplateResult>, AppError>> GetTrainerTemplatesAsync(UserEntity currentTrainer, CancellationToken cancellationToken = default);
    Task<Result<ReportTemplateResult, AppError>> GetTrainerTemplateAsync(UserEntity currentTrainer, Id<ReportTemplate> templateId, CancellationToken cancellationToken = default);
    Task<Result<ReportTemplateResult, AppError>> UpdateTemplateAsync(UserEntity currentTrainer, Id<ReportTemplate> templateId, CreateReportTemplateCommand command, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> DeleteTemplateAsync(UserEntity currentTrainer, Id<ReportTemplate> templateId, CancellationToken cancellationToken = default);

    Task<Result<ReportRequestResult, AppError>> CreateReportRequestAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CreateReportRequestCommand command, CancellationToken cancellationToken = default);
    Task<Result<List<ReportRequestResult>, AppError>> GetPendingRequestsForTraineeAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default);
    Task<Result<ReportSubmissionResult, AppError>> SubmitReportRequestAsync(UserEntity currentTrainee, Id<ReportRequest> requestId, SubmitReportRequestCommand command, CancellationToken cancellationToken = default);
    Task<Result<List<ReportSubmissionResult>, AppError>> GetTraineeSubmissionsAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
}
