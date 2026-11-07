using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Coaching.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Repositories;
using LgymApi.Resources;

namespace LgymApi.Application.Coaching.Relationships.DetachFromTrainer;

internal sealed class DetachFromTrainerUseCase : IDetachFromTrainerUseCase
{
    private readonly ICoachingEpisodeAuthorizationService _authorization;
    private readonly ICoachingActiveLinkPersistence _activeLinks;
    private readonly ICommandDispatcher _commands;
    private readonly IUnitOfWork _unitOfWork;

    public DetachFromTrainerUseCase(
        ICoachingEpisodeAuthorizationService authorization,
        ICoachingActiveLinkPersistence activeLinks,
        ICommandDispatcher commands,
        IUnitOfWork unitOfWork)
    {
        _authorization = authorization;
        _activeLinks = activeLinks;
        _commands = commands;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit, AppError>> ExecuteAsync(
        DetachFromTrainerCommand command,
        CancellationToken cancellationToken = default)
    {
        var grant = await _authorization.AuthorizeTraineeDetachAndStageAsync(command.TraineeId, cancellationToken);
        if (grant is null)
        {
            return Result<Unit, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        await _activeLinks.RemoveAsync(grant, cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception commitFailure)
        {
            if (!await _activeLinks.TryRecoverCompletedUnlinkAsync(grant, commitFailure, cancellationToken))
            {
                throw;
            }
        }
        await _commands.EnqueueAsync(new TrainerRelationshipEndedInAppNotificationCommand
        {
            TrainerId = grant.TrainerId,
            TraineeId = command.TraineeId
        });

        return Result<Unit, AppError>.Success(Unit.Value);
    }
}
