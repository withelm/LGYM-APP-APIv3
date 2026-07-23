using FluentAssertions;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Repositories;
using LgymApi.Application.TrainingPlanning;
using LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;
using LgymApi.Application.TrainingPlanning.Plan.ActivePlanPointer;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
internal sealed class PostgreSqlManagedPlanTransactionTests : PostgreSqlIntegrationTestBase
{
    [Test]
    public async Task AssignManagedPlanAsync_WhenSaveFlushesThenFails_RollsBackCloneAndActivePointer()
    {
        var trainer = await SeedUserAsync("managed-plan-trainer", "managed-plan-trainer@example.com");
        var trainee = await SeedUserAsync("managed-plan-trainee", "managed-plan-trainee@example.com");
        var cloneId = Id<Plan>.New();

        await using (var serviceScope = Factory.Services.CreateAsyncScope())
        {
            var database = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var template = new Plan
            {
                Id = Id<Plan>.New(),
                UserId = trainer.Id,
                Name = "Trainer template",
                IsActive = false
            };
            database.Plans.Add(template);
            await database.SaveChangesAsync();

            var clonedPlan = new Plan
            {
                Id = cloneId,
                UserId = trainee.Id,
                Name = "Cloned template",
                IsActive = true
            };
            var planRepository = Substitute.For<IPlanRepository>();
            planRepository.FindByIdAsync(template.Id, Arg.Any<CancellationToken>()).Returns(template);
            planRepository.ClonePlanAsync(template.Id, trainee.Id, true, Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    database.Plans.Add(clonedPlan);
                    return Task.FromResult(clonedPlan);
                });

            var accountReadService = Substitute.For<IAccountReadService>();
            accountReadService.GetByIdAsync(trainee.Id, Arg.Any<CancellationToken>())
                .Returns(new AccountReadModel(trainee.Id, trainee.Name, trainee.Email.Value, null, "en", "UTC"));

            var services = new ServiceCollection();
            services.AddTrainingPlanningModule();
            services.AddScoped<IPlanRepository>(_ => planRepository);
            services.AddScoped<IActivePlanPointerStore>(_ => new ActivePlanPointerStore(database));
            services.AddScoped<IAccountReadService>(_ => accountReadService);
            services.AddScoped<IUnitOfWork>(_ => new FlushThenThrowUnitOfWork(database));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<IAssignManagedPlanUseCase>();

            Func<Task> action = () => useCase.ExecuteAsync(new AssignManagedPlanCommand(trainer.Id, trainee.Id, template.Id));

            await action.Should().ThrowAsync<InvalidOperationException>();
        }

        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedClone = await verificationDatabase.Plans.SingleOrDefaultAsync(plan => plan.Id == cloneId);
        var persistedTrainee = await verificationDatabase.Users.SingleAsync(user => user.Id == trainee.Id);

        persistedClone.Should().BeNull();
        persistedTrainee.PlanId.Should().BeNull();
    }

    private sealed class FlushThenThrowUnitOfWork : IUnitOfWork
    {
        private readonly EfUnitOfWork _inner;

        public FlushThenThrowUnitOfWork(AppDbContext database)
        {
            _inner = new EfUnitOfWork(database);
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _inner.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Forced post-flush failure.");
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return _inner.BeginTransactionAsync(cancellationToken);
        }
    }
}
