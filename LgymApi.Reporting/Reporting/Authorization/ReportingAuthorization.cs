using LgymApi.Authorization.Contracts;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Platform.Contracts;
using LgymApi.Application.Reporting.Persistence;

namespace LgymApi.Application.Reporting.Authorization;

internal enum ReportingAuthorizationAction
{
    ReadSubmission,
    ReadPhoto,
    ManageActiveRequest,
    ManageOwnedDefinition
}

internal sealed record ReportingAuthorizationContext(
    Id<ActorReference> SourceTrainerId,
    Id<ActorReference> TraineeId,
    bool ActorIsTrainer,
    bool HasActiveRelationship,
    bool HasHistoricalSourceRelationship,
    bool IsSubmitted,
    ReportingAuthorizationAction Action);

internal sealed class ReportingAuthorizationPolicy : IAuthorizationPolicy<ReportingAuthorizationContext>
{
    public AuthorizationDecision Evaluate(AuthorizationActor actor, ReportingAuthorizationContext context)
    {
        if (actor.Id == context.TraineeId)
        {
            return context.Action switch
            {
                ReportingAuthorizationAction.ReadSubmission => context.IsSubmitted ? AuthorizationDecision.Allow : AuthorizationDecision.Deny,
                ReportingAuthorizationAction.ReadPhoto => AuthorizationDecision.Allow,
                _ => AuthorizationDecision.Deny
            };
        }

        if (!context.ActorIsTrainer || actor.Id != context.SourceTrainerId)
        {
            return AuthorizationDecision.Deny;
        }

        return context.Action switch
        {
            ReportingAuthorizationAction.ManageOwnedDefinition => AuthorizationDecision.Allow,
            ReportingAuthorizationAction.ManageActiveRequest => context.HasActiveRelationship
                ? AuthorizationDecision.Allow
                : AuthorizationDecision.Deny,
            ReportingAuthorizationAction.ReadSubmission => context.IsSubmitted
                && (context.HasActiveRelationship || context.HasHistoricalSourceRelationship)
                    ? AuthorizationDecision.Allow
                    : AuthorizationDecision.Deny,
            ReportingAuthorizationAction.ReadPhoto => context.HasActiveRelationship
                || (context.IsSubmitted && context.HasHistoricalSourceRelationship)
                    ? AuthorizationDecision.Allow
                    : AuthorizationDecision.Deny,
            _ => AuthorizationDecision.Deny
        };
    }
}

public interface IReportingAuthorizationCoordinator
{
    bool CanManageActiveRequest(AuthenticatedAccountContext actor, Id<AccountReference> traineeId, ReportingRelationshipAccessFact access);
    bool CanManageOwnedDefinition(AuthenticatedAccountContext actor, Id<AccountReference> ownerId);
    bool CanReadSubmission(AuthenticatedAccountContext actor, ReportSubmissionPersistenceModel submission, ReportingRelationshipAccessFact access);
    bool CanReadPhoto(AuthenticatedAccountContext actor, ReportRequestPersistenceModel request, ReportingRelationshipAccessFact access);
}

internal sealed class ReportingAuthorizationCoordinator(
    IAuthorizationEvaluator<ReportingAuthorizationContext> evaluator) : IReportingAuthorizationCoordinator
{
    public bool CanManageActiveRequest(AuthenticatedAccountContext actor, Id<AccountReference> traineeId, ReportingRelationshipAccessFact access)
        => Evaluate(actor, actor.Id, traineeId, access, false, null, ReportingAuthorizationAction.ManageActiveRequest);

    public bool CanManageOwnedDefinition(AuthenticatedAccountContext actor, Id<AccountReference> ownerId)
        => Evaluate(actor, ownerId, Id<AccountReference>.Empty, new ReportingRelationshipAccessFact(false), false, null, ReportingAuthorizationAction.ManageOwnedDefinition);

    public bool CanReadSubmission(AuthenticatedAccountContext actor, ReportSubmissionPersistenceModel submission, ReportingRelationshipAccessFact access)
    {
        var request = submission.ReportRequest;
        return !request.IsDeleted
            && !request.TrainerId.IsEmpty
            && !request.TraineeId.IsEmpty
            && submission.ReportRequestId == request.Id
            && submission.TraineeId == request.TraineeId
            && Evaluate(actor, request.TrainerId, request.TraineeId, access, request.Status == ReportRequestStatus.Submitted, request.CreatedAt, ReportingAuthorizationAction.ReadSubmission);
    }

    public bool CanReadPhoto(AuthenticatedAccountContext actor, ReportRequestPersistenceModel request, ReportingRelationshipAccessFact access)
        => !request.IsDeleted
            && !request.TrainerId.IsEmpty
            && !request.TraineeId.IsEmpty
            && request.Status != ReportRequestStatus.Cancelled
            && Evaluate(actor, request.TrainerId, request.TraineeId, access, request.Status == ReportRequestStatus.Submitted, request.CreatedAt, ReportingAuthorizationAction.ReadPhoto);

    private bool Evaluate(
        AuthenticatedAccountContext actor,
        Id<AccountReference> sourceTrainerId,
        Id<AccountReference> traineeId,
        ReportingRelationshipAccessFact access,
        bool isSubmitted,
        DateTimeOffset? sourceCreatedAt,
        ReportingAuthorizationAction action)
        => evaluator.Evaluate(
                new AuthorizationActor(actor.Id.Rebind<ActorReference>()),
                new ReportingAuthorizationContext(
                    sourceTrainerId.Rebind<ActorReference>(),
                    traineeId.Rebind<ActorReference>(),
                    actor.Roles.Contains(Domain.Security.AuthConstants.Roles.Trainer, StringComparer.Ordinal),
                    access.HasActiveRelationship,
                    sourceCreatedAt.HasValue && access.CanReadTrainerAuthoredRecordAt(sourceCreatedAt.Value),
                    isSubmitted,
                    action))
            .IsAllowed;
}
