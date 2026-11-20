using LgymApi.Application.Coaching.ManagedPlans.GetActive;
using LgymApi.Application.Coaching.ManagedPlans.List;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Coaching.Errors;
using LgymApi.Application.TrainingPlanning.Errors;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using LgymApi.Identity.Contracts;
using LgymApi.TrainingPlanning.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoachingAssign = LgymApi.Application.Coaching.ManagedPlans.Assign;
using CoachingCreate = LgymApi.Application.Coaching.ManagedPlans.Create;
using CoachingDelete = LgymApi.Application.Coaching.ManagedPlans.Delete;
using CoachingUnassign = LgymApi.Application.Coaching.ManagedPlans.Unassign;
using CoachingUpdate = LgymApi.Application.Coaching.ManagedPlans.Update;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class CoachingManagedPlanSliceIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task ManagedPlanSlices_PreserveTemplateCloneActivationPointerAndActiveReadSemantics()
    {
        var trainer = await SeedUserAsync("slice-plan-trainer", "slice-plan-trainer@example.test");
        var trainee = await SeedUserAsync("slice-plan-trainee", "slice-plan-trainee@example.test");
        await SeedRelationshipAsync(trainer.Id, trainee.Id);

        Id<Plan> templateId;
        Id<Plan> cloneId;
        using (var actionScope = Factory.Services.CreateScope())
        {
            var services = actionScope.ServiceProvider;
            var created = await services.GetRequiredService<CoachingCreate.ICreateTraineeManagedPlanUseCase>()
                .ExecuteAsync(new CoachingCreate.CreateTraineeManagedPlanCommand(
                    trainer.Id.Rebind<AccountReference>(),
                    trainee.Id.Rebind<AccountReference>(),
                    "  Template  "));
            Assert.That(created.IsSuccess, Is.True);
            Assert.That(created.Value.Name, Is.EqualTo("Template"));
            Assert.That(created.Value.IsActive, Is.False);
            templateId = created.Value.Id.Rebind<Plan>();

            var updatedTemplate = await services.GetRequiredService<CoachingUpdate.IUpdateTraineeManagedPlanUseCase>()
                .ExecuteAsync(new CoachingUpdate.UpdateTraineeManagedPlanCommand(
                    trainer.Id.Rebind<AccountReference>(),
                    trainee.Id.Rebind<AccountReference>(),
                    templateId.Rebind<PlanReference>(),
                    "  Updated template  "));
            Assert.That(updatedTemplate.Value.Name, Is.EqualTo("Updated template"));

            var beforeAssignment = await services.GetRequiredService<IListManagedPlansUseCase>()
                .ExecuteAsync(new ListManagedPlansQuery(trainer.Id.Rebind<AccountReference>(), trainee.Id.Rebind<AccountReference>()));
            Assert.That(beforeAssignment.Value, Is.Empty);

            var assignedTemplate = await services.GetRequiredService<CoachingAssign.IAssignTraineeManagedPlanUseCase>()
                .ExecuteAsync(new CoachingAssign.AssignTraineeManagedPlanCommand(
                    trainer.Id.Rebind<AccountReference>(),
                    trainee.Id.Rebind<AccountReference>(),
                    templateId.Rebind<PlanReference>()));
            Assert.That(assignedTemplate.IsSuccess, Is.True);

            var afterClone = await services.GetRequiredService<IListManagedPlansUseCase>()
                .ExecuteAsync(new ListManagedPlansQuery(trainer.Id.Rebind<AccountReference>(), trainee.Id.Rebind<AccountReference>()));
            Assert.That(afterClone.Value, Has.Exactly(1).Items);
            cloneId = afterClone.Value.Single().Id.Rebind<Plan>();
            Assert.That(cloneId, Is.Not.EqualTo(templateId));
            Assert.That(afterClone.Value.Single().IsActive, Is.True);

            var activeClone = await services.GetRequiredService<IGetActiveManagedPlanUseCase>()
                .ExecuteAsync(new GetActiveManagedPlanQuery(trainee.Id.Rebind<AccountReference>()));
            Assert.That(activeClone.Value.Id, Is.EqualTo(cloneId.Rebind<PlanReference>()));

            var updatedClone = await services.GetRequiredService<CoachingUpdate.IUpdateTraineeManagedPlanUseCase>()
                .ExecuteAsync(new CoachingUpdate.UpdateTraineeManagedPlanCommand(
                    trainer.Id.Rebind<AccountReference>(),
                    trainee.Id.Rebind<AccountReference>(),
                    cloneId.Rebind<PlanReference>(),
                    "Updated clone"));
            Assert.That(updatedClone.Value.Name, Is.EqualTo("Updated clone"));

            var unassigned = await services.GetRequiredService<CoachingUnassign.IUnassignTraineeManagedPlanUseCase>()
                .ExecuteAsync(new CoachingUnassign.UnassignTraineeManagedPlanCommand(
                    trainer.Id.Rebind<AccountReference>(),
                    trainee.Id.Rebind<AccountReference>()));
            Assert.That(unassigned.IsSuccess, Is.True);
            var noActivePlan = await services.GetRequiredService<IGetActiveManagedPlanUseCase>()
                .ExecuteAsync(new GetActiveManagedPlanQuery(trainee.Id.Rebind<AccountReference>()));
            Assert.That(noActivePlan.Error, Is.TypeOf<PlanNotFoundError>());

            var assignedExisting = await services.GetRequiredService<CoachingAssign.IAssignTraineeManagedPlanUseCase>()
                .ExecuteAsync(new CoachingAssign.AssignTraineeManagedPlanCommand(
                    trainer.Id.Rebind<AccountReference>(),
                    trainee.Id.Rebind<AccountReference>(),
                    cloneId.Rebind<PlanReference>()));
            Assert.That(assignedExisting.IsSuccess, Is.True);

            var deletedTemplate = await services.GetRequiredService<CoachingDelete.IDeleteTraineeManagedPlanUseCase>()
                .ExecuteAsync(new CoachingDelete.DeleteTraineeManagedPlanCommand(
                    trainer.Id.Rebind<AccountReference>(),
                    trainee.Id.Rebind<AccountReference>(),
                    templateId.Rebind<PlanReference>()));
            Assert.That(deletedTemplate.IsSuccess, Is.True);
        }

        using var verificationScope = Factory.Services.CreateScope();
        var database = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plans = await database.Plans.IgnoreQueryFilters()
            .Where(plan => plan.Id == templateId || plan.Id == cloneId)
            .ToListAsync();
        var persistedTemplate = plans.Single(plan => plan.Id == templateId);
        var persistedClone = plans.Single(plan => plan.Id == cloneId);

        Assert.That(persistedTemplate.UserId, Is.EqualTo(trainer.Id));
        Assert.That(persistedTemplate.IsDeleted, Is.True);
        Assert.That(persistedTemplate.IsActive, Is.False);
        Assert.That(persistedClone.UserId, Is.EqualTo(trainee.Id));
        Assert.That(persistedClone.Name, Is.EqualTo("Updated clone"));
        Assert.That(persistedClone.IsActive, Is.True);
        var assertedValue9 = (plans.Where(plan => plan.UserId == trainee.Id && plan.IsActive && !plan.IsDeleted)
                    .Select(plan => plan.Id)).ToArray();
        Assert.That(assertedValue9, Has.Exactly(1).Items);
        Assert.That(assertedValue9.Single(), Is.EqualTo(cloneId));
    }

    [Test]
    public async Task TrainerManagedPlanSlices_RejectNonTrainerForeignRelationshipAndEmptyTraineeWithoutWrites()
    {
        var trainer = await SeedUserAsync("slice-plan-access-trainer", "slice-plan-access-trainer@example.test");
        var trainee = await SeedUserAsync("slice-plan-access-trainee", "slice-plan-access-trainee@example.test");
        var nonTrainer = await SeedUserAsync("slice-plan-access-user", "slice-plan-access-user@example.test");
        await SeedTrainerRoleAsync(trainer.Id);

        using var actionScope = Factory.Services.CreateScope();
        var services = actionScope.ServiceProvider;
        var forbidden = await services.GetRequiredService<CoachingCreate.ICreateTraineeManagedPlanUseCase>()
            .ExecuteAsync(new CoachingCreate.CreateTraineeManagedPlanCommand(
                nonTrainer.Id.Rebind<AccountReference>(),
                trainee.Id.Rebind<AccountReference>(),
                "Nope"));
        var foreign = await services.GetRequiredService<IListManagedPlansUseCase>()
            .ExecuteAsync(new ListManagedPlansQuery(trainer.Id.Rebind<AccountReference>(), trainee.Id.Rebind<AccountReference>()));
        var invalid = await services.GetRequiredService<CoachingCreate.ICreateTraineeManagedPlanUseCase>()
            .ExecuteAsync(new CoachingCreate.CreateTraineeManagedPlanCommand(
                trainer.Id.Rebind<AccountReference>(),
                Id<AccountReference>.Empty,
                "Nope"));

        Assert.That(forbidden.Error, Is.TypeOf<TrainerRelationshipForbiddenError>());
        Assert.That(foreign.Error, Is.TypeOf<TrainerRelationshipNotFoundError>());
        Assert.That(invalid.Error, Is.TypeOf<InvalidTrainerRelationshipError>());
        Assert.That((await services.GetRequiredService<AppDbContext>().Plans.CountAsync()), Is.EqualTo(0));
    }

    [Test]
    public async Task TrainerManagedPlanSlices_RejectForeignRelationshipForEveryMutationWithoutWrites()
    {
        var trainer = await SeedUserAsync("slice-plan-unlinked-trainer", "slice-plan-unlinked-trainer@example.test");
        var trainee = await SeedUserAsync("slice-plan-unlinked-trainee", "slice-plan-unlinked-trainee@example.test");
        await SeedTrainerRoleAsync(trainer.Id);
        var planId = Id<Plan>.New();

        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.Plans.Add(new Plan { Id = planId, UserId = trainer.Id, Name = "Template" });
            await database.SaveChangesAsync();
        }

        using var actionScope = Factory.Services.CreateScope();
        var services = actionScope.ServiceProvider;
        var trainerId = trainer.Id.Rebind<AccountReference>();
        var traineeId = trainee.Id.Rebind<AccountReference>();
        var planReference = planId.Rebind<PlanReference>();

        var results = new AppError[]
        {
            (await services.GetRequiredService<CoachingCreate.ICreateTraineeManagedPlanUseCase>()
                .ExecuteAsync(new CoachingCreate.CreateTraineeManagedPlanCommand(trainerId, traineeId, "Blocked"))).Error,
            (await services.GetRequiredService<CoachingUpdate.IUpdateTraineeManagedPlanUseCase>()
                .ExecuteAsync(new CoachingUpdate.UpdateTraineeManagedPlanCommand(trainerId, traineeId, planReference, "Blocked"))).Error,
            (await services.GetRequiredService<CoachingDelete.IDeleteTraineeManagedPlanUseCase>()
                .ExecuteAsync(new CoachingDelete.DeleteTraineeManagedPlanCommand(trainerId, traineeId, planReference))).Error,
            (await services.GetRequiredService<CoachingAssign.IAssignTraineeManagedPlanUseCase>()
                .ExecuteAsync(new CoachingAssign.AssignTraineeManagedPlanCommand(trainerId, traineeId, planReference))).Error,
            (await services.GetRequiredService<CoachingUnassign.IUnassignTraineeManagedPlanUseCase>()
                .ExecuteAsync(new CoachingUnassign.UnassignTraineeManagedPlanCommand(trainerId, traineeId))).Error
        };

        Assert.That((results).All(error => error is TrainerRelationshipNotFoundError), Is.True);
        var persistedPlan = await services.GetRequiredService<AppDbContext>().Plans.SingleAsync(plan => plan.Id == planId);
        Assert.That(persistedPlan.Name, Is.EqualTo("Template"));
        Assert.That(persistedPlan.IsDeleted, Is.False);
        Assert.That(persistedPlan.IsActive, Is.True);
    }

    [Test]
    public async Task TrainerManagedPlanSlices_PreserveOwnerValidationForeignPlanAndMissingOwnerFailures()
    {
        var trainer = await SeedUserAsync("slice-plan-failure-trainer", "slice-plan-failure-trainer@example.test");
        var trainee = await SeedUserAsync("slice-plan-failure-trainee", "slice-plan-failure-trainee@example.test");
        var missingOwner = await SeedUserAsync(
            "slice-plan-missing-owner",
            "slice-plan-missing-owner@example.test",
            isDeleted: true);
        var foreignOwner = await SeedUserAsync("slice-plan-foreign-owner", "slice-plan-foreign-owner@example.test");
        await SeedRelationshipAsync(trainer.Id, trainee.Id);
        await SeedRelationshipAsync(trainer.Id, missingOwner.Id, addTrainerRole: false);
        Id<Plan> templateId;
        Id<Plan> foreignPlanId;

        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var template = new Plan { Id = Id<Plan>.New(), UserId = trainer.Id, Name = "Template" };
            var foreignPlan = new Plan { Id = Id<Plan>.New(), UserId = foreignOwner.Id, Name = "Foreign" };
            database.Plans.AddRange(template, foreignPlan);
            await database.SaveChangesAsync();
            templateId = template.Id;
            foreignPlanId = foreignPlan.Id;
        }

        using var actionScope = Factory.Services.CreateScope();
        var services = actionScope.ServiceProvider;
        var invalidName = await services.GetRequiredService<CoachingCreate.ICreateTraineeManagedPlanUseCase>()
            .ExecuteAsync(new CoachingCreate.CreateTraineeManagedPlanCommand(
                trainer.Id.Rebind<AccountReference>(),
                trainee.Id.Rebind<AccountReference>(),
                " "));
        var invalidPlanId = await services.GetRequiredService<CoachingUpdate.IUpdateTraineeManagedPlanUseCase>()
            .ExecuteAsync(new CoachingUpdate.UpdateTraineeManagedPlanCommand(
                trainer.Id.Rebind<AccountReference>(),
                trainee.Id.Rebind<AccountReference>(),
                Id<PlanReference>.Empty,
                "Name"));
        var foreignPlanResult = await services.GetRequiredService<CoachingAssign.IAssignTraineeManagedPlanUseCase>()
            .ExecuteAsync(new CoachingAssign.AssignTraineeManagedPlanCommand(
                trainer.Id.Rebind<AccountReference>(),
                trainee.Id.Rebind<AccountReference>(),
                foreignPlanId.Rebind<PlanReference>()));
        var unavailableOwner = await services.GetRequiredService<CoachingAssign.IAssignTraineeManagedPlanUseCase>()
            .ExecuteAsync(new CoachingAssign.AssignTraineeManagedPlanCommand(
                trainer.Id.Rebind<AccountReference>(),
                missingOwner.Id.Rebind<AccountReference>(),
                templateId.Rebind<PlanReference>()));

        Assert.That(invalidName.Error, Is.TypeOf<InvalidPlanError>());
        Assert.That(invalidPlanId.Error, Is.TypeOf<InvalidPlanError>());
        Assert.That(foreignPlanResult.Error, Is.TypeOf<PlanNotFoundError>());
        Assert.That(unavailableOwner.Error, Is.TypeOf<LgymApi.Application.Coaching.Errors.TrainerRelationshipNotFoundError>());
    }

    private async Task SeedRelationshipAsync(
        Id<User> trainerId,
        Id<User> traineeId,
        bool addTrainerRole = true)
    {
        using var seedScope = Factory.Services.CreateScope();
        var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (addTrainerRole)
        {
            database.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, trainerId));
        }

        database.TrainerTraineeLinks.Add(new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId
        });
        await database.SaveChangesAsync();
    }

    private async Task SeedTrainerRoleAsync(Id<User> trainerId)
    {
        using var seedScope = Factory.Services.CreateScope();
        var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
        database.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, trainerId));
        await database.SaveChangesAsync();
    }
}
