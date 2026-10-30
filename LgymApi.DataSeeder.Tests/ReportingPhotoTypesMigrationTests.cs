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

        dryRun.Applied.Should().BeFalse();
        dryRun.Counters.RequestsPlanned.Should().Be(1);
        dryRun.Counters.DefaultsPlanned.Should().Be(1);
        dryRun.Counters.PhotoMappingsPlanned.Should().Be(1);
        (await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()).Should().Be(0);
        (await fixture.Context.Photos.SingleAsync()).SourcePhotoTypeId.Should().BeNull();
        await fixture.AssertPreservedValuesAsync(original);

        var apply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, dryRun.Fingerprint),
            CancellationToken.None);

        apply.Applied.Should().BeTrue();
        apply.HasGlobalBlockers.Should().BeFalse();
        apply.HasUnresolvedRequests.Should().BeFalse();
        apply.Counters.SnapshotsWritten.Should().Be(1);
        apply.Counters.PhotosMapped.Should().Be(1);
        apply.Counters.DefaultsWritten.Should().Be(1);
        var snapshot = await fixture.Context.ReportRequestDefinitionSnapshots.AsNoTracking().SingleAsync();
        snapshot.Provenance.Should().Be("MigrationCapture");
        snapshot.SourceTemplateId.Should().Be(fixture.Template.Id);
        var runtimeType = System.Reflection.Assembly.Load("LgymApi.Application")
            .GetType("LgymApi.Application.Features.Reporting.Models.ReportTemplateSnapshotDocument", throwOnError: true)!;
        var runtimeDocument = JsonSerializer.Deserialize(snapshot.DefinitionJson, runtimeType);
        runtimeDocument.Should().NotBeNull();
        runtimeType.GetProperty("Provenance")!.GetValue(runtimeDocument).Should().Be("MigrationCapture");
        runtimeType.GetProperty("SchemaVersion")!.GetValue(runtimeDocument).Should().Be(2);
        using var serializedSnapshot = JsonDocument.Parse(snapshot.DefinitionJson);
        var photoConfig = serializedSnapshot.RootElement.GetProperty("Fields")[0].GetProperty("ModuleConfig");
        photoConfig.GetProperty("schemaVersion").GetInt32().Should().Be(2);
        var frozenTypes = photoConfig.GetProperty("photoTypes").EnumerateArray().ToList();
        frozenTypes.Should().HaveCount(2);
        frozenTypes[0].GetProperty("labels").GetProperty("en").GetString().Should().Be("Front");
        frozenTypes[0].GetProperty("labels").GetProperty("pl").GetString().Should().Be("Przód");

        var frontType = await fixture.Context.TrainerPhotoTypes
            .IgnoreQueryFilters()
            .SingleAsync(photoType => photoType.Id == fixture.ExistingFrontType.Id);
        frontType.Name.Should().Be("Coach check-in front");
        frontType.NameMode.Should().Be(PhotoTypeNameMode.TrainerProvided);
        frontType.IsArchived.Should().BeTrue();
        var backType = await fixture.Context.TrainerPhotoTypes.SingleAsync(photoType => photoType.SystemKey == "Back");
        backType.IsArchived.Should().BeFalse();
        var migratedPhoto = await fixture.Context.Photos.AsNoTracking().SingleAsync();
        migratedPhoto.SourcePhotoTypeId.Should().Be(fixture.ExistingFrontType.Id);
        migratedPhoto.ReportPhotoSlotId.Should().NotBeNullOrWhiteSpace();
        await fixture.AssertPreservedValuesAsync(original);

        var firstSnapshotBytes = snapshot.DefinitionJson;
        var secondDryRun = await runner.RunAsync(Options(ReportingPhotoTypesMigrationMode.DryRun), CancellationToken.None);
        secondDryRun.Fingerprint.Should().NotBe(dryRun.Fingerprint);
        var secondApply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, secondDryRun.Fingerprint),
            CancellationToken.None);

        secondApply.Applied.Should().BeTrue();
        secondApply.Counters.SnapshotsWritten.Should().Be(0);
        secondApply.Counters.PhotosMapped.Should().Be(0);
        secondApply.Counters.DefaultsWritten.Should().Be(0);
        (await fixture.Context.ReportRequestDefinitionSnapshots.AsNoTracking().SingleAsync()).DefinitionJson
            .Should().Be(firstSnapshotBytes);
        (await fixture.Context.TrainerPhotoTypes.CountAsync()).Should().Be(2);
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

        apply.Applied.Should().BeFalse();
        apply.Exceptions.Should().Contain(issue => issue.Code == "FINGERPRINT_CHANGED");
        (await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()).Should().Be(0);
        (await fixture.Context.Photos.SingleAsync()).SourcePhotoTypeId.Should().BeNull();
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

        apply.Counters.SnapshotsWritten.Should().Be(0);
        apply.Counters.ExistingSnapshotsPreserved.Should().BeGreaterThan(0);
        (await fixture.Context.ReportRequestDefinitionSnapshots.AsNoTracking().SingleAsync()).DefinitionJson
            .Should().Be(originalBytes);
    }

    [Test]
    public async Task UnknownPhotoView_LeavesRequestAndPhotoUnresolvedWithoutGuessing()
    {
        await using var fixture = await MigrationFixture.CreateAsync(photoView: "ThreeQuarter");
        var runner = new ReportingPhotoTypesMigrationRunner(fixture.Context);
        var dryRun = await runner.RunAsync(Options(ReportingPhotoTypesMigrationMode.DryRun), CancellationToken.None);

        dryRun.Exceptions.Should().Contain(issue => issue.Code == "PHOTO_VIEW_UNKNOWN");
        dryRun.Counters.RequestsUnsafe.Should().Be(1);
        var apply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, dryRun.Fingerprint),
            CancellationToken.None);

        apply.HasUnresolvedRequests.Should().BeTrue();
        (await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()).Should().Be(0);
        var photo = await fixture.Context.Photos.AsNoTracking().SingleAsync();
        photo.SourcePhotoTypeId.Should().BeNull();
        photo.ReportPhotoSlotId.Should().BeNull();
    }

    [Test]
    public async Task PendingLegacyUpload_BlocksApplyUntilDrained()
    {
        await using var fixture = await MigrationFixture.CreateAsync(includePendingUpload: true);
        var runner = new ReportingPhotoTypesMigrationRunner(fixture.Context);
        var dryRun = await runner.RunAsync(Options(ReportingPhotoTypesMigrationMode.DryRun), CancellationToken.None);

        dryRun.Exceptions.Should().Contain(issue => issue.Code == "PENDING_LEGACY_UPLOAD");
        var apply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, dryRun.Fingerprint),
            CancellationToken.None);

        apply.Applied.Should().BeFalse();
        apply.Counters.BatchesCommitted.Should().Be(0);
        (await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()).Should().Be(0);
        var session = await fixture.Context.PhotoUploadSessions.AsNoTracking().SingleAsync();
        session.SourcePhotoTypeId.Should().BeNull();
        session.ReportPhotoSlotId.Should().BeNull();
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

        dryRun.Exceptions.Should().Contain(issue => issue.Code == "PENDING_UPLOAD_OWNER_UNAVAILABLE");
        dryRun.Exceptions.Should().NotContain(issue => issue.Code == "PENDING_LEGACY_UPLOAD");
        var apply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, dryRun.Fingerprint, true),
            CancellationToken.None);

        apply.Applied.Should().BeFalse();
        apply.Counters.BatchesCommitted.Should().Be(0);
        (await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task DeletedOwner_RequiresExplicitRetentionApproval_WithoutRestoringAccount()
    {
        await using var fixture = await MigrationFixture.CreateAsync(deletedTrainee: true);
        var runner = new ReportingPhotoTypesMigrationRunner(fixture.Context);
        var blockedDryRun = await runner.RunAsync(Options(ReportingPhotoTypesMigrationMode.DryRun), CancellationToken.None);

        blockedDryRun.Exceptions.Should().Contain(issue => issue.Code == "RETENTION_APPROVAL_REQUIRED");
        var blockedApply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, blockedDryRun.Fingerprint),
            CancellationToken.None);
        blockedApply.Applied.Should().BeFalse();

        var approvedDryRun = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.DryRun, allowRetainedDeletedOwners: true),
            CancellationToken.None);
        var approvedApply = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, approvedDryRun.Fingerprint, true),
            CancellationToken.None);

        approvedApply.Applied.Should().BeTrue();
        (await fixture.Context.ReportRequestDefinitionSnapshots.CountAsync()).Should().Be(1);
        var trainee = await fixture.Context.Users.IgnoreQueryFilters().SingleAsync(user => user.Id == fixture.Trainee.Id);
        trainee.IsDeleted.Should().BeTrue();
        trainee.Name.Should().StartWith("anonymized_user_");
        (await fixture.Context.TrainerPhotoTypes.SingleAsync(photoType => photoType.SystemKey == "Back"))
            .IsArchived.Should().BeTrue();
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
            request.Id.Should().Be(expected.RequestId);
            request.CreatedAt.Should().Be(expected.RequestCreatedAt);
            request.UpdatedAt.Should().Be(expected.RequestUpdatedAt);
            request.DueAt.Should().Be(expected.DueAt);
            request.SubmittedAt.Should().Be(expected.SubmittedAt);
            request.Note.Should().Be(expected.Note);
            submission.PayloadJson.Should().Be(expected.PayloadJson);
            photo.Id.Should().Be(expected.PhotoId);
            photo.StorageKey.Should().Be(expected.StorageKey);
            photo.CreatedAt.Should().Be(expected.PhotoCreatedAt);
            photo.UpdatedAt.Should().Be(expected.PhotoUpdatedAt);
            photo.ViewType.Should().Be(expected.ViewType);
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
