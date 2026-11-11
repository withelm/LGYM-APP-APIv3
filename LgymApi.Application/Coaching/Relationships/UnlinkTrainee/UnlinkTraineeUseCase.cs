using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Coaching.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Repositories;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Resources;

namespace LgymApi.Application.Coaching.Relationships.UnlinkTrainee;

internal sealed class UnlinkTraineeUseCase : IUnlinkTraineeUseCase
{
    private readonly ICoachingRelationshipAccessService _relationshipAccess;
    private readonly ICoachingEpisodeAuthorizationService _authorization;
    private readonly ICoachingActiveLinkPersistence _activeLinks;
    private readonly ICommandDispatcher _commands;
    private readonly ICommandOutboxWriter _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public UnlinkTraineeUseCase(
        ICoachingRelationshipAccessService relationshipAccess,
        ICoachingEpisodeAuthorizationService authorization,
        ICoachingActiveLinkPersistence activeLinks,
        ICommandDispatcher commands,
        ICommandOutboxWriter outbox,
        IUnitOfWork unitOfWork)
    {
        _relationshipAccess = relationshipAccess;
        _authorization = authorization;
        _activeLinks = activeLinks;
        _commands = commands;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit, AppError>> ExecuteAsync(
        UnlinkTraineeCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await _relationshipAccess.GetAccessDecisionAsync(
            command.TrainerId,
            command.TraineeId,
            cancellationToken);
        if (!access.IsTrainer)
        {
            return Result<Unit, AppError>.Failure(new TrainerRelationshipForbiddenError(Messages.TrainerRoleRequired));
        }

        if (command.TraineeId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired));
        }

        if (!access.HasActiveRelationship)
        {
            return Result<Unit, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        var grant = await _authorization.AuthorizeAndStageAsync(
            command.TrainerId,
            command.TrainerId,
            command.TraineeId,
            CoachingEpisodeAuthorizationAction.TrainerUnlink,
            cancellationToken);
        if (grant is null)
        {
            return Result<Unit, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        await _activeLinks.RemoveAsync(grant, cancellationToken);
        var staged = await _outbox.StageAsync(new RelationshipEndedReportCancellationCommand
        {
            EpisodeId = grant.EpisodeId
        }, cancellationToken);
        if (staged.EnvelopeId is null)
        {
            throw new InvalidOperationException("Relationship cancellation command was not staged.");
        }
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception commitFailure)
        {
            if (!await _activeLinks.TryRecoverCompletedUnlinkAsync(grant, staged.EnvelopeId, commitFailure, cancellationToken))
            {
                throw;
            }
        }

        await _commands.EnqueueAsync(new TrainerRelationshipEndedInAppNotificationCommand
        {
            TrainerId = grant.TrainerId,
            TraineeId = grant.TraineeId
        });
        return Result<Unit, AppError>.Success(Unit.Value);
    }
}
