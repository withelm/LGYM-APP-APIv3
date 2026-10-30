using System.Security.Cryptography;
using System.Text.Json;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

internal sealed record ReportingPhotoTypesMigrationState(
    IReadOnlyList<User> Users,
    IReadOnlyList<ReportTemplate> Templates,
    IReadOnlyList<ReportTemplateField> Fields,
    IReadOnlyList<ReportRequest> Requests,
    IReadOnlyList<ReportRequestDefinitionSnapshot> Snapshots,
    IReadOnlyList<TrainerPhotoType> PhotoTypes,
    IReadOnlyList<Photo> Photos,
    IReadOnlyList<PhotoUploadSession> UploadSessions)
{
    public static async Task<ReportingPhotoTypesMigrationState> LoadAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
        => new(
            await context.Users.IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken),
            await context.ReportTemplates.IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken),
            await context.ReportTemplateFields.IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken),
            await context.ReportRequests.IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken),
            await context.ReportRequestDefinitionSnapshots.IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken),
            await context.TrainerPhotoTypes.IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken),
            await context.Photos.IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken),
            await context.PhotoUploadSessions.IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken));

    public string Fingerprint()
    {
        var canonical = new
        {
            Users = Users.OrderBy(user => user.Id).Select(user => new { Id = user.Id.ToString(), user.IsDeleted }),
            Templates = Templates.OrderBy(template => template.Id).Select(template => new
            {
                Id = template.Id.ToString(),
                TrainerId = template.TrainerId.ToString(),
                template.Name,
                template.Description,
                template.IsDeleted
            }),
            Fields = Fields.OrderBy(field => field.Id).Select(field => new
            {
                Id = field.Id.ToString(),
                TemplateId = field.TemplateId.ToString(),
                field.Key,
                field.Label,
                Type = (int)field.Type,
                field.IsRequired,
                field.Order,
                field.ModuleConfig,
                field.IsDeleted
            }),
            Requests = Requests.OrderBy(request => request.Id).Select(request => new
            {
                Id = request.Id.ToString(),
                TrainerId = request.TrainerId.ToString(),
                TraineeId = request.TraineeId.ToString(),
                TemplateId = request.TemplateId.ToString(),
                request.IsDeleted
            }),
            Snapshots = Snapshots.OrderBy(snapshot => snapshot.Id).Select(snapshot => new
            {
                Id = snapshot.Id.ToString(),
                ReportRequestId = snapshot.ReportRequestId.ToString(),
                snapshot.IsDeleted,
                DefinitionHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(snapshot.DefinitionJson)))
            }),
            PhotoTypes = PhotoTypes.OrderBy(photoType => photoType.Id).Select(photoType => new
            {
                Id = photoType.Id.ToString(),
                TrainerId = photoType.TrainerId.ToString(),
                photoType.SystemKey,
                photoType.Name,
                NameMode = (int)photoType.NameMode,
                photoType.IsArchived,
                photoType.IsDeleted
            }),
            Photos = Photos.OrderBy(photo => photo.Id).Select(photo => new
            {
                Id = photo.Id.ToString(),
                ReportRequestId = photo.ReportRequestId.ToString(),
                OwnerUserId = photo.OwnerUserId.ToString(),
                UploaderUserId = photo.UploaderUserId.ToString(),
                photo.ViewType,
                SourcePhotoTypeId = photo.SourcePhotoTypeId?.ToString(),
                photo.ReportPhotoSlotId,
                photo.IsDeleted
            }),
            UploadSessions = UploadSessions.OrderBy(session => session.Id).Select(session => new
            {
                Id = session.Id.ToString(),
                ReportRequestId = session.ReportRequestId.ToString(),
                Status = (int)session.Status,
                SourcePhotoTypeId = session.SourcePhotoTypeId?.ToString(),
                session.ReportPhotoSlotId,
                session.IsDeleted
            })
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(canonical);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
