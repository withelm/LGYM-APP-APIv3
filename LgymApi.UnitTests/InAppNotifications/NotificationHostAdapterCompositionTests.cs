using LgymApi.Api.Features.InAppNotification;
using LgymApi.Api.Features.InAppNotification.Controllers;
using LgymApi.Api.Features.User.Controllers;
using LgymApi.Api.Hubs;
using LgymApi.Api.Mapping;
using LgymApi.Application.Notifications;
using LgymApi.Application.Repositories;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;
using LgymApi.Notifications.ApiAdapters;
using LgymApi.Notifications.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.UnitTests.InAppNotifications;

[TestFixture]
public sealed class NotificationHostAdapterCompositionTests
{
    [Test]
    public void SignalRPublisher_HostBinding_ResolvesExactlyOnce()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();
        services.AddSingleton<IAccountSessionConnectionRegistry, AccountSessionConnectionRegistry>();
        services.AddSingleton(Substitute.For<IAuthenticatedAccountContextResolver>());
        services.AddScoped(_ => Substitute.For<INotificationReplayAccessService>());
        services.AddScoped(_ => Substitute.For<LgymApi.Application.Mapping.Core.IMapper>());
        services.AddScoped<IInAppNotificationPushPublisher, SignalRNotificationPushPublisher>();

        var registrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(IInAppNotificationPushPublisher))
            .ToArray();
        var assertedValue186 = (registrations).ToArray();
        Assert.That(assertedValue186, Has.Exactly(1).Items);
        Assert.That(assertedValue186.Single().ImplementationType, Is.EqualTo(typeof(SignalRNotificationPushPublisher)));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<IInAppNotificationPushPublisher>(), Is.TypeOf<SignalRNotificationPushPublisher>());
    }

    [Test]
    public void NotificationsModule_WhenPublisherHostBindingIsOmitted_FailsWithTheMissingPublisherContract()
    {
        var services = new ServiceCollection();
        services.AddNotificationsModule();
        services.AddScoped(_ => Substitute.For<IInAppNotificationRepository>());
        services.AddScoped(_ => Substitute.For<IUnitOfWork>());
        services.AddScoped(_ => Substitute.For<INotificationEventBridge>());

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var resolve = () => scope.ServiceProvider.GetRequiredService<IInAppNotificationService>();

        Assert.That(() => { resolve(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{nameof(IInAppNotificationPushPublisher)}*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void ApiNotificationAdapters_UsePublicNotificationsContractsAndTheCanonicalMappingMarker()
    {
        var notificationsAssembly = typeof(NotificationReference).Assembly;
        var notificationContracts = new[]
        {
            typeof(IInAppNotificationApiAdapter),
            typeof(INotificationEventApiAdapter),
            typeof(IPushInstallationApiAdapter),
            typeof(IInAppNotificationPushPublisher)
        };

        Assert.That((notificationContracts).All(type => type.IsPublic && type.Assembly == notificationsAssembly), Is.True);
        Assert.That((MappingAssemblyMarkers.All).Where(assembly => assembly == notificationsAssembly), Has.Exactly(1).Items);
        Assert.That(typeof(InAppNotificationController).GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType), Does.Contain(typeof(IInAppNotificationApiAdapter)));
        Assert.That(typeof(PushNotificationAdminController).GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType), Does.Contain(typeof(INotificationEventApiAdapter)));
        Assert.That(typeof(PushInstallationController).GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType), Does.Contain(typeof(IPushInstallationApiAdapter)));
        Assert.That(typeof(SignalRNotificationPushPublisher).GetInterfaces(), Does.Contain(typeof(IInAppNotificationPushPublisher)));
    }

    [Test]
    public void NotificationsApiAdapters_RegisterAndResolveExactlyOnceAsScoped()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Substitute.For<IInAppNotificationService>());
        services.AddScoped(_ => Substitute.For<INotificationEventBridge>());
        services.AddScoped(_ => Substitute.For<IPushInstallationLifecycleService>());
        services.AddScoped(_ => Substitute.For<IAuthenticatedAccountAuthorizationService>());
        services.AddNotificationsApiAdapters();

        AssertScopedRegistration<IInAppNotificationApiAdapter, InAppNotificationApiAdapter>(services);
        AssertScopedRegistration<INotificationEventApiAdapter, NotificationEventApiAdapter>(services);
        AssertScopedRegistration<IPushInstallationApiAdapter, PushInstallationApiAdapter>(services);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<IInAppNotificationApiAdapter>(), Is.TypeOf<InAppNotificationApiAdapter>());
        Assert.That(scope.ServiceProvider.GetRequiredService<INotificationEventApiAdapter>(), Is.TypeOf<NotificationEventApiAdapter>());
        Assert.That(scope.ServiceProvider.GetRequiredService<IPushInstallationApiAdapter>(), Is.TypeOf<PushInstallationApiAdapter>());
    }

    private static void AssertScopedRegistration<TContract, TImplementation>(IServiceCollection services)
    {
        var assertedValue187 = (services.Where(descriptor => descriptor.ServiceType == typeof(TContract))).ToArray();
        Assert.That(assertedValue187, Has.Exactly(1).Items);
        Assert.That(assertedValue187.Single(), new NUnit.Framework.Constraints.PredicateConstraint<ServiceDescriptor>(descriptor =>
                descriptor.Lifetime == ServiceLifetime.Scoped
                && descriptor.ImplementationType == typeof(TImplementation)));
    }
}
