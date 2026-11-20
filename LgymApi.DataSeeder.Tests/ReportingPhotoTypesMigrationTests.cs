using System.Text.Json;
using LgymApi.Infrastructure.Reporting.PhotoTypesMigration;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Tests;

[TestFixture]
public sealed partial class ReportingPhotoTypesMigrationTests
{
    [Test]
    public async Task DryRun_ThenApply_PreservesLegacyData_ProducesRuntimeSnapshot_AndRerunsIdempotently()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var original = await fixture.CapturePreservedValuesAsync();
        var runner = new ReportingPhotoTypesMigrationRunner(fixture.Context);

        var dryRun = await runner.RunAsync(Options(ReportingPhotoTypesMigrationMode.DryRun), CancellationToken.None);

        Assert.That(dryRun.Applied, Is.False);
        Assert.That(dryRun.Counters.RequestsPlanned, Is.EqualTo(1));
        Assert.That(dryRun.Counters.DefaultsPlanned, Is.EqualTo(1));
        Assert.That(dryRun.Counters.PhotoMappingsPlanned, Is.EqualTo(1));
        Assert.That((await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()), Is.EqualTo(0));
        Assert.That((await fixture.Context.Photos.SingleAsync()).SourcePhotoTypeId, Is.Null);
        await fixture.AssertPreservedValuesAsync(original);

        var apply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, dryRun.Fingerprint),
            CancellationToken.None);

        Assert.That(apply.Applied, Is.True);
        Assert.That(apply.HasGlobalBlockers, Is.False);
        Assert.That(apply.HasUnresolvedRequests, Is.False);
        Assert.That(apply.Counters.SnapshotsWritten, Is.EqualTo(1));
        Assert.That(apply.Counters.PhotosMapped, Is.EqualTo(1));
        Assert.That(apply.Counters.DefaultsWritten, Is.EqualTo(1));
        var snapshot = await fixture.Context.ReportRequestDefinitionSnapshots.AsNoTracking().SingleAsync();
        Assert.That(snapshot.Provenance, Is.EqualTo("MigrationCapture"));
        Assert.That(snapshot.SourceTemplateId, Is.EqualTo(fixture.Template.Id));
        var runtimeType = System.Reflection.Assembly.Load("LgymApi.Reporting")
            .GetType("LgymApi.Application.Features.Reporting.Models.ReportTemplateSnapshotDocument", throwOnError: true)!;
        var runtimeReader = runtimeType.Assembly.GetType("LgymApi.Application.Features.Reporting.ReportTemplateSnapshotFactory", throwOnError: true)!
            .GetMethod("Deserialize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
        var runtimeDocument = runtimeReader.Invoke(null, [snapshot.DefinitionJson]);
        Assert.That(runtimeDocument, Is.Not.Null);
        Assert.That(runtimeType.GetProperty("Provenance")!.GetValue(runtimeDocument), Is.EqualTo("MigrationCapture"));
        Assert.That(runtimeType.GetProperty("SchemaVersion")!.GetValue(runtimeDocument), Is.EqualTo(2));
        using var serializedSnapshot = JsonDocument.Parse(snapshot.DefinitionJson);
        var photoConfig = serializedSnapshot.RootElement.GetProperty("fields")[0].GetProperty("moduleConfig");
        Assert.That(photoConfig.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(2));
        var frozenTypes = photoConfig.GetProperty("photoTypes").EnumerateArray().ToList();
        Assert.That(frozenTypes, Has.Exactly(2).Items);
        Assert.That(frozenTypes[0].GetProperty("labels").GetProperty("en").GetString(), Is.EqualTo("Front"));
        Assert.That(frozenTypes[0].GetProperty("labels").GetProperty("pl").GetString(), Is.EqualTo("Przód"));

        var frontType = await fixture.Context.TrainerPhotoTypes
            .IgnoreQueryFilters()
            .SingleAsync(photoType => photoType.Id == fixture.ExistingFrontType.Id);
        Assert.That(frontType.Name, Is.EqualTo("Coach check-in front"));
        Assert.That(frontType.NameMode, Is.EqualTo(PhotoTypeNameMode.TrainerProvided));
        Assert.That(frontType.IsArchived, Is.True);
        var backType = await fixture.Context.TrainerPhotoTypes.SingleAsync(photoType => photoType.SystemKey == "Back");
        Assert.That(backType.IsArchived, Is.False);
        var migratedPhoto = await fixture.Context.Photos.AsNoTracking().SingleAsync();
        Assert.That(migratedPhoto.SourcePhotoTypeId, Is.EqualTo(fixture.ExistingFrontType.Id));
        Assert.That(string.IsNullOrWhiteSpace(migratedPhoto.ReportPhotoSlotId), Is.False);
        await fixture.AssertPreservedValuesAsync(original);

        var firstSnapshotBytes = snapshot.DefinitionJson;
        var secondDryRun = await runner.RunAsync(Options(ReportingPhotoTypesMigrationMode.DryRun), CancellationToken.None);
        Assert.That(secondDryRun.Fingerprint, Is.Not.EqualTo(dryRun.Fingerprint));
        var secondApply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, secondDryRun.Fingerprint),
            CancellationToken.None);

        Assert.That(secondApply.Applied, Is.True);
        Assert.That(secondApply.Counters.SnapshotsWritten, Is.EqualTo(0));
        Assert.That(secondApply.Counters.PhotosMapped, Is.EqualTo(0));
        Assert.That(secondApply.Counters.DefaultsWritten, Is.EqualTo(0));
        Assert.That((await fixture.Context.ReportRequestDefinitionSnapshots.AsNoTracking().SingleAsync()).DefinitionJson, Is.EqualTo(firstSnapshotBytes));
        Assert.That((await fixture.Context.TrainerPhotoTypes.CountAsync()), Is.EqualTo(2));
        await fixture.AssertPreservedValuesAsync(original);
    }

    [Test]
    public async Task Apply_WithChangedFingerprint_RefusesAllWrites()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var runner = new ReportingPhotoTypesMigrationRunner(fixture.Context);
        var dryRun = await runner.RunAsync(Options(ReportingPhotoTypesMigrationMode.DryRun), CancellationToken.None);
        var template = await fixture.Context.ReportTemplates.SingleAsync();
        template.Name = "Definition changed after dry-run";
        await fixture.Context.SaveChangesAsync();

        var apply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, dryRun.Fingerprint),
            CancellationToken.None);

        Assert.That(apply.Applied, Is.False);
        Assert.That((apply.Exceptions).Any(issue => issue.Code == "FINGERPRINT_CHANGED"), Is.True);
        Assert.That((await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()), Is.EqualTo(0));
        Assert.That((await fixture.Context.Photos.SingleAsync()).SourcePhotoTypeId, Is.Null);
    }

    [Test]
    public async Task ExistingSnapshotBytes_AreNeverReplacedOrNormalized()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        const string originalBytes = "{ \"legacySnapshot\" : true }";
        fixture.Context.ReportRequestDefinitionSnapshots.Add(new ReportRequestDefinitionSnapshot
        {
            Id = Id<ReportRequestDefinitionSnapshot>.New(),
            ReportRequestId = fixture.Request.Id,
            SourceTemplateId = fixture.Template.Id,
            DefinitionJson = originalBytes,
            Provenance = "Issued",
            CreatedAt = DateTimeOffset.Parse("2025-01-10T12:00:00Z")
        });
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var runner = new ReportingPhotoTypesMigrationRunner(fixture.Context);
        var dryRun = await runner.RunAsync(Options(ReportingPhotoTypesMigrationMode.DryRun), CancellationToken.None);

        var apply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, dryRun.Fingerprint),
            CancellationToken.None);

        Assert.That(apply.Counters.SnapshotsWritten, Is.EqualTo(0));
        Assert.That(apply.Counters.ExistingSnapshotsPreserved, Is.GreaterThan(0));
        Assert.That((await fixture.Context.ReportRequestDefinitionSnapshots.AsNoTracking().SingleAsync()).DefinitionJson, Is.EqualTo(originalBytes));
    }

    [Test]
    public async Task UnknownPhotoView_LeavesRequestAndPhotoUnresolvedWithoutGuessing()
    {
        await using var fixture = await MigrationFixture.CreateAsync(photoView: "ThreeQuarter");
        var runner = new ReportingPhotoTypesMigrationRunner(fixture.Context);
        var dryRun = await runner.RunAsync(Options(ReportingPhotoTypesMigrationMode.DryRun), CancellationToken.None);

        Assert.That((dryRun.Exceptions).Any(issue => issue.Code == "PHOTO_VIEW_UNKNOWN"), Is.True);
        Assert.That(dryRun.Counters.RequestsUnsafe, Is.EqualTo(1));
        var apply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, dryRun.Fingerprint),
            CancellationToken.None);

        Assert.That(apply.HasUnresolvedRequests, Is.True);
        Assert.That((await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()), Is.EqualTo(0));
        var photo = await fixture.Context.Photos.AsNoTracking().SingleAsync();
        Assert.That(photo.SourcePhotoTypeId, Is.Null);
        Assert.That(photo.ReportPhotoSlotId, Is.Null);
    }

    [Test]
    public async Task PendingLegacyUpload_BlocksApplyUntilDrained()
    {
        await using var fixture = await MigrationFixture.CreateAsync(includePendingUpload: true);
        var runner = new ReportingPhotoTypesMigrationRunner(fixture.Context);
        var dryRun = await runner.RunAsync(Options(ReportingPhotoTypesMigrationMode.DryRun), CancellationToken.None);

        Assert.That((dryRun.Exceptions).Any(issue => issue.Code == "PENDING_LEGACY_UPLOAD"), Is.True);
        var apply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, dryRun.Fingerprint),
            CancellationToken.None);

        Assert.That(apply.Applied, Is.False);
        Assert.That(apply.Counters.BatchesCommitted, Is.EqualTo(0));
        Assert.That((await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()), Is.EqualTo(0));
        var session = await fixture.Context.PhotoUploadSessions.AsNoTracking().SingleAsync();
        Assert.That(session.SourcePhotoTypeId, Is.Null);
        Assert.That(session.ReportPhotoSlotId, Is.Null);
    }

    [Test]
    public async Task BoundPendingUpload_WithDeletedOwner_BlocksApplyEvenWithRetentionApproval()
    {
        await using var fixture = await MigrationFixture.CreateAsync(
            includePendingUpload: true,
            deletedTrainee: true,
            bindPendingUpload: true);
        var runner = new ReportingPhotoTypesMigrationRunner(fixture.Context);
        var dryRun = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.DryRun, allowRetainedDeletedOwners: true),
            CancellationToken.None);

        Assert.That((dryRun.Exceptions).Any(issue => issue.Code == "PENDING_UPLOAD_OWNER_UNAVAILABLE"), Is.True);
        Assert.That((dryRun.Exceptions).Any(issue => issue.Code == "PENDING_LEGACY_UPLOAD"), Is.False);
        var apply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, dryRun.Fingerprint, true),
            CancellationToken.None);

        Assert.That(apply.Applied, Is.False);
        Assert.That(apply.Counters.BatchesCommitted, Is.EqualTo(0));
        Assert.That((await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()), Is.EqualTo(0));
    }

    [Test]
    public async Task DeletedOwner_RequiresExplicitRetentionApproval_WithoutRestoringAccount()
    {
        await using var fixture = await MigrationFixture.CreateAsync(deletedTrainee: true);
        var runner = new ReportingPhotoTypesMigrationRunner(fixture.Context);
        var blockedDryRun = await runner.RunAsync(Options(ReportingPhotoTypesMigrationMode.DryRun), CancellationToken.None);

        Assert.That((blockedDryRun.Exceptions).Any(issue => issue.Code == "RETENTION_APPROVAL_REQUIRED"), Is.True);
        var blockedApply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, blockedDryRun.Fingerprint),
            CancellationToken.None);
        Assert.That(blockedApply.Applied, Is.False);

        var approvedDryRun = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.DryRun, allowRetainedDeletedOwners: true),
            CancellationToken.None);
        var approvedApply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, approvedDryRun.Fingerprint, true),
            CancellationToken.None);

        Assert.That(approvedApply.Applied, Is.True);
        Assert.That((await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()), Is.EqualTo(1));
        var trainee = await fixture.Context.Users.IgnoreQueryFilters().SingleAsync(user => user.Id == fixture.Trainee.Id);
        Assert.That(trainee.IsDeleted, Is.True);
        Assert.That(trainee.Name, Does.StartWith("anonymized_user_"));
        Assert.That((await fixture.Context.TrainerPhotoTypes.SingleAsync(photoType => photoType.SystemKey == "Back"))
            .IsArchived, Is.False, "the surviving trainer still references this type in a live template");
    }

    private static ReportingPhotoTypesMigrationOptions Options(
        ReportingPhotoTypesMigrationMode mode,
        string? fingerprint = null,
        bool allowRetainedDeletedOwners = false)
        => new(
            mode,
            2,
            fingerprint,
            allowRetainedDeletedOwners,
            mode == ReportingPhotoTypesMigrationMode.Apply);

    private sealed class MigrationFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private MigrationFixture(
            SqliteConnection connection,
            AppDbContext context,
            User trainer,
            User trainee,
            ReportTemplate template,
            ReportRequest request,
            ReportSubmission submission,
            Photo photo,
            TrainerPhotoType existingFrontType)
        {
            _connection = connection;
            Context = context;
            Trainer = trainer;
            Trainee = trainee;
            Template = template;
            Request = request;
            Submission = submission;
            Photo = photo;
            ExistingFrontType = existingFrontType;
        }

        public AppDbContext Context { get; }
        public User Trainer { get; }
        public User Trainee { get; }
        public ReportTemplate Template { get; }
        public ReportRequest Request { get; }
        public ReportSubmission Submission { get; }
        public Photo Photo { get; }
        public TrainerPhotoType ExistingFrontType { get; }

        public static async Task<MigrationFixture> CreateAsync(
            string photoView = "Front",
            bool includePendingUpload = false,
            bool deletedTrainee = false,
            bool bindPendingUpload = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();
            var trainer = CreateUser("trainer@example.test", "Trainer");
            var trainee = CreateUser("trainee@example.test", "Trainee");
            if (deletedTrainee)
            {
                trainee.Name = $"anonymized_user_{trainee.Id}";
                trainee.Email = new LgymApi.Domain.ValueObjects.Email($"anonymized_{trainee.Id}@example.com");
                trainee.IsDeleted = true;
            }
            var template = new ReportTemplate
            {
                Id = Id<ReportTemplate>.New(),
                TrainerId = trainer.Id,
                Name = "Legacy weekly report",
                Description = "Definition captured at migration time",
                CreatedAt = DateTimeOffset.Parse("2025-01-01T10:00:00Z")
            };
            var field = new ReportTemplateField
            {
                Id = Id<ReportTemplateField>.New(),
                TemplateId = template.Id,
                Key = "photos",
                Label = "Progress photos",
                Type = ReportFieldType.Photos,
                IsRequired = true,
                Order = 1,
                ModuleConfig = "{\"requiredViews\":[\"Front\",\"Back\"]}",
                CreatedAt = DateTimeOffset.Parse("2025-01-01T10:01:00Z")
            };
            var request = new ReportRequest
            {
                Id = Id<ReportRequest>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                TemplateId = template.Id,
                Status = ReportRequestStatus.Submitted,
                DueAt = DateTimeOffset.Parse("2025-02-01T00:00:00Z"),
                SubmittedAt = DateTimeOffset.Parse("2025-01-20T08:00:00Z"),
                Note = "Legacy note",
                CreatedAt = DateTimeOffset.Parse("2025-01-10T12:00:00Z"),
                UpdatedAt = DateTimeOffset.Parse("2025-01-20T08:00:00Z")
            };
            var submission = new ReportSubmission
            {
                Id = Id<ReportSubmission>.New(),
                ReportRequestId = request.Id,
                TraineeId = trainee.Id,
                PayloadJson = "{\"photos\":[\"legacy-photo\"],\"unknownFutureAnswer\":{\"kept\":true}}",
                CreatedAt = DateTimeOffset.Parse("2025-01-20T08:00:00Z")
            };
            var frontType = new TrainerPhotoType
            {
                Id = Id<TrainerPhotoType>.New(),
                TrainerId = trainer.Id,
                SystemKey = "Front",
                Name = "Coach check-in front",
                NormalizedName = "COACH CHECK-IN FRONT",
                NameMode = PhotoTypeNameMode.TrainerProvided,
                IsArchived = true,
                CreatedAt = DateTimeOffset.Parse("2025-06-01T00:00:00Z")
            };
            var photo = new Photo
            {
                Id = Id<Photo>.New(),
                StorageKey = "photos/retained/original.jpg",
                MimeType = "image/jpeg",
                SizeBytes = 1234,
                Checksum = "checksum",
                ThumbnailStorageKey = "photos/retained/thumb.jpg",
                ViewType = photoView,
                ReportRequestId = request.Id,
                UploaderUserId = trainee.Id,
                OwnerUserId = trainee.Id,
                CreatedAt = DateTimeOffset.Parse("2025-01-19T12:00:00Z"),
                UpdatedAt = DateTimeOffset.Parse("2025-01-19T12:00:00Z")
            };

            context.AddRange(trainer, trainee, template, field, request, submission, frontType, photo);
            if (includePendingUpload)
            {
                context.PhotoUploadSessions.Add(new PhotoUploadSession
                {
                    Id = Id<PhotoUploadSession>.New(),
                    StorageKey = "photos/pending/legacy.jpg",
                    OwnerUserId = trainee.Id,
                    InitiatedByUserId = trainee.Id,
                    ReportRequestId = request.Id,
                    ViewType = "Back",
                    DeclaredContentType = "image/jpeg",
                    DeclaredSizeBytes = 100,
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
                    Status = PhotoUploadSessionStatus.Pending,
                    SourcePhotoTypeId = bindPendingUpload ? frontType.Id : null,
                    ReportPhotoSlotId = bindPendingUpload ? Id<PhotoUploadSession>.New().ToString() : null
                });
            }
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return new MigrationFixture(connection, context, trainer, trainee, template, request, submission, photo, frontType);
        }

        public async Task<PreservedValues> CapturePreservedValuesAsync()
        {
            var request = await Context.ReportRequests.AsNoTracking().SingleAsync();
            var submission = await Context.ReportSubmissions.AsNoTracking().SingleAsync();
            var photo = await Context.Photos.AsNoTracking().SingleAsync();
            return new PreservedValues(
                request.Id,
                request.CreatedAt,
                request.UpdatedAt,
                request.DueAt,
                request.SubmittedAt,
                request.Note,
                submission.PayloadJson,
                photo.Id,
                photo.StorageKey,
                photo.CreatedAt,
                photo.UpdatedAt,
                photo.ViewType);
        }

        public async Task AssertPreservedValuesAsync(PreservedValues expected)
        {
            var request = await Context.ReportRequests.AsNoTracking().SingleAsync();
            var submission = await Context.ReportSubmissions.AsNoTracking().SingleAsync();
            var photo = await Context.Photos.AsNoTracking().SingleAsync();
            Assert.That(request.Id, Is.EqualTo(expected.RequestId));
            Assert.That(request.CreatedAt, Is.EqualTo(expected.RequestCreatedAt));
            Assert.That(request.UpdatedAt, Is.EqualTo(expected.RequestUpdatedAt));
            Assert.That(request.DueAt, Is.EqualTo(expected.DueAt));
            Assert.That(request.SubmittedAt, Is.EqualTo(expected.SubmittedAt));
            Assert.That(request.Note, Is.EqualTo(expected.Note));
            Assert.That(submission.PayloadJson, Is.EqualTo(expected.PayloadJson));
            Assert.That(photo.Id, Is.EqualTo(expected.PhotoId));
            Assert.That(photo.StorageKey, Is.EqualTo(expected.StorageKey));
            Assert.That(photo.CreatedAt, Is.EqualTo(expected.PhotoCreatedAt));
            Assert.That(photo.UpdatedAt, Is.EqualTo(expected.PhotoUpdatedAt));
            Assert.That(photo.ViewType, Is.EqualTo(expected.ViewType));
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static User CreateUser(string email, string name)
            => new()
            {
                Id = Id<User>.New(),
                Email = new LgymApi.Domain.ValueObjects.Email(email),
                Name = name,
                PreferredLanguage = "en",
                PreferredTimeZone = "UTC",
                ProfileRank = "Bronze"
            };
    }

    private sealed record PreservedValues(
        Id<ReportRequest> RequestId,
        DateTimeOffset RequestCreatedAt,
        DateTimeOffset RequestUpdatedAt,
        DateTimeOffset? DueAt,
        DateTimeOffset? SubmittedAt,
        string? Note,
        string PayloadJson,
        Id<Photo> PhotoId,
        string StorageKey,
        DateTimeOffset PhotoCreatedAt,
        DateTimeOffset PhotoUpdatedAt,
        string ViewType);
}
