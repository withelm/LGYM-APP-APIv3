using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed partial class TrainerRelationshipRepository
{
    private IQueryable<TrainerInvitationResult> BuildInvitationBaseQuery(Id<User> trainerId)
    {
        return
            from invitation in _dbContext.TrainerInvitations.AsNoTracking()
            where invitation.TrainerId == trainerId
            join trainee in _dbContext.Users.AsNoTracking() on invitation.TraineeId equals trainee.Id into traineeGroup
            from trainee in traineeGroup.DefaultIfEmpty()
            select new TrainerInvitationResult
            {
                Id = invitation.Id,
                TrainerId = invitation.TrainerId,
                TraineeId = invitation.TraineeId,
                InviteeEmail = invitation.InviteeEmail,
                Code = invitation.Code,
                Status = invitation.Status,
                ExpiresAt = invitation.ExpiresAt,
                RespondedAt = invitation.RespondedAt,
                CreatedAt = invitation.CreatedAt,
                TraineeName = trainee.Name,
                TraineeEmail = trainee.Email
            };
    }

    public async Task AddLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default)
    {
        await _dbContext.TrainerTraineeLinks.AddAsync(link, cancellationToken);
    }

    public Task RemoveLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default)
    {
        _dbContext.TrainerTraineeLinks.Remove(link);
        return Task.CompletedTask;
    }

    internal sealed class DashboardTraineeProjection
    {
        public Id<User> Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? Avatar { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? LinkedAt { get; init; }
        public TrainerInvitationStatus? LastInvitationStatus { get; init; }
        public DateTimeOffset? LastInvitationExpiresAt { get; init; }
        public DateTimeOffset? LastInvitationRespondedAt { get; init; }
        public int StatusOrder { get; init; }
    }
}
