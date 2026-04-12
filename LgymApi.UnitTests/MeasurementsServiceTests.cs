using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.Measurements;
using LgymApi.Application.Repositories;
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
            Unit = HeightUnits.Centimeters.ToString(),
            Value = 100
        };

        var service = CreateService(findById: (_, _) => Task.FromResult<Measurement?>(measurement));

        var result = await service.GetMeasurementDetailAsync(currentUser, measurementId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<MeasurementForbiddenError>();
        result.Error.HttpStatusCode.Should().Be(403);
    }

    [Test]
    public async Task GetMeasurementsListAsync_WhenStoredUnitIsInvalid_ReturnsBadRequest()
    {
        var userId = Id<User>.New();
        var currentUser = new User { Id = userId, Name = "user", Email = "list-invalid-unit@example.com", ProfileRank = "Rookie" };

        var measurements = new List<Measurement>
        {
            new()
            {
                Id = Id<Measurement>.New(),
                UserId = userId,
                BodyPart = BodyParts.Chest,
                Unit = "invalid-unit",
                Value = 95,
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        var service = CreateService(getByUser: (_, _, _) => Task.FromResult(measurements));

        var result = await service.GetMeasurementsListAsync(currentUser, userId, BodyParts.Chest, HeightUnits.Centimeters);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidMeasurementError>();
        result.Error.HttpStatusCode.Should().Be(400);
    }

      [Test]
      public async Task GetMeasurementsTrendAsync_WhenStartValueIsNearZero_ReturnsZeroPercentageAndFlatDirection()
      {
          var userId = Id<User>.New();
          var currentUser = new User { Id = userId, Name = "user", Email = "trend-zero@example.com", ProfileRank = "Rookie" };
          var createdAt = DateTimeOffset.UtcNow;

          var measurements = new List<Measurement>
          {
              new()
              {
                  Id = Id<Measurement>.New(),
                  UserId = userId,
                  BodyPart = BodyParts.Chest,
                  Unit = HeightUnits.Centimeters.ToString(),
                  Value = 0,
                  CreatedAt = createdAt
              },
              new()
              {
                  Id = Id<Measurement>.New(),
                  UserId = userId,
                  BodyPart = BodyParts.Chest,
                  Unit = HeightUnits.Centimeters.ToString(),
                  Value = 0,
                  CreatedAt = createdAt.AddMinutes(5)
              }
          };

          var service = CreateService(getByUser: (_, _, _) => Task.FromResult(measurements));

          var result = await service.GetMeasurementsTrendAsync(currentUser, userId, BodyParts.Chest, HeightUnits.Centimeters);

          result.IsSuccess.Should().BeTrue();
          result.Value.ChangePercentage.Should().Be(0);
          result.Value.Direction.Should().Be("flat");
          result.Value.Points.Should().Be(2);
      }

      [Test]
      public async Task AddMeasurementAsync_WhenCurrentUserIsNull_ReturnsInvalidMeasurementError()
      {
          var service = CreateService();

          var result = await service.AddMeasurementAsync(null, BodyParts.Chest, HeightUnits.Centimeters, 100);

          result.IsFailure.Should().BeTrue();
          result.Error.Should().BeOfType<InvalidMeasurementError>();
          result.Error.HttpStatusCode.Should().Be(400);
      }

      [Test]
      public async Task AddMeasurementAsync_WhenBodyPartIsUnknown_ReturnsInvalidMeasurementError()
      {
          var currentUser = new User { Id = Id<User>.New(), Name = "user", Email = "unknown-part@example.com", ProfileRank = "Rookie" };
          var service = CreateService();

          var result = await service.AddMeasurementAsync(currentUser, BodyParts.Unknown, HeightUnits.Centimeters, 100);

          result.IsFailure.Should().BeTrue();
          result.Error.Should().BeOfType<InvalidMeasurementError>();
          result.Error.HttpStatusCode.Should().Be(400);
      }

      [Test]
      public async Task AddMeasurementAsync_WhenUnitIsUnknown_ReturnsInvalidMeasurementError()
      {
          var currentUser = new User { Id = Id<User>.New(), Name = "user", Email = "unknown-unit@example.com", ProfileRank = "Rookie" };
          var service = CreateService();

          var result = await service.AddMeasurementAsync(currentUser, BodyParts.Chest, HeightUnits.Unknown, 100);

          result.IsFailure.Should().BeTrue();
          result.Error.Should().BeOfType<InvalidMeasurementError>();
          result.Error.HttpStatusCode.Should().Be(400);
      }

      [Test]
      public async Task GetMeasurementDetailAsync_WhenMeasurementIdIsEmpty_ReturnsInvalidMeasurementError()
      {
          var userId = Id<User>.New();
          var currentUser = new User { Id = userId, Name = "user", Email = "empty-id@example.com", ProfileRank = "Rookie" };

          var service = CreateService();

          var result = await service.GetMeasurementDetailAsync(currentUser, Id<Measurement>.Empty);

          result.IsFailure.Should().BeTrue();
          result.Error.Should().BeOfType<InvalidMeasurementError>();
          result.Error.HttpStatusCode.Should().Be(400);
      }

      [Test]
      public async Task GetMeasurementDetailAsync_WhenMeasurementNotFound_ReturnsMeasurementNotFoundError()
      {
          var userId = Id<User>.New();
          var currentUser = new User { Id = userId, Name = "user", Email = "not-found@example.com", ProfileRank = "Rookie" };
          var measurementId = Id<Measurement>.New();

          var service = CreateService(findById: (_, _) => Task.FromResult<Measurement?>(null));

          var result = await service.GetMeasurementDetailAsync(currentUser, measurementId);

          result.IsFailure.Should().BeTrue();
          result.Error.Should().BeOfType<MeasurementNotFoundError>();
          result.Error.HttpStatusCode.Should().Be(404);
      }

      [Test]
      public async Task GetMeasurementsTrendAsync_WhenRouteUserIdIsEmpty_ReturnsInvalidMeasurementError()
      {
          var userId = Id<User>.New();
          var currentUser = new User { Id = userId, Name = "user", Email = "trend-empty-route@example.com", ProfileRank = "Rookie" };

          var service = CreateService();

          var result = await service.GetMeasurementsTrendAsync(currentUser, Id<User>.Empty, BodyParts.Chest, HeightUnits.Centimeters);

          result.IsFailure.Should().BeTrue();
          result.Error.Should().BeOfType<InvalidMeasurementError>();
          result.Error.HttpStatusCode.Should().Be(400);
      }

    private static MeasurementsService CreateService(
        Func<Id<Measurement>, CancellationToken, Task<Measurement?>>? findById = null,
        Func<Id<User>, BodyParts?, CancellationToken, Task<List<Measurement>>>? getByUser = null)
    {
        var measurementRepository = new StubMeasurementRepository
        {
            FindByIdHandler = findById ?? ((_, _) => Task.FromResult<Measurement?>(null)),
            GetByUserHandler = getByUser ?? ((_, _, _) => Task.FromResult(new List<Measurement>()))
        };

        var converter = new StubHeightUnitConverter();
        var unitOfWork = new StubUnitOfWork();

        return new MeasurementsService(measurementRepository, converter, unitOfWork);
    }

    private sealed class StubMeasurementRepository : IMeasurementRepository
    {
        public Func<Id<Measurement>, CancellationToken, Task<Measurement?>> FindByIdHandler { get; init; } = (_, _) => Task.FromResult<Measurement?>(null);
        public Func<Id<User>, BodyParts?, CancellationToken, Task<List<Measurement>>> GetByUserHandler { get; init; } = (_, _, _) => Task.FromResult(new List<Measurement>());

        public Task AddAsync(Measurement measurement, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Measurement?> FindByIdAsync(Id<Measurement> id, CancellationToken cancellationToken = default)
            => FindByIdHandler(id, cancellationToken);

        public Task<List<Measurement>> GetByUserAsync(Id<User> userId, BodyParts? bodyPart, CancellationToken cancellationToken = default)
            => GetByUserHandler(userId, bodyPart, cancellationToken);
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
}
