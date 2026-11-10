using System.Text.Json;
using FluentAssertions;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportSubmissionPhotoHydrationTests
{
    [Test]
    public async Task GetOwnSubmissionsAsync_WhenHistoricalPhotoHasCanonicalThumbnail_ReturnsFreshCanonicalUrls()
    {
        var fixture = CreateFixture();
        var photo = fixture.Photos[0];
        var submission = CreateSubmission(fixture.RequestId, fixture.TraineeId, new Dictionary<string, object?>
        {
            ["photoId"] = photo.Id.ToString(),
            ["_id"] = photo.Id.ToString(),
            ["StorageKey"] = photo.StorageKey,
            ["ReadUrl"] = "https://expired.example/read",
            ["ThumbnailUrl"] = "https://expired.example/thumb"
        });
        fixture.ReturnSubmissions(submission);

        var result = await fixture.Service.GetOwnSubmissionsAsync(ReportingTestData.Account(fixture.TraineeId));

        var hydrated = GetOnlyPhoto(result.Value.Single());
        hydrated.GetProperty("readUrl").GetString().Should().Be($"fresh:{photo.StorageKey}");
        hydrated.GetProperty("thumbnailUrl").GetString().Should().Be($"fresh:{photo.ThumbnailStorageKey}");
        hydrated.TryGetProperty("ReadUrl", out _).Should().BeFalse();
        hydrated.TryGetProperty("ThumbnailUrl", out _).Should().BeFalse();
    }

    [Test]
    public async Task GetOwnSubmissionsAsync_WhenCanonicalPhotoHasNoThumbnail_ClearsExpiredThumbnailUrl()
    {
        var fixture = CreateFixture(thumbnailStorageKey: null);
        var photo = fixture.Photos[0];
        fixture.ReturnSubmissions(CreateSubmission(fixture.RequestId, fixture.TraineeId, new Dictionary<string, object?>
        {
            ["photoId"] = photo.Id.ToString(),
            ["readUrl"] = "https://expired.example/read",
            ["thumbnailUrl"] = "https://expired.example/thumb"
        }));

        var result = await fixture.Service.GetOwnSubmissionsAsync(ReportingTestData.Account(fixture.TraineeId));

        var hydrated = GetOnlyPhoto(result.Value.Single());
        hydrated.GetProperty("readUrl").GetString().Should().Be($"fresh:{photo.StorageKey}");
        hydrated.GetProperty("thumbnailUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Test]
    public async Task GetOwnSubmissionsAsync_WhenPayloadStorageKeyIsTampered_SignsCanonicalStorageKey()
    {
        var fixture = CreateFixture();
        using var cancellation = new CancellationTokenSource();
        var photo = fixture.Photos[0];
        fixture.ReturnSubmissions(CreateSubmission(fixture.RequestId, fixture.TraineeId, new Dictionary<string, object?>
        {
            ["photoId"] = photo.Id.ToString(),
            ["storageKey"] = "photos/attacker/foreign.jpg",
            ["readUrl"] = "https://expired.example/read"
        }));

        await fixture.Service.GetOwnSubmissionsAsync(ReportingTestData.Account(fixture.TraineeId), cancellation.Token);

        await fixture.Storage.Received().GenerateSignedReadUrlAsync(photo.StorageKey, Arg.Any<TimeSpan>(), cancellation.Token);
        await fixture.Storage.DidNotReceive().GenerateSignedReadUrlAsync("photos/attacker/foreign.jpg", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetOwnSubmissionsAsync_WhenPayloadHasMalformedId_OmitsPhotoEnvelopeWithoutSigning()
    {
        var fixture = CreateFixture();
        fixture.ReturnSubmissions(CreateSubmission(fixture.RequestId, fixture.TraineeId, new Dictionary<string, object?>
        {
            ["photoId"] = "not-a-photo-id",
            ["storageKey"] = fixture.Photos[0].StorageKey,
            ["readUrl"] = "https://expired.example/read",
            ["thumbnailUrl"] = "https://expired.example/thumb"
        }));

        var result = await fixture.Service.GetOwnSubmissionsAsync(ReportingTestData.Account(fixture.TraineeId));

        result.Value.Single().Answers["photos"].GetArrayLength().Should().Be(0);
        await fixture.Storage.DidNotReceive().GenerateSignedReadUrlAsync(fixture.Photos[0].StorageKey, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetOwnSubmissionsAsync_WhenCanonicalPhotoIsMissing_OmitsPhotoEnvelopeWithoutSigning()
    {
        var fixture = CreateFixture(photos: []);
        fixture.ReturnSubmissions(CreateSubmission(fixture.RequestId, fixture.TraineeId, new Dictionary<string, object?>
        {
            ["photoId"] = Id<Photo>.New().ToString(),
            ["storageKey"] = "photos/missing.jpg",
            ["readUrl"] = "https://expired.example/read"
        }));

        var result = await fixture.Service.GetOwnSubmissionsAsync(ReportingTestData.Account(fixture.TraineeId));

        result.IsSuccess.Should().BeTrue();
        result.Value.Single().Answers["photos"].GetArrayLength().Should().Be(0);
        await fixture.Storage.DidNotReceive().GenerateSignedReadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetOwnSubmissionsAsync_WhenCanonicalPhotoOwnerDoesNotMatchSourceRequest_OmitsMetadataWithoutSigning()
    {
        var fixture = CreateFixture();
        var inconsistentPhoto = fixture.Photos[0] with { OwnerAccountId = ReportingTestData.AccountId(Id<User>.New()) };
        var inconsistentFixture = new Fixture(fixture.TraineeId, fixture.RequestId, [inconsistentPhoto]);
        inconsistentFixture.ReturnSubmissions(CreateSubmission(fixture.RequestId, fixture.TraineeId, new Dictionary<string, object?>
        {
            ["photoId"] = inconsistentPhoto.Id.ToString(),
            ["storageKey"] = inconsistentPhoto.StorageKey,
            ["readUrl"] = "https://expired.example/read",
            ["thumbnailUrl"] = "https://expired.example/thumb"
        }));

        var result = await inconsistentFixture.Service.GetOwnSubmissionsAsync(ReportingTestData.Account(inconsistentFixture.TraineeId));

        result.Value.Single().Answers["photos"].GetArrayLength().Should().Be(0);
        await inconsistentFixture.Storage.DidNotReceive().GenerateSignedReadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetOwnSubmissionsAsync_WhenAuthoritativeSourceRequestIsMissing_OmitsPhotoEnvelopeWithoutSigning()
    {
        var fixture = CreateFixture();
        var photo = fixture.Photos[0];
        fixture.ReturnSubmissions(CreateSubmission(fixture.RequestId, fixture.TraineeId, new Dictionary<string, object?>
        {
            ["photoId"] = photo.Id.ToString(),
            ["storageKey"] = photo.StorageKey,
            ["readUrl"] = "https://expired.example/read"
        }, additionalPhotoItems: [new Dictionary<string, object?> { ["storageKey"] = "photos/leaked.jpg", ["caption"] = "front" }]));
        fixture.RequestPersistence.FindRequestByIdAsync(fixture.RequestId, Arg.Any<CancellationToken>()).Returns((ReportRequestPersistenceModel?)null);

        var result = await fixture.Service.GetOwnSubmissionsAsync(ReportingTestData.Account(fixture.TraineeId));

        result.Value.Single().Answers["photos"].GetArrayLength().Should().Be(0);
        await fixture.Storage.DidNotReceive().GenerateSignedReadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetOwnSubmissionsAsync_WhenAuthoritativeRequestDisagreesWithSubmissionBinding_OmitsPhotoEnvelopeWithoutSigning()
    {
        var traineeId = Id<User>.New();
        var firstRequestId = Id<ReportRequest>.New();
        var secondRequestId = Id<ReportRequest>.New();
        var firstPhoto = CreatePhoto(firstRequestId, traineeId, "photos/first.jpg");
        var fixture = new Fixture(traineeId, firstRequestId, [firstPhoto]);
        var mismatchedSource = CreateSubmission(secondRequestId, traineeId, new Dictionary<string, object?> { ["photoId"] = firstPhoto.Id.ToString() });
        var inconsistentSubmission = mismatchedSource with { ReportRequestId = firstRequestId };
        fixture.ReturnSubmissions(inconsistentSubmission);

        var result = await fixture.Service.GetOwnSubmissionsAsync(ReportingTestData.Account(traineeId));

        result.Value.Should().BeEmpty();
        await fixture.Storage.DidNotReceive().GenerateSignedReadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetOwnSubmissionsAsync_WhenAuthoritativeRequestTraineeDisagreesWithSubmission_OmitsPhotoEnvelopeWithoutSigning()
    {
        var fixture = CreateFixture();
        var photo = fixture.Photos[0];
        var submission = CreateSubmission(fixture.RequestId, fixture.TraineeId, new Dictionary<string, object?> { ["photoId"] = photo.Id.ToString() });
        fixture.ReturnSubmissions(submission);
        var mismatchedSource = submission.ReportRequest with { TraineeId = ReportingTestData.AccountId(Id<User>.New()) };
        fixture.RequestPersistence.FindRequestByIdAsync(fixture.RequestId, Arg.Any<CancellationToken>()).Returns(mismatchedSource);

        var result = await fixture.Service.GetOwnSubmissionsAsync(ReportingTestData.Account(fixture.TraineeId));

        result.Value.Single().Answers["photos"].GetArrayLength().Should().Be(0);
        await fixture.Storage.DidNotReceive().GenerateSignedReadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetOwnSubmissionsAsync_WhenLegacyPayloadHasMatchingStorageKey_ReturnsFreshCanonicalUrl()
    {
        var fixture = CreateFixture();
        var photo = fixture.Photos[0];
        fixture.ReturnSubmissions(CreateSubmission(fixture.RequestId, fixture.TraineeId, new Dictionary<string, object?>
        {
            ["storageKey"] = photo.StorageKey,
            ["readUrl"] = "https://expired.example/read"
        }, photoAsObject: true));

        var result = await fixture.Service.GetOwnSubmissionsAsync(ReportingTestData.Account(fixture.TraineeId));

        result.Value.Single().Answers["photos"].GetProperty("readUrl").GetString().Should().Be($"fresh:{photo.StorageKey}");
    }

    [Test]
    public async Task GetOwnSubmissionsAsync_WhenMultipleReportsContainPhotos_LoadsCanonicalPhotosOnce()
    {
        var traineeId = Id<User>.New();
        var firstRequestId = Id<ReportRequest>.New();
        var secondRequestId = Id<ReportRequest>.New();
        var photos = new[]
        {
            CreatePhoto(firstRequestId, traineeId, "photos/first.jpg"),
            CreatePhoto(secondRequestId, traineeId, "photos/second.jpg")
        };
        var fixture = new Fixture(traineeId, firstRequestId, photos);
        fixture.ReturnSubmissions(
            CreateSubmission(firstRequestId, traineeId, new Dictionary<string, object?> { ["photoId"] = photos[0].Id.ToString() }),
            CreateSubmission(secondRequestId, traineeId, new Dictionary<string, object?> { ["photoId"] = photos[1].Id.ToString() }));

        await fixture.Service.GetOwnSubmissionsAsync(ReportingTestData.Account(traineeId));

        await fixture.PhotoPersistence.Received(1).ListByRequestsAsync(
            Arg.Is<IReadOnlyCollection<Id<ReportRequest>>>(ids => ids.Count == 2 && ids.Contains(firstRequestId) && ids.Contains(secondRequestId)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetOwnSubmissionsAsync_WhenPhotosArrayContainsCaptionMetadata_PreservesMetadataObject()
    {
        var fixture = CreateFixture();
        fixture.ReturnSubmissions(CreateSubmission(
            fixture.RequestId,
            fixture.TraineeId,
            new Dictionary<string, object?> { ["photoId"] = fixture.Photos[0].Id.ToString(), ["readUrl"] = "https://expired.example/read" },
            additionalPhotoItems: [new Dictionary<string, object?> { ["caption"] = "front" }]));

        var result = await fixture.Service.GetOwnSubmissionsAsync(ReportingTestData.Account(fixture.TraineeId));

        var metadata = result.Value.Single().Answers["photos"].EnumerateArray().Last();
        metadata.EnumerateObject().Select(property => property.Name).Should().Equal("caption");
        metadata.GetProperty("caption").GetString().Should().Be("front");
    }

    [Test]
    public async Task GetOwnSubmissionsAsync_WhenPhotosArrayContainsStorageOnlyEnvelope_OmitsItWithoutSigning()
    {
        var fixture = CreateFixture();
        fixture.ReturnSubmissions(CreateSubmission(
            fixture.RequestId,
            fixture.TraineeId,
            new Dictionary<string, object?> { ["photoId"] = fixture.Photos[0].Id.ToString(), ["readUrl"] = "https://expired.example/read" },
            additionalPhotoItems: [new Dictionary<string, object?> { ["storageKey"] = "captions/front.txt", ["caption"] = "front" }]));

        var result = await fixture.Service.GetOwnSubmissionsAsync(ReportingTestData.Account(fixture.TraineeId));

        result.Value.Single().Answers["photos"].GetArrayLength().Should().Be(1);
        await fixture.Storage.DidNotReceive().GenerateSignedReadUrlAsync("captions/front.txt", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetOwnSubmissionsAsync_WhenAnswerIsNotDeclaredAsPhotosField_LeavesAnswerUntouched()
    {
        var fixture = CreateFixture();
        fixture.ReturnSubmissions(CreateSubmission(
            fixture.RequestId,
            fixture.TraineeId,
            new Dictionary<string, object?>
            {
                ["photoId"] = fixture.Photos[0].Id.ToString(),
                ["readUrl"] = "https://expired.example/read"
            },
            fieldType: ReportFieldType.Text));

        var result = await fixture.Service.GetOwnSubmissionsAsync(ReportingTestData.Account(fixture.TraineeId));

        GetOnlyPhoto(result.Value.Single()).GetProperty("readUrl").GetString().Should().Be("https://expired.example/read");
        await fixture.Storage.DidNotReceive().GenerateSignedReadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetOwnSubmissionsAsync_WhenSubjectIsSoftDeleted_RedactsNestedPhotoMetadataWithoutSigning()
    {
        var accountLookup = Substitute.For<IAccountLookupService>();
        accountLookup.GetByIdAsync(Arg.Any<Id<AccountReference>>(), Arg.Any<CancellationToken>())
            .Returns((AccountLookup?)null);
        var fixture = CreateFixture(accountLookupService: accountLookup);
        fixture.ReturnSubmissions(CreateSubmission(fixture.RequestId, fixture.TraineeId, new Dictionary<string, object?>
        {
            ["photoId"] = fixture.Photos[0].Id.ToString(),
            ["storageKey"] = fixture.Photos[0].StorageKey,
            ["readUrl"] = "https://expired.example/read"
        }));

        var result = await fixture.Service.GetOwnSubmissionsAsync(ReportingTestData.Account(fixture.TraineeId));

        result.IsSuccess.Should().BeTrue();
        result.Value.Single().Answers["photos"].GetArrayLength().Should().Be(0);
        await fixture.Storage.DidNotReceive().GenerateSignedReadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    private static Fixture CreateFixture(
        string? thumbnailStorageKey = "photos/thumb.jpg",
        IReadOnlyList<ReportPhotoPersistenceModel>? photos = null,
        IAccountLookupService? accountLookupService = null)
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        photos ??= [CreatePhoto(requestId, traineeId, "photos/canonical.jpg", thumbnailStorageKey)];
        return new Fixture(traineeId, requestId, photos, accountLookupService);
    }

    private static ReportPhotoPersistenceModel CreatePhoto(
        Id<ReportRequest> requestId,
        Id<User> traineeId,
        string storageKey,
        string? thumbnailStorageKey = null)
        => new(
            Id<Photo>.New(),
            storageKey,
            "image/jpeg",
            1024,
            "etag",
            thumbnailStorageKey,
            PhotoViewType.Front.ToString(),
            requestId,
            ReportingTestData.AccountId(traineeId),
            ReportingTestData.AccountId(traineeId),
            DateTimeOffset.UtcNow,
            false);

    private static ReportSubmissionPersistenceModel CreateSubmission(
        Id<ReportRequest> requestId,
        Id<User> traineeId,
        Dictionary<string, object?> photo,
        bool photoAsObject = false,
        object?[]? additionalPhotoItems = null,
        ReportFieldType fieldType = ReportFieldType.Photos)
    {
        var now = DateTimeOffset.UtcNow;
        var templateId = Id<ReportTemplate>.New();
        var template = new ReportTemplatePersistenceModel(
            templateId,
            Id<AccountReference>.New(),
            "Photo report",
            null,
            now,
            false,
            [new ReportTemplateFieldPersistenceModel(Id<ReportTemplateField>.New(), "photos", "Photos", fieldType, false, 1, null, now)]);
        var request = new ReportRequestPersistenceModel(
            requestId,
            template.TrainerId,
            ReportingTestData.AccountId(traineeId),
            templateId,
            null,
            ReportRequestStatus.Submitted,
            null,
            now,
            null,
            now,
            false,
            template,
            null);
        return new ReportSubmissionPersistenceModel(
            Id<ReportSubmission>.New(),
            requestId,
            ReportingTestData.AccountId(traineeId),
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["photos"] = photoAsObject
                    ? photo
                    : new object?[] { photo }.Concat(additionalPhotoItems ?? []).ToArray()
            }),
            null,
            null,
            null,
            null,
            now,
            request);
    }

    private static JsonElement GetOnlyPhoto(ReportSubmissionResult submission)
        => submission.Answers["photos"].EnumerateArray().Single();

    private sealed class Fixture
    {
        private readonly IReportRequestSubmissionPersistence _submissions;
        private readonly IReportPhotoPersistence _photos;

        public Fixture(
            Id<User> traineeId,
            Id<ReportRequest> requestId,
            IReadOnlyList<ReportPhotoPersistenceModel> photos,
            IAccountLookupService? accountLookupService = null)
        {
            TraineeId = traineeId;
            RequestId = requestId;
            Photos = photos;
            _submissions = Substitute.For<IReportRequestSubmissionPersistence>();
            _photos = Substitute.For<IReportPhotoPersistence>();
            Storage = Substitute.For<IPhotoStorageProvider>();
            _photos.ListByRequestAsync(requestId, Arg.Any<CancellationToken>()).Returns(photos);
            _photos.ListByRequestsAsync(Arg.Any<IReadOnlyCollection<Id<ReportRequest>>>(), Arg.Any<CancellationToken>())
                .Returns(call => Photos.Where(photo => call.ArgAt<IReadOnlyCollection<Id<ReportRequest>>>(0).Contains(photo.ReportRequestId)).ToList());
            Storage.GenerateSignedReadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                .Returns(call => $"fresh:{call.ArgAt<string>(0)}");
            var accountLookup = accountLookupService ?? CreateActiveAccountLookup();
            Service = new ReportingService(
                Substitute.For<IReportTemplatePersistence>(),
                _submissions,
                Substitute.For<IRecurringReportAssignmentPersistence>(),
                _photos,
                Substitute.For<IReportingRelationshipAccessPersistence>(),
                ReportingAuthorizationTestFactory.Create(),
                accountLookup,
                new ReportSubmissionAcceptedProgressCommandFactory(),
                Substitute.For<ICommandDispatcher>(),
                Substitute.For<ICommandOutboxWriter>(),
                Substitute.For<IUnitOfWork>(),
                Storage,
                ReportingTestData.Mapper(),
                Substitute.For<ILogger<ReportingService>>(),
                new PhotoStorageOptions());
        }

        public Id<User> TraineeId { get; }
        public Id<ReportRequest> RequestId { get; }
        public IReadOnlyList<ReportPhotoPersistenceModel> Photos { get; }
        public IReportPhotoPersistence PhotoPersistence => _photos;
        public IReportRequestSubmissionPersistence RequestPersistence => _submissions;
        public IPhotoStorageProvider Storage { get; }
        public ReportingService Service { get; }

        public void ReturnSubmissions(params ReportSubmissionPersistenceModel[] submissions)
        {
            _submissions.ListSubmissionsByTraineeAsync(ReportingTestData.AccountId(TraineeId), Arg.Any<CancellationToken>())
                .Returns(submissions);
            _submissions.FindRequestByIdAsync(Arg.Any<Id<ReportRequest>>(), Arg.Any<CancellationToken>())
                .Returns(call => submissions.FirstOrDefault(submission => submission.ReportRequestId == call.ArgAt<Id<ReportRequest>>(0))?.ReportRequest);
        }

        private static IAccountLookupService CreateActiveAccountLookup()
        {
            var accountLookup = Substitute.For<IAccountLookupService>();
            accountLookup.GetByIdAsync(Arg.Any<Id<AccountReference>>(), Arg.Any<CancellationToken>())
                .Returns(call => new AccountLookup(
                    call.ArgAt<Id<AccountReference>>(0),
                    "Active subject",
                    "active-subject@example.test",
                    null,
                    "en",
                    "UTC",
                    DateTimeOffset.UtcNow));
            return accountLookup;
        }
    }
}
