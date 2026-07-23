using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.TraineeNotes.Delete;

public interface IDeleteTraineeNoteUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(
        DeleteTraineeNoteCommand command,
        CancellationToken cancellationToken = default);
}
