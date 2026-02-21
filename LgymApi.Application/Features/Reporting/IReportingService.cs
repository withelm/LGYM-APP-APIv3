using LgymApi.Application.Features.Reporting.Models;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Reporting;

public interface IReportingService
{
    Task<ReportTemplateResult> CreateTemplateAsync(UserEntity currentTrainer, CreateReportTemplateCommand command);
    Task<List<ReportTemplateResult>> GetTrainerTemplatesAsync(UserEntity currentTrainer);
    Task<ReportTemplateResult> GetTrainerTemplateAsync(UserEntity currentTrainer, Guid templateId);
    Task<ReportTemplateResult> UpdateTemplateAsync(UserEntity currentTrainer, Guid templateId, CreateReportTemplateCommand command);
    Task DeleteTemplateAsync(UserEntity currentTrainer, Guid templateId);

    Task<ReportRequestResult> CreateReportRequestAsync(UserEntity currentTrainer, Guid traineeId, CreateReportRequestCommand command);
    Task<List<ReportRequestResult>> GetPendingRequestsForTraineeAsync(UserEntity currentTrainee);
    Task<ReportSubmissionResult> SubmitReportRequestAsync(UserEntity currentTrainee, Guid requestId, SubmitReportRequestCommand command);
    Task<List<ReportSubmissionResult>> GetTraineeSubmissionsAsync(UserEntity currentTrainer, Guid traineeId);
}
