namespace LgymApi.Authorization.Contracts;

public sealed record AuthorizationDecision(bool IsAllowed)
{
    public static AuthorizationDecision Allow { get; } = new(true);

    public static AuthorizationDecision Deny { get; } = new(false);
}
