using System.Net;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.Supplementation;
using LgymApi.Application.Features.Supplementation.Models;
using LgymApi.Application.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class SupplementationServiceTests
{
    private SupplementationService _service = null!;
    private FakeRoleRepository _roleRepository = null!;
    private FakeTrainerRelationshipRepository _trainerRelationshipRepository = null!;
    private FakeSupplementationRepository _supplementationRepository = null!;
    private FakeUnitOfWork _unitOfWork = null!;

    [SetUp]
    public void SetUp()
    {
        _roleRepository = new FakeRoleRepository();
        _trainerRelationshipRepository = new FakeTrainerRelationshipRepository();
        _supplementationRepository = new FakeSupplementationRepository();
        _unitOfWork = new FakeUnitOfWork();
        _service = new SupplementationService(_roleRepository, _trainerRelationshipRepository, _supplementationRepository, _unitOfWork);
    }

    [Test]
    public async Task UpdateTraineePlanAsync_CreatesNewVersion_AndSoftDeletesOldPlan()
    {
        var trainer = NewTrainer();
        var trainee = NewTrainee();
        Link(trainer.Id, trainee.Id);
        var existing = NewPlan(trainer.Id, trainee.Id, isActive: true, "Old");
        _supplementationRepository.Plans.Add(existing);

        var result = await _service.UpdateTraineePlanAsync(trainer, trainee.Id, existing.Id, new UpsertSupplementPlanCommand
        {
            Name = "New",
            Items =
            [
                new UpsertSupplementPlanItemCommand
                {
                    SupplementName = "Omega",
                    Dosage = "1",
                    TimeOfDay = "08:00",
                    DaysOfWeekMask = 127,
                    Order = 0
                }
            ]
        });

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value.Id, Is.Not.EqualTo(existing.Id));
            Assert.That(existing.IsDeleted, Is.True);
            Assert.That(existing.IsActive, Is.False);
            Assert.That(result.Value.IsActive, Is.True);
            Assert.That(_supplementationRepository.Plans.Count, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task GetActiveScheduleForDateAsync_ReturnsTakenEntry_WhenLogExists()
    {
        var trainee = NewTrainee();
        var date = DateOnly.FromDateTime(new DateTime(2026, 2, 23));
        var mask = MaskForDate(date);
        var activePlan = NewPlan(Id<User>.New(), trainee.Id, isActive: true, "Cut",
            NewPlanItem("Omega", "2", "08:00", mask, 0));
        _supplementationRepository.Plans.Add(activePlan);
        _supplementationRepository.IntakeLogs.Add(new SupplementIntakeLog
        {
            Id = Id<SupplementIntakeLog>.New(),
            TraineeId = trainee.Id,
            PlanItemId = activePlan.Items.First().Id,
            IntakeDate = date,
            TakenAt = DateTimeOffset.UtcNow,
            PlanItem = activePlan.Items.First()
        });

        var result = await _service.GetActiveScheduleForDateAsync(trainee, date);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Has.Count.EqualTo(1));
            Assert.That(result.Value[0].Taken, Is.True);
            Assert.That(result.Value[0].TakenAt, Is.Not.Null);
        });
    }

    [Test]
    public async Task GetActiveScheduleForDateAsync_OrdersByOrderThenTime()
    {
        var trainee = NewTrainee();
        var date = DateOnly.FromDateTime(new DateTime(2026, 2, 23));
        var mask = MaskForDate(date);
        var firstExpected = NewPlanItem("First", "1", "21:00", mask, 0);
        var secondExpected = NewPlanItem("Second", "1", "08:00", mask, 1);
        var activePlan = NewPlan(Id<User>.New(), trainee.Id, isActive: true, "Cut", secondExpected, firstExpected);
        _supplementationRepository.Plans.Add(activePlan);

        var result = await _service.GetActiveScheduleForDateAsync(trainee, date);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Has.Count.EqualTo(2));
            Assert.That(result.Value[0].SupplementName, Is.EqualTo("First"));
            Assert.That(result.Value[1].SupplementName, Is.EqualTo("Second"));
        });
    }

    [Test]
    public async Task GetComplianceSummaryAsync_ThrowsBadRequest_WhenRangeTooLarge()
    {
        var trainer = NewTrainer();
        var trainee = NewTrainee();
        Link(trainer.Id, trainee.Id);

        var result = await _service.GetComplianceSummaryAsync(trainer, trainee.Id, new DateOnly(2026, 1, 1), new DateOnly(2027, 2, 2));

        Assert.That(result.IsFailure, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.InstanceOf<InvalidSupplementationError>());
            Assert.That(result.Error.Message, Is.EqualTo(Messages.DateRangeTooLarge));
            Assert.That(result.Error.HttpStatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task GetComplianceSummaryAsync_AllowsRangeAtLimit()
    {
        var trainer = NewTrainer();
        var trainee = NewTrainee();
        Link(trainer.Id, trainee.Id);

        var result = await _service.GetComplianceSummaryAsync(trainer, trainee.Id, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.TraineeId, Is.EqualTo(trainee.Id));
    }

    [Test]
    public async Task GetComplianceSummaryAsync_ThrowsBadRequest_WhenRangeExceedsLimitByOneDay()
    {
        var trainer = NewTrainer();
        var trainee = NewTrainee();
        Link(trainer.Id, trainee.Id);

        var result = await _service.GetComplianceSummaryAsync(trainer, trainee.Id, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2));

        Assert.That(result.IsFailure, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.InstanceOf<InvalidSupplementationError>());
            Assert.That(result.Error.HttpStatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task CreateTraineePlanAsync_ThrowsBadRequest_WhenTimeFormatInvalid()
    {
        var trainer = NewTrainer();
        var trainee = NewTrainee();
        Link(trainer.Id, trainee.Id);

        var result = await _service.CreateTraineePlanAsync(trainer, trainee.Id, new UpsertSupplementPlanCommand
        {
            Name = "Plan",
            Items =
            [
                new UpsertSupplementPlanItemCommand
                {
                    SupplementName = "Omega",
                    Dosage = "1",
                    TimeOfDay = "invalid",
                    DaysOfWeekMask = 127,
                    Order = 0
                }
            ]
        });

        Assert.That(result.IsFailure, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.InstanceOf<InvalidSupplementationError>());
            Assert.That(result.Error.HttpStatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task CreateTraineePlanAsync_ThrowsBadRequest_WhenNameExceedsMaxLength()
    {
        var trainer = NewTrainer();
        var trainee = NewTrainee();
        Link(trainer.Id, trainee.Id);

        var result = await _service.CreateTraineePlanAsync(trainer, trainee.Id, new UpsertSupplementPlanCommand
        {
            Name = new string('A', 121),
            Items =
            [
                new UpsertSupplementPlanItemCommand
                {
                    SupplementName = "Omega",
                    Dosage = "1",
                    TimeOfDay = "08:00",
                    DaysOfWeekMask = 127,
                    Order = 0
                }
            ]
        });

        Assert.That(result.IsFailure, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.InstanceOf<InvalidSupplementationError>());
            Assert.That(result.Error.HttpStatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task CheckOffIntakeAsync_ThrowsBadRequest_WhenIntakeDateIsDefault()
    {
        var trainee = NewTrainee();

        var result = await _service.CheckOffIntakeAsync(trainee, new CheckOffSupplementIntakeCommand
        {
            PlanItemId = Id<SupplementPlanItem>.New(),
            IntakeDate = default
        });

        Assert.That(result.IsFailure, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.InstanceOf<InvalidSupplementationError>());
            Assert.That(result.Error.HttpStatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
            Assert.That(result.Error.Message, Is.EqualTo(Messages.DateRequired));
        });
    }

    [Test]
    public async Task CheckOffIntakeAsync_WhenInsertRaceOccurs_ReturnsPersistedEntry()
    {
        var trainee = NewTrainee();
        var date = DateOnly.FromDateTime(new DateTime(2026, 2, 23));
        var mask = MaskForDate(date);
        var item = NewPlanItem("Omega", "2", "08:00", mask, 0);
        var activePlan = NewPlan(Id<User>.New(), trainee.Id, isActive: true, "Cut", item);
        _supplementationRepository.Plans.Add(activePlan);
        _unitOfWork.ThrowOnNextSave = true;
        var findCallCount = 0;
        _supplementationRepository.OnFindIntakeLog = (traineeId, planItemId, intakeDate) =>
        {
            findCallCount++;
            if (findCallCount == 1)
            {
                return null;
            }

            return new SupplementIntakeLog
            {
                Id = Id<SupplementIntakeLog>.New(),
                TraineeId = (LgymApi.Domain.ValueObjects.Id<User>)traineeId,
                PlanItemId = (LgymApi.Domain.ValueObjects.Id<SupplementPlanItem>)planItemId,
                IntakeDate = intakeDate,
                TakenAt = DateTimeOffset.UtcNow,
                PlanItem = item
            };
        };

        var result = await _service.CheckOffIntakeAsync(trainee, new CheckOffSupplementIntakeCommand
        {
            PlanItemId = item.Id,
            IntakeDate = date
        });

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value.Taken, Is.True);
            Assert.That(result.Value.TakenAt, Is.Not.Null);
        });
    }

    [Test]
    public async Task AssignTraineePlanAsync_DeactivatesOtherActivePlans()
    {
        var trainer = NewTrainer();
        var trainee = NewTrainee();
        Link(trainer.Id, trainee.Id);

        var oldActive = NewPlan(trainer.Id, trainee.Id, isActive: true, "Old", NewPlanItem("A", "1", "08:00", 127, 0));
        var toAssign = NewPlan(trainer.Id, trainee.Id, isActive: false, "New", NewPlanItem("B", "1", "09:00", 127, 0));
        _supplementationRepository.Plans.Add(oldActive);
        _supplementationRepository.Plans.Add(toAssign);

        var result = await _service.AssignTraineePlanAsync(trainer, trainee.Id, toAssign.Id);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(oldActive.IsActive, Is.False);
            Assert.That(toAssign.IsActive, Is.True);
        });
    }

    [Test]
    public async Task UnassignTraineePlanAsync_ReturnsWithoutSave_WhenNoOwnedActivePlan()
    {
        var trainer = NewTrainer();
        var trainee = NewTrainee();
        Link(trainer.Id, trainee.Id);

        var result = await _service.UnassignTraineePlanAsync(trainer, trainee.Id);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_unitOfWork.SaveChangesCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task CheckOffIntakeAsync_ThrowsNotFound_WhenItemNotScheduledForDate()
    {
        var trainee = NewTrainee();
        var date = new DateOnly(2026, 2, 23);
        var notTodayMask = 1 << (((int)date.DayOfWeek + 5) % 7);
        var activePlan = NewPlan(Id<User>.New(), trainee.Id, isActive: true, "Cut",
            NewPlanItem("Omega", "2", "08:00", notTodayMask, 0));
        _supplementationRepository.Plans.Add(activePlan);

        var result = await _service.CheckOffIntakeAsync(trainee, new CheckOffSupplementIntakeCommand
        {
            PlanItemId = activePlan.Items.First().Id,
            IntakeDate = date
        });

        Assert.That(result.IsFailure, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.InstanceOf<SupplementationNotFoundError>());
            Assert.That(result.Error.HttpStatusCode, Is.EqualTo((int)HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task GetTraineePlansAsync_ThrowsForbidden_WhenUserIsNotTrainer()
    {
        var notTrainer = new User
        {
            Id = Id<User>.New(),
            Name = "u",
            Email = "u@example.com"
        };

        var result = await _service.GetTraineePlansAsync(notTrainer, Id<User>.New());

        Assert.That(result.IsFailure, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.InstanceOf<SupplementationForbiddenError>());
            Assert.That(result.Error.HttpStatusCode, Is.EqualTo((int)HttpStatusCode.Forbidden));
        });
    }

    private User NewTrainer()
    {
        var user = new User
        {
            Id = Id<User>.New(),
            Name = $"trainer-{Id<User>.New():N}",
            Email = $"{Id<User>.New():N}@example.com"
        };

        _roleRepository.TrainerUserIds.Add(user.Id);
        return user;
    }

    private static User NewTrainee()
    {
        return new User
        {
            Id = Id<User>.New(),
            Name = $"trainee-{Id<User>.New():N}",
            Email = $"{Id<User>.New():N}@example.com"
        };
    }

    private void Link(Id<User> trainerId, Id<User> traineeId)
    {
        _trainerRelationshipRepository.Links[(trainerId, traineeId)] = new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId
        };
    }

    private static SupplementPlan NewPlan(Id<User> trainerId, Id<User> traineeId, bool isActive, string name, params SupplementPlanItem[] items)
    {
        var plan = new SupplementPlan
        {
            Id = Id<SupplementPlan>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            Name = name,
            IsActive = isActive,
            IsDeleted = false,
            Items = items.ToList()
        };

        foreach (var item in plan.Items)
        {
            item.PlanId = plan.Id;
            item.Plan = plan;
        }

        return plan;
    }

    private static SupplementPlanItem NewPlanItem(string supplementName, string dosage, string timeOfDay, int mask, int order)
    {
        return new SupplementPlanItem
        {
            Id = Id<SupplementPlanItem>.New(),
            SupplementName = supplementName,
            Dosage = dosage,
            TimeOfDay = TimeOnly.Parse(timeOfDay).ToTimeSpan(),
            DaysOfWeekMask = (LgymApi.Domain.ValueObjects.DaysOfWeekSet)mask,
            Order = order
        };
    }

    private static int MaskForDate(DateOnly date)
    {
        var normalizedDay = ((int)date.DayOfWeek + 6) % 7;
        return 1 << normalizedDay;
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        public HashSet<Id<User>> TrainerUserIds { get; } = [];

        public Task<bool> UserHasRoleAsync(Id<User> userId, string roleName, CancellationToken cancellationToken = default)
            => Task.FromResult(TrainerUserIds.Contains(userId) && roleName == AuthConstants.Roles.Trainer);

        public Task<List<LgymApi.Domain.Entities.Role>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<LgymApi.Domain.Entities.Role>());
        public Task<LgymApi.Domain.Entities.Role?> FindByIdAsync(Id<LgymApi.Domain.Entities.Role> roleId, CancellationToken cancellationToken = default) => Task.FromResult<LgymApi.Domain.Entities.Role?>(null);
        public Task<LgymApi.Domain.Entities.Role?> FindByNameAsync(string roleName, CancellationToken cancellationToken = default) => Task.FromResult<LgymApi.Domain.Entities.Role?>(null);
        public Task<List<LgymApi.Domain.Entities.Role>> GetByNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) => Task.FromResult(new List<LgymApi.Domain.Entities.Role>());
        public Task<bool> ExistsByNameAsync(string roleName, Id<LgymApi.Domain.Entities.Role>? excludeRoleId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<List<string>> GetRoleNamesByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<string>());
        public Task<List<string>> GetPermissionClaimsByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<string>());
        public Task<List<string>> GetPermissionClaimsByRoleIdAsync(Id<LgymApi.Domain.Entities.Role> roleId, CancellationToken cancellationToken = default) => Task.FromResult(new List<string>());
        public Task<Dictionary<Id<LgymApi.Domain.Entities.Role>, List<string>>> GetPermissionClaimsByRoleIdsAsync(IReadOnlyCollection<Id<LgymApi.Domain.Entities.Role>> roleIds, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<Id<LgymApi.Domain.Entities.Role>, List<string>>());
        public Task<bool> UserHasPermissionAsync(Id<User> userId, string permission, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddRoleAsync(LgymApi.Domain.Entities.Role role, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateRoleAsync(LgymApi.Domain.Entities.Role role, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteRoleAsync(LgymApi.Domain.Entities.Role role, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReplaceRolePermissionClaimsAsync(Id<LgymApi.Domain.Entities.Role> roleId, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddUserRolesAsync(Id<User> userId, IReadOnlyCollection<Id<LgymApi.Domain.Entities.Role>> roleIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReplaceUserRolesAsync(Id<User> userId, IReadOnlyCollection<Id<LgymApi.Domain.Entities.Role>> roleIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Dictionary<Id<User>, List<string>>> GetRoleNamesByUserIdsAsync(IReadOnlyCollection<Id<User>> userIds, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<Id<User>, List<string>>());
        public Task<Pagination<LgymApi.Domain.Entities.Role>> GetRolesPaginatedAsync(FilterInput filterInput, CancellationToken cancellationToken = default) => Task.FromResult(new Pagination<LgymApi.Domain.Entities.Role>());
    }

    private sealed class FakeTrainerRelationshipRepository : ITrainerRelationshipRepository
    {
        public Dictionary<(Id<User> TrainerId, Id<User> TraineeId), TrainerTraineeLink> Links { get; } = new();

        public Task<TrainerTraineeLink?> FindActiveLinkByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Links.TryGetValue((trainerId, traineeId), out var link) ? link : null);

        public Task AddInvitationAsync(TrainerInvitation invitation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TrainerInvitation?> FindInvitationByIdAsync(Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default) => Task.FromResult<TrainerInvitation?>(null);
        public Task<TrainerInvitation?> FindPendingInvitationAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default) => Task.FromResult<TrainerInvitation?>(null);
        public Task<TrainerInvitation?> FindPendingInvitationByEmailAsync(Id<User> trainerId, string inviteeEmail, CancellationToken cancellationToken = default) => Task.FromResult<TrainerInvitation?>(null);
        public Task<bool> IsEmailAlreadyTraineeAsync(Id<User> trainerId, string inviteeEmail, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<TrainerInvitation?> FindInvitationByIdWithCodeAsync(Id<TrainerInvitation> invitationId, string code, CancellationToken cancellationToken = default) => Task.FromResult<TrainerInvitation?>(null);
        public Task<List<TrainerInvitation>> GetInvitationsByTrainerIdAsync(Id<User> trainerId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TrainerInvitation>());
        public Task<bool> HasActiveLinkForTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<TrainerTraineeLink?> FindActiveLinkByTraineeIdAsync(Id<User> traineeId, CancellationToken cancellationToken = default) => Task.FromResult<TrainerTraineeLink?>(null);
        public Task<LgymApi.Application.Features.TrainerRelationships.Models.TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(Id<User> trainerId, LgymApi.Application.Features.TrainerRelationships.Models.TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new LgymApi.Application.Features.TrainerRelationships.Models.TrainerDashboardTraineeListResult());
        public Task<LgymApi.Application.Pagination.Pagination<LgymApi.Application.Features.TrainerRelationships.Models.TrainerInvitationResult>> GetInvitationsPaginatedAsync(Id<User> trainerId, LgymApi.Application.Pagination.FilterInput filterInput, CancellationToken cancellationToken = default)
            => Task.FromResult(new LgymApi.Application.Pagination.Pagination<LgymApi.Application.Features.TrainerRelationships.Models.TrainerInvitationResult>());
        public Task AddLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSupplementationRepository : ISupplementationRepository
    {
        public List<SupplementPlan> Plans { get; } = [];
        public List<SupplementIntakeLog> IntakeLogs { get; } = [];
        public Func<Id<User>, Id<SupplementPlanItem>, DateOnly, SupplementIntakeLog?>? OnFindIntakeLog { get; set; }

        public Task AddPlanAsync(SupplementPlan plan, CancellationToken cancellationToken = default)
        {
            Plans.Add(plan);
            return Task.CompletedTask;
        }

        public Task<SupplementPlan?> FindPlanByIdAsync(Id<SupplementPlan> planId, CancellationToken cancellationToken = default)
            => Task.FromResult(Plans.FirstOrDefault(x => x.Id == planId));

        public Task<List<SupplementPlan>> GetPlansByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Plans.Where(x => x.TrainerId == trainerId && x.TraineeId == traineeId && !x.IsDeleted).ToList());

        public Task<SupplementPlan?> GetActivePlanForTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default)
            => Task.FromResult(Plans.FirstOrDefault(x => x.TraineeId == traineeId && x.IsActive && !x.IsDeleted));

        public Task<List<SupplementIntakeLog>> GetIntakeLogsForPlanAsync(Id<User> traineeId, Id<SupplementPlan> planId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
        {
            var planItemIds = Plans
                .Where(p => p.Id == planId)
                .SelectMany(p => p.Items)
                .Select(i => i.Id)
                .ToHashSet();

            var logs = IntakeLogs
                .Where(x => x.TraineeId == traineeId
                            && planItemIds.Contains(x.PlanItemId)
                            && x.IntakeDate >= fromDate
                            && x.IntakeDate <= toDate)
                .ToList();

            return Task.FromResult(logs);
        }

        public Task<SupplementIntakeLog?> FindIntakeLogAsync(Id<User> traineeId, Id<SupplementPlanItem> planItemId, DateOnly intakeDate, CancellationToken cancellationToken = default)
        {
            var overrideResult = OnFindIntakeLog?.Invoke(traineeId, planItemId, intakeDate);
            if (overrideResult != null)
            {
                return Task.FromResult<SupplementIntakeLog?>(overrideResult);
            }

            return Task.FromResult(IntakeLogs.FirstOrDefault(x => x.TraineeId == traineeId && x.PlanItemId == planItemId && x.IntakeDate == intakeDate));
        }

        public Task AddIntakeLogAsync(SupplementIntakeLog intakeLog, CancellationToken cancellationToken = default)
        {
            IntakeLogs.Add(intakeLog);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }
        public bool ThrowOnNextSave { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnNextSave)
            {
                ThrowOnNextSave = false;
                throw new InvalidOperationException("simulated unique constraint violation");
            }

            SaveChangesCalls++;
            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction());

        public void DetachEntity<TEntity>(TEntity entity) where TEntity : class { }
    }

    private sealed class FakeTransaction : IUnitOfWorkTransaction
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
