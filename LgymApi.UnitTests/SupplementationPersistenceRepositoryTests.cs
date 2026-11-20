using System.Reflection;
using LgymApi.Application.Nutrition.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories.Nutrition;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class SupplementationPersistenceRepositoryTests
{
    [Test]
    public async Task MutationLoads_TrackPlansAndIntakeLogs()
    {
        await using var database = CreateDbContext();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var plan = CreatePlan(trainerId, traineeId, "plan", true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var item = CreateItem(plan.Id, "Magnesium", 0, new TimeSpan(8, 0, 0), DateTimeOffset.UtcNow);
        var log = CreateLog(traineeId, item.Id, new DateOnly(2026, 7, 20), DateTimeOffset.UtcNow);
        database.SupplementPlans.Add(plan);
        database.SupplementPlanItems.Add(item);
        database.SupplementIntakeLogs.Add(log);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        var repository = new SupplementationPersistenceRepository(database);

        var trackedPlan = await repository.FindTrackedPlanByIdAsync(plan.Id);

        Assert.That(trackedPlan, Is.Not.Null);
        Assert.That(database.ChangeTracker.Entries(), Is.Not.Empty);

        database.ChangeTracker.Clear();
        var trackedPlans = await repository.ListTrackedPlansByTrainerAndTraineeAsync(trainerId, traineeId);

        Assert.That(trackedPlans, Has.Exactly(1).Items);
        Assert.That(database.ChangeTracker.Entries(), Is.Not.Empty);

        database.ChangeTracker.Clear();
        var trackedActivePlan = await repository.GetTrackedActivePlanForTraineeAsync(traineeId);

        Assert.That(trackedActivePlan!.Id, Is.EqualTo(plan.Id));
        Assert.That(database.ChangeTracker.Entries(), Is.Not.Empty);

        database.ChangeTracker.Clear();
        var trackedLog = await repository.FindTrackedIntakeLogAsync(traineeId, item.Id, log.IntakeDate);

        Assert.That(trackedLog!.Id, Is.EqualTo(log.Id));
        Assert.That(database.ChangeTracker.Entries(), Is.Not.Empty);
    }

    [Test]
    public async Task ReadQueries_PreserveLegacyFiltersIncludesOrderingRangesAndOneRowReadsWithoutTracking()
    {
        await using var database = CreateDbContext();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var otherTrainerId = Id<User>.New();
        var latest = CreatePlan(trainerId, traineeId, "latest", true, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddHours(-2));
        var earlier = CreatePlan(trainerId, traineeId, "earlier", false, DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-2));
        var deleted = CreatePlan(trainerId, traineeId, "deleted", true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, isDeleted: true);
        var otherTrainerActive = CreatePlan(otherTrainerId, traineeId, "other-trainer", true, DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow);
        var firstItem = CreateItem(latest.Id, "first", 0, new TimeSpan(8, 0, 0), DateTimeOffset.UtcNow.AddDays(-1));
        var secondItem = CreateItem(latest.Id, "second", 0, new TimeSpan(9, 0, 0), DateTimeOffset.UtcNow.AddDays(-2));
        var outsideItem = CreateItem(earlier.Id, "outside", 0, new TimeSpan(10, 0, 0), DateTimeOffset.UtcNow);
        var firstLog = CreateLog(traineeId, firstItem.Id, new DateOnly(2026, 7, 20), DateTimeOffset.UtcNow.AddHours(9));
        var secondLog = CreateLog(traineeId, secondItem.Id, new DateOnly(2026, 7, 20), DateTimeOffset.UtcNow.AddHours(10));
        var outsideRangeLog = CreateLog(traineeId, firstItem.Id, new DateOnly(2026, 7, 21), DateTimeOffset.UtcNow.AddHours(8));
        var otherPlanLog = CreateLog(traineeId, outsideItem.Id, new DateOnly(2026, 7, 20), DateTimeOffset.UtcNow.AddHours(7));
        database.SupplementPlans.AddRange(latest, earlier, deleted, otherTrainerActive);
        database.SupplementPlanItems.AddRange(firstItem, secondItem, outsideItem);
        database.SupplementIntakeLogs.AddRange(firstLog, secondLog, outsideRangeLog, otherPlanLog);
        await database.SaveChangesAsync();
        otherTrainerActive.Notes = "newest active plan";
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        var repository = new SupplementationPersistenceRepository(database);

        var plans = await repository.ListPlansByTrainerAndTraineeAsync(trainerId, traineeId);
        var activePlan = await repository.GetActivePlanForTraineeAsync(traineeId);
        var logs = await repository.ListIntakeLogsForPlanAsync(traineeId, latest.Id, new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 20));
        var log = await repository.FindIntakeLogAsync(traineeId, secondItem.Id, secondLog.IntakeDate);

        Assert.That(plans.Select(plan => plan.Id), Is.EqualTo(new[] { latest.Id, earlier.Id }));
        Assert.That(plans.Single(plan => plan.Id == latest.Id).Items.Select(item => item.Id), Is.EqualTo(new[] { firstItem.Id, secondItem.Id }));
        Assert.That(activePlan!.Id, Is.EqualTo(otherTrainerActive.Id));
        Assert.That(logs.Select(intakeLog => intakeLog.Id), Is.EqualTo(new[] { firstLog.Id, secondLog.Id }));
        Assert.That((logs).All(intakeLog => intakeLog.PlanItem != null), Is.True);
        Assert.That(log!.Id, Is.EqualTo(secondLog.Id));
        Assert.That(database.ChangeTracker.Entries(), Is.Empty);
    }

    [Test]
    public async Task StagedWrites_AreInvisibleToFreshContextUntilExternalUnitOfWorkSaves()
    {
        var databaseName = $"supplementation-persistence-stage-{Id<SupplementationPersistenceRepositoryTests>.New():N}";
        var options = CreateOptions(databaseName);
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var plan = CreatePlan(trainerId, traineeId, "staged", true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var item = CreateItem(plan.Id, "Creatine", 0, new TimeSpan(8, 0, 0), DateTimeOffset.UtcNow);
        plan.Items.Add(item);
        var log = CreateLog(traineeId, item.Id, new DateOnly(2026, 7, 20), DateTimeOffset.UtcNow);

        await using (var writeContext = new AppDbContext(options))
        {
            var repository = new SupplementationPersistenceRepository(writeContext);
            await repository.AddPlanAsync(plan);
            await repository.AddIntakeLogAsync(log);
            Assert.That((writeContext.ChangeTracker.Entries().Select(entry => entry.State)).All(state => state == EntityState.Added), Is.True);

            await using (var beforeCommitContext = new AppDbContext(options))
            {
                Assert.That((await beforeCommitContext.SupplementPlans.AsNoTracking().AnyAsync()), Is.False);
                Assert.That((await beforeCommitContext.SupplementIntakeLogs.AsNoTracking().AnyAsync()), Is.False);
            }

            await new EfUnitOfWork(writeContext).SaveChangesAsync();
        }

        await using var afterCommitContext = new AppDbContext(options);
        Assert.That((await afterCommitContext.SupplementPlans.AsNoTracking().SingleAsync()).Id, Is.EqualTo(plan.Id));
        Assert.That((await afterCommitContext.SupplementIntakeLogs.AsNoTracking().SingleAsync()).Id, Is.EqualTo(log.Id));
    }

    [Test]
    public void Repository_DoesNotExposeOrCallSaveOrTransactionOperations()
    {
        var declaredMethodNames = typeof(SupplementationPersistenceRepository)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Select(method => method.Name);
        var sourcePath = Path.Combine(
            GetRepositoryRoot(),
            "LgymApi.Infrastructure",
            "Repositories",
            "Nutrition",
            "SupplementationPersistenceRepository.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.That(declaredMethodNames, Has.None.EqualTo(nameof(AppDbContext.SaveChangesAsync)).And.None.EqualTo(nameof(EfUnitOfWork.BeginTransactionAsync)));
        Assert.That(source, Does.Not.Contain("SaveChangesAsync("));
        Assert.That(source, Does.Not.Contain("BeginTransactionAsync("));
    }

    [Test]
    public void NutritionInfrastructure_RegistersSupplementationPersistencePortOnceAndRetainsDietPersistence()
    {
        var services = new ServiceCollection();
        services.AddNutritionInfrastructure();

        var supplementationRegistrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(ISupplementationPersistence))
            .ToList();

        Assert.That(supplementationRegistrations, Has.Exactly(1).Items);
        Assert.That(supplementationRegistrations[0].ImplementationType, Is.EqualTo(typeof(SupplementationPersistenceRepository)));
        Assert.That((services).Where(descriptor => descriptor.ServiceType == typeof(IDietPlanPersistence)), Has.Exactly(1).Items);
    }

    private static AppDbContext CreateDbContext()
        => new(CreateOptions($"supplementation-persistence-{Id<SupplementationPersistenceRepositoryTests>.New():N}"));

    private static DbContextOptions<AppDbContext> CreateOptions(string databaseName)
        => new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

    private static SupplementPlan CreatePlan(
        Id<User> trainerId,
        Id<User> traineeId,
        string name,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        bool isDeleted = false)
        => new()
        {
            Id = Id<SupplementPlan>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            Name = name,
            IsActive = isActive,
            IsDeleted = isDeleted,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

    private static SupplementPlanItem CreateItem(
        Id<SupplementPlan> planId,
        string supplementName,
        int order,
        TimeSpan timeOfDay,
        DateTimeOffset createdAt)
        => new()
        {
            Id = Id<SupplementPlanItem>.New(),
            PlanId = planId,
            SupplementName = supplementName,
            Dosage = "1 dose",
            Order = order,
            TimeOfDay = timeOfDay,
            DaysOfWeekMask = DaysOfWeekSet.EveryDay,
            IsDeleted = false,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

    private static SupplementIntakeLog CreateLog(
        Id<User> traineeId,
        Id<SupplementPlanItem> planItemId,
        DateOnly intakeDate,
        DateTimeOffset takenAt)
        => new()
        {
            Id = Id<SupplementIntakeLog>.New(),
            TraineeId = traineeId,
            PlanItemId = planItemId,
            IntakeDate = intakeDate,
            TakenAt = takenAt,
            IsDeleted = false,
            CreatedAt = takenAt,
            UpdatedAt = takenAt
        };

    private static string GetRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LgymApi.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
