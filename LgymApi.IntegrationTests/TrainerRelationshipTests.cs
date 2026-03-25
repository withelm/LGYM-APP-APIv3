using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.BackgroundWorker.Common.Notifications;
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
    private static readonly double[] ExpectedMainRecordWeights = [140d, 150d];
    [SetUp]
    public void ResetEmailCapture()
    {
        Factory.EmailSender.Reset();
    }

    [Test]
    public async Task CreateInvitation_AsTrainer_CreatesPendingInvitation()
    {
        var trainer = await SeedTrainerAsync("trainer-invite", "trainer-invite@example.com");
        var trainee = await SeedUserAsync(name: "trainee-invite", email: "trainee-invite@example.com", password: "password123");
        SetAuthorizationHeader((Guid)trainer.Id);

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
        SetAuthorizationHeader((Guid)trainer.Id);

        var response = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Process pending commands to trigger handler execution
        await ProcessPendingCommandsAsync();

        var invitation = await response.Content.ReadFromJsonAsync<TrainerInvitationResponse>();
        invitation.Should().NotBeNull();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await db.NotificationMessages.FirstOrDefaultAsync(x => x.CorrelationId == Guid.Parse(invitation!.Id));
        log.Should().NotBeNull();
        log!.Status.Should().Be(EmailNotificationStatus.Pending);
        log.Recipient.Value.Should().Be("trainee-email-log@example.com");
    }

    [Test]
    public async Task InvitationEmailJob_ProcessesPendingNotification_AndMarksSent()
    {
        var trainer = await SeedTrainerAsync("trainer-email-job", "trainer-email-job@example.com", preferredLanguage: "en");
        var trainee = await SeedUserAsync(name: "trainee-email-job", email: "trainee-email-job@example.com", password: "password123");
        SetAuthorizationHeader((Guid)trainer.Id);

        var response = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });
        var invitation = await response.Content.ReadFromJsonAsync<TrainerInvitationResponse>();

        // Process pending commands to trigger handler execution
        await ProcessPendingCommandsAsync();
        invitation.Should().NotBeNull();

        Guid notificationId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.NotificationMessages.FirstAsync(x => x.CorrelationId == Guid.Parse(invitation!.Id));
            notificationId = (Guid)log.Id;
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IEmailJobHandler>();
            await handler.ProcessAsync(notificationId);
        }

        using (var verifyScope = Factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.NotificationMessages.FirstAsync(x => x.Id == (Domain.ValueObjects.Id<NotificationMessage>)notificationId);
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
        SetAuthorizationHeader((Guid)trainer.Id);

        var response = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });
        var invitation = await response.Content.ReadFromJsonAsync<TrainerInvitationResponse>();

        // Process pending commands to trigger handler execution
        await ProcessPendingCommandsAsync();

        Guid notificationId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.NotificationMessages.FirstAsync(x => x.CorrelationId == Guid.Parse(invitation!.Id));
            notificationId = (Guid)log.Id;
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IEmailJobHandler>();
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
        SetAuthorizationHeader((Guid)trainer.Id);

        var response = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });
        var invitation = await response.Content.ReadFromJsonAsync<TrainerInvitationResponse>();

        // Process pending commands to trigger handler execution
        await ProcessPendingCommandsAsync();

        Guid notificationId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.NotificationMessages.FirstAsync(x => x.CorrelationId == Guid.Parse(invitation!.Id));
            notificationId = (Guid)log.Id;
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IEmailJobHandler>();
            await handler.ProcessAsync(notificationId);
            await handler.ProcessAsync(notificationId);
        }

        Factory.EmailSender.SentMessages.Should().ContainSingle();
    }

    [Test]
    public async Task InvitationEmailJob_WhenSenderFails_MarksNotificationAsFailed()
    {
        var trainer = await SeedTrainerAsync("trainer-email-fail", "trainer-email-fail@example.com");
        var trainee = await SeedUserAsync(name: "trainee-email-fail", email: "trainee-email-fail@example.com", password: "password123");
        SetAuthorizationHeader((Guid)trainer.Id);

        var response = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });
        var invitation = await response.Content.ReadFromJsonAsync<TrainerInvitationResponse>();

        // Process pending commands to trigger handler execution
        await ProcessPendingCommandsAsync();

        Guid notificationId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.NotificationMessages.FirstAsync(x => x.CorrelationId == Guid.Parse(invitation!.Id));
            notificationId = (Guid)log.Id;
        }

        Factory.EmailSender.FailuresRemaining = 1;

        using (var scope = Factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IEmailJobHandler>();
            Assert.ThrowsAsync<InvalidOperationException>(() => handler.ProcessAsync(notificationId));
        }

        using (var verifyScope = Factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.NotificationMessages.FirstAsync(x => x.Id == (Domain.ValueObjects.Id<NotificationMessage>)notificationId);
            log.Status.Should().Be(EmailNotificationStatus.Failed);
            log.Attempts.Should().Be(1);
            log.LastError.Should().NotBeNullOrWhiteSpace();
            log.SentAt.Should().BeNull();
        }

        Factory.EmailSender.SentMessages.Should().BeEmpty();
    }

    [Test]
    public async Task InvitationEmailJob_AfterRetry_SetsSentStatusAndKeepsAuditTrail()
    {
        var trainer = await SeedTrainerAsync("trainer-email-retry", "trainer-email-retry@example.com");
        var trainee = await SeedUserAsync(name: "trainee-email-retry", email: "trainee-email-retry@example.com", password: "password123");
        SetAuthorizationHeader((Guid)trainer.Id);

        var response = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });
        var invitation = await response.Content.ReadFromJsonAsync<TrainerInvitationResponse>();

        // Process pending commands to trigger handler execution
        await ProcessPendingCommandsAsync();

        Guid notificationId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.NotificationMessages.FirstAsync(x => x.CorrelationId == Guid.Parse(invitation!.Id));
            notificationId = (Guid)log.Id;
        }

        Factory.EmailSender.FailuresRemaining = 1;

        using (var scope = Factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IEmailJobHandler>();
            Assert.ThrowsAsync<InvalidOperationException>(() => handler.ProcessAsync(notificationId));
            await handler.ProcessAsync(notificationId);
        }

        using (var verifyScope = Factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.NotificationMessages.FirstAsync(x => x.Id == (Domain.ValueObjects.Id<NotificationMessage>)notificationId);
            log.Status.Should().Be(EmailNotificationStatus.Sent);
            log.Attempts.Should().Be(2);
            log.SentAt.Should().NotBeNull();
            log.LastError.Should().BeNull();
            log.LastAttemptAt.Should().NotBeNull();
        }

        Factory.EmailSender.SentMessages.Should().ContainSingle();
    }

    [Test]
    public async Task CreateInvitation_RepeatedRequest_ReturnsExistingPendingInvitation()
    {
        var trainer = await SeedTrainerAsync("trainer-repeat", "trainer-repeat@example.com");
        var trainee = await SeedUserAsync(name: "trainee-repeat", email: "trainee-repeat@example.com", password: "password123");
        SetAuthorizationHeader((Guid)trainer.Id);

        var first = await Client.PostAsJsonAsync("/api/trainer/invitations", new { traineeId = trainee.Id.ToString() });
        var firstBody = await first.Content.ReadFromJsonAsync<TrainerInvitationResponse>();

        // Process pending commands to trigger handler execution
        await ProcessPendingCommandsAsync();

        var second = await Client.PostAsJsonAsync("/api/trainer/invitations", new { traineeId = trainee.Id.ToString() });
        var secondBody = await second.Content.ReadFromJsonAsync<TrainerInvitationResponse>();

        // Process pending commands to trigger handler execution
        await ProcessPendingCommandsAsync();

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        firstBody.Should().NotBeNull();
        secondBody.Should().NotBeNull();
        secondBody!.Id.Should().Be(firstBody!.Id);
        secondBody.Code.Should().Be(firstBody.Code);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logsCount = await db.NotificationMessages
            .CountAsync(x => x.CorrelationId == Guid.Parse(firstBody.Id));
        logsCount.Should().Be(1);
    }

    [Test]
    public async Task GetInvitations_AsTrainer_ReturnsCreatedInvitations()
    {
        var trainer = await SeedTrainerAsync("trainer-list", "trainer-list@example.com");
        var traineeA = await SeedUserAsync(name: "trainee-list-a", email: "trainee-list-a@example.com", password: "password123");
        var traineeB = await SeedUserAsync(name: "trainee-list-b", email: "trainee-list-b@example.com", password: "password123");
        SetAuthorizationHeader((Guid)trainer.Id);

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
                     Id = (Domain.ValueObjects.Id<TrainerTraineeLink>)Guid.NewGuid(),
                     TrainerId = (Domain.ValueObjects.Id<User>)trainerA.Id,
                     TraineeId = (Domain.ValueObjects.Id<User>)traineeLinkedToA.Id
                 },
                 new TrainerTraineeLink
                 {
                     Id = (Domain.ValueObjects.Id<TrainerTraineeLink>)Guid.NewGuid(),
                     TrainerId = (Domain.ValueObjects.Id<User>)trainerB.Id,
                     TraineeId = (Domain.ValueObjects.Id<User>)traineeLinkedToB.Id
                 });

             db.TrainerInvitations.Add(new TrainerInvitation
             {
                 Id = (Domain.ValueObjects.Id<TrainerInvitation>)Guid.NewGuid(),
                 TrainerId = (Domain.ValueObjects.Id<User>)trainerA.Id,
                 TraineeId = (Domain.ValueObjects.Id<User>)traineeInvitedByA.Id,
                Code = "OWNERSHIPA001",
                Status = TrainerInvitationStatus.Pending,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(3)
            });

            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader((Guid)trainerA.Id);
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
                 Id = (Domain.ValueObjects.Id<TrainerTraineeLink>)Guid.NewGuid(),
                 TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                 TraineeId = (Domain.ValueObjects.Id<User>)linked.Id
             });

             db.TrainerInvitations.AddRange(
                 new TrainerInvitation
                 {
                     Id = (Domain.ValueObjects.Id<TrainerInvitation>)Guid.NewGuid(),
                     TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                     TraineeId = (Domain.ValueObjects.Id<User>)pending.Id,
                     Code = "STATUSPENDING1",
                     Status = TrainerInvitationStatus.Pending,
                     ExpiresAt = DateTimeOffset.UtcNow.AddDays(2)
                 },
                 new TrainerInvitation
                 {
                     Id = (Domain.ValueObjects.Id<TrainerInvitation>)Guid.NewGuid(),
                     TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                     TraineeId = (Domain.ValueObjects.Id<User>)expired.Id,
                     Code = "STATUSEXPIRED1",
                     Status = TrainerInvitationStatus.Pending,
                     ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10)
                 });

            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader((Guid)trainer.Id);

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
                 Id = (Domain.ValueObjects.Id<TrainerInvitation>)Guid.NewGuid(),
                 TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                 TraineeId = (Domain.ValueObjects.Id<User>)trainee.Id,
                    Code = $"PAGE{i:000}CODE",
                    Status = TrainerInvitationStatus.Pending,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
                });
            }

            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader((Guid)trainer.Id);
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
    public async Task GetDashboardTrainees_SortByStatus_OrdersByComputedRelationshipStatus()
    {
        var trainer = await SeedTrainerAsync("trainer-dashboard-sort-status", "trainer-dashboard-sort-status@example.com");
        var linked = await SeedUserAsync(name: "Zulu Linked", email: "zulu-linked@example.com", password: "password123");
        var pending = await SeedUserAsync(name: "Alpha Pending", email: "alpha-pending@example.com", password: "password123");
        var expired = await SeedUserAsync(name: "Mike Expired", email: "mike-expired@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

             db.TrainerTraineeLinks.Add(new TrainerTraineeLink
             {
                 Id = (Domain.ValueObjects.Id<TrainerTraineeLink>)Guid.NewGuid(),
                 TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                 TraineeId = (Domain.ValueObjects.Id<User>)linked.Id
            });

             db.TrainerInvitations.AddRange(
                 new TrainerInvitation
                 {
                     Id = (Domain.ValueObjects.Id<TrainerInvitation>)Guid.NewGuid(),
                     TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                     TraineeId = (Domain.ValueObjects.Id<User>)pending.Id,
                    Code = "STATUSSORTPEND",
                    Status = TrainerInvitationStatus.Pending,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(2)
                },
                 new TrainerInvitation
                 {
                     Id = (Domain.ValueObjects.Id<TrainerInvitation>)Guid.NewGuid(),
                     TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                     TraineeId = (Domain.ValueObjects.Id<User>)expired.Id,
                    Code = "STATUSSORTEXP",
                    Status = TrainerInvitationStatus.Pending,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10)
                });

            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader((Guid)trainer.Id);
        var response = await Client.GetAsync("/api/trainer/trainees?sortBy=status&sortDirection=asc&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TrainerDashboardTraineesResponse>();
        body.Should().NotBeNull();
        body!.Items.Select(x => x.Name).Should().Equal("Zulu Linked", "Alpha Pending", "Mike Expired");
    }

    [Test]
    public async Task GetDashboardTrainees_SearchByName_IsCaseInsensitive()
    {
        var trainer = await SeedTrainerAsync("trainer-dashboard-search-case", "trainer-dashboard-search-case@example.com");
        var matching = await SeedUserAsync(name: "Omega Case", email: "person-one@example.com", password: "password123");
        var nonMatching = await SeedUserAsync(name: "Delta Other", email: "person-two@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

             db.TrainerInvitations.AddRange(
                 new TrainerInvitation
                 {
                     Id = (Domain.ValueObjects.Id<TrainerInvitation>)Guid.NewGuid(),
                     TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                     TraineeId = (Domain.ValueObjects.Id<User>)matching.Id,
                    Code = "SEARCHCASE001",
                    Status = TrainerInvitationStatus.Pending,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(2)
                },
                 new TrainerInvitation
                 {
                     Id = (Domain.ValueObjects.Id<TrainerInvitation>)Guid.NewGuid(),
                     TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                     TraineeId = (Domain.ValueObjects.Id<User>)nonMatching.Id,
                    Code = "SEARCHCASE002",
                    Status = TrainerInvitationStatus.Pending,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(2)
                });

            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader((Guid)trainer.Id);
        var response = await Client.GetAsync("/api/trainer/trainees?search=omega");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TrainerDashboardTraineesResponse>();
        body.Should().NotBeNull();
        body!.Total.Should().Be(1);
        body.Items.Should().ContainSingle(x => x.Id == matching.Id.ToString());
    }

    [TestCase("sortBy=unknown", nameof(Messages.DashboardSortByInvalid), TestName = "GetDashboardTrainees_WithInvalidSortBy_ReturnsBadRequestWithResourceMessage")]
    [TestCase("status=NotAStatus", nameof(Messages.DashboardStatusInvalid), TestName = "GetDashboardTrainees_WithInvalidStatus_ReturnsBadRequestWithResourceMessage")]
    [TestCase("page=0", nameof(Messages.DashboardPageRange), TestName = "GetDashboardTrainees_WithInvalidPage_ReturnsBadRequestWithResourceMessage")]
    [TestCase("page=21474838", nameof(Messages.DashboardPageRange), TestName = "GetDashboardTrainees_WithTooLargePage_ReturnsBadRequestWithResourceMessage")]
    [TestCase("sortDirection=invalid", nameof(Messages.DashboardSortDirectionInvalid), TestName = "GetDashboardTrainees_WithInvalidSortDirection_ReturnsBadRequestWithResourceMessage")]
    public async Task GetDashboardTrainees_WithInvalidQueryParam_ReturnsBadRequestWithResourceMessage(string queryString, string expectedMessagePropertyName)
    {
        var uniqueKey = queryString.Replace("=", "-").Replace("&", "-");
        var trainer = await SeedTrainerAsync($"trainer-dashboard-{uniqueKey}", $"trainer-dashboard-{Guid.NewGuid():N}@example.com");
        SetAuthorizationHeader((Guid)trainer.Id);

        var response = await Client.GetAsync($"/api/trainer/trainees?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseBody = await response.Content.ReadAsStringAsync();
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            var expectedMessage = typeof(Messages).GetProperty(expectedMessagePropertyName)!.GetValue(null) as string;
            responseBody.Should().Contain(expectedMessage!);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(101)]
    public async Task GetDashboardTrainees_WithInvalidPageSize_ReturnsBadRequestWithResourceMessage(int pageSize)
    {
        var trainer = await SeedTrainerAsync($"trainer-dashboard-invalid-pagesize-{pageSize}", $"trainer-dashboard-invalid-pagesize-{Guid.NewGuid():N}@example.com");
        SetAuthorizationHeader((Guid)trainer.Id);

        var response = await Client.GetAsync($"/api/trainer/trainees?pageSize={pageSize}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseBody = await response.Content.ReadAsStringAsync();
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            responseBody.Should().Contain(Messages.DashboardPageSizeRange);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Test]
    public async Task GetTraineeTrainingReads_AsLinkedTrainer_ReturnsOnlyOwnedTraineeData()
    {
        var trainer = await SeedTrainerAsync("trainer-read-training", "trainer-read-training@example.com");
        var trainee = await SeedUserAsync(name: "trainee-read-training", email: "trainee-read-training@example.com", password: "password123");
        var foreignTrainee = await SeedUserAsync(name: "trainee-read-training-foreign", email: "trainee-read-training-foreign@example.com", password: "password123");

        var trackedDay = DateTime.UtcNow.Date.AddDays(-1).AddHours(10);
        var otherDay = trackedDay.AddDays(1);

        Guid exerciseId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await LinkTrainerAndTraineeAsync(db, (Guid)trainer.Id, (Guid)trainee.Id);

             var traineePlan = new Plan { Id = (Domain.ValueObjects.Id<Plan>)Guid.NewGuid(), UserId = (Domain.ValueObjects.Id<User>)trainee.Id, Name = "Plan A" };
             var traineePlanDay = new PlanDay { Id = (Domain.ValueObjects.Id<PlanDay>)Guid.NewGuid(), PlanId = (Domain.ValueObjects.Id<Plan>)traineePlan.Id, Name = "Push Day" };
             var traineeGym = new Gym { Id = (Domain.ValueObjects.Id<Gym>)Guid.NewGuid(), UserId = (Domain.ValueObjects.Id<User>)trainee.Id, Name = "Gym A" };

             var foreignPlan = new Plan { Id = (Domain.ValueObjects.Id<Plan>)Guid.NewGuid(), UserId = (Domain.ValueObjects.Id<User>)foreignTrainee.Id, Name = "Plan B" };
             var foreignPlanDay = new PlanDay { Id = (Domain.ValueObjects.Id<PlanDay>)Guid.NewGuid(), PlanId = (Domain.ValueObjects.Id<Plan>)foreignPlan.Id, Name = "Pull Day" };
             var foreignGym = new Gym { Id = (Domain.ValueObjects.Id<Gym>)Guid.NewGuid(), UserId = (Domain.ValueObjects.Id<User>)foreignTrainee.Id, Name = "Gym B" };

             var exercise = new Exercise { Id = (Domain.ValueObjects.Id<Exercise>)Guid.NewGuid(), Name = "Bench Press", BodyPart = BodyParts.Chest };
             exerciseId = (Guid)exercise.Id;

            db.Plans.AddRange(traineePlan, foreignPlan);
            db.PlanDays.AddRange(traineePlanDay, foreignPlanDay);
            db.Gyms.AddRange(traineeGym, foreignGym);
            db.Exercises.Add(exercise);

             var traineeTrainingA = new Training
             {
                 Id = (Domain.ValueObjects.Id<Training>)Guid.NewGuid(),
                 UserId = (Domain.ValueObjects.Id<User>)trainee.Id,
                 TypePlanDayId = (Domain.ValueObjects.Id<PlanDay>)traineePlanDay.Id,
                 GymId = (Domain.ValueObjects.Id<Gym>)traineeGym.Id,
                 CreatedAt = trackedDay
             };
             var traineeTrainingB = new Training
             {
                 Id = (Domain.ValueObjects.Id<Training>)Guid.NewGuid(),
                 UserId = (Domain.ValueObjects.Id<User>)trainee.Id,
                 TypePlanDayId = (Domain.ValueObjects.Id<PlanDay>)traineePlanDay.Id,
                 GymId = (Domain.ValueObjects.Id<Gym>)traineeGym.Id,
                 CreatedAt = otherDay
             };
             var foreignTraining = new Training
             {
                 Id = (Domain.ValueObjects.Id<Training>)Guid.NewGuid(),
                 UserId = (Domain.ValueObjects.Id<User>)foreignTrainee.Id,
                 TypePlanDayId = (Domain.ValueObjects.Id<PlanDay>)foreignPlanDay.Id,
                 GymId = (Domain.ValueObjects.Id<Gym>)foreignGym.Id,
                 CreatedAt = otherDay.AddDays(1)
            };

            db.Trainings.AddRange(traineeTrainingA, traineeTrainingB, foreignTraining);

             var traineeScore = new ExerciseScore
             {
                 Id = (Domain.ValueObjects.Id<ExerciseScore>)Guid.NewGuid(),
                 ExerciseId = (Domain.ValueObjects.Id<Exercise>)exercise.Id,
                 UserId = (Domain.ValueObjects.Id<User>)trainee.Id,
                 Reps = 8,
                 Series = 1,
                 Weight = 80,
                 Unit = WeightUnits.Kilograms,
                 TrainingId = (Domain.ValueObjects.Id<Training>)traineeTrainingA.Id,
                 CreatedAt = trackedDay
             };

             db.ExerciseScores.Add(traineeScore);
             db.TrainingExerciseScores.Add(new TrainingExerciseScore
             {
                 Id = (Domain.ValueObjects.Id<TrainingExerciseScore>)Guid.NewGuid(),
                 TrainingId = (Domain.ValueObjects.Id<Training>)traineeTrainingA.Id,
                 ExerciseScoreId = (Domain.ValueObjects.Id<ExerciseScore>)traineeScore.Id
            });

            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader((Guid)trainer.Id);

        var datesResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/trainings/dates");
        datesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dates = await datesResponse.Content.ReadFromJsonAsync<List<DateTime>>();
        dates.Should().NotBeNull();
        dates!.Count.Should().Be(2);

        var byDateResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/trainings/by-date", new
        {
            createdAt = trackedDay
        });
        byDateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var byDate = await byDateResponse.Content.ReadFromJsonAsync<List<TrainingByDateDetailsResponse>>();
        byDate.Should().NotBeNull();
        byDate!.Count.Should().Be(1);
        byDate[0].Gym.Should().Be("Gym A");
        byDate[0].Exercises.Should().ContainSingle();
        byDate[0].Exercises[0].ExerciseDetails.Name.Should().Be("Bench Press");
        byDate[0].Exercises[0].ExerciseDetails.Id.Should().Be(exerciseId.ToString());
    }

    [Test]
    public async Task GetTraineeProgressReads_AsLinkedTrainer_ReturnsExerciseAndEloCharts()
    {
        var trainer = await SeedTrainerAsync("trainer-read-progress", "trainer-read-progress@example.com");
        var trainee = await SeedUserAsync(name: "trainee-read-progress", email: "trainee-read-progress@example.com", password: "password123");

        Guid exerciseId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await LinkTrainerAndTraineeAsync(db, (Guid)trainer.Id, (Guid)trainee.Id);

             var plan = new Plan { Id = (Domain.ValueObjects.Id<Plan>)Guid.NewGuid(), UserId = (Domain.ValueObjects.Id<User>)trainee.Id, Name = "Plan Progress" };
             var planDay = new PlanDay { Id = (Domain.ValueObjects.Id<PlanDay>)Guid.NewGuid(), PlanId = (Domain.ValueObjects.Id<Plan>)plan.Id, Name = "Progress Day" };
             var gym = new Gym { Id = (Domain.ValueObjects.Id<Gym>)Guid.NewGuid(), UserId = (Domain.ValueObjects.Id<User>)trainee.Id, Name = "Progress Gym" };
             var exercise = new Exercise { Id = (Domain.ValueObjects.Id<Exercise>)Guid.NewGuid(), Name = "Squat", BodyPart = BodyParts.Quads };
             exerciseId = (Guid)exercise.Id;

            db.Plans.Add(plan);
            db.PlanDays.Add(planDay);
            db.Gyms.Add(gym);
            db.Exercises.Add(exercise);

            var dayA = DateTimeOffset.UtcNow.AddDays(-3);
            var dayB = DateTimeOffset.UtcNow.AddDays(-1);
             var trainingA = new Training { Id = (Domain.ValueObjects.Id<Training>)Guid.NewGuid(), UserId = (Domain.ValueObjects.Id<User>)trainee.Id, TypePlanDayId = (Domain.ValueObjects.Id<PlanDay>)planDay.Id, GymId = (Domain.ValueObjects.Id<Gym>)gym.Id, CreatedAt = dayA };
             var trainingB = new Training { Id = (Domain.ValueObjects.Id<Training>)Guid.NewGuid(), UserId = (Domain.ValueObjects.Id<User>)trainee.Id, TypePlanDayId = (Domain.ValueObjects.Id<PlanDay>)planDay.Id, GymId = (Domain.ValueObjects.Id<Gym>)gym.Id, CreatedAt = dayB };
            db.Trainings.AddRange(trainingA, trainingB);

             var scoreA = new ExerciseScore
             {
                 Id = (Domain.ValueObjects.Id<ExerciseScore>)Guid.NewGuid(),
                 ExerciseId = (Domain.ValueObjects.Id<Exercise>)exercise.Id,
                 UserId = (Domain.ValueObjects.Id<User>)trainee.Id,
                 Reps = 5,
                 Series = 1,
                 Weight = 100,
                 Unit = WeightUnits.Kilograms,
                 TrainingId = (Domain.ValueObjects.Id<Training>)trainingA.Id,
                 CreatedAt = dayA
             };
             var scoreB = new ExerciseScore
             {
                 Id = (Domain.ValueObjects.Id<ExerciseScore>)Guid.NewGuid(),
                 ExerciseId = (Domain.ValueObjects.Id<Exercise>)exercise.Id,
                 UserId = (Domain.ValueObjects.Id<User>)trainee.Id,
                 Reps = 5,
                 Series = 1,
                 Weight = 110,
                 Unit = WeightUnits.Kilograms,
                 TrainingId = (Domain.ValueObjects.Id<Training>)trainingB.Id,
                 CreatedAt = dayB
            };

            db.ExerciseScores.AddRange(scoreA, scoreB);
             db.EloRegistries.AddRange(
                 new EloRegistry { Id = (Domain.ValueObjects.Id<EloRegistry>)Guid.NewGuid(), UserId = (Domain.ValueObjects.Id<User>)trainee.Id, Date = dayA, Elo = 1010 },
                 new EloRegistry { Id = (Domain.ValueObjects.Id<EloRegistry>)Guid.NewGuid(), UserId = (Domain.ValueObjects.Id<User>)trainee.Id, Date = dayB, Elo = 1030 });

            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader((Guid)trainer.Id);

        var progressResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/exercise-scores/chart", new
        {
            exerciseId = exerciseId.ToString()
        });
        progressResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var progress = await progressResponse.Content.ReadFromJsonAsync<List<ExerciseScoresChartDataResponse>>();
        progress.Should().NotBeNull();
        progress!.Count.Should().Be(2);
        progress.Select(x => x.ExerciseId).Should().OnlyContain(x => x == exerciseId.ToString());

        var eloResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/elo/chart");
        eloResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var elo = await eloResponse.Content.ReadFromJsonAsync<List<EloRegistryChartResponse>>();
        elo.Should().NotBeNull();
        elo!.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task GetTraineeMainRecordsHistory_AsLinkedTrainer_ReturnsHistory()
    {
        var trainer = await SeedTrainerAsync("trainer-read-records", "trainer-read-records@example.com");
        var trainee = await SeedUserAsync(name: "trainee-read-records", email: "trainee-read-records@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await LinkTrainerAndTraineeAsync(db, (Guid)trainer.Id, (Guid)trainee.Id);

             var exercise = new Exercise { Id = (Domain.ValueObjects.Id<Exercise>)Guid.NewGuid(), Name = "Deadlift", BodyPart = BodyParts.Back };
             db.Exercises.Add(exercise);

             db.MainRecords.AddRange(
                 new MainRecord
                 {
                     Id = (Domain.ValueObjects.Id<MainRecord>)Guid.NewGuid(),
                     UserId = (Domain.ValueObjects.Id<User>)trainee.Id,
                     ExerciseId = (Domain.ValueObjects.Id<Exercise>)exercise.Id,
                     Weight = 140,
                     Unit = WeightUnits.Kilograms,
                     Date = DateTimeOffset.UtcNow.AddDays(-20)
                 },
                 new MainRecord
                 {
                     Id = (Domain.ValueObjects.Id<MainRecord>)Guid.NewGuid(),
                     UserId = (Domain.ValueObjects.Id<User>)trainee.Id,
                     ExerciseId = (Domain.ValueObjects.Id<Exercise>)exercise.Id,
                     Weight = 150,
                     Unit = WeightUnits.Kilograms,
                     Date = DateTimeOffset.UtcNow.AddDays(-10)
                });

            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader((Guid)trainer.Id);

        var response = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/main-records/history");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var records = await response.Content.ReadFromJsonAsync<List<MainRecordResponse>>();
        records.Should().NotBeNull();
        records!.Count.Should().Be(2);
        records.Select(x => x.Weight).Should().Contain(ExpectedMainRecordWeights);
    }

    [Test]
    public async Task TraineeReadEndpoints_WhenTraineeBelongsToAnotherTrainer_ReturnsNotFound()
    {
        var trainerA = await SeedTrainerAsync("trainer-read-owner-a", "trainer-read-owner-a@example.com");
        var trainerB = await SeedTrainerAsync("trainer-read-owner-b", "trainer-read-owner-b@example.com");
        var trainee = await SeedUserAsync(name: "trainee-read-owner", email: "trainee-read-owner@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await LinkTrainerAndTraineeAsync(db, (Guid)trainerB.Id, (Guid)trainee.Id);
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader((Guid)trainerA.Id);
        var response = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/trainings/dates");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task TraineeReadEndpoints_WithInvalidIds_ReturnsBadRequestWithResourceMessage()
    {
        var trainer = await SeedTrainerAsync("trainer-read-invalid", "trainer-read-invalid@example.com");
        var trainee = await SeedUserAsync(name: "trainee-read-invalid", email: "trainee-read-invalid@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await LinkTrainerAndTraineeAsync(db, (Guid)trainer.Id, (Guid)trainee.Id);
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader((Guid)trainer.Id);

        var invalidTraineeResponse = await Client.GetAsync("/api/trainer/trainees/not-a-guid/trainings/dates");
        invalidTraineeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var invalidTraineeBody = await invalidTraineeResponse.Content.ReadAsStringAsync();
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            invalidTraineeBody.Should().Contain(Messages.UserIdRequired);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        var invalidExerciseResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/exercise-scores/chart", new
        {
            exerciseId = "not-a-guid"
        });
        invalidExerciseResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var invalidExerciseBody = await invalidExerciseResponse.Content.ReadAsStringAsync();
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            invalidExerciseBody.Should().Contain(Messages.ExerciseIdRequired);
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

        SetAuthorizationHeader((Guid)trainer.Id);
        var createResponse = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });
        var invitation = await createResponse.Content.ReadFromJsonAsync<TrainerInvitationResponse>();

        SetAuthorizationHeader((Guid)trainee.Id);
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

        SetAuthorizationHeader((Guid)trainer.Id);
        var createResponse = await Client.PostAsJsonAsync("/api/trainer/invitations", new
        {
            traineeId = trainee.Id.ToString()
        });
        var invitation = await createResponse.Content.ReadFromJsonAsync<TrainerInvitationResponse>();

        SetAuthorizationHeader((Guid)trainee.Id);
        var rejectResponse = await Client.PostAsync($"/api/trainee/invitations/{invitation!.Id}/reject", null);
        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
         var invitationEntity = await db.TrainerInvitations.FirstAsync(i => i.Id == (Domain.ValueObjects.Id<TrainerInvitation>)Guid.Parse(invitation.Id));
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
                 Id = (Domain.ValueObjects.Id<TrainerInvitation>)invitationId,
                 TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                 TraineeId = (Domain.ValueObjects.Id<User>)trainee.Id,
                Code = "EXPIRED123456",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader((Guid)trainee.Id);
        var response = await Client.PostAsync($"/api/trainee/invitations/{invitationId}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        body.Should().NotBeNull();
        body!.Message.Should().Be("Invitation has expired.");

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
         var invitation = await verifyDb.TrainerInvitations.FirstAsync(i => i.Id == (Domain.ValueObjects.Id<TrainerInvitation>)invitationId);
        invitation.Status.ToString().Should().Be("Expired");
        invitation.RespondedAt.Should().NotBeNull();
    }

    [Test]
    public async Task CreateInvitation_AsRegularUser_ReturnsForbidden()
    {
        var regularUser = await SeedUserAsync(name: "regular-invite", email: "regular-invite@example.com", password: "password123");
        var trainee = await SeedUserAsync(name: "regular-target", email: "regular-target@example.com", password: "password123");
        SetAuthorizationHeader((Guid)regularUser.Id);

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
                 Id = (Domain.ValueObjects.Id<TrainerTraineeLink>)Guid.NewGuid(),
                 TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                 TraineeId = (Domain.ValueObjects.Id<User>)trainee.Id
             });
             await db.SaveChangesAsync();
         }

         SetAuthorizationHeader((Guid)trainer.Id);
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
                 Id = (Domain.ValueObjects.Id<TrainerTraineeLink>)Guid.NewGuid(),
                 TrainerId = (Domain.ValueObjects.Id<User>)trainer.Id,
                 TraineeId = (Domain.ValueObjects.Id<User>)trainee.Id
             });
             await db.SaveChangesAsync();
         }

         SetAuthorizationHeader((Guid)trainee.Id);
         var response = await Client.PostAsync("/api/trainee/trainer/detach", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var link = await verifyDb.TrainerTraineeLinks.FirstOrDefaultAsync(x => x.TrainerId == trainer.Id && x.TraineeId == trainee.Id);
        link.Should().BeNull();
    }

    [Test]
    public async Task TrainerPlanManagement_AssignAndUnassignPlan_UpdatesTraineeActivePlan()
    {
        var trainer = await SeedTrainerAsync("trainer-plan-assign", "trainer-plan-assign@example.com");
        var trainee = await SeedUserAsync(name: "trainee-plan-assign", email: "trainee-plan-assign@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await LinkTrainerAndTraineeAsync(db, (Guid)trainer.Id, (Guid)trainee.Id);
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader((Guid)trainer.Id);
        var createResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/plans", new
        {
            name = "Trainer Owned Plan"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdPlan = await createResponse.Content.ReadFromJsonAsync<TrainerManagedPlanResponse>();
        createdPlan.Should().NotBeNull();
        createdPlan!.Name.Should().Be("Trainer Owned Plan");
        createdPlan.IsActive.Should().BeFalse();

        var assignResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/plans/{createdPlan.Id}/assign", null);
        assignResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthorizationHeader((Guid)trainee.Id);
        var activeResponse = await Client.GetAsync("/api/trainee/plan/active");
        activeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var activePlan = await activeResponse.Content.ReadFromJsonAsync<TrainerManagedPlanResponse>();
        activePlan.Should().NotBeNull();
        activePlan!.Id.Should().Be(createdPlan.Id);
        activePlan.IsActive.Should().BeTrue();

        SetAuthorizationHeader((Guid)trainer.Id);
        var unassignResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/plans/unassign", null);
        unassignResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthorizationHeader((Guid)trainee.Id);
        var noActiveResponse = await Client.GetAsync("/api/trainee/plan/active");
        noActiveResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task TrainerPlanManagement_WhenTrainerDoesNotOwnTrainee_ReturnsNotFound()
    {
        var trainerA = await SeedTrainerAsync("trainer-plan-owner-a", "trainer-plan-owner-a@example.com");
        var trainerB = await SeedTrainerAsync("trainer-plan-owner-b", "trainer-plan-owner-b@example.com");
        var trainee = await SeedUserAsync(name: "trainee-plan-owner", email: "trainee-plan-owner@example.com", password: "password123");

        Guid planId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await LinkTrainerAndTraineeAsync(db, (Guid)trainerB.Id, (Guid)trainee.Id);

             var plan = new Plan
             {
                 Id = (Domain.ValueObjects.Id<Plan>)Guid.NewGuid(),
                 UserId = (Domain.ValueObjects.Id<User>)trainee.Id,
                 Name = "Foreign Plan",
                 IsActive = false,
                 IsDeleted = false
             };

             db.Plans.Add(plan);
             await db.SaveChangesAsync();
             planId = (Guid)plan.Id;
        }

        SetAuthorizationHeader((Guid)trainerA.Id);
        var assignResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/plans/{planId}/assign", null);
        assignResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

     private static async Task LinkTrainerAndTraineeAsync(AppDbContext db, Guid trainerId, Guid traineeId)
     {
         var existing = await db.TrainerTraineeLinks.FirstOrDefaultAsync(x => x.TrainerId == (Domain.ValueObjects.Id<User>)trainerId && x.TraineeId == (Domain.ValueObjects.Id<User>)traineeId);
        if (existing != null)
        {
            return;
        }

         db.TrainerTraineeLinks.Add(new TrainerTraineeLink
         {
             Id = (Domain.ValueObjects.Id<TrainerTraineeLink>)Guid.NewGuid(),
             TrainerId = (Domain.ValueObjects.Id<User>)trainerId,
             TraineeId = (Domain.ValueObjects.Id<User>)traineeId
         });
     }

     private async Task<User> SeedTrainerAsync(string name, string email, string preferredLanguage = "en-US")
     {
         var trainer = await SeedUserAsync(name: name, email: email, password: "password123");

         using var scope = Factory.Services.CreateScope();
         var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

          var alreadyLinked = await db.UserRoles.AnyAsync(ur => ur.UserId == (Domain.ValueObjects.Id<User>)trainer.Id && ur.RoleId == (Domain.ValueObjects.Id<Role>)AppDbContext.TrainerRoleSeedId);
         if (!alreadyLinked)
         {
              db.UserRoles.Add(new UserRole
              {
                  UserId = (Domain.ValueObjects.Id<User>)trainer.Id,
                  RoleId = (Domain.ValueObjects.Id<Role>)AppDbContext.TrainerRoleSeedId
             });
         }

         var trainerToUpdate = await db.Users.FirstAsync(u => u.Id == (Domain.ValueObjects.Id<User>)trainer.Id);
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

    private sealed class TrainingByDateDetailsResponse
    {
        [JsonPropertyName("gym")]
        public string? Gym { get; set; }

        [JsonPropertyName("exercises")]
        public List<TrainingExerciseGroupResponse> Exercises { get; set; } = [];
    }

    private sealed class TrainingExerciseGroupResponse
    {
        [JsonPropertyName("exerciseDetails")]
        public ExerciseDetailsResponse ExerciseDetails { get; set; } = new();
    }

    private sealed class ExerciseDetailsResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ExerciseScoresChartDataResponse
    {
        [JsonPropertyName("exerciseId")]
        public string ExerciseId { get; set; } = string.Empty;
    }

    private sealed class EloRegistryChartResponse
    {
        [JsonPropertyName("value")]
        public int Value { get; set; }
    }

    private sealed class MainRecordResponse
    {
        [JsonPropertyName("weight")]
        public double Weight { get; set; }
    }

    private sealed class TrainerManagedPlanResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }
    }

    private sealed class MessageResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }
}
