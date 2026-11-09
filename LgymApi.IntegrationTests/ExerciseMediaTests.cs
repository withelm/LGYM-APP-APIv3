using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Identity.Contracts.Accounts;
using Microsoft.AspNetCore.Http;
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
    private bool _downstreamFault;
    private bool _afterAcceptance;
    private int _faultRemaining;
    private static readonly byte[] Gif = Convert.FromHexString("47494638396101000100800000000000ffffff21ff0b4e45545343415045322e30030100000021f904000a0000002c000000000100010000020244010021f90400140000002c00000000010001000002024c01003b");

    [TearDown]
    public void DisposeMediaHost() { _mediaClient?.Dispose(); _mediaFactory?.Dispose(); _mediaClient = null; _mediaFactory = null; _storage.Reset(); }
    private HttpClient MediaClient()
    {
        if (_mediaClient is null)
        {
            _mediaFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.AddSingleton<IExerciseMediaStorage>(_storage);
                if (_downstreamFault)
                {
                    var descriptor = services.Last(x => x.ServiceType == typeof(IExerciseMediaService));
                    services.Remove(descriptor);
                    services.AddScoped<IExerciseMediaService>(provider => new InterruptingMediaService(
                        (IExerciseMediaService)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!),
                        () => Interlocked.Exchange(ref _faultRemaining, 0) == 1, _afterAcceptance));
                }
            }));
            _mediaClient = _mediaFactory.CreateClient();
        }
        _mediaClient.DefaultRequestHeaders.Authorization = Client.DefaultRequestHeaders.Authorization;
        return _mediaClient;
    }
    private async Task ManagerAsync() { var manager = await SeedAdminAsync(); SetAuthorizationHeader(manager.Id); }
    private async Task<Exercise> GlobalAsync(string? image = null)
    {
        var exercise = new Exercise { Id = Id<Exercise>.New(), Name = "Reviewed fixture", BodyPart = BodyParts.Chest, Description = "Retain description", EloFormula = ExerciseEloFormula.VolumeWeighted, Image = image };
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.Exercises.Add(exercise); await db.SaveChangesAsync(); return exercise;
    }
    private static Dictionary<string, string> CreateFields(string name = "GIF create fixture", bool media = true)
    {
        var fields = new Dictionary<string, string> { ["operationId"] = Id<ExerciseMediaOperationReference>.New().ToString(), ["name"] = name, ["bodyPart"] = "Chest" };
        if (media) AddManifest(fields, Gif);
        return fields;
    }
    private static Dictionary<string, string> ChangeFields(string? expectedImage, bool media = true)
    {
        var fields = new Dictionary<string, string> { ["operationId"] = Id<ExerciseMediaOperationReference>.New().ToString(), ["expectedImage"] = expectedImage ?? "" };
        if (media) AddManifest(fields, Gif);
        return fields;
    }
    private static void AddManifest(Dictionary<string, string> fields, byte[] bytes)
    { fields["contentType"] = "image/gif"; fields["sizeBytes"] = bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture); fields["sourceSha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes)); }
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, Dictionary<string, string> fields, byte[]? bytes = null)
    {
        using var request = new HttpRequestMessage(method, route);
        var form = new MultipartFormDataContent();
        foreach (var (key, value) in fields) form.Add(new StringContent(value), key);
        if (bytes is not null)
        {
            var file = new ByteArrayContent(bytes); file.Headers.ContentType = new MediaTypeHeaderValue(fields.GetValueOrDefault("contentType", "image/gif")); form.Add(file, "file", "original.gif");
        }
        request.Content = form;
        request.Headers.Add("X-Exercise-Media-Operation-Id", fields["operationId"]);
        return await MediaClient().SendAsync(request);
    }
    private Task<HttpResponseMessage> CreateAsync(Dictionary<string, string> fields, byte[]? bytes = null) => SendAsync(HttpMethod.Post, "/api/exercise/media/create", fields, bytes);
    private Task<HttpResponseMessage> ChangeAsync(Exercise exercise, Dictionary<string, string> fields, byte[]? bytes = null) => SendAsync(HttpMethod.Patch, $"/api/exercise/{exercise.Id}/media", fields, bytes);

    [Test]
    [AuthorizationEvidence("POST", "/api/exercise/media/create", "admin", "current-permission-allow")]
    public async Task GlobalCreate_WithGif_IsIdempotentAndPublishesOnlyVerifiedBytes()
    {
        await ManagerAsync(); var fields = CreateFields();
        using var first = await CreateAsync(fields, Gif);
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        var saved = (await first.Content.ReadFromJsonAsync<ExerciseMediaMutationDto>())!;
        _storage.Published.Should().ContainSingle();
        var stored = _storage.Published.Single();
        saved.Image.Should().Be(_storage.GetPublicUrl(stored.Key));
        stored.Key.Should().StartWith($"exercises/{saved.Id}/").And.Contain(Convert.ToHexStringLower(SHA256.HashData(stored.Value))).And.EndWith("/animation.gif");
        using var replay = await CreateAsync(fields);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replay.Content.ReadFromJsonAsync<ExerciseMediaMutationDto>())!.Id.Should().Be(saved.Id);
        using var scope = Factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Exercises.CountAsync(x => x.Name == fields["name"])).Should().Be(1);
    }

    [Test]
    public async Task GlobalCreate_WithoutMedia_DuplicateSubmissionCreatesOneRecord()
    {
        await ManagerAsync(); var fields = CreateFields("No media fixture", false);
        using var first = await CreateAsync(fields); using var second = await CreateAsync(fields);
        first.StatusCode.Should().Be(HttpStatusCode.OK); second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<ExerciseMediaMutationDto>())!.Id.Should().Be((await second.Content.ReadFromJsonAsync<ExerciseMediaMutationDto>())!.Id);
        _storage.Published.Should().BeEmpty();
    }

    [Test]
    public async Task DisabledStorage_AllowsDatabaseOnlyCreateAndReadWhileFileIntentRemainsUnresolved()
    {
        await ManagerAsync(); _storage.IsConfigured = false;
        using var created = await CreateAsync(CreateFields("Storage disabled fixture", false));
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = (await created.Content.ReadFromJsonAsync<ExerciseMediaMutationDto>())!;
        using var read = await MediaClient().GetAsync($"/api/exercise/{saved.Id}/getExercise");
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        var fileFields = CreateFields("Unconfigured file intent");
        using var unavailable = await CreateAsync(fileFields, Gif);
        unavailable.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var error = (await unavailable.Content.ReadFromJsonAsync<ExerciseMediaErrorDto>())!;
        error.OperationId.Should().Be(fileFields["operationId"]); error.Disposition.Should().Be("unresolved");
        _storage.Published.Should().BeEmpty();
    }

    [Test]
    public async Task PublicationFailure_RetryReusesDurablyReservedUuidAndImmutableObject()
    {
        await ManagerAsync(); var fields = CreateFields("Interrupted create"); _storage.FailAfterPublish = true;
        using var first = await CreateAsync(fields, Gif); first.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        string reserved;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var operation = OperationId(fields["operationId"]);
            var intent = await db.ExerciseMediaUploads.SingleAsync(x => x.OperationId == operation);
            intent.Status.Should().Be("publishing"); reserved = intent.TargetExerciseId!.Value.ToString();
            (await db.Exercises.AnyAsync(x => x.Name == fields["name"])).Should().BeFalse();
        }
        using var replay = await CreateAsync(fields);
        replay.StatusCode.Should().Be(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
        (await replay.Content.ReadFromJsonAsync<ExerciseMediaMutationDto>())!.Id.Should().Be(reserved); _storage.Published.Should().ContainSingle();
    }

    [Test]
    public async Task MissingSourceAfterRestart_RequiresSameOriginalAndNeverDowngradesToNoMedia()
    {
        await ManagerAsync(); var fields = CreateFields(); _storage.FailBeforePublish = true;
        using var first = await CreateAsync(fields, Gif); first.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        using var missing = await CreateAsync(fields); missing.StatusCode.Should().Be((HttpStatusCode)428);
        (await missing.Content.ReadFromJsonAsync<ExerciseMediaErrorDto>())!.Disposition.Should().Be("sourceRequired");
        using var wrong = await CreateAsync(fields, [1, 2, 3]); wrong.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        using var replay = await CreateAsync(fields, Gif); replay.StatusCode.Should().Be(HttpStatusCode.OK);
        _storage.Published.Should().ContainSingle();
    }

    [Test]
    public async Task DefinitiveInvalidName_IsFencedAndCorrectedCreateUsesNewOperation()
    {
        await ManagerAsync(); var fields = CreateFields(new string('x', 201), false);
        using var invalid = await CreateAsync(fields); invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = (await invalid.Content.ReadFromJsonAsync<ExerciseMediaErrorDto>())!;
        error.OperationId.Should().Be(fields["operationId"]); error.Disposition.Should().Be("rejected");
        fields["name"] = new string('x', 200);
        using var fenced = await CreateAsync(fields); fenced.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fields["operationId"] = Id<ExerciseMediaOperationReference>.New().ToString();
        using var corrected = await CreateAsync(fields); corrected.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task AcceptedReplay_ValidationOrOversizeCannotReportDefinitiveRejection()
    {
        await ManagerAsync(); var fields = CreateFields("Accepted replay", false);
        using var accepted = await CreateAsync(fields); accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        var changed = new Dictionary<string, string>(fields) { ["name"] = new string('x', 201) };
        using var invalidReplay = await CreateAsync(changed); invalidReplay.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await invalidReplay.Content.ReadFromJsonAsync<ExerciseMediaErrorDto>())!.Disposition.Should().Be("unresolved");
        using var oversized = await CreateAsync(fields, new byte[ExerciseMediaLimits.MaxBytes + 1]); oversized.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        using var exact = await CreateAsync(fields); exact.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task UnicodeDescription_UsesDocumentedUtf16LimitWithoutByteCliff()
    {
        await ManagerAsync(); var fields = CreateFields("Unicode description", false); fields["description"] = new string((char)0x017B, 16000);
        using var accepted = await CreateAsync(fields); accepted.StatusCode.Should().Be(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());
        fields["operationId"] = Id<ExerciseMediaOperationReference>.New().ToString(); fields["description"] += char.ConvertFromUtf32(0x017B);
        using var rejected = await CreateAsync(fields); rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await rejected.Content.ReadFromJsonAsync<ExerciseMediaErrorDto>())!.Disposition.Should().Be("rejected");
    }

    [Test]
    public async Task InvalidBytesOversizeAndUnknownFields_AreRejectedWithoutPublishing()
    {
        await ManagerAsync(); var fields = CreateFields(); AddManifest(fields, [1, 2, 3]);
        using var invalid = await CreateAsync(fields, [1, 2, 3]); invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await invalid.Content.ReadFromJsonAsync<ExerciseMediaErrorDto>())!.Disposition.Should().Be("rejected");
        fields = CreateFields(); using var oversized = await CreateAsync(fields, new byte[ExerciseMediaLimits.MaxBytes + 1]); oversized.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await oversized.Content.ReadFromJsonAsync<ExerciseMediaErrorDto>())!.Disposition.Should().Be("rejected");
        fields = CreateFields("Unknown field", false); fields["unexpectedField"] = "unsupported-value";
        using var unknown = await CreateAsync(fields); unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var removedRoute = await MediaClient().PostAsJsonAsync("/api/exercise/media/uploads", new {}); removedRoute.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _storage.Published.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DownstreamSizeException_IsRejectedOnlyWhenNoOperationWasAccepted(bool afterAcceptance)
    {
        await ManagerAsync(); _downstreamFault = true; _afterAcceptance = afterAcceptance; _faultRemaining = 1;
        var fields = CreateFields("Downstream guarded fixture", false);
        using var first = await CreateAsync(fields);
        first.StatusCode.Should().Be(afterAcceptance ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.RequestEntityTooLarge, await first.Content.ReadAsStringAsync());
        var error = (await first.Content.ReadFromJsonAsync<ExerciseMediaErrorDto>())!;
        error.OperationId.Should().Be(fields["operationId"]); error.Disposition.Should().Be(afterAcceptance ? "unresolved" : "rejected");
        using var scope = Factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Exercises.CountAsync(x => x.Name == fields["name"])).Should().Be(afterAcceptance ? 1 : 0);
        if (afterAcceptance) { using var replay = await CreateAsync(fields); replay.StatusCode.Should().Be(HttpStatusCode.OK); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task MalformedOrChunkedOversizeBody_IsFencedBeforeAnyPublication(bool oversized)
    {
        await ManagerAsync(); var operationId = Id<ExerciseMediaOperationReference>.New().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/exercise/media/create");
        request.Headers.Add("X-Exercise-Media-Operation-Id", operationId);
        var text = oversized ? "--boundary\r\nContent-Disposition: form-data; name=\"name\"\r\n\r\n" + new string('x', ExerciseMediaLimits.MaxRequestBytes + 1) + "\r\n--boundary--\r\n" : "truncated multipart";
        request.Content = new ChunkedMultipartContent(Encoding.UTF8.GetBytes(text));
        using var response = await MediaClient().SendAsync(request);
        response.StatusCode.Should().Be(oversized ? HttpStatusCode.RequestEntityTooLarge : HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var error = (await response.Content.ReadFromJsonAsync<ExerciseMediaErrorDto>())!;
        error.OperationId.Should().Be(operationId); error.Disposition.Should().Be("rejected"); _storage.Published.Should().BeEmpty();
    }

    [Test]
    [AuthorizationEvidence("PATCH", "/api/exercise/{id}/media", "admin", "current-permission-allow")]
    public async Task MediaOnly_ReplaceRemoveAndConditionalRollbackPreserveMetadata()
    {
        await ManagerAsync(); var exercise = await GlobalAsync("https://legacy.example/retained.png");
        using var replaced = await ChangeAsync(exercise, ChangeFields(exercise.Image), Gif); replaced.StatusCode.Should().Be(HttpStatusCode.OK, await replaced.Content.ReadAsStringAsync());
        var published = (await replaced.Content.ReadFromJsonAsync<ExerciseMediaMutationDto>())!.Image;
        var stale = ChangeFields(exercise.Image, false); stale["remove"] = "true";
        using var conflicted = await ChangeAsync(exercise, stale); conflicted.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var restore = ChangeFields(published, false); restore["restoreImage"] = exercise.Image!;
        using var restored = await ChangeAsync(exercise, restore); restored.StatusCode.Should().Be(HttpStatusCode.OK);
        var remove = ChangeFields(exercise.Image, false); remove["remove"] = "true";
        using var removed = await ChangeAsync(exercise, remove); removed.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope(); var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Exercises.SingleAsync(x => x.Id == exercise.Id);
        saved.Image.Should().BeNull(); saved.Name.Should().Be(exercise.Name); saved.Description.Should().Be(exercise.Description); saved.BodyPart.Should().Be(exercise.BodyPart); saved.EloFormula.Should().Be(exercise.EloFormula); saved.UserId.Should().BeNull();
        _storage.Published.Should().ContainSingle();
    }

    [Test]
    public async Task FirstAsset_RequiresExplicitNullAndRejectsForgedRollback()
    {
        await ManagerAsync(); var exercise = await GlobalAsync(); var fields = ChangeFields(null); fields.Remove("expectedImage");
        using var missing = await ChangeAsync(exercise, fields, Gif); missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var forged = ChangeFields(null, false); forged["restoreImage"] = "https://attacker.test/image.gif";
        using var forbidden = await ChangeAsync(exercise, forged); forbidden.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var accepted = await ChangeAsync(exercise, ChangeFields(null), Gif); accepted.StatusCode.Should().Be(HttpStatusCode.OK);
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
    [AuthorizationEvidence("POST", "/api/exercise/media/cleanup", "admin", "current-permission-allow")]
    public async Task Cleanup_LeavesBoundAndRetainedVersionsAvailable()
    {
        await ManagerAsync(); var exercise = await GlobalAsync();
        using var first = await ChangeAsync(exercise, ChangeFields(null), Gif); first.StatusCode.Should().Be(HttpStatusCode.OK);
        var image = (await first.Content.ReadFromJsonAsync<ExerciseMediaMutationDto>())!.Image;
        var remove = ChangeFields(image, false); remove["remove"] = "true";
        using var removed = await ChangeAsync(exercise, remove); removed.StatusCode.Should().Be(HttpStatusCode.OK);
        using var cleanup = await MediaClient().PostAsync("/api/exercise/media/cleanup", null); cleanup.StatusCode.Should().Be(HttpStatusCode.OK);
        _storage.Published.Should().ContainSingle(); _storage.DeleteCalls.Should().Be(0);
        var restore = ChangeFields(null, false); restore["restoreImage"] = image!;
        using var restored = await ChangeAsync(exercise, restore); restored.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/exercise/media/create", "admin", "anonymous-denial")]
    [AuthorizationEvidence("PATCH", "/api/exercise/{id}/media", "admin", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/cleanup", "admin", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/create", "admin", "ordinary-user-denial")]
    [AuthorizationEvidence("PATCH", "/api/exercise/{id}/media", "admin", "ordinary-user-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/cleanup", "admin", "ordinary-user-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/create", "admin", "stale-token-demotion-denial")]
    [AuthorizationEvidence("PATCH", "/api/exercise/{id}/media", "admin", "stale-token-demotion-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/cleanup", "admin", "stale-token-demotion-denial")]
    public async Task EveryMediaRoute_RejectsAnonymousOrdinaryAndPreIssuedDemotedSessions()
    {
        async Task AssertDenied(HttpStatusCode expected)
        {
            var exercise = new Exercise { Id = Id<Exercise>.New() };
            using var create = await CreateAsync(CreateFields()); create.StatusCode.Should().Be(expected);
            using var change = await ChangeAsync(exercise, ChangeFields(null)); change.StatusCode.Should().Be(expected);
            using var cleanup = await MediaClient().PostAsync("/api/exercise/media/cleanup", null); cleanup.StatusCode.Should().Be(expected);
        }
        await AssertDenied(HttpStatusCode.Unauthorized);
        var ordinary = await SeedUserAsync("media-ordinary", "media-ordinary@test.invalid"); SetAuthorizationHeader(ordinary.Id); await AssertDenied(HttpStatusCode.Forbidden);
        var demoted = await SeedUserAsync("media-demoted", "media-demoted@test.invalid", isAdmin: true); SetAuthorizationHeader(demoted.Id); var preIssued = Client.DefaultRequestHeaders.Authorization;
        await ManagerAsync(); (await Client.PostAsJsonAsync($"/api/roles/users/{demoted.Id}/roles", new { roles = new[] { "User" } })).StatusCode.Should().Be(HttpStatusCode.OK);
        Client.DefaultRequestHeaders.Authorization = preIssued; await AssertDenied(HttpStatusCode.Forbidden);
        _storage.Published.Should().BeEmpty();
    }

    [Test]
    public async Task DeletedUnknownAndPersonalTargets_RefusePublication()
    {
        await ManagerAsync(); var deleted = await GlobalAsync(); var personal = await GlobalAsync(); var owner = await SeedUserAsync("personal-owner", "personal-owner@test.invalid");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); (await db.Exercises.SingleAsync(x => x.Id == deleted.Id)).IsDeleted = true;
            (await db.Exercises.SingleAsync(x => x.Id == personal.Id)).UserId = owner.Id; await db.SaveChangesAsync();
        }
        foreach (var exercise in new[] { deleted, personal, new Exercise { Id = Id<Exercise>.New() } })
        { using var response = await ChangeAsync(exercise, ChangeFields(null), Gif); response.StatusCode.Should().Be(HttpStatusCode.NotFound); }
        _storage.Published.Should().BeEmpty();
    }

    private static Id<ExerciseMediaOperationReference> OperationId(string value) => Id<ExerciseMediaOperationReference>.TryParse(value, out var id) ? id : throw new InvalidOperationException("Invalid fixture operation ID");
    private sealed class InterruptingMediaService(IExerciseMediaService inner, Func<bool> shouldInterrupt, bool afterAcceptance) : IExerciseMediaService
    {
        public async Task<Result<ExerciseMediaMutationResult, AppError>> CreateAsync(AuthenticatedAccountContext? actor, CreateExerciseMediaInput input, byte[]? sourceBytes, CancellationToken cancellationToken)
        {
            if (!shouldInterrupt()) return await inner.CreateAsync(actor, input, sourceBytes, cancellationToken);
            if (afterAcceptance) await inner.CreateAsync(actor, input, sourceBytes, cancellationToken);
            throw new BadHttpRequestException("Synthetic downstream size guard", 413);
        }
        public Task<Result<ExerciseMediaMutationResult, AppError>> ChangeAsync(AuthenticatedAccountContext? actor, Id<Exercise> id, ChangeExerciseMediaInput input, byte[]? sourceBytes, CancellationToken cancellationToken) => inner.ChangeAsync(actor, id, input, sourceBytes, cancellationToken);
        public Task<bool> RejectAsync(AuthenticatedAccountContext? actor, Id<ExerciseMediaOperationReference> operationId, int statusCode, CancellationToken cancellationToken) => inner.RejectAsync(actor, operationId, statusCode, cancellationToken);
        public Task<Result<ExerciseMediaCleanupResult, AppError>> CleanupAsync(AuthenticatedAccountContext? actor, CancellationToken cancellationToken) => inner.CleanupAsync(actor, cancellationToken);
    }

    private sealed class ChunkedMultipartContent : HttpContent
    {
        private readonly byte[] _bytes;
        public ChunkedMultipartContent(byte[] bytes) { _bytes = bytes; Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=boundary"); }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(_bytes).AsTask();
    }

    private sealed class MemoryExerciseMediaStorage : IExerciseMediaStorage
    {
        public ConcurrentDictionary<string, byte[]> Published { get; } = new();
        public bool FailAfterPublish { get; set; }
        public bool FailBeforePublish { get; set; }
        public int DeleteCalls { get; private set; }
        public bool IsConfigured { get; set; } = true;
        public string GetPublicUrl(string key) => "https://exercise.test/" + key;
        public Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken) => Task.FromResult(Published.TryGetValue(key, out var bytes) ? bytes.ToArray() : null);
        public Task<string> PublishAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken)
        {
            if (FailBeforePublish) { FailBeforePublish = false; throw new IOException("Simulated write interruption"); }
            Published[key] = bytes.ToArray();
            if (FailAfterPublish) { FailAfterPublish = false; throw new IOException("Simulated lost write acknowledgement"); }
            return Task.FromResult(GetPublicUrl(key));
        }
        public Task DeleteAsync(string key, CancellationToken cancellationToken) { DeleteCalls++; Published.TryRemove(key, out _); return Task.CompletedTask; }
        public void Reset() { Published.Clear(); FailAfterPublish = false; FailBeforePublish = false; DeleteCalls = 0; IsConfigured = true; }
    }
}
