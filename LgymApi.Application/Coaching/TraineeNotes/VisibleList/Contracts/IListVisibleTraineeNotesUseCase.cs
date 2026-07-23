using LgymApi.Application.Coaching.TraineeNotes.Models;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.TraineeNotes.VisibleList;

public interface IListVisibleTraineeNotesUseCase
{
    Task<Result<IReadOnlyList<TraineeNoteReadModel>, AppError>> ExecuteAsync(
        ListVisibleTraineeNotesQuery query,
        CancellationToken cancellationToken = default);
}
