using LgymApi.Api;
using LgymApi.Api.Features.Public.Contracts;
using LgymApi.Api.Features.Public.Controllers;
using LgymApi.Application.Coaching.Invitations.PublicStatus;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Coaching.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PublicInvitationControllerTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public async Task GetInvitationStatus_WithBlankCode_ReturnsNotFoundWithoutCallingUseCase(string? code)
    {
        var useCase = Substitute.For<IPublicInvitationStatusUseCase>();
        var controller = CreateController(useCase);

        var result = await controller.GetInvitationStatus(Id<TrainerInvitation>.New().ToString(), code);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
        await useCase.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    [Test]
    public async Task GetInvitationStatus_WithMalformedId_ReturnsNotFoundWithoutCallingUseCase()
    {
        var useCase = Substitute.For<IPublicInvitationStatusUseCase>();
        var controller = CreateController(useCase);

        var result = await controller.GetInvitationStatus("not-a-guid", "exact-code");

        Assert.That(result, Is.TypeOf<NotFoundResult>());
        await useCase.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    [Test]
    public async Task GetInvitationStatus_WithSuccessfulUseCase_ForwardsQueryAndMapsLegacyDto()
    {
        var invitationId = Id<TrainerInvitation>.New();
        using var cancellation = new CancellationTokenSource();
        var useCase = Substitute.For<IPublicInvitationStatusUseCase>();
        useCase.ExecuteAsync(Arg.Any<PublicInvitationStatusQuery>(), cancellation.Token)
            .Returns(Result<PublicInvitationStatusReadModel, AppError>.Success(
                new PublicInvitationStatusReadModel(TrainerInvitationStatus.Accepted, true)));
        var controller = CreateController(useCase);

        var result = await controller.GetInvitationStatus(invitationId.ToString(), "exact-code", cancellation.Token);

        var assertedValue142 = result;
        Assert.That(assertedValue142, Is.TypeOf<OkObjectResult>());
        var response = ((OkObjectResult)assertedValue142);
        Assert.That(response.Value, Is.EqualTo(new PublicInvitationStatusDto
        {
            Status = "Accepted",
            UserExists = true
        }).UsingPropertiesComparer());
        await useCase.Received(1).ExecuteAsync(
            Arg.Is<PublicInvitationStatusQuery>(query => query.InvitationId == invitationId && query.Code == "exact-code"),
            cancellation.Token);
    }

    [Test]
    public async Task GetInvitationStatus_WithUseCaseFailure_ReturnsBareNotFound()
    {
        var useCase = Substitute.For<IPublicInvitationStatusUseCase>();
        useCase.ExecuteAsync(Arg.Any<PublicInvitationStatusQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PublicInvitationStatusReadModel, AppError>.Failure(
                new TrainerRelationshipNotFoundError(Messages.DidntFind)));
        var controller = CreateController(useCase);

        var result = await controller.GetInvitationStatus(Id<TrainerInvitation>.New().ToString(), "wrong-code");

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    private static PublicInvitationController CreateController(IPublicInvitationStatusUseCase useCase)
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        using var provider = services.BuildServiceProvider();
        return new PublicInvitationController(useCase, provider.GetRequiredService<IMapper>());
    }
}
