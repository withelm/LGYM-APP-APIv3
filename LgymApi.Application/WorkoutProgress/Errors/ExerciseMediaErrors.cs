using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Resources;

namespace LgymApi.Application.WorkoutProgress.Errors;

public sealed class InvalidExerciseMediaError : AppError
{
    public override string Message => Messages.ExerciseMediaInvalid;
    public override int HttpStatusCode => 422;
}

public sealed class ExerciseMediaUnavailableError : AppError
{
    public override string Message => Messages.ExerciseMediaUnavailable;
    public override int HttpStatusCode => 503;
}
