namespace LgymApi.Authorization.Contracts;

public interface IAuthorizationEvaluator<in TContext>
    where TContext : notnull
{
    AuthorizationDecision Evaluate(AuthorizationActor actor, TContext context);
}
