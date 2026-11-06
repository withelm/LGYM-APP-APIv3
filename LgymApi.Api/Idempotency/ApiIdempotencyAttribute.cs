namespace LgymApi.Api.Idempotency;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ApiIdempotencyAttribute : Attribute
{
    public ApiIdempotencyAttribute(
        string routeTemplate,
        ApiIdempotencyScopeSource scopeSource,
        ApiIdempotencyReplayOwner replayOwner = ApiIdempotencyReplayOwner.NoReplay,
        ApiIdempotencyReplayResourceBinding replayResourceBinding = ApiIdempotencyReplayResourceBinding.None,
        string? requiredPermission = null)
    {
        RouteTemplate = routeTemplate;
        ScopeSource = scopeSource;
        ReplayOwner = replayOwner;
        ReplayResourceBinding = replayResourceBinding;
        RequiredPermission = requiredPermission;
    }

    public string RouteTemplate { get; }

    public ApiIdempotencyScopeSource ScopeSource { get; }

    public ApiIdempotencyReplayOwner ReplayOwner { get; }

    public ApiIdempotencyReplayResourceBinding ReplayResourceBinding { get; }

    public string? RequiredPermission { get; }
}

public enum ApiIdempotencyScopeSource
{
    AuthenticatedUser = 0,
    NormalizedEmail = 1
}

public enum ApiIdempotencyReplayOwner
{
    Public = 0,
    Identity = 1,
    Reporting = 2,
    Coaching = 3,
    TrainingPlanning = 4,
    WorkoutProgress = 5,
    Nutrition = 6,
    Notifications = 7,
    NoReplay = 8
}

public enum ApiIdempotencyReplayResourceBinding
{
    None = 0,
    RouteAccount = 1
}

public static class ApiIdempotencyHeaders
{
    public const string IdempotencyKey = "Idempotency-Key";
}
