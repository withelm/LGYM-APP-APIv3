using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
internal sealed class PostgreSqlTrainerRelationshipPaginationTests : PostgreSqlIntegrationTestBase
{
    [Test]
    public async Task DashboardPagination_UsesCaseInsensitiveEnrichedRowsBeforeTotalsAndPages()
    {
        // Arrange
        var trainer = await SeedTrainerAsync("postgres-pagination-dashboard-trainer", "postgres-pagination-dashboard-trainer@example.com");
        var matchingByName = await SeedUserAsync("Alpha MIXED profile", "postgres-pagination-alpha@example.com");
        var matchingByEmail = await SeedUserAsync("Zulu profile", "postgres-pagination.MIXED@example.com");
        var deleted = await SeedUserAsync("Aardvark MIXED deleted", "postgres-pagination-deleted@example.com");
        var linkedAt = new DateTimeOffset(2025, 5, 5, 8, 0, 0, TimeSpan.Zero);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.SingleAsync(user => user.Id == deleted.Id)).IsDeleted = true;
            db.TrainerTraineeLinks.AddRange(
                CreateLink("60000000-0000-0000-0000-000000000001", trainer.Id, matchingByName.Id, linkedAt),
                CreateLink("60000000-0000-0000-0000-000000000002", trainer.Id, matchingByEmail.Id, linkedAt),
                CreateLink("60000000-0000-0000-0000-000000000003", trainer.Id, deleted.Id, linkedAt));
            await db.SaveChangesAsync();
        }

        await AuthenticateAsAsync(trainer);

        // Act
        var response = await Client.GetAsync("/api/trainer/trainees?search=mIxEd&sortBy=name&sortDirection=asc&page=2&pageSize=1");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("total").GetInt32(), Is.EqualTo(2));
        Assert.That((body.RootElement.GetProperty("items").EnumerateArray()).Where(item => item.GetProperty("_id").GetString() == matchingByEmail.Id.ToString()), Has.Exactly(1).Items);
    }

    [Test]
    public async Task InvitationPagination_KeepsNullTraineeRowsAndEnrichedFieldsAcrossPages()
    {
        // Arrange
        var trainer = await SeedTrainerAsync("postgres-pagination-invitation-trainer", "postgres-pagination-invitation-trainer@example.com");
        var trainee = await SeedUserAsync("PostgreSQL identity profile", "postgres-pagination-profile@example.com");
        var createdAt = new DateTimeOffset(2025, 6, 6, 8, 0, 0, TimeSpan.Zero);
        var emailOnlyInvitationId = new Id<TrainerInvitation>(new Guid("70000000-0000-0000-0000-000000000001"));
        var profileInvitationId = new Id<TrainerInvitation>(new Guid("80000000-0000-0000-0000-000000000001"));
        var latestInvitationId = new Id<TrainerInvitation>(new Guid("90000000-0000-0000-0000-000000000001"));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerInvitations.AddRange(
                CreateInvitation(emailOnlyInvitationId, trainer.Id, null, "postgres-email-only@example.com", "POSTGRES-EMAIL-ONLY", createdAt),
                CreateInvitation(profileInvitationId, trainer.Id, trainee.Id, trainee.Email, "POSTGRES-PROFILE", createdAt.AddDays(1)),
                CreateInvitation(latestInvitationId, trainer.Id, trainee.Id, trainee.Email, "POSTGRES-LATEST", createdAt.AddDays(2)));
            await db.SaveChangesAsync();
        }

        await AuthenticateAsAsync(trainer);

        // Act
        var firstPageResponse = await Client.PostAsJsonAsync("/api/trainer/invitations/paginated", new { page = 1, pageSize = 2 });
        var secondPageResponse = await Client.PostAsJsonAsync("/api/trainer/invitations/paginated", new { page = 2, pageSize = 2 });

        // Assert
        Assert.That(firstPageResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(secondPageResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var firstPage = JsonDocument.Parse(await firstPageResponse.Content.ReadAsStringAsync());
        Assert.That(firstPage.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(3));
        var firstPageItems = firstPage.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.That(firstPageItems.Select(item => item.GetProperty("_id").GetString()), Is.EqualTo(new[] { emailOnlyInvitationId.ToString(), profileInvitationId.ToString() }));
        Assert.That(firstPageItems[0].GetProperty("traineeId").GetString(), Is.Empty);
        Assert.That(firstPageItems[0].TryGetProperty("traineeName", out _), Is.False);
        Assert.That(firstPageItems[0].TryGetProperty("traineeEmail", out _), Is.False);
        Assert.That(firstPageItems[1].GetProperty("traineeName").GetString(), Is.EqualTo(trainee.Name));
        Assert.That(firstPageItems[1].GetProperty("traineeEmail").GetString(), Is.EqualTo((string)(trainee.Email)));

        using var secondPage = JsonDocument.Parse(await secondPageResponse.Content.ReadAsStringAsync());
        Assert.That(secondPage.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("_id").GetString()), Is.EqualTo(new[] { latestInvitationId.ToString() }));
    }

    [Test]
    public async Task DashboardPagination_UsesIdTieBreakForDuplicateComputedStatuses()
    {
        // Arrange
        var trainer = await SeedTrainerAsync("postgres-pagination-name-tie-trainer", "postgres-pagination-name-tie-trainer@example.com");
        var firstById = CreateUser("11000000-0000-0000-0000-000000000001", "Zulu PostgreSQL status tie", "postgres-status-tie-first@example.com");
        var secondById = CreateUser("f1000000-0000-0000-0000-000000000001", "Alpha PostgreSQL status tie", "postgres-status-tie-second@example.com");
        var linkedAt = new DateTimeOffset(2025, 7, 7, 8, 0, 0, TimeSpan.Zero);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.AddRange(secondById, firstById);
            db.TrainerTraineeLinks.AddRange(
                CreateLink("f2000000-0000-0000-0000-000000000001", trainer.Id, secondById.Id, linkedAt),
                CreateLink("12000000-0000-0000-0000-000000000001", trainer.Id, firstById.Id, linkedAt));
            await db.SaveChangesAsync();
        }

        await AuthenticateAsAsync(trainer);

        // Act
        var response = await Client.GetAsync("/api/trainer/trainees?sortBy=status&sortDirection=asc&page=1&pageSize=2");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("total").GetInt32(), Is.EqualTo(2));
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.That(items.Select(item => item.GetProperty("_id").GetString()), Is.EqualTo(new[] { firstById.Id.ToString(), secondById.Id.ToString() }));
        Assert.That(items.Select(item => item.GetProperty("status").GetString()), Is.EqualTo(new[] { "Linked", "Linked" }));
    }

    [Test]
    public async Task DashboardPagination_UsesLatestInvitationIdForComputedStatusOrder()
    {
        // Arrange
        var trainer = await SeedTrainerAsync("postgres-pagination-status-trainer", "postgres-pagination-status-trainer@example.com");
        var linked = CreateUser("13000000-0000-0000-0000-000000000001", "Zulu linked profile", "postgres-status-linked@example.com");
        var pending = CreateUser("23000000-0000-0000-0000-000000000001", "Alpha pending profile", "postgres-status-pending@example.com");
        var expired = CreateUser("33000000-0000-0000-0000-000000000001", "Bravo expired profile", "postgres-status-expired@example.com");
        var createdAt = new DateTimeOffset(2025, 8, 8, 8, 0, 0, TimeSpan.Zero);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.AddRange(linked, pending, expired);
            db.TrainerTraineeLinks.Add(CreateLink("14000000-0000-0000-0000-000000000001", trainer.Id, linked.Id, createdAt));
            db.TrainerInvitations.AddRange(
                CreateInvitation(
                    new Id<TrainerInvitation>(new Guid("24000000-0000-0000-0000-000000000001")),
                    trainer.Id,
                    pending.Id,
                    pending.Email,
                    "POSTGRES-STATUS-PENDING",
                    createdAt),
                CreateInvitation(
                    new Id<TrainerInvitation>(new Guid("15000000-0000-0000-0000-000000000001")),
                    trainer.Id,
                    expired.Id,
                    expired.Email,
                    "POSTGRES-LATEST-FUTURE",
                    createdAt,
                    new DateTimeOffset(2030, 8, 8, 8, 0, 0, TimeSpan.Zero)),
                CreateInvitation(
                    new Id<TrainerInvitation>(new Guid("f5000000-0000-0000-0000-000000000001")),
                    trainer.Id,
                    expired.Id,
                    expired.Email,
                    "POSTGRES-LATEST-EXPIRED",
                    createdAt,
                    new DateTimeOffset(2020, 8, 8, 8, 0, 0, TimeSpan.Zero)));
            await db.SaveChangesAsync();
        }

        await AuthenticateAsAsync(trainer);

        // Act
        var response = await Client.GetAsync("/api/trainer/trainees?sortBy=status&sortDirection=asc&page=1&pageSize=10");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.That(items.Select(item => item.GetProperty("_id").GetString()), Is.EqualTo(new[] { linked.Id.ToString(), pending.Id.ToString(), expired.Id.ToString() }));
        Assert.That(items.Select(item => item.GetProperty("status").GetString()), Is.EqualTo(new[] { "Linked", "InvitationPending", "InvitationExpired" }));
        Assert.That(items[2].GetProperty("hasPendingInvitation").GetBoolean(), Is.False);
        Assert.That(items[2].GetProperty("hasExpiredInvitation").GetBoolean(), Is.True);
    }

    [Test]
    public async Task InvitationPagination_UsesIdTieBreakForDuplicateCreatedAtAcrossPages()
    {
        // Arrange
        var trainer = await SeedTrainerAsync("postgres-pagination-invitation-tie-trainer", "postgres-pagination-invitation-tie-trainer@example.com");
        var trainee = await SeedUserAsync("PostgreSQL invitation tie profile", "postgres-invitation-tie@example.com");
        var createdAt = new DateTimeOffset(2025, 9, 9, 8, 0, 0, TimeSpan.Zero);
        var firstById = new Id<TrainerInvitation>(new Guid("16000000-0000-0000-0000-000000000001"));
        var secondById = new Id<TrainerInvitation>(new Guid("26000000-0000-0000-0000-000000000001"));
        var thirdById = new Id<TrainerInvitation>(new Guid("f6000000-0000-0000-0000-000000000001"));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerInvitations.AddRange(
                CreateInvitation(thirdById, trainer.Id, trainee.Id, trainee.Email, "POSTGRES-TIE-THIRD", createdAt),
                CreateInvitation(secondById, trainer.Id, trainee.Id, trainee.Email, "POSTGRES-TIE-SECOND", createdAt),
                CreateInvitation(firstById, trainer.Id, trainee.Id, trainee.Email, "POSTGRES-TIE-FIRST", createdAt));
            await db.SaveChangesAsync();
        }

        await AuthenticateAsAsync(trainer);

        // Act
        var firstPageResponse = await Client.PostAsJsonAsync("/api/trainer/invitations/paginated", new { page = 1, pageSize = 2 });
        var secondPageResponse = await Client.PostAsJsonAsync("/api/trainer/invitations/paginated", new { page = 2, pageSize = 2 });

        // Assert
        Assert.That(firstPageResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(secondPageResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var firstPage = JsonDocument.Parse(await firstPageResponse.Content.ReadAsStringAsync());
        using var secondPage = JsonDocument.Parse(await secondPageResponse.Content.ReadAsStringAsync());
        Assert.That(firstPage.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("_id").GetString()), Is.EqualTo(new[] { firstById.ToString(), secondById.ToString() }));
        Assert.That(secondPage.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("_id").GetString()), Is.EqualTo(new[] { thirdById.ToString() }));
    }

    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name, email);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(db, trainer.Id));
        await db.SaveChangesAsync();

        return trainer;
    }

    private async Task AuthenticateAsAsync(User user)
    {
        var response = await Client.PostAsJsonAsync("/api/login", new { name = user.Name, password = "password123" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.RootElement.GetProperty("token").GetString());
    }

    private static TrainerTraineeLink CreateLink(string id, Id<User> trainerId, Id<User> traineeId, DateTimeOffset linkedAt) => new()
    {
        Id = new Id<TrainerTraineeLink>(new Guid(id)),
        TrainerId = trainerId,
        TraineeId = traineeId,
        CreatedAt = linkedAt,
        UpdatedAt = linkedAt
    };

    private static User CreateUser(string id, string name, string email) => new()
    {
        Id = new Id<User>(new Guid(id)),
        Name = name,
        Email = new Email(email),
        PreferredLanguage = "en-US",
        PreferredTimeZone = "UTC"
    };

    private static TrainerInvitation CreateInvitation(
        Id<TrainerInvitation> id,
        Id<User> trainerId,
        Id<User>? traineeId,
        string inviteeEmail,
        string code,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt = null) => new()
        {
            Id = id,
            TrainerId = trainerId,
            TraineeId = traineeId,
            InviteeEmail = inviteeEmail,
            Code = code,
            Status = TrainerInvitationStatus.Pending,
            ExpiresAt = expiresAt ?? new DateTimeOffset(2030, 6, 6, 8, 0, 0, TimeSpan.Zero),
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
}
