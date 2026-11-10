using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Resources;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class RecurringReportAssignmentService : IRecurringReportAssignmentService
{
    private readonly IReportTemplatePersistence _templatePersistence;
    private readonly IReportRequestSubmissionPersistence _requestSubmissionPersistence;
    private readonly IRecurringReportAssignmentPersistence _assignmentPersistence;
    private readonly IReportingRelationshipAccessPersistence _relationshipAccessPersistence;
    private readonly IMapper _mapper;
    private readonly ICommandOutboxWriter _commandOutboxWriter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RecurringReportAssignmentService> _logger;

    public RecurringReportAssignmentService(
        IReportTemplatePersistence templatePersistence,
        IReportRequestSubmissionPersistence requestSubmissionPersistence,
        IRecurringReportAssignmentPersistence recurringAssignmentPersistence,
        IReportingRelationshipAccessPersistence relationshipAccessPersistence,
        IMapper mapper,
        ICommandOutboxWriter commandOutboxWriter,
        IUnitOfWork unitOfWork,
        ILogger<RecurringReportAssignmentService> logger)
    {
        _templatePersistence = templatePersistence;
        _requestSubmissionPersistence = requestSubmissionPersistence;
        _assignmentPersistence = recurringAssignmentPersistence;
        _relationshipAccessPersistence = relationshipAccessPersistence;
        _mapper = mapper;
        _commandOutboxWriter = commandOutboxWriter;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<RecurringReportAssignmentResult, AppError>> CreateAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<AccountReference> traineeId,
        UpsertRecurringReportAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateTrainerAndCommandAsync(currentTrainer, traineeId, command, cancellationToken);
        if (validation.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(validation.Error);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var relationshipAccess = await _relationshipAccessPersistence.GetAccessAsync(
            currentTrainer.Id,
            traineeId,
            cancellationToken);
        if (!relationshipAccess.HasActiveRelationship
            || relationshipAccess.ActiveEpisode is null
            || !await _relationshipAccessPersistence.StageActiveEpisodeRevisionCheckAsync(
                relationshipAccess.ActiveEpisode,
                cancellationToken))
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        var assignment = new NewRecurringReportAssignmentPersistenceModel(
            Id<RecurringReportAssignment>.New(),
            currentTrainer.Id,
            traineeId,
            validation.Value.Id,
            command.IntervalValue,
            command.IntervalUnit,
            command.StartsAt,
            command.EndsAt,
            true,
            NormalizeNote(command.Note),
            null,
            null,
            command.StartsAt,
            DateTimeOffset.UtcNow,
            0,
            relationshipAccess.ActiveEpisode.EpisodeId);

        await _assignmentPersistence.AddAsync(assignment, cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (_relationshipAccessPersistence.IsActiveEpisodeRevisionConflict(exception))
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }
        await transaction.CommitAsync(cancellationToken);

        return Result<RecurringReportAssignmentResult, AppError>.Success(
            MapAssignment(ToPersistenceModel(assignment, validation.Value)));
    }

    public async Task<Result<List<RecurringReportAssignmentResult>, AppError>> GetForTraineeAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<AccountReference> traineeId,
        CancellationToken cancellationToken = default)
    {
        var ownershipCheck = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ownershipCheck.IsFailure)
        {
            return Result<List<RecurringReportAssignmentResult>, AppError>.Failure(ownershipCheck.Error);
        }

        var access = await _relationshipAccessPersistence.GetAccessAsync(currentTrainer.Id, traineeId, cancellationToken);
        var assignments = await _assignmentPersistence.ListByTrainerAndTraineeAsync(
            currentTrainer.Id,
            traineeId,
            cancellationToken);
        return Result<List<RecurringReportAssignmentResult>, AppError>.Success(
            _mapper.MapList<RecurringReportAssignmentPersistenceModel, RecurringReportAssignmentResult>(
                assignments.Where(assignment =>
                    access.ActiveEpisode is not null
                    && assignment.SourceCoachingEpisodeId == access.ActiveEpisode.EpisodeId)));
    }

    public async Task<Result<List<RecurringReportAssignmentResult>, AppError>> GetForTraineeAsync(
        AuthenticatedAccountContext currentTrainee,
        CancellationToken cancellationToken = default)
    {
        var assignments = await _assignmentPersistence.ListByTraineeAsync(currentTrainee.Id, cancellationToken);
        var visibleAssignments = new List<RecurringReportAssignmentPersistenceModel>();
        foreach (var assignment in assignments)
        {
            var access = await _relationshipAccessPersistence.GetAccessAsync(
                assignment.TrainerId,
                currentTrainee.Id,
                cancellationToken);
            if (access.ActiveEpisode is not null
                && assignment.SourceCoachingEpisodeId == access.ActiveEpisode.EpisodeId)
            {
                visibleAssignments.Add(assignment);
            }
        }
        return Result<List<RecurringReportAssignmentResult>, AppError>.Success(
            _mapper.MapList<RecurringReportAssignmentPersistenceModel, RecurringReportAssignmentResult>(visibleAssignments));
    }

    public async Task<Result<RecurringReportAssignmentResult, AppError>> UpdateAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<AccountReference> traineeId,
        Id<RecurringReportAssignment> assignmentId,
        UpsertRecurringReportAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateTrainerAndCommandAsync(currentTrainer, traineeId, command, cancellationToken);
        if (validation.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(validation.Error);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var assignmentResult = await GetOwnedAssignmentForUpdateAsync(currentTrainer, traineeId, assignmentId, cancellationToken);
        if (assignmentResult.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(assignmentResult.Error);
        }

        var updated = assignmentResult.Value with
        {
            TemplateId = validation.Value.Id,
            Template = validation.Value,
            IntervalValue = command.IntervalValue,
            IntervalUnit = command.IntervalUnit,
            StartsAt = command.StartsAt,
            EndsAt = command.EndsAt,
            Note = NormalizeNote(command.Note)
        };
        updated = await InvalidatePendingScheduleStateAsync(updated, currentTrainer.Id, cancellationToken);
        updated = updated with { NextEligibleAt = RecalculateNextEligibleAt(updated), ScheduleRevision = updated.ScheduleRevision + 1 };

        await _assignmentPersistence.UpdateAsync(updated.Id, ToUpdateModel(updated), cancellationToken);
        var saveResult = await SaveEpisodeFencedChangesAsync(transaction, cancellationToken);
        if (saveResult.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(saveResult.Error);
        }
        var reloaded = await _assignmentPersistence.FindByIdAsync(updated.Id, cancellationToken) ?? updated;
        await transaction.CommitAsync(cancellationToken);
        return Result<RecurringReportAssignmentResult, AppError>.Success(MapAssignment(reloaded));
    }

    public Task<Result<RecurringReportAssignmentResult, AppError>> PauseAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<AccountReference> traineeId,
        Id<RecurringReportAssignment> assignmentId,
        CancellationToken cancellationToken = default)
        => SetActiveAsync(currentTrainer, traineeId, assignmentId, false, cancellationToken);

    public Task<Result<RecurringReportAssignmentResult, AppError>> ResumeAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<AccountReference> traineeId,
        Id<RecurringReportAssignment> assignmentId,
        CancellationToken cancellationToken = default)
        => SetActiveAsync(currentTrainer, traineeId, assignmentId, true, cancellationToken);

    public async Task<Result<Unit, AppError>> DeleteAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<AccountReference> traineeId,
        Id<RecurringReportAssignment> assignmentId,
        CancellationToken cancellationToken = default)
    {
        var ownershipCheck = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ownershipCheck.IsFailure)
        {
            return Result<Unit, AppError>.Failure(ownershipCheck.Error);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var assignmentResult = await GetOwnedAssignmentForUpdateAsync(currentTrainer, traineeId, assignmentId, cancellationToken);
        if (assignmentResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(assignmentResult.Error);
        }

        var deleted = await InvalidatePendingScheduleStateAsync(assignmentResult.Value, currentTrainer.Id, cancellationToken);
        deleted = deleted with { IsActive = false, IsDeleted = true, ScheduleRevision = deleted.ScheduleRevision + 1 };
        await _assignmentPersistence.UpdateAsync(deleted.Id, ToUpdateModel(deleted), cancellationToken);
        var saveResult = await SaveEpisodeFencedChangesAsync(transaction, cancellationToken);
        if (saveResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(saveResult.Error);
        }
        await transaction.CommitAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private async Task<Result<RecurringReportAssignmentResult, AppError>> SetActiveAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<AccountReference> traineeId,
        Id<RecurringReportAssignment> assignmentId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var ownershipCheck = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ownershipCheck.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(ownershipCheck.Error);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var assignmentResult = await GetOwnedAssignmentForUpdateAsync(currentTrainer, traineeId, assignmentId, cancellationToken);
        if (assignmentResult.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(assignmentResult.Error);
        }

        var updated = await InvalidatePendingScheduleStateAsync(assignmentResult.Value, currentTrainer.Id, cancellationToken);
        updated = updated with { IsActive = isActive, ScheduleRevision = updated.ScheduleRevision + 1 };
        if (isActive)
        {
            updated = updated with { NextEligibleAt = RecalculateNextEligibleAt(updated) };
        }

        await _assignmentPersistence.UpdateAsync(updated.Id, ToUpdateModel(updated), cancellationToken);
        var saveResult = await SaveEpisodeFencedChangesAsync(transaction, cancellationToken);
        if (saveResult.IsFailure)
        {
            return Result<RecurringReportAssignmentResult, AppError>.Failure(saveResult.Error);
        }
        var reloaded = await _assignmentPersistence.FindByIdAsync(updated.Id, cancellationToken) ?? updated;
        await transaction.CommitAsync(cancellationToken);
        return Result<RecurringReportAssignmentResult, AppError>.Success(MapAssignment(reloaded));
    }
}
