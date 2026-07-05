using FluentAssertions;
using LgymApi.Application.Features.Reporting;
using LgymApi.BackgroundWorker.Jobs;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExpiredPhotoUploadCleanupJobTests
{
    [Test]
    public async Task ExecuteAsync_DelegatesToCleanupService()
    {
        var cleanupService = Substitute.For<IExpiredPhotoUploadCleanupService>();
        var job = new ExpiredPhotoUploadCleanupJob(cleanupService);
        using var cts = new CancellationTokenSource();

        await job.ExecuteAsync(cts.Token);

        await cleanupService.Received(1).CleanupExpiredUploadsAsync(cts.Token);
    }
}
