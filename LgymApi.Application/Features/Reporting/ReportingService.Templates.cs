using System.Text.Json;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Resources;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class ReportingService : IReportingService
{
    public async Task<Result<ReportTemplateResult, AppError>> CreateTemplateAsync(
        AuthenticatedAccountContext currentTrainer,
        CreateReportTemplateCommand command,
        CancellationToken cancellationToken = default)
    {
        var trainerCheck = EnsureTrainer(currentTrainer);
        if (trainerCheck.IsFailure)
        {
            return Result<ReportTemplateResult, AppError>.Failure(trainerCheck.Error);
        }

        var validationCheck = ValidateTemplateCommand(command);
        if (validationCheck.IsFailure)
        {
            return Result<ReportTemplateResult, AppError>.Failure(validationCheck.Error);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await _templatePersistence.LockTrainerDefinitionsAsync(currentTrainer.Id, cancellationToken);
        var photoTypeCheck = await ValidatePhotoTypeSelectionsAsync(currentTrainer, command.Fields, cancellationToken);
        if (photoTypeCheck.IsFailure)
        {
            return Result<ReportTemplateResult, AppError>.Failure(photoTypeCheck.Error);
        }

        var createdAt = DateTimeOffset.UtcNow;
        var fields = CreateTemplateFields(command.Fields, createdAt);
        var template = new NewReportTemplatePersistenceModel(
            Id<ReportTemplate>.New(),
            currentTrainer.Id,
            command.Name.Trim(),
            string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            createdAt,
            fields);

        await _templatePersistence.AddAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<ReportTemplateResult, AppError>.Success(
            _mapper.Map<ReportTemplatePersistenceModel, ReportTemplateResult>(ToPersistenceModel(template)));
    }

    public async Task<Result<List<ReportTemplateResult>, AppError>> GetTrainerTemplatesAsync(
        AuthenticatedAccountContext currentTrainer,
        CancellationToken cancellationToken = default)
    {
        var trainerCheck = EnsureTrainer(currentTrainer);
        if (trainerCheck.IsFailure)
        {
            return Result<List<ReportTemplateResult>, AppError>.Failure(trainerCheck.Error);
        }

        var templates = await _templatePersistence.ListByTrainerAsync(currentTrainer.Id, cancellationToken);
        return Result<List<ReportTemplateResult>, AppError>.Success(
            _mapper.MapList<ReportTemplatePersistenceModel, ReportTemplateResult>(templates));
    }

    public async Task<Result<ReportTemplateResult, AppError>> GetTrainerTemplateAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<ReportTemplate> templateId,
        CancellationToken cancellationToken = default)
    {
        var trainerCheck = EnsureTrainer(currentTrainer);
        if (trainerCheck.IsFailure)
        {
            return Result<ReportTemplateResult, AppError>.Failure(trainerCheck.Error);
        }

        var templateResult = await EnsureOwnedTemplateAsync(currentTrainer, templateId, cancellationToken);
        return templateResult.IsFailure
            ? Result<ReportTemplateResult, AppError>.Failure(templateResult.Error)
            : Result<ReportTemplateResult, AppError>.Success(
                _mapper.Map<ReportTemplatePersistenceModel, ReportTemplateResult>(templateResult.Value));
    }

    public async Task<Result<ReportTemplateResult, AppError>> UpdateTemplateAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<ReportTemplate> templateId,
        CreateReportTemplateCommand command,
        CancellationToken cancellationToken = default)
    {
        var trainerCheck = EnsureTrainer(currentTrainer);
        if (trainerCheck.IsFailure)
        {
            return Result<ReportTemplateResult, AppError>.Failure(trainerCheck.Error);
        }

        var validationCheck = ValidateTemplateCommand(command);
        if (validationCheck.IsFailure)
        {
            return Result<ReportTemplateResult, AppError>.Failure(validationCheck.Error);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await _templatePersistence.LockTrainerDefinitionsAsync(currentTrainer.Id, cancellationToken);
        var photoTypeCheck = await ValidatePhotoTypeSelectionsAsync(currentTrainer, command.Fields, cancellationToken);
        if (photoTypeCheck.IsFailure)
        {
            return Result<ReportTemplateResult, AppError>.Failure(photoTypeCheck.Error);
        }

        var templateResult = await EnsureOwnedTemplateAsync(currentTrainer, templateId, cancellationToken);
        if (templateResult.IsFailure)
        {
            return Result<ReportTemplateResult, AppError>.Failure(templateResult.Error);
        }

        var fields = CreateTemplateFields(command.Fields, DateTimeOffset.UtcNow);
        var update = new UpdateReportTemplatePersistenceModel(
            command.Name.Trim(),
            string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            fields);
        await _templatePersistence.UpdateAsync(templateId, update, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var updated = templateResult.Value with
        {
            Name = update.Name,
            Description = update.Description,
            Fields = update.Fields
        };
        return Result<ReportTemplateResult, AppError>.Success(
            _mapper.Map<ReportTemplatePersistenceModel, ReportTemplateResult>(updated));
    }

    public async Task<Result<Unit, AppError>> DeleteTemplateAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<ReportTemplate> templateId,
        CancellationToken cancellationToken = default)
    {
        var trainerCheck = EnsureTrainer(currentTrainer);
        if (trainerCheck.IsFailure)
        {
            return Result<Unit, AppError>.Failure(trainerCheck.Error);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await _templatePersistence.LockTrainerDefinitionsAsync(currentTrainer.Id, cancellationToken);
        var templateResult = await EnsureOwnedTemplateAsync(currentTrainer, templateId, cancellationToken);
        if (templateResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(templateResult.Error);
        }

        await _templatePersistence.MarkDeletedAsync(templateId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private static IReadOnlyList<ReportTemplateFieldPersistenceModel> CreateTemplateFields(
        IEnumerable<ReportTemplateFieldCommand> fields,
        DateTimeOffset createdAt)
        => fields
            .OrderBy(field => field.Order)
            .ThenBy(field => field.Key, StringComparer.OrdinalIgnoreCase)
            .Select(field => new ReportTemplateFieldPersistenceModel(
                Id<ReportTemplateField>.New(),
                field.Key.Trim(),
                field.Label.Trim(),
                field.Type,
                field.IsRequired,
                field.Order,
                NormalizeModuleConfig(field.Type, field.ModuleConfig),
                createdAt))
            .ToList();

    private static ReportTemplatePersistenceModel ToPersistenceModel(NewReportTemplatePersistenceModel template)
        => new(
            template.Id,
            template.TrainerId,
            template.Name,
            template.Description,
            template.CreatedAt,
            false,
            template.Fields);

    private static Result<Unit, AppError> ValidateTemplateCommand(CreateReportTemplateCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || command.Fields.Count == 0)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        }

        if (command.Fields.Any(field => string.IsNullOrWhiteSpace(field.Key) || string.IsNullOrWhiteSpace(field.Label)))
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        }

        if (command.Fields.GroupBy(field => field.Key.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
        }

        foreach (var field in command.Fields)
        {
            if (!Enum.IsDefined(field.Type) || !IsValidModuleConfig(field.Type, field.ModuleConfig))
            {
                return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            }
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private static bool IsValidModuleConfig(ReportFieldType fieldType, JsonElement? moduleConfig)
        => fieldType switch
        {
            ReportFieldType.Photos => ReportingModuleConfigParser.TryNormalizePhotoModuleConfig(moduleConfig, out _, out _),
            ReportFieldType.Measurements => ReportingModuleConfigParser.TryNormalizeMeasurementModuleConfig(moduleConfig, out _, out _),
            ReportFieldType.Text or ReportFieldType.Number or ReportFieldType.Boolean or ReportFieldType.Date => !moduleConfig.HasValue,
            _ => false
        };

    private static string? NormalizeModuleConfig(ReportFieldType fieldType, JsonElement? moduleConfig)
        => fieldType switch
        {
            ReportFieldType.Photos when ReportingModuleConfigParser.TryNormalizePhotoModuleConfig(moduleConfig, out var photos, out _)
                => JsonSerializer.Serialize(photos),
            ReportFieldType.Measurements when ReportingModuleConfigParser.TryNormalizeMeasurementModuleConfig(moduleConfig, out var measurements, out _)
                => JsonSerializer.Serialize(measurements),
            _ => moduleConfig.HasValue ? JsonSerializer.Serialize(moduleConfig.Value) : null
        };

    private async Task<Result<Unit, AppError>> ValidatePhotoTypeSelectionsAsync(
        AuthenticatedAccountContext trainer,
        IEnumerable<ReportTemplateFieldCommand> fields,
        CancellationToken cancellationToken)
    {
        var photoFields = fields.Where(field => field.Type == ReportFieldType.Photos).ToList();
        if (photoFields.Count == 0)
        {
            return Result<Unit, AppError>.Success(Unit.Value);
        }
        var types = await ReportTemplateSnapshotFactory.EnsureDefaultsAsync(_templatePersistence, trainer.Id, cancellationToken);
        var allowed = types.Where(type => !type.IsArchived).Select(type => type.Id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var field in photoFields)
        {
            if (!ReportingModuleConfigParser.TryNormalizePhotoModuleConfig(field.ModuleConfig, out var normalized, out _))
            {
                return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            }
            if (normalized.TryGetProperty("requiredPhotoTypeIds", out var ids)
                && ids.EnumerateArray().Any(id => !allowed.Contains(id.GetString() ?? string.Empty)))
            {
                return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            }
        }
        return Result<Unit, AppError>.Success(Unit.Value);
    }
}
