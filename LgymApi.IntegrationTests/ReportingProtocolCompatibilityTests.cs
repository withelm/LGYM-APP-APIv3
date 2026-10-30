using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Api.Middleware;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class ReportingProtocolCompatibilityTests : IntegrationTestBase
{
    [Test]
    public async Task Capabilities_AreAvailableWithoutFeatureFlagAndRespectRolesAcknowledgementCorsAndCacheContract()
    {
        var trainer = await SeedTrainerAsync("protocol-capability-trainer", "protocol-capability-trainer@example.test");
        var trainee = await SeedUserAsync("protocol-capability-trainee", "protocol-capability-trainee@example.test");

        SetAuthorizationHeader(trainer.Id);
        using (var defaultResponse = await Client.GetAsync("/api/trainer/reporting/capabilities"))
        {
            defaultResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            using var body = JsonDocument.Parse(await defaultResponse.Content.ReadAsStringAsync());
            body.RootElement.GetProperty("supportedProtocols").EnumerateArray().Select(value => value.GetInt32()).Should().Equal(1, 2);
            body.RootElement.GetProperty("photoTypesEnabled").GetBoolean().Should().BeTrue();
            AssertProtocolHeaders(defaultResponse, "1");
        }

        using (var readyP2 = await SendAsync(
                   Client,
                   HttpMethod.Get,
                   "/api/trainer/reporting/capabilities",
                   protocol: "2"))
        {
            readyP2.StatusCode.Should().Be(HttpStatusCode.OK);
            AssertProtocolHeaders(readyP2, "2");
        }

        SetAuthorizationHeader(trainee.Id);
        using (var forbidden = await Client.GetAsync("/api/trainer/reporting/capabilities"))
        {
            forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        using (var traineeCapability = await Client.GetAsync("/api/trainee/reporting/capabilities"))
        {
            traineeCapability.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        SetAuthorizationHeader(trainer.Id);
        using var enabledFactory = CreateEnabledFactory();
        using var enabledClient = CreateAuthorizedClient(enabledFactory);
        using var enabledRequest = new HttpRequestMessage(HttpMethod.Get, "/api/trainer/reporting/capabilities");
        enabledRequest.Headers.Add(ReportingProtocolMiddleware.HeaderName, "2");
        enabledRequest.Headers.Add("Origin", "http://localhost");
        using var enabledResponse = await enabledClient.SendAsync(enabledRequest);

        enabledResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var body = JsonDocument.Parse(await enabledResponse.Content.ReadAsStringAsync()))
        {
            body.RootElement.GetProperty("supportedProtocols").EnumerateArray().Select(value => value.GetInt32()).Should().Equal(1, 2);
            body.RootElement.GetProperty("photoTypesEnabled").GetBoolean().Should().BeTrue();
        }
        AssertProtocolHeaders(enabledResponse, "2");
        enabledResponse.Headers.GetValues("Access-Control-Expose-Headers")
            .Should().ContainSingle(value => value.Contains(ReportingProtocolMiddleware.HeaderName, StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task ExistingRoutes_RejectDowngradeAndProtectSchemaTwoTemplateFromP1Editor()
    {
        var trainer = await SeedTrainerAsync("protocol-write-trainer", "protocol-write-trainer@example.test");
        SetAuthorizationHeader(trainer.Id);
        using var enabledFactory = CreateEnabledFactory();
        using var client = CreateAuthorizedClient(enabledFactory);

        using (var p1Dictionary = await client.GetAsync("/api/trainer/reporting/photo-types"))
        {
            p1Dictionary.StatusCode.Should().Be(HttpStatusCode.UpgradeRequired);
            await AssertErrorCodeAsync(p1Dictionary, "REPORTING_PROTOCOL_V2_REQUIRED");
        }
        using (var scope = enabledFactory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await database.TrainerPhotoTypes.CountAsync()).Should().Be(0, "P1 dictionary reads must not lazily initialize protocol-2 state");
        }

        string photoTypeId;
        using (var dictionary = await SendAsync(client, HttpMethod.Get, "/api/trainer/reporting/photo-types", protocol: "2"))
        {
            dictionary.StatusCode.Should().Be(HttpStatusCode.OK);
            using var body = JsonDocument.Parse(await dictionary.Content.ReadAsStringAsync());
            photoTypeId = body.RootElement.EnumerateArray()
                .Single(item => item.GetProperty("systemKey").GetString() == "Front")
                .GetProperty("_id").GetString()!;
        }

        var schemaTwoTemplate = new
        {
            name = "Protocol 2 template",
            fields = new[]
            {
                new
                {
                    key = "photos",
                    label = "Photos",
                    type = "Photos",
                    isRequired = true,
                    order = 0,
                    moduleConfig = new { schemaVersion = 2, requiredPhotoTypeIds = new[] { photoTypeId } }
                }
            }
        };
        string templateId;
        using (var created = await SendJsonAsync(client, "/api/trainer/report-templates", schemaTwoTemplate, "2"))
        {
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            created.Headers.GetValues(ReportingProtocolMiddleware.HeaderName).Should().ContainSingle("2");
            using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            templateId = body.RootElement.GetProperty("_id").GetString()!;
        }

        using (var p1Write = await client.PostAsJsonAsync("/api/trainer/report-templates", schemaTwoTemplate))
        {
            p1Write.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await AssertErrorCodeAsync(p1Write, "REPORTING_PROTOCOL_V2_REQUIRED");
        }

        using (var strippedChunkedWrite = await SendChunkedJsonAsync(client, "/api/trainer/report-templates", schemaTwoTemplate))
        {
            strippedChunkedWrite.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await AssertErrorCodeAsync(strippedChunkedWrite, "REPORTING_PROTOCOL_V2_REQUIRED");
        }

        using (var p1Read = await client.GetAsync($"/api/trainer/report-templates/{templateId}"))
        {
            p1Read.StatusCode.Should().Be(HttpStatusCode.UpgradeRequired);
            await AssertErrorCodeAsync(p1Read, "REPORTING_PROTOCOL_UPGRADE_REQUIRED");
        }

        var legacyEdit = new
        {
            name = "Downgraded name",
            fields = new[]
            {
                new
                {
                    key = "photos",
                    label = "Photos",
                    type = "Photos",
                    isRequired = true,
                    order = 0,
                    moduleConfig = new { requiredViews = new[] { "Front" } }
                }
            }
        };
        using (var overwrite = await client.PostAsJsonAsync($"/api/trainer/report-templates/{templateId}/update", legacyEdit))
        {
            overwrite.StatusCode.Should().Be(HttpStatusCode.UpgradeRequired);
            await AssertErrorCodeAsync(overwrite, "REPORTING_PROTOCOL_UPGRADE_REQUIRED");
        }

        using var unchanged = await SendAsync(client, HttpMethod.Get, $"/api/trainer/report-templates/{templateId}", protocol: "2");
        unchanged.StatusCode.Should().Be(HttpStatusCode.OK);
        using var unchangedBody = JsonDocument.Parse(await unchanged.Content.ReadAsStringAsync());
        unchangedBody.RootElement.GetProperty("name").GetString().Should().Be("Protocol 2 template");
    }

    [Test]
    public async Task SnapshotReads_AdaptBuiltInsButRejectCustomPhotoTypesForP1()
    {
        var trainer = await SeedTrainerAsync("protocol-read-trainer", "protocol-read-trainer@example.test");
        var trainee = await SeedUserAsync("protocol-read-trainee", "protocol-read-trainee@example.test");
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id
            });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        using var enabledFactory = CreateEnabledFactory();
        using var trainerClient = CreateAuthorizedClient(enabledFactory);
        var builtInId = await GetBuiltInPhotoTypeIdAsync(trainerClient);
        var builtInTemplateId = await CreateSchemaTwoTemplateAsync(trainerClient, "Built-in snapshot", builtInId);
        await CreateReportRequestAsync(trainerClient, trainee.Id, builtInTemplateId);

        SetAuthorizationHeader(trainee.Id);
        using var traineeClient = CreateAuthorizedClient(enabledFactory);
        using (var compatibleRead = await traineeClient.GetAsync("/api/trainee/report-requests"))
        {
            compatibleRead.StatusCode.Should().Be(HttpStatusCode.OK);
            using var body = JsonDocument.Parse(await compatibleRead.Content.ReadAsStringAsync());
            var moduleConfig = body.RootElement[0].GetProperty("template").GetProperty("fields")[0].GetProperty("moduleConfig");
            moduleConfig.TryGetProperty("requiredPhotoTypeIds", out _).Should().BeFalse();
            moduleConfig.GetProperty("requiredViews")[0].GetString().Should().Be("Front");
        }

        SetAuthorizationHeader(trainer.Id);
        using var customTrainerClient = CreateAuthorizedClient(enabledFactory);
        string customTypeId;
        using (var customType = await SendJsonAsync(
                   customTrainerClient,
                   "/api/trainer/reporting/photo-types",
                   new { name = "Competition pose" },
                   "2"))
        {
            customType.StatusCode.Should().Be(HttpStatusCode.Created);
            using var body = JsonDocument.Parse(await customType.Content.ReadAsStringAsync());
            customTypeId = body.RootElement.GetProperty("_id").GetString()!;
        }
        var customTemplateId = await CreateSchemaTwoTemplateAsync(customTrainerClient, "Custom snapshot", customTypeId);
        await CreateReportRequestAsync(customTrainerClient, trainee.Id, customTemplateId);

        SetAuthorizationHeader(trainee.Id);
        using var customTraineeClient = CreateAuthorizedClient(enabledFactory);
        using var incompatibleRead = await customTraineeClient.GetAsync("/api/trainee/report-requests");
        incompatibleRead.StatusCode.Should().Be(HttpStatusCode.UpgradeRequired);
        await AssertErrorCodeAsync(incompatibleRead, "REPORTING_PROTOCOL_UPGRADE_REQUIRED");
    }

    private WebApplicationFactory<Program> CreateEnabledFactory() =>
        Factory.WithWebHostBuilder(_ => { });

    private HttpClient CreateAuthorizedClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Client.DefaultRequestHeaders.Authorization;
        return client;
    }

    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name, email);
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        database.UserRoles.Add(new UserRole
        {
            UserId = trainer.Id,
            RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId
        });
        await database.SaveChangesAsync();
        return trainer;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        HttpContent? content = null,
        string? protocol = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        if (protocol is not null)
        {
            request.Headers.Add(ReportingProtocolMiddleware.HeaderName, protocol);
        }
        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendJsonAsync(HttpClient client, string path, object body, string protocol) =>
        SendAsync(client, HttpMethod.Post, path, JsonContent.Create(body), protocol);

    private static Task<HttpResponseMessage> SendChunkedJsonAsync(HttpClient client, string path, object body)
    {
        var json = JsonSerializer.Serialize(body);
        return SendAsync(client, HttpMethod.Post, path, new ChunkedJsonContent(json));
    }

    private static void AssertProtocolHeaders(HttpResponseMessage response, string protocol)
    {
        response.Headers.GetValues(ReportingProtocolMiddleware.HeaderName).Should().ContainSingle(protocol);
        response.Headers.CacheControl!.Private.Should().BeTrue();
        response.Headers.CacheControl.NoStore.Should().BeTrue();
        response.Headers.Vary.Should().Contain(ReportingProtocolMiddleware.HeaderName);
    }

    private static async Task AssertErrorCodeAsync(HttpResponseMessage response, string expected)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().Should().Be(expected);
        body.RootElement.GetProperty("msg").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<string> GetBuiltInPhotoTypeIdAsync(HttpClient client)
    {
        using var response = await SendAsync(client, HttpMethod.Get, "/api/trainer/reporting/photo-types", protocol: "2");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("systemKey").GetString() == "Front")
            .GetProperty("_id").GetString()!;
    }

    private static async Task<string> CreateSchemaTwoTemplateAsync(HttpClient client, string name, string photoTypeId)
    {
        using var response = await SendJsonAsync(client, "/api/trainer/report-templates", new
        {
            name,
            fields = new[]
            {
                new
                {
                    key = "photos",
                    label = "Photos",
                    type = "Photos",
                    isRequired = true,
                    order = 0,
                    moduleConfig = new { schemaVersion = 2, requiredPhotoTypeIds = new[] { photoTypeId } }
                }
            }
        }, "2");
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("_id").GetString()!;
    }

    private static async Task CreateReportRequestAsync(HttpClient client, Id<User> traineeId, string templateId)
    {
        using var response = await SendJsonAsync(
            client,
            $"/api/trainer/trainees/{traineeId}/report-requests",
            new { templateId },
            "2");
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private sealed class ChunkedJsonContent : HttpContent
    {
        private readonly byte[] _content;

        public ChunkedJsonContent(string content)
        {
            _content = Encoding.UTF8.GetBytes(content);
            Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(_content).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
