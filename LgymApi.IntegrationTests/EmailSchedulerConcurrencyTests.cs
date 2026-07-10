using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using LgymApi.Application.Features.TrainerRelationships;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using LgymApi.IntegrationTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class EmailSchedulerConcurrencyTests : IntegrationTestBase
{
    [Test]
    public async Task ConcurrentSchedule_SameCorrelationId_CreatesSingleNotification()
    {
        // Arrange
        var trainer = await SeedTrainerAsync();
        var trainee = await SeedUserAsync();
        var invitation = await SeedInvitationAsync(trainer.Id, trainee.Email.Value, status: TrainerInvitationStatus.Pending);
        var payload = new InvitationAcceptedEmailPayload
        {
            InvitationId = invitation,
            TrainerName = trainer.Name,
            TraineeName = trainee.Name,
            RecipientEmail = trainer.Email,
            CultureName = "en-US",
            PreferredTimeZone = "UTC"
        };

        var scheduler = Factory.Services.GetRequiredService<IEmailScheduler<InvitationAcceptedEmailPayload>>();

        // Schedule 10 identical payloads concurrently
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(scheduler.ScheduleAsync(payload));
        }

        // Wait for all tasks to complete
        await Task.WhenAll(tasks);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = await db.NotificationMessages
            .Where(n => n.CorrelationId == payload.CorrelationId)
            .ToListAsync();

        notifications.Should().HaveCount(1);
    }

    private async Task<User> SeedTrainerAsync(string name = "trainer", string email = "trainer@example.com")
    {
        var trainer = await SeedUserAsync(name: name, email: email, password: "password123");
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alreadyLinked = await db.UserRoles.AnyAsync(ur => ur.UserId == trainer.Id && ur.RoleId == RoleSeedDataConfiguration.TrainerRoleSeedId);
        if (!alreadyLinked)
        {
            db.UserRoles.Add(new UserRole { UserId = trainer.Id, RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId });
        }

        var trainerToUpdate = await db.Users.FirstAsync(u => u.Id == trainer.Id);
        trainerToUpdate.PreferredLanguage = "en-US";
        await db.SaveChangesAsync();
        return trainer;
    }

    private async Task<Id<TrainerInvitation>> SeedInvitationAsync(
        Id<User> trainerId,
        string inviteeEmail,
        TrainerInvitationStatus status,
        string code = "TESTCODE0001",
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? respondedAt = null)
    {
        var invitationId = Id<TrainerInvitation>.New();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TrainerInvitations.Add(new TrainerInvitation
        {
            Id = invitationId,
            TrainerId = trainerId,
            InviteeEmail = inviteeEmail,
            TraineeId = null,
            Code = code,
            Status = status,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(7),
            RespondedAt = respondedAt
        });
        await db.SaveChangesAsync();
        return invitationId;
    }
}
