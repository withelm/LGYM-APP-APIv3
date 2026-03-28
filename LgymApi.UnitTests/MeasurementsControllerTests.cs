using LgymApi.Api;
using LgymApi.Api.Features.Measurements.Controllers;
using LgymApi.Api.Features.Measurements.Contracts;
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

        Assert.That(service.LastMeasurementId, Is.EqualTo(Id<Measurement>.Empty));
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

        Assert.That(service.LastRouteUserId, Is.EqualTo(Id<User>.Empty));
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

        Assert.That(service.LastRouteUserId, Is.EqualTo(Id<User>.Empty));
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

        public Task AddMeasurementAsync(User currentUser, BodyParts bodyPart, HeightUnits unit, double value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Measurement> GetMeasurementDetailAsync(User currentUser, Id<Measurement> measurementId, CancellationToken cancellationToken = default)
        {
            LastMeasurementId = measurementId;
            return Task.FromResult(new Measurement
            {
                Id = Id<Measurement>.New(),
                UserId = Id<User>.New(),
                BodyPart = BodyParts.Chest,
                Unit = HeightUnits.Centimeters.ToString(),
                Value = 10
            });
        }

        public Task<List<Measurement>> GetMeasurementsListAsync(User currentUser, Id<User> routeUserId, BodyParts? bodyPart, HeightUnits? unit, CancellationToken cancellationToken = default)
        {
            LastRouteUserId = routeUserId;
            return Task.FromResult(new List<Measurement>());
        }

        public Task<List<Measurement>> GetMeasurementsHistoryAsync(User currentUser, Id<User> routeUserId, BodyParts? bodyPart, HeightUnits? unit, CancellationToken cancellationToken = default)
        {
            LastRouteUserId = routeUserId;
            return Task.FromResult(new List<Measurement>());
        }

        public Task<MeasurementTrendResult> GetMeasurementsTrendAsync(User currentUser, Id<User> routeUserId, BodyParts bodyPart, HeightUnits unit, CancellationToken cancellationToken = default)
        {
            LastRouteUserId = routeUserId;
            return Task.FromResult(new MeasurementTrendResult
            {
                BodyPart = bodyPart,
                Unit = unit,
                Direction = "flat",
                Points = 0
            });
        }
    }
}
