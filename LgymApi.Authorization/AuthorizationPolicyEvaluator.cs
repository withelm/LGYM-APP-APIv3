using LgymApi.Authorization.Contracts;

namespace LgymApi.Authorization;

public sealed class AuthorizationPolicyEvaluator<TContext>(IEnumerable<IAuthorizationPolicy<TContext>> policies)
    : IAuthorizationEvaluator<TContext>
    where TContext : notnull
{
    private readonly IAuthorizationPolicy<TContext>? _policy = GetSinglePolicy(policies);

    public AuthorizationDecision Evaluate(AuthorizationActor actor, TContext context)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return _policy?.Evaluate(actor, context) ?? AuthorizationDecision.Deny;
    }

    private static IAuthorizationPolicy<TContext>? GetSinglePolicy(IEnumerable<IAuthorizationPolicy<TContext>> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);

        var configuredPolicies = policies.Take(2).ToArray();
        return configuredPolicies.Length switch
        {
            0 => null,
            1 => configuredPolicies[0],
            _ => throw new InvalidOperationException(
                $"Multiple authorization policies are registered for '{typeof(TContext).FullName}'.")
        };
    }
}
