using FluentAssertions;
using LgymApi.Application.Features.PasswordReset.Contracts;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Actions.Contracts;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.BackgroundWorker.Notifications;
using LgymApi.BackgroundWorker.Runtime;
using LgymApi.BackgroundWorker.Services;
using LgymApi.Infrastructure;
using LgymApi.TestUtils;
using Microsoft.Extensions.DependencyInjection;
using FcmPushSender = LgymApi.Infrastructure.Services.FcmPushSender;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CompositionRootConvergenceTests
{
    [TestCase(true, typeof(NoOpPushBackgroundScheduler))]
    [TestCase(false, typeof(HangfirePushBackgroundScheduler))]
    public void HostEquivalentComposition_RegistersExactCentralDescriptorsAndHandlers(
        bool isTesting,
        Type expectedSchedulerType)
    {
        var services = new ServiceCollection();
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
        });

        services.AddNotificationsModule(configuration);
        services.AddBackgroundWorkerServices(isTesting);

        var action = () => ValidateCentralComposition(services, expectedSchedulerType);

        action.Should().NotThrow();
    }

    [Test]
    public void CentralDescriptorValidation_RejectsMissingRegistration()
    {
        var services = CreateExpectedCentralComposition(isTesting: true);
        services.Remove(services.Single(descriptor =>
            descriptor.ServiceType == typeof(IPasswordRecoveryEmailScheduler)));

        var action = () => ValidateCentralComposition(services, typeof(NoOpPushBackgroundScheduler));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{typeof(IPasswordRecoveryEmailScheduler).FullName}*exactly once*");
    }

    [Test]
    public void CentralDescriptorValidation_RejectsDuplicateRegistration()
    {
        var services = CreateExpectedCentralComposition(isTesting: true);
        services.AddScoped(typeof(ICommandDispatcher), typeof(CommandDispatcher));

        var action = () => ValidateCentralComposition(services, typeof(NoOpPushBackgroundScheduler));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{typeof(ICommandDispatcher).FullName}*exactly once*");
    }

    [Test]
    public void CentralDescriptorValidation_RejectsWrongEnvironmentSchedulerBranch()
    {
        var services = CreateExpectedCentralComposition(isTesting: true);
        services.Remove(services.Single(descriptor => descriptor.ServiceType == typeof(IPushBackgroundScheduler)));
        services.AddScoped<IPushBackgroundScheduler, HangfirePushBackgroundScheduler>();

        var action = () => ValidateCentralComposition(services, typeof(NoOpPushBackgroundScheduler));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{typeof(IPushBackgroundScheduler).FullName}*{typeof(NoOpPushBackgroundScheduler).FullName}*");
    }

    private static ServiceCollection CreateExpectedCentralComposition(bool isTesting)
    {
        var services = new ServiceCollection();
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test"
        });
        services.AddNotificationsModule(configuration);
        var registry = CommandContractRegistry.CreateDefault();
        services.AddSingleton(registry);
        services.AddScoped(typeof(ICommandDispatcher), typeof(CommandDispatcher));
        services.AddScoped<IBackgroundActionResolver, BackgroundActionResolver>();
        services.AddScoped<IEmailScheduler<PasswordRecoveryEmailPayload>, EmailSchedulerService<PasswordRecoveryEmailPayload>>();
        services.AddScoped<IPasswordRecoveryEmailScheduler, PasswordRecoveryEmailSchedulerAdapter>();
        services.AddScoped<IPushProviderSender, FcmPushSender>();

        if (isTesting)
        {
            services.AddScoped<IPushBackgroundScheduler, NoOpPushBackgroundScheduler>();
        }
        else
        {
            services.AddScoped<IPushBackgroundScheduler, HangfirePushBackgroundScheduler>();
        }

        foreach (var contract in registry.Contracts)
        {
            foreach (var handlerType in contract.ExpectedHandlerTypes)
            {
                services.AddScoped(typeof(IBackgroundAction<>).MakeGenericType(contract.RuntimeType), handlerType);
            }
        }

        return services;
    }

    private static void ValidateCentralComposition(IServiceCollection services, Type expectedSchedulerType)
    {
        ValidateSingleDescriptor(services, typeof(CommandContractRegistry), ServiceLifetime.Singleton);
        ValidateSingleDescriptor(
            services,
            typeof(IInAppNotificationService),
            ServiceLifetime.Scoped,
            typeof(InAppNotificationService));
        ValidateSingleDescriptor(
            services,
            typeof(INotificationEventBridge),
            ServiceLifetime.Scoped,
            typeof(NotificationEventBridge));
        ValidateSingleDescriptor(services, typeof(ICommandDispatcher), ServiceLifetime.Scoped, typeof(CommandDispatcher));
        ValidateSingleDescriptor(
            services,
            typeof(IBackgroundActionResolver),
            ServiceLifetime.Scoped,
            typeof(BackgroundActionResolver));
        ValidateSingleDescriptor(
            services,
            typeof(IEmailScheduler<PasswordRecoveryEmailPayload>),
            ServiceLifetime.Scoped,
            typeof(EmailSchedulerService<PasswordRecoveryEmailPayload>));
        ValidateSingleDescriptor(
            services,
            typeof(IPasswordRecoveryEmailScheduler),
            ServiceLifetime.Scoped,
            typeof(PasswordRecoveryEmailSchedulerAdapter));
        ValidateSingleDescriptor(
            services,
            typeof(IPushBackgroundScheduler),
            ServiceLifetime.Scoped,
            expectedSchedulerType);
        ValidateSingleDescriptor(
            services,
            typeof(IPushProviderSender),
            ServiceLifetime.Scoped,
            typeof(FcmPushSender));

        var registry = CommandContractRegistry.CreateDefault();
        registry.Contracts.Should().HaveCount(15);
        registry.Contracts.Sum(contract => contract.ExpectedHandlerTypes.Count).Should().Be(16);
        BackgroundActionRegistrationValidator.Validate(services, registry);
    }

    private static void ValidateSingleDescriptor(
        IServiceCollection services,
        Type serviceType,
        ServiceLifetime expectedLifetime,
        Type? expectedImplementationType = null)
    {
        var descriptors = services.Where(descriptor => descriptor.ServiceType == serviceType).ToArray();
        if (descriptors.Length != 1)
        {
            throw new InvalidOperationException(
                $"Service '{serviceType.FullName}' must be registered exactly once; actual count is {descriptors.Length}.");
        }

        var descriptor = descriptors[0];
        if (descriptor.Lifetime != expectedLifetime)
        {
            throw new InvalidOperationException(
                $"Service '{serviceType.FullName}' must use lifetime '{expectedLifetime}'; actual lifetime is '{descriptor.Lifetime}'.");
        }

        if (expectedImplementationType != null && descriptor.ImplementationType != expectedImplementationType)
        {
            throw new InvalidOperationException(
                $"Service '{serviceType.FullName}' must use implementation '{expectedImplementationType.FullName}'; "
                + $"actual implementation is '{descriptor.ImplementationType?.FullName ?? "factory or instance"}'.");
        }
    }
}
