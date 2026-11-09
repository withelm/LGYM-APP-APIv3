using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Resources;

namespace LgymApi.Application.WorkoutProgress.Errors;

public sealed class ExerciseMediaOperationError(string disposition, int statusCode) : AppError
{
    public const string Rejected = "rejected";
    public const string Unresolved = "unresolved";
    public const string Conflict = "conflict";
    public const string SourceRequired = "sourceRequired";
    public string Disposition { get; } = disposition;
    public override string Message => statusCode == 409 ? Messages.ExerciseMediaConflict : statusCode == 503 ? Messages.ExerciseMediaRetry : Messages.ExerciseMediaInvalid;
    public override int HttpStatusCode => statusCode;
}

public sealed class ExerciseMediaRetryError : AppError
{
    public override string Message => Messages.ExerciseMediaRetry;
    public override int HttpStatusCode => 503;
}
