using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Reporting.PhotoTypesMigration;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Tests;

public sealed partial class ReportingPhotoTypesMigrationTests
{
    [Test]
    public async Task Startup_WithArchivedSourceTemplate_CapturesSurvivingReportWithoutRestoringTemplate()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var template = await fixture.Context.ReportTemplates.SingleAsync();
        template.IsDeleted = true;
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        await ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);

        (await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()).Should().Be(1);
        (await fixture.Context.ReportTemplates.IgnoreQueryFilters().SingleAsync()).IsDeleted.Should().BeTrue();
    }

    [Test]
    public async Task Startup_WithConflictingOriginalOwnership_RefusesToGuess()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var template = await fixture.Context.ReportTemplates.SingleAsync();
        template.TrainerId = fixture.Trainee.Id;
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var action = () => ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*SOURCE_TEMPLATE_OWNER_CONFLICT*");

        (await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Startup_CapturesLegacyDataAutomaticallyAndRestartIgnoresNewPendingUploads()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var original = await fixture.CapturePreservedValuesAsync();

        await ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);

        var snapshot = await fixture.Context.ReportRequestDefinitionSnapshots.AsNoTracking().SingleAsync();
        snapshot.Provenance.Should().Be("MigrationCapture");
        await fixture.AssertPreservedValuesAsync(original);
        fixture.Context.PhotoUploadSessions.Add(new PhotoUploadSession
        {
            Id = Id<PhotoUploadSession>.New(), ReportRequestId = fixture.Request.Id,
            OwnerUserId = fixture.Trainee.Id, InitiatedByUserId = fixture.Trainee.Id,
            StorageKey = "pending-after-migration", ViewType = "Front", DeclaredContentType = "image/jpeg",
            DeclaredSizeBytes = 100, ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(10), Status = PhotoUploadSessionStatus.Pending
        });
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        await ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);

        (await fixture.Context.ReportRequestDefinitionSnapshots.AsNoTracking().SingleAsync()).DefinitionJson.Should().Be(snapshot.DefinitionJson);
        (await fixture.Context.PhotoUploadSessions.SingleAsync()).Status.Should().Be(PhotoUploadSessionStatus.Pending);
        await fixture.AssertPreservedValuesAsync(original);
    }

    [Test]
    public async Task Startup_WithUnknownHistoricalView_BlocksWithoutPartialMigration()
    {
        await using var fixture = await MigrationFixture.CreateAsync(photoView: "Unknown-view");
        var original = await fixture.CapturePreservedValuesAsync();
        var action = () => ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*PHOTO_VIEW_UNKNOWN*");
        fixture.Context.ChangeTracker.Clear();
        (await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()).Should().Be(0);
        (await fixture.Context.TrainerPhotoTypes.CountAsync()).Should().Be(1);
        await fixture.AssertPreservedValuesAsync(original);
    }

    [Test]
    public async Task Startup_WhenLaterCheckpointFails_RollsBackEarlierDefaultsAndPhotoWrites()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var original = await fixture.CapturePreservedValuesAsync();
        var saves = 0;
        fixture.Context.SavingChanges += (_, _) =>
        {
            if (++saves == 2) throw new InvalidOperationException("Injected second-checkpoint failure");
        };
        var action = () => ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Injected second-checkpoint failure");

        fixture.Context.ChangeTracker.Clear();
        (await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()).Should().Be(0);
        (await fixture.Context.TrainerPhotoTypes.CountAsync()).Should().Be(1);
        (await fixture.Context.Photos.SingleAsync()).ReportPhotoSlotId.Should().BeNull();
        await fixture.AssertPreservedValuesAsync(original);
    }

    [Test]
    public async Task Startup_WithActiveLegacyUpload_BlocksUntilCapabilityExpiresWithoutDeletingTheSession()
    {
        await using var fixture = await MigrationFixture.CreateAsync(includePendingUpload: true);
        var action = () => ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*PENDING_LEGACY_UPLOAD*");
        var session = await fixture.Context.PhotoUploadSessions.SingleAsync();
        session.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        await ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);

        (await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()).Should().Be(1);
        (await fixture.Context.PhotoUploadSessions.SingleAsync()).Status.Should().Be(PhotoUploadSessionStatus.Pending);
    }
}
