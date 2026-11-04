namespace LgymApi.Authorization.Contracts;

public interface IAuthorizationPolicy<in TContext>
    where TContext : notnull
{
    AuthorizationDecision Evaluate(AuthorizationActor actor, TContext context);
}
