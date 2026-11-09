using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class MeasurementsTests : IntegrationTestBase
{
    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/measurements/add", "own", "owner-allow")]
    public async Task AddMeasurement_WithBodyWeight_CreatesMeasurement()
    {
        var (_, token) = await RegisterUserViaEndpointAsync(
            name: "measureuser",
            email: "measure@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            bodyPart = BodyParts.BodyWeight.ToString(),
            value = 80.5,
            unit = MeasurementUnits.Kilograms.ToString()
        };

        var response = await Client.PostAsJsonAsync("/api/measurements/add", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        body!.Message.Should().Be("Created");
    }

    [Test]
    public async Task AddMeasurementsBulk_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/api/measurements/add-bulk", new
        {
            measurements = new object[]
            {
                new { bodyPart = BodyParts.BodyWeight.ToString(), value = 80.2, unit = MeasurementUnits.Kilograms.ToString() }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetMeasurementsTrends_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync($"/api/measurements/{Id<UserEntity>.New()}/trends");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/measurements/{id}/getHistory", "own", "owner-allow")]
    public async Task GetMeasurementsHistory_WithLengthUnitConversion_ReturnsConvertedValues()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "historymeasureuser",
            email: "historymeasure@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await AddMeasurementAsync(BodyParts.Waist, 120.0, MeasurementUnits.Centimeters);
        await AddMeasurementAsync(BodyParts.Waist, 1.1, MeasurementUnits.Meters);

        var response = await Client.GetAsync($"/api/measurements/{userId}/getHistory?bodyPart=Waist&unit=Centimeters");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MeasurementsHistoryResponse>();
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            body!.Measurements.Should().HaveCount(2);
            body.Measurements[0].Value.Should().Be(120.0);
            body.Measurements[1].Value.Should().Be(110.0);
            body.Measurements[0].Unit!.Id.Should().Be(MeasurementUnits.Centimeters.ToString());
            body.Measurements[0].Unit!.Name.Should().Be(LgymApi.Resources.Enums.MeasurementUnits_Centimeters);
            body.Measurements[0].Unit!.DisplayName.Should().Be(LgymApi.Resources.Enums.MeasurementUnits_Centimeters);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/measurements/{id}/trend", "own", "owner-allow")]
    public async Task GetMeasurementsTrend_WhenValueGrows_ReturnsUpDirection()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "trendupuser",
            email: "trendup@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await AddMeasurementAsync(BodyParts.BodyWeight, 80.0, MeasurementUnits.Kilograms);
        await AddMeasurementAsync(BodyParts.BodyWeight, 94.1, MeasurementUnits.Kilograms);

        var response = await Client.GetAsync($"/api/measurements/{userId}/trend?bodyPart=BodyWeight&unit=Kilograms");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MeasurementTrendResponse>();
        body!.FirstMeasurementValue.Should().Be(80.0);
        body.LastMeasurementValue.Should().Be(94.1);
        body.Difference.Should().Be(14.1);
        body.Direction.Should().Be("up");
    }

    [Test]
    public async Task GetMeasurementsTrend_WhenValueDrops_ReturnsDownDirection()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "trenddownuser",
            email: "trenddown@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await AddMeasurementAsync(BodyParts.Waist, 90.0, MeasurementUnits.Centimeters);
        await AddMeasurementAsync(BodyParts.Waist, 86.6, MeasurementUnits.Centimeters);

        var response = await Client.GetAsync($"/api/measurements/{userId}/trend?bodyPart=Waist&unit=Centimeters");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MeasurementTrendResponse>();
        body!.Difference.Should().Be(3.4);
        body.Direction.Should().Be("down");
    }

    [Test]
    public async Task GetMeasurementsTrend_WhenValueDoesNotChange_ReturnsSameDirection()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "trendsameuser",
            email: "trendsame@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await AddMeasurementAsync(BodyParts.BodyFat, 18.0, MeasurementUnits.Percent);
        await AddMeasurementAsync(BodyParts.BodyFat, 18.0, MeasurementUnits.Percent);

        var response = await Client.GetAsync($"/api/measurements/{userId}/trend?bodyPart=BodyFat&unit=Percent");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MeasurementTrendResponse>();
        body!.Difference.Should().Be(0.0);
        body.Direction.Should().Be("same");
    }

    [Test]
    public async Task GetMeasurementsTrend_WhenOnlyOneMeasurementExists_ReturnsInsufficientData()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "trendoneuser",
            email: "trendone@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await AddMeasurementAsync(BodyParts.Neck, 40.0, MeasurementUnits.Centimeters);

        var response = await Client.GetAsync($"/api/measurements/{userId}/trend?bodyPart=Neck&unit=Centimeters");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MeasurementTrendResponse>();
        body!.Direction.Should().Be("insufficient_data");
        body.Points.Should().Be(1);
    }

    [Test]
    public async Task GetMeasurementsTrend_WhenNoMeasurementsExist_ReturnsInsufficientData()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "trendemptyuser",
            email: "trendempty@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync($"/api/measurements/{userId}/trend?bodyPart=Bmi&unit=Bmi");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MeasurementTrendResponse>();
        body!.Direction.Should().Be("insufficient_data");
        body.Points.Should().Be(0);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/measurements/{id}/trends", "own", "owner-allow")]
    public async Task GetMeasurementsTrends_WithMultipleTypes_ReturnsSummariesPerType()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "trendmultiuser",
            email: "trendmulti@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await AddMeasurementAsync(BodyParts.BodyWeight, 80.0, MeasurementUnits.Kilograms);
        await AddMeasurementAsync(BodyParts.BodyWeight, 82.0, MeasurementUnits.Kilograms);
        await AddMeasurementAsync(BodyParts.Waist, 90.0, MeasurementUnits.Centimeters);
        await AddMeasurementAsync(BodyParts.Waist, 88.0, MeasurementUnits.Centimeters);
        await AddMeasurementAsync(BodyParts.BodyFat, 15.0, MeasurementUnits.Percent);

        var response = await Client.GetAsync($"/api/measurements/{userId}/trends");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MeasurementTrendsResponse>();
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            var bodyWeightTrend = body!.Trends.Should().ContainSingle(x => x.BodyPart != null && x.BodyPart.Id == BodyParts.BodyWeight.ToString() && x.Direction == "up").Which;
            bodyWeightTrend.BodyPart!.Name.Should().Be(LgymApi.Resources.Enums.BodyParts_BodyWeight);
            bodyWeightTrend.BodyPart.DisplayName.Should().Be(LgymApi.Resources.Enums.BodyParts_BodyWeight);

            var waistTrend = body.Trends.Should().ContainSingle(x => x.BodyPart != null && x.BodyPart.Id == BodyParts.Waist.ToString() && x.Direction == "down").Which;
            waistTrend.BodyPart!.Name.Should().Be(LgymApi.Resources.Enums.BodyParts_Waist);
            waistTrend.BodyPart.DisplayName.Should().Be(LgymApi.Resources.Enums.BodyParts_Waist);

            var bodyFatTrend = body.Trends.Should().ContainSingle(x => x.BodyPart != null && x.BodyPart.Id == BodyParts.BodyFat.ToString() && x.Direction == "insufficient_data").Which;
            bodyFatTrend.BodyPart!.Name.Should().Be(LgymApi.Resources.Enums.BodyParts_BodyFat);
            bodyFatTrend.BodyPart.DisplayName.Should().Be(LgymApi.Resources.Enums.BodyParts_BodyFat);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/measurements/add-bulk", "own", "owner-allow")]
    public async Task AddMeasurementsBulk_WithMultipleOptionalMeasurements_CreatesAllProvidedMeasurements()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "bulkmeasureuser",
            email: "bulkmeasure@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await Client.PostAsJsonAsync("/api/measurements/add-bulk", new
        {
            measurements = new object[]
            {
                new { bodyPart = BodyParts.BodyWeight.ToString(), value = 80.2, unit = MeasurementUnits.Kilograms.ToString() },
                new { bodyPart = BodyParts.Waist.ToString(), value = 89.4, unit = MeasurementUnits.Centimeters.ToString() },
                new { bodyPart = BodyParts.BodyFat.ToString(), value = 16.1, unit = MeasurementUnits.Percent.ToString() }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var historyResponse = await Client.GetFromJsonAsync<MeasurementsHistoryResponse>($"/api/measurements/{userId}/getHistory");
        historyResponse!.Measurements.Should().HaveCount(3);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/measurements:/{id}/getMeasurementDetail", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/measurements:/{id}/getMeasurementDetail", "own", "foreign-object-denial-no-mutation")]
    public async Task GetMeasurementDetail_UsesLegacyColonRouteAndEnforcesOwnership()
    {
        var owner = await SeedUserAsync(name: "measurement-owner", email: "measurement-owner@example.com");
        var otherUser = await SeedUserAsync(name: "measurement-other", email: "measurement-other@example.com");
        SetAuthorizationHeader(owner.Id);

        var addResponse = await Client.PostAsJsonAsync("/api/measurements/add", new
        {
            bodyPart = BodyParts.BodyWeight.ToString(),
            value = 75.5,
            unit = MeasurementUnits.Kilograms.ToString()
        });
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = GetDbContext();
        var measurementId = await db.Measurements
            .Where(measurement => measurement.UserId == owner.Id)
            .Select(measurement => measurement.Id)
            .SingleAsync();

        SetAuthorizationHeader(otherUser.Id);
        var forbiddenResponse = await Client.GetAsync($"/api/measurements:/{measurementId}/getMeasurementDetail");
        forbiddenResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        SetAuthorizationHeader(owner.Id);
        var detailResponse = await Client.GetAsync($"/api/measurements:/{measurementId}/getMeasurementDetail");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var detailJson = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        detailJson.RootElement.GetProperty("user").GetString().Should().Be(owner.Id.ToString());
        detailJson.RootElement.GetProperty("value").GetDouble().Should().Be(75.5);
        detailJson.RootElement.GetProperty("bodyPart").GetProperty("id").GetString().Should().Be(BodyParts.BodyWeight.ToString());
        detailJson.RootElement.GetProperty("unit").GetProperty("id").GetString().Should().Be(MeasurementUnits.Kilograms.ToString());
    }

    [Test]
    public async Task GetMeasurementDetail_WithMalformedId_ReturnsBadRequest()
    {
        var user = await SeedUserAsync(name: "measurement-invalid-id", email: "measurement-invalid-id@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync("/api/measurements:/not-a-guid/getMeasurementDetail");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/measurements/{id}/getHistory", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/measurements/{id}/getHistory", "own", "foreign-object-denial-no-mutation")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/measurements/{id}/list", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/measurements/{id}/list", "own", "foreign-object-denial-no-mutation")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/measurements/{id}/trend", "own", "foreign-object-denial-no-mutation")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/measurements/{id}/trends", "own", "foreign-object-denial-no-mutation")]
    public async Task MeasurementAccountReadRoutes_EnforceRouteOwnership()
    {
        var owner = await SeedUserAsync(name: "measurement-read-owner", email: "measurement-read-owner@example.com");
        var attacker = await SeedUserAsync(name: "measurement-read-attacker", email: "measurement-read-attacker@example.com");
        SetAuthorizationHeader(owner.Id);
        await AddMeasurementAsync(BodyParts.BodyWeight, 80, MeasurementUnits.Kilograms);

        using var ownerHistory = await Client.GetAsync($"/api/measurements/{owner.Id}/getHistory?bodyPart=BodyWeight&unit=Kilograms");
        using var ownerList = await Client.GetAsync($"/api/measurements/{owner.Id}/list");
        ownerHistory.StatusCode.Should().Be(HttpStatusCode.OK);
        ownerList.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthorizationHeader(attacker.Id);
        var foreignResponses = new[]
        {
            await Client.GetAsync($"/api/measurements/{owner.Id}/getHistory?bodyPart=BodyWeight&unit=Kilograms"),
            await Client.GetAsync($"/api/measurements/{owner.Id}/list"),
            await Client.GetAsync($"/api/measurements/{owner.Id}/trend?bodyPart=BodyWeight&unit=Kilograms"),
            await Client.GetAsync($"/api/measurements/{owner.Id}/trends")
        };

        foreach (var response in foreignResponses)
        {
            using (response)
            {
                response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            }
        }
    }

    [Test]
    public async Task TrainerMeasurementRoutes_TransitionFromFullActiveAccessToEndedEpisodeScope()
    {
        var trainer = await SeedUserAsync(name: "measurement-scope-trainer", email: "measurement-scope-trainer@example.com");
        var trainee = await SeedUserAsync(name: "measurement-scope-trainee", email: "measurement-scope-trainee@example.com");
        var episodeId = Id<CoachingEpisode>.New();
        var startedAt = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
        var endedAt = startedAt.AddDays(2);
        var manualInsideId = Id<Measurement>.New();
        var manualOutsideId = Id<Measurement>.New();
        var trustedReportId = Id<Measurement>.New();
        var incompleteReportId = Id<Measurement>.New();

        using (var database = GetDbContext())
        {
            database.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, trainer.Id));
            database.CoachingEpisodes.Add(new CoachingEpisode
            {
                Id = episodeId,
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                StartedAt = startedAt,
                StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation,
                AuthorizationRevision = 0
            });
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                EpisodeId = episodeId
            });
            database.Measurements.AddRange(
                MeasurementRow(manualInsideId, trainee.Id, 71, startedAt.AddHours(1), startedAt.AddHours(1)),
                MeasurementRow(manualOutsideId, trainee.Id, 72, endedAt.AddHours(1), endedAt.AddHours(1)),
                MeasurementRow(
                    trustedReportId,
                    trainee.Id,
                    73,
                    endedAt.AddDays(1),
                    recordedAt: null,
                    sourceSubmissionId: Id<ReportSubmission>.New(),
                    sourceRequestId: Id<ReportRequest>.New(),
                    sourceEpisodeId: episodeId,
                    sourceAcceptedAt: endedAt.AddTicks(-1)),
                MeasurementRow(
                    incompleteReportId,
                    trainee.Id,
                    74,
                    endedAt.AddDays(1),
                    recordedAt: null,
                    sourceEpisodeId: episodeId,
                    sourceAcceptedAt: endedAt.AddTicks(-1)));
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var activeHistory = await Client.GetFromJsonAsync<MeasurementsHistoryResponse>(
            $"/api/measurements/{trainee.Id}/getHistory?bodyPart=BodyWeight&unit=Kilograms");
        activeHistory!.Measurements.Should().HaveCount(4, "an active coach has full measurement access");

        using (var database = GetDbContext())
        {
            var link = await database.TrainerTraineeLinks.SingleAsync(candidate => candidate.EpisodeId == episodeId);
            database.TrainerTraineeLinks.Remove(link);
            var episode = await database.CoachingEpisodes.SingleAsync(candidate => candidate.Id == episodeId);
            episode.EndedAt = endedAt;
            episode.AuthorizationRevision++;
            await database.SaveChangesAsync();
        }

        var historyResponse = await Client.GetAsync(
            $"/api/measurements/{trainee.Id}/getHistory?bodyPart=BodyWeight&unit=Kilograms");
        var listResponse = await Client.GetAsync($"/api/measurements/{trainee.Id}/list?bodyPart=BodyWeight&unit=Kilograms");
        var trendResponse = await Client.GetAsync(
            $"/api/measurements/{trainee.Id}/trend?bodyPart=BodyWeight&unit=Kilograms");
        var trendsResponse = await Client.GetAsync($"/api/measurements/{trainee.Id}/trends");
        var trustedDetailResponse = await Client.GetAsync($"/api/measurements:/{trustedReportId}/getMeasurementDetail");
        var incompleteDetailResponse = await Client.GetAsync($"/api/measurements:/{incompleteReportId}/getMeasurementDetail");

        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        trendResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        trendsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        trustedDetailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        incompleteDetailResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var history = await historyResponse.Content.ReadFromJsonAsync<MeasurementsHistoryResponse>();
        var list = await listResponse.Content.ReadFromJsonAsync<MeasurementsHistoryResponse>();
        var trend = await trendResponse.Content.ReadFromJsonAsync<MeasurementTrendResponse>();
        var trends = await trendsResponse.Content.ReadFromJsonAsync<MeasurementTrendsResponse>();
        history!.Measurements.Select(measurement => measurement.Value).Should().Equal(71, 73);
        list!.Measurements.Select(measurement => measurement.Value).Should().Equal(73, 71);
        trend!.Points.Should().Be(2);
        trends!.Trends.Should().ContainSingle().Which.Points.Should().Be(2);
    }

    [Test]
    public async Task MeasurementReadRoutes_WhenSubjectIsSoftDeleted_ReturnNotFoundWithRetainedData()
    {
        var trainer = await SeedUserAsync(name: "deleted-subject-trainer", email: "deleted-subject-trainer@example.com");
        var trainee = await SeedUserAsync(name: "deleted-subject-trainee", email: "deleted-subject-trainee@example.com");
        var episodeId = Id<CoachingEpisode>.New();
        var measurementId = Id<Measurement>.New();

        using (var database = GetDbContext())
        {
            database.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, trainer.Id));
            database.CoachingEpisodes.Add(new CoachingEpisode
            {
                Id = episodeId,
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                StartedAt = DateTimeOffset.UtcNow.AddDays(-1),
                StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation,
                AuthorizationRevision = 0
            });
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                EpisodeId = episodeId
            });
            database.Measurements.Add(MeasurementRow(
                measurementId,
                trainee.Id,
                81,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow));
            await database.SaveChangesAsync();

            var subject = await database.Users.IgnoreQueryFilters().SingleAsync(user => user.Id == trainee.Id);
            subject.IsDeleted = true;
            await database.SaveChangesAsync();

            (await database.Measurements.CountAsync(measurement => measurement.UserId == trainee.Id)).Should().Be(1);
            (await database.CoachingEpisodes.CountAsync(episode => episode.Id == episodeId)).Should().Be(1);
        }

        SetAuthorizationHeader(trainer.Id);
        var responses = new[]
        {
            await Client.GetAsync($"/api/measurements:/{measurementId}/getMeasurementDetail"),
            await Client.GetAsync($"/api/measurements/{trainee.Id}/list"),
            await Client.GetAsync($"/api/measurements/{trainee.Id}/getHistory?bodyPart=BodyWeight&unit=Kilograms"),
            await Client.GetAsync($"/api/measurements/{trainee.Id}/trend?bodyPart=BodyWeight&unit=Kilograms"),
            await Client.GetAsync($"/api/measurements/{trainee.Id}/trends")
        };

        foreach (var response in responses)
        {
            using (response)
            {
                response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            }
        }
    }

    private static Measurement MeasurementRow(
        Id<Measurement> id,
        Id<UserEntity> traineeId,
        double value,
        DateTimeOffset createdAt,
        DateTimeOffset? recordedAt,
        Id<ReportSubmission>? sourceSubmissionId = null,
        Id<ReportRequest>? sourceRequestId = null,
        Id<CoachingEpisode>? sourceEpisodeId = null,
        DateTimeOffset? sourceAcceptedAt = null)
        => new()
        {
            Id = id,
            UserId = traineeId,
            BodyPart = BodyParts.BodyWeight,
            Unit = MeasurementUnits.Kilograms.ToString(),
            Value = value,
            CreatedAt = createdAt,
            RecordedAt = recordedAt,
            SourceReportSubmissionId = sourceSubmissionId,
            SourceReportRequestId = sourceRequestId,
            SourceCoachingEpisodeId = sourceEpisodeId,
            SourceReportAcceptedAt = sourceAcceptedAt
        };

    private async Task AddMeasurementAsync(BodyParts bodyPart, double value, MeasurementUnits unit)
    {
        var response = await Client.PostAsJsonAsync("/api/measurements/add", new
        {
            bodyPart = bodyPart.ToString(),
            value,
            unit = unit.ToString()
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed class MessageResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class MeasurementsHistoryResponse
    {
        [JsonPropertyName("measurements")]
        public List<MeasurementResponse> Measurements { get; set; } = new();
    }

    private sealed class MeasurementTrendsResponse
    {
        [JsonPropertyName("trends")]
        public List<MeasurementTrendResponse> Trends { get; set; } = new();
    }

    private sealed class MeasurementResponse
    {
        [JsonPropertyName("bodyPart")]
        public BodyPartLookup? BodyPart { get; set; }

        [JsonPropertyName("unit")]
        public UnitLookup? Unit { get; set; }

        [JsonPropertyName("value")]
        public double Value { get; set; }
    }

    private sealed class BodyPartLookup
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;
    }

    private sealed class UnitLookup
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;
    }

    private sealed class MeasurementTrendResponse
    {
        [JsonPropertyName("bodyPart")]
        public BodyPartLookup? BodyPart { get; set; }

        [JsonPropertyName("firstMeasurementValue")]
        public double? FirstMeasurementValue { get; set; }

        [JsonPropertyName("lastMeasurementValue")]
        public double? LastMeasurementValue { get; set; }

        [JsonPropertyName("difference")]
        public double? Difference { get; set; }

        [JsonPropertyName("direction")]
        public string Direction { get; set; } = string.Empty;

        [JsonPropertyName("points")]
        public int Points { get; set; }
    }
}
