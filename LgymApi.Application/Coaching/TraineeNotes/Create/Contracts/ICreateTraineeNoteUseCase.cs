using LgymApi.Application.Coaching.TraineeNotes.Models;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.TraineeNotes.Create;

public interface ICreateTraineeNoteUseCase
{
    Task<Result<TraineeNoteReadModel, AppError>> ExecuteAsync(
        CreateTraineeNoteCommand command,
        CancellationToken cancellationToken = default);
}
