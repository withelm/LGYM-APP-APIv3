using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Resources;

namespace LgymApi.Application.WorkoutProgress.Errors;

public sealed class ExerciseMediaExpiredError : AppError
{
    public override string Message => Messages.ExerciseMediaExpired;
    public override int HttpStatusCode => 410;
}

public sealed class ExerciseMediaRetryError : AppError
{
    public override string Message => Messages.ExerciseMediaRetry;
    public override int HttpStatusCode => 503;
}
