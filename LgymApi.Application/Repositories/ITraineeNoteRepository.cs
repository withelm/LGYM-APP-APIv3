using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface ITraineeNoteRepository
{
    Task AddNoteAsync(TraineeNote note, CancellationToken cancellationToken = default);
    Task AddHistoryEntryAsync(TraineeNoteHistory historyEntry, CancellationToken cancellationToken = default);
    Task<TraineeNote?> FindNoteByIdAsync(Id<TraineeNote> noteId, CancellationToken cancellationToken = default);
    Task<List<TraineeNote>> GetNotesByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<List<TraineeNote>> GetVisibleNotesByTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<List<TraineeNoteHistory>> GetNoteHistoryAsync(Id<TraineeNote> noteId, CancellationToken cancellationToken = default);
}
