using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Reporting.PhotoTypesMigration;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Tests;

public sealed partial class ReportingPhotoTypesMigrationTests
{
    [Test]
    public async Task Startup_WithThirdPartyPhotoUploader_DoesNotCanonicalizeForeignPhoto()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var stranger = new User { Id = Id<User>.New(), Name = "Third party", Email = "stranger@example.test", ProfileRank = "Rookie" };
        fixture.Context.Users.Add(stranger);
        var photo = await fixture.Context.Photos.SingleAsync();
        photo.UploaderUserId = stranger.Id;
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var action = () => ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*PHOTO_OWNER_UNSAFE*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));

        Assert.That((await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()), Is.EqualTo(0));
        Assert.That((await fixture.Context.Photos.SingleAsync()).SourcePhotoTypeId, Is.Null);
    }

    [Test]
    public async Task Startup_WithArchivedSourceTemplate_CapturesSurvivingReportWithoutRestoringTemplate()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var template = await fixture.Context.ReportTemplates.SingleAsync();
        template.IsDeleted = true;
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        await ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);

        Assert.That((await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()), Is.EqualTo(1));
        Assert.That((await fixture.Context.ReportTemplates.IgnoreQueryFilters().SingleAsync()).IsDeleted, Is.True);
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

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*SOURCE_TEMPLATE_OWNER_CONFLICT*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));

        Assert.That((await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()), Is.EqualTo(0));
    }

    [Test]
    public async Task Startup_CapturesLegacyDataAutomaticallyAndRestartIgnoresNewPendingUploads()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var original = await fixture.CapturePreservedValuesAsync();

        await ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);

        var snapshot = await fixture.Context.ReportRequestDefinitionSnapshots.AsNoTracking().SingleAsync();
        Assert.That(snapshot.Provenance, Is.EqualTo("MigrationCapture"));
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

        Assert.That((await fixture.Context.ReportRequestDefinitionSnapshots.AsNoTracking().SingleAsync()).DefinitionJson, Is.EqualTo(snapshot.DefinitionJson));
        Assert.That((await fixture.Context.PhotoUploadSessions.SingleAsync()).Status, Is.EqualTo(PhotoUploadSessionStatus.Pending));
        await fixture.AssertPreservedValuesAsync(original);
    }

    [Test]
    public async Task Startup_WithUnknownHistoricalView_BlocksWithoutPartialMigration()
    {
        await using var fixture = await MigrationFixture.CreateAsync(photoView: "Unknown-view");
        var original = await fixture.CapturePreservedValuesAsync();
        var action = () => ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);
        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*PHOTO_VIEW_UNKNOWN*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        fixture.Context.ChangeTracker.Clear();
        Assert.That((await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()), Is.EqualTo(0));
        Assert.That((await fixture.Context.TrainerPhotoTypes.CountAsync()), Is.EqualTo(1));
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

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Injected second-checkpoint failure").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));

        fixture.Context.ChangeTracker.Clear();
        Assert.That((await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()), Is.EqualTo(0));
        Assert.That((await fixture.Context.TrainerPhotoTypes.CountAsync()), Is.EqualTo(1));
        Assert.That((await fixture.Context.Photos.SingleAsync()).ReportPhotoSlotId, Is.Null);
        await fixture.AssertPreservedValuesAsync(original);
    }

    [Test]
    public async Task Startup_WithActiveLegacyUpload_BlocksUntilCapabilityExpiresWithoutDeletingTheSession()
    {
        await using var fixture = await MigrationFixture.CreateAsync(includePendingUpload: true);
        var action = () => ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);
        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*PENDING_LEGACY_UPLOAD*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        var session = await fixture.Context.PhotoUploadSessions.SingleAsync();
        session.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        await ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);

        Assert.That((await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()), Is.EqualTo(1));
        Assert.That((await fixture.Context.PhotoUploadSessions.SingleAsync()).Status, Is.EqualTo(PhotoUploadSessionStatus.Pending));
    }
}
