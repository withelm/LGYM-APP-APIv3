using System.Net;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertHydratedPhoto(document.RootElement, scenario.Photo.StorageKey);
    }

    [Test]
    public async Task GetOwnReportSubmissions_WhenPhotoIdAliasesConflict_OmitsPhotoEnvelope()
    {
        var scenario = await SeedScenarioAsync(conflictingPhotoId: Id<Photo>.New().ToString());
        SetAuthorizationHeader(scenario.TraineeId);

        using var response = await Client.GetAsync("/api/trainee/report-submissions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement[0].GetProperty("answers").GetProperty("photos").GetArrayLength().Should().Be(0);
    }

    [Test]
    public async Task GetOwnReportSubmissions_WhenExactPhotoIdIsDuplicated_DoesNotFailTheResponse()
    {
        var scenario = await SeedScenarioAsync(duplicateExactPhotoId: true);
        SetAuthorizationHeader(scenario.TraineeId);

        using var response = await Client.GetAsync("/api/trainee/report-submissions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertHydratedPhoto(document.RootElement[0], scenario.Photo.StorageKey);
    }

    [Test]
    public async Task GetOwnReportSubmissions_WhenPhotoArrayContainsStorageMetadata_OmitsUnboundEnvelope()
    {
        var scenario = await SeedScenarioAsync(includeMetadataObject: true);
        SetAuthorizationHeader(scenario.TraineeId);

        using var response = await Client.GetAsync("/api/trainee/report-submissions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement[0].GetProperty("answers").GetProperty("photos").GetArrayLength().Should().Be(1);
    }

    [Test]
    public async Task SignedPhotoRead_ActiveSuccessorTrainerCannotReadAnotherTrainersSourcePhoto()
    {
        var scenario = await SeedScenarioAsync();
        var successor = await SeedUserAsync("photo-hydration-successor", "photo-hydration-successor@example.test");
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.UserRoles.Add(new UserRole { UserId = successor.Id, RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId });
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = successor.Id, TraineeId = scenario.TraineeId });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(scenario.TrainerId);
        using var sourceResponse = await Client.GetAsync($"/api/trainer/reporting/photos/{scenario.Photo.Id}/signed-url");
        sourceResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthorizationHeader(successor.Id);
        using var successorResponse = await Client.GetAsync($"/api/trainer/reporting/photos/{scenario.Photo.Id}/signed-url");
        successorResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await successorResponse.Content.ReadAsStringAsync();
        body.Should().NotContain(scenario.Photo.Id.ToString());
        body.Should().NotContain(scenario.Photo.StorageKey);
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
                new UserRole { UserId = successor.Id, RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId },
                new UserRole { UserId = outsider.Id, RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId },
                new UserRole { UserId = admin.Id, RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId },
                new UserRole { UserId = admin.Id, RoleId = RoleSeedDataConfiguration.AdminRoleSeedId });
            database.TrainerTraineeLinks.AddRange(
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = successor.Id, TraineeId = scenario.TraineeId },
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = admin.Id, TraineeId = scenario.TraineeId });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(scenario.TraineeId);
        using var traineeResponse = await Client.GetAsync($"/api/trainee/reporting/photos/history?requestId={scenario.RequestId}");
        traineeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PhotoCountAsync(traineeResponse)).Should().Be(1);

        SetAuthorizationHeader(scenario.TrainerId);
        using var sourceResponse = await Client.GetAsync($"/api/trainer/reporting/photos/history?traineeId={scenario.TraineeId}");
        sourceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PhotoCountAsync(sourceResponse)).Should().Be(1);

        foreach (var actorId in new[] { successor.Id, admin.Id })
        {
            SetAuthorizationHeader(actorId);
            using var response = await Client.GetAsync($"/api/trainer/reporting/photos/history?traineeId={scenario.TraineeId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain(scenario.Photo.Id.ToString());
            body.Should().NotContain(scenario.Photo.StorageKey);
            using var document = JsonDocument.Parse(body);
            document.RootElement.GetProperty("photos").GetArrayLength().Should().Be(0);
        }

        SetAuthorizationHeader(outsider.Id);
        using var outsiderResponse = await Client.GetAsync($"/api/trainer/reporting/photos/history?traineeId={scenario.TraineeId}");
        outsiderResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
                new UserRole { UserId = successor.Id, RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId },
                new UserRole { UserId = outsider.Id, RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId },
                new UserRole { UserId = admin.Id, RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId },
                new UserRole { UserId = admin.Id, RoleId = RoleSeedDataConfiguration.AdminRoleSeedId });
            database.TrainerTraineeLinks.AddRange(
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = successor.Id, TraineeId = scenario.TraineeId },
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = admin.Id, TraineeId = scenario.TraineeId });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(scenario.TraineeId);
        using var traineeResponse = await Client.GetAsync("/api/trainee/report-submissions");
        traineeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await SubmissionCountAsync(traineeResponse)).Should().Be(1);

        SetAuthorizationHeader(scenario.TrainerId);
        using var sourceResponse = await Client.GetAsync($"/api/trainer/trainees/{scenario.TraineeId}/report-submissions");
        sourceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await SubmissionCountAsync(sourceResponse)).Should().Be(1);

        foreach (var actorId in new[] { successor.Id, admin.Id })
        {
            SetAuthorizationHeader(actorId);
            using var response = await Client.GetAsync($"/api/trainer/trainees/{scenario.TraineeId}/report-submissions");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain(scenario.Photo.Id.ToString());
            body.Should().NotContain(scenario.Photo.StorageKey);
            using var document = JsonDocument.Parse(body);
            document.RootElement.GetArrayLength().Should().Be(0);
        }

        SetAuthorizationHeader(outsider.Id);
        using var outsiderResponse = await Client.GetAsync($"/api/trainer/trainees/{scenario.TraineeId}/report-submissions");
        outsiderResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
        var request = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Template = template,
            Status = ReportRequestStatus.Submitted,
            SubmittedAt = now,
            CreatedAt = now
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
            TrainerFeedbackAddedAt = feedbackAddedAt,
            CreatedAt = now
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.ReportTemplates.Add(template);
            database.UserRoles.Add(new UserRole { UserId = trainer.Id, RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId });
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id });
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
        hydrated.GetProperty("storageKey").GetString().Should().Be(canonicalStorageKey);
        hydrated.GetProperty("readUrl").GetString().Should().NotBe("https://expired.example/read");
        hydrated.GetProperty("readUrl").GetString().Should().NotBeNullOrWhiteSpace();
        hydrated.GetProperty("thumbnailUrl").ValueKind.Should().Be(JsonValueKind.Null);
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
