using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Resources;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class ReportingService
{
    public async Task<Result<List<TrainerPhotoTypeResult>, AppError>> GetPhotoTypesAsync(AuthenticatedAccountContext trainer, CancellationToken cancellationToken = default)
    {
        var check = EnsureTrainer(trainer);
        if (check.IsFailure) return Result<List<TrainerPhotoTypeResult>, AppError>.Failure(check.Error);
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await _templatePersistence.LockTrainerDefinitionsAsync(trainer.Id, cancellationToken);
        var types = await ReportTemplateSnapshotFactory.EnsureDefaultsAsync(_templatePersistence, trainer.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<List<TrainerPhotoTypeResult>, AppError>.Success(types.Select(MapPhotoType).ToList());
    }

    public async Task<Result<TrainerPhotoTypeResult, AppError>> CreatePhotoTypeAsync(AuthenticatedAccountContext trainer, string name, CancellationToken cancellationToken = default)
    {
        var check = EnsureTrainer(trainer);
        if (check.IsFailure) return Result<TrainerPhotoTypeResult, AppError>.Failure(check.Error);
        var normalized = NormalizePhotoTypeName(name);
        if (normalized is null) return Result<TrainerPhotoTypeResult, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await _templatePersistence.LockTrainerDefinitionsAsync(trainer.Id, cancellationToken);
        var existing = await ReportTemplateSnapshotFactory.EnsureDefaultsAsync(_templatePersistence, trainer.Id, cancellationToken);
        if (existing.Any(type => PhotoNameMatches(type, normalized)))
            return Result<TrainerPhotoTypeResult, AppError>.Failure(new ReportingConflictError(Messages.ReportFieldValidationFailed));
        var type = new NewTrainerPhotoTypePersistenceModel(Id<TrainerPhotoType>.New(), trainer.Id, null, normalized, PhotoTypeNameMode.TrainerProvided, false, DateTimeOffset.UtcNow);
        await _templatePersistence.AddPhotoTypeAsync(type, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<TrainerPhotoTypeResult, AppError>.Success(MapPhotoType(new TrainerPhotoTypePersistenceModel(type.Id, type.TrainerId, null, type.Name, type.NameMode, false, type.CreatedAt)));
    }

    public async Task<Result<TrainerPhotoTypeResult, AppError>> UpdatePhotoTypeAsync(AuthenticatedAccountContext trainer, Id<TrainerPhotoType> photoTypeId, string? name, bool? archived, CancellationToken cancellationToken = default)
    {
        var check = EnsureTrainer(trainer);
        if (check.IsFailure) return Result<TrainerPhotoTypeResult, AppError>.Failure(check.Error);
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await _templatePersistence.LockTrainerDefinitionsAsync(trainer.Id, cancellationToken);
        var type = await _templatePersistence.FindPhotoTypeAsync(photoTypeId, cancellationToken);
        if (type is null || type.TrainerId != trainer.Id) return Result<TrainerPhotoTypeResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        var normalized = name is null ? type.Name : NormalizePhotoTypeName(name);
        if (normalized is null) return Result<TrainerPhotoTypeResult, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        var all = await _templatePersistence.ListPhotoTypesAsync(trainer.Id, cancellationToken);
        if (all.Any(candidate => candidate.Id != type.Id && PhotoNameMatches(candidate, normalized)))
            return Result<TrainerPhotoTypeResult, AppError>.Failure(new ReportingConflictError(Messages.ReportFieldValidationFailed));
        var isArchived = archived ?? type.IsArchived;
        var nameMode = name is null ? type.NameMode : PhotoTypeNameMode.TrainerProvided;
        await _templatePersistence.UpdatePhotoTypeAsync(type.Id, normalized, NormalizePhotoTypeNameForComparison(normalized), nameMode, isArchived, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<TrainerPhotoTypeResult, AppError>.Success(MapPhotoType(type with { Name = normalized, NameMode = nameMode, IsArchived = isArchived }));
    }

    public async Task<Result<TrainerPhotoTypeResult, AppError>> ResetPhotoTypeNameAsync(AuthenticatedAccountContext trainer, Id<TrainerPhotoType> photoTypeId, CancellationToken cancellationToken = default)
    {
        var check = EnsureTrainer(trainer);
        if (check.IsFailure) return Result<TrainerPhotoTypeResult, AppError>.Failure(check.Error);
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await _templatePersistence.LockTrainerDefinitionsAsync(trainer.Id, cancellationToken);
        var type = await _templatePersistence.FindPhotoTypeAsync(photoTypeId, cancellationToken);
        if (type is null || type.TrainerId != trainer.Id) return Result<TrainerPhotoTypeResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        if (string.IsNullOrWhiteSpace(type.SystemKey)) return Result<TrainerPhotoTypeResult, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));

        var all = await _templatePersistence.ListPhotoTypesAsync(trainer.Id, cancellationToken);
        var restoredLabels = ReportingPhotoTypeLabels.Resolve(type with { Name = type.SystemKey, NameMode = PhotoTypeNameMode.SystemLocalized });
        if (all.Any(candidate => candidate.Id != type.Id &&
            (PhotoNameMatches(candidate, type.SystemKey) || PhotoNameMatches(candidate, restoredLabels.Pl) || PhotoNameMatches(candidate, restoredLabels.En))))
            return Result<TrainerPhotoTypeResult, AppError>.Failure(new ReportingConflictError(Messages.ReportFieldValidationFailed));

        await _templatePersistence.UpdatePhotoTypeAsync(type.Id, type.SystemKey, NormalizePhotoTypeNameForComparison(type.SystemKey), PhotoTypeNameMode.SystemLocalized, type.IsArchived, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<TrainerPhotoTypeResult, AppError>.Success(MapPhotoType(type with { Name = type.SystemKey, NameMode = PhotoTypeNameMode.SystemLocalized }));
    }

    private TrainerPhotoTypeResult MapPhotoType(TrainerPhotoTypePersistenceModel source)
        => _mapper.Map<TrainerPhotoTypePersistenceModel, TrainerPhotoTypeResult>(source);

    private static bool PhotoNameMatches(TrainerPhotoTypePersistenceModel candidate, string name)
    {
        var labels = ReportingPhotoTypeLabels.Resolve(candidate);
        var key = NormalizePhotoTypeNameForComparison(name);
        return NormalizePhotoTypeNameForComparison(candidate.Name) == key
            || NormalizePhotoTypeNameForComparison(labels.Pl) == key || NormalizePhotoTypeNameForComparison(labels.En) == key;
    }

    private static string? NormalizePhotoTypeName(string name)
    {
        var normalized = name?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > 120 ? null : normalized;
    }

    private static string NormalizePhotoTypeNameForComparison(string name) => name.Trim().ToUpperInvariant();
}
