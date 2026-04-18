using FluentAssertions;
using LgymApi.Application.Notifications;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Notifications;
using LgymApi.Infrastructure.Jobs;
using LgymApi.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class ApiHostResolutionTests
{
    [Test]
    public void ProductionApiHost_ResolvesConcreteInvitationEmailServices()
    {
        using var factory = new ProductionApiHostFactory();
        using var scope = factory.Services.CreateScope();

        var scheduler = scope.ServiceProvider.GetRequiredService<IEmailBackgroundScheduler>();
        scheduler.Should().BeOfType<HangfireEmailBackgroundScheduler>();

        var handler = scope.ServiceProvider.GetRequiredService<IEmailJobHandler>();
        handler.Should().BeOfType<EmailJobHandlerService>();

        var concreteJob = scope.ServiceProvider.GetRequiredService<EmailJob>();
        concreteJob.Should().NotBeNull();

        scope.ServiceProvider.GetRequiredService<IEmailJob>().Should().BeOfType<EmailJob>();
    }
}
