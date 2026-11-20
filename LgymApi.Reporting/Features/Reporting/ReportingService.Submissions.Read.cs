using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.Authorization;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Resources;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class ReportingService
{
    public async Task<Result<ReportSubmissionArchiveResult, AppError>> GetSourceTrainerArchiveAsync(
        AuthenticatedAccountContext currentTrainer, int offset, int limit, CancellationToken cancellationToken = default)
    {
        var trainerCheck = EnsureTrainer(currentTrainer);
        if (trainerCheck.IsFailure)
        {
            return Result<ReportSubmissionArchiveResult, AppError>.Failure(trainerCheck.Error);
        }
        if (offset < 0 || offset > int.MaxValue - 101 || limit is < 1 or > 100)
        {
            return Result<ReportSubmissionArchiveResult, AppError>.Failure(new InvalidReportingError(Messages.FieldRequired));
        }

        // Advance over candidates, not disclosed items: an authorization-filtered page
        // may be empty and still carry a continuation. Each request has bounded work.
        var candidates = await _requestSubmissionPersistence.ListSourceTrainerSubmissionsAsync(
            currentTrainer.Id, offset, limit + 1, cancellationToken);
        var authorized = new List<ReportSubmissionPersistenceModel>();
        var accessByTrainee = new Dictionary<Id<AccountReference>, ReportingRelationshipAccessFact>();
        foreach (var submission in candidates.Take(limit))
        {
            if (!accessByTrainee.TryGetValue(submission.TraineeId, out var access))
            {
                access = await _relationshipAccessPersistence.GetAccessAsync(currentTrainer.Id, submission.TraineeId, cancellationToken);
                accessByTrainee.Add(submission.TraineeId, access);
            }
            if (_authorization.CanReadSubmission(currentTrainer, submission, access))
            {
                authorized.Add(submission);
            }
        }
        var results = _mapper.MapList<ReportSubmissionPersistenceModel, ReportSubmissionResult>(authorized);
        await PopulateFeedbackCapabilitiesForListAsync(currentTrainer, authorized, results, cancellationToken);
        await HydrateSubmissionPhotosAsync(currentTrainer, results, cancellationToken);
        return Result<ReportSubmissionArchiveResult, AppError>.Success(new ReportSubmissionArchiveResult
        {
            Items = results,
            NextPage = candidates.Count > limit ? offset / limit + 2 : null
        });
    }

    public async Task<Result<List<ReportSubmissionResult>, AppError>> GetTraineeSubmissionsAsync(
        AuthenticatedAccountContext currentTrainer,
        Id<AccountReference> traineeId,
        CancellationToken cancellationToken = default)
    {
        var trainerCheck = EnsureTrainer(currentTrainer);
        if (trainerCheck.IsFailure)
        {
            return Result<List<ReportSubmissionResult>, AppError>.Failure(trainerCheck.Error);
        }
        var access = await _relationshipAccessPersistence.GetAccessAsync(currentTrainer.Id, traineeId, cancellationToken);
        if (!access.HasActiveRelationship && access.RelationshipPeriods.Count == 0)
        {
            return Result<List<ReportSubmissionResult>, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }
        var submissions = await _requestSubmissionPersistence.ListSubmissionsByTrainerAndTraineeAsync(
            currentTrainer.Id,
            traineeId,
            cancellationToken);
        var authorizedSubmissions = submissions
            .Where(submission => _authorization.CanReadSubmission(currentTrainer, submission, access))
            .ToList();
        var results = _mapper.MapList<ReportSubmissionPersistenceModel, ReportSubmissionResult>(authorizedSubmissions);
        await PopulateFeedbackCapabilitiesForListAsync(currentTrainer, authorizedSubmissions, results, cancellationToken);
        await HydrateSubmissionPhotosAsync(currentTrainer, results, cancellationToken);
        return Result<List<ReportSubmissionResult>, AppError>.Success(results);
    }

    public async Task<Result<List<ReportSubmissionResult>, AppError>> GetOwnSubmissionsAsync(
        AuthenticatedAccountContext currentTrainee,
        CancellationToken cancellationToken = default)
    {
        var submissions = await _requestSubmissionPersistence.ListSubmissionsByTraineeAsync(currentTrainee.Id, cancellationToken);
        var authorizedSubmissions = submissions
            .Where(submission => _authorization.CanReadSubmission(
                currentTrainee,
                submission,
                new ReportingRelationshipAccessFact(false))).ToList();
        var results = _mapper.MapList<ReportSubmissionPersistenceModel, ReportSubmissionResult>(authorizedSubmissions);
        await PopulateFeedbackCapabilitiesForListAsync(currentTrainee, authorizedSubmissions, results, cancellationToken);
        await HydrateSubmissionPhotosAsync(currentTrainee, results, cancellationToken);
        return Result<List<ReportSubmissionResult>, AppError>.Success(results);
    }

    private ReportSubmissionResult MapSubmission(ReportSubmissionPersistenceModel submission)
        => _mapper.Map<ReportSubmissionPersistenceModel, ReportSubmissionResult>(submission);

    private async Task<ReportSubmissionResult> MapAndHydrateSubmissionAsync(
        AuthenticatedAccountContext currentUser,
        ReportSubmissionPersistenceModel submission,
        CancellationToken cancellationToken)
    {
        var result = MapSubmission(submission);
        await PopulateFeedbackCapabilitiesAsync(currentUser, submission, result, cancellationToken);
        await HydrateSubmissionPhotosAsync(currentUser, [result], cancellationToken);
        return result;
    }

    private async Task PopulateFeedbackCapabilitiesForListAsync(
        AuthenticatedAccountContext actor,
        IReadOnlyList<ReportSubmissionPersistenceModel> submissions,
        IReadOnlyList<ReportSubmissionResult> results,
        CancellationToken cancellationToken)
    {
        if (submissions.Count == 0)
        {
            return;
        }

        var pairs = submissions.Select(submission => (submission.ReportRequest.TrainerId, submission.ReportRequest.TraineeId)).Distinct().ToArray();
        var requestIds = submissions.Select(submission => submission.ReportRequest.Id).Distinct().ToArray();
        var accessByPair = await _relationshipAccessPersistence.GetActiveAccessForPairsAsync(pairs, cancellationToken);
        var assignments = await _recurringAssignmentPersistence.ListByCurrentRequestsAsync(requestIds, cancellationToken);
        var assignmentsByRequest = assignments.ToDictionary(assignment => assignment.CurrentReportRequestId!.Value);
        for (var i = 0; i < submissions.Count; i++)
        {
            var submission = submissions[i];
            var request = submission.ReportRequest;
            accessByPair.TryGetValue((request.TrainerId, request.TraineeId), out var access);
            assignmentsByRequest.TryGetValue(request.Id, out var assignment);
            PopulateFeedbackCapabilities(actor, submission, results[i], access ?? new ReportingRelationshipAccessFact(false), assignment);
        }
    }

    private async Task PopulateFeedbackCapabilitiesAsync(
        AuthenticatedAccountContext actor,
        ReportSubmissionPersistenceModel submission,
        ReportSubmissionResult result,
        CancellationToken cancellationToken)
    {
        var request = submission.ReportRequest;
        var access = await _relationshipAccessPersistence.GetAccessAsync(request.TrainerId, request.TraineeId, cancellationToken);
        var assignment = await _recurringAssignmentPersistence.FindByCurrentRequestAsync(request.Id, cancellationToken);
        PopulateFeedbackCapabilities(actor, submission, result, access, assignment);
    }

    private static void PopulateFeedbackCapabilities(
        AuthenticatedAccountContext actor,
        ReportSubmissionPersistenceModel submission,
        ReportSubmissionResult result,
        ReportingRelationshipAccessFact access,
        RecurringReportAssignmentPersistenceModel? assignment)
    {
        var facts = new ReportFeedbackFacts(submission, access, assignment);
        result.CanEditFeedback = ReportFeedbackLifecyclePolicy.Allows(actor, facts, ReportFeedbackAction.Edit);
        result.CanEditAnswers = ReportFeedbackLifecyclePolicy.Allows(actor, facts, ReportFeedbackAction.EditAnswers);
        result.CanMarkFeedbackRead = ReportFeedbackLifecyclePolicy.Allows(actor, facts, ReportFeedbackAction.MarkRead);
    }

    private static ReportSubmissionPersistenceModel ToPersistenceModel(
        NewReportSubmissionPersistenceModel submission,
        ReportRequestPersistenceModel request)
        => new(
            submission.Id,
            submission.ReportRequestId,
            submission.TraineeId,
            submission.PayloadJson,
            null,
            null,
            null,
            null,
            submission.CreatedAt,
            request,
            submission.SourceCoachingEpisodeId,
            submission.AcceptedAt,
            false);

    private static RecurringReportAssignmentUpdatePersistenceModel ToUpdateModel(
        RecurringReportAssignmentPersistenceModel assignment)
        => new(
            assignment.TemplateId,
            assignment.IntervalValue,
            assignment.IntervalUnit,
            assignment.StartsAt,
            assignment.EndsAt,
            assignment.IsActive,
            assignment.Note,
            assignment.CurrentReportRequestId,
            assignment.LastRequestCreatedAt,
            assignment.NextEligibleAt,
            assignment.IsDeleted,
            assignment.ScheduleRevision);
}
