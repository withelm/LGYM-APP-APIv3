using Hangfire;
using Hangfire.Logging;
using System.Reflection;
using LgymApi.Application.Pagination;
using LgymApi.Application.Notifications;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Infrastructure;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Pagination;
using LgymApi.Infrastructure.UnitOfWork;
using LgymApi.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
[NonParallelizable]
public sealed class PlatformServiceCollectionDecompositionTests
{
    private static readonly MethodInfo ResolveLogProvider = typeof(LogProvider).GetMethod(
        "ResolveLogProvider",
        BindingFlags.Static | BindingFlags.NonPublic)!;

    [TestCase(true)]
    [TestCase(false)]
    public void AddPlatformServices_Preserves_Baseline_Descriptors_And_Resolves_Providers(bool isTesting)
    {
        var originalLogProvider = (ILogProvider)ResolveLogProvider.Invoke(null, null)!;
        try
        {
            var services = CreateServices();
            var configuration = CreateConfiguration();

            services.AddPlatformServices(configuration, enableSensitiveLogging: false, isTesting, hostBackgroundServer: true);
            AssertPlatformDescriptors(services, isTesting);

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            using var scope = provider.CreateScope();
            var scopedServices = scope.ServiceProvider;

            Assert.That(scopedServices.GetRequiredService<AppDbContext>().Database.ProviderName, Is.EqualTo("Npgsql.EntityFrameworkCore.PostgreSQL"));
            Assert.That(scopedServices.GetRequiredService<IUnitOfWork>(), Is.TypeOf<EfUnitOfWork>());
            Assert.That(scopedServices.GetRequiredService<ICommittedIntentDispatcher>().GetType().Name, Is.EqualTo("CommittedIntentDispatcher"));
            Assert.That(provider.GetRequiredService<IMapperRegistry>().GetType().Name, Is.EqualTo("MapperRegistry"));

            if (isTesting)
            {
                Assert.That(provider.GetService<JobStorage>(), Is.Null);
            }
            else
            {
                Assert.That(provider.GetRequiredService<JobStorage>().GetType().FullName, Is.EqualTo("Hangfire.PostgreSql.PostgreSqlStorage"));
            }
        }
        finally
        {
            LogProvider.SetCurrentLogProvider(originalLogProvider);
        }
    }

    [Test]
    public void PlatformDescriptorValidation_Fixture_Should_Reject_Duplicate_DbContext()
    {
        var services = CreateServices();
        services.AddPlatformServices(CreateConfiguration(), enableSensitiveLogging: false, isTesting: true);
        services.AddScoped<AppDbContext>(_ => throw new InvalidOperationException());

        var action = () => AssertPlatformDescriptors(services, isTesting: true);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*AppDbContext*exactly once*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void PlatformDescriptorValidation_Fixture_Should_Reject_Duplicate_UnitOfWork()
    {
        var services = CreateServices();
        services.AddPlatformServices(CreateConfiguration(), enableSensitiveLogging: false, isTesting: true);
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        var action = () => AssertPlatformDescriptors(services, isTesting: true);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*IUnitOfWork*exactly once*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void AddNotificationsModule_Validates_EmailOptions_Before_Adding_Descriptors()
    {
        var services = CreateServices();
        var values = TestConfigurationBuilder.ToDictionary(TestConfigurationBuilder.BuildEnabledEmailConfiguration());
        values["Email:DeliveryMode"] = "invalid";

        var action = () => services.AddNotificationsModule(TestConfigurationBuilder.BuildConfiguration(values));

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Email:DeliveryMode must be one of: Smtp, Dummy.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        Assert.That((services).Where(descriptor => descriptor.ServiceType == typeof(Microsoft.Extensions.Logging.ILoggerFactory)), Has.Exactly(1).Items);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static IConfiguration CreateConfiguration()
    {
        return TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=platform-decomposition;Username=test;Password=test"
        });
    }

    private static void AssertPlatformDescriptors(IServiceCollection services, bool isTesting)
    {
        AssertSingleTypeDescriptor(services, typeof(AppDbContext), ServiceLifetime.Scoped, typeof(AppDbContext));
        AssertSingleTypeDescriptor(services, typeof(IUnitOfWork), ServiceLifetime.Scoped, typeof(EfUnitOfWork));
        AssertSingleFactoryDescriptor(services, typeof(IMapperRegistry), ServiceLifetime.Singleton);
        AssertSingleInstanceDescriptor(services, typeof(PaginationPolicy), ServiceLifetime.Singleton);

        if (isTesting)
        {
            Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(JobStorage)), Is.False);
            return;
        }

        AssertSingleFactoryDescriptor(services, typeof(JobStorage), ServiceLifetime.Singleton);
    }

    private static void AssertSingleTypeDescriptor(
        IServiceCollection services,
        Type serviceType,
        ServiceLifetime lifetime,
        Type implementationType)
    {
        var descriptor = GetSingleDescriptor(services, serviceType);
        if (descriptor.Lifetime != lifetime || descriptor.ImplementationType != implementationType ||
            descriptor.ImplementationFactory is not null || descriptor.ImplementationInstance is not null)
        {
            throw new InvalidOperationException($"Service '{serviceType.Name}' must use one {lifetime} type descriptor for '{implementationType.Name}'.");
        }
    }

    private static void AssertSingleFactoryDescriptor(IServiceCollection services, Type serviceType, ServiceLifetime lifetime)
    {
        var descriptor = GetSingleDescriptor(services, serviceType);
        if (descriptor.Lifetime != lifetime || descriptor.ImplementationFactory is null ||
            descriptor.ImplementationType is not null || descriptor.ImplementationInstance is not null)
        {
            throw new InvalidOperationException($"Service '{serviceType.Name}' must use one {lifetime} factory descriptor.");
        }
    }

    private static void AssertSingleInstanceDescriptor(IServiceCollection services, Type serviceType, ServiceLifetime lifetime)
    {
        var descriptor = GetSingleDescriptor(services, serviceType);
        if (descriptor.Lifetime != lifetime || descriptor.ImplementationInstance is null ||
            descriptor.ImplementationType is not null || descriptor.ImplementationFactory is not null)
        {
            throw new InvalidOperationException($"Service '{serviceType.Name}' must use one {lifetime} instance descriptor.");
        }
    }

    private static ServiceDescriptor GetSingleDescriptor(IServiceCollection services, Type serviceType)
    {
        var descriptors = services.Where(descriptor => descriptor.ServiceType == serviceType).ToArray();
        if (descriptors.Length != 1)
        {
            throw new InvalidOperationException($"Service '{serviceType.Name}' must be registered exactly once; actual count is {descriptors.Length}.");
        }

        return descriptors[0];
    }
}
