using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Application.Platform.ReferenceData.AppConfig;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Runtime;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.TestUtils;
using LgymApi.TestUtils.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TestServiceProviderFactoryTests
{
    [Test]
    public void CreateServiceProvider_ComposesProductionFacadesAndCanonicalMappings()
    {
        var configuration = CompositionRootTestHost.CreateConfiguration();
        var composition = CompositionRootTestHost.CreateFactoryComposition(configuration);

        using var provider = TestServiceProviderFactory.CreateServiceProvider(composition);
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        Assert.That(services.GetServices<IAppConfigService>(), Has.Exactly(1).Items);
        Assert.That(services.GetServices<ITokenService>(), Has.Exactly(1).Items);
        Assert.That(services.GetServices<IPlanDayService>(), Has.Exactly(1).Items);
        Assert.That(services.GetServices<IInAppNotificationService>(), Has.Exactly(1).Items);
        Assert.That(services.GetServices<IReportingService>(), Has.Exactly(1).Items);
        Assert.That(services.GetServices<IUnitOfWork>(), Has.Exactly(1).Items);
        Assert.That(services.GetServices<CommandContractRegistry>(), Has.Exactly(1).Items);
        Assert.That(services.GetServices<IMappingProfile>(), Has.Exactly(49).Items);
        Assert.That(services.GetServices<IMappingProfile>().Select(profile => profile.GetType()), Is.Unique);
    }

    [Test]
    public void CreateServiceProvider_WhenTrainingPlanningIsOmitted_FailsForItsPublicContract()
    {
        var configuration = CompositionRootTestHost.CreateConfiguration();
        var composition = CompositionRootTestHost.CreateFactoryComposition(
            configuration,
            omittedFacade: CompositionFacade.TrainingPlanning);
        using var provider = TestServiceProviderFactory.CreateServiceProvider(composition);

        var action = () => provider.GetRequiredService<IPlanDayService>();

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{typeof(IPlanDayService).FullName}*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void CreateServiceProvider_WhenFakesAreRegisteredBeforeModules_ModuleRegistrationsWin()
    {
        var configuration = CompositionRootTestHost.CreateConfiguration();
        var composition = CompositionRootTestHost.CreateFactoryComposition(configuration);
        var earlyEmailSender = new TestEmailSender();
        var earlyPushSender = new RecordingPushProviderSender();
        var earlySessionStore = new FakeUserSessionStore();

        using var provider = TestServiceProviderFactory.CreateServiceProvider(
            composition,
            configureServicesBeforeModules: services =>
            {
                services.AddSingleton<IEmailSender>(earlyEmailSender);
                services.AddSingleton<IPushProviderSender>(earlyPushSender);
                services.AddSingleton<IUserSessionStore>(earlySessionStore);
            });
        using var scope = provider.CreateScope();

        Assert.That(scope.ServiceProvider.GetRequiredService<IEmailSender>(), Is.Not.SameAs(earlyEmailSender));
        Assert.That(scope.ServiceProvider.GetRequiredService<IPushProviderSender>(), Is.Not.SameAs(earlyPushSender));
        Assert.That(scope.ServiceProvider.GetRequiredService<IUserSessionStore>(), Is.Not.SameAs(earlySessionStore));
    }

    [Test]
    public void CreateServiceProvider_WhenFakesReplaceAfterModules_CapturedEffectsAndIdentityStoreWin()
    {
        var configuration = CompositionRootTestHost.CreateConfiguration();
        var composition = CompositionRootTestHost.CreateFactoryComposition(configuration);
        var emailSender = new TestEmailSender();
        var pushSender = new RecordingPushProviderSender();
        var sessionStore = new FakeUserSessionStore();

        using var provider = TestServiceProviderFactory.CreateServiceProvider(
            composition,
            replaceServicesAfterModules: services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(emailSender);
                services.RemoveAll<IPushProviderSender>();
                services.AddSingleton<IPushProviderSender>(pushSender);
                services.RemoveAll<IUserSessionStore>();
                services.AddSingleton<IUserSessionStore>(sessionStore);
            });
        using var scope = provider.CreateScope();

        var assertedValue154 = (scope.ServiceProvider.GetServices<IEmailSender>()).ToArray();
        Assert.That(assertedValue154, Has.Exactly(1).Items);
        Assert.That(assertedValue154.Single(), Is.SameAs(emailSender));
        var assertedValue155 = (scope.ServiceProvider.GetServices<IPushProviderSender>()).ToArray();
        Assert.That(assertedValue155, Has.Exactly(1).Items);
        Assert.That(assertedValue155.Single(), Is.SameAs(pushSender));
        var assertedValue156 = (scope.ServiceProvider.GetServices<IUserSessionStore>()).ToArray();
        Assert.That(assertedValue156, Has.Exactly(1).Items);
        Assert.That(assertedValue156.Single(), Is.SameAs(sessionStore));
    }

    private sealed class RecordingPushProviderSender : IPushProviderSender
    {
        public Task<PushSendAttemptResult> SendAsync(
            PushInstallationDeliveryTarget target,
            PushEventPayload payload,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PushSendAttemptResult(
                PushSendOutcome.Skipped,
                "TestCapture",
                null,
                null,
                null));
        }
    }
}
