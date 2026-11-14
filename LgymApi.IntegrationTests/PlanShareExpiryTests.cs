using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class PlanShareExpiryTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestCase(false)]
    [TestCase(true)]
    public async Task Share_LegacyEmptyRequest_DefaultsToSevenDays(bool emptyObject)
    {
        var owner = await SeedUserAsync("default-owner", "default-owner@example.com");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Plan", ShareCode = "LEGACY0001" };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(owner.Id);
        using var factory = Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Client.DefaultRequestHeaders.Authorization;

        var response = emptyObject
            ? await client.PostAsJsonAsync($"/api/{plan.Id}/share", new { })
            : await client.PostAsync($"/api/{plan.Id}/share", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("shareCode").GetString().Should().NotBe("LEGACY0001");
        using var verifyScope = factory.Services.CreateScope();
        var persisted = await verifyScope.ServiceProvider.GetRequiredService<AppDbContext>().Plans.SingleAsync(p => p.Id == plan.Id);
        persisted.ShareCodeExpiresAt.Should().Be(Now.AddDays(7));
        persisted.ShareCodeDurationDays.Should().Be(7);
    }

    [TestCase(1)]
    [TestCase(7)]
    [TestCase(30)]
    public async Task Share_ExplicitDuration_PersistsExpiryAndPreservesResponseShape(int durationDays)
    {
        var owner = await SeedUserAsync("duration-owner", "duration-owner@example.com");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Plan" };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(owner.Id);
        using var factory = Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Client.DefaultRequestHeaders.Authorization;

        var response = await client.PostAsJsonAsync($"/api/{plan.Id}/share", new { durationDays });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.EnumerateObject().Select(property => property.Name).Should().Equal("shareCode");
        using var verifyScope = factory.Services.CreateScope();
        var persisted = await verifyScope.ServiceProvider.GetRequiredService<AppDbContext>().Plans.SingleAsync(p => p.Id == plan.Id);
        persisted.ShareCode.Should().Be(body.GetProperty("shareCode").GetString());
        persisted.ShareCodeDurationDays.Should().Be(durationDays);
        persisted.ShareCodeExpiresAt.Should().Be(Now.AddDays(durationDays));
    }

    [TestCase(-1, HttpStatusCode.Created)]
    [TestCase(0, HttpStatusCode.NotFound)]
    [TestCase(1, HttpStatusCode.NotFound)]
    public async Task Copy_RespectsStrictExpiryBoundaryAndPreservesStateOnDenial(int secondsAfterExpiry, HttpStatusCode expected)
    {
        var owner = await SeedUserAsync("boundary-owner", "boundary-owner@example.com");
        var recipient = await SeedUserAsync("boundary-recipient", "boundary-recipient@example.com");
        var existing = new Plan { Id = Id<Plan>.New(), UserId = recipient.Id, Name = "Existing", IsActive = true };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plans.AddRange(existing, new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Source", ShareCode = "BOUNDARY01", ShareCodeExpiresAt = Now, ShareCodeDurationDays = 7 });
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(recipient.Id);
        using var factory = Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now.AddSeconds(secondsAfterExpiry)));
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Client.DefaultRequestHeaders.Authorization;

        var response = await client.PostAsJsonAsync("/api/copy", new { shareCode = "BOUNDARY01" });

        response.StatusCode.Should().Be(expected);
        using var verifyScope = factory.Services.CreateScope();
        var dbAfter = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recipientPlans = await dbAfter.Plans.Where(p => p.UserId == recipient.Id).ToListAsync();
        recipientPlans.Should().HaveCount(expected == HttpStatusCode.Created ? 2 : 1);
        recipientPlans.Single(p => p.Id == existing.Id).IsActive.Should().Be(expected != HttpStatusCode.Created);
    }

    [Test]
    public async Task Copy_LegacyCodeWithoutExpiry_IsDeniedWithoutChangingRecipient()
    {
        var owner = await SeedUserAsync("expiry-owner", "expiry-owner@example.com");
        var recipient = await SeedUserAsync("expiry-recipient", "expiry-recipient@example.com");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plans.Add(new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Legacy", ShareCode = "LEGACY0001" });
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(recipient.Id);

        var response = await Client.PostAsJsonAsync("/api/copy", new { shareCode = "LEGACY0001" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verifyDb.Plans.CountAsync(p => p.UserId == recipient.Id)).Should().Be(0);
    }

    [TestCase(0)]
    [TestCase(2)]
    [TestCase(31)]
    public async Task Share_InvalidDuration_IsRejectedWithoutIssuingCode(int durationDays)
    {
        var owner = await SeedUserAsync("duration-owner", "duration-owner@example.com");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Plan" };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(owner.Id);

        var response = await Client.PostAsJsonAsync($"/api/{plan.Id}/share", new { durationDays });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verifyDb.Plans.SingleAsync(p => p.Id == plan.Id)).ShareCode.Should().BeNull();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
