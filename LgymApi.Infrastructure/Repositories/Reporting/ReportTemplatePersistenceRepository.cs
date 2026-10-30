using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories.Reporting;

public sealed class ReportTemplatePersistenceRepository : IReportTemplatePersistence
{
    private readonly AppDbContext _dbContext;

    public ReportTemplatePersistenceRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task LockTrainerDefinitionsAsync(Id<AccountReference> trainerId, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.IsRelational() && _dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Reporting definition locks require a caller-owned transaction.");
        var persistedId = ReportingPersistenceAccountIds.ToPersisted(trainerId);
        // The retained account row is the stable lock anchor even before dictionary initialization.
        // This no-op update changes no account data and follows the recurring-assignment lock pattern.
        await _dbContext.Users.IgnoreQueryFilters().Where(user => user.Id == persistedId)
            .StageUpdateAsync(_dbContext, user => user.UpdatedAt, user => user.UpdatedAt, cancellationToken);
    }

    public Task AddAsync(NewReportTemplatePersistenceModel template, CancellationToken cancellationToken = default)
        => _dbContext.ReportTemplates.AddAsync(CreateEntity(template), cancellationToken).AsTask();

    public async Task<ReportTemplatePersistenceModel?> FindByIdAsync(
        Id<ReportTemplate> templateId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ReportTemplates
            .AsNoTracking()
            .Include(template => template.Fields)
            .FirstOrDefaultAsync(template => template.Id == templateId, cancellationToken);
        return entity is null ? null : ReportingPersistenceProjection.Template(entity);
    }

    public async Task<IReadOnlyList<ReportTemplatePersistenceModel>> ListByTrainerAsync(
        Id<AccountReference> trainerId,
        CancellationToken cancellationToken = default)
    {
        var persistedTrainerId = ReportingPersistenceAccountIds.ToPersisted(trainerId);
        var entities = await _dbContext.ReportTemplates
            .AsNoTracking()
            .Where(template => template.TrainerId == persistedTrainerId && !template.IsDeleted)
            .Include(template => template.Fields.OrderBy(field => field.Order).ThenBy(field => field.CreatedAt))
            .OrderByDescending(template => template.CreatedAt)
            .ToListAsync(cancellationToken);
        return entities.Select(ReportingPersistenceProjection.Template).ToList();
    }

    public async Task UpdateAsync(
        Id<ReportTemplate> templateId,
        UpdateReportTemplatePersistenceModel update,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ReportTemplates
            .Include(template => template.Fields)
            .FirstAsync(template => template.Id == templateId, cancellationToken);
        entity.Name = update.Name;
        entity.Description = update.Description;
        entity.Fields.Clear();
        foreach (var field in update.Fields)
        {
            entity.Fields.Add(CreateFieldEntity(templateId, field));
        }
    }

    public async Task MarkDeletedAsync(Id<ReportTemplate> templateId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ReportTemplates.FirstAsync(template => template.Id == templateId, cancellationToken);
        entity.IsDeleted = true;
    }

    public async Task<IReadOnlyList<TrainerPhotoTypePersistenceModel>> ListPhotoTypesAsync(Id<AccountReference> trainerId, CancellationToken cancellationToken = default)
    {
        var persistedTrainerId = ReportingPersistenceAccountIds.ToPersisted(trainerId);
        var entities = await _dbContext.TrainerPhotoTypes.AsNoTracking()
            .Where(type => type.TrainerId == persistedTrainerId && !type.IsDeleted)
            .OrderBy(type => type.SystemKey == null).ThenBy(type => type.Name)
            .ToListAsync(cancellationToken);
        return entities.Select(ReportingPersistenceProjection.PhotoType).ToList();
    }

    public async Task<TrainerPhotoTypePersistenceModel?> FindPhotoTypeAsync(Id<TrainerPhotoType> photoTypeId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TrainerPhotoTypes.AsNoTracking().FirstOrDefaultAsync(type => type.Id == photoTypeId, cancellationToken);
        return entity is null ? null : ReportingPersistenceProjection.PhotoType(entity);
    }

    public Task AddPhotoTypeAsync(NewTrainerPhotoTypePersistenceModel photoType, CancellationToken cancellationToken = default)
        => _dbContext.TrainerPhotoTypes.AddAsync(new TrainerPhotoType
        {
            Id = photoType.Id,
            TrainerId = ReportingPersistenceAccountIds.ToPersisted(photoType.TrainerId),
            SystemKey = photoType.SystemKey,
            Name = photoType.Name,
            NormalizedName = NormalizePhotoTypeName(photoType.Name),
            NameMode = photoType.NameMode,
            IsArchived = photoType.IsArchived,
            CreatedAt = photoType.CreatedAt
        }, cancellationToken).AsTask();

    public async Task UpdatePhotoTypeAsync(Id<TrainerPhotoType> photoTypeId, string name, string normalizedName, LgymApi.Domain.Enums.PhotoTypeNameMode nameMode, bool isArchived, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TrainerPhotoTypes.FirstAsync(type => type.Id == photoTypeId, cancellationToken);
        entity.Name = name;
        entity.NormalizedName = normalizedName;
        entity.NameMode = nameMode;
        entity.IsArchived = isArchived;
    }

    private static string NormalizePhotoTypeName(string name) => name.Trim().ToUpperInvariant();

    private static ReportTemplate CreateEntity(NewReportTemplatePersistenceModel template)
        => new()
        {
            Id = template.Id,
            TrainerId = ReportingPersistenceAccountIds.ToPersisted(template.TrainerId),
            Name = template.Name,
            Description = template.Description,
            CreatedAt = template.CreatedAt,
            Fields = template.Fields.Select(field => CreateFieldEntity(template.Id, field)).ToList()
        };

    private static ReportTemplateField CreateFieldEntity(
        Id<ReportTemplate> templateId,
        ReportTemplateFieldPersistenceModel field)
        => new()
        {
            Id = field.Id,
            TemplateId = templateId,
            Key = field.Key,
            Label = field.Label,
            Type = field.Type,
            IsRequired = field.IsRequired,
            Order = field.Order,
            ModuleConfig = field.ModuleConfig,
            CreatedAt = field.CreatedAt
        };
}
