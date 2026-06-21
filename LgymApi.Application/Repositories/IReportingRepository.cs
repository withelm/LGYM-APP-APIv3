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
    Task<List<ReportRequest>> GetPendingOrExpiredRequestsByTraineeIdAsync(Id<User> traineeId, CancellationToken cancellationToken = default);
    Task AddSubmissionAsync(ReportSubmission submission, CancellationToken cancellationToken = default);
    Task<ReportSubmission?> FindSubmissionByIdForTrainerAsync(Id<ReportSubmission> submissionId, Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<List<ReportSubmission>> GetSubmissionsByTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<List<ReportSubmission>> GetSubmissionsByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<Photo?> FindPhotoByIdAsync(Id<Photo> photoId, CancellationToken cancellationToken = default);
    Task<Photo?> FindActivePhotoByRequestAndViewAsync(Id<ReportRequest> requestId, Domain.Enums.PhotoViewType viewType, CancellationToken cancellationToken = default);
    Task<List<Photo>> GetPhotosByTraineeIdAsync(Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<List<Photo>> GetPhotosByRequestIdAsync(Id<ReportRequest> requestId, CancellationToken cancellationToken = default);
    Task<long> GetActivePhotoStorageBytesAsync(CancellationToken cancellationToken = default);
    Task<int> CountPhotosCreatedSinceAsync(DateTimeOffset sinceUtc, CancellationToken cancellationToken = default);
    Task SavePhotoAsync(Photo photo, CancellationToken cancellationToken = default);
}
