using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LgymApi.Application.Coaching.Relationships.DetachFromTrainer;
using LgymApi.Application.Coaching.Relationships.GetCurrentTrainer;
using LgymApi.Application.Coaching.Relationships.UnlinkTrainee;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Coaching.Errors;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using LgymApi.IntegrationTests.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class CoachingRelationshipSliceIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task UnlinkTrainee_RemovesOwnedLinkAndQueuesRelationshipEndedCommand()
    {
        var trainer = await SeedUserAsync("slice-unlink-trainer", "slice-unlink-trainer@example.test");
        var trainee = await SeedUserAsync("slice-unlink-trainee", "slice-unlink-trainee@example.test");

        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, trainer.Id));
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id
            });
            await database.SaveChangesAsync();
        }

        using (var actionScope = Factory.Services.CreateScope())
        {
            var useCase = actionScope.ServiceProvider.GetRequiredService<IUnlinkTraineeUseCase>();
            var result = await useCase.ExecuteAsync(new UnlinkTraineeCommand(trainer.Id, trainee.Id));
            Assert.That(result.IsSuccess, Is.True);
        }

        using var verificationScope = Factory.Services.CreateScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await verificationDatabase.TrainerTraineeLinks.AnyAsync(link => link.TraineeId == trainee.Id)), Is.False);
        Assert.That((await verificationDatabase.CommandEnvelopes.CountAsync(envelope =>
            envelope.CommandTypeFullName.Contains("TrainerRelationshipEndedInAppNotificationCommand"))), Is.EqualTo(1));
    }

    [Test]
    public async Task DetachFromTrainer_RemovesLinkAndQueuesRelationshipEndedCommand()
    {
        var trainer = await SeedUserAsync("slice-detach-trainer", "slice-detach-trainer@example.test");
        var trainee = await SeedUserAsync("slice-detach-trainee", "slice-detach-trainee@example.test");

        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id
            });
            await database.SaveChangesAsync();
        }

        using (var actionScope = Factory.Services.CreateScope())
        {
            var useCase = actionScope.ServiceProvider.GetRequiredService<IDetachFromTrainerUseCase>();
            var result = await useCase.ExecuteAsync(new DetachFromTrainerCommand(trainee.Id));
            Assert.That(result.IsSuccess, Is.True);
        }

        using var verificationScope = Factory.Services.CreateScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await verificationDatabase.TrainerTraineeLinks.AnyAsync(link => link.TraineeId == trainee.Id)), Is.False);
        Assert.That((await verificationDatabase.CommandEnvelopes.CountAsync(envelope =>
            envelope.CommandTypeFullName.Contains("TrainerRelationshipEndedInAppNotificationCommand"))), Is.EqualTo(1));
    }

    [Test]
    public async Task GetCurrentTrainer_ReturnsIdentityProfileAndExactLinkTimestamp()
    {
        var trainer = await SeedUserAsync("Slice current trainer", "slice-current-trainer@example.test");
        var trainee = await SeedUserAsync("slice-current-trainee", "slice-current-trainee@example.test");
        var linkedAt = new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.Zero);

        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                CreatedAt = linkedAt,
                UpdatedAt = linkedAt
            });
            await database.SaveChangesAsync();
        }

        using var actionScope = Factory.Services.CreateScope();
        var useCase = actionScope.ServiceProvider.GetRequiredService<IGetCurrentTrainerUseCase>();
        var result = await useCase.ExecuteAsync(new GetCurrentTrainerQuery(trainee.Id));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.TrainerId, Is.EqualTo(trainer.Id));
        Assert.That(result.Value.Name, Is.EqualTo(trainer.Name));
        Assert.That(result.Value.Email, Is.EqualTo(trainer.Email.Value));
        Assert.That(result.Value.Avatar, Is.EqualTo(trainer.Avatar));
        Assert.That(result.Value.LinkedAt, Is.EqualTo(linkedAt));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainee/trainer", "own", "owner-allow")]
    [AuthorizationEvidence("GET", "/api/trainee/trainer", "own", "no-client-subject")]
    [AuthorizationEvidence("GET", "/api/trainee/trainer", "own", "anonymous-denial")]
    public async Task CurrentTrainerRoute_IsActorScopedAndNonDisclosing()
    {
        var trainer = await SeedUserAsync("http-current-trainer", "http-current-trainer@example.test");
        var trainee = await SeedUserAsync("http-current-trainee", "http-current-trainee@example.test");
        var otherTrainee = await SeedUserAsync("http-current-other", "http-current-other@example.test");
        var linkedAt = new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.Zero);

        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                CreatedAt = linkedAt,
                UpdatedAt = linkedAt
            });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainee.Id);
        using var ownerResponse = await Client.GetAsync("/api/trainee/trainer");
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerProfile = await ownerResponse.Content.ReadFromJsonAsync<TraineeTrainerProfileResponse>();
        Assert.That(ownerProfile, Is.Not.Null);
        Assert.That(ownerProfile!.TrainerId, Is.EqualTo(trainer.Id.ToString()));
        Assert.That(ownerProfile.Name, Is.EqualTo(trainer.Name));
        Assert.That(ownerProfile.Email, Is.EqualTo(trainer.Email.Value));
        Assert.That(ownerProfile.LinkedAt, Is.EqualTo(linkedAt));

        SetAuthorizationHeader(otherTrainee.Id);
        using var otherResponse = await Client.GetAsync("/api/trainee/trainer");
        Assert.That(otherResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var otherResponseText = await otherResponse.Content.ReadAsStringAsync();
        Assert.That(otherResponseText, Does.Not.Contain(trainer.Id.ToString()));
        Assert.That(otherResponseText, Does.Not.Contain(trainer.Name));
        Assert.That(otherResponseText, Does.Not.Contain(trainer.Email.Value));

        using (var unlinkScope = Factory.Services.CreateScope())
        {
            var database = unlinkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks
                .SingleAsync(candidate => candidate.TrainerId == trainer.Id && candidate.TraineeId == trainee.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainee.Id);
        using var formerResponse = await Client.GetAsync("/api/trainee/trainer");
        Assert.That(formerResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var formerResponseText = await formerResponse.Content.ReadAsStringAsync();
        Assert.That(formerResponseText, Does.Not.Contain(trainer.Id.ToString()));
        Assert.That(formerResponseText, Does.Not.Contain(trainer.Name));
        Assert.That(formerResponseText, Does.Not.Contain(trainer.Email.Value));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.GetAsync("/api/trainee/trainer");
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousResponseText = await anonymousResponse.Content.ReadAsStringAsync();
        Assert.That(anonymousResponseText, Does.Not.Contain(trainer.Id.ToString()));
        Assert.That(anonymousResponseText, Does.Not.Contain(trainer.Name));
        Assert.That(anonymousResponseText, Does.Not.Contain(trainer.Email.Value));
    }

    [Test]
    public async Task GetCurrentTrainer_WhenTrainerAccountIsDeletedReturnsCurrentNotFoundError()
    {
        var trainer = await SeedUserAsync(
            "slice-deleted-trainer",
            "slice-deleted-trainer@example.test",
            isDeleted: true);
        var trainee = await SeedUserAsync("slice-deleted-trainee", "slice-deleted-trainee@example.test");

        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id
            });
            await database.SaveChangesAsync();
        }

        using var actionScope = Factory.Services.CreateScope();
        var useCase = actionScope.ServiceProvider.GetRequiredService<IGetCurrentTrainerUseCase>();
        var result = await useCase.ExecuteAsync(new GetCurrentTrainerQuery(trainee.Id));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<TrainerRelationshipNotFoundError>());
    }

    private sealed class TraineeTrainerProfileResponse
    {
        [JsonPropertyName("trainerId")]
        public string TrainerId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("linkedAt")]
        public DateTimeOffset LinkedAt { get; set; }
    }
}
