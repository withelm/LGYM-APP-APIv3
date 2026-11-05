using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;

namespace LgymApi.TestUtils;

public enum AuthQ01RelationshipScenario
{
    Initial,
    Chain
}

public sealed record AuthQ01SeededFixture(
    AuthQ01Fixture Definition,
    IReadOnlyDictionary<string, User> Actors,
    IReadOnlyDictionary<string, Plan> Plans);

public static class AuthQ01Seeder
{
    public static async Task<AuthQ01SeededFixture> SeedAsync(
        AppDbContext dbContext,
        AuthQ01RelationshipScenario scenario = AuthQ01RelationshipScenario.Initial,
        CancellationToken cancellationToken = default)
    {
        var definition = AuthQ01FixtureLoader.Load();
        var actors = new Dictionary<string, User>(StringComparer.Ordinal);
        foreach (var actor in definition.Actors)
        {
            actors.Add(actor.Alias, await TestDataFactory.SeedUserAsync(
                dbContext,
                name: actor.Alias,
                email: actor.Email,
                password: "password123",
                isAdmin: actor.Roles.Contains(AuthConstants.Roles.Admin, StringComparer.Ordinal),
                cancellationToken: cancellationToken,
                userId: Id<User>.TryParse(actor.Id, out var id) ? id : throw new InvalidOperationException($"Invalid actor ID '{actor.Id}'."),
                timestamp: definition.Timeline.ReferenceNow,
                additionalRoles: actor.Roles));
        }

        var plans = new Dictionary<string, Plan>(StringComparer.Ordinal);
        foreach (var planDefinition in definition.Plans)
        {
            var plan = new Plan
            {
                Id = Id<Plan>.TryParse(planDefinition.Id, out var planId)
                    ? planId
                    : throw new InvalidOperationException($"Invalid plan ID '{planDefinition.Id}'."),
                UserId = actors[planDefinition.Owner].Id,
                Name = planDefinition.Name,
                IsActive = planDefinition.Owner is "U" or "B" or "C",
                IsDeleted = false
            };
            dbContext.Plans.Add(plan);
            plans.Add(planDefinition.Id, plan);
        }

        var relationships = scenario switch
        {
            AuthQ01RelationshipScenario.Initial => definition.RelationshipScenarios.Initial,
            AuthQ01RelationshipScenario.Chain => definition.RelationshipScenarios.Chain,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        foreach (var relationship in relationships)
        {
            dbContext.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = actors[relationship.Trainer].Id,
                TraineeId = actors[relationship.Trainee].Id
            });
        }

        return new AuthQ01SeededFixture(definition, actors, plans);
    }
}
