using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Actions.Contracts;
using LgymApi.BackgroundWorker.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ActionScopeIsolationTests
{
    [Test]
    public void BackgroundActionResolver_ReturnsExactHandlerNames()
    {
        using var provider = BuildProvider();
        var resolver = provider.GetRequiredService<IBackgroundActionResolver>();

        var assertedValue79 = (resolver.GetHandlerTypeNames(typeof(TestCommand))).ToArray();
        Assert.That(assertedValue79, Has.Exactly(1).Items);
        Assert.That(assertedValue79.Single(), Is.EqualTo(typeof(ScopedHandler).FullName));
    }

    [Test]
    public void BackgroundActionResolver_CreatesIsolatedResolutionScopes()
    {
        using var provider = BuildProvider();
        var resolver = provider.GetRequiredService<IBackgroundActionResolver>();

        using var firstScope = resolver.CreateScope(typeof(TestCommand));
        using var secondScope = resolver.CreateScope(typeof(TestCommand));
        var firstHandler = (ScopedHandler)firstScope.ResolveHandler(typeof(ScopedHandler).FullName!);
        var secondHandler = (ScopedHandler)secondScope.ResolveHandler(typeof(ScopedHandler).FullName!);

        Assert.That(firstHandler.Tracker, Is.Not.SameAs(secondHandler.Tracker));
    }

    [Test]
    public void BackgroundActionResolver_RejectsUnknownHandlerName()
    {
        using var provider = BuildProvider();
        var resolver = provider.GetRequiredService<IBackgroundActionResolver>();
        using var scope = resolver.CreateScope(typeof(TestCommand));

        var action = () => scope.ResolveHandler("Missing.Handler");

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*Missing.Handler*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void BackgroundActionResolver_ExposesNoServiceProvider()
    {
        Assert.That((typeof(IBackgroundActionResolver).GetProperties()).Any(property => property.PropertyType == typeof(IServiceProvider)), Is.False);
        Assert.That((typeof(IBackgroundActionResolutionScope).GetProperties()).Any(property => property.PropertyType == typeof(IServiceProvider)), Is.False);
    }

    private static Microsoft.Extensions.DependencyInjection.ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedTracker>();
        services.AddScoped<IBackgroundAction<TestCommand>, ScopedHandler>();
        services.AddScoped<IBackgroundActionResolver, BackgroundActionResolver>();
        return services.BuildServiceProvider();
    }

    private sealed class TestCommand : IActionCommand
    {
    }

    private sealed class ScopedTracker
    {
    }

    private sealed class ScopedHandler : IBackgroundAction<TestCommand>
    {
        public ScopedHandler(ScopedTracker tracker)
        {
            Tracker = tracker;
        }

        public ScopedTracker Tracker { get; }

        public Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
