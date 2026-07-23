using LgymApi.Application.Coaching.TraineeNotes.Models;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.TraineeNotes.TrainerList;

public interface IListTrainerNotesUseCase
{
    Task<Result<IReadOnlyList<TraineeNoteReadModel>, AppError>> ExecuteAsync(
        ListTrainerNotesQuery query,
        CancellationToken cancellationToken = default);
}
