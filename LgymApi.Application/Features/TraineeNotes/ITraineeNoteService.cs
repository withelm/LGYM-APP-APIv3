using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.TraineeNotes.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TraineeNotes;

public interface ITraineeNoteService
{
    Task<Result<List<TraineeNoteResult>, AppError>> GetTrainerNotesAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<Result<TraineeNoteResult, AppError>> CreateTrainerNoteAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, UpsertTraineeNoteCommand command, CancellationToken cancellationToken = default);
    Task<Result<TraineeNoteResult, AppError>> UpdateTrainerNoteAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<TraineeNote> noteId, UpsertTraineeNoteCommand command, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> DeleteTrainerNoteAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<TraineeNote> noteId, CancellationToken cancellationToken = default);
    Task<Result<List<TraineeNoteHistoryResult>, AppError>> GetTrainerNoteHistoryAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<TraineeNote> noteId, CancellationToken cancellationToken = default);
    Task<Result<List<TraineeNoteResult>, AppError>> GetVisibleNotesAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default);
    Task<Result<TraineeNoteResult, AppError>> GetVisibleNoteAsync(UserEntity currentTrainee, Id<TraineeNote> noteId, CancellationToken cancellationToken = default);
}
