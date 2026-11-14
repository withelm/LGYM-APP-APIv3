using FluentAssertions;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.AccountDeletion;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories.Reporting;
using LgymApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class AccountDeletionUploadRaceTests : IntegrationTestBase
{
    [Test]
    public async Task ExpiredUploadCleanup_WhenDeletionCommitsAfterSelection_PreservesTheObject()
    {
        var user = await SeedUserAsync();
        var key = $"photos/{user.Id}/expired-recovery.jpg";
        var uploadId = Id<PhotoUploadSession>.New();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PhotoUploadSessions.Add(new PhotoUploadSession
        {
            Id = uploadId, OwnerUserId = user.Id, InitiatedByUserId = user.Id,
            ReportRequestId = Id<ReportRequest>.New(), StorageKey = key,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(-1), Status = PhotoUploadSessionStatus.Pending
        });
        await db.SaveChangesAsync();
        var store = scope.ServiceProvider.GetRequiredService<LocalPhotoDevelopmentStore>();
        await using var bytes = new MemoryStream([1, 2, 3]);
        await store.SaveAsync(key, bytes);
        var actual = new ReportPhotoPersistenceRepository(db);
        var selection = Substitute.For<IReportPhotoPersistence>();
        selection.ListCleanupCandidatesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var candidates = await actual.ListCleanupCandidatesAsync(call.Arg<DateTimeOffset>(), call.Arg<CancellationToken>());
            candidates.Should().ContainSingle(candidate => candidate.Id == uploadId);
            using var deletion = Factory.Services.CreateScope();
            await deletion.ServiceProvider.GetRequiredService<IAccountDeletionService>().RequestAsync(user.Id.Rebind<AccountReference>());
            return candidates;
        });
        var cleanup = new ExpiredPhotoUploadCleanupService(selection,
            scope.ServiceProvider.GetRequiredService<IPhotoStorageProvider>(), scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
            NullLogger<ExpiredPhotoUploadCleanupService>.Instance, scope.ServiceProvider.GetRequiredService<IAccountDeletionSynchronization>(),
            scope.ServiceProvider.GetRequiredService<IAccountDeletionRetention>());
        try
        {
            (await cleanup.CleanupExpiredUploadsAsync()).Should().Be(0);

            (await store.ReadAsync(key)).Should().Equal(new byte[] { 1, 2, 3 });
            await db.Entry(await db.PhotoUploadSessions.SingleAsync(upload => upload.Id == uploadId)).ReloadAsync();
            (await db.PhotoUploadSessions.SingleAsync(upload => upload.Id == uploadId)).Status.Should().Be(PhotoUploadSessionStatus.Pending);
        }
        finally
        {
            await store.DeleteAsync(key);
        }
    }
}
