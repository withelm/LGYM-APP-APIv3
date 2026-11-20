using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Coaching.Errors;
using LgymApi.Application.Nutrition.Errors;
using LgymApi.Application.Nutrition.DietPlans;
using LgymApi.Application.Nutrition.DietPlans.Models;
using LgymApi.Application.Nutrition.Supplementation;
using LgymApi.Application.Nutrition.Supplementation.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using NUnit.Framework;

namespace LgymApi.UnitTests.Nutrition;

[TestFixture]
public sealed class NutritionRulesAndProjectorTests
{
    [Test]
    public void DietRules_NormalizeAndApplyTheDietSpecificAccessOrder()
    {
        var data = new DietPlanUpsertData(
            " Cut ",
            new DateOnly(2026, 7, 1),
            null,
            2200,
            null,
            null,
            null,
            " ",
            false,
            [new DietMealInput(" Dinner ", -1, " rice ", null, null, null, null)]);

        var normalized = DietPlanRules.Normalize(data);

        Assert.That(DietPlanRules.GetUpsertError(data), Is.Null);
        Assert.That(normalized.Name, Is.EqualTo("Cut"));
        Assert.That(normalized.Notes, Is.Null);
        var assertedValue190 = (normalized.Meals).ToArray();
        Assert.That(assertedValue190, Has.Exactly(1).Items);
        Assert.That(assertedValue190.Single(), Is.EqualTo(new DietMealInput("Dinner", 0, "rice", null, null, null, null)));
        var actorId = Id<User>.New();
        var actor = NutritionAuthorizationTestFactory.CreateActor(actorId);
        Assert.That(DietPlanAccess.GetAccessError(NutritionAuthorizationTestFactory.CreateDietCoordinator(), actor, DietPlanAction.Read, actorId, Id<User>.New(), false, true), Is.TypeOf<TrainerRelationshipForbiddenError>());
        Assert.That(DietPlanAccess.GetAccessError(NutritionAuthorizationTestFactory.CreateDietCoordinator(), actor, DietPlanAction.Read, actorId, Id<User>.New(), true, false), Is.TypeOf<NotFoundError>());
    }

    [Test]
    public void SupplementationRules_PreserveValidationNormalizationAndAccessPrecedence()
    {
        var data = new SupplementPlanUpsertData(
            " Daily ",
            " before bed ",
            [
                new SupplementPlanItemInput(" Zinc ", " 20 mg ", "09:00", 127, 2),
                new SupplementPlanItemInput(" Magnesium ", " 400 mg ", "08:00", 127, 2)
            ]);

        var normalized = SupplementationRules.Normalize(data);

        Assert.That(SupplementationRules.GetUpsertError(data), Is.Null);
        Assert.That(normalized.Items.Select(item => item.SupplementName), Is.EqualTo(new[] { "Magnesium", "Zinc" }));
        Assert.That(SupplementationRules.GetComplianceRangeError(new DateOnly(2026, 7, 2), new DateOnly(2026, 7, 1)), Is.TypeOf<InvalidSupplementationError>());
        var actorId = Id<User>.New();
        var actor = NutritionAuthorizationTestFactory.CreateActor(actorId);
        Assert.That(SupplementationAccess.GetTrainerAccessError(NutritionAuthorizationTestFactory.CreateSupplementationCoordinator(), actor, false, false, default), Is.TypeOf<SupplementationForbiddenError>());
        Assert.That(SupplementationAccess.GetTrainerAccessError(NutritionAuthorizationTestFactory.CreateSupplementationCoordinator(), actor, true, true, default), Is.TypeOf<InvalidSupplementationError>());
        Assert.That(SupplementationAccess.GetTrainerAccessError(NutritionAuthorizationTestFactory.CreateSupplementationCoordinator(), actor, true, false, Id<User>.New()), Is.TypeOf<SupplementationNotFoundError>());
    }

    [Test]
    public void NutritionAccessHelpers_DoNotExposeTheCoachingDecisionType()
    {
        var forbiddenType = "LgymApi.Application.Coaching.Contracts.Access.CoachingRelationshipAccessDecision";
        var parameterTypes = new[] { typeof(DietPlanAccess), typeof(SupplementationAccess) }
            .SelectMany(type => type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType.FullName);

        Assert.That(parameterTypes, Does.Not.Contain(forbiddenType));
    }

    [Test]
    public void SupplementProjectors_PreserveMondayOrderTakenStateAndInclusiveRounding()
    {
        var traineeId = Id<User>.New();
        var firstItemId = Id<SupplementPlanItem>.New();
        var secondItemId = Id<SupplementPlanItem>.New();
        var date = new DateOnly(2026, 7, 6);
        var plan = new SupplementPlan
        {
            Items =
            [
                new SupplementPlanItem
                {
                    Id = secondItemId,
                    SupplementName = "Zinc",
                    Dosage = "20 mg",
                    DaysOfWeekMask = DaysOfWeekSet.Monday,
                    TimeOfDay = new TimeSpan(9, 0, 0),
                    Order = 1,
                    CreatedAt = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero)
                },
                new SupplementPlanItem
                {
                    Id = firstItemId,
                    SupplementName = "Magnesium",
                    Dosage = "400 mg",
                    DaysOfWeekMask = DaysOfWeekSet.Monday,
                    TimeOfDay = new TimeSpan(8, 0, 0),
                    Order = 1,
                    CreatedAt = new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero)
                }
            ]
        };
        var logs = new[]
        {
            new SupplementIntakeLog
            {
                TraineeId = traineeId,
                PlanItemId = firstItemId,
                IntakeDate = date,
                TakenAt = new DateTimeOffset(2026, 7, 6, 8, 5, 0, TimeSpan.Zero)
            }
        };

        var schedule = SupplementScheduleProjector.Project(plan, date, logs);
        var compliance = SupplementComplianceProjector.Project(traineeId, plan.Items, date, date.AddDays(2), logs);

        Assert.That(schedule.Select(entry => entry.SupplementName), Is.EqualTo(new[] { "Magnesium", "Zinc" }));
        Assert.That(schedule[0].Taken, Is.True);
        Assert.That(schedule[0].TakenAt, Is.EqualTo(logs[0].TakenAt));
        Assert.That(schedule[1].Taken, Is.False);
        Assert.That(compliance.PlannedDoses, Is.EqualTo(2));
        Assert.That(compliance.TakenDoses, Is.EqualTo(1));
        Assert.That(compliance.AdherenceRate, Is.EqualTo(50));
    }
}
