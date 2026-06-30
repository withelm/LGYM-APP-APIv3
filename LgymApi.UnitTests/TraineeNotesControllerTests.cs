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
public sealed class TraineeNotesControllerTests
{
    [Test]
    public async Task GetVisibleNotes_WhenServiceSucceeds_ReturnsMappedDtos()
    {
        var service = Substitute.For<ITraineeNoteService>();
        service.GetVisibleNotesAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<List<TraineeNoteResult>, AppError>([CreateNoteResult()]));
        var controller = CreateController(service);

        var result = await controller.GetVisibleNotes();

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeAssignableTo<IEnumerable<TraineeNoteDto>>();
    }

    [Test]
    public async Task GetVisibleNote_WithInvalidId_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<ITraineeNoteService>());

        var result = await controller.GetVisibleNote("bad-id");

        result.Should().BeAssignableTo<ObjectResult>();
        ((ObjectResult)result).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Test]
    public async Task GetVisibleNote_WhenServiceSucceeds_ReturnsMappedDto()
    {
        var service = Substitute.For<ITraineeNoteService>();
        var noteId = Id<TraineeNote>.New();
        service.GetVisibleNoteAsync(Arg.Any<User>(), noteId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<TraineeNoteResult, AppError>(CreateNoteResult(noteId)));
        var controller = CreateController(service);

        var result = await controller.GetVisibleNote(noteId.ToString());

        result.Should().BeOfType<OkObjectResult>();
    }

    private static TraineeNotesController CreateController(ITraineeNoteService service)
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var controller = new TraineeNotesController(service, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Items["User"] = new User { Id = Id<User>.New(), Name = "Trainee", Email = "trainee@notes.test", ProfileRank = "Rookie" };
        return controller;
    }

    private static TraineeNoteResult CreateNoteResult(Id<TraineeNote>? noteId = null)
        => new()
        {
            Id = noteId ?? Id<TraineeNote>.New(),
            TrainerId = Id<User>.New(),
            TraineeId = Id<User>.New(),
            Title = "Reminder",
            Content = "Stay consistent",
            VisibleToTrainee = true,
            IsPinned = true,
            LastUpdatedByUserId = Id<User>.New(),
            LastUpdatedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
}
