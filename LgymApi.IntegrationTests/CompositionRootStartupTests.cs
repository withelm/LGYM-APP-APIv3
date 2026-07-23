using FluentAssertions;
using LgymApi.Application.Features.PasswordReset.Contracts;
using LgymApi.Application.Notifications.Contracts.Events;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Notifications;
using LgymApi.BackgroundWorker.Runtime;
using LgymApi.BackgroundWorker.Services;
using LgymApi.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class CompositionRootStartupTests : IntegrationTestBase
{
    [Test]
    public void TestingApiHost_ResolvesCanonicalPasswordCommandAndPushServices()
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        services.GetServices<CommandContractRegistry>().Should().ContainSingle();
        services.GetServices<ICommandDispatcher>().Should().ContainSingle()
            .Which.Should().BeOfType<CommandDispatcher>();
        services.GetServices<IPasswordRecoveryEmailScheduler>().Should().ContainSingle()
            .Which.Should().BeOfType<PasswordRecoveryEmailSchedulerAdapter>();
        services.GetServices<ICoachingEmailNotificationFeature>().Should().ContainSingle()
            .Which.Should().BeOfType<CoachingEmailNotificationSchedulerAdapter>();
        services.GetServices<ICoachingEmailNotificationScheduler>().Should().ContainSingle()
            .Which.Should().BeOfType<CoachingEmailNotificationSchedulerAdapter>();
        services.GetServices<IPushBackgroundScheduler>().Should().ContainSingle()
            .Which.Should().BeOfType<NoOpPushBackgroundScheduler>();
        services.GetServices<IPushProviderSender>().Should().ContainSingle()
            .Which.Should().BeOfType<FcmPushSender>();
    }
}
