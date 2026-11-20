using LgymApi.Api.Features.EloRegistry.Controllers;
using LgymApi.Api.Features.Exercise.Controllers;
using LgymApi.Api.Features.ExerciseScores.Controllers;
using LgymApi.Api.Features.Gym.Controllers;
using LgymApi.Api.Features.MainRecords.Controllers;
using LgymApi.Api.Features.Measurements.Controllers;
using LgymApi.Api.Features.Training.Controllers;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Features.Exercise;
using LgymApi.Application.Features.Exercise.Models;
using LgymApi.Application.Features.ExerciseScores;
using LgymApi.Application.Features.Gym;
using LgymApi.Application.Features.MainRecords;
using LgymApi.Application.Features.MainRecords.Models;
using LgymApi.Application.Features.Measurements;
using LgymApi.Application.Features.Training;
using LgymApi.Application.WorkoutProgress.ApiAdapters;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class WorkoutProgressApiHandoffTests
{
    private static readonly ControllerServiceContract[] DirectControllerServiceContracts =
    [
        new(typeof(GymController), typeof(IGymService)),
        new(typeof(MeasurementsController), typeof(IMeasurementsService)),
        new(typeof(ExerciseScoresController), typeof(IExerciseScoresService)),
        new(typeof(TrainingController), typeof(ITrainingService)),
        new(typeof(EloRegistryController), typeof(IEloRegistryService))
    ];

    [Test]
    public void HostComposition_ResolvesDirectWorkoutProgressControllerServicesAndRetainedApiAdapters()
    {
        var services = CompositionRootTestHost.Create();
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        foreach (var contract in DirectControllerServiceContracts)
        {
            var assertedValue180 = (services.Where(candidate => candidate.ServiceType == contract.ServiceType)).ToArray();
            Assert.That(assertedValue180, Has.Exactly(1).Items);
            var descriptor = assertedValue180.Single();
            Assert.That(descriptor.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
            Assert.That(scope.ServiceProvider.GetRequiredService(contract.ServiceType), Is.Not.Null);

            var constructors = contract.ControllerType.GetConstructors();
            Assert.That(constructors, Has.Exactly(1).Items);
            Assert.That(constructors[0].GetParameters().Select(parameter => parameter.ParameterType), Does.Contain(contract.ServiceType));
        }

        Assert.That(scope.ServiceProvider.GetRequiredService<IExerciseApiAdapter>(), Is.TypeOf<ExerciseApiAdapter>());
        Assert.That(scope.ServiceProvider.GetRequiredService<IMainRecordsApiAdapter>(), Is.TypeOf<MainRecordsApiAdapter>());
    }

    [Test]
    public async Task ExerciseApiAdapter_AddUserExercise_MapsInputAndDelegatesOnce()
    {
        var exerciseService = Substitute.For<IExerciseService>();
        var accountId = Id<AccountReference>.New();
        var currentAccount = new AuthenticatedAccountContext(accountId, null, [], [], false, false);
        var expectedInput = new AddUserExerciseInput(accountId, "Exercise", BodyParts.Back, "Description", "Image");
        exerciseService.AddUserExerciseAsync(currentAccount, expectedInput, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Unit, AppError>.Success(Unit.Value)));
        var adapter = new ExerciseApiAdapter(exerciseService);

        var result = await adapter.AddUserExerciseAsync(
            currentAccount,
            accountId,
            expectedInput.Name,
            expectedInput.BodyPart,
            expectedInput.Description,
            expectedInput.Image);

        Assert.That(result.IsSuccess, Is.True);
        await exerciseService.Received(1).AddUserExerciseAsync(currentAccount, expectedInput, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MainRecordsApiAdapter_AddNewRecord_MapsInputAndDelegatesOnce()
    {
        var mainRecordsService = Substitute.For<IMainRecordsService>();
        var accountId = Id<AccountReference>.New();
        var exerciseId = Id<LgymApi.Domain.Entities.Exercise>.New();
        var expectedInput = new AddMainRecordInput(accountId, exerciseId, 120, WeightUnits.Kilograms, DateTime.UtcNow);
        mainRecordsService.AddNewRecordAsync(expectedInput, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<Unit, AppError>.Success(Unit.Value)));
        var adapter = new MainRecordsApiAdapter(mainRecordsService);

        var result = await adapter.AddNewRecordAsync(
            accountId,
            exerciseId,
            expectedInput.Weight,
            expectedInput.Unit,
            expectedInput.Date);

        Assert.That(result.IsSuccess, Is.True);
        await mainRecordsService.Received(1).AddNewRecordAsync(expectedInput, Arg.Any<CancellationToken>());
    }

    private sealed record ControllerServiceContract(Type ControllerType, Type ServiceType);
}
