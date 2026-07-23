using LgymApi.Application.Coaching.TraineeNotes.Models;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.TraineeNotes.History;

public interface IGetTraineeNoteHistoryUseCase
{
    Task<Result<IReadOnlyList<TraineeNoteHistoryReadModel>, AppError>> ExecuteAsync(
        GetTraineeNoteHistoryQuery query,
        CancellationToken cancellationToken = default);
}
