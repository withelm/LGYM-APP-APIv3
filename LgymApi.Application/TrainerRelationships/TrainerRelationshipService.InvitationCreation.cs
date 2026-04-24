using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Pagination;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TrainerRelationships;

public sealed partial class TrainerRelationshipService
{
    public async Task<Result<TrainerInvitationResult, AppError>> CreateInvitationAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ensureTrainerResult = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (ensureTrainerResult.IsFailure)
        {
            return Result<TrainerInvitationResult, AppError>.Failure(ensureTrainerResult.Error);
        }

        if (traineeId.IsEmpty)
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired));
        }

        if (currentTrainer.Id == traineeId)
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.CannotInviteYourself));
        }

        var trainee = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)traineeId, cancellationToken);
        if (trainee == null || trainee.IsDeleted)
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        if (await _trainerRelationshipRepository.HasActiveLinkForTraineeAsync(traineeId, cancellationToken))
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.TraineeAlreadyLinked));
        }

        var existingPending = await _trainerRelationshipRepository.FindPendingInvitationAsync(currentTrainer.Id, traineeId, cancellationToken);
        var reusableInvitation = await HandleExistingPendingInvitationAsync(existingPending, cancellationToken);
        if (reusableInvitation != null)
        {
            return Result<TrainerInvitationResult, AppError>.Success(reusableInvitation);
        }

        var invitation = new TrainerInvitation
        {
            Id = Id<TrainerInvitation>.New(),
            TrainerId = currentTrainer.Id,
            TraineeId = traineeId,
            Code = CreateInvitationCode(),
            Status = TrainerInvitationStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        await _trainerRelationshipRepository.AddInvitationAsync(invitation, cancellationToken);
        await _commandDispatcher.EnqueueAsync(new InvitationCreatedCommand { InvitationId = invitation.Id });
        await _commandDispatcher.EnqueueAsync(new TrainerInvitationCreatedInAppNotificationCommand
        {
            TraineeId = traineeId,
            TrainerId = currentTrainer.Id
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<TrainerInvitationResult, AppError>.Success(MapInvitation(invitation));
    }

    public async Task<Result<TrainerInvitationResult, AppError>> CreateInvitationByEmailAsync(
        UserEntity currentTrainer,
        string inviteeEmail,
        string preferredLanguage,
        string preferredTimeZone,
        CancellationToken cancellationToken = default)
    {
        var ensureTrainerResult = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (ensureTrainerResult.IsFailure)
        {
            return Result<TrainerInvitationResult, AppError>.Failure(ensureTrainerResult.Error);
        }

        var normalizedInviteeEmail = new Email(inviteeEmail).Value;
        if (string.Equals(currentTrainer.Email.Value, normalizedInviteeEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.CannotInviteYourself));
        }

        var existingPending = await _trainerRelationshipRepository.FindPendingInvitationByEmailAsync(currentTrainer.Id, normalizedInviteeEmail, cancellationToken);
        if (existingPending != null)
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new TrainerRelationshipConflictError(Messages.InvitationPendingForEmail));
        }

        if (await _trainerRelationshipRepository.IsEmailAlreadyTraineeAsync(currentTrainer.Id, normalizedInviteeEmail, cancellationToken))
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new TrainerRelationshipConflictError(Messages.EmailAlreadyYourTrainee));
        }

        var trainee = await _userRepository.FindByEmailAsync(normalizedInviteeEmail, cancellationToken);

        var invitation = new TrainerInvitation
        {
            Id = Id<TrainerInvitation>.New(),
            TrainerId = currentTrainer.Id,
            InviteeEmail = normalizedInviteeEmail,
            TraineeId = trainee?.Id,
            Code = CreateInvitationCode(),
            Status = TrainerInvitationStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        await _trainerRelationshipRepository.AddInvitationAsync(invitation, cancellationToken);
        await _commandDispatcher.EnqueueAsync(new InvitationCreatedCommand { InvitationId = invitation.Id });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<TrainerInvitationResult, AppError>.Success(MapInvitation(invitation));
    }

    public async Task<Result<List<TrainerInvitationResult>, AppError>> GetTrainerInvitationsAsync(UserEntity currentTrainer, CancellationToken cancellationToken = default)
    {
        var ensureTrainerResult = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (ensureTrainerResult.IsFailure)
        {
            return Result<List<TrainerInvitationResult>, AppError>.Failure(ensureTrainerResult.Error);
        }

        var invitations = await _trainerRelationshipRepository.GetInvitationsByTrainerIdAsync(currentTrainer.Id, cancellationToken);
        var hasUpdates = false;

        foreach (var invitation in invitations)
        {
            if (invitation.Status == TrainerInvitationStatus.Pending && invitation.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                invitation.Status = TrainerInvitationStatus.Expired;
                invitation.RespondedAt = DateTimeOffset.UtcNow;
                hasUpdates = true;
            }
        }

        if (hasUpdates)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<List<TrainerInvitationResult>, AppError>.Success(invitations.Select(MapInvitation).ToList());
    }

    public async Task<Result<List<TrainerInvitationResult>, AppError>> GetPendingInvitationsForTraineeAsync(
        UserEntity currentTrainee,
        CancellationToken cancellationToken = default)
    {
        var invitations = await _trainerRelationshipRepository.GetPendingInvitationsForTraineeAsync(
            currentTrainee.Id,
            currentTrainee.Email.Value,
            cancellationToken);

        return Result<List<TrainerInvitationResult>, AppError>.Success(invitations);
    }

    public async Task<Result<Pagination<TrainerInvitationResult>, AppError>> GetInvitationsPaginatedAsync(UserEntity currentTrainer, FilterInput filterInput, CancellationToken cancellationToken = default)
    {
        var ensureTrainerResult = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (ensureTrainerResult.IsFailure)
        {
            return Result<Pagination<TrainerInvitationResult>, AppError>.Failure(ensureTrainerResult.Error);
        }

        return Result<Pagination<TrainerInvitationResult>, AppError>.Success(
            await _trainerRelationshipRepository.GetInvitationsPaginatedAsync(currentTrainer.Id, filterInput, cancellationToken));
    }

    private async Task<TrainerInvitationResult?> HandleExistingPendingInvitationAsync(TrainerInvitation? existingPending, CancellationToken cancellationToken)
    {
        if (existingPending == null)
        {
            return null;
        }

        if (existingPending.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return MapInvitation(existingPending);
        }

        existingPending.Status = TrainerInvitationStatus.Expired;
        existingPending.RespondedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return null;
    }
}
