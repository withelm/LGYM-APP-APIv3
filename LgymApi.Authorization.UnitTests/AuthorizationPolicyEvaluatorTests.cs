using LgymApi.Authorization;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.ValueObjects;
using LgymApi.Platform.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Authorization.UnitTests;

[TestFixture]
public sealed class AuthorizationKernelTests
{
    [Test]
    public void Evaluate_UsesTheClosedRegisteredPolicy()
    {
        var services = new ServiceCollection();
        services.AddAuthorizationPolicy<TestContext, AllowPolicy>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var evaluator = scope.ServiceProvider.GetRequiredService<IAuthorizationEvaluator<TestContext>>();

        Assert.That(evaluator.Evaluate(CreateActor(), new TestContext()), Is.EqualTo(AuthorizationDecision.Allow));
    }

    [Test]
    public void Evaluate_DeniesWhenNoPolicyIsRegistered()
    {
        var evaluator = new AuthorizationPolicyEvaluator<TestContext>([]);

        Assert.That(evaluator.Evaluate(CreateActor(), new TestContext()), Is.EqualTo(AuthorizationDecision.Deny));
    }

    [Test]
    public void Evaluate_PreservesAnExplicitPolicyDenial()
    {
        var evaluator = new AuthorizationPolicyEvaluator<TestContext>([new DenyPolicy()]);

        Assert.That(evaluator.Evaluate(CreateActor(), new TestContext()), Is.EqualTo(AuthorizationDecision.Deny));
    }

    [Test]
    public void AddAuthorizationPolicy_RejectsDuplicateContextDuringComposition()
    {
        var services = new ServiceCollection();
        services.AddAuthorizationPolicy<TestContext, AllowPolicy>();

        var act = () => services.AddAuthorizationPolicy<TestContext, DenyPolicy>();

        Assert.That(() => act(), Throws.InstanceOf<InvalidOperationException>()
            .With.Message.Contains("TestContext").IgnoreCase);
    }

    [Test]
    public void AuthorizationActor_RejectsAnEmptyActorIdentifier()
    {
        var act = () => new AuthorizationActor(Id<ActorReference>.Empty);

        Assert.That(() => act(), Throws.InstanceOf<ArgumentException>());
    }

    private static AuthorizationActor CreateActor() => new(Id<ActorReference>.New());

    private sealed record TestContext;

    private sealed class AllowPolicy : IAuthorizationPolicy<TestContext>
    {
        public AuthorizationDecision Evaluate(AuthorizationActor actor, TestContext context) => AuthorizationDecision.Allow;
    }

    private sealed class DenyPolicy : IAuthorizationPolicy<TestContext>
    {
        public AuthorizationDecision Evaluate(AuthorizationActor actor, TestContext context) => AuthorizationDecision.Deny;
    }
}
