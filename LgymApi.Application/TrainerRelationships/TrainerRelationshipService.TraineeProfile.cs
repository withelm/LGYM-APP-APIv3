using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TrainerRelationships;

public sealed partial class TrainerRelationshipService
{
    public async Task<Result<TraineeTrainerProfileResult, AppError>> GetCurrentTrainerAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default)
    {
        var link = await _trainerRelationshipRepository.FindActiveLinkByTraineeIdAsync(currentTrainee.Id, cancellationToken);
        if (link == null)
        {
            return Result<TraineeTrainerProfileResult, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        var trainer = await _userRepository.FindByIdAsync(link.TrainerId, cancellationToken);
        if (trainer == null)
        {
            return Result<TraineeTrainerProfileResult, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        return Result<TraineeTrainerProfileResult, AppError>.Success(new TraineeTrainerProfileResult
        {
            TrainerId = trainer.Id,
            Name = trainer.Name,
            Email = trainer.Email,
            Avatar = trainer.Avatar,
            LinkedAt = link.CreatedAt
        });
    }
}
