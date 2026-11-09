using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FluentAssertions;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LgymApi.IntegrationTests.Authorization;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class ExerciseMediaTests : IntegrationTestBase
{
    private WebApplicationFactory<Program>? _mediaFactory;
    private HttpClient? _mediaClient;
    private readonly MemoryExerciseMediaStorage _storage = new();
    private static readonly byte[] Gif = Convert.FromHexString("47494638396101000100800000000000ffffff21ff0b4e45545343415045322e30030100000021f904000a0000002c000000000100010000020244010021f90400140000002c00000000010001000002024c01003b");

    [TearDown]
    public void DisposeMediaHost() { _mediaClient?.Dispose(); _mediaFactory?.Dispose(); _mediaClient = null; _mediaFactory = null; _storage.Reset(); }

    private HttpClient MediaClient()
    {
        if (_mediaClient is null)
        {
            _mediaFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IExerciseMediaStorage>(_storage)));
            _mediaClient = _mediaFactory.CreateClient();
        }
        _mediaClient.DefaultRequestHeaders.Authorization = Client.DefaultRequestHeaders.Authorization;
        return _mediaClient;
    }

    private async Task<Exercise> GlobalAsync(string? image = null)
    {
        var exercise = new Exercise { Id = Id<Exercise>.New(), Name = "Reviewed fixture", BodyPart = BodyParts.Chest, Description = "Retain description", EloFormula = ExerciseEloFormula.VolumeWeighted, Image = image };
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        return exercise;
    }

    private async Task<ExerciseMediaUploadDto> UploadAsync(Exercise? exercise = null, byte[]? bytes = null, string contentType = "image/gif", bool verify = true)
    {
        bytes ??= Gif;
        var result = await MediaClient().PostAsJsonAsync("/api/exercise/media/uploads", new { contentType, sizeBytes = bytes.Length, sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), exerciseId = exercise?.Id.ToString() });
        result.StatusCode.Should().Be(HttpStatusCode.OK, await result.Content.ReadAsStringAsync());
        var upload = (await result.Content.ReadFromJsonAsync<ExerciseMediaUploadDto>())!;
        _storage.Private["staging/" + upload.UploadReference + "/source"] = bytes;
        if (verify)
        {
            var verified = await MediaClient().PostAsync($"/api/exercise/media/uploads/{upload.UploadReference}/verify", null);
            verified.StatusCode.Should().Be(HttpStatusCode.OK, await verified.Content.ReadAsStringAsync());
        }
        return upload;
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/exercise/media/uploads", "admin", "current-permission-allow")]
    [AuthorizationEvidence("POST", "/api/exercise/media/uploads/{uploadReference}/verify", "admin", "current-permission-allow")]
    [AuthorizationEvidence("POST", "/api/exercise/media/create", "admin", "current-permission-allow")]
    public async Task GlobalCreate_WithGif_IsIdempotentAndPublishesOnlyVerifiedBytes()
    {
        var manager = await SeedAdminAsync(); SetAuthorizationHeader(manager.Id);
        var upload = await UploadAsync();
        var body = new { operationId = Id<ExerciseMediaOperationReference>.New().ToString(), name = "GIF create fixture", bodyPart = "Chest", uploadReference = upload.UploadReference };
        var first = await MediaClient().PostAsJsonAsync("/api/exercise/media/create", body);
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        var saved = (await first.Content.ReadFromJsonAsync<ExerciseMediaMutationDto>())!;
        var second = await MediaClient().PostAsJsonAsync("/api/exercise/media/create", body);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<ExerciseMediaMutationDto>())!.Id.Should().Be(saved.Id);
        saved.Image.Should().StartWith("https://exercise.test/exercises/" + saved.Id + "/");
        _storage.Published.Should().ContainSingle();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Exercises.CountAsync(x => x.Name == body.name)).Should().Be(1);
        var session = await db.ExerciseMediaUploads.SingleAsync(x => x.OperationId == OperationId(body.operationId));
        session.Status.Should().Be("bound");
        session.SourceSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(Gif)));
        saved.Image.Should().Contain(session.PublishedSha256!);
        // Reusable staging PUT cannot replace frozen, verified publication bytes.
        _storage.Private["staging/" + upload.UploadReference + "/source"] = [1, 2, 3];
        var replay = await MediaClient().PostAsJsonAsync("/api/exercise/media/create", body);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task GlobalCreate_WithoutMedia_DuplicateSubmissionCreatesOneRecord()
    {
        var manager = await SeedAdminAsync(); SetAuthorizationHeader(manager.Id);
        var body = new { operationId = Id<ExerciseMediaOperationReference>.New().ToString(), name = "No media fixture", bodyPart = "Back" };
        var first = await MediaClient().PostAsJsonAsync("/api/exercise/media/create", body);
        var second = await MediaClient().PostAsJsonAsync("/api/exercise/media/create", body);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<ExerciseMediaMutationDto>())!.Id.Should().Be((await second.Content.ReadFromJsonAsync<ExerciseMediaMutationDto>())!.Id);
    }

    [Test]
    public async Task PublicationFailure_RetryReusesDurablyReservedUuidAndImmutableObject()
    {
        var manager = await SeedAdminAsync(); SetAuthorizationHeader(manager.Id);
        var upload = await UploadAsync();
        var body = new { operationId = Id<ExerciseMediaOperationReference>.New().ToString(), name = "Interrupted create", bodyPart = "Chest", uploadReference = upload.UploadReference };
        _storage.FailAfterPublish = true;
        (await MediaClient().PostAsJsonAsync("/api/exercise/media/create", body)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        string reserved;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var intent = await db.ExerciseMediaUploads.SingleAsync(x => x.OperationId == OperationId(body.operationId));
            intent.Status.Should().Be("publishing");
            reserved = intent.TargetExerciseId!.Value.ToString();
            (await db.Exercises.AnyAsync(x => x.Name == body.name)).Should().BeFalse();
        }
        var retry = await MediaClient().PostAsJsonAsync("/api/exercise/media/create", body);
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        (await retry.Content.ReadFromJsonAsync<ExerciseMediaMutationDto>())!.Id.Should().Be(reserved);
        _storage.Published.Should().ContainSingle();
    }

    [Test]
    [AuthorizationEvidence("PATCH", "/api/exercise/{id}/media", "admin", "current-permission-allow")]
    public async Task MediaOnly_ReplaceRemoveAndConditionalRollbackPreserveMetadata()
    {
        var manager = await SeedAdminAsync(); SetAuthorizationHeader(manager.Id);
        var exercise = await GlobalAsync("https://legacy.example/retained.png");
        var upload = await UploadAsync(exercise);
        var change = new { operationId = Id<ExerciseMediaOperationReference>.New().ToString(), expectedImage = exercise.Image, uploadReference = upload.UploadReference };
        var replaced = await MediaClient().PatchAsJsonAsync($"/api/exercise/{exercise.Id}/media", change);
        replaced.StatusCode.Should().Be(HttpStatusCode.OK, await replaced.Content.ReadAsStringAsync());
        var published = (await replaced.Content.ReadFromJsonAsync<ExerciseMediaMutationDto>())!.Image;
        (await MediaClient().PatchAsJsonAsync($"/api/exercise/{exercise.Id}/media", new { operationId = Id<ExerciseMediaOperationReference>.New().ToString(), expectedImage = exercise.Image, remove = true })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MediaClient().PatchAsJsonAsync($"/api/exercise/{exercise.Id}/media", new { operationId = Id<ExerciseMediaOperationReference>.New().ToString(), expectedImage = published, restoreImage = exercise.Image })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await MediaClient().PatchAsJsonAsync($"/api/exercise/{exercise.Id}/media", new { operationId = Id<ExerciseMediaOperationReference>.New().ToString(), expectedImage = exercise.Image, remove = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Exercises.SingleAsync(x => x.Id == exercise.Id);
        saved.Image.Should().BeNull(); saved.Name.Should().Be(exercise.Name); saved.Description.Should().Be(exercise.Description); saved.BodyPart.Should().Be(exercise.BodyPart); saved.EloFormula.Should().Be(exercise.EloFormula); saved.UserId.Should().BeNull();
        _storage.Published.Should().ContainSingle("replaced objects are retained");
    }

    [Test]
    public async Task FirstAsset_RequiresExplicitNullAndRejectsForgedRollback()
    {
        var manager = await SeedAdminAsync(); SetAuthorizationHeader(manager.Id);
        var exercise = await GlobalAsync(); var upload = await UploadAsync(exercise);
        (await MediaClient().PatchAsJsonAsync($"/api/exercise/{exercise.Id}/media", new { operationId = Id<ExerciseMediaOperationReference>.New().ToString(), uploadReference = upload.UploadReference })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MediaClient().PatchAsJsonAsync($"/api/exercise/{exercise.Id}/media", new { operationId = Id<ExerciseMediaOperationReference>.New().ToString(), expectedImage = (string?)null, restoreImage = "https://attacker.test/image.gif" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MediaClient().PatchAsJsonAsync($"/api/exercise/{exercise.Id}/media", new { operationId = Id<ExerciseMediaOperationReference>.New().ToString(), expectedImage = (string?)null, uploadReference = upload.UploadReference })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestCase("/api/exercise/updateExercise")]
    [TestCase("/api/exercise/updateExerciseWithFormula")]
    public async Task LegacyMetadataEdits_PreserveOmittedNullEmptyAndStaleImages(string route)
    {
        var manager = await SeedAdminAsync(); SetAuthorizationHeader(manager.Id);
        var exercise = await GlobalAsync("https://exercise.test/current.gif");
        foreach (var image in new[] { "OMIT", null, "", "https://legacy.example/stale.gif" })
        {
            var request = new Dictionary<string, object?> { ["_id"] = exercise.Id.ToString(), ["name"] = "Updated", ["bodyPart"] = "Chest", ["description"] = null };
            if (image != "OMIT") request["image"] = image;
            var response = await MediaClient().PostAsJsonAsync(route, request);
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            using var scope = Factory.Services.CreateScope();
            var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Exercises.SingleAsync(x => x.Id == exercise.Id);
            saved.Image.Should().Be(exercise.Image); saved.Description.Should().BeNull("description clearing retains its original semantics");
        }
    }

    [Test]
    public async Task InitiateAndVerify_RejectNonManagerForeignReferenceAndPersonalTarget()
    {
        var ordinary = await SeedUserAsync();
        var request = new { contentType = "image/gif", sizeBytes = Gif.Length, sha256 = Convert.ToHexStringLower(SHA256.HashData(Gif)) };
        (await MediaClient().PostAsJsonAsync("/api/exercise/media/uploads", request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        SetAuthorizationHeader(ordinary.Id);
        (await MediaClient().PostAsJsonAsync("/api/exercise/media/uploads", request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var manager = await SeedAdminAsync(); SetAuthorizationHeader(manager.Id);
        var upload = await UploadAsync(verify: false);
        var other = await SeedUserAsync("other-manager", "other-manager@test.invalid", isAdmin: true); SetAuthorizationHeader(other.Id);
        (await MediaClient().PostAsync($"/api/exercise/media/uploads/{upload.UploadReference}/verify", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var personal = new Exercise { Id = Id<Exercise>.New(), UserId = other.Id, Name = "Personal", BodyPart = BodyParts.Chest };
        db.Exercises.Add(personal); await db.SaveChangesAsync();
        (await MediaClient().PostAsJsonAsync("/api/exercise/media/uploads", new { request.contentType, request.sizeBytes, request.sha256, exerciseId = personal.Id.ToString() })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/exercise/media/cleanup", "admin", "current-permission-allow")]
    public async Task Verification_ReportsRejectedMissingAndExpiredWithoutPersistence()
    {
        var manager = await SeedAdminAsync(); SetAuthorizationHeader(manager.Id);
        var malformed = await UploadAsync(bytes: [1, 2, 3], verify: false);
        (await MediaClient().PostAsync($"/api/exercise/media/uploads/{malformed.UploadReference}/verify", null)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var missing = await UploadAsync(verify: false); _storage.Private.Clear();
        (await MediaClient().PostAsync($"/api/exercise/media/uploads/{missing.UploadReference}/verify", null)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var expired = await UploadAsync(verify: false);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.ExerciseMediaUploads.SingleAsync(x => x.Id == UploadId(expired.UploadReference));
            session.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        }
        (await MediaClient().PostAsync($"/api/exercise/media/uploads/{expired.UploadReference}/verify", null)).StatusCode.Should().Be(HttpStatusCode.Gone);
        (await MediaClient().PostAsync("/api/exercise/media/cleanup", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        _storage.Private.Keys.Should().NotContain("staging/" + expired.UploadReference + "/source");
        (await MediaClient().PostAsync($"/api/exercise/media/uploads/{expired.UploadReference}/verify", null)).StatusCode.Should().Be(HttpStatusCode.Gone);
        (await MediaClient().PostAsJsonAsync("/api/exercise/media/create", new { operationId = Id<ExerciseMediaOperationReference>.New().ToString(), name = "Expired cleaned upload", bodyPart = "Chest", uploadReference = expired.UploadReference })).StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Test]
    [AuthorizationEvidence("DELETE", "/api/exercise/media/uploads/{uploadReference}", "admin", "current-permission-allow")]
    public async Task CancelUnboundUpload_FencesVerificationWithoutPublishing()
    {
        var manager = await SeedAdminAsync(); SetAuthorizationHeader(manager.Id);
        var upload = await UploadAsync(verify: false);
        (await MediaClient().DeleteAsync($"/api/exercise/media/uploads/{upload.UploadReference}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await MediaClient().PostAsync($"/api/exercise/media/uploads/{upload.UploadReference}/verify", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _storage.Published.Should().BeEmpty();
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/exercise/media/uploads", "admin", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/uploads/{uploadReference}/verify", "admin", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/create", "admin", "anonymous-denial")]
    [AuthorizationEvidence("PATCH", "/api/exercise/{id}/media", "admin", "anonymous-denial")]
    [AuthorizationEvidence("DELETE", "/api/exercise/media/uploads/{uploadReference}", "admin", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/cleanup", "admin", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/uploads", "admin", "ordinary-user-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/uploads/{uploadReference}/verify", "admin", "ordinary-user-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/create", "admin", "ordinary-user-denial")]
    [AuthorizationEvidence("PATCH", "/api/exercise/{id}/media", "admin", "ordinary-user-denial")]
    [AuthorizationEvidence("DELETE", "/api/exercise/media/uploads/{uploadReference}", "admin", "ordinary-user-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/cleanup", "admin", "ordinary-user-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/uploads", "admin", "stale-token-demotion-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/uploads/{uploadReference}/verify", "admin", "stale-token-demotion-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/create", "admin", "stale-token-demotion-denial")]
    [AuthorizationEvidence("PATCH", "/api/exercise/{id}/media", "admin", "stale-token-demotion-denial")]
    [AuthorizationEvidence("DELETE", "/api/exercise/media/uploads/{uploadReference}", "admin", "stale-token-demotion-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/cleanup", "admin", "stale-token-demotion-denial")]
    public async Task EveryMediaRoute_RejectsAnonymousOrdinaryAndPreIssuedDemotedSessions()
    {
        async Task AssertDenied(HttpStatusCode expected)
        {
            var id = Id<Exercise>.New().ToString();
            foreach (var (method, route) in new[] { ("POST", "/api/exercise/media/uploads"), ("POST", $"/api/exercise/media/uploads/{id}/verify"), ("POST", "/api/exercise/media/create"), ("PATCH", $"/api/exercise/{id}/media"), ("DELETE", $"/api/exercise/media/uploads/{id}"), ("POST", "/api/exercise/media/cleanup") })
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), route) { Content = JsonContent.Create(new { }) };
                using var response = await MediaClient().SendAsync(request);
                response.StatusCode.Should().Be(expected, "{0} {1} must be denied before executing any media action", method, route);
            }
        }
        await AssertDenied(HttpStatusCode.Unauthorized);
        var ordinary = await SeedUserAsync("media-ordinary", "media-ordinary@test.invalid"); SetAuthorizationHeader(ordinary.Id);
        await AssertDenied(HttpStatusCode.Forbidden);
        var demoted = await SeedUserAsync("media-demoted", "media-demoted@test.invalid", isAdmin: true); SetAuthorizationHeader(demoted.Id);
        var preIssued = Client.DefaultRequestHeaders.Authorization;
        var manager = await SeedAdminAsync(); SetAuthorizationHeader(manager.Id);
        (await Client.PostAsJsonAsync($"/api/roles/users/{demoted.Id}/roles", new { roles = new[] { "User" } })).StatusCode.Should().Be(HttpStatusCode.OK);
        Client.DefaultRequestHeaders.Authorization = preIssued;
        await AssertDenied(HttpStatusCode.Forbidden);
        _storage.SignRequests.Should().Be(0); _storage.Published.Should().BeEmpty();
    }

    [Test]
    public async Task DeletedGlobalTarget_RefusesInitiationVerificationAndPublication()
    {
        var manager = await SeedAdminAsync(); SetAuthorizationHeader(manager.Id);
        var exercise = await GlobalAsync();
        var upload = await UploadAsync(exercise, verify: false);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Exercises.SingleAsync(x => x.Id == exercise.Id)).IsDeleted = true; await db.SaveChangesAsync();
        }
        var signatures = _storage.SignRequests;
        (await MediaClient().PostAsJsonAsync("/api/exercise/media/uploads", new { contentType = "image/gif", sizeBytes = Gif.Length, sha256 = Convert.ToHexStringLower(SHA256.HashData(Gif)), exerciseId = exercise.Id.ToString() })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MediaClient().PostAsync($"/api/exercise/media/uploads/{upload.UploadReference}/verify", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MediaClient().PatchAsJsonAsync($"/api/exercise/{exercise.Id}/media", new { operationId = Id<ExerciseMediaOperationReference>.New().ToString(), expectedImage = (string?)null, uploadReference = upload.UploadReference })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        _storage.SignRequests.Should().Be(signatures); _storage.Published.Should().BeEmpty();
    }

    private static Id<ExerciseMediaOperationReference> OperationId(string value) => Id<ExerciseMediaOperationReference>.TryParse(value, out var id) ? id : throw new InvalidOperationException("Invalid fixture operation ID");
    private static Id<ExerciseMediaUpload> UploadId(string value) => Id<ExerciseMediaUpload>.TryParse(value, out var id) ? id : throw new InvalidOperationException("Invalid fixture upload ID");
    private sealed class MemoryExerciseMediaStorage : IExerciseMediaStorage
    {
        public ConcurrentDictionary<string, byte[]> Private { get; } = new();
        public ConcurrentDictionary<string, byte[]> Published { get; } = new();
        public bool FailAfterPublish { get; set; }
        public int SignRequests { get; private set; }
        public bool IsConfigured => true;
        public Task<string> SignUploadAsync(string key, string contentType, DateTimeOffset expiresAt, CancellationToken token) { SignRequests++; return Task.FromResult("https://private.test/" + key); }
        public Task<byte[]> ReadPrivateAsync(string key, CancellationToken token) => Task.FromResult(Private.TryGetValue(key, out var bytes) ? bytes.ToArray() : throw new IOException("Missing technical fixture"));
        public Task WriteVerifiedAsync(string key, byte[] bytes, string contentType, CancellationToken token) { Private[key] = bytes.ToArray(); return Task.CompletedTask; }
        public Task<string> PublishAsync(string key, byte[] bytes, string contentType, CancellationToken token) { Published[key] = bytes.ToArray(); if (FailAfterPublish) { FailAfterPublish = false; throw new IOException("Simulated interrupted promotion"); } return Task.FromResult("https://exercise.test/" + key); }
        public Task DeletePrivateAsync(string key, CancellationToken token) { Private.TryRemove(key, out _); return Task.CompletedTask; }
        public void Reset() { Private.Clear(); Published.Clear(); FailAfterPublish = false; SignRequests = 0; }
    }
}
