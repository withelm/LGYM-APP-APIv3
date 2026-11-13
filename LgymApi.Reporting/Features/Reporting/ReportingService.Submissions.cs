using System.Text.Json;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.Authorization;
using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Resources;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class ReportingService : IReportingService
{
    public async Task<Result<ReportSubmissionResult, AppError>> SubmitReportRequestAsync(
        AuthenticatedAccountContext currentTrainee,
        Id<ReportRequest> requestId,
        SubmitReportRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        if (requestId.IsEmpty || command.Answers == null)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        }

        var request = await _requestSubmissionPersistence.FindRequestByIdAsync(requestId, cancellationToken);
        if (request == null || request.TraineeId != currentTrainee.Id)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        if (request.Status is not (ReportRequestStatus.Pending or ReportRequestStatus.Expired))
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.ReportRequestNotPending));
        }

        var initialAccess = await _relationshipAccessPersistence.GetAccessAsync(request.TrainerId, request.TraineeId, cancellationToken);
        var initialEpisode = initialAccess.ActiveEpisode;
        if (!initialAccess.HasActiveRelationship || initialEpisode is null
            || request.SourceCoachingEpisodeId is null || request.SourceCoachingEpisodeId != initialEpisode.EpisodeId)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        if (request.Status == ReportRequestStatus.Pending && IsRequestExpired(request.DueAt, DateTimeOffset.UtcNow))
        {
            if (!await _relationshipAccessPersistence.StageActiveEpisodeRevisionCheckAsync(initialEpisode, cancellationToken))
            {
                return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
            }
            await _requestSubmissionPersistence.SetRequestExpiredAsync(request.Id, cancellationToken);
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (_relationshipAccessPersistence.IsActiveEpisodeRevisionConflict(exception))
            {
                return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
            }
            request = request with { Status = ReportRequestStatus.Expired };
        }

        var normalizedAnswers = NormalizeAnswers(command.Answers);
        var effectiveTemplate = ReportTemplateSnapshotFactory.EffectiveTemplate(request);
        var validationAnswers = _reportSubmissionAcceptedProgressCommandFactory.FilterInvalidMeasurementAnswers(
            effectiveTemplate,
            normalizedAnswers);
        var validationResult = ValidateAnswersAgainstTemplate(effectiveTemplate, validationAnswers);
        if (validationResult.IsFailure)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(validationResult.Error);
        }

        var photoValidationResult = await ValidateRequiredPhotosAsync(request, cancellationToken);
        if (photoValidationResult.IsFailure)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(photoValidationResult.Error);
        }

        var submittedAtUtc = DateTimeOffset.UtcNow;
        var relationshipAccess = await _relationshipAccessPersistence.GetAccessAsync(
            request.TrainerId,
            request.TraineeId,
            cancellationToken);
        var activeEpisode = relationshipAccess.ActiveEpisode;
        var sourceEpisodeId = request.SourceCoachingEpisodeId;
        if (!relationshipAccess.HasActiveRelationship
            || activeEpisode is null
            || sourceEpisodeId is null
            || activeEpisode.EpisodeId != sourceEpisodeId
            || !await _relationshipAccessPersistence.StageActiveEpisodeRevisionCheckAsync(
                activeEpisode,
                cancellationToken))
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        var submission = new NewReportSubmissionPersistenceModel(
            Id<ReportSubmission>.New(),
            request.Id,
            currentTrainee.Id,
            JsonSerializer.Serialize(normalizedAnswers),
            submittedAtUtc,
            sourceEpisodeId,
            submittedAtUtc);

        await _requestSubmissionPersistence.AddSubmissionAsync(submission, cancellationToken);
        await _requestSubmissionPersistence.SetRequestSubmittedAsync(request.Id, submittedAtUtc, cancellationToken);

        var acceptedProgressCommand = _reportSubmissionAcceptedProgressCommandFactory.Create(
            effectiveTemplate,
            normalizedAnswers,
            submission.Id,
            request.Id,
            currentTrainee.Id,
            submittedAtUtc,
            sourceEpisodeId);
        if (acceptedProgressCommand != null)
        {
            await _commandOutboxWriter.StageAsync(acceptedProgressCommand, cancellationToken);
        }

        var submittedRequest = request with { Status = ReportRequestStatus.Submitted, SubmittedAt = submittedAtUtc, Template = effectiveTemplate };
        var result = await MapAndHydrateSubmissionAsync(
            currentTrainee,
            ToPersistenceModel(submission, submittedRequest),
            cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (_relationshipAccessPersistence.IsActiveEpisodeRevisionConflict(exception))
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }
        catch (Exception exception) when (IsDuplicateSubmissionException(exception))
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.ReportRequestNotPending));
        }

        await _commandDispatcher.EnqueueAsync(new ReportSubmissionCreatedInAppNotificationCommand
        {
            SubmissionId = submission.Id,
            TrainerId = request.TrainerId,
            TraineeId = currentTrainee.Id,
            TemplateName = effectiveTemplate.Name
        });

        return Result<ReportSubmissionResult, AppError>.Success(result);
    }

    public async Task<Result<ReportSubmissionResult, AppError>> EditSubmissionAnswersAsync(
        AuthenticatedAccountContext currentTrainee,
        Id<ReportSubmission> submissionId,
        SubmitReportRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        if (submissionId.IsEmpty || command.Answers == null)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var submission = await _requestSubmissionPersistence.FindSubmissionForTraineeForUpdateAsync(submissionId, currentTrainee.Id, cancellationToken);
        if (submission == null)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        var assignment = await _recurringAssignmentPersistence.FindByCurrentRequestForUpdateAsync(submission.ReportRequestId, cancellationToken);
        var access = await _relationshipAccessPersistence.GetAccessAsync(submission.ReportRequest.TrainerId, currentTrainee.Id, cancellationToken);
        var facts = new ReportFeedbackFacts(submission, access, assignment);
        if (!ReportFeedbackLifecyclePolicy.Allows(currentTrainee, facts, ReportFeedbackAction.EditAnswers)
            || access.ActiveEpisode is null
            || !await _relationshipAccessPersistence.StageActiveEpisodeRevisionCheckAsync(access.ActiveEpisode, cancellationToken))
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        var template = ReportTemplateSnapshotFactory.EffectiveTemplate(submission.ReportRequest);
        var answers = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var answer in command.Answers)
        {
            if (!answers.TryAdd(answer.Key, answer.Value))
            {
                return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            }
        }

        var stored = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(submission.PayloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!stored.TryAdd(property.Name, property.Value.Clone()))
                {
                    return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
                }
            }
        }
        catch (JsonException)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
        }
        foreach (var field in template.Fields.Where(field => field.Type == ReportFieldType.Measurements))
        {
            var hadValue = stored.TryGetValue(field.Key, out var original);
            var hasValue = answers.TryGetValue(field.Key, out var proposed);
            if (hadValue != hasValue || (hadValue && (!HasUniqueJsonKeys(original)
                || !HasUniqueJsonKeys(proposed) || !JsonElement.DeepEquals(original, proposed))))
            {
                return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            }
            if (hadValue)
            {
                answers[field.Key] = original;
            }
        }

        var validation = ValidateAnswersAgainstTemplate(template, answers, preserveMeasurements: true);
        if (validation.IsFailure)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(validation.Error);
        }
        var photos = await ValidateRequiredPhotosAsync(submission.ReportRequest, cancellationToken);
        if (photos.IsFailure)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(photos.Error);
        }

        var payloadJson = JsonSerializer.Serialize(answers);
        await _requestSubmissionPersistence.UpdateAnswersAsync(submission.Id, payloadJson, cancellationToken);
        var result = await MapAndHydrateSubmissionAsync(currentTrainee, submission with { PayloadJson = payloadJson }, cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (_relationshipAccessPersistence.IsActiveEpisodeRevisionConflict(exception))
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }
        await transaction.CommitAsync(cancellationToken);
        return Result<ReportSubmissionResult, AppError>.Success(result);
    }

    public async Task<Result<ReportSubmissionResult, AppError>> UpdateTrainerFeedbackAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<AccountReference> traineeId,
        Id<ReportSubmission> submissionId,
        UpdateReportSubmissionFeedbackCommand command,
        CancellationToken cancellationToken = default)
    {
        var ownershipCheck = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ownershipCheck.IsFailure)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(ownershipCheck.Error);
        }

        if (submissionId.IsEmpty)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var submission = await _requestSubmissionPersistence.FindSubmissionForTrainerForUpdateAsync(
            submissionId,
            currentTrainer.Id,
            traineeId,
            cancellationToken);
        if (submission == null)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        var assignment = await _recurringAssignmentPersistence.FindByCurrentRequestForUpdateAsync(
            submission.ReportRequestId,
            cancellationToken);
        var relationshipAccess = await _relationshipAccessPersistence.GetAccessAsync(
            currentTrainer.Id,
            traineeId,
            cancellationToken);
        var activeEpisode = relationshipAccess.ActiveEpisode;
        var feedbackFacts = new ReportFeedbackFacts(submission, relationshipAccess, assignment);
        if (!ReportFeedbackLifecyclePolicy.HasCurrentEpisode(feedbackFacts)
            || activeEpisode is null
            || !await _relationshipAccessPersistence.StageActiveEpisodeRevisionCheckAsync(
                activeEpisode,
                cancellationToken))
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        var normalizedFieldComments = NormalizeTrainerFieldComments(command.FieldComments);
        var effectiveTemplate = ReportTemplateSnapshotFactory.EffectiveTemplate(submission.ReportRequest);
        var validationResult = ValidateTrainerFieldComments(effectiveTemplate, normalizedFieldComments);
        if (validationResult.IsFailure)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(validationResult.Error);
        }

        var overallComment = string.IsNullOrWhiteSpace(command.TrainerOverallComment)
            ? null
            : command.TrainerOverallComment.Trim();
        var fieldCommentsJson = normalizedFieldComments.Count == 0
            ? null
            : JsonSerializer.Serialize(normalizedFieldComments);
        var feedbackChanged = !string.Equals(submission.TrainerOverallComment, overallComment, StringComparison.Ordinal)
            || !string.Equals(submission.TrainerFieldCommentsJson, fieldCommentsJson, StringComparison.Ordinal);
        if (feedbackChanged && submission.TrainerFeedbackReadAt.HasValue)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingConflictError(Messages.ReportFeedbackAlreadyRead));
        }
        if (feedbackChanged && !ReportFeedbackLifecyclePolicy.Allows(currentTrainer, feedbackFacts, ReportFeedbackAction.Edit))
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingConflictError(Messages.ReportFeedbackAlreadyRead));
        }

        var hasFeedback = overallComment != null || normalizedFieldComments.Count > 0;
        var feedbackAddedAt = feedbackChanged && hasFeedback ? DateTimeOffset.UtcNow : feedbackChanged ? null : submission.TrainerFeedbackAddedAt;
        var feedbackReadAt = submission.TrainerFeedbackReadAt;

        if (feedbackChanged)
        {
            await _requestSubmissionPersistence.UpdateFeedbackAsync(
                submission.Id,
                new ReportSubmissionFeedbackUpdatePersistenceModel(
                    overallComment,
                    fieldCommentsJson,
                    feedbackAddedAt,
                    feedbackReadAt),
                cancellationToken);

            if (assignment != null)
            {
                await _recurringAssignmentPersistence.UpdateAsync(
                    assignment.Id,
                    ToUpdateModel(assignment with { NextEligibleAt = null }),
                    cancellationToken);
            }
        }

        var result = await MapAndHydrateSubmissionAsync(currentTrainer, submission with
        {
            TrainerOverallComment = overallComment,
            TrainerFieldCommentsJson = fieldCommentsJson,
            TrainerFeedbackAddedAt = feedbackAddedAt,
            TrainerFeedbackReadAt = feedbackReadAt
        }, cancellationToken);

        if (feedbackChanged && hasFeedback)
        {
            await _commandOutboxWriter.StageAsync(new ReportFeedbackAddedInAppNotificationCommand
            {
                SubmissionId = submission.Id,
                TraineeId = traineeId,
                TrainerId = currentTrainer.Id,
                TemplateName = effectiveTemplate.Name,
                TriggeredAt = DateTimeOffset.UtcNow
            }, cancellationToken);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (_relationshipAccessPersistence.IsActiveEpisodeRevisionConflict(exception))
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }
        await transaction.CommitAsync(cancellationToken);

        return Result<ReportSubmissionResult, AppError>.Success(result);
    }

    public async Task<Result<ReportSubmissionResult, AppError>> MarkTrainerFeedbackAsReadAsync(
        AuthenticatedAccountContext currentTrainee,
        Id<ReportSubmission> submissionId,
        CancellationToken cancellationToken = default)
    {
        if (submissionId.IsEmpty)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var submission = await _requestSubmissionPersistence.FindSubmissionForTraineeForUpdateAsync(
            submissionId,
            currentTrainee.Id,
            cancellationToken);
        if (submission == null)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        if (!submission.TrainerFeedbackAddedAt.HasValue)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.DidntFind));
        }

        if (submission.TrainerFeedbackReadAt.HasValue)
        {
            var existingResult = await MapAndHydrateSubmissionAsync(currentTrainee, submission, cancellationToken);
            var existingAssignment = await _recurringAssignmentPersistence.FindByCurrentRequestAsync(submission.ReportRequestId, cancellationToken);
            existingResult.NextRecurringRequest = GetNextRecurringRequest(existingAssignment, submission.TrainerFeedbackReadAt.Value, usePersistedSchedule: true);
            return Result<ReportSubmissionResult, AppError>.Success(existingResult);
        }

        if (submission.ReportRequest.Status != ReportRequestStatus.Submitted)
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new InvalidReportingError(Messages.DidntFind));
        }

        var relationshipAccess = await _relationshipAccessPersistence.GetAccessAsync(
            submission.ReportRequest.TrainerId,
            currentTrainee.Id,
            cancellationToken);
        var assignment = await _recurringAssignmentPersistence.FindByCurrentRequestForUpdateAsync(
            submission.ReportRequestId,
            cancellationToken);
        var activeEpisode = relationshipAccess.ActiveEpisode;
        if (!ReportFeedbackLifecyclePolicy.Allows(currentTrainee, new ReportFeedbackFacts(submission, relationshipAccess, assignment), ReportFeedbackAction.MarkRead)
            || activeEpisode is null
            || !await _relationshipAccessPersistence.StageActiveEpisodeRevisionCheckAsync(
                activeEpisode,
                cancellationToken))
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        var readAt = DateTimeOffset.UtcNow;
        await _requestSubmissionPersistence.UpdateFeedbackAsync(
            submission.Id,
            new ReportSubmissionFeedbackUpdatePersistenceModel(
                submission.TrainerOverallComment,
                submission.TrainerFieldCommentsJson,
                submission.TrainerFeedbackAddedAt,
                readAt),
            cancellationToken);

        await _requestSubmissionPersistence.SetRequestCompletedAsync(submission.ReportRequestId, cancellationToken);

        var nextRecurringRequest = GetNextRecurringRequest(assignment, readAt, usePersistedSchedule: false);
        if (assignment != null && nextRecurringRequest.ScheduledAt.HasValue)
        {
            await _recurringAssignmentPersistence.UpdateAsync(
                assignment.Id,
                ToUpdateModel(assignment with
                {
                    NextEligibleAt = nextRecurringRequest.ScheduledAt
                }),
                cancellationToken);
        }

        var result = await MapAndHydrateSubmissionAsync(
            currentTrainee,
            submission with
            {
                TrainerFeedbackReadAt = readAt,
                ReportRequest = submission.ReportRequest with { Status = ReportRequestStatus.Completed }
            },
            cancellationToken);
        result.NextRecurringRequest = nextRecurringRequest;
        await _commandOutboxWriter.StageAsync(new ReportFeedbackReadInAppNotificationCommand
        {
            SubmissionId = submission.Id,
            TraineeId = currentTrainee.Id,
            TrainerId = submission.ReportRequest.TrainerId,
            TriggeredAt = readAt,
            NextRecurringRequestAt = nextRecurringRequest.ScheduledAt
        }, cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (_relationshipAccessPersistence.IsActiveEpisodeRevisionConflict(exception))
        {
            return Result<ReportSubmissionResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }
        await transaction.CommitAsync(cancellationToken);
        return Result<ReportSubmissionResult, AppError>.Success(result);
    }

}
