using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class TrainerRelationshipTests : IntegrationTestBase
{
    [Test]
    public async Task CreateInvitation_AsTrainer_CreatesPendingInvitation()
    {
        var trainer = await SeedTrainerAsync("trainer-invite", "trainer-invite@example.com");
        var trainee = await SeedUserAsync(name: "trainee-invite", email: "trainee-invite@example.com", password: "password123");
        SetAuthorizationHeader(trainer.Id);

        var response = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TrainerInvitationResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("Pending");
        body.TraineeId.Should().Be(trainee.Id.ToString());
        body.Code.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task CreateInvitation_RepeatedRequest_ReturnsExistingPendingInvitation()
    {
        var trainer = await SeedTrainerAsync("trainer-repeat", "trainer-repeat@example.com");
        var trainee = await SeedUserAsync(name: "trainee-repeat", email: "trainee-repeat@example.com", password: "password123");
        SetAuthorizationHeader(trainer.Id);

        var first = await Client.PostAsJsonAsync("/api/trainer/invitations", new { traineeId = trainee.Id.ToString() });
        var firstBody = await first.Content.ReadFromJsonAsync<TrainerInvitationResponse>();

        var second = await Client.PostAsJsonAsync("/api/trainer/invitations", new { traineeId = trainee.Id.ToString() });
        var secondBody = await second.Content.ReadFromJsonAsync<TrainerInvitationResponse>();

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        firstBody.Should().NotBeNull();
        secondBody.Should().NotBeNull();
        secondBody!.Id.Should().Be(firstBody!.Id);
        secondBody.Code.Should().Be(firstBody.Code);
    }

    [Test]
    public async Task GetInvitations_AsTrainer_ReturnsCreatedInvitations()
    {
        var trainer = await SeedTrainerAsync("trainer-list", "trainer-list@example.com");
        var traineeA = await SeedUserAsync(name: "trainee-list-a", email: "trainee-list-a@example.com", password: "password123");
        var traineeB = await SeedUserAsync(name: "trainee-list-b", email: "trainee-list-b@example.com", password: "password123");
        SetAuthorizationHeader(trainer.Id);

        await Client.PostAsJsonAsync("/api/trainer/invitations", new { traineeId = traineeA.Id.ToString() });
        await Client.PostAsJsonAsync("/api/trainer/invitations", new { traineeId = traineeB.Id.ToString() });

        var response = await Client.GetAsync("/api/trainer/invitations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<TrainerInvitationResponse>>();
        body.Should().NotBeNull();
        body!.Count.Should().Be(2);
        body.Select(x => x.TraineeId).Should().BeEquivalentTo(new[] { traineeA.Id.ToString(), traineeB.Id.ToString() });
    }

    [Test]
    public async Task AcceptInvitation_AsTrainee_CreatesLink()
    {
        var trainer = await SeedTrainerAsync("trainer-accept", "trainer-accept@example.com");
        var trainee = await SeedUserAsync(name: "trainee-accept", email: "trainee-accept@example.com", password: "password123");

        SetAuthorizationHeader(trainer.Id);
        var createResponse = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });
        var invitation = await createResponse.Content.ReadFromJsonAsync<TrainerInvitationResponse>();

        SetAuthorizationHeader(trainee.Id);
        var acceptResponse = await Client.PostAsync($"/api/trainee/invitations/{invitation!.Id}/accept", null);

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var link = await db.TrainerTraineeLinks.FirstOrDefaultAsync(x => x.TrainerId == trainer.Id && x.TraineeId == trainee.Id);
        link.Should().NotBeNull();
    }

    [Test]
    public async Task RejectInvitation_AsTrainee_ChangesInvitationStatusToRejected()
    {
        var trainer = await SeedTrainerAsync("trainer-reject", "trainer-reject@example.com");
        var trainee = await SeedUserAsync(name: "trainee-reject", email: "trainee-reject@example.com", password: "password123");

        SetAuthorizationHeader(trainer.Id);
        var createResponse = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });
        var invitation = await createResponse.Content.ReadFromJsonAsync<TrainerInvitationResponse>();

        SetAuthorizationHeader(trainee.Id);
        var rejectResponse = await Client.PostAsync($"/api/trainee/invitations/{invitation!.Id}/reject", null);
        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invitationEntity = await db.TrainerInvitations.FirstAsync(i => i.Id == Guid.Parse(invitation.Id));
        invitationEntity.Status.ToString().Should().Be("Rejected");
        invitationEntity.RespondedAt.Should().NotBeNull();
    }

    [Test]
    public async Task AcceptInvitation_WhenExpired_ReturnsBadRequestAndMarksInvitationAsExpired()
    {
        var trainer = await SeedTrainerAsync("trainer-expired", "trainer-expired@example.com");
        var trainee = await SeedUserAsync(name: "trainee-expired", email: "trainee-expired@example.com", password: "password123");

        var invitationId = Guid.NewGuid();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerInvitations.Add(new TrainerInvitation
            {
                Id = invitationId,
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                Code = "EXPIRED123456",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainee.Id);
        var response = await Client.PostAsync($"/api/trainee/invitations/{invitationId}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        body.Should().NotBeNull();
        body!.Message.Should().Be("Invitation has expired.");

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invitation = await verifyDb.TrainerInvitations.FirstAsync(i => i.Id == invitationId);
        invitation.Status.ToString().Should().Be("Expired");
        invitation.RespondedAt.Should().NotBeNull();
    }

    [Test]
    public async Task CreateInvitation_AsRegularUser_ReturnsForbidden()
    {
        var regularUser = await SeedUserAsync(name: "regular-invite", email: "regular-invite@example.com", password: "password123");
        var trainee = await SeedUserAsync(name: "regular-target", email: "regular-target@example.com", password: "password123");
        SetAuthorizationHeader(regularUser.Id);

        var response = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task UnlinkByTrainer_RemovesExistingLink()
    {
        var trainer = await SeedTrainerAsync("trainer-unlink", "trainer-unlink@example.com");
        var trainee = await SeedUserAsync(name: "trainee-unlink", email: "trainee-unlink@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Guid.NewGuid(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var response = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var link = await verifyDb.TrainerTraineeLinks.FirstOrDefaultAsync(x => x.TrainerId == trainer.Id && x.TraineeId == trainee.Id);
        link.Should().BeNull();
    }

    [Test]
    public async Task DetachByTrainee_RemovesExistingLink()
    {
        var trainer = await SeedTrainerAsync("trainer-detach", "trainer-detach@example.com");
        var trainee = await SeedUserAsync(name: "trainee-detach", email: "trainee-detach@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Guid.NewGuid(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainee.Id);
        var response = await Client.PostAsync("/api/trainee/trainer/detach", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var link = await verifyDb.TrainerTraineeLinks.FirstOrDefaultAsync(x => x.TrainerId == trainer.Id && x.TraineeId == trainee.Id);
        link.Should().BeNull();
    }

    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name: name, email: email, password: "password123");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var alreadyLinked = await db.UserRoles.AnyAsync(ur => ur.UserId == trainer.Id && ur.RoleId == AppDbContext.TrainerRoleSeedId);
        if (!alreadyLinked)
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = trainer.Id,
                RoleId = AppDbContext.TrainerRoleSeedId
            });
            await db.SaveChangesAsync();
        }

        return trainer;
    }

    private sealed class TrainerInvitationResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("traineeId")]
        public string TraineeId { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    private sealed class MessageResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }
}
