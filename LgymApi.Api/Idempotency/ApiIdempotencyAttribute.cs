namespace LgymApi.Api.Idempotency;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ApiIdempotencyAttribute : Attribute
{
    public ApiIdempotencyAttribute(string routeTemplate, ApiIdempotencyScopeSource scopeSource)
    {
        RouteTemplate = routeTemplate;
        ScopeSource = scopeSource;
    }

    public string RouteTemplate { get; }

    public ApiIdempotencyScopeSource ScopeSource { get; }
}

public enum ApiIdempotencyScopeSource
{
    AuthenticatedUser = 0,
    NormalizedEmail = 1
}

public static class ApiIdempotencyHeaders
{
    public const string IdempotencyKey = "Idempotency-Key";
}
