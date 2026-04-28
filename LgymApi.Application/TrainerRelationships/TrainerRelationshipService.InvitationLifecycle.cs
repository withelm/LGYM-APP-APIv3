using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TrainerRelationships;

public sealed partial class TrainerRelationshipService
{
    public async Task<Result<Unit, AppError>> AcceptInvitationAsync(UserEntity currentTrainee, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default)
    {
        if (invitationId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired));
        }

        var invitationResult = await GetInvitationForTraineeAsync(currentTrainee, invitationId, cancellationToken);
        if (invitationResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(invitationResult.Error);
        }

        var invitation = invitationResult.Value;
        if (invitation.Status == TrainerInvitationStatus.Accepted)
        {
            var existing = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(invitation.TrainerId, currentTrainee.Id, cancellationToken);
            if (existing != null)
            {
                return Result<Unit, AppError>.Success(Unit.Value);
            }
        }

        var invitationPendingResult = await EnsureInvitationPendingAsync(invitation, cancellationToken);
        if (invitationPendingResult.IsFailure)
        {
            return invitationPendingResult;
        }

        if (await _trainerRelationshipRepository.HasActiveLinkForTraineeAsync(currentTrainee.Id, cancellationToken))
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.TraineeAlreadyLinked));
        }

        invitation.Status = TrainerInvitationStatus.Accepted;
        invitation.RespondedAt = DateTimeOffset.UtcNow;

        try
        {
            await _trainerRelationshipRepository.AddLinkAsync(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = invitation.TrainerId,
                TraineeId = currentTrainee.Id
            }, cancellationToken);
        }
        catch
        {
            if (await _trainerRelationshipRepository.HasActiveLinkForTraineeAsync(currentTrainee.Id, cancellationToken))
            {
                return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.TraineeAlreadyLinked));
            }

            throw;
        }

        await _commandDispatcher.EnqueueAsync(new InvitationAcceptedCommand { InvitationId = invitation.Id });

        await _commandDispatcher.EnqueueAsync(new TrainerInvitationAcceptedInAppNotificationCommand
        {
            TrainerId = invitation.TrainerId,
            TraineeId = currentTrainee.Id
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> RejectInvitationAsync(UserEntity currentTrainee, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default)
    {
        if (invitationId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired));
        }

        var invitationResult = await GetInvitationForTraineeAsync(currentTrainee, invitationId, cancellationToken);
        if (invitationResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(invitationResult.Error);
        }

        var invitation = invitationResult.Value;
        if (invitation.Status == TrainerInvitationStatus.Rejected)
        {
            return Result<Unit, AppError>.Success(Unit.Value);
        }

        var invitationPendingResult = await EnsureInvitationPendingAsync(invitation, cancellationToken);
        if (invitationPendingResult.IsFailure)
        {
            return invitationPendingResult;
        }

        invitation.Status = TrainerInvitationStatus.Rejected;
        invitation.RespondedAt = DateTimeOffset.UtcNow;

        await _commandDispatcher.EnqueueAsync(new TrainerInvitationRejectedInAppNotificationCommand
        {
            TrainerId = invitation.TrainerId,
            TraineeId = currentTrainee.Id
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> RevokeInvitationAsync(UserEntity currentTrainer, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default)
    {
        var ensureTrainerResult = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (ensureTrainerResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(ensureTrainerResult.Error);
        }

        if (invitationId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired));
        }

        var invitation = await _trainerRelationshipRepository.FindInvitationByIdAsync(invitationId, cancellationToken);
        if (invitation == null || invitation.TrainerId != currentTrainer.Id)
        {
            return Result<Unit, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        if (invitation.Status != TrainerInvitationStatus.Pending)
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.InvitationNoLongerPending));
        }

        invitation.Status = TrainerInvitationStatus.Revoked;
        invitation.RespondedAt = DateTimeOffset.UtcNow;

        await _commandDispatcher.EnqueueAsync(new InvitationRevokedCommand { InvitationId = invitation.Id });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private async Task<Result<TrainerInvitation, AppError>> GetInvitationForTraineeAsync(UserEntity currentTrainee, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken)
    {
        var invitation = await _trainerRelationshipRepository.FindInvitationByIdAsync(invitationId, cancellationToken);
        if (invitation == null)
        {
            return Result<TrainerInvitation, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        if (invitation.TraineeId == null)
        {
            if (!string.Equals(invitation.InviteeEmail, currentTrainee.Email.Value, StringComparison.OrdinalIgnoreCase))
            {
                return Result<TrainerInvitation, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
            }

            invitation.TraineeId = currentTrainee.Id;
        }
        else if (invitation.TraineeId != currentTrainee.Id)
        {
            return Result<TrainerInvitation, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        return Result<TrainerInvitation, AppError>.Success(invitation);
    }

    private async Task<Result<Unit, AppError>> EnsureInvitationPendingAsync(TrainerInvitation invitation, CancellationToken cancellationToken)
    {
        if (invitation.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            if (invitation.Status == TrainerInvitationStatus.Pending)
            {
                invitation.Status = TrainerInvitationStatus.Expired;
                invitation.RespondedAt = DateTimeOffset.UtcNow;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.InvitationExpired));
        }

        if (invitation.Status != TrainerInvitationStatus.Pending)
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.InvitationNoLongerPending));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private static TrainerInvitationResult MapInvitation(TrainerInvitation invitation)
    {
        return new TrainerInvitationResult
        {
            Id = invitation.Id,
            TrainerId = invitation.TrainerId,
            TraineeId = invitation.TraineeId,
            InviteeEmail = invitation.InviteeEmail,
            Code = invitation.Code,
            Status = invitation.Status,
            ExpiresAt = invitation.ExpiresAt,
            RespondedAt = invitation.RespondedAt,
            CreatedAt = invitation.CreatedAt
        };
    }

    private static string CreateInvitationCode()
    {
        return Id<TrainerInvitation>.New().ToString().Replace("-", string.Empty, StringComparison.Ordinal)[..12].ToUpperInvariant();
    }
}

