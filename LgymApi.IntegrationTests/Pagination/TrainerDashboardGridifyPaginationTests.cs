using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LgymApi.IntegrationTests.Pagination;

/// <summary>
/// Integration tests for trainer dashboard Gridify pagination.
/// Validates the contract after the Gridify-backed pagination pipeline is wired into the trainer dashboard flow.
/// </summary>
[TestFixture]
public sealed class TrainerDashboardGridifyPaginationTests : IntegrationTestBase
{
    [Test]
    public async Task TrainerDashboardGridifyPagination_ReturnsCorrectPageAndTotal()
    {
        // Arrange
        var trainer = await SeedTrainerAsync("trainer-grid-page", "trainer-grid-page@example.com");
        var alpha = await SeedDashboardTraineeAsync(trainer.Id, "alpha-page", "alpha-page@example.com", linked: true, linkedAt: DateTimeOffset.UtcNow.AddDays(-3));
        var beta = await SeedDashboardTraineeAsync(trainer.Id, "beta-page", "beta-page@example.com", invited: true, invitationStatus: TrainerInvitationStatus.Pending, invitationExpiresAt: DateTimeOffset.UtcNow.AddDays(2));
        var gamma = await SeedDashboardTraineeAsync(trainer.Id, "gamma-page", "gamma-page@example.com", linked: true, linkedAt: DateTimeOffset.UtcNow.AddDays(-1));
        SetAuthorizationHeader(trainer.Id);

        // Act
        var response = await Client.GetAsync("/api/trainer/trainees?page=1&pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ParseDashboardResponseAsync(response);
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(2);
        body.GetProperty("total").GetInt32().Should().Be(3);

        var items = body.GetProperty("items").EnumerateArray().ToArray();
        items.Should().HaveCount(2);
        items.Select(x => x.GetProperty("name").GetString()).Should().Equal("alpha-page", "beta-page");
    }

    [Test]
    public async Task TrainerDashboardGridifyPagination_AppliesMultiSortDeterministically()
    {
        // Arrange
        var trainer = await SeedTrainerAsync("trainer-grid-sort", "trainer-grid-sort@example.com");
        var first = await SeedDashboardTraineeAsync(trainer.Id, "Alpha Name", "alpha-name@example.com", linked: true, linkedAt: DateTimeOffset.UtcNow.AddDays(-5));
        var second = await SeedDashboardTraineeAsync(trainer.Id, "Beta Name", "beta-name@example.com", linked: true, linkedAt: DateTimeOffset.UtcNow.AddDays(-1));
        var third = await SeedDashboardTraineeAsync(trainer.Id, "Zeta Name", "zeta-name@example.com", linked: true, linkedAt: DateTimeOffset.UtcNow.AddDays(-3));
        SetAuthorizationHeader(trainer.Id);

        // Act
        var response = await Client.GetAsync("/api/trainer/trainees?sortBy=name&sortDirection=asc&page=1&pageSize=50");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ParseDashboardResponseAsync(response);
        var items = body.GetProperty("items").EnumerateArray().ToArray();
        items.Select(x => x.GetProperty("name").GetString()).Should().Equal("Alpha Name", "Beta Name", "Zeta Name");
    }

    [Test]
    public async Task TrainerDashboardGridifyPagination_AppliesSearchFilter()
    {
        // Arrange
        var trainer = await SeedTrainerAsync("trainer-grid-filter", "trainer-grid-filter@example.com");
        var matching = await SeedDashboardTraineeAsync(trainer.Id, "Search Match", "search-match@example.com", invited: true, invitationStatus: TrainerInvitationStatus.Pending, invitationExpiresAt: DateTimeOffset.UtcNow.AddDays(1));
        await SeedDashboardTraineeAsync(trainer.Id, "Other Person", "other-person@example.com", linked: true, linkedAt: DateTimeOffset.UtcNow.AddDays(-1));
        SetAuthorizationHeader(trainer.Id);

        // Act
        var response = await Client.GetAsync("/api/trainer/trainees?search=match&page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ParseDashboardResponseAsync(response);
        body.GetProperty("total").GetInt32().Should().Be(1);
        body.GetProperty("items").EnumerateArray().Should().ContainSingle(x => x.GetProperty("_id").GetString() == matching.Id.ToString());
    }

    [Test]
    public async Task TrainerDashboardGridifyPagination_ReturnsEmptyPageBeyondTotal()
    {
        // Arrange
        var trainer = await SeedTrainerAsync("trainer-grid-beyond", "trainer-grid-beyond@example.com");
        SetAuthorizationHeader(trainer.Id);

        // Act: request a page far beyond any reasonable total
        var response = await Client.GetAsync("/api/trainer/trainees?page=99999&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ParseDashboardResponseAsync(response);
        body.GetProperty("items").EnumerateArray().Should().BeEmpty();
    }

    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name: name, email: email, password: "password123");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var alreadyLinked = await db.UserRoles.AnyAsync(ur => ur.UserId == trainer.Id && ur.RoleId == RoleSeedDataConfiguration.TrainerRoleSeedId);
        if (!alreadyLinked)
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = trainer.Id,
                RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId
            });
        }

        await db.SaveChangesAsync();

        return trainer;
    }

    private async Task<User> SeedDashboardTraineeAsync(
        Id<User> trainerId,
        string name,
        string email,
        bool linked = false,
        DateTimeOffset? linkedAt = null,
        bool invited = false,
        TrainerInvitationStatus invitationStatus = TrainerInvitationStatus.Pending,
        DateTimeOffset? invitationExpiresAt = null)
    {
        var trainee = await SeedUserAsync(name: name, email: email, password: "password123");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (linked)
        {
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainerId,
                TraineeId = trainee.Id,
                CreatedAt = linkedAt ?? DateTimeOffset.UtcNow,
                UpdatedAt = linkedAt ?? DateTimeOffset.UtcNow
            });
        }

        if (invited)
        {
            db.TrainerInvitations.Add(new TrainerInvitation
            {
                Id = Id<TrainerInvitation>.New(),
                TrainerId = trainerId,
                TraineeId = trainee.Id,
                Code = $"INV-{Guid.NewGuid():N}",
                Status = invitationStatus,
                ExpiresAt = invitationExpiresAt ?? DateTimeOffset.UtcNow.AddDays(1),
                CreatedAt = invitationExpiresAt ?? DateTimeOffset.UtcNow,
                UpdatedAt = invitationExpiresAt ?? DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();

        return trainee;
    }

    private static async Task<JsonElement> ParseDashboardResponseAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
