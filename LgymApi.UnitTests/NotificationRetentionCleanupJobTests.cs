using LgymApi.Application.Notifications;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.BackgroundWorker.Jobs;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class NotificationRetentionCleanupJobTests
{
    [Test]
    public async Task ExecuteAsync_WhenCleaningPushNotificationMessages_ForwardsCancellation()
    {
        var cleanupService = Substitute.For<IPushNotificationMessageRetentionCleanupService>();
        cleanupService.CleanupAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(0));
        var job = new PushNotificationMessageRetentionCleanupJob(cleanupService);
        using var cancellationTokenSource = new CancellationTokenSource();

        await job.ExecuteAsync(cancellationTokenSource.Token);

        await cleanupService.Received(1).CleanupAsync(cancellationTokenSource.Token);
    }

    [Test]
    public async Task ExecuteAsync_WhenCleaningDisabledPushInstallations_ForwardsCancellation()
    {
        var cleanupService = Substitute.For<IDisabledPushInstallationRetentionCleanupService>();
        cleanupService.CleanupAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(0));
        var job = new DisabledPushInstallationRetentionCleanupJob(cleanupService);
        using var cancellationTokenSource = new CancellationTokenSource();

        await job.ExecuteAsync(cancellationTokenSource.Token);

        await cleanupService.Received(1).CleanupAsync(cancellationTokenSource.Token);
    }

    [Test]
    public async Task ExecuteAsync_WhenCleaningInAppNotifications_ForwardsCancellation()
    {
        var cleanupService = Substitute.For<IInAppNotificationRetentionCleanupService>();
        cleanupService.CleanupAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(0));
        var job = new InAppNotificationRetentionCleanupJob(cleanupService);
        using var cancellationTokenSource = new CancellationTokenSource();

        await job.ExecuteAsync(cancellationTokenSource.Token);

        await cleanupService.Received(1).CleanupAsync(cancellationTokenSource.Token);
    }

    [Test]
    public async Task ExecuteAsync_WhenCleanupFails_PropagatesTheFailure()
    {
        var cleanupService = Substitute.For<IPushNotificationMessageRetentionCleanupService>();
        var failure = new InvalidOperationException("cleanup failed");
        cleanupService.CleanupAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<int>(failure));
        var job = new PushNotificationMessageRetentionCleanupJob(cleanupService);

        var action = async () => await job.ExecuteAsync();

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("cleanup failed").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public async Task AddBackgroundWorkerServices_WhenRetentionJobIsResolvedFromScope_ForwardsCancellation()
    {
        var cleanupService = Substitute.For<IInAppNotificationRetentionCleanupService>();
        cleanupService.CleanupAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(0));
        var services = new ServiceCollection();
        services.AddScoped(_ => cleanupService);
        services.AddBackgroundWorkerServices(isTesting: true);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var cancellationTokenSource = new CancellationTokenSource();

        var job = scope.ServiceProvider.GetRequiredService<IInAppNotificationRetentionCleanupJob>();
        await job.ExecuteAsync(cancellationTokenSource.Token);

        await cleanupService.Received(1).CleanupAsync(cancellationTokenSource.Token);
    }
}
