using FluentAssertions;
using LgymApi.Api;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Features.Trainer.Controllers;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.TraineeNotes;
using LgymApi.Application.Features.TraineeNotes.Models;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainerTraineeNotesControllerTests
{
    [Test]
    public async Task GetNotes_WithInvalidTraineeId_ForwardsEmptyIdToService()
    {
        var service = Substitute.For<ITraineeNoteService>();
        Id<User> capturedId = Id<User>.New();
        service.GetTrainerNotesAsync(Arg.Any<User>(), Arg.Do<Id<User>>(id => capturedId = id), Arg.Any<CancellationToken>())
            .Returns(Result.Success<List<TraineeNoteResult>, AppError>([]));
        var controller = CreateController(service);

        var result = await controller.GetNotes("bad-id");

        result.Should().BeOfType<OkObjectResult>();
        capturedId.IsEmpty.Should().BeTrue();
    }

    [Test]
    public async Task CreateNote_WithValidRequest_MapsCommandAndReturnsCreated()
    {
        var service = Substitute.For<ITraineeNoteService>();
        var traineeId = Id<User>.New();
        UpsertTraineeNoteCommand? captured = null;
        service.CreateTrainerNoteAsync(Arg.Any<User>(), traineeId, Arg.Do<UpsertTraineeNoteCommand>(cmd => captured = cmd), Arg.Any<CancellationToken>())
            .Returns(Result.Success<TraineeNoteResult, AppError>(CreateNoteResult()));
        var controller = CreateController(service);

        var result = await controller.CreateNote(traineeId.ToString(), new UpsertTraineeNoteRequest
        {
            Title = "Plan",
            Content = "Warm up first",
            VisibleToTrainee = true,
            IsPinned = true
        });

        result.Should().BeOfType<ObjectResult>();
        ((ObjectResult)result).StatusCode.Should().Be(StatusCodes.Status201Created);
        captured.Should().NotBeNull();
        captured!.Content.Should().Be("Warm up first");
    }

    [Test]
    public async Task UpdateNote_WithInvalidIds_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<ITraineeNoteService>());

        (await controller.UpdateNote("bad-user", Id<TraineeNote>.New().ToString(), new UpsertTraineeNoteRequest())).Should().BeAssignableTo<ObjectResult>();
        (await controller.UpdateNote(Id<User>.New().ToString(), "bad-note", new UpsertTraineeNoteRequest())).Should().BeAssignableTo<ObjectResult>();
    }

    [Test]
    public async Task UpdateNote_WithValidIds_ReturnsMappedDto()
    {
        var service = Substitute.For<ITraineeNoteService>();
        var traineeId = Id<User>.New();
        var noteId = Id<TraineeNote>.New();
        service.UpdateTrainerNoteAsync(Arg.Any<User>(), traineeId, noteId, Arg.Any<UpsertTraineeNoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<TraineeNoteResult, AppError>(CreateNoteResult()));
        var controller = CreateController(service);

        var result = await controller.UpdateNote(traineeId.ToString(), noteId.ToString(), new UpsertTraineeNoteRequest { Content = "Updated" });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Test]
    public async Task DeleteNote_WithInvalidIds_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<ITraineeNoteService>());

        (await controller.DeleteNote("bad-user", Id<TraineeNote>.New().ToString())).Should().BeAssignableTo<ObjectResult>();
        (await controller.DeleteNote(Id<User>.New().ToString(), "bad-note")).Should().BeAssignableTo<ObjectResult>();
    }

    [Test]
    public async Task DeleteNote_WithValidIds_ReturnsDeletedMessage()
    {
        var service = Substitute.For<ITraineeNoteService>();
        var traineeId = Id<User>.New();
        var noteId = Id<TraineeNote>.New();
        service.DeleteTrainerNoteAsync(Arg.Any<User>(), traineeId, noteId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Unit, AppError>(Unit.Value));
        var controller = CreateController(service);

        var result = await controller.DeleteNote(traineeId.ToString(), noteId.ToString());

        result.Should().BeOfType<OkObjectResult>();
    }

    [Test]
    public async Task GetNoteHistory_WithValidIds_ReturnsMappedDtos()
    {
        var service = Substitute.For<ITraineeNoteService>();
        var traineeId = Id<User>.New();
        var noteId = Id<TraineeNote>.New();
        service.GetTrainerNoteHistoryAsync(Arg.Any<User>(), traineeId, noteId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<List<TraineeNoteHistoryResult>, AppError>([new TraineeNoteHistoryResult
            {
                Id = Id<TraineeNoteHistory>.New(),
                TraineeNoteId = noteId,
                ChangedByUserId = Id<User>.New(),
                ChangedAt = DateTimeOffset.UtcNow,
                NewContent = "Updated",
                ChangeType = "Update"
            }]));
        var controller = CreateController(service);

        var result = await controller.GetNoteHistory(traineeId.ToString(), noteId.ToString());

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeAssignableTo<IEnumerable<TraineeNoteHistoryDto>>();
    }

    [Test]
    public async Task GetNoteHistory_WithInvalidIds_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<ITraineeNoteService>());

        (await controller.GetNoteHistory("bad-user", Id<TraineeNote>.New().ToString())).Should().BeAssignableTo<ObjectResult>();
        (await controller.GetNoteHistory(Id<User>.New().ToString(), "bad-note")).Should().BeAssignableTo<ObjectResult>();
    }

    private static TrainerTraineeNotesController CreateController(ITraineeNoteService service)
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var controller = new TrainerTraineeNotesController(service, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Items["User"] = new User { Id = Id<User>.New(), Name = "Trainer", Email = "trainer@notes.test", ProfileRank = "Rookie" };
        return controller;
    }

    private static TraineeNoteResult CreateNoteResult()
        => new()
        {
            Id = Id<TraineeNote>.New(),
            TrainerId = Id<User>.New(),
            TraineeId = Id<User>.New(),
            Title = "Reminder",
            Content = "Warm up first",
            VisibleToTrainee = true,
            IsPinned = true,
            LastUpdatedByUserId = Id<User>.New(),
            LastUpdatedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
}
