using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LgymApi.IntegrationTests.Pagination;

[TestFixture]
public sealed class TrainerInvitationPaginationTests : IntegrationTestBase
{
    [Test]
    public async Task TrainerInvitationPagination_DefaultCreatedAtOrderAndIdentityFieldsApplyBeforePages()
    {
        // Arrange
        var trainer = await SeedTrainerAsync("trainer-invitation-pagination", "trainer-invitation-pagination@example.com");
        var newestTrainee = await SeedUserAsync(name: "Newest identity profile", email: "newest-profile@example.com", password: "password123");
        var tiedTrainee = await SeedUserAsync(name: "Tied identity profile", email: "tied-profile@example.com", password: "password123");
        var oldestTrainee = await SeedUserAsync(name: "Oldest identity profile", email: "oldest-profile@example.com", password: "password123");
        var sharedCreatedAt = new DateTimeOffset(2025, 4, 4, 10, 0, 0, TimeSpan.Zero);
        var newestInvitationId = new Id<TrainerInvitation>(new Guid("f1000000-0000-0000-0000-000000000001"));
        var tiedInvitationId = new Id<TrainerInvitation>(new Guid("e1000000-0000-0000-0000-000000000001"));
        var oldestInvitationId = new Id<TrainerInvitation>(new Guid("d1000000-0000-0000-0000-000000000001"));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerInvitations.AddRange(
                new TrainerInvitation
                {
                    Id = newestInvitationId,
                    TrainerId = trainer.Id,
                    TraineeId = newestTrainee.Id,
                    InviteeEmail = newestTrainee.Email,
                    Code = "PAGING-NEWEST",
                    Status = TrainerInvitationStatus.Pending,
                    ExpiresAt = new DateTimeOffset(2030, 4, 4, 10, 0, 0, TimeSpan.Zero),
                    CreatedAt = sharedCreatedAt,
                    UpdatedAt = sharedCreatedAt
                },
                new TrainerInvitation
                {
                    Id = tiedInvitationId,
                    TrainerId = trainer.Id,
                    TraineeId = tiedTrainee.Id,
                    InviteeEmail = tiedTrainee.Email,
                    Code = "PAGING-TIED",
                    Status = TrainerInvitationStatus.Pending,
                    ExpiresAt = new DateTimeOffset(2030, 4, 4, 10, 0, 0, TimeSpan.Zero),
                    CreatedAt = sharedCreatedAt,
                    UpdatedAt = sharedCreatedAt
                },
                new TrainerInvitation
                {
                    Id = oldestInvitationId,
                    TrainerId = trainer.Id,
                    TraineeId = oldestTrainee.Id,
                    InviteeEmail = oldestTrainee.Email,
                    Code = "PAGING-OLDEST",
                    Status = TrainerInvitationStatus.Pending,
                    ExpiresAt = new DateTimeOffset(2030, 4, 4, 10, 0, 0, TimeSpan.Zero),
                    CreatedAt = sharedCreatedAt.AddDays(-1),
                    UpdatedAt = sharedCreatedAt.AddDays(-1)
                });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);

        // Act
        var firstPageResponse = await Client.PostAsJsonAsync("/api/trainer/invitations/paginated", new { page = 1, pageSize = 2 });
        var secondPageResponse = await Client.PostAsJsonAsync("/api/trainer/invitations/paginated", new { page = 2, pageSize = 2 });
        var cappedPageResponse = await Client.PostAsJsonAsync("/api/trainer/invitations/paginated", new { page = 1, pageSize = 101 });

        // Assert
        Assert.That(firstPageResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(secondPageResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(cappedPageResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var firstPage = JsonDocument.Parse(await firstPageResponse.Content.ReadAsStringAsync());
        Assert.That(firstPage.RootElement.GetProperty("page").GetInt32(), Is.EqualTo(1));
        Assert.That(firstPage.RootElement.GetProperty("pageSize").GetInt32(), Is.EqualTo(2));
        Assert.That(firstPage.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(3));
        Assert.That(firstPage.RootElement.GetProperty("totalPages").GetInt32(), Is.EqualTo(2));
        Assert.That(firstPage.RootElement.GetProperty("hasNextPage").GetBoolean(), Is.True);
        Assert.That(firstPage.RootElement.GetProperty("hasPreviousPage").GetBoolean(), Is.False);

        var firstPageItems = firstPage.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.That(firstPageItems.Select(item => item.GetProperty("_id").GetString()), Is.EqualTo(new[] { oldestInvitationId.ToString(), tiedInvitationId.ToString() }));
        Assert.That(firstPageItems[0].GetProperty("traineeId").GetString(), Is.EqualTo(oldestTrainee.Id.ToString()));
        Assert.That(firstPageItems[0].GetProperty("traineeName").GetString(), Is.EqualTo(oldestTrainee.Name));
        Assert.That(firstPageItems[0].GetProperty("traineeEmail").GetString(), Is.EqualTo((string)(oldestTrainee.Email)));
        Assert.That(firstPageItems[1].GetProperty("traineeId").GetString(), Is.EqualTo(tiedTrainee.Id.ToString()));
        Assert.That(firstPageItems[1].GetProperty("traineeName").GetString(), Is.EqualTo(tiedTrainee.Name));
        Assert.That(firstPageItems[1].GetProperty("traineeEmail").GetString(), Is.EqualTo((string)(tiedTrainee.Email)));

        using var secondPage = JsonDocument.Parse(await secondPageResponse.Content.ReadAsStringAsync());
        Assert.That(secondPage.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("_id").GetString()), Is.EqualTo(new[] { newestInvitationId.ToString() }));
        Assert.That(secondPage.RootElement.GetProperty("hasNextPage").GetBoolean(), Is.False);
        Assert.That(secondPage.RootElement.GetProperty("hasPreviousPage").GetBoolean(), Is.True);

        using var cappedPage = JsonDocument.Parse(await cappedPageResponse.Content.ReadAsStringAsync());
        Assert.That(cappedPage.RootElement.GetProperty("pageSize").GetInt32(), Is.EqualTo(100));
        Assert.That(cappedPage.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(3));
    }

    [Test]
    public async Task TrainerInvitationPagination_NonPositivePageSizeUsesDefault()
    {
        // Arrange
        var trainer = await SeedTrainerAsync("trainer-invitation-normalization", "trainer-invitation-normalization@example.com");
        var trainee = await SeedUserAsync(name: "Normalization identity profile", email: "normalization-profile@example.com", password: "password123");
        var invitationId = new Id<TrainerInvitation>(new Guid("c1000000-0000-0000-0000-000000000001"));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerInvitations.Add(new TrainerInvitation
            {
                Id = invitationId,
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                InviteeEmail = trainee.Email,
                Code = "INMEMORY-NORMALIZATION",
                Status = TrainerInvitationStatus.Pending,
                ExpiresAt = new DateTimeOffset(2030, 7, 7, 8, 0, 0, TimeSpan.Zero),
                CreatedAt = new DateTimeOffset(2025, 7, 7, 8, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2025, 7, 7, 8, 0, 0, TimeSpan.Zero)
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);

        // Act
        var response = await Client.PostAsJsonAsync("/api/trainer/invitations/paginated", new { page = 1, pageSize = 0 });

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("page").GetInt32(), Is.EqualTo(1));
        Assert.That(body.RootElement.GetProperty("pageSize").GetInt32(), Is.EqualTo(20));
        var assertedValue183 = (body.RootElement.GetProperty("items").EnumerateArray()).ToArray();
        Assert.That(assertedValue183, Has.Exactly(1).Items);
        var item = assertedValue183.Single();
        Assert.That(item.GetProperty("_id").GetString(), Is.EqualTo(invitationId.ToString()));
    }

    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name: name, email: email, password: "password123");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(db, trainer.Id));
        await db.SaveChangesAsync();

        return trainer;
    }
}
