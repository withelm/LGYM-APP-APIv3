using LgymApi.Application.Features.Plan;
using LgymApi.Application.Features.PlanDay;
using LgymApi.Application.Features.PlanDay.Models;
using LgymApi.Application.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ServiceTransactionBehaviorTests
{
    [Test]
    public async Task SetNewActivePlanAsync_WhenSuccessful_CommitsTransaction()
    {
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var currentUser = new User { Id = userId };
        var unitOfWork = new RecordingUnitOfWork();
        var planRepository = new PlanRepositoryStub
        {
            PlanToReturn = new Plan { Id = planId, UserId = userId }
        };

        var service = new PlanService(
            new UserRepositoryStub(),
            planRepository,
            new PlanDayRepositoryStub(),
            unitOfWork);

        await service.SetNewActivePlanAsync(currentUser, userId, planId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(1));
            Assert.That(unitOfWork.Transaction.CommitCalls, Is.EqualTo(1));
            Assert.That(unitOfWork.Transaction.RollbackCalls, Is.EqualTo(0));
            Assert.That(currentUser.PlanId, Is.EqualTo(planId));
            Assert.That(planRepository.SetActiveCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void SetNewActivePlanAsync_WhenSetActiveFails_RollsBackTransaction()
    {
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var currentUser = new User { Id = userId };
        var unitOfWork = new RecordingUnitOfWork();
        var planRepository = new PlanRepositoryStub
        {
            PlanToReturn = new Plan { Id = planId, UserId = userId },
            SetActiveException = new InvalidOperationException("boom")
        };

        var service = new PlanService(
            new UserRepositoryStub(),
            planRepository,
            new PlanDayRepositoryStub(),
            unitOfWork);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.SetNewActivePlanAsync(currentUser, userId, planId, CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(unitOfWork.Transaction.CommitCalls, Is.EqualTo(0));
            Assert.That(unitOfWork.Transaction.RollbackCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task UpdatePlanDayAsync_WhenSuccessful_CommitsTransaction()
    {
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var planDayId = Guid.NewGuid();
        var unitOfWork = new RecordingUnitOfWork();
        var exercisesRepository = new PlanDayExerciseRepositoryStub();

        var planRepository = new PlanRepositoryStub
        {
            PlanToReturn = new Plan { Id = planId, UserId = userId }
        };

        var planDayRepository = new PlanDayRepositoryStub
        {
            PlanDayToReturn = new PlanDay { Id = planDayId, PlanId = planId, Name = "old" }
        };

        var service = new PlanDayService(
            planRepository,
            planDayRepository,
            exercisesRepository,
            new ExerciseRepositoryStub(),
            new TrainingRepositoryStub(),
            unitOfWork);

        await service.UpdatePlanDayAsync(
            new User { Id = userId },
            planDayId.ToString(),
            "new",
            [new PlanDayExerciseInput { ExerciseId = Guid.NewGuid().ToString(), Series = 3, Reps = "8" }],
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(planDayRepository.UpdateCalls, Is.EqualTo(1));
            Assert.That(exercisesRepository.RemoveCalls, Is.EqualTo(1));
            Assert.That(exercisesRepository.AddRangeCalls, Is.EqualTo(1));
            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(1));
            Assert.That(unitOfWork.Transaction.CommitCalls, Is.EqualTo(1));
            Assert.That(unitOfWork.Transaction.RollbackCalls, Is.EqualTo(0));
        });
    }

    [Test]
    public void UpdatePlanDayAsync_WhenRemoveFails_RollsBackTransaction()
    {
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var planDayId = Guid.NewGuid();
        var unitOfWork = new RecordingUnitOfWork();
        var exercisesRepository = new PlanDayExerciseRepositoryStub
        {
            RemoveException = new InvalidOperationException("remove failed")
        };

        var service = new PlanDayService(
            new PlanRepositoryStub
            {
                PlanToReturn = new Plan { Id = planId, UserId = userId }
            },
            new PlanDayRepositoryStub
            {
                PlanDayToReturn = new PlanDay { Id = planDayId, PlanId = planId, Name = "old" }
            },
            exercisesRepository,
            new ExerciseRepositoryStub(),
            new TrainingRepositoryStub(),
            unitOfWork);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.UpdatePlanDayAsync(
                new User { Id = userId },
                planDayId.ToString(),
                "new",
                [new PlanDayExerciseInput { ExerciseId = Guid.NewGuid().ToString(), Series = 3, Reps = "8" }],
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(unitOfWork.Transaction.CommitCalls, Is.EqualTo(0));
            Assert.That(unitOfWork.Transaction.RollbackCalls, Is.EqualTo(1));
        });
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public RecordingTransaction Transaction { get; } = new();
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IUnitOfWorkTransaction>(Transaction);
        }
    }

    private sealed class RecordingTransaction : IUnitOfWorkTransaction
    {
        public int CommitCalls { get; private set; }
        public int RollbackCalls { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCalls++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCalls++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class PlanRepositoryStub : IPlanRepository
    {
        public Plan? PlanToReturn { get; set; }
        public Exception? SetActiveException { get; set; }
        public int SetActiveCalls { get; private set; }

        public Task<Plan?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PlanToReturn);
        }

        public Task SetActivePlanAsync(Guid userId, Guid planId, CancellationToken cancellationToken = default)
        {
            SetActiveCalls++;
            if (SetActiveException != null)
            {
                throw SetActiveException;
            }

            return Task.CompletedTask;
        }

        public Task<Plan?> FindActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Plan?> FindLastActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Plan>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Plan plan, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(Plan plan, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Plan> CopyPlanByShareCodeAsync(string shareCode, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GenerateShareCodeAsync(Guid planId, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class PlanDayRepositoryStub : IPlanDayRepository
    {
        public PlanDay? PlanDayToReturn { get; set; }
        public int UpdateCalls { get; private set; }

        public Task<PlanDay?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PlanDayToReturn);
        }

        public Task UpdateAsync(PlanDay planDay, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }

        public Task<List<PlanDay>> GetByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(PlanDay planDay, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MarkDeletedAsync(Guid planDayId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MarkDeletedByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> AnyByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class PlanDayExerciseRepositoryStub : IPlanDayExerciseRepository
    {
        public Exception? RemoveException { get; set; }
        public int RemoveCalls { get; private set; }
        public int AddRangeCalls { get; private set; }

        public Task RemoveByPlanDayIdAsync(Guid planDayId, CancellationToken cancellationToken = default)
        {
            RemoveCalls++;
            if (RemoveException != null)
            {
                throw RemoveException;
            }

            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<PlanDayExercise> exercises, CancellationToken cancellationToken = default)
        {
            AddRangeCalls++;
            return Task.CompletedTask;
        }

        public Task<List<PlanDayExercise>> GetByPlanDayIdsAsync(List<Guid> planDayIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<PlanDayExercise>> GetByPlanDayIdAsync(Guid planDayId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UserRepositoryStub : IUserRepository
    {
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ExerciseRepositoryStub : IExerciseRepository
    {
        public Task<Exercise?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Exercise>> GetAllForUserAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Exercise>> GetAllGlobalAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Exercise>> GetUserExercisesAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Exercise>> GetByBodyPartAsync(Guid userId, BodyParts bodyPart, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Exercise>> GetByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<Guid, string>> GetTranslationsAsync(IEnumerable<Guid> exerciseIds, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpsertTranslationAsync(Guid exerciseId, string culture, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Exercise exercise, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(Exercise exercise, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TrainingRepositoryStub : ITrainingRepository
    {
        public Task AddAsync(Training training, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Training?> GetLastByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Training>> GetByUserIdAndDateAsync(Guid userId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<DateTimeOffset>> GetDatesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Training>> GetByGymIdsAsync(List<Guid> gymIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Training>> GetByPlanDayIdsAsync(List<Guid> planDayIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
