using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class TraineeNoteRepository : ITraineeNoteRepository
{
    private readonly AppDbContext _dbContext;

    public TraineeNoteRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddNoteAsync(TraineeNote note, CancellationToken cancellationToken = default)
        => _dbContext.TraineeNotes.AddAsync(note, cancellationToken).AsTask();

    public Task AddHistoryEntryAsync(TraineeNoteHistory historyEntry, CancellationToken cancellationToken = default)
        => _dbContext.TraineeNoteHistories.AddAsync(historyEntry, cancellationToken).AsTask();

    public Task<TraineeNote?> FindNoteByIdAsync(Id<TraineeNote> noteId, CancellationToken cancellationToken = default)
        => _dbContext.TraineeNotes.FirstOrDefaultAsync(x => x.Id == noteId, cancellationToken);

    public Task<List<TraineeNote>> GetNotesByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default)
        => _dbContext.TraineeNotes
            .Where(x => x.TrainerId == trainerId && x.TraineeId == traineeId && !x.IsDeleted)
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.LastUpdatedAt)
            .ToListAsync(cancellationToken);

    public Task<List<TraineeNote>> GetVisibleNotesByTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default)
        => _dbContext.TraineeNotes
            .Where(x => x.TraineeId == traineeId && x.VisibleToTrainee && !x.IsDeleted)
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.LastUpdatedAt)
            .ToListAsync(cancellationToken);

    public Task<List<TraineeNoteHistory>> GetNoteHistoryAsync(Id<TraineeNote> noteId, CancellationToken cancellationToken = default)
        => _dbContext.TraineeNoteHistories
            .Where(x => x.TraineeNoteId == noteId && !x.IsDeleted)
            .OrderByDescending(x => x.ChangedAt)
            .ToListAsync(cancellationToken);
}
