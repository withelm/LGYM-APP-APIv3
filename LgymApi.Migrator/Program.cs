using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var mongoConnection = configuration["Mongo:ConnectionString"] ?? string.Empty;
var mongoDatabaseName = configuration["Mongo:Database"] ?? string.Empty;
var postgresConnection = configuration.GetConnectionString("Postgres") ?? string.Empty;

if (string.IsNullOrWhiteSpace(mongoConnection) || string.IsNullOrWhiteSpace(postgresConnection))
{
    Console.WriteLine("Missing Mongo/Postgres configuration.");
    return;
}

Log("Migrator starting.");

var batchSize = 1000;
var batchSizeRaw = configuration["Migrator:BatchSize"];
if (int.TryParse(batchSizeRaw, out var parsedBatchSize) && parsedBatchSize > 0)
{
    batchSize = parsedBatchSize;
}

Log($"Batch size: {batchSize}");

var mongoUrl = new MongoUrl(mongoConnection);
var databaseName = !string.IsNullOrWhiteSpace(mongoDatabaseName)
    ? mongoDatabaseName
    : (!string.IsNullOrWhiteSpace(mongoUrl.DatabaseName) ? mongoUrl.DatabaseName : "test");

Log($"Mongo database: {databaseName}");

var mongoClient = new MongoClient(mongoConnection);
var mongoDb = mongoClient.GetDatabase(databaseName);

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(postgresConnection)
    .Options;

await using var dbContext = new AppDbContext(options);

var userMap = new Dictionary<string, Guid>();
var planMap = new Dictionary<string, Guid>();
var planDayMap = new Dictionary<string, Guid>();
var exerciseMap = new Dictionary<string, Guid>();
var trainingMap = new Dictionary<string, Guid>();
var exerciseScoreMap = new Dictionary<string, Guid>();
var gymMap = new Dictionary<string, Guid>();
var addressMap = new Dictionary<string, Guid>();
var measurementMap = new Dictionary<string, Guid>();
var mainRecordMap = new Dictionary<string, Guid>();
var eloRegistryMap = new Dictionary<string, Guid>();
var appConfigMap = new Dictionary<string, Guid>();
var userPlanLegacyMap = new Dictionary<Guid, string>();

try
{
    await RunStep("EF Core schema migrations", () => dbContext.Database.MigrateAsync());
    await RunStep(nameof(MigrateUsers), MigrateUsers);
    await RunStep(nameof(MigrateAddresses), MigrateAddresses);
    await RunStep(nameof(MigrateGyms), MigrateGyms);
    await RunStep(nameof(MigrateExercises), MigrateExercises);
    await RunStep(nameof(MigratePlans), MigratePlans);
    await RunStep("Link users to plans", LinkUsersToPlans);
    await RunStep(nameof(MigratePlanDays), MigratePlanDays);
    await RunStep(nameof(MigrateTrainings), MigrateTrainings);
    await RunStep(nameof(MigrateExerciseScores), MigrateExerciseScores);
    await RunStep(nameof(MigrateTrainingExerciseScores), MigrateTrainingExerciseScores);
    await RunStep(nameof(MigrateMeasurements), MigrateMeasurements);
    await RunStep(nameof(MigrateMainRecords), MigrateMainRecords);
    await RunStep(nameof(MigrateEloRegistries), MigrateEloRegistries);
    await RunStep(nameof(MigrateAppConfigs), MigrateAppConfigs);

    Log("Migration completed.");
}
catch (Exception ex)
{
    Log($"Migration failed: {ex}");
    Environment.ExitCode = 1;
}

async Task MigrateUsers()
{
    var collection = mongoDb.GetCollection<BsonDocument>("users");
    var processed = 0;
    var inserted = 0;
    var users = new List<User>(batchSize);

    async Task Flush()
    {
        if (users.Count == 0)
        {
            return;
        }

        await dbContext.Users.AddRangeAsync(users);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        inserted += users.Count;
        Log($"MigrateUsers: saved batch ({users.Count}), total inserted={inserted}");
        users.Clear();
    }

    using var cursor = await collection.FindAsync(
        FilterDefinition<BsonDocument>.Empty,
        new FindOptions<BsonDocument> { BatchSize = batchSize });

    while (await cursor.MoveNextAsync())
    {
        foreach (var doc in cursor.Current)
        {
            processed++;

            var legacyId = doc.GetValue("_id").ToString();
            var id = Guid.NewGuid();
            userMap[legacyId] = id;

            var planValue = doc.GetValue("plan", BsonNull.Value);
            if (!planValue.IsBsonNull)
            {
                userPlanLegacyMap[id] = planValue.ToString();
            }

            users.Add(new User
            {
                Id = id,
                LegacyMongoId = legacyId,
                Name = doc.GetValue("name", string.Empty).AsString,
                Admin = doc.GetValue("admin", BsonBoolean.False).ToBoolean(),
                Email = doc.GetValue("email", string.Empty).AsString,
                ProfileRank = doc.GetValue("profileRank", string.Empty).AsString,
                Avatar = doc.GetValue("avatar", BsonNull.Value).IsBsonNull ? null : doc.GetValue("avatar").AsString,
                IsDeleted = doc.GetValue("isDeleted", BsonBoolean.False).ToBoolean(),
                IsTester = doc.GetValue("isTester", BsonBoolean.False).ToBoolean(),
                IsVisibleInRanking = doc.GetValue("isVisibleInRanking", BsonBoolean.True).ToBoolean(),
                LegacyHash = doc.GetValue("hash", BsonNull.Value).IsBsonNull ? null : doc.GetValue("hash").AsString,
                LegacySalt = doc.GetValue("salt", BsonNull.Value).IsBsonNull ? null : doc.GetValue("salt").AsString,
                LegacyIterations = doc.GetValue("iterations", BsonNull.Value).IsBsonNull ? null : (int?)doc.GetValue("iterations").ToInt32(),
                LegacyKeyLength = doc.GetValue("keylen", BsonNull.Value).IsBsonNull ? null : (int?)doc.GetValue("keylen").ToInt32(),
                LegacyDigest = doc.GetValue("digest", BsonNull.Value).IsBsonNull ? null : doc.GetValue("digest").AsString,
                CreatedAt = ToPolandOffset(doc.GetValue("createdAt", BsonNull.Value)),
                UpdatedAt = ToPolandOffset(doc.GetValue("updatedAt", BsonNull.Value))
            });

            if (users.Count >= batchSize)
            {
                await Flush();
            }
        }
    }

    await Flush();

    if (processed == 0)
    {
        Log("MigrateUsers: skipped (no documents). ");
        return;
    }

    Log($"MigrateUsers: processed={processed}, inserted={inserted}, planRefs={userPlanLegacyMap.Count}");
}

async Task LinkUsersToPlans()
{
    if (userPlanLegacyMap.Count == 0)
    {
        Log("LinkUsersToPlans: skipped (no plan references on users).");
        return;
    }

    var updated = 0;
    var missingPlans = 0;
    var batch = 0;

    foreach (var (userId, planLegacyId) in userPlanLegacyMap)
    {
        if (!planMap.TryGetValue(planLegacyId, out var planId))
        {
            missingPlans++;
            continue;
        }

        var user = new User { Id = userId };
        dbContext.Users.Attach(user);
        user.PlanId = planId;
        dbContext.Entry(user).Property(u => u.PlanId).IsModified = true;

        updated++;
        batch++;

        if (batch >= batchSize)
        {
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();
            Log($"LinkUsersToPlans: saved batch ({batch}), total updated={updated}");
            batch = 0;
        }
    }

    if (batch > 0)
    {
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
    }

    Log($"LinkUsersToPlans: updated={updated}, missingPlans={missingPlans}");
}

async Task MigrateAddresses()
{
    var collection = mongoDb.GetCollection<BsonDocument>("addresses");
    var processed = 0;
    var inserted = 0;
    var addresses = new List<Address>(batchSize);

    async Task Flush()
    {
        if (addresses.Count == 0)
        {
            return;
        }

        await dbContext.Addresses.AddRangeAsync(addresses);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        inserted += addresses.Count;
        Log($"MigrateAddresses: saved batch ({addresses.Count}), total inserted={inserted}");
        addresses.Clear();
    }

    using var cursor = await collection.FindAsync(
        FilterDefinition<BsonDocument>.Empty,
        new FindOptions<BsonDocument> { BatchSize = batchSize });

    while (await cursor.MoveNextAsync())
    {
        foreach (var doc in cursor.Current)
        {
            processed++;

            var legacyId = doc.GetValue("_id").ToString();
            var id = Guid.NewGuid();
            addressMap[legacyId] = id;

            addresses.Add(new Address
            {
                Id = id,
                LegacyMongoId = legacyId,
                City = doc.GetValue("city", BsonNull.Value).IsBsonNull ? null : doc.GetValue("city").AsString,
                Country = doc.GetValue("country", BsonNull.Value).IsBsonNull ? null : doc.GetValue("country").AsString,
                District = doc.GetValue("district", BsonNull.Value).IsBsonNull ? null : doc.GetValue("district").AsString,
                FormattedAddress = doc.GetValue("formattedAddress", BsonNull.Value).IsBsonNull ? null : doc.GetValue("formattedAddress").AsString,
                IsoCountryCode = doc.GetValue("isoCountryCode", BsonNull.Value).IsBsonNull ? null : doc.GetValue("isoCountryCode").AsString,
                Name = doc.GetValue("name", BsonNull.Value).IsBsonNull ? null : doc.GetValue("name").AsString,
                PostalCode = doc.GetValue("postalCode", BsonNull.Value).IsBsonNull ? null : doc.GetValue("postalCode").AsString,
                Region = doc.GetValue("region", BsonNull.Value).IsBsonNull ? null : doc.GetValue("region").AsString,
                Street = doc.GetValue("street", BsonNull.Value).IsBsonNull ? null : doc.GetValue("street").AsString,
                StreetNumber = doc.GetValue("streetNumber", BsonNull.Value).IsBsonNull ? null : doc.GetValue("streetNumber").AsString,
                Subregion = doc.GetValue("subregion", BsonNull.Value).IsBsonNull ? null : doc.GetValue("subregion").AsString,
                Latitude = doc.GetValue("latitude", BsonNull.Value).IsBsonNull ? 0 : doc.GetValue("latitude").ToDouble(),
                Longitude = doc.GetValue("longitude", BsonNull.Value).IsBsonNull ? 0 : doc.GetValue("longitude").ToDouble(),
                CreatedAt = ToPolandOffset(doc.GetValue("createdAt", BsonNull.Value)),
                UpdatedAt = ToPolandOffset(doc.GetValue("updatedAt", BsonNull.Value))
            });

            if (addresses.Count >= batchSize)
            {
                await Flush();
            }
        }
    }

    await Flush();

    if (processed == 0)
    {
        Log("MigrateAddresses: skipped (no documents).");
        return;
    }

    Log($"MigrateAddresses: processed={processed}, inserted={inserted}");
}

async Task MigrateGyms()
{
    var collection = mongoDb.GetCollection<BsonDocument>("gyms");
    var processed = 0;
    var inserted = 0;
    var skippedMissingUser = 0;
    var gyms = new List<Gym>(batchSize);

    async Task Flush()
    {
        if (gyms.Count == 0)
        {
            return;
        }

        await dbContext.Gyms.AddRangeAsync(gyms);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        inserted += gyms.Count;
        Log($"MigrateGyms: saved batch ({gyms.Count}), total inserted={inserted}");
        gyms.Clear();
    }

    using var cursor = await collection.FindAsync(
        FilterDefinition<BsonDocument>.Empty,
        new FindOptions<BsonDocument> { BatchSize = batchSize });

    while (await cursor.MoveNextAsync())
    {
        foreach (var doc in cursor.Current)
        {
            processed++;

            var legacyId = doc.GetValue("_id").ToString();
            var userId = ResolveGuid(doc.GetValue("user", BsonNull.Value), userMap);
            var addressId = ResolveGuid(doc.GetValue("address", BsonNull.Value), addressMap);

            if (userId == Guid.Empty)
            {
                skippedMissingUser++;
                continue;
            }

            var id = Guid.NewGuid();
            gymMap[legacyId] = id;

            gyms.Add(new Gym
            {
                Id = id,
                LegacyMongoId = legacyId,
                UserId = userId,
                Name = doc.GetValue("name", string.Empty).AsString,
                AddressId = addressId == Guid.Empty ? null : addressId,
                IsDeleted = doc.GetValue("isDeleted", BsonBoolean.False).ToBoolean(),
                CreatedAt = ToPolandOffset(doc.GetValue("createdAt", BsonNull.Value)),
                UpdatedAt = ToPolandOffset(doc.GetValue("updatedAt", BsonNull.Value))
            });

            if (gyms.Count >= batchSize)
            {
                await Flush();
            }
        }
    }

    await Flush();

    if (processed == 0)
    {
        Log("MigrateGyms: skipped (no documents).");
        return;
    }

    Log($"MigrateGyms: processed={processed}, inserted={inserted}, skippedMissingUser={skippedMissingUser}");
}

async Task MigrateExercises()
{
    var collection = mongoDb.GetCollection<BsonDocument>("exercises");
    var processed = 0;
    var inserted = 0;
    var exercises = new List<Exercise>(batchSize);

    async Task Flush()
    {
        if (exercises.Count == 0)
        {
            return;
        }

        await dbContext.Exercises.AddRangeAsync(exercises);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        inserted += exercises.Count;
        Log($"MigrateExercises: saved batch ({exercises.Count}), total inserted={inserted}");
        exercises.Clear();
    }

    using var cursor = await collection.FindAsync(
        FilterDefinition<BsonDocument>.Empty,
        new FindOptions<BsonDocument> { BatchSize = batchSize });

    while (await cursor.MoveNextAsync())
    {
        foreach (var doc in cursor.Current)
        {
            processed++;

            var legacyId = doc.GetValue("_id").ToString();
            var id = Guid.NewGuid();
            exerciseMap[legacyId] = id;

            var userId = ResolveGuid(doc.GetValue("user", BsonNull.Value), userMap);
            var bodyPartRaw = doc.GetValue("bodyPart", string.Empty).AsString;
            Enum.TryParse<BodyParts>(bodyPartRaw, out var bodyPart);

            exercises.Add(new Exercise
            {
                Id = id,
                LegacyMongoId = legacyId,
                Name = doc.GetValue("name", string.Empty).AsString,
                BodyPart = bodyPart,
                Description = doc.GetValue("description", BsonNull.Value).IsBsonNull ? null : doc.GetValue("description").AsString,
                Image = doc.GetValue("image", BsonNull.Value).IsBsonNull ? null : doc.GetValue("image").AsString,
                UserId = userId == Guid.Empty ? null : userId,
                IsDeleted = doc.GetValue("isDeleted", BsonBoolean.False).ToBoolean(),
                CreatedAt = ToPolandOffset(doc.GetValue("createdAt", BsonNull.Value)),
                UpdatedAt = ToPolandOffset(doc.GetValue("updatedAt", BsonNull.Value))
            });

            if (exercises.Count >= batchSize)
            {
                await Flush();
            }
        }
    }

    await Flush();

    if (processed == 0)
    {
        Log("MigrateExercises: skipped (no documents).");
        return;
    }

    Log($"MigrateExercises: processed={processed}, inserted={inserted}");
}

async Task MigratePlans()
{
    var collection = mongoDb.GetCollection<BsonDocument>("plans");
    var processed = 0;
    var inserted = 0;
    var skippedMissingUser = 0;
    var plans = new List<Plan>(batchSize);

    async Task Flush()
    {
        if (plans.Count == 0)
        {
            return;
        }

        await dbContext.Plans.AddRangeAsync(plans);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        inserted += plans.Count;
        Log($"MigratePlans: saved batch ({plans.Count}), total inserted={inserted}");
        plans.Clear();
    }

    using var cursor = await collection.FindAsync(
        FilterDefinition<BsonDocument>.Empty,
        new FindOptions<BsonDocument> { BatchSize = batchSize });

    while (await cursor.MoveNextAsync())
    {
        foreach (var doc in cursor.Current)
        {
            processed++;

            var legacyId = doc.GetValue("_id").ToString();
            var userId = ResolveGuid(doc.GetValue("user", BsonNull.Value), userMap);
            if (userId == Guid.Empty)
            {
                skippedMissingUser++;
                continue;
            }

            var id = Guid.NewGuid();
            planMap[legacyId] = id;

            plans.Add(new Plan
            {
                Id = id,
                LegacyMongoId = legacyId,
                UserId = userId,
                Name = doc.GetValue("name", string.Empty).AsString,
                IsActive = doc.GetValue("isActive", BsonBoolean.True).ToBoolean(),
                CreatedAt = ToPolandOffset(doc.GetValue("createdAt", BsonNull.Value)),
                UpdatedAt = ToPolandOffset(doc.GetValue("updatedAt", BsonNull.Value))
            });

            if (plans.Count >= batchSize)
            {
                await Flush();
            }
        }
    }

    await Flush();

    if (processed == 0)
    {
        Log("MigratePlans: skipped (no documents).");
        return;
    }

    Log($"MigratePlans: processed={processed}, inserted={inserted}, skippedMissingUser={skippedMissingUser}");
}

async Task MigratePlanDays()
{
    var collection = mongoDb.GetCollection<BsonDocument>("plandays");
    var processed = 0;
    var insertedPlanDays = 0;
    var insertedExercises = 0;
    var skippedMissingPlan = 0;
    var skippedMissingExercise = 0;

    var planDays = new List<PlanDay>(batchSize);
    var planDayExercises = new List<PlanDayExercise>(batchSize);

    async Task Flush()
    {
        if (planDays.Count == 0 && planDayExercises.Count == 0)
        {
            return;
        }

        if (planDays.Count > 0)
        {
            await dbContext.PlanDays.AddRangeAsync(planDays);
        }

        if (planDayExercises.Count > 0)
        {
            await dbContext.PlanDayExercises.AddRangeAsync(planDayExercises);
        }

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        insertedPlanDays += planDays.Count;
        insertedExercises += planDayExercises.Count;
        Log($"MigratePlanDays: saved batch (planDays={planDays.Count}, exercises={planDayExercises.Count}), total planDays={insertedPlanDays}, total exercises={insertedExercises}");

        planDays.Clear();
        planDayExercises.Clear();
    }

    using var cursor = await collection.FindAsync(
        FilterDefinition<BsonDocument>.Empty,
        new FindOptions<BsonDocument> { BatchSize = batchSize });

    while (await cursor.MoveNextAsync())
    {
        foreach (var doc in cursor.Current)
        {
            processed++;

            var legacyId = doc.GetValue("_id").ToString();
            var planId = ResolveGuid(doc.GetValue("plan", BsonNull.Value), planMap);
            if (planId == Guid.Empty)
            {
                skippedMissingPlan++;
                continue;
            }

            var id = Guid.NewGuid();
            planDayMap[legacyId] = id;

            var planDay = new PlanDay
            {
                Id = id,
                LegacyMongoId = legacyId,
                PlanId = planId,
                Name = doc.GetValue("name", string.Empty).AsString,
                IsDeleted = doc.GetValue("isDeleted", BsonBoolean.False).ToBoolean(),
                CreatedAt = ToPolandOffset(doc.GetValue("createdAt", BsonNull.Value)),
                UpdatedAt = ToPolandOffset(doc.GetValue("updatedAt", BsonNull.Value))
            };

            planDays.Add(planDay);

            if (doc.TryGetValue("exercises", out var exercisesValue) && exercisesValue.IsBsonArray)
            {
                foreach (var exerciseDoc in exercisesValue.AsBsonArray)
                {
                    if (!exerciseDoc.IsBsonDocument)
                    {
                        continue;
                    }

                    var exerciseDocument = exerciseDoc.AsBsonDocument;
                    var exerciseId = ResolveGuid(exerciseDocument.GetValue("exercise", BsonNull.Value), exerciseMap);
                    if (exerciseId == Guid.Empty)
                    {
                        skippedMissingExercise++;
                        continue;
                    }

                    planDayExercises.Add(new PlanDayExercise
                    {
                        Id = Guid.NewGuid(),
                        PlanDayId = planDay.Id,
                        ExerciseId = exerciseId,
                        Series = exerciseDocument.GetValue("series", 0).ToInt32(),
                        Reps = exerciseDocument.GetValue("reps", string.Empty).AsString
                    });
                }
            }

            if (planDays.Count >= batchSize)
            {
                await Flush();
            }
        }
    }

    await Flush();

    if (processed == 0)
    {
        Log("MigratePlanDays: skipped (no documents).");
        return;
    }

    Log($"MigratePlanDays: processed={processed}, planDays={insertedPlanDays}, exercises={insertedExercises}, skippedMissingPlan={skippedMissingPlan}, skippedMissingExercise={skippedMissingExercise}");
}

async Task MigrateTrainings()
{
    var collection = mongoDb.GetCollection<BsonDocument>("trainings");
    var processed = 0;
    var inserted = 0;
    var skippedMissingRefs = 0;
    var trainings = new List<Training>(batchSize);

    async Task Flush()
    {
        if (trainings.Count == 0)
        {
            return;
        }

        await dbContext.Trainings.AddRangeAsync(trainings);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        inserted += trainings.Count;
        Log($"MigrateTrainings: saved batch ({trainings.Count}), total inserted={inserted}");
        trainings.Clear();
    }

    using var cursor = await collection.FindAsync(
        FilterDefinition<BsonDocument>.Empty,
        new FindOptions<BsonDocument> { BatchSize = batchSize });

    while (await cursor.MoveNextAsync())
    {
        foreach (var doc in cursor.Current)
        {
            processed++;

            var legacyId = doc.GetValue("_id").ToString();
            var userId = ResolveGuid(doc.GetValue("user", BsonNull.Value), userMap);
            var typeId = ResolveGuid(doc.GetValue("type", BsonNull.Value), planDayMap);
            var gymId = ResolveGuid(doc.GetValue("gym", BsonNull.Value), gymMap);
            if (userId == Guid.Empty || typeId == Guid.Empty || gymId == Guid.Empty)
            {
                skippedMissingRefs++;
                continue;
            }

            var id = Guid.NewGuid();
            trainingMap[legacyId] = id;

            trainings.Add(new Training
            {
                Id = id,
                LegacyMongoId = legacyId,
                UserId = userId,
                TypePlanDayId = typeId,
                GymId = gymId,
                CreatedAt = ToPolandOffset(doc.GetValue("createdAt", BsonNull.Value)),
                UpdatedAt = ToPolandOffset(doc.GetValue("updatedAt", BsonNull.Value))
            });

            if (trainings.Count >= batchSize)
            {
                await Flush();
            }
        }
    }

    await Flush();

    if (processed == 0)
    {
        Log("MigrateTrainings: skipped (no documents).");
        return;
    }

    Log($"MigrateTrainings: processed={processed}, inserted={inserted}, skippedMissingRefs={skippedMissingRefs}");
}

async Task MigrateExerciseScores()
{
    var collection = mongoDb.GetCollection<BsonDocument>("exercisescores");
    var processed = 0;
    var inserted = 0;
    var skippedMissingRefs = 0;
    var scores = new List<ExerciseScore>(batchSize);

    async Task Flush()
    {
        if (scores.Count == 0)
        {
            return;
        }

        await dbContext.ExerciseScores.AddRangeAsync(scores);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        inserted += scores.Count;
        Log($"MigrateExerciseScores: saved batch ({scores.Count}), total inserted={inserted}");
        scores.Clear();
    }

    using var cursor = await collection.FindAsync(
        FilterDefinition<BsonDocument>.Empty,
        new FindOptions<BsonDocument> { BatchSize = batchSize });

    while (await cursor.MoveNextAsync())
    {
        foreach (var doc in cursor.Current)
        {
            processed++;

            var legacyId = doc.GetValue("_id").ToString();
            var userId = ResolveGuid(doc.GetValue("user", BsonNull.Value), userMap);
            var exerciseId = ResolveGuid(doc.GetValue("exercise", BsonNull.Value), exerciseMap);
            var trainingId = ResolveGuid(doc.GetValue("training", BsonNull.Value), trainingMap);
            if (userId == Guid.Empty || exerciseId == Guid.Empty || trainingId == Guid.Empty)
            {
                skippedMissingRefs++;
                continue;
            }

            var id = Guid.NewGuid();
            exerciseScoreMap[legacyId] = id;

            var unitRaw = doc.GetValue("unit", "kg").AsString;
            var unit = unitRaw == "lbs" ? WeightUnits.Pounds : WeightUnits.Kilograms;

            scores.Add(new ExerciseScore
            {
                Id = id,
                LegacyMongoId = legacyId,
                UserId = userId,
                ExerciseId = exerciseId,
                TrainingId = trainingId,
                Reps = doc.GetValue("reps", 0).ToInt32(),
                Series = doc.GetValue("series", 0).ToInt32(),
                Weight = doc.GetValue("weight", 0).ToDouble(),
                Unit = unit,
                CreatedAt = ToPolandOffset(doc.GetValue("createdAt", BsonNull.Value)),
                UpdatedAt = ToPolandOffset(doc.GetValue("updatedAt", BsonNull.Value))
            });

            if (scores.Count >= batchSize)
            {
                await Flush();
            }
        }
    }

    await Flush();

    if (processed == 0)
    {
        Log("MigrateExerciseScores: skipped (no documents).");
        return;
    }

    Log($"MigrateExerciseScores: processed={processed}, inserted={inserted}, skippedMissingRefs={skippedMissingRefs}");
}

async Task MigrateTrainingExerciseScores()
{
    var collection = mongoDb.GetCollection<BsonDocument>("trainings");
    var processedTrainings = 0;
    var inserted = 0;
    var skippedMissingTraining = 0;
    var skippedMissingScore = 0;
    var scores = new List<TrainingExerciseScore>(batchSize);

    async Task Flush()
    {
        if (scores.Count == 0)
        {
            return;
        }

        await dbContext.TrainingExerciseScores.AddRangeAsync(scores);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        inserted += scores.Count;
        Log($"MigrateTrainingExerciseScores: saved batch ({scores.Count}), total inserted={inserted}");
        scores.Clear();
    }

    using var cursor = await collection.FindAsync(
        FilterDefinition<BsonDocument>.Empty,
        new FindOptions<BsonDocument> { BatchSize = batchSize });

    while (await cursor.MoveNextAsync())
    {
        foreach (var doc in cursor.Current)
        {
            processedTrainings++;

            var legacyId = doc.GetValue("_id").ToString();
            if (!trainingMap.TryGetValue(legacyId, out var trainingId))
            {
                skippedMissingTraining++;
                continue;
            }

            if (!doc.TryGetValue("exercises", out var exercisesValue) || !exercisesValue.IsBsonArray)
            {
                continue;
            }

            foreach (var exerciseDoc in exercisesValue.AsBsonArray)
            {
                if (!exerciseDoc.IsBsonDocument)
                {
                    continue;
                }

                var exerciseDocument = exerciseDoc.AsBsonDocument;
                var exerciseScoreLegacy = exerciseDocument.GetValue("exerciseScoreId", BsonNull.Value);
                if (exerciseScoreLegacy.IsBsonNull)
                {
                    continue;
                }

                var legacyScoreId = exerciseScoreLegacy.ToString();
                if (!exerciseScoreMap.TryGetValue(legacyScoreId, out var scoreId))
                {
                    skippedMissingScore++;
                    continue;
                }

                scores.Add(new TrainingExerciseScore
                {
                    Id = Guid.NewGuid(),
                    TrainingId = trainingId,
                    ExerciseScoreId = scoreId
                });

                if (scores.Count >= batchSize)
                {
                    await Flush();
                }
            }
        }
    }

    await Flush();

    if (processedTrainings == 0)
    {
        Log("MigrateTrainingExerciseScores: skipped (no documents).");
        return;
    }

    Log($"MigrateTrainingExerciseScores: processedTrainings={processedTrainings}, inserted={inserted}, skippedMissingTraining={skippedMissingTraining}, skippedMissingScore={skippedMissingScore}");
}

async Task MigrateMeasurements()
{
    var collection = mongoDb.GetCollection<BsonDocument>("measurements");
    var processed = 0;
    var inserted = 0;
    var skippedMissingUser = 0;
    var measurements = new List<Measurement>(batchSize);

    async Task Flush()
    {
        if (measurements.Count == 0)
        {
            return;
        }

        await dbContext.Measurements.AddRangeAsync(measurements);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        inserted += measurements.Count;
        Log($"MigrateMeasurements: saved batch ({measurements.Count}), total inserted={inserted}");
        measurements.Clear();
    }

    using var cursor = await collection.FindAsync(
        FilterDefinition<BsonDocument>.Empty,
        new FindOptions<BsonDocument> { BatchSize = batchSize });

    while (await cursor.MoveNextAsync())
    {
        foreach (var doc in cursor.Current)
        {
            processed++;

            var legacyId = doc.GetValue("_id").ToString();
            var userId = ResolveGuid(doc.GetValue("user", BsonNull.Value), userMap);
            if (userId == Guid.Empty)
            {
                skippedMissingUser++;
                continue;
            }

            var id = Guid.NewGuid();
            measurementMap[legacyId] = id;

            Enum.TryParse<BodyParts>(doc.GetValue("bodyPart", string.Empty).AsString, out var bodyPart);

            measurements.Add(new Measurement
            {
                Id = id,
                LegacyMongoId = legacyId,
                UserId = userId,
                BodyPart = bodyPart,
                Unit = doc.GetValue("unit", string.Empty).AsString,
                Value = doc.GetValue("value", 0).ToDouble(),
                CreatedAt = ToPolandOffset(doc.GetValue("createdAt", BsonNull.Value)),
                UpdatedAt = ToPolandOffset(doc.GetValue("updatedAt", BsonNull.Value))
            });

            if (measurements.Count >= batchSize)
            {
                await Flush();
            }
        }
    }

    await Flush();

    if (processed == 0)
    {
        Log("MigrateMeasurements: skipped (no documents).");
        return;
    }

    Log($"MigrateMeasurements: processed={processed}, inserted={inserted}, skippedMissingUser={skippedMissingUser}");
}

async Task MigrateMainRecords()
{
    var collection = mongoDb.GetCollection<BsonDocument>("mainrecords");
    var processed = 0;
    var inserted = 0;
    var skippedMissingRefs = 0;
    var records = new List<MainRecord>(batchSize);

    async Task Flush()
    {
        if (records.Count == 0)
        {
            return;
        }

        await dbContext.MainRecords.AddRangeAsync(records);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        inserted += records.Count;
        Log($"MigrateMainRecords: saved batch ({records.Count}), total inserted={inserted}");
        records.Clear();
    }

    using var cursor = await collection.FindAsync(
        FilterDefinition<BsonDocument>.Empty,
        new FindOptions<BsonDocument> { BatchSize = batchSize });

    while (await cursor.MoveNextAsync())
    {
        foreach (var doc in cursor.Current)
        {
            processed++;

            var legacyId = doc.GetValue("_id").ToString();
            var userId = ResolveGuid(doc.GetValue("user", BsonNull.Value), userMap);
            var exerciseId = ResolveGuid(doc.GetValue("exercise", BsonNull.Value), exerciseMap);
            if (userId == Guid.Empty || exerciseId == Guid.Empty)
            {
                skippedMissingRefs++;
                continue;
            }

            var id = Guid.NewGuid();
            mainRecordMap[legacyId] = id;

            var unitRaw = doc.GetValue("unit", "kg").AsString;
            var unit = unitRaw == "lbs" ? WeightUnits.Pounds : WeightUnits.Kilograms;

            records.Add(new MainRecord
            {
                Id = id,
                LegacyMongoId = legacyId,
                UserId = userId,
                ExerciseId = exerciseId,
                Weight = doc.GetValue("weight", 0).ToDouble(),
                Unit = unit,
                Date = ToPolandOffset(doc.GetValue("date", BsonNull.Value)),
                CreatedAt = ToPolandOffset(doc.GetValue("createdAt", BsonNull.Value)),
                UpdatedAt = ToPolandOffset(doc.GetValue("updatedAt", BsonNull.Value))
            });

            if (records.Count >= batchSize)
            {
                await Flush();
            }
        }
    }

    await Flush();

    if (processed == 0)
    {
        Log("MigrateMainRecords: skipped (no documents).");
        return;
    }

    Log($"MigrateMainRecords: processed={processed}, inserted={inserted}, skippedMissingRefs={skippedMissingRefs}");
}

async Task MigrateEloRegistries()
{
    var collection = mongoDb.GetCollection<BsonDocument>("eloregistries");
    var processed = 0;
    var inserted = 0;
    var skippedMissingUser = 0;
    var entries = new List<EloRegistry>(batchSize);

    async Task Flush()
    {
        if (entries.Count == 0)
        {
            return;
        }

        await dbContext.EloRegistries.AddRangeAsync(entries);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        inserted += entries.Count;
        Log($"MigrateEloRegistries: saved batch ({entries.Count}), total inserted={inserted}");
        entries.Clear();
    }

    using var cursor = await collection.FindAsync(
        FilterDefinition<BsonDocument>.Empty,
        new FindOptions<BsonDocument> { BatchSize = batchSize });

    while (await cursor.MoveNextAsync())
    {
        foreach (var doc in cursor.Current)
        {
            processed++;

            var legacyId = doc.GetValue("_id").ToString();
            var userId = ResolveGuid(doc.GetValue("user", BsonNull.Value), userMap);
            if (userId == Guid.Empty)
            {
                skippedMissingUser++;
                continue;
            }

            var id = Guid.NewGuid();
            eloRegistryMap[legacyId] = id;

            var trainingId = ResolveGuid(doc.GetValue("training", BsonNull.Value), trainingMap);

            entries.Add(new EloRegistry
            {
                Id = id,
                LegacyMongoId = legacyId,
                UserId = userId,
                TrainingId = trainingId == Guid.Empty ? null : trainingId,
                Date = ToPolandOffset(doc.GetValue("date", BsonNull.Value)),
                Elo = doc.GetValue("elo", 0).ToInt32(),
                CreatedAt = ToPolandOffset(doc.GetValue("createdAt", BsonNull.Value)),
                UpdatedAt = ToPolandOffset(doc.GetValue("updatedAt", BsonNull.Value))
            });

            if (entries.Count >= batchSize)
            {
                await Flush();
            }
        }
    }

    await Flush();

    if (processed == 0)
    {
        Log("MigrateEloRegistries: skipped (no documents).");
        return;
    }

    Log($"MigrateEloRegistries: processed={processed}, inserted={inserted}, skippedMissingUser={skippedMissingUser}");
}

async Task MigrateAppConfigs()
{
    var collection = mongoDb.GetCollection<BsonDocument>("appconfigs");
    // Intentionally no per-document logs here (keeps console readable).

    var processed = 0;
    var inserted = 0;
    var configs = new List<AppConfig>(batchSize);

    async Task Flush()
    {
        if (configs.Count == 0)
        {
            return;
        }

        await dbContext.AppConfigs.AddRangeAsync(configs);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        inserted += configs.Count;
        Log($"MigrateAppConfigs: saved batch ({configs.Count}), total inserted={inserted}");
        configs.Clear();
    }

    using var cursor = await collection.FindAsync(
        FilterDefinition<BsonDocument>.Empty,
        new FindOptions<BsonDocument> { BatchSize = batchSize });

    while (await cursor.MoveNextAsync())
    {
        foreach (var doc in cursor.Current)
        {
            processed++;

            var legacyId = doc.GetValue("_id").ToString();
            var id = Guid.NewGuid();
            appConfigMap[legacyId] = id;

            var platformRaw = doc.GetValue("platform", string.Empty).AsString;
            if (!Enum.TryParse(platformRaw, true, out Platforms platform))
            {
                Log($"MigrateAppConfigs: unknown platform '{platformRaw}' for legacyId={legacyId}, skipping.");
                continue;
            }

            configs.Add(new AppConfig
            {
                Id = id,
                LegacyMongoId = legacyId,
                Platform = platform,
                MinRequiredVersion = doc.GetValue("minRequiredVersion", string.Empty).AsString,
                LatestVersion = doc.GetValue("latestVersion", string.Empty).AsString,
                ForceUpdate = doc.GetValue("forceUpdate", BsonBoolean.False).ToBoolean(),
                UpdateUrl = doc.GetValue("updateUrl", string.Empty).AsString,
                ReleaseNotes = doc.GetValue("releaseNotes", BsonNull.Value).IsBsonNull ? null : doc.GetValue("releaseNotes").AsString,
                CreatedAt = ToPolandOffset(doc.GetValue("createdAt", BsonNull.Value)),
                UpdatedAt = ToPolandOffset(doc.GetValue("updatedAt", BsonNull.Value))
            });

            if (configs.Count >= batchSize)
            {
                await Flush();
            }
        }
    }

    await Flush();

    if (processed == 0)
    {
        Log("MigrateAppConfigs: skipped (no documents).");
        return;
    }

    Log($"MigrateAppConfigs: processed={processed}, inserted={inserted}");
}

static Guid ResolveGuid(BsonValue value, Dictionary<string, Guid> map)
{
    if (value == BsonNull.Value)
    {
        return Guid.Empty;
    }

    var legacyId = value.ToString();
    return map.TryGetValue(legacyId, out var id) ? id : Guid.Empty;
}

static TimeZoneInfo GetPolandTimeZone()
{
    try
    {
        return TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
    }
    catch (TimeZoneNotFoundException)
    {
        return TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
    }
}

static DateTimeOffset ToPolandOffset(BsonValue value)
{
    var timeZone = GetPolandTimeZone();
    if (value == BsonNull.Value)
    {
        var nowUtc = DateTime.UtcNow;
        return new DateTimeOffset(nowUtc, TimeSpan.Zero);
    }

    var raw = value.ToUniversalTime();
    var unspecified = DateTime.SpecifyKind(raw, DateTimeKind.Unspecified);
    var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
    return new DateTimeOffset(utc, TimeSpan.Zero);
}

static void Log(string message)
{
    Console.WriteLine($"[{DateTimeOffset.UtcNow:O}] {message}");
}

static async Task RunStep(string name, Func<Task> action)
{
    Log($"{name}: start");
    var sw = Stopwatch.StartNew();
    try
    {
        await action();
        Log($"{name}: done ({sw.Elapsed.TotalSeconds:0.000}s)");
    }
    catch (Exception ex)
    {
        Log($"{name}: failed after {sw.Elapsed.TotalSeconds:0.000}s\n{ex}");
        throw;
    }
}
