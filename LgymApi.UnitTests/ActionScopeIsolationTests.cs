using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ActionScopeIsolationTests
{
    [Test]
    public void ActionExecutionScopeProvider_CreateActionExecutionScope_ReturnsNewScope()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = BuildTestConfiguration();
        services.AddLogging();
        
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
        services.AddScoped<IActionExecutionScopeProvider, ActionExecutionScopeProvider>();
        
        using var provider = services.BuildServiceProvider();
        var scopeProvider = provider.GetRequiredService<IActionExecutionScopeProvider>();

        // Act
        using var scope = scopeProvider.CreateActionExecutionScope();

        // Assert
        Assert.That(scope, Is.Not.Null);
        Assert.That(scope.ServiceProvider, Is.Not.Null);
    }

    [Test]
    public void ActionExecutionScopeProvider_CreateMultipleScopes_ScopesAreDistinct()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = BuildTestConfiguration();
        services.AddLogging();
        
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
        services.AddScoped<IActionExecutionScopeProvider, ActionExecutionScopeProvider>();
        
        using var provider = services.BuildServiceProvider();
        var scopeProvider = provider.GetRequiredService<IActionExecutionScopeProvider>();

        // Act
        using var scope1 = scopeProvider.CreateActionExecutionScope();
        using var scope2 = scopeProvider.CreateActionExecutionScope();

        // Assert
        Assert.That(scope1, Is.Not.SameAs(scope2), "Scopes must be distinct instances");
        Assert.That(scope1.ServiceProvider, Is.Not.SameAs(scope2.ServiceProvider), "Service providers must be different");
    }

    [Test]
    public void ActionExecutionScopeProvider_ParallelScopes_EachReturnsNewScope()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = BuildTestConfiguration();
        services.AddLogging();
        
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
        services.AddScoped<IActionExecutionScopeProvider, ActionExecutionScopeProvider>();
        
        using var provider = services.BuildServiceProvider();
        var scopeProvider = provider.GetRequiredService<IActionExecutionScopeProvider>();

        // Act
        var scopes = new List<IServiceScope>();
        var tasks = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() =>
            {
                var scope = scopeProvider.CreateActionExecutionScope();
                lock (scopes)
                {
                    scopes.Add(scope);
                }
            }))
            .ToArray();
        
        Task.WaitAll(tasks);

        // Assert
        Assert.That(scopes, Has.Count.EqualTo(4), "Should have 4 scopes created");
        
        // Verify all scopes are distinct
        var distinctScopes = scopes.Distinct().Count();
        Assert.That(distinctScopes, Is.EqualTo(4), "All parallel scopes must be distinct instances");
        
        // Verify all service providers are distinct
        var distinctProviders = scopes.Select(s => s.ServiceProvider).Distinct().Count();
        Assert.That(distinctProviders, Is.EqualTo(4), "All service providers must be distinct");
        
        // Clean up
        foreach (var scope in scopes)
        {
            scope.Dispose();
        }
    }

    [Test]
    public void ActionExecutionScopeProvider_ScopesInSequence_EachReturnsIndependentScope()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = BuildTestConfiguration();
        services.AddLogging();
        
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
        services.AddScoped<IActionExecutionScopeProvider, ActionExecutionScopeProvider>();
        
        using var provider = services.BuildServiceProvider();
        var scopeProvider = provider.GetRequiredService<IActionExecutionScopeProvider>();

        // Act
        using var scope1 = scopeProvider.CreateActionExecutionScope();
        using var scope2 = scopeProvider.CreateActionExecutionScope();
        using var scope3 = scopeProvider.CreateActionExecutionScope();

        // Assert - verify they're from different scopes
        Assert.That(scope1, Is.Not.SameAs(scope2));
        Assert.That(scope2, Is.Not.SameAs(scope3));
        Assert.That(scope1, Is.Not.SameAs(scope3));
        
        // Verify within-scope identity (same scope returns same instance for repeated requests)
        var provider1A = scope1.ServiceProvider;
        var provider1B = scope1.ServiceProvider;
        Assert.That(provider1A, Is.SameAs(provider1B), "Same scope should return same service provider instance");
    }

    [Test]
    public void ActionExecutionScopeProvider_DisposesScope_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = BuildTestConfiguration();
        services.AddLogging();
        
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
        services.AddScoped<IActionExecutionScopeProvider, ActionExecutionScopeProvider>();
        
        using var provider = services.BuildServiceProvider();
        var scopeProvider = provider.GetRequiredService<IActionExecutionScopeProvider>();

        // Act & Assert - scope disposal should not throw
        Assert.DoesNotThrow(() =>
        {
            using (var scope = scopeProvider.CreateActionExecutionScope())
            {
                _ = scope.ServiceProvider;
            }
        });
    }

    [Test]
    public void ActionExecutionScopeProvider_IsRegistrable_WithExplicitRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = BuildTestConfiguration();
        services.AddLogging();
        
        // Explicitly register scope provider (simulating later orchestrator DI)
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
        services.AddScoped<IActionExecutionScopeProvider, ActionExecutionScopeProvider>();
        
        using var provider = services.BuildServiceProvider();

        // Act
        var scopeProvider = provider.GetService<IActionExecutionScopeProvider>();

        // Assert
        Assert.That(scopeProvider, Is.Not.Null);
        Assert.That(scopeProvider, Is.TypeOf<ActionExecutionScopeProvider>());
    }

    [Test]
    public void ActionExecutionScopeProvider_ScopedRegistration_CreatesNewInstancePerScope()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = BuildTestConfiguration();
        services.AddLogging();
        
        // Add a test scoped service to verify scope isolation
        services.AddScoped<TestScopedService>();
        services.AddInfrastructure(configuration, enableSensitiveLogging: false, isTesting: true);
        services.AddScoped<IActionExecutionScopeProvider, ActionExecutionScopeProvider>();
        
        using var provider = services.BuildServiceProvider();
        var scopeProvider = provider.GetRequiredService<IActionExecutionScopeProvider>();

        // Act
        using var scope1 = scopeProvider.CreateActionExecutionScope();
        using var scope2 = scopeProvider.CreateActionExecutionScope();
        
        var service1 = scope1.ServiceProvider.GetService<TestScopedService>();
        var service2 = scope2.ServiceProvider.GetService<TestScopedService>();

        // Assert
        Assert.That(service1, Is.Not.Null);
        Assert.That(service2, Is.Not.Null);
        Assert.That(service1, Is.Not.SameAs(service2), "Each scope should have its own instance of scoped services");
    }

    private static IConfiguration BuildTestConfiguration()
    {
        var configDict = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
        };
        
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict);
        
        return configBuilder.Build();
    }
    
    /// <summary>
    /// Test helper scoped service to verify scope isolation
    /// </summary>
    private sealed class TestScopedService
    {
        public Guid Id { get; } = Guid.NewGuid();
    }
}
