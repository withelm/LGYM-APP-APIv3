using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using ImageMagick;
using LgymApi.Api.Features.Exercise.Contracts;
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var image = (await response.Content.ReadFromJsonAsync<ExerciseMediaUploadResponseDto>())!.Image;
        _storage.Published.Should().ContainSingle();
        var item = _storage.Published.Single(); item.Mime.Should().Be("image/gif");
        image.Should().Be("https://media.test/" + item.Key);
        using var frames = new MagickImageCollection(item.Bytes); frames.Count.Should().Be(2);
        using var scope = Factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Exercises.CountAsync()).Should().Be(0);
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/exercise/media/upload", "admin", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/upload", "admin", "ordinary-user-denial")]
    [AuthorizationEvidence("POST", "/api/exercise/media/upload", "admin", "stale-token-demotion-denial")]
    public async Task Upload_RequiresCurrentPermissionEvenForPreIssuedToken()
    {
        using (var anonymous = await UploadAsync(Gif)) anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var ordinary = await SeedUserAsync("media-ordinary", "media-ordinary@test.invalid"); SetAuthorizationHeader(ordinary.Id);
        using (var denied = await UploadAsync(Gif)) denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var demoted = await SeedUserAsync("media-demoted", "media-demoted@test.invalid", isAdmin: true); SetAuthorizationHeader(demoted.Id); var token = Client.DefaultRequestHeaders.Authorization;
        await ManagerAsync();
        (await Client.PostAsJsonAsync($"/api/roles/users/{demoted.Id}/roles", new { roles = new[] { "User" } })).StatusCode.Should().Be(HttpStatusCode.OK);
        Client.DefaultRequestHeaders.Authorization = token;
        using var revoked = await UploadAsync(Gif); revoked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _storage.Published.Should().BeEmpty();
    }

    [Test]
    public async Task Upload_RejectsEmptyCorruptOversizedAndUnexpectedParts()
    {
        await ManagerAsync();
        foreach (var bytes in new[] { Array.Empty<byte>(), "not an image"u8.ToArray(), "GIF89a"u8.ToArray() })
        { using var response = await UploadAsync(bytes); response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity); }
        using var missing = await UploadAsync(null); missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var wrongPart = await UploadAsync(Gif, field: "other"); wrongPart.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var oversized = await UploadAsync(new byte[ExerciseMediaLimits.MaxBytes + 1]); oversized.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(new string('x', ExerciseMediaLimits.MaxRequestBytes)), "extra");
        using var tooLarge = await MediaClient().PostAsync("/api/exercise/media/upload", multipart);
        tooLarge.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        _storage.Published.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task StorageUnavailable_ReturnsOrdinaryErrorWithoutSecretsOrExerciseWrite(bool configured)
    {
        await ManagerAsync(); _storage.IsConfigured = configured; _storage.Fail = true;
        using var response = await UploadAsync(Gif); response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("synthetic-secret");
        _storage.Published.Should().BeEmpty();
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
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("msg").And.NotContain("_id");
            using var scope = Factory.Services.CreateScope();
            (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Exercises.SingleAsync(x => x.Name == name)).Image.Should().Be(image);
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
        using (var response = await Client.PostAsJsonAsync(route, new { _id = exercise.Id.ToString(), name = "Metadata", bodyPart = "Chest", image = staleImage })) response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ImageAsync(exercise.Id)).Should().Be(exercise.Image);
        using (var response = await Client.PostAsJsonAsync(route, new { _id = exercise.Id.ToString(), name = "Metadata", bodyPart = "Chest", image = "https://media.test/new.gif", updateImage = true })) response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ImageAsync(exercise.Id)).Should().Be("https://media.test/new.gif");
        using (var response = await Client.PostAsJsonAsync(route, new { _id = exercise.Id.ToString(), name = "Metadata", bodyPart = "Chest", image = (string?)null, updateImage = true })) response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ImageAsync(exercise.Id)).Should().BeNull();
        _storage.Published.Should().BeEmpty();
    }

    [Test]
    public async Task GlobalMetadata_WithMissingImagePreservesReference()
    {
        await ManagerAsync(); var exercise = await GlobalAsync("https://legacy.test/current.gif");
        using var response = await Client.PostAsJsonAsync("/api/exercise/updateExercise", new { _id = exercise.Id.ToString(), name = "Metadata", bodyPart = "Chest", updateImage = false });
        response.StatusCode.Should().Be(HttpStatusCode.OK); (await ImageAsync(exercise.Id)).Should().Be(exercise.Image);
    }

    [Test]
    public async Task PrivateExercise_KeepsLegacyImageUpdateAndRejectsForeignManager()
    {
        var owner = await SeedUserAsync(); SetAuthorizationHeader(owner.Id);
        var exercise = await GlobalAsync("old", owner.Id);
        using (var own = await Client.PostAsJsonAsync("/api/exercise/updateExercise", new { _id = exercise.Id.ToString(), name = "Personal", bodyPart = "Chest", image = "https://private.test/new.gif" })) own.StatusCode.Should().Be(HttpStatusCode.OK);
        await ManagerAsync();
        using var foreign = await Client.PostAsJsonAsync("/api/exercise/updateExercise", new { _id = exercise.Id.ToString(), name = "Foreign", bodyPart = "Chest", image = (string?)null, updateImage = true });
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound); (await ImageAsync(exercise.Id)).Should().Be("https://private.test/new.gif");
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
