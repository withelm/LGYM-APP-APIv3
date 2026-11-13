using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.Application.Reporting.Authorization;

internal enum ReportFeedbackAction
{
    Edit,
    MarkRead,
    EditAnswers
}

internal sealed record ReportFeedbackFacts(
    ReportSubmissionPersistenceModel Submission,
    ReportingRelationshipAccessFact Access,
    RecurringReportAssignmentPersistenceModel? Assignment);

internal static class ReportFeedbackLifecyclePolicy
{
    public static bool Allows(AuthenticatedAccountContext actor, ReportFeedbackFacts facts, ReportFeedbackAction action)
    {
        var submission = facts.Submission;
        var request = submission.ReportRequest;
        if (!HasCurrentEpisode(facts) || request.Status != ReportRequestStatus.Submitted || submission.TrainerFeedbackReadAt.HasValue)
        {
            return false;
        }

        return action switch
        {
            ReportFeedbackAction.Edit => actor.Id == request.TrainerId
                && actor.Roles.Contains(AuthConstants.Roles.Trainer, StringComparer.Ordinal),
            ReportFeedbackAction.MarkRead => actor.Id == request.TraineeId && submission.TrainerFeedbackAddedAt.HasValue,
            ReportFeedbackAction.EditAnswers => actor.Id == request.TraineeId && submission.HasEverHadFeedback == false && !submission.TrainerFeedbackAddedAt.HasValue,
            _ => false
        };
    }

    public static bool HasCurrentEpisode(ReportFeedbackFacts facts)
    {
        var submission = facts.Submission;
        var request = submission.ReportRequest;
        var episode = facts.Access.ActiveEpisode;
        return !request.IsDeleted && submission.ReportRequestId == request.Id && submission.TraineeId == request.TraineeId
            && facts.Access.HasActiveRelationship && episode is not null
            && submission.SourceCoachingEpisodeId == episode.EpisodeId
            && request.SourceCoachingEpisodeId == episode.EpisodeId
            && (facts.Assignment is null || facts.Assignment.SourceCoachingEpisodeId == episode.EpisodeId);
    }
}
