using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.Measurements;
using LgymApi.Application.Features.Measurements.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Units;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class MeasurementsServiceTests
{
    [Test]
    public async Task GetMeasurementDetailAsync_WhenMeasurementBelongsToDifferentUser_ReturnsForbidden()
    {
        var currentUser = new User { Id = Id<User>.New(), Name = "current", Email = "current-measure@example.com", ProfileRank = "Rookie" };
        var foreignUserId = Id<User>.New();
        var measurementId = Id<Measurement>.New();
        var measurement = new Measurement
        {
            Id = measurementId,
            UserId = foreignUserId,
            BodyPart = BodyParts.Chest,
            Unit = MeasurementUnits.Centimeters.ToString(),
            Value = 100
        };

        var service = CreateService(findById: (_, _) => Task.FromResult<Measurement?>(measurement));

        var result = await service.GetMeasurementDetailAsync(currentUser, measurementId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<MeasurementForbiddenError>();
    }

    [Test]
    public async Task GetMeasurementsHistoryAsync_WhenTrainerOwnsTrainee_ReturnsTraineeMeasurements()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var trainer = CreateUser(trainerId, "trainer@example.com");
        var measurements = new List<Measurement>
        {
            CreateMeasurement(traineeId, BodyParts.BodyWeight, MeasurementUnits.Kilograms, 82, 0),
            CreateMeasurement(traineeId, BodyParts.BodyWeight, MeasurementUnits.Kilograms, 81.5, 3)
        };

        var service = CreateService(
            getByUser: (userId, _, _) => Task.FromResult(userId == traineeId ? measurements : new List<Measurement>()),
            userHasRole: (_, _, _) => Task.FromResult(true),
            hasTrainerTraineeLink: (_, _, _) => Task.FromResult(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainerId,
                TraineeId = traineeId,
            }));

        var result = await service.GetMeasurementsHistoryAsync(trainer, traineeId, BodyParts.BodyWeight, MeasurementUnits.Kilograms);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().OnlyContain(item => item.UserId == traineeId);
    }

    [Test]
    public async Task AddMeasurementAsync_WhenUnitDoesNotMatchBodyPart_ReturnsInvalidMeasurementError()
    {
        var currentUser = new User { Id = Id<User>.New(), Name = "user", Email = "weight-unit@example.com", ProfileRank = "Rookie" };
        var service = CreateService();

        var result = await service.AddMeasurementAsync(currentUser, BodyParts.BodyWeight, MeasurementUnits.Centimeters, 80);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidMeasurementError>();
    }

    [Test]
    public async Task GetMeasurementsTrendAsync_WhenValueGrows_ReturnsUpDirectionAndDifference()
    {
        var userId = Id<User>.New();
        var currentUser = CreateUser(userId, "trend-up@example.com");
        var measurements = new List<Measurement>
        {
            CreateMeasurement(userId, BodyParts.BodyWeight, MeasurementUnits.Kilograms, 80, 0),
            CreateMeasurement(userId, BodyParts.BodyWeight, MeasurementUnits.Kilograms, 94.1, 5)
        };

        var service = CreateService(getByUser: (_, _, _) => Task.FromResult(measurements));

        var result = await service.GetMeasurementsTrendAsync(currentUser, userId, BodyParts.BodyWeight, MeasurementUnits.Kilograms);

        result.IsSuccess.Should().BeTrue();
        result.Value.Direction.Should().Be("up");
        result.Value.Difference.Should().Be(14.1);
        result.Value.FirstMeasurementValue.Should().Be(80);
        result.Value.LastMeasurementValue.Should().Be(94.1);
    }

    [Test]
    public async Task GetMeasurementsTrendAsync_WhenValueDrops_ReturnsDownDirectionAndAbsoluteDifference()
    {
        var userId = Id<User>.New();
        var currentUser = CreateUser(userId, "trend-down@example.com");
        var measurements = new List<Measurement>
        {
            CreateMeasurement(userId, BodyParts.Waist, MeasurementUnits.Centimeters, 90, 0),
            CreateMeasurement(userId, BodyParts.Waist, MeasurementUnits.Centimeters, 86.6, 7)
        };

        var service = CreateService(getByUser: (_, _, _) => Task.FromResult(measurements));

        var result = await service.GetMeasurementsTrendAsync(currentUser, userId, BodyParts.Waist, MeasurementUnits.Centimeters);

        result.IsSuccess.Should().BeTrue();
        result.Value.Direction.Should().Be("down");
        result.Value.Difference.Should().Be(3.4);
        result.Value.Change.Should().Be(-3.4);
    }

    [Test]
    public async Task GetMeasurementsTrendAsync_WhenValueStaysTheSame_ReturnsSameDirection()
    {
        var userId = Id<User>.New();
        var currentUser = CreateUser(userId, "trend-same@example.com");
        var measurements = new List<Measurement>
        {
            CreateMeasurement(userId, BodyParts.BodyFat, MeasurementUnits.Percent, 15, 0),
            CreateMeasurement(userId, BodyParts.BodyFat, MeasurementUnits.Percent, 15, 2)
        };

        var service = CreateService(getByUser: (_, _, _) => Task.FromResult(measurements));

        var result = await service.GetMeasurementsTrendAsync(currentUser, userId, BodyParts.BodyFat, MeasurementUnits.Percent);

        result.IsSuccess.Should().BeTrue();
        result.Value.Direction.Should().Be("same");
        result.Value.Difference.Should().Be(0);
    }

    [Test]
    public async Task GetMeasurementsTrendAsync_WhenOnlyOneMeasurementExists_ReturnsInsufficientData()
    {
        var userId = Id<User>.New();
        var currentUser = CreateUser(userId, "trend-one@example.com");
        var measurements = new List<Measurement>
        {
            CreateMeasurement(userId, BodyParts.Neck, MeasurementUnits.Centimeters, 42, 0)
        };

        var service = CreateService(getByUser: (_, _, _) => Task.FromResult(measurements));

        var result = await service.GetMeasurementsTrendAsync(currentUser, userId, BodyParts.Neck, MeasurementUnits.Centimeters);

        result.IsSuccess.Should().BeTrue();
        result.Value.Direction.Should().Be("insufficient_data");
        result.Value.Points.Should().Be(1);
    }

    [Test]
    public async Task GetMeasurementsTrendAsync_WhenNoMeasurementsExist_ReturnsInsufficientData()
    {
        var userId = Id<User>.New();
        var currentUser = CreateUser(userId, "trend-none@example.com");
        var service = CreateService(getByUser: (_, _, _) => Task.FromResult(new List<Measurement>()));

        var result = await service.GetMeasurementsTrendAsync(currentUser, userId, BodyParts.Bmi, MeasurementUnits.Bmi);

        result.IsSuccess.Should().BeTrue();
        result.Value.Direction.Should().Be("insufficient_data");
        result.Value.Points.Should().Be(0);
    }

    [Test]
    public async Task GetMeasurementsTrendsAsync_WhenMultipleMeasurementTypesExist_ReturnsTrendPerType()
    {
        var userId = Id<User>.New();
        var currentUser = CreateUser(userId, "trend-many@example.com");
        var measurements = new List<Measurement>
        {
            CreateMeasurement(userId, BodyParts.BodyWeight, MeasurementUnits.Kilograms, 80, 0),
            CreateMeasurement(userId, BodyParts.BodyWeight, MeasurementUnits.Kilograms, 82, 4),
            CreateMeasurement(userId, BodyParts.Waist, MeasurementUnits.Centimeters, 90, 1),
            CreateMeasurement(userId, BodyParts.Waist, MeasurementUnits.Centimeters, 88, 6),
            CreateMeasurement(userId, BodyParts.BodyFat, MeasurementUnits.Percent, 16, 3)
        };

        var service = CreateService(getByUser: (_, _, _) => Task.FromResult(measurements));

        var result = await service.GetMeasurementsTrendsAsync(currentUser, userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Should().ContainSingle(x => x.BodyPart == BodyParts.BodyWeight && x.Direction == "up");
        result.Value.Should().ContainSingle(x => x.BodyPart == BodyParts.Waist && x.Direction == "down");
        result.Value.Should().ContainSingle(x => x.BodyPart == BodyParts.BodyFat && x.Direction == "insufficient_data");
    }

    [Test]
    public async Task AddMeasurementsAsync_WhenBulkPayloadIsValid_SavesAllMeasurements()
    {
        var userId = Id<User>.New();
        var currentUser = CreateUser(userId, "bulk-valid@example.com");
        var repository = new CapturingMeasurementRepository();
        var service = CreateService(repository: repository);

        var result = await service.AddMeasurementsAsync(currentUser,
        [
            new MeasurementCreateInput { BodyPart = BodyParts.BodyWeight, Unit = MeasurementUnits.Kilograms, Value = 80 },
            new MeasurementCreateInput { BodyPart = BodyParts.Waist, Unit = MeasurementUnits.Centimeters, Value = 90 }
        ]);

        result.IsSuccess.Should().BeTrue();
        repository.AddedMeasurements.Should().HaveCount(2);
    }

    private static User CreateUser(Id<User> userId, string email)
        => new() { Id = userId, Name = email.Split('@')[0], Email = email, ProfileRank = "Rookie" };

    private static Measurement CreateMeasurement(Id<User> userId, BodyParts bodyPart, MeasurementUnits unit, double value, int dayOffset)
        => new()
        {
            Id = Id<Measurement>.New(),
            UserId = userId,
            BodyPart = bodyPart,
            Unit = unit.ToString(),
            Value = value,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero).AddDays(dayOffset)
        };

    private static MeasurementsService CreateService(
        Func<Id<Measurement>, CancellationToken, Task<Measurement?>>? findById = null,
        Func<Id<User>, BodyParts?, CancellationToken, Task<List<Measurement>>>? getByUser = null,
        Func<Id<User>, string, CancellationToken, Task<bool>>? userHasRole = null,
        Func<Id<User>, Id<User>, CancellationToken, Task<TrainerTraineeLink?>>? hasTrainerTraineeLink = null,
        CapturingMeasurementRepository? repository = null)
    {
        var measurementRepository = repository ?? new CapturingMeasurementRepository();
        measurementRepository.FindByIdHandler = findById ?? ((_, _) => Task.FromResult<Measurement?>(null));
        measurementRepository.GetByUserHandler = getByUser ?? ((_, _, _) => Task.FromResult(new List<Measurement>()));
        var roleRepository = new StubRoleRepository
        {
            UserHasRoleHandler = userHasRole ?? ((_, _, _) => Task.FromResult(false))
        };
        var trainerRelationshipRepository = new StubTrainerRelationshipRepository
        {
            FindActiveLinkByTrainerAndTraineeHandler = hasTrainerTraineeLink ?? ((_, _, _) => Task.FromResult<TrainerTraineeLink?>(null))
        };

        var heightConverter = new StubHeightUnitConverter();
        var weightConverter = new StubWeightUnitConverter();
        var unitOfWork = new StubUnitOfWork();

        return new MeasurementsService(measurementRepository, roleRepository, trainerRelationshipRepository, heightConverter, weightConverter, unitOfWork);
    }

    private sealed class CapturingMeasurementRepository : IMeasurementRepository
    {
        public List<Measurement> AddedMeasurements { get; } = new();
        public Func<Id<Measurement>, CancellationToken, Task<Measurement?>> FindByIdHandler { get; set; } = (_, _) => Task.FromResult<Measurement?>(null);
        public Func<Id<User>, BodyParts?, CancellationToken, Task<List<Measurement>>> GetByUserHandler { get; set; } = (_, _, _) => Task.FromResult(new List<Measurement>());

        public Task AddAsync(Measurement measurement, CancellationToken cancellationToken = default)
        {
            AddedMeasurements.Add(measurement);
            return Task.CompletedTask;
        }

        public Task<Measurement?> FindByIdAsync(Id<Measurement> id, CancellationToken cancellationToken = default)
            => FindByIdHandler(id, cancellationToken);

        public Task<List<Measurement>> GetByUserAsync(Id<User> userId, BodyParts? bodyPart, CancellationToken cancellationToken = default)
            => GetFilteredAsync(userId, bodyPart, cancellationToken);

        private async Task<List<Measurement>> GetFilteredAsync(Id<User> userId, BodyParts? bodyPart, CancellationToken cancellationToken)
        {
            var items = await GetByUserHandler(userId, bodyPart, cancellationToken);
            return bodyPart.HasValue ? items.Where(item => item.BodyPart == bodyPart.Value).ToList() : items;
        }
    }

    private sealed class StubHeightUnitConverter : IUnitConverter<HeightUnits>
    {
        public double Convert(double value, HeightUnits fromUnit, HeightUnits toUnit)
        {
            if (fromUnit == toUnit)
            {
                return value;
            }

            return (fromUnit, toUnit) switch
            {
                (HeightUnits.Meters, HeightUnits.Centimeters) => value * 100d,
                (HeightUnits.Centimeters, HeightUnits.Meters) => value / 100d,
                (HeightUnits.Centimeters, HeightUnits.Millimeters) => value * 10d,
                (HeightUnits.Millimeters, HeightUnits.Centimeters) => value / 10d,
                _ => value
            };
        }
    }

    private sealed class StubWeightUnitConverter : IUnitConverter<WeightUnits>
    {
        public double Convert(double value, WeightUnits fromUnit, WeightUnits toUnit)
        {
            if (fromUnit == toUnit)
            {
                return value;
            }

            return (fromUnit, toUnit) switch
            {
                (WeightUnits.Kilograms, WeightUnits.Pounds) => value * 2.20462d,
                (WeightUnits.Pounds, WeightUnits.Kilograms) => value / 2.20462d,
                _ => value
            };
        }
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void DetachEntity<TEntity>(TEntity entity) where TEntity : class => throw new NotSupportedException();
    }

    private sealed class StubRoleRepository : IRoleRepository
    {
        public Func<Id<User>, string, CancellationToken, Task<bool>> UserHasRoleHandler { get; set; } = (_, _, _) => Task.FromResult(false);

        public Task<bool> UserHasRoleAsync(Id<User> userId, string roleName, CancellationToken cancellationToken = default)
            => UserHasRoleHandler(userId, roleName, cancellationToken);

        public Task<List<Role>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Role?> FindByIdAsync(Id<Role> roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Role?> FindByNameAsync(string roleName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Role>> GetByNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsByNameAsync(string roleName, Id<Role>? excludeRoleId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<string>> GetRoleNamesByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<Id<User>, List<string>>> GetRoleNamesByUserIdsAsync(IReadOnlyCollection<Id<User>> userIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<string>> GetPermissionClaimsByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<string>> GetPermissionClaimsByRoleIdAsync(Id<Role> targetRoleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<Id<Role>, List<string>>> GetPermissionClaimsByRoleIdsAsync(IReadOnlyCollection<Id<Role>> targetRoleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UserHasPermissionAsync(Id<User> userId, string permission, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRoleAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateRoleAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteRoleAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReplaceRolePermissionClaimsAsync(Id<Role> targetRoleId, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddUserRolesAsync(Id<User> userId, IReadOnlyCollection<Id<Role>> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReplaceUserRolesAsync(Id<User> userId, IReadOnlyCollection<Id<Role>> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LgymApi.Application.Pagination.Pagination<Role>> GetRolesPaginatedAsync(FilterInput filterInput, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubTrainerRelationshipRepository : ITrainerRelationshipRepository
    {
        public Func<Id<User>, Id<User>, CancellationToken, Task<TrainerTraineeLink?>> FindActiveLinkByTrainerAndTraineeHandler { get; set; } = (_, _, _) => Task.FromResult<TrainerTraineeLink?>(null);

        public Task<TrainerTraineeLink?> FindActiveLinkByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default)
            => FindActiveLinkByTrainerAndTraineeHandler(trainerId, traineeId, cancellationToken);

        public Task AddInvitationAsync(TrainerInvitation invitation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TrainerInvitation?> FindInvitationByIdAsync(Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TrainerInvitation?> FindPendingInvitationAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TrainerInvitation?> FindPendingInvitationByEmailAsync(Id<User> trainerId, string inviteeEmail, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsEmailAlreadyTraineeAsync(Id<User> trainerId, string inviteeEmail, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TrainerInvitation?> FindInvitationByIdWithCodeAsync(Id<TrainerInvitation> invitationId, string code, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<TrainerInvitation>> GetInvitationsByTrainerIdAsync(Id<User> trainerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasActiveLinkForTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TrainerTraineeLink?> FindActiveLinkByTraineeIdAsync(Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LgymApi.Application.Features.TrainerRelationships.Models.TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(Id<User> trainerId, LgymApi.Application.Features.TrainerRelationships.Models.TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LgymApi.Application.Pagination.Pagination<LgymApi.Application.Features.TrainerRelationships.Models.TrainerInvitationResult>> GetInvitationsPaginatedAsync(Id<User> trainerId, FilterInput filterInput, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
