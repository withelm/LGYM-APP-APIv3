using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class TrainerRelationshipRepository : ITrainerRelationshipRepository
{
    private readonly AppDbContext _dbContext;

    public TrainerRelationshipRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddInvitationAsync(TrainerInvitation invitation, CancellationToken cancellationToken = default)
    {
        await _dbContext.TrainerInvitations.AddAsync(invitation, cancellationToken);
    }

    public Task<TrainerInvitation?> FindInvitationByIdAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerInvitations
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken);
    }

    public Task<TrainerInvitation?> FindPendingInvitationAsync(Guid trainerId, Guid traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerInvitations
            .Where(i => i.TrainerId == trainerId && i.TraineeId == traineeId && i.Status == TrainerInvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<TrainerInvitation>> GetInvitationsByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerInvitations
            .Where(i => i.TrainerId == trainerId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasActiveLinkForTraineeAsync(Guid traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerTraineeLinks.AnyAsync(l => l.TraineeId == traineeId, cancellationToken);
    }

    public Task<TrainerTraineeLink?> FindActiveLinkByTrainerAndTraineeAsync(Guid trainerId, Guid traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerTraineeLinks
            .FirstOrDefaultAsync(l => l.TrainerId == trainerId && l.TraineeId == traineeId, cancellationToken);
    }

    public Task<TrainerTraineeLink?> FindActiveLinkByTraineeIdAsync(Guid traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainerTraineeLinks
            .FirstOrDefaultAsync(l => l.TraineeId == traineeId, cancellationToken);
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
}
