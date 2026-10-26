using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class TrainerDashboardTests : IntegrationTestBase
{
    [TestCase("2026-10-22", "Europe/Warsaw", "2026-10-21T22:00:00Z", "2026-10-22T22:00:00Z")]
    [TestCase("2026-03-29", "Europe/Warsaw", "2026-03-28T23:00:00Z", "2026-03-29T22:00:00Z")]
    [TestCase("2026-10-25", "Europe/Warsaw", "2026-10-24T22:00:00Z", "2026-10-25T23:00:00Z")]
    [TestCase("2026-10-22", "Asia/Kathmandu", "2026-10-21T18:15:00Z", "2026-10-22T18:15:00Z")]
    [TestCase("2026-10-22", "America/St_Johns", "2026-10-22T02:30:00Z", "2026-10-23T02:30:00Z")]
    [TestCase("2026-10-22", "UTC", "2026-10-22T00:00:00Z", "2026-10-23T00:00:00Z")]
    public async Task AccountAndTrainerHistory_UseInclusiveStartExclusiveEnd(
        string date, string timeZone, string start, string end)
    {
        var trainee = await SeedUserAsync(name: "local-trainee", email: "local-trainee@example.com");
        var trainer = await SeedUserAsync(name: "local-trainer", email: "local-trainer@example.com");
        var foreign = await SeedUserAsync(name: "local-foreign", email: "local-foreign@example.com");
        var from = DateTimeOffset.Parse(start);
        var to = DateTimeOffset.Parse(end);
        var instants = new[] { from.AddTicks(-1), from, from.AddMinutes(22).AddSeconds(4).AddMilliseconds(551), to.AddTicks(-1), to };
        var rows = await SeedHistoryAsync(trainee.Id, trainer.Id, foreign.Id, instants);
        var expected = rows.Skip(1).Take(3).Select(row => row.Id.ToString()).ToArray();

        SetAuthorizationHeader(trainee.Id);
        await AssertHistoryAsync($"/api/{trainee.Id}/getTrainingByDate", new { date, timeZone }, expected);
        SetAuthorizationHeader(trainer.Id);
        await AssertHistoryAsync($"/api/trainer/trainees/{trainee.Id}/trainings/by-date", new { date, timeZone }, expected);

        var legacyDay = new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
        var legacyExpected = rows.Where(row => row.CreatedAt >= legacyDay && row.CreatedAt < legacyDay.AddDays(1))
            .Select(row => row.Id.ToString()).ToArray();
        // Legacy UTC-day requests must select the same rows irrespective of the local-day query.
        if (legacyExpected.Length > 0)
        {
            await AssertHistoryAsync($"/api/trainer/trainees/{trainee.Id}/trainings/by-date", new { createdAt = legacyDay }, legacyExpected);
            SetAuthorizationHeader(trainee.Id);
            await AssertHistoryAsync($"/api/{trainee.Id}/getTrainingByDate", new { createdAt = legacyDay }, legacyExpected);
        }
    }

    [TestCase("{}")]
    [TestCase("null")]
    [TestCase("{\"date\":\"2026-02-30\",\"timeZone\":\"UTC\"}")]
    [TestCase("{\"date\":\"not-a-date\",\"timeZone\":\"UTC\"}")]
    [TestCase("{\"createdAt\":\"bad\"}")]
    [TestCase("{\"date\":null,\"timeZone\":\"UTC\"}")]
    [TestCase("{\"createdAt\":null}")]
    [TestCase("{\"date\":\"2026-10-22\",\"timeZone\":null}")]
    [TestCase("{\"date\":\"2026-10-22\"}")]
    [TestCase("{\"date\":\"2026-10-22\",\"timeZone\":\"Not/AZone\"}")]
    [TestCase("{\"date\":\"2026-10-22\",\"timeZone\":\"Pacific Standard Time\"}")]
    [TestCase("{\"date\":\"9999-12-31\",\"timeZone\":\"UTC\"}")]
    [TestCase("{\"date\":\"0001-01-01\",\"timeZone\":\"Etc/GMT-14\"}")]
    [TestCase("{\"date\":\"2011-12-30\",\"timeZone\":\"Pacific/Apia\"}")]
    [TestCase("{\"date\":\"2026-10-22\",\"timeZone\":\"UTC\",\"createdAt\":\"2026-10-22T00:00:00Z\"}")]
    public async Task AccountAndTrainerHistory_RejectInvalidBodies(string json)
    {
        var trainee = await SeedUserAsync(name: "invalid-trainee", email: "invalid-trainee@example.com");
        var trainer = await SeedUserAsync(name: "invalid-trainer", email: "invalid-trainer@example.com");
        await SeedHistoryAsync(trainee.Id, trainer.Id, trainee.Id, []);
        foreach (var (actor, route) in new[]
        {
            (trainee.Id, $"/api/{trainee.Id}/getTrainingByDate"),
            (trainer.Id, $"/api/trainer/trainees/{trainee.Id}/trainings/by-date")
        })
        {
            SetAuthorizationHeader(actor);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Client.PostAsync(route, content);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "{0}: {1}", route, await response.Content.ReadAsStringAsync());
        }
    }

    private async Task AssertHistoryAsync(string route, object body, string[] expected)
    {
        using var response = await Client.PostAsJsonAsync(route, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.EnumerateArray().Select(row => row.GetProperty("_id").GetString())
            .Should().BeEquivalentTo(expected);
    }

    private async Task<Training[]> SeedHistoryAsync(Id<User> trainee, Id<User> trainer, Id<User> foreign, DateTimeOffset[] instants)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserRoles.Add(new UserRole { UserId = trainer, RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId });
        db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer, TraineeId = trainee });
        var plan = new Plan { Id = Id<Plan>.New(), UserId = trainee, Name = "Local history" };
        var day = new PlanDay { Id = Id<PlanDay>.New(), PlanId = plan.Id, Name = "Local day" };
        var gym = new Gym { Id = Id<Gym>.New(), UserId = trainee, Name = "Local gym" };
        db.Plans.Add(plan);
        db.PlanDays.Add(day);
        db.Gyms.Add(gym);
        var rows = instants.Select(instant => new Training
        {
            Id = Id<Training>.New(), UserId = trainee, TypePlanDayId = day.Id, GymId = gym.Id, CreatedAt = instant
        }).ToArray();
        db.Trainings.AddRange(rows);
        if (instants.Length > 0)
        {
            db.Trainings.Add(new Training { Id = Id<Training>.New(), UserId = foreign, TypePlanDayId = day.Id, GymId = gym.Id, CreatedAt = instants[1] });
        }
        await db.SaveChangesAsync();
        return rows;
    }
}
