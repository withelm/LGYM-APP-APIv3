using LgymApi.Application.Coaching.TraineeNotes.Models;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.TraineeNotes.VisibleSingle;

public interface IGetVisibleTraineeNoteUseCase
{
    Task<Result<TraineeNoteReadModel, AppError>> ExecuteAsync(
        GetVisibleTraineeNoteQuery query,
        CancellationToken cancellationToken = default);
}
