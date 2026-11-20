using LgymApi.Api;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Features.Trainer.Controllers;
using LgymApi.Api.Middleware;
using LgymApi.Application.Coaching.Invitations.Accept;
using LgymApi.Application.Coaching.Invitations.Reject;
using LgymApi.Application.Coaching.ManagedPlans.GetActive;
using LgymApi.Application.Coaching.Relationships.DetachFromTrainer;
using LgymApi.Application.Coaching.Relationships.GetCurrentTrainer;
using LgymApi.Application.Coaching.ApiAdapters;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TraineeRelationshipControllerTests
{
    [Test]
    public async Task AcceptInvitation_WhenFocusedUseCaseSucceeds_ForwardsTypedIdsAndReturnsLegacyMessage()
    {
        var acceptInvitation = Substitute.For<IAcceptInvitationUseCase>();
        var traineeId = Id<User>.New();
        var invitationId = Id<TrainerInvitation>.New();
        AcceptInvitationCommand? captured = null;
        acceptInvitation.ExecuteAsync(Arg.Do<AcceptInvitationCommand>(command => captured = command), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Unit, AppError>(Unit.Value));
        var controller = CreateController(acceptInvitation: acceptInvitation, traineeId: traineeId);

        var result = await controller.AcceptInvitation(invitationId.ToString());

        var assertedValue157 = ((OkObjectResult)result).Value;
        Assert.That(assertedValue157, Is.TypeOf<ResponseMessageDto>());
        var message = ((ResponseMessageDto)assertedValue157);
        Assert.That(string.IsNullOrWhiteSpace(message.Message), Is.False);
        Assert.That(captured, Is.EqualTo(new AcceptInvitationCommand(traineeId, invitationId)));
    }

    [Test]
    public async Task GetCurrentTrainer_WhenFocusedUseCaseSucceeds_MapsLegacyProfileDto()
    {
        var currentTrainer = Substitute.For<IGetCurrentTrainerUseCase>();
        var traineeId = Id<User>.New();
        var trainerId = Id<User>.New();
        currentTrainer.ExecuteAsync(Arg.Any<GetCurrentTrainerQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<CurrentTrainerReadModel, AppError>(new CurrentTrainerReadModel(
                trainerId,
                "Trainer",
                "trainer@example.test",
                "avatar.png",
                DateTimeOffset.UtcNow)));
        var controller = CreateController(currentTrainer: currentTrainer, traineeId: traineeId);

        var result = await controller.GetCurrentTrainer();

        var assertedValue158 = ((OkObjectResult)result).Value;
        Assert.That(assertedValue158, Is.TypeOf<TraineeTrainerProfileDto>());
        var profile = ((TraineeTrainerProfileDto)assertedValue158);
        Assert.That(profile.TrainerId, Is.EqualTo(trainerId.ToString()));
        Assert.That(profile.Name, Is.EqualTo("Trainer"));
    }

    [Test]
    public async Task GetActiveAssignedPlan_WhenFocusedUseCaseSucceeds_MapsLegacyPlanDto()
    {
        var activePlan = Substitute.For<IGetActiveManagedPlanUseCase>();
        var traineeId = Id<User>.New();
        var plan = new ManagedPlanReadModel(Id<Plan>.New(), "Active plan", true, DateTimeOffset.UtcNow);
        activePlan.ExecuteAsync(Arg.Any<GetActiveManagedPlanQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ManagedPlanReadModel, AppError>(plan));
        var controller = CreateController(activePlan: activePlan, traineeId: traineeId);

        var result = await controller.GetActiveAssignedPlan();

        var assertedValue159 = ((OkObjectResult)result).Value;
        Assert.That(assertedValue159, Is.TypeOf<TrainerManagedPlanDto>());
        var response = ((TrainerManagedPlanDto)assertedValue159);
        Assert.That(response.Id, Is.EqualTo(plan.Id.ToString()));
        Assert.That(response.Name, Is.EqualTo(plan.Name));
    }

    private static TraineeRelationshipController CreateController(
        IAcceptInvitationUseCase? acceptInvitation = null,
        IRejectInvitationUseCase? rejectInvitation = null,
        IDetachFromTrainerUseCase? detachFromTrainer = null,
        IGetCurrentTrainerUseCase? currentTrainer = null,
        IGetActiveManagedPlanUseCase? activePlan = null,
        Id<User>? traineeId = null)
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var controller = new TraineeRelationshipController(
            new TraineeRelationshipApiAdapter(acceptInvitation ?? Substitute.For<IAcceptInvitationUseCase>(), rejectInvitation ?? Substitute.For<IRejectInvitationUseCase>(), detachFromTrainer ?? Substitute.For<IDetachFromTrainerUseCase>(), currentTrainer ?? Substitute.For<IGetCurrentTrainerUseCase>(), activePlan ?? Substitute.For<IGetActiveManagedPlanUseCase>(), mapper),
            mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Features.Set<IAuthenticatedAccountContextFeature>(new AuthenticatedAccountContextFeature(
            new AuthenticatedAccountContext((traineeId ?? Id<User>.New()).Rebind<AccountReference>(), null, [], [], false, false)));
        return controller;
    }
}
