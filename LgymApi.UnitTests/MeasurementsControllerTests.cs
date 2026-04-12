using FluentAssertions;
using LgymApi.Api;
using LgymApi.Api.Features.Measurements.Controllers;
using LgymApi.Api.Features.Measurements.Contracts;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Measurements;
using LgymApi.Application.Features.Measurements.Models;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class MeasurementsControllerTests
{
    [Test]
    public async Task GetMeasurementDetail_WithInvalidId_UsesEmptyTypedId()
    {
        var service = new StubMeasurementsService();
        var controller = CreateController(service);

        await controller.GetMeasurementDetail("not-a-guid");

        service.LastMeasurementId.Should().Be(Id<Measurement>.Empty);
    }

    [Test]
    public async Task GetMeasurementsHistory_WithInvalidRouteId_UsesEmptyRouteUserId()
    {
        var service = new StubMeasurementsService();
        var controller = CreateController(service);

        await controller.GetMeasurementsHistory("invalid", new MeasurementsHistoryRequestDto
        {
            BodyPart = BodyParts.Chest,
            Unit = HeightUnits.Centimeters
        });

        service.LastRouteUserId.Should().Be(Id<User>.Empty);
    }

    [Test]
    public async Task GetMeasurementsTrend_WithInvalidRouteId_UsesEmptyRouteUserId()
    {
        var service = new StubMeasurementsService();
        var controller = CreateController(service);

        await controller.GetMeasurementsTrend("invalid", new MeasurementTrendRequestDto
        {
            BodyPart = BodyParts.Chest,
            Unit = HeightUnits.Centimeters
        });

        service.LastRouteUserId.Should().Be(Id<User>.Empty);
    }

    private static MeasurementsController CreateController(StubMeasurementsService service)
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var controller = new MeasurementsController(service, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        return controller;
    }

    private sealed class StubMeasurementsService : IMeasurementsService
    {
        public Id<Measurement> LastMeasurementId { get; private set; } = Id<Measurement>.Empty;
        public Id<User> LastRouteUserId { get; private set; } = Id<User>.Empty;

        public Task<Result<Unit, AppError>> AddMeasurementAsync(User currentUser, BodyParts bodyPart, HeightUnits unit, double value, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));

        public Task<Result<Measurement, AppError>> GetMeasurementDetailAsync(User currentUser, Id<Measurement> measurementId, CancellationToken cancellationToken = default)
        {
            LastMeasurementId = measurementId;
            return Task.FromResult(Result<Measurement, AppError>.Success(new Measurement
            {
                Id = Id<Measurement>.New(),
                UserId = Id<User>.New(),
                BodyPart = BodyParts.Chest,
                Unit = HeightUnits.Centimeters.ToString(),
                Value = 10
            }));
        }

        public Task<Result<List<Measurement>, AppError>> GetMeasurementsListAsync(User currentUser, Id<User> routeUserId, BodyParts? bodyPart, HeightUnits? unit, CancellationToken cancellationToken = default)
        {
            LastRouteUserId = routeUserId;
            return Task.FromResult(Result<List<Measurement>, AppError>.Success(new List<Measurement>()));
        }

        public Task<Result<List<Measurement>, AppError>> GetMeasurementsHistoryAsync(User currentUser, Id<User> routeUserId, BodyParts? bodyPart, HeightUnits? unit, CancellationToken cancellationToken = default)
        {
            LastRouteUserId = routeUserId;
            return Task.FromResult(Result<List<Measurement>, AppError>.Success(new List<Measurement>()));
        }

        public Task<Result<MeasurementTrendResult, AppError>> GetMeasurementsTrendAsync(User currentUser, Id<User> routeUserId, BodyParts bodyPart, HeightUnits unit, CancellationToken cancellationToken = default)
        {
            LastRouteUserId = routeUserId;
            return Task.FromResult(Result<MeasurementTrendResult, AppError>.Success(new MeasurementTrendResult
            {
                BodyPart = bodyPart,
                Unit = unit,
                Direction = "flat",
                Points = 0
            }));
        }
    }
}
