using System.Net;
using System.Text.Json;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using LgymApi.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class ReportSubmissionPhotoHydrationIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task GetOwnReportSubmissions_WhenPhotoUrlsAreExpired_ReturnsFreshCanonicalUrls()
    {
        var scenario = await SeedScenarioAsync();
        SetAuthorizationHeader(scenario.TraineeId);

        using var response = await Client.GetAsync("/api/trainee/report-submissions");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertHydratedPhoto(document.RootElement[0], scenario.Photo.StorageKey);
    }

    [Test]
    public async Task MarkFeedbackRead_WhenHistoricalPhotoUrlsAreExpired_ReturnsFreshCanonicalUrls()
    {
        var scenario = await SeedScenarioAsync(feedbackAddedAt: DateTimeOffset.UtcNow);
        SetAuthorizationHeader(scenario.TraineeId);

        using var response = await Client.PostAsync(
            $"/api/trainee/report-submissions/{scenario.SubmissionId}/mark-feedback-read",
            content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertHydratedPhoto(document.RootElement, scenario.Photo.StorageKey);
        Assert.That(document.RootElement.GetProperty("trainerFeedbackReadAt").ValueKind, Is.EqualTo(JsonValueKind.String));
        Assert.That(document.RootElement.GetProperty("nextRecurringRequest").GetProperty("status").GetString(), Is.EqualTo(nameof(NextRecurringReportRequestStatus.Unscheduled)));
    }

    [Test]
    public async Task GetOwnReportSubmissions_WhenPhotoIdAliasesConflict_OmitsPhotoEnvelope()
    {
        var scenario = await SeedScenarioAsync(conflictingPhotoId: Id<Photo>.New().ToString());
        SetAuthorizationHeader(scenario.TraineeId);

        using var response = await Client.GetAsync("/api/trainee/report-submissions");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement[0].GetProperty("answers").GetProperty("photos").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task GetOwnReportSubmissions_WhenExactPhotoIdIsDuplicated_DoesNotFailTheResponse()
    {
        var scenario = await SeedScenarioAsync(duplicateExactPhotoId: true);
        SetAuthorizationHeader(scenario.TraineeId);

        using var response = await Client.GetAsync("/api/trainee/report-submissions");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertHydratedPhoto(document.RootElement[0], scenario.Photo.StorageKey);
    }

    [Test]
    public async Task GetOwnReportSubmissions_WhenPhotoArrayContainsStorageMetadata_OmitsUnboundEnvelope()
    {
        var scenario = await SeedScenarioAsync(includeMetadataObject: true);
        SetAuthorizationHeader(scenario.TraineeId);

        using var response = await Client.GetAsync("/api/trainee/report-submissions");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement[0].GetProperty("answers").GetProperty("photos").GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public async Task SignedPhotoRead_ActiveSuccessorTrainerCannotReadAnotherTrainersSourcePhoto()
    {
        var scenario = await SeedScenarioAsync();
        var successor = await SeedUserAsync("photo-hydration-successor", "photo-hydration-successor@example.test");
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, successor.Id));
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = successor.Id, TraineeId = scenario.TraineeId });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(scenario.TrainerId);
        using var sourceResponse = await Client.GetAsync($"/api/trainer/reporting/photos/{scenario.Photo.Id}/signed-url");
        Assert.That(sourceResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        SetAuthorizationHeader(successor.Id);
        using var successorResponse = await Client.GetAsync($"/api/trainer/reporting/photos/{scenario.Photo.Id}/signed-url");
        Assert.That(successorResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var body = await successorResponse.Content.ReadAsStringAsync();
        Assert.That(body, Does.Not.Contain(scenario.Photo.Id.ToString()));
        Assert.That(body, Does.Not.Contain(scenario.Photo.StorageKey));
    }

    [Test]
    public async Task PhotoHistory_SourceTraineeAndActiveSourceTrainerCanReadWhileSuccessorOutsiderAndAdminCannotSeePhotoMetadata()
    {
        var scenario = await SeedScenarioAsync();
        var successor = await SeedUserAsync("photo-history-successor", "photo-history-successor@example.test");
        var outsider = await SeedUserAsync("photo-history-outsider", "photo-history-outsider@example.test");
        var admin = await SeedUserAsync("photo-history-admin", "photo-history-admin@example.test");
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.UserRoles.AddRange(
                LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, successor.Id),
                LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, outsider.Id),
                LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, admin.Id),
                new UserRole { UserId = admin.Id, RoleId = RoleSeedDataConfiguration.AdminRoleSeedId });
            database.TrainerTraineeLinks.AddRange(
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = successor.Id, TraineeId = scenario.TraineeId },
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = admin.Id, TraineeId = scenario.TraineeId });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(scenario.TraineeId);
        using var traineeResponse = await Client.GetAsync($"/api/trainee/reporting/photos/history?requestId={scenario.RequestId}");
        Assert.That(traineeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await PhotoCountAsync(traineeResponse)), Is.EqualTo(1));

        SetAuthorizationHeader(scenario.TrainerId);
        using var sourceResponse = await Client.GetAsync($"/api/trainer/reporting/photos/history?traineeId={scenario.TraineeId}");
        Assert.That(sourceResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await PhotoCountAsync(sourceResponse)), Is.EqualTo(1));

        foreach (var actorId in new[] { successor.Id, admin.Id })
        {
            SetAuthorizationHeader(actorId);
            using var response = await Client.GetAsync($"/api/trainer/reporting/photos/history?traineeId={scenario.TraineeId}");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await response.Content.ReadAsStringAsync();
            Assert.That(body, Does.Not.Contain(scenario.Photo.Id.ToString()));
            Assert.That(body, Does.Not.Contain(scenario.Photo.StorageKey));
            using var document = JsonDocument.Parse(body);
            Assert.That(document.RootElement.GetProperty("photos").GetArrayLength(), Is.EqualTo(0));
        }

        SetAuthorizationHeader(outsider.Id);
        using var outsiderResponse = await Client.GetAsync($"/api/trainer/reporting/photos/history?traineeId={scenario.TraineeId}");
        Assert.That(outsiderResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task SubmissionHydration_SourceTraineeAndActiveSourceTrainerCanReadWhileSuccessorOutsiderAndAdminCannotReceivePhotoData()
    {
        var scenario = await SeedScenarioAsync();
        var successor = await SeedUserAsync("photo-submission-successor", "photo-submission-successor@example.test");
        var outsider = await SeedUserAsync("photo-submission-outsider", "photo-submission-outsider@example.test");
        var admin = await SeedUserAsync("photo-submission-admin", "photo-submission-admin@example.test");
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.UserRoles.AddRange(
                LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, successor.Id),
                LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, outsider.Id),
                LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, admin.Id),
                new UserRole { UserId = admin.Id, RoleId = RoleSeedDataConfiguration.AdminRoleSeedId });
            database.TrainerTraineeLinks.AddRange(
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = successor.Id, TraineeId = scenario.TraineeId },
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = admin.Id, TraineeId = scenario.TraineeId });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(scenario.TraineeId);
        using var traineeResponse = await Client.GetAsync("/api/trainee/report-submissions");
        Assert.That(traineeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await SubmissionCountAsync(traineeResponse)), Is.EqualTo(1));

        SetAuthorizationHeader(scenario.TrainerId);
        using var sourceResponse = await Client.GetAsync($"/api/trainer/trainees/{scenario.TraineeId}/report-submissions");
        Assert.That(sourceResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await SubmissionCountAsync(sourceResponse)), Is.EqualTo(1));

        foreach (var actorId in new[] { successor.Id, admin.Id })
        {
            SetAuthorizationHeader(actorId);
            using var response = await Client.GetAsync($"/api/trainer/trainees/{scenario.TraineeId}/report-submissions");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await response.Content.ReadAsStringAsync();
            Assert.That(body, Does.Not.Contain(scenario.Photo.Id.ToString()));
            Assert.That(body, Does.Not.Contain(scenario.Photo.StorageKey));
            using var document = JsonDocument.Parse(body);
            Assert.That(document.RootElement.GetArrayLength(), Is.EqualTo(0));
        }

        SetAuthorizationHeader(outsider.Id);
        using var outsiderResponse = await Client.GetAsync($"/api/trainer/trainees/{scenario.TraineeId}/report-submissions");
        Assert.That(outsiderResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task DeletedSubject_CutsOffExistingTrainerSessionFromPhotoUrlsAndNestedMetadataWithoutDeletingStoredObject(bool pending, bool formerTrainer)
    {
        var scenario = await SeedScenarioAsync();
        LocalPhotoDevelopmentStore store;
        using (var scope = Factory.Services.CreateScope())
        {
            store = scope.ServiceProvider.GetRequiredService<LocalPhotoDevelopmentStore>();
        }

        var storedBytes = new byte[] { 1, 2, 3 };
        await using (var content = new MemoryStream(storedBytes))
        {
            await store.SaveAsync(scenario.Photo.StorageKey, content);
        }

        try
        {
            SetAuthorizationHeader(scenario.TrainerId);
            using (var scope = Factory.Services.CreateScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var subject = database.Users.Single(user => user.Id == scenario.TraineeId);
                subject.IsDeleted = !pending;
                subject.DeletionState = pending ? AccountDeletionState.PendingDeletion : AccountDeletionState.Active;
                if (formerTrainer)
                {
                    foreach (var link in database.TrainerTraineeLinks.Where(link => link.TraineeId == scenario.TraineeId)) link.IsDeleted = true;
                    foreach (var episode in database.CoachingEpisodes.Where(episode => episode.TraineeId == scenario.TraineeId)) episode.EndedAt = DateTimeOffset.UtcNow;
                }
                await database.SaveChangesAsync();
            }

            using var signedResponse = await Client.GetAsync($"/api/trainer/reporting/photos/{scenario.Photo.Id}/signed-url");
            Assert.That(signedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var signedBody = await signedResponse.Content.ReadAsStringAsync();
            Assert.That(signedBody, Does.Not.Contain(scenario.Photo.Id.ToString()));
            Assert.That(signedBody, Does.Not.Contain(scenario.Photo.StorageKey));

            using var historyResponse = await Client.GetAsync($"/api/trainer/reporting/photos/history?traineeId={scenario.TraineeId}");
            Assert.That(historyResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var historyBody = await historyResponse.Content.ReadAsStringAsync();
            Assert.That(historyBody, Does.Not.Contain(scenario.Photo.Id.ToString()));
            Assert.That(historyBody, Does.Not.Contain(scenario.Photo.StorageKey));

            using var submissionsResponse = await Client.GetAsync($"/api/trainer/trainees/{scenario.TraineeId}/report-submissions");
            Assert.That(submissionsResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var submissionsBody = await submissionsResponse.Content.ReadAsStringAsync();
            Assert.That(submissionsBody, Does.Not.Contain(scenario.Photo.Id.ToString()));
            Assert.That(submissionsBody, Does.Not.Contain(scenario.Photo.StorageKey));
            Assert.That((await store.ReadAsync(scenario.Photo.StorageKey)), Is.EqualTo(storedBytes));
        }
        finally
        {
            await store.DeleteAsync(scenario.Photo.StorageKey);
        }
    }

    private async Task<Scenario> SeedScenarioAsync(
        DateTimeOffset? feedbackAddedAt = null,
        string? conflictingPhotoId = null,
        bool duplicateExactPhotoId = false,
        bool includeMetadataObject = false)
    {
        var trainer = await SeedUserAsync("photo-hydration-trainer", "photo-hydration-trainer@example.test");
        var trainee = await SeedUserAsync("photo-hydration-trainee", "photo-hydration-trainee@example.test");
        var now = DateTimeOffset.UtcNow;
        var template = new ReportTemplate
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainer.Id,
            Name = "Historical photo report",
            CreatedAt = now,
            Fields =
            [
                new ReportTemplateField
                {
                    Id = Id<ReportTemplateField>.New(),
                    Key = "photos",
                    Label = "Photos",
                    Type = ReportFieldType.Photos,
                    IsRequired = false,
                    Order = 0,
                    CreatedAt = now
                }
            ]
        };
        var episode = new CoachingEpisode
        {
            Id = Id<CoachingEpisode>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            StartedAt = now,
            StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation
        };
        var request = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Template = template,
            Status = ReportRequestStatus.Submitted,
            SubmittedAt = now,
            CreatedAt = now,
            SourceCoachingEpisodeId = episode.Id
        };
        var photo = new Photo
        {
            Id = Id<Photo>.New(),
            ReportRequestId = request.Id,
            OwnerUserId = trainee.Id,
            UploaderUserId = trainee.Id,
            ViewType = PhotoViewType.Front.ToString(),
            StorageKey = $"photos/{trainee.Id}/{request.Id}/Front/canonical.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 1024,
            Checksum = "etag",
            CreatedAt = now
        };
        var photoPayload = new Dictionary<string, object?>
        {
            ["photoId"] = photo.Id.ToString(),
            ["_id"] = photo.Id.ToString(),
            ["storageKey"] = "photos/attacker/foreign.jpg",
            ["readUrl"] = "https://expired.example/read",
            ["thumbnailUrl"] = "https://expired.example/thumb"
        };
        if (conflictingPhotoId != null)
        {
            photoPayload["PhotoId"] = conflictingPhotoId;
        }

        var photoAnswers = new List<object?> { photoPayload };
        if (includeMetadataObject)
        {
            photoAnswers.Add(new Dictionary<string, object?>
            {
                ["caption"] = "front",
                ["_id"] = "caption-1",
                ["storageKey"] = "captions/front.txt"
            });
        }

        var payloadJson = duplicateExactPhotoId
            ? $"{{\"photos\":[{{\"photoId\":\"{photo.Id}\",\"photoId\":\"{photo.Id}\",\"storageKey\":\"{photo.StorageKey}\",\"readUrl\":\"https://expired.example/read\"}}]}}"
            : JsonSerializer.Serialize(new Dictionary<string, object?> { ["photos"] = photoAnswers });

        var submission = new ReportSubmission
        {
            Id = Id<ReportSubmission>.New(),
            ReportRequestId = request.Id,
            ReportRequest = request,
            TraineeId = trainee.Id,
            PayloadJson = payloadJson,
            SourceCoachingEpisodeId = episode.Id,
            TrainerFeedbackAddedAt = feedbackAddedAt,
            CreatedAt = now
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.ReportTemplates.Add(template);
            database.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, trainer.Id));
            database.CoachingEpisodes.Add(episode);
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                EpisodeId = episode.Id
            });
            database.ReportRequests.Add(request);
            database.Photos.Add(photo);
            database.ReportSubmissions.Add(submission);
            await database.SaveChangesAsync();
        }

        return new Scenario(trainer.Id, trainee.Id, request.Id, photo, submission.Id);
    }

    private static void AssertHydratedPhoto(JsonElement submission, string canonicalStorageKey)
    {
        var hydrated = submission.GetProperty("answers").GetProperty("photos")[0];
        Assert.That(hydrated.GetProperty("storageKey").GetString(), Is.EqualTo(canonicalStorageKey));
        Assert.That(hydrated.GetProperty("readUrl").GetString(), Is.Not.EqualTo("https://expired.example/read"));
        Assert.That(string.IsNullOrWhiteSpace(hydrated.GetProperty("readUrl").GetString()), Is.False);
        Assert.That(hydrated.GetProperty("thumbnailUrl").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    private static async Task<int> PhotoCountAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("photos").GetArrayLength();
    }

    private static async Task<int> SubmissionCountAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetArrayLength();
    }

    private sealed record Scenario(Id<User> TrainerId, Id<User> TraineeId, Id<ReportRequest> RequestId, Photo Photo, Id<ReportSubmission> SubmissionId);
}
