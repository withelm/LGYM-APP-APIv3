using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.Features.Plan;
using LgymApi.Application.Features.PlanDay;
using LgymApi.Application.Features.PlanDay.Models;
using LgymApi.Application.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ServiceTransactionBehaviorTests
{
      [Test]
      public async Task SetNewActivePlanAsync_WhenSuccessful_CommitsTransaction()
      {
          var userId = Id<User>.New();
          var planId = Id<Plan>.New();
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
          var userId = Id<User>.New();
          var planId = Id<Plan>.New();
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
          var userId = Id<User>.New();
          var planId = Id<Plan>.New();
          var planDayId = Id<PlanDay>.New();
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

         var service = new PlanDayService(new PlanDayServiceDependenciesStub(
             planRepository,
             planDayRepository,
             exercisesRepository,
             new ExerciseRepositoryStub(),
             new TrainingRepositoryStub(),
             unitOfWork));

         await service.UpdatePlanDayAsync(
             new User { Id = userId },
             planDayId,
             "new",
             [new PlanDayExerciseInput { ExerciseId = Id<Exercise>.New(), Series = 3, Reps = "8" }],
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
           var userId = Id<User>.New();
           var planId = Id<Plan>.New();
           var planDayId = Id<PlanDay>.New();
           var unitOfWork = new RecordingUnitOfWork();
           var exercisesRepository = new PlanDayExerciseRepositoryStub
           {
               RemoveException = new InvalidOperationException("remove failed")
           };

          var service = new PlanDayService(new PlanDayServiceDependenciesStub(
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
              unitOfWork));

          Assert.ThrowsAsync<InvalidOperationException>(async () =>
              await service.UpdatePlanDayAsync(
                  new User { Id = userId },
                  planDayId,
                  "new",
                  [new PlanDayExerciseInput { ExerciseId = Id<Exercise>.New(), Series = 3, Reps = "8" }],
                  CancellationToken.None));

          Assert.Multiple(() =>
          {
              Assert.That(unitOfWork.Transaction.CommitCalls, Is.EqualTo(0));
              Assert.That(unitOfWork.Transaction.RollbackCalls, Is.EqualTo(1));
          });
      }

      // ===== VALIDATION BRANCH TESTS (Wave 3, Batch A) =====

      [Test]
      public async Task CreatePlanAsync_WhenCurrentUserNull_ReturnsInvalidPlanError()
      {
          var userId = Id<User>.New();
          var service = new PlanService(
              new UserRepositoryStub(),
              new PlanRepositoryStub(),
              new PlanDayRepositoryStub(),
              new RecordingUnitOfWork());

          var result = await service.CreatePlanAsync(null, userId, "Test Plan", CancellationToken.None);

          Assert.That(result.IsFailure, Is.True);
          Assert.That(result.Error, Is.InstanceOf<InvalidPlanError>());
      }

      [Test]
      public async Task CreatePlanAsync_WhenRouteUserIdEmpty_ReturnsInvalidPlanError()
      {
          var currentUser = new User { Id = Id<User>.New() };
          var service = new PlanService(
              new UserRepositoryStub(),
              new PlanRepositoryStub(),
              new PlanDayRepositoryStub(),
              new RecordingUnitOfWork());

          var result = await service.CreatePlanAsync(currentUser, Id<User>.Empty, "Test Plan", CancellationToken.None);

          Assert.That(result.IsFailure, Is.True);
          Assert.That(result.Error, Is.InstanceOf<InvalidPlanError>());
      }

      [Test]
      public async Task UpdatePlanAsync_WhenRouteUserIdEmpty_ReturnsInvalidPlanError()
      {
          var currentUser = new User { Id = Id<User>.New() };
          var planId = Id<Plan>.New();
          var service = new PlanService(
              new UserRepositoryStub(),
              new PlanRepositoryStub(),
              new PlanDayRepositoryStub(),
              new RecordingUnitOfWork());

          var result = await service.UpdatePlanAsync(currentUser, Id<User>.Empty, planId, "Updated", CancellationToken.None);

          Assert.That(result.IsFailure, Is.True);
          Assert.That(result.Error, Is.InstanceOf<InvalidPlanError>());
      }

      [Test]
      public async Task UpdatePlanAsync_WhenPlanIdEmpty_ReturnsInvalidPlanError()
      {
          var userId = Id<User>.New();
          var currentUser = new User { Id = userId };
          var service = new PlanService(
              new UserRepositoryStub(),
              new PlanRepositoryStub(),
              new PlanDayRepositoryStub(),
              new RecordingUnitOfWork());

          var result = await service.UpdatePlanAsync(currentUser, userId, Id<Plan>.Empty, "Updated", CancellationToken.None);

          Assert.That(result.IsFailure, Is.True);
          Assert.That(result.Error, Is.InstanceOf<InvalidPlanError>());
      }

      [Test]
      public async Task GetPlanConfigAsync_WhenRouteUserIdEmpty_ReturnsInvalidPlanError()
      {
          var currentUser = new User { Id = Id<User>.New() };
          var service = new PlanService(
              new UserRepositoryStub(),
              new PlanRepositoryStub(),
              new PlanDayRepositoryStub(),
              new RecordingUnitOfWork());

          var result = await service.GetPlanConfigAsync(currentUser, Id<User>.Empty, CancellationToken.None);

          Assert.That(result.IsFailure, Is.True);
          Assert.That(result.Error, Is.InstanceOf<InvalidPlanError>());
      }

      [Test]
      public async Task CheckIsUserHavePlanAsync_WhenRouteUserIdEmpty_ReturnsPlanFlagBadRequestErrorWithFalsePayload()
      {
          var currentUser = new User { Id = Id<User>.New() };
          var service = new PlanService(
              new UserRepositoryStub(),
              new PlanRepositoryStub(),
              new PlanDayRepositoryStub(),
              new RecordingUnitOfWork());

          var result = await service.CheckIsUserHavePlanAsync(currentUser, Id<User>.Empty, CancellationToken.None);

          Assert.Multiple(() =>
          {
              Assert.That(result.IsFailure, Is.True);
              Assert.That(result.Error, Is.InstanceOf<BadRequestError>());
              Assert.That(result.Error.GetPayload(), Is.EqualTo(false));
          });
      }

      [Test]
      public async Task CreatePlanDayAsync_WhenPlanIdEmpty_ReturnsInvalidPlanDayError()
      {
          var currentUser = new User { Id = Id<User>.New() };
          var service = new PlanDayService(new PlanDayServiceDependenciesStub(
              new PlanRepositoryStub(),
              new PlanDayRepositoryStub(),
              new PlanDayExerciseRepositoryStub(),
              new ExerciseRepositoryStub(),
              new TrainingRepositoryStub(),
              new RecordingUnitOfWork()));

          var result = await service.CreatePlanDayAsync(
              currentUser,
              Id<Plan>.Empty,
              "Test PlanDay",
              [new PlanDayExerciseInput { ExerciseId = Id<Exercise>.New(), Series = 3, Reps = "8" }],
              CancellationToken.None);

          Assert.That(result.IsFailure, Is.True);
          Assert.That(result.Error, Is.InstanceOf<InvalidPlanDayError>());
      }

      [Test]
      public async Task GetPlanDayAsync_WhenPlanDayIdEmpty_ReturnsInvalidPlanDayError()
      {
          var currentUser = new User { Id = Id<User>.New() };
          var service = new PlanDayService(new PlanDayServiceDependenciesStub(
              new PlanRepositoryStub(),
              new PlanDayRepositoryStub(),
              new PlanDayExerciseRepositoryStub(),
              new ExerciseRepositoryStub(),
              new TrainingRepositoryStub(),
              new RecordingUnitOfWork()));

          var result = await service.GetPlanDayAsync(currentUser, Id<PlanDay>.Empty, ["en"], CancellationToken.None);

          Assert.That(result.IsFailure, Is.True);
          Assert.That(result.Error, Is.InstanceOf<InvalidPlanDayError>());
      }

    private sealed class PlanDayServiceDependenciesStub : IPlanDayServiceDependencies
    {
        public PlanDayServiceDependenciesStub(
            IPlanRepository planRepository,
            IPlanDayRepository planDayRepository,
            IPlanDayExerciseRepository planDayExerciseRepository,
            IExerciseRepository exerciseRepository,
            ITrainingRepository trainingRepository,
            IUnitOfWork unitOfWork)
        {
            PlanRepository = planRepository;
            PlanDayRepository = planDayRepository;
            PlanDayExerciseRepository = planDayExerciseRepository;
            ExerciseRepository = exerciseRepository;
            TrainingRepository = trainingRepository;
            UnitOfWork = unitOfWork;
        }

        public IPlanRepository PlanRepository { get; }
        public IPlanDayRepository PlanDayRepository { get; }
        public IPlanDayExerciseRepository PlanDayExerciseRepository { get; }
        public IExerciseRepository ExerciseRepository { get; }
        public ITrainingRepository TrainingRepository { get; }
        public IUnitOfWork UnitOfWork { get; }
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

        public void DetachEntity<TEntity>(TEntity entity) where TEntity : class { }
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

        public Task<Plan?> FindByIdAsync(Id<Plan> id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PlanToReturn);
        }

        public Task SetActivePlanAsync(Id<User> userId, Id<Plan> planId, CancellationToken cancellationToken = default)
        {
            SetActiveCalls++;
            if (SetActiveException != null)
            {
                throw SetActiveException;
            }

            return Task.CompletedTask;
        }

        public Task ClearActivePlansAsync(Id<User> userId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Plan?> FindActiveByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Plan?> FindLastActiveByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Plan>> GetByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Plan plan, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(Plan plan, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Plan> CopyPlanByShareCodeAsync(string shareCode, Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GenerateShareCodeAsync(Id<Plan> planId, Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class PlanDayRepositoryStub : IPlanDayRepository
    {
        public PlanDay? PlanDayToReturn { get; set; }
        public int UpdateCalls { get; private set; }

        public Task<PlanDay?> FindByIdAsync(Id<PlanDay> id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PlanDayToReturn);
        }

        public Task UpdateAsync(PlanDay planDay, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }

        public Task<List<PlanDay>> GetByPlanIdAsync(Id<Plan> planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(PlanDay planDay, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MarkDeletedAsync(Id<PlanDay> planDayId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MarkDeletedByPlanIdAsync(Id<Plan> planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> AnyByPlanIdAsync(Id<Plan> planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class PlanDayExerciseRepositoryStub : IPlanDayExerciseRepository
    {
        public Exception? RemoveException { get; set; }
        public int RemoveCalls { get; private set; }
        public int AddRangeCalls { get; private set; }

        public Task RemoveByPlanDayIdAsync(Id<PlanDay> planDayId, CancellationToken cancellationToken = default)
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

        public Task<List<PlanDayExercise>> GetByPlanDayIdsAsync(List<Id<PlanDay>> planDayIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<PlanDayExercise>> GetByPlanDayIdAsync(Id<PlanDay> planDayId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UserRepositoryStub : IUserRepository
    {
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<User?> FindByIdAsync(Id<LgymApi.Domain.Entities.User> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByIdIncludingDeletedAsync(Id<LgymApi.Domain.Entities.User> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Pagination<UserResult>> GetUsersPaginatedAsync(FilterInput filterInput, bool includeDeleted, CancellationToken cancellationToken = default) => Task.FromResult(new Pagination<UserResult>());
    }

    private sealed class ExerciseRepositoryStub : IExerciseRepository
    {
        public Task<Exercise?> FindByIdAsync(Id<Exercise> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Exercise>> GetAllForUserAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Exercise>> GetAllGlobalAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Exercise>> GetUserExercisesAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Exercise>> GetByBodyPartAsync(Id<User> userId, BodyParts bodyPart, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Exercise>> GetByIdsAsync(List<Id<Exercise>> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<Id<Exercise>, string>> GetTranslationsAsync(IEnumerable<Id<Exercise>> exerciseIds, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpsertTranslationAsync(Id<Exercise> exerciseId, string culture, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Exercise exercise, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(Exercise exercise, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TrainingRepositoryStub : ITrainingRepository
    {
        public Task AddAsync(Training training, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Training?> GetByIdAsync(Id<Training> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Training?> GetLastByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Training>> GetByUserIdAndDateAsync(Id<User> userId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<DateTimeOffset>> GetDatesByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Training>> GetByGymIdsAsync(List<Id<Gym>> gymIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Training>> GetByPlanDayIdsAsync(List<Id<PlanDay>> planDayIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
