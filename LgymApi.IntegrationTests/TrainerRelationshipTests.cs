using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Application.Notifications;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using LgymApi.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class TrainerRelationshipTests : IntegrationTestBase
{
    [SetUp]
    public void ResetEmailCapture()
    {
        Factory.EmailSender.SentMessages.Clear();
    }

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
    public async Task CreateInvitation_CreatesPendingEmailNotificationLog()
    {
        var trainer = await SeedTrainerAsync("trainer-email-log", "trainer-email-log@example.com");
        var trainee = await SeedUserAsync(name: "trainee-email-log", email: "trainee-email-log@example.com", password: "password123");
        SetAuthorizationHeader(trainer.Id);

        var response = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitation = await response.Content.ReadFromJsonAsync<TrainerInvitationResponse>();
        invitation.Should().NotBeNull();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await db.EmailNotificationLogs.FirstOrDefaultAsync(x => x.CorrelationId == Guid.Parse(invitation!.Id));
        log.Should().NotBeNull();
        log!.Status.Should().Be(EmailNotificationStatus.Pending);
        log.RecipientEmail.Should().Be("trainee-email-log@example.com");
    }

    [Test]
    public async Task InvitationEmailJob_ProcessesPendingNotification_AndMarksSent()
    {
        var trainer = await SeedTrainerAsync("trainer-email-job", "trainer-email-job@example.com", preferredLanguage: "en");
        var trainee = await SeedUserAsync(name: "trainee-email-job", email: "trainee-email-job@example.com", password: "password123");
        SetAuthorizationHeader(trainer.Id);

        var response = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });
        var invitation = await response.Content.ReadFromJsonAsync<TrainerInvitationResponse>();
        invitation.Should().NotBeNull();

        Guid notificationId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.EmailNotificationLogs.FirstAsync(x => x.CorrelationId == Guid.Parse(invitation!.Id));
            notificationId = log.Id;
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IInvitationEmailJobHandler>();
            await handler.ProcessAsync(notificationId);
        }

        using (var verifyScope = Factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.EmailNotificationLogs.FirstAsync(x => x.Id == notificationId);
            log.Status.Should().Be(EmailNotificationStatus.Sent);
            log.SentAt.Should().NotBeNull();
            log.Attempts.Should().BeGreaterThanOrEqualTo(1);
        }

        Factory.EmailSender.SentMessages.Should().ContainSingle();
        Factory.EmailSender.SentMessages[0].To.Should().Be("trainee-email-job@example.com");
    }

    [Test]
    public async Task InvitationEmailJob_UsesTrainerLanguageTemplate_WhenTrainerIsPolish()
    {
        var trainer = await SeedTrainerAsync("trainer-email-pl", "trainer-email-pl@example.com", preferredLanguage: "pl");
        var trainee = await SeedUserAsync(name: "trainee-email-pl", email: "trainee-email-pl@example.com", password: "password123");
        SetAuthorizationHeader(trainer.Id);

        var response = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });
        var invitation = await response.Content.ReadFromJsonAsync<TrainerInvitationResponse>();

        Guid notificationId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.EmailNotificationLogs.FirstAsync(x => x.CorrelationId == Guid.Parse(invitation!.Id));
            notificationId = log.Id;
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IInvitationEmailJobHandler>();
            await handler.ProcessAsync(notificationId);
        }

        Factory.EmailSender.SentMessages.Should().ContainSingle();
        Factory.EmailSender.SentMessages[0].Subject.Should().Contain("Zaproszenie");
        Factory.EmailSender.SentMessages[0].Body.Should().Contain("Akceptuj");
    }

    [Test]
    public async Task InvitationEmailJob_DoesNotResend_WhenAlreadySent()
    {
        var trainer = await SeedTrainerAsync("trainer-email-idempotent", "trainer-email-idempotent@example.com");
        var trainee = await SeedUserAsync(name: "trainee-email-idempotent", email: "trainee-email-idempotent@example.com", password: "password123");
        SetAuthorizationHeader(trainer.Id);

        var response = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });
        var invitation = await response.Content.ReadFromJsonAsync<TrainerInvitationResponse>();

        Guid notificationId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.EmailNotificationLogs.FirstAsync(x => x.CorrelationId == Guid.Parse(invitation!.Id));
            notificationId = log.Id;
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IInvitationEmailJobHandler>();
            await handler.ProcessAsync(notificationId);
            await handler.ProcessAsync(notificationId);
        }

        Factory.EmailSender.SentMessages.Should().ContainSingle();
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

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logsCount = await db.EmailNotificationLogs
            .CountAsync(x => x.CorrelationId == Guid.Parse(firstBody.Id));
        logsCount.Should().Be(1);
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
    public async Task GetDashboardTrainees_AppliesOwnershipIsolation()
    {
        var trainerA = await SeedTrainerAsync("trainer-dashboard-a", "trainer-dashboard-a@example.com");
        var trainerB = await SeedTrainerAsync("trainer-dashboard-b", "trainer-dashboard-b@example.com");
        var traineeLinkedToA = await SeedUserAsync(name: "trainee-owned-link", email: "trainee-owned-link@example.com", password: "password123");
        var traineeInvitedByA = await SeedUserAsync(name: "trainee-owned-invite", email: "trainee-owned-invite@example.com", password: "password123");
        var traineeLinkedToB = await SeedUserAsync(name: "trainee-foreign", email: "trainee-foreign@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.AddRange(
                new TrainerTraineeLink
                {
                    Id = Guid.NewGuid(),
                    TrainerId = trainerA.Id,
                    TraineeId = traineeLinkedToA.Id
                },
                new TrainerTraineeLink
                {
                    Id = Guid.NewGuid(),
                    TrainerId = trainerB.Id,
                    TraineeId = traineeLinkedToB.Id
                });

            db.TrainerInvitations.Add(new TrainerInvitation
            {
                Id = Guid.NewGuid(),
                TrainerId = trainerA.Id,
                TraineeId = traineeInvitedByA.Id,
                Code = "OWNERSHIPA001",
                Status = TrainerInvitationStatus.Pending,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(3)
            });

            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainerA.Id);
        var response = await Client.GetAsync("/api/trainer/trainees?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TrainerDashboardTraineesResponse>();
        body.Should().NotBeNull();
        body!.Total.Should().Be(2);
        body.Items.Select(x => x.Id).Should().BeEquivalentTo(new[]
        {
            traineeLinkedToA.Id.ToString(),
            traineeInvitedByA.Id.ToString()
        });
        body.Items.Should().NotContain(x => x.Id == traineeLinkedToB.Id.ToString());
    }

    [Test]
    public async Task GetDashboardTrainees_AppliesSearchFilterSortAndStatusFlags()
    {
        var trainer = await SeedTrainerAsync("trainer-dashboard-filter", "trainer-dashboard-filter@example.com");
        var linked = await SeedUserAsync(name: "Alpha Linked", email: "alpha-linked@example.com", password: "password123");
        var pending = await SeedUserAsync(name: "Beta Pending", email: "beta-pending@example.com", password: "password123");
        var expired = await SeedUserAsync(name: "Gamma Expired", email: "gamma-expired@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Guid.NewGuid(),
                TrainerId = trainer.Id,
                TraineeId = linked.Id
            });

            db.TrainerInvitations.AddRange(
                new TrainerInvitation
                {
                    Id = Guid.NewGuid(),
                    TrainerId = trainer.Id,
                    TraineeId = pending.Id,
                    Code = "STATUSPENDING1",
                    Status = TrainerInvitationStatus.Pending,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(2)
                },
                new TrainerInvitation
                {
                    Id = Guid.NewGuid(),
                    TrainerId = trainer.Id,
                    TraineeId = expired.Id,
                    Code = "STATUSEXPIRED1",
                    Status = TrainerInvitationStatus.Pending,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10)
                });

            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);

        var filtered = await Client.GetAsync("/api/trainer/trainees?search=gamma&status=InvitationExpired&sortBy=name&sortDirection=desc&page=1&pageSize=10");
        filtered.StatusCode.Should().Be(HttpStatusCode.OK);
        var filteredBody = await filtered.Content.ReadFromJsonAsync<TrainerDashboardTraineesResponse>();

        filteredBody.Should().NotBeNull();
        filteredBody!.Total.Should().Be(1);
        filteredBody.Items.Should().ContainSingle();
        filteredBody.Items[0].Id.Should().Be(expired.Id.ToString());
        filteredBody.Items[0].Status.Should().Be("InvitationExpired");
        filteredBody.Items[0].HasExpiredInvitation.Should().BeTrue();
        filteredBody.Items[0].HasPendingInvitation.Should().BeFalse();
        filteredBody.Items[0].IsLinked.Should().BeFalse();

        var full = await Client.GetAsync("/api/trainer/trainees?sortBy=name&sortDirection=asc&page=1&pageSize=10");
        full.StatusCode.Should().Be(HttpStatusCode.OK);
        var fullBody = await full.Content.ReadFromJsonAsync<TrainerDashboardTraineesResponse>();
        fullBody.Should().NotBeNull();
        fullBody!.Items.Select(x => x.Name).Should().Equal("Alpha Linked", "Beta Pending", "Gamma Expired");

        var linkedItem = fullBody.Items.Single(x => x.Id == linked.Id.ToString());
        linkedItem.Status.Should().Be("Linked");
        linkedItem.IsLinked.Should().BeTrue();

        var pendingItem = fullBody.Items.Single(x => x.Id == pending.Id.ToString());
        pendingItem.Status.Should().Be("InvitationPending");
        pendingItem.HasPendingInvitation.Should().BeTrue();
    }

    [Test]
    public async Task GetDashboardTrainees_AppliesPagination_ForLargeResultSet()
    {
        var trainer = await SeedTrainerAsync("trainer-dashboard-pagination", "trainer-dashboard-pagination@example.com");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 35; i++)
            {
                var trainee = await SeedUserAsync(name: $"trainee-{i:00}", email: $"trainee-{i:00}@example.com", password: "password123");
                db.TrainerInvitations.Add(new TrainerInvitation
                {
                    Id = Guid.NewGuid(),
                    TrainerId = trainer.Id,
                    TraineeId = trainee.Id,
                    Code = $"PAGE{i:000}CODE",
                    Status = TrainerInvitationStatus.Pending,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
                });
            }

            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var response = await Client.GetAsync("/api/trainer/trainees?sortBy=name&sortDirection=asc&page=2&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TrainerDashboardTraineesResponse>();
        body.Should().NotBeNull();
        body!.Total.Should().Be(35);
        body.Page.Should().Be(2);
        body.PageSize.Should().Be(10);
        body.Items.Count.Should().Be(10);
        body.Items.First().Name.Should().Be("trainee-10");
        body.Items.Last().Name.Should().Be("trainee-19");
    }

    [Test]
    public async Task GetDashboardTrainees_WithInvalidSortBy_ReturnsBadRequestWithResourceMessage()
    {
        var trainer = await SeedTrainerAsync("trainer-dashboard-invalid-sort", "trainer-dashboard-invalid-sort@example.com");
        SetAuthorizationHeader(trainer.Id);

        var response = await Client.GetAsync("/api/trainer/trainees?sortBy=unknown");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseBody = await response.Content.ReadAsStringAsync();
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            responseBody.Should().Contain(Messages.DashboardSortByInvalid);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Test]
    public async Task GetDashboardTrainees_WithInvalidStatus_ReturnsBadRequestWithResourceMessage()
    {
        var trainer = await SeedTrainerAsync("trainer-dashboard-invalid-status", "trainer-dashboard-invalid-status@example.com");
        SetAuthorizationHeader(trainer.Id);

        var response = await Client.GetAsync("/api/trainer/trainees?status=NotAStatus");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseBody = await response.Content.ReadAsStringAsync();
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            responseBody.Should().Contain(Messages.DashboardStatusInvalid);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Test]
    public async Task GetDashboardTrainees_WithInvalidPage_ReturnsBadRequestWithResourceMessage()
    {
        var trainer = await SeedTrainerAsync("trainer-dashboard-invalid-page", "trainer-dashboard-invalid-page@example.com");
        SetAuthorizationHeader(trainer.Id);

        var response = await Client.GetAsync("/api/trainer/trainees?page=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseBody = await response.Content.ReadAsStringAsync();
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            responseBody.Should().Contain(Messages.DashboardPageMustBeGreaterThanZero);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
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

    private async Task<User> SeedTrainerAsync(string name, string email, string preferredLanguage = "en-US")
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
        }

        var trainerToUpdate = await db.Users.FirstAsync(u => u.Id == trainer.Id);
        trainerToUpdate.PreferredLanguage = preferredLanguage;
        await db.SaveChangesAsync();

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

    private sealed class TrainerDashboardTraineesResponse
    {
        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("items")]
        public List<TrainerDashboardTraineeResponse> Items { get; set; } = [];
    }

    private sealed class TrainerDashboardTraineeResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("isLinked")]
        public bool IsLinked { get; set; }

        [JsonPropertyName("hasPendingInvitation")]
        public bool HasPendingInvitation { get; set; }

        [JsonPropertyName("hasExpiredInvitation")]
        public bool HasExpiredInvitation { get; set; }
    }

    private sealed class MessageResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }
}
