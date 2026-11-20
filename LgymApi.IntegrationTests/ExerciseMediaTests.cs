using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ImageMagick;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Features.Exercise.Filters;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.IntegrationTests.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
    private async Task ManagerAsync() { var manager = await SeedAdminAsync(); SetAuthorizationHeader(manager.Id); }
    private async Task<HttpResponseMessage> UploadAsync(byte[]? bytes, string mime = "application/octet-stream", string filename = "untrusted.exe", string field = "file")
    {
        using var form = new MultipartFormDataContent();
        if (bytes is not null)
        {
            var file = new ByteArrayContent(bytes); file.Headers.ContentType = new MediaTypeHeaderValue(mime); form.Add(file, field, filename);
        }
        return await MediaClient().PostAsync("/api/exercise/media/upload", form);
    }
    private async Task<Exercise> GlobalAsync(string? image = null, Id<User>? owner = null)
    {
        var exercise = new Exercise { Id = Id<Exercise>.New(), Name = "Reviewed fixture", BodyPart = BodyParts.Chest, Description = "Description", EloFormula = ExerciseEloFormula.Standard, Image = image, UserId = owner };
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.Exercises.Add(exercise); await db.SaveChangesAsync(); return exercise;
    }
    private async Task<string?> ImageAsync(Id<Exercise> id)
    {
        using var scope = Factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Exercises.AsNoTracking().SingleAsync(x => x.Id == id)).Image;
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/exercise/media/upload", "admin", "current-permission-allow")]
    public async Task Upload_VerifiesActualBytesPreservesAnimationAndDoesNotCreateExercise()
    {
        await ManagerAsync();
        using var response = await UploadAsync(Gif);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var image = (await response.Content.ReadFromJsonAsync<ExerciseMediaUploadResponseDto>())!.Image;
        Assert.That(_storage.Published, Has.Exactly(1).Items);
        var item = _storage.Published.Single(); Assert.That(item.Mime, Is.EqualTo("image/gif"));
        Assert.That(image, Is.EqualTo("https://media.test/" + item.Key));
        using var frames = new MagickImageCollection(item.Bytes); Assert.That(frames.Count, Is.EqualTo(2));
        using var scope = Factory.Services.CreateScope();
        Assert.That((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Exercises.CountAsync()), Is.EqualTo(0));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/exercise/media/upload", "admin", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/upload", "admin", "ordinary-user-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/upload", "admin", "stale-token-demotion-denial")]
    public async Task Upload_RequiresCurrentPermissionEvenForPreIssuedToken()
    {
        using (var anonymous = await UploadAsync(Gif)) Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var ordinary = await SeedUserAsync("media-ordinary", "media-ordinary@test.invalid"); SetAuthorizationHeader(ordinary.Id);
        using (var denied = await UploadAsync(Gif)) Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var demoted = await SeedUserAsync("media-demoted", "media-demoted@test.invalid", isAdmin: true); SetAuthorizationHeader(demoted.Id); var token = Client.DefaultRequestHeaders.Authorization;
        await ManagerAsync();
        Assert.That((await Client.PostAsJsonAsync($"/api/roles/users/{demoted.Id}/roles", new { roles = new[] { "User" } })).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Client.DefaultRequestHeaders.Authorization = token;
        using var revoked = await UploadAsync(Gif); Assert.That(revoked.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(_storage.Published, Is.Empty);
    }

    [Test]
    public async Task Upload_RejectsEmptyCorruptOversizedAndUnexpectedParts()
    {
        await ManagerAsync();
        foreach (var bytes in new[] { Array.Empty<byte>(), "not an image"u8.ToArray(), "GIF89a"u8.ToArray() })
        { using var response = await UploadAsync(bytes); Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity)); }
        using var missing = await UploadAsync(null); Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var wrongPart = await UploadAsync(Gif, field: "other"); Assert.That(wrongPart.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        using var oversized = await UploadAsync(new byte[ExerciseMediaLimits.MaxBytes + 1]); Assert.That(oversized.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(new string('x', ExerciseMediaLimits.MaxRequestBytes)), "extra");
        using var tooLarge = await MediaClient().PostAsync("/api/exercise/media/upload", multipart);
        Assert.That(tooLarge.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
        Assert.That(_storage.Published, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task StorageUnavailable_ReturnsOrdinaryErrorWithoutSecretsOrExerciseWrite(bool configured)
    {
        await ManagerAsync(); _storage.IsConfigured = configured; _storage.Fail = true;
        using var response = await UploadAsync(Gif); Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That((await response.Content.ReadAsStringAsync()), Does.Not.Contain("synthetic-secret"));
        Assert.That(_storage.Published, Is.Empty);
    }

    [Test]
    public async Task SaturatedIntake_Returns503_AndReleasedCapacityAllowsVerifiedUpload()
    {
        await ManagerAsync();
        _ = MediaClient();
        var admission = _mediaFactory!.Services.GetRequiredService<ExerciseMediaRequestAdmission>();
        using var first = admission.TryAcquire();
        using var second = admission.TryAcquire();
        Assert.That(first.IsAcquired, Is.True);
        Assert.That(second.IsAcquired, Is.True);
        using (var rejected = await UploadAsync(Gif))
        {
            Assert.That(rejected.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That((await rejected.Content.ReadAsStringAsync()), Does.Contain("msg"));
            Assert.That(_storage.Published, Is.Empty);
        }
        first.Dispose();
        using var accepted = await UploadAsync(Gif);
        Assert.That(accepted.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_storage.Published, Has.Exactly(1).Items);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task OrdinaryCreate_AcceptsUploadedOrNoImageWithoutChangingResponseShape(bool formula)
    {
        await ManagerAsync();
        using var uploaded = await UploadAsync(Gif);
        var url = (await uploaded.Content.ReadFromJsonAsync<ExerciseMediaUploadResponseDto>())!.Image;
        foreach (var image in new[] { url, null })
        {
            var name = "Created " + Id<ExerciseMediaTests>.New();
            using var response = await Client.PostAsJsonAsync("/api/exercise/" + (formula ? "addExerciseWithFormula" : "addExercise"), new { name, bodyPart = "Chest", description = "Fixture", image });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var assertedChain8 = (await response.Content.ReadAsStringAsync());
            Assert.That(assertedChain8, Does.Contain("msg"));
            Assert.That(assertedChain8, Does.Not.Contain("_id"));
            using var scope = Factory.Services.CreateScope();
            Assert.That((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Exercises.SingleAsync(x => x.Name == name)).Image, Is.EqualTo(image));
        }
    }

    [TestCase(false, null)]
    [TestCase(false, "")]
    [TestCase(false, "https://old.test/stale.gif")]
    [TestCase(true, null)]
    [TestCase(true, "")]
    [TestCase(true, "https://old.test/stale.gif")]
    public async Task GlobalMetadata_WithoutIntentionPreservesImageAndExplicitReplaceRemoveWorks(bool formula, string? staleImage)
    {
        await ManagerAsync(); var exercise = await GlobalAsync("https://media.test/current.gif");
        var route = "/api/exercise/" + (formula ? "updateExerciseWithFormula" : "updateExercise");
        using (var response = await Client.PostAsJsonAsync(route, new { _id = exercise.Id.ToString(), name = "Metadata", bodyPart = "Chest", image = staleImage })) Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await ImageAsync(exercise.Id)), Is.EqualTo(exercise.Image));
        using (var response = await Client.PostAsJsonAsync(route, new { _id = exercise.Id.ToString(), name = "Metadata", bodyPart = "Chest", image = "https://media.test/new.gif", updateImage = true })) Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await ImageAsync(exercise.Id)), Is.EqualTo("https://media.test/new.gif"));
        using (var response = await Client.PostAsJsonAsync(route, new { _id = exercise.Id.ToString(), name = "Metadata", bodyPart = "Chest", image = (string?)null, updateImage = true })) Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await ImageAsync(exercise.Id)), Is.Null);
        Assert.That(_storage.Published, Is.Empty);
    }

    [Test]
    public async Task GlobalMetadata_WithMissingImagePreservesReference()
    {
        await ManagerAsync(); var exercise = await GlobalAsync("https://legacy.test/current.gif");
        using var response = await Client.PostAsJsonAsync("/api/exercise/updateExercise", new { _id = exercise.Id.ToString(), name = "Metadata", bodyPart = "Chest", updateImage = false });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK)); Assert.That((await ImageAsync(exercise.Id)), Is.EqualTo(exercise.Image));
    }

    [Test]
    public async Task PrivateExercise_KeepsLegacyImageUpdateAndRejectsForeignManager()
    {
        var owner = await SeedUserAsync(); SetAuthorizationHeader(owner.Id);
        var exercise = await GlobalAsync("old", owner.Id);
        using (var own = await Client.PostAsJsonAsync("/api/exercise/updateExercise", new { _id = exercise.Id.ToString(), name = "Personal", bodyPart = "Chest", image = "https://private.test/new.gif" })) Assert.That(own.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await ManagerAsync();
        using var foreign = await Client.PostAsJsonAsync("/api/exercise/updateExercise", new { _id = exercise.Id.ToString(), name = "Foreign", bodyPart = "Chest", image = (string?)null, updateImage = true });
        Assert.That(foreign.StatusCode, Is.EqualTo(HttpStatusCode.NotFound)); Assert.That((await ImageAsync(exercise.Id)), Is.EqualTo("https://private.test/new.gif"));
    }

    private sealed class MemoryExerciseMediaStorage : IExerciseMediaStorage
    {
        public bool IsConfigured { get; set; } = true;
        public bool Fail { get; set; }
        public List<(string Key, byte[] Bytes, string Mime)> Published { get; } = [];
        public Task<string> PublishAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Fail) throw new IOException("synthetic-secret");
            Published.Add((key, bytes, contentType)); return Task.FromResult("https://media.test/" + key);
        }
        public void Reset() { Published.Clear(); IsConfigured = true; Fail = false; }
    }
}
