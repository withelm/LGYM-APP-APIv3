using LgymApi.Api;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Features.Trainer.Controllers;
using LgymApi.Api.Middleware;
using LgymApi.Application.Coaching.Invitations.Create;
using LgymApi.Application.Coaching.Invitations.CreateByEmail;
using LgymApi.Application.Coaching.Invitations.ListPaginated;
using LgymApi.Application.Coaching.Invitations.Models;
using LgymApi.Application.Coaching.Invitations.Revoke;
using LgymApi.Application.Coaching.ApiAdapters;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainerRelationshipControllerTests
{
    [Test]
    public async Task CreateInvitation_WithInvalidTraineeId_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.CreateInvitation(new CreateTrainerInvitationRequest { TraineeId = "not-a-guid" });

        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task CreateInvitation_WithFocusedResult_MapsLegacyInvitationDto()
    {
        var createInvitation = Substitute.For<ICreateInvitationUseCase>();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        CreateInvitationCommand? captured = null;
        var invitation = new InvitationReadModel(
            Id<TrainerInvitation>.New(),
            trainerId,
            traineeId,
            "trainee@example.com",
            "CODE",
            TrainerInvitationStatus.Pending,
            DateTimeOffset.UtcNow.AddDays(1),
            null,
            DateTimeOffset.UtcNow,
            "Trainee",
            "trainee@example.com");
        createInvitation.ExecuteAsync(Arg.Do<CreateInvitationCommand>(command => captured = command), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<InvitationReadModel, AppError>(invitation)));
        var controller = CreateController(createInvitation, trainerId);

        var result = await controller.CreateInvitation(new CreateTrainerInvitationRequest { TraineeId = traineeId.ToString() });

        var assertedValue161 = ((OkObjectResult)result).Value;
        Assert.That(assertedValue161, Is.TypeOf<TrainerInvitationDto>());
        var dto = ((TrainerInvitationDto)assertedValue161);
        Assert.That(dto.Id, Is.EqualTo(invitation.Id.ToString()));
        Assert.That(dto.Status, Is.EqualTo("Pending"));
        Assert.That(captured, Is.EqualTo(new CreateInvitationCommand(trainerId, traineeId)));
    }

    private static TrainerInvitationController CreateController(ICreateInvitationUseCase? createInvitation = null, Id<User>? trainerId = null)
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var controller = new TrainerInvitationController(
            new TrainerInvitationApiAdapter(createInvitation ?? Substitute.For<ICreateInvitationUseCase>(), Substitute.For<ICreateInvitationByEmailUseCase>(), Substitute.For<IListPaginatedInvitationsUseCase>(), Substitute.For<IRevokeInvitationUseCase>(), mapper),
            mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Features.Set<IAuthenticatedAccountContextFeature>(new AuthenticatedAccountContextFeature(
            new AuthenticatedAccountContext((trainerId ?? Id<User>.New()).Rebind<AccountReference>(), null, [], [], false, false)));
        return controller;
    }
}
