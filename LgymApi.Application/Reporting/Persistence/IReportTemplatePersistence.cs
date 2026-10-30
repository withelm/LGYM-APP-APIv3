using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Application.Reporting.Persistence;

public interface IReportTemplatePersistence
{
    Task LockTrainerDefinitionsAsync(Id<AccountReference> trainerId, CancellationToken cancellationToken = default);
    Task AddAsync(NewReportTemplatePersistenceModel template, CancellationToken cancellationToken = default);
    Task<ReportTemplatePersistenceModel?> FindByIdAsync(Id<ReportTemplate> templateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportTemplatePersistenceModel>> ListByTrainerAsync(Id<AccountReference> trainerId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Id<ReportTemplate> templateId, UpdateReportTemplatePersistenceModel update, CancellationToken cancellationToken = default);
    Task MarkDeletedAsync(Id<ReportTemplate> templateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainerPhotoTypePersistenceModel>> ListPhotoTypesAsync(Id<AccountReference> trainerId, CancellationToken cancellationToken = default);
    Task<TrainerPhotoTypePersistenceModel?> FindPhotoTypeAsync(Id<TrainerPhotoType> photoTypeId, CancellationToken cancellationToken = default);
    Task AddPhotoTypeAsync(NewTrainerPhotoTypePersistenceModel photoType, CancellationToken cancellationToken = default);
    Task UpdatePhotoTypeAsync(Id<TrainerPhotoType> photoTypeId, string name, string normalizedName, LgymApi.Domain.Enums.PhotoTypeNameMode nameMode, bool isArchived, CancellationToken cancellationToken = default);
}
