using FluentAssertions;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Domain.Security;
using LgymApi.Application.Features.Exercise;
using LgymApi.Application.Features.Exercise.Models;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
public sealed class PostgreSqlExerciseMediaConcurrencyTests : PostgreSqlIntegrationTestBase
{
    [TestCase("metadata")]
    [TestCase("formula")]
    [TestCase("delete")]
    public async Task LegacyMutation_RacingMediaCommitReturnsConflictAndPreservesNewImage(string operation)
    {
        var actor = new AuthenticatedAccountContext(Id<AccountReference>.New(), null, [], [AuthConstants.Permissions.ManageGlobalExercises], false, false);
        var exercise = await SeedAsync(null);
        using var legacy = Factory.Services.CreateScope();
        // Freeze the tracked metadata write's original concurrency state before the competing commit.
        var legacyDb = legacy.ServiceProvider.GetRequiredService<AppDbContext>();
        await legacyDb.Exercises.SingleAsync(x => x.Id == exercise.Id);
        using (var media = Factory.Services.CreateScope())
        {
            await media.ServiceProvider.GetRequiredService<IExerciseMediaPersistence>().StageImageAsync(exercise.Id, null, "https://exercise.test/newer.gif", default);
            await media.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        }
        var service = legacy.ServiceProvider.GetRequiredService<IExerciseService>();
        var result = operation switch
        {
            "metadata" => await service.UpdateExerciseAsync(actor, new UpdateExerciseInput(exercise.Id, "Stale metadata", BodyParts.Back, "Stale description", null)),
            "formula" => await service.UpdateExerciseWithFormulaAsync(actor, new UpdateExerciseWithFormulaInput(exercise.Id, "Stale formula", BodyParts.Back, ExerciseEloFormula.Standard, "Stale description", null)),
            _ => await service.DeleteExerciseAsync(actor, actor.Id, exercise.Id)
        };
        result.IsFailure.Should().BeTrue(); result.Error.HttpStatusCode.Should().Be(409);
        using var check = Factory.Services.CreateScope();
        var saved = await check.ServiceProvider.GetRequiredService<AppDbContext>().Exercises.SingleAsync(x => x.Id == exercise.Id);
        saved.Image.Should().Be("https://exercise.test/newer.gif"); saved.Name.Should().Be(exercise.Name); saved.Description.Should().Be(exercise.Description); saved.IsDeleted.Should().BeFalse();
    }

    [Test]
    public async Task ConcurrentCreateSameOperation_ResolvesOneUuidAndOneRecordOnRetry()
    {
        var actor = new AuthenticatedAccountContext(Id<AccountReference>.New(), null, [], [AuthConstants.Permissions.ManageGlobalExercises], false, false);
        var input = new CreateExerciseMediaInput(Id<ExerciseMediaOperationReference>.New(), "Concurrent create receipt", BodyParts.Chest, null, null, null);
        using var first = Factory.Services.CreateScope(); using var second = Factory.Services.CreateScope();
        var results = await Task.WhenAll(
            first.ServiceProvider.GetRequiredService<IExerciseMediaService>().CreateAsync(actor, input, null, default),
            second.ServiceProvider.GetRequiredService<IExerciseMediaService>().CreateAsync(actor, input, null, default));
        results.Should().Contain(x => x.IsSuccess);
        using var retry = Factory.Services.CreateScope();
        var confirmed = await retry.ServiceProvider.GetRequiredService<IExerciseMediaService>().CreateAsync(actor, input, null, default);
        confirmed.IsSuccess.Should().BeTrue();
        var db = retry.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Exercises.CountAsync(x => x.Name == input.Name)).Should().Be(1);
        (await db.ExerciseMediaUploads.CountAsync(x => x.OperationId == input.OperationId)).Should().Be(1);
        foreach (var result in results.Where(x => x.IsSuccess)) result.Value.Id.Should().Be(confirmed.Value.Id);
    }

    [TestCase(null)]
    [TestCase("https://legacy.test/previous.gif")]
    public async Task MediaCompareAndUpdate_RejectsSecondWriterAtDatabaseBoundary(string? expectedImage)
    {
        var exercise = await SeedAsync(expectedImage);
        using var first = Factory.Services.CreateScope(); using var second = Factory.Services.CreateScope();
        await first.ServiceProvider.GetRequiredService<IExerciseMediaPersistence>().StageImageAsync(exercise.Id, expectedImage, "https://exercise.test/first.gif", default);
        await second.ServiceProvider.GetRequiredService<IExerciseMediaPersistence>().StageImageAsync(exercise.Id, expectedImage, "https://exercise.test/second.gif", default);
        await first.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () => await second.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync());
        using var check = Factory.Services.CreateScope();
        var saved = await check.ServiceProvider.GetRequiredService<AppDbContext>().Exercises.SingleAsync(x => x.Id == exercise.Id);
        saved.Image.Should().Be("https://exercise.test/first.gif"); saved.Name.Should().Be(exercise.Name); saved.Description.Should().Be(exercise.Description); saved.EloFormula.Should().Be(exercise.EloFormula);
    }

    [Test]
    public async Task MediaCompareAndUpdate_RejectsTargetDeletedAfterRead()
    {
        var exercise = await SeedAsync(null);
        using var writer = Factory.Services.CreateScope();
        await writer.ServiceProvider.GetRequiredService<IExerciseMediaPersistence>().StageImageAsync(exercise.Id, null, "https://exercise.test/new.gif", default);
        using (var deleting = Factory.Services.CreateScope())
        {
            var db = deleting.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Exercises.SingleAsync(x => x.Id == exercise.Id)).IsDeleted = true; await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () => await writer.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync());
    }

    [Test]
    public async Task ActorOperationUniqueness_AndFailedMediaTransactionDoNotPersistFalseReceipts()
    {
        var exercise = await SeedAsync("https://exercise.test/current.gif");
        var actor = Id<AccountReference>.New(); var operation = Id<ExerciseMediaOperationReference>.New();
        using var first = Factory.Services.CreateScope(); using var second = Factory.Services.CreateScope();
        var a = first.ServiceProvider.GetRequiredService<IExerciseMediaPersistence>(); var b = second.ServiceProvider.GetRequiredService<IExerciseMediaPersistence>();
        await a.AddAsync(Session(actor, operation, exercise.Id), default); await b.AddAsync(Session(actor, operation, exercise.Id), default);
        await first.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(async () => await second.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync());
        b.IsConcurrencyFailure(duplicate!).Should().BeTrue();
        using var failed = Factory.Services.CreateScope(); var db = failed.ServiceProvider.GetRequiredService<AppDbContext>(); var persistence = failed.ServiceProvider.GetRequiredService<IExerciseMediaPersistence>();
        var receipt = Session(actor, Id<ExerciseMediaOperationReference>.New(), exercise.Id);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await persistence.AddAsync(receipt, default);
            await persistence.StageImageAsync(exercise.Id, null, "https://exercise.test/incorrect.gif", default);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () => await db.SaveChangesAsync());
            await transaction.RollbackAsync();
        }
        using var check = Factory.Services.CreateScope(); var persisted = check.ServiceProvider.GetRequiredService<AppDbContext>();
        (await persisted.ExerciseMediaUploads.AnyAsync(x => x.Id == receipt.Id)).Should().BeFalse();
        (await persisted.Exercises.SingleAsync(x => x.Id == exercise.Id)).Image.Should().Be("https://exercise.test/current.gif");
    }

    private async Task<Exercise> SeedAsync(string? image)
    {
        using var scope = Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new Exercise { Id = Id<Exercise>.New(), Name = "Atomic media fixture", BodyPart = BodyParts.Chest, EloFormula = ExerciseEloFormula.VolumeWeighted, Description = "Preserve metadata", Image = image };
        db.Exercises.Add(entity); await db.SaveChangesAsync(); return entity;
    }

    private static ExerciseMediaSession Session(Id<AccountReference> actor, Id<ExerciseMediaOperationReference> operation, Id<Exercise> target) => new() { Id = Id<ExerciseMediaUpload>.New(), ActorId = actor, OperationId = operation, TargetExerciseId = target, Status = "bound", ExpiresAt = DateTimeOffset.UtcNow.AddHours(24) };
}
