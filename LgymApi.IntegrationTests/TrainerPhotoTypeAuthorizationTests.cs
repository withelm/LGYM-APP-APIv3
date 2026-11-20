using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LgymApi.Api.Middleware;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using LgymApi.IntegrationTests.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class TrainerPhotoTypeAuthorizationTests : IntegrationTestBase
{
    private const string CollectionRoute = "/api/trainer/reporting/photo-types";

    [Test]
    [AuthorizationEvidence("GET", CollectionRoute, "own", "owner-allow")]
    [AuthorizationEvidence("GET", CollectionRoute, "own", "anonymous-denial")]
    [AuthorizationEvidence("GET", CollectionRoute, "own", "no-client-subject")]
    [AuthorizationEvidence("POST", CollectionRoute, "own", "owner-allow")]
    [AuthorizationEvidence("POST", CollectionRoute, "own", "anonymous-denial")]
    [AuthorizationEvidence("POST", CollectionRoute, "own", "no-client-subject")]
    public async Task PhotoTypeCollectionRoutes_RequireOwnerAndAnonymousRequestsDoNotMutate()
    {
        var owner = await SeedTrainerAsync("photo-type-owner", "photo-type-owner@example.test");
        SetAuthorizationHeader(owner.Id);

        using var ownerGet = await SendAsync(HttpMethod.Get, CollectionRoute);
        Assert.That(ownerGet.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var defaults = await ReadArrayAsync(ownerGet);
        Assert.That((defaults).Any(item => item.GetProperty("systemKey").GetString() == "Front"), Is.True);
        var defaultCount = await CountPhotoTypesAsync();

        using var ownerCreate = await SendJsonAsync(CollectionRoute, new { name = "Competition pose" });
        Assert.That(ownerCreate.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var createdId = await ReadIdAsync(ownerCreate);
        Assert.That((await CountPhotoTypesAsync()), Is.EqualTo(defaultCount + 1));

        ClearAuthorizationHeader();
        using var anonymousGet = await SendAsync(HttpMethod.Get, CollectionRoute);
        Assert.That(anonymousGet.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using var anonymousCreate = await SendJsonAsync(CollectionRoute, new { name = "Anonymous pose" });
        Assert.That(anonymousCreate.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        Assert.That((await CountPhotoTypesAsync()), Is.EqualTo(defaultCount + 1));
        Assert.That((await GetPhotoTypeAsync(createdId)).Name, Is.EqualTo("Competition pose"));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/reporting/photo-types/{photoTypeId}/update", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/reporting/photo-types/{photoTypeId}/update", "own", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/reporting/photo-types/{photoTypeId}/update", "own", "anonymous-denial")]
    public async Task PhotoTypeUpdateRoute_IsOwnerScopedAndDenialsDoNotMutate()
    {
        var owner = await SeedTrainerAsync("photo-type-update-owner", "photo-type-update-owner@example.test");
        var foreignTrainer = await SeedTrainerAsync("photo-type-update-foreign", "photo-type-update-foreign@example.test");
        SetAuthorizationHeader(owner.Id);
        var photoTypeId = await CreatePhotoTypeAsync("Original custom name");
        var route = $"{CollectionRoute}/{photoTypeId}/update";

        using var ownerUpdate = await SendJsonAsync(route, new { name = "Owner updated name", isArchived = true });
        Assert.That(ownerUpdate.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerSnapshot = await GetPhotoTypeAsync(photoTypeId);
        Assert.That(ownerSnapshot, Is.EqualTo(new PhotoTypeSnapshot("Owner updated name", PhotoTypeNameMode.TrainerProvided, true)));

        SetAuthorizationHeader(foreignTrainer.Id);
        using var foreignUpdate = await SendJsonAsync(route, new { name = "Foreign mutation", isArchived = false });
        Assert.That(foreignUpdate.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await GetPhotoTypeAsync(photoTypeId)), Is.EqualTo(ownerSnapshot));

        ClearAuthorizationHeader();
        using var anonymousUpdate = await SendJsonAsync(route, new { name = "Anonymous mutation", isArchived = false });
        Assert.That(anonymousUpdate.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await GetPhotoTypeAsync(photoTypeId)), Is.EqualTo(ownerSnapshot));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/reporting/photo-types/{photoTypeId}/reset-name", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/reporting/photo-types/{photoTypeId}/reset-name", "own", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/reporting/photo-types/{photoTypeId}/reset-name", "own", "anonymous-denial")]
    public async Task PhotoTypeResetNameRoute_IsOwnerScopedAndDenialsDoNotMutate()
    {
        var owner = await SeedTrainerAsync("photo-type-reset-owner", "photo-type-reset-owner@example.test");
        var foreignTrainer = await SeedTrainerAsync("photo-type-reset-foreign", "photo-type-reset-foreign@example.test");
        SetAuthorizationHeader(owner.Id);
        var frontId = await GetBuiltInPhotoTypeIdAsync("Front");
        using (var rename = await SendJsonAsync($"{CollectionRoute}/{frontId}/update", new { name = "Owner front label" }))
        {
            Assert.That(rename.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        var renamedSnapshot = await GetPhotoTypeAsync(frontId);
        var resetRoute = $"{CollectionRoute}/{frontId}/reset-name";

        SetAuthorizationHeader(foreignTrainer.Id);
        using var foreignReset = await SendAsync(HttpMethod.Post, resetRoute);
        Assert.That(foreignReset.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await GetPhotoTypeAsync(frontId)), Is.EqualTo(renamedSnapshot));

        ClearAuthorizationHeader();
        using var anonymousReset = await SendAsync(HttpMethod.Post, resetRoute);
        Assert.That(anonymousReset.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await GetPhotoTypeAsync(frontId)), Is.EqualTo(renamedSnapshot));

        SetAuthorizationHeader(owner.Id);
        using var ownerReset = await SendAsync(HttpMethod.Post, resetRoute);
        Assert.That(ownerReset.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await GetPhotoTypeAsync(frontId)), Is.EqualTo(new PhotoTypeSnapshot("Front", PhotoTypeNameMode.SystemLocalized, false)));
    }

    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name, email);
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        database.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, trainer.Id));
        await database.SaveChangesAsync();
        return trainer;
    }

    private async Task<Id<TrainerPhotoType>> CreatePhotoTypeAsync(string name)
    {
        using var response = await SendJsonAsync(CollectionRoute, new { name });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return await ReadIdAsync(response);
    }

    private async Task<Id<TrainerPhotoType>> GetBuiltInPhotoTypeIdAsync(string systemKey)
    {
        using var response = await SendAsync(HttpMethod.Get, CollectionRoute);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var types = await ReadArrayAsync(response);
        var id = types.Single(item => item.GetProperty("systemKey").GetString() == systemKey).GetProperty("_id").GetString();
        Assert.That(string.IsNullOrWhiteSpace(id), Is.False);
        Assert.That(Id<TrainerPhotoType>.TryParse(id!, out var parsed), Is.True);
        return parsed;
    }

    private async Task<int> CountPhotoTypesAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await database.TrainerPhotoTypes.CountAsync();
    }

    private async Task<PhotoTypeSnapshot> GetPhotoTypeAsync(Id<TrainerPhotoType> id)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await database.TrainerPhotoTypes
            .AsNoTracking()
            .Where(photoType => photoType.Id == id)
            .Select(photoType => new PhotoTypeSnapshot(photoType.Name, photoType.NameMode, photoType.IsArchived))
            .SingleAsync();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route)
    {
        using var request = new HttpRequestMessage(method, route);
        request.Headers.Add(ReportingProtocolMiddleware.HeaderName, "2");
        return await Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SendJsonAsync(string route, object body)
    {
        var content = JsonContent.Create(body);
        return SendAsync(HttpMethod.Post, route, content);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, HttpContent content)
    {
        using var request = new HttpRequestMessage(method, route) { Content = content };
        request.Headers.Add(ReportingProtocolMiddleware.HeaderName, "2");
        return await Client.SendAsync(request);
    }

    private static async Task<JsonElement[]> ReadArrayAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.EnumerateArray().Select(item => item.Clone()).ToArray();
    }

    private static async Task<Id<TrainerPhotoType>> ReadIdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = document.RootElement.GetProperty("_id").GetString();
        Assert.That(string.IsNullOrWhiteSpace(id), Is.False);
        Assert.That(Id<TrainerPhotoType>.TryParse(id!, out var parsed), Is.True);
        return parsed;
    }

    private sealed record PhotoTypeSnapshot(string Name, PhotoTypeNameMode NameMode, bool IsArchived);
}
