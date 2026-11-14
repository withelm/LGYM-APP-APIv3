using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.AccountDeletion.Finalization;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Services;
using LgymApi.Reporting.AccountDeletion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
internal sealed class PostgreSqlAccountDeletionSharedRecoveryTests : PostgreSqlIntegrationTestBase
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Finalization_PreservesReportsAndPhotoBytesDuringRecovery(bool currentRelationship)
    {
        var trainer = await SeedUserAsync("shared-trainer", "shared-trainer@example.test");
        var trainee = await SeedUserAsync("shared-trainee", "shared-trainee@example.test");
        var generation = Id<CorrelationScope>.New();
        var deadline = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).AddDays(10);
        var trainerDeadline = deadline.AddDays(-11);
        var templateId = Id<ReportTemplate>.New();
        var requestIds = new[] { Id<ReportRequest>.New(), Id<ReportRequest>.New() };
        var photoIds = new[] { Id<Photo>.New(), Id<Photo>.New() };
        var keys = requestIds.SelectMany(request => new[] { $"photos/{trainee.Id}/{request}/original.jpg", $"photos/{trainee.Id}/{request}/thumbnail.jpg" }).ToArray();
        var bytes = new byte[] { 12, 34, 56, 78 };
        var storage = new LocalPhotoDevelopmentStore();
        try
        {
            foreach (var key in keys) await storage.SaveAsync(key, new MemoryStream(bytes));
            await using (var scope = Factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var accounts = await db.Users.Where(x => x.Id == trainer.Id || x.Id == trainee.Id).ToListAsync();
                foreach (var account in accounts)
                {
                    account.DeletionState = AccountDeletionState.PendingDeletion;
                    account.DeletionGeneration = account.Id == trainer.Id ? generation : Id<CorrelationScope>.New();
                    account.DeletionScheduledGeneration = account.DeletionGeneration;
                    account.DeletionEffectiveAt = account.Id == trainer.Id ? trainerDeadline : deadline;
                }
                db.ReportTemplates.Add(new ReportTemplate { Id = templateId, TrainerId = trainer.Id, Name = "shared" });
                if (currentRelationship)
                    db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id });
                for (var i = 0; i < requestIds.Length; i++)
                {
                    db.ReportRequests.Add(new ReportRequest { Id = requestIds[i], TrainerId = trainer.Id, TraineeId = trainee.Id, TemplateId = templateId, Status = ReportRequestStatus.Submitted });
                    db.ReportSubmissions.Add(new ReportSubmission { Id = Id<ReportSubmission>.New(), ReportRequestId = requestIds[i], TraineeId = trainee.Id, PayloadJson = "{\"answer\":42}" });
                    db.Photos.Add(new Photo { Id = photoIds[i], ReportRequestId = requestIds[i], OwnerUserId = trainee.Id, UploaderUserId = trainer.Id, StorageKey = keys[i * 2], ThumbnailStorageKey = keys[i * 2 + 1] });
                }
                await db.SaveChangesAsync();
            }

            await using (var scope = Factory.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<IAccountFinalizationService>().ProcessDueAsync();
                await scope.ServiceProvider.GetRequiredService<IAccountPhotoDeletionService>().ProcessPendingAsync();
            }
            await using (var scope = Factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var persistedTrainer = await db.Users.IgnoreQueryFilters().SingleAsync(x => x.Id == trainer.Id);
                persistedTrainer.DeletionState.Should().Be(AccountDeletionState.PendingDeletion);
                persistedTrainer.DeletionEffectiveAt.Should().Be(trainerDeadline);
                (await db.PhotoDeletionWorkItems.CountAsync(x => x.DeletionGeneration == generation)).Should().Be(0);
                (await db.ReportRequests.IgnoreQueryFilters().Where(x => requestIds.Contains(x.Id)).CountAsync()).Should().Be(2);
                (await db.ReportSubmissions.IgnoreQueryFilters().Where(x => requestIds.Contains(x.ReportRequestId)).CountAsync()).Should().Be(2);
                (await db.Photos.IgnoreQueryFilters().Where(x => photoIds.Contains(x.Id)).CountAsync()).Should().Be(2);
            }
            foreach (var key in keys) (await storage.ReadAsync(key)).Should().Equal(bytes);

            var login = await Client.PostAsJsonAsync("/api/account-deletion/recovery/login", new { name = trainee.Name, password = "password123" });
            login.StatusCode.Should().Be(HttpStatusCode.OK);
            var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recoveryToken").GetString();
            (await Client.PostAsJsonAsync("/api/account-deletion/recovery/cancel", new { recoveryToken = token })).StatusCode.Should().Be(HttpStatusCode.OK);
            await using var verify = Factory.Services.CreateAsyncScope();
            var database = verify.ServiceProvider.GetRequiredService<AppDbContext>();
            var restored = await database.Users.SingleAsync(x => x.Id == trainee.Id);
            restored.DeletionState.Should().Be(AccountDeletionState.Active);
            restored.Name.Should().Be(trainee.Name);
            restored.Email.Should().Be(trainee.Email);
            (await database.ReportSubmissions.IgnoreQueryFilters().Where(x => requestIds.Contains(x.ReportRequestId)).Select(x => x.PayloadJson).ToListAsync()).Should().Equal("{\"answer\":42}", "{\"answer\":42}");
            (await database.Photos.IgnoreQueryFilters().Where(x => photoIds.Contains(x.Id)).Select(x => x.StorageKey).ToListAsync()).Should().BeEquivalentTo(keys[0], keys[2]);
            foreach (var key in keys) (await storage.ReadAsync(key)).Should().Equal(bytes);

            await using (var resumed = Factory.Services.CreateAsyncScope())
            {
                await resumed.ServiceProvider.GetRequiredService<IAccountFinalizationService>().ProcessDueAsync();
                await resumed.ServiceProvider.GetRequiredService<IAccountPhotoDeletionService>().ProcessPendingAsync();
            }
            (await database.Users.IgnoreQueryFilters().SingleAsync(account => account.Id == trainer.Id))
                .DeletionState.Should().Be(AccountDeletionState.PermanentlyDeleted);
            foreach (var key in keys) (await storage.ReadAsync(key)).Should().BeNull();
        }
        finally
        {
            foreach (var key in keys) await storage.DeleteAsync(key);
        }
    }

    [Test]
    public async Task Finalization_EmailOnlyInvitationPreservesTheInviteesRecoveryWindow()
    {
        var trainer = await SeedUserAsync("email-protected-trainer", "email-protected-trainer@example.test");
        var trainee = await SeedUserAsync("email-protected-trainee", "email-protected-trainee@example.test");
        await using (var setup = Factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var account in await db.Users.Where(account => account.Id == trainer.Id || account.Id == trainee.Id).ToListAsync())
            {
                account.DeletionState = AccountDeletionState.PendingDeletion;
                account.DeletionGeneration = Id<CorrelationScope>.New();
                account.DeletionScheduledGeneration = account.DeletionGeneration;
                account.DeletionEffectiveAt = DateTimeOffset.UtcNow.AddDays(account.Id == trainer.Id ? -1 : 10);
            }
            db.TrainerInvitations.Add(new TrainerInvitation
            {
                Id = Id<TrainerInvitation>.New(), TrainerId = trainer.Id, InviteeEmail = trainee.Email.Value,
                Code = "protected-invitation", ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
            });
            await db.SaveChangesAsync();
        }
        await using (var execution = Factory.Services.CreateAsyncScope())
            await execution.ServiceProvider.GetRequiredService<IAccountFinalizationService>().ProcessDueAsync();
        await using var verify = Factory.Services.CreateAsyncScope();
        var database = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        (await database.Users.SingleAsync(account => account.Id == trainer.Id)).DeletionState.Should().Be(AccountDeletionState.PendingDeletion);
        (await database.TrainerInvitations.CountAsync(invitation => invitation.TrainerId == trainer.Id)).Should().Be(1);
    }

    [Test]
    public async Task Finalization_AllSharedRecoveryWindowsExpired_FinalizesBothAccounts()
    {
        var trainer = await SeedUserAsync("due-shared-trainer", "due-shared-trainer@example.test");
        var trainee = await SeedUserAsync("due-shared-trainee", "due-shared-trainee@example.test");
        await using (var setup = Factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var account in await db.Users.Where(account => account.Id == trainer.Id || account.Id == trainee.Id).ToListAsync())
            {
                account.DeletionState = AccountDeletionState.PendingDeletion;
                account.DeletionGeneration = Id<CorrelationScope>.New();
                account.DeletionScheduledGeneration = account.DeletionGeneration;
                account.DeletionEffectiveAt = DateTimeOffset.UtcNow.AddDays(account.Id == trainer.Id ? -2 : -1);
            }
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id });
            await db.SaveChangesAsync();
        }
        await using (var execution = Factory.Services.CreateAsyncScope())
            await execution.ServiceProvider.GetRequiredService<IAccountFinalizationService>().ProcessDueAsync();
        await using var verify = Factory.Services.CreateAsyncScope();
        var states = await verify.ServiceProvider.GetRequiredService<AppDbContext>().Users.IgnoreQueryFilters()
            .Where(account => account.Id == trainer.Id || account.Id == trainee.Id).Select(account => account.DeletionState).ToListAsync();
        states.Should().Equal(AccountDeletionState.PermanentlyDeleted, AccountDeletionState.PermanentlyDeleted);
    }
}
