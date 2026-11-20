using System.Reflection;
using LgymApi.Application.Coaching;
using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.Coaching.TraineeNotes.Create;
using LgymApi.Application.Coaching.TraineeNotes.Delete;
using LgymApi.Application.Coaching.TraineeNotes.History;
using LgymApi.Application.Coaching.TraineeNotes.Models;
using LgymApi.Application.Coaching.TraineeNotes.TrainerList;
using LgymApi.Application.Coaching.TraineeNotes.Update;
using LgymApi.Application.Coaching.TraineeNotes.VisibleList;
using LgymApi.Application.Coaching.TraineeNotes.VisibleSingle;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class CoachingTraineeNoteSliceArchitectureTests
{
    private static readonly (Type Contract, string ImplementationName)[] Slices =
    [
        (typeof(IListTrainerNotesUseCase), "LgymApi.Application.Coaching.TraineeNotes.TrainerList.ListTrainerNotesUseCase"),
        (typeof(ICreateTraineeNoteUseCase), "LgymApi.Application.Coaching.TraineeNotes.Create.CreateTraineeNoteUseCase"),
        (typeof(IUpdateTraineeNoteUseCase), "LgymApi.Application.Coaching.TraineeNotes.Update.UpdateTraineeNoteUseCase"),
        (typeof(IDeleteTraineeNoteUseCase), "LgymApi.Application.Coaching.TraineeNotes.Delete.DeleteTraineeNoteUseCase"),
        (typeof(IGetTraineeNoteHistoryUseCase), "LgymApi.Application.Coaching.TraineeNotes.History.GetTraineeNoteHistoryUseCase"),
        (typeof(IListVisibleTraineeNotesUseCase), "LgymApi.Application.Coaching.TraineeNotes.VisibleList.ListVisibleTraineeNotesUseCase"),
        (typeof(IGetVisibleTraineeNoteUseCase), "LgymApi.Application.Coaching.TraineeNotes.VisibleSingle.GetVisibleTraineeNoteUseCase")
    ];

    [Test]
    public void TrainerNoteSlices_ExposeOneMethodContractsWithInternalImplementations()
    {
        var assembly = typeof(IListTrainerNotesUseCase).Assembly;

        foreach (var slice in Slices)
        {
            Assert.That(slice.Contract.IsPublic, Is.True);
            Assert.That(slice.Contract.GetMethods(BindingFlags.Public | BindingFlags.Instance), Has.Exactly(1).Items);
            Assert.That(assembly.GetType(slice.ImplementationName)!.IsNotPublic, Is.True);
        }
    }

    [Test]
    public void TrainerNoteSlices_AreRegisteredExactlyOnceByCoachingModule()
    {
        var services = new ServiceCollection();
        services.AddCoachingModule();

        foreach (var slice in Slices)
        {
            Assert.That(services.Count(descriptor => descriptor.ServiceType == slice.Contract), Is.EqualTo(1));
        }
    }

    [Test]
    public void TrainerNotePublicInputs_AreImmutableAndUseOnlyTypedIdentifiersAndUpsertData()
    {
        var inputs = new[]
        {
            typeof(ListTrainerNotesQuery),
            typeof(CreateTraineeNoteCommand),
            typeof(UpdateTraineeNoteCommand),
            typeof(DeleteTraineeNoteCommand),
            typeof(GetTraineeNoteHistoryQuery),
            typeof(ListVisibleTraineeNotesQuery),
            typeof(GetVisibleTraineeNoteQuery),
            typeof(TraineeNoteUpsertData)
        };

        Assert.That((inputs).All(type => type.IsSealed), Is.True);
        Assert.That((typeof(ListTrainerNotesQuery).GetProperties()).All(property => IsTypedId(property.PropertyType)), Is.True);
        Assert.That((typeof(DeleteTraineeNoteCommand).GetProperties()).All(property => IsTypedId(property.PropertyType)), Is.True);
        Assert.That((typeof(GetTraineeNoteHistoryQuery).GetProperties()).All(property => IsTypedId(property.PropertyType)), Is.True);
        Assert.That((typeof(ListVisibleTraineeNotesQuery).GetProperties()).All(property => IsTypedId(property.PropertyType)), Is.True);
        Assert.That((typeof(GetVisibleTraineeNoteQuery).GetProperties()).All(property => IsTypedId(property.PropertyType)), Is.True);
        Assert.That(typeof(CreateTraineeNoteCommand).GetProperty(nameof(CreateTraineeNoteCommand.Data))!.PropertyType, Is.EqualTo(typeof(TraineeNoteUpsertData)));
        Assert.That(typeof(UpdateTraineeNoteCommand).GetProperty(nameof(UpdateTraineeNoteCommand.Data))!.PropertyType, Is.EqualTo(typeof(TraineeNoteUpsertData)));
        Assert.That((typeof(TraineeNoteUpsertData).GetProperties().Select(property => property.PropertyType)).All(type => type == typeof(string) || type == typeof(bool)), Is.True);
    }

    [Test]
    public void TraineeVisibleNoteSlices_ExposeOnlyReadDependenciesAndImmutableReadModels()
    {
        var assembly = typeof(IListVisibleTraineeNotesUseCase).Assembly;
        var implementationNames = new[]
        {
            "LgymApi.Application.Coaching.TraineeNotes.VisibleList.ListVisibleTraineeNotesUseCase",
            "LgymApi.Application.Coaching.TraineeNotes.VisibleSingle.GetVisibleTraineeNoteUseCase"
        };

        foreach (var implementationName in implementationNames)
        {
            Assert.That(assembly.GetType(implementationName)!.GetConstructors().Single().GetParameters()
                .Select(parameter => parameter.ParameterType), Is.EquivalentTo(new[] { typeof(ICoachingTraineeNotePersistence), typeof(IAccountReadService), typeof(IMapper) }));
        }

        Assert.That(typeof(IListVisibleTraineeNotesUseCase).GetMethods().Single().ReturnType, Is.EqualTo(typeof(Task<Result<IReadOnlyList<TraineeNoteReadModel>, AppError>>)));
        Assert.That(typeof(IGetVisibleTraineeNoteUseCase).GetMethods().Single().ReturnType, Is.EqualTo(typeof(Task<Result<TraineeNoteReadModel, AppError>>)));
    }

    [Test]
    public void TrainerNoteSlices_DoNotDependOnLegacyServicesRepositoriesOrUserValues()
    {
        var root = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var directory = Path.Combine(root, "LgymApi.Application", "Coaching", "TraineeNotes");
        var source = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();

        Assert.That((source).Any(text => text.Contains("IUserRepository", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("ITraineeNoteService", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("ITraineeNoteRepository", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("LgymApi.Infrastructure", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void TrainerNoteSlices_UseRegisteredMappingForEveryFactWriteAndReadTransformation()
    {
        var root = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var directory = Path.Combine(root, "LgymApi.Application", "Coaching", "TraineeNotes");
        var profile = File.ReadAllText(Path.Combine(directory, "TraineeNoteMappingProfile.cs"));
        var useCases = Directory.GetFiles(directory, "*UseCase.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).StartsWith("I", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToArray();

        Assert.That(profile.Split("configuration.CreateMap<", StringSplitOptions.None).Length, Is.EqualTo(8));
        Assert.That((useCases).All(source => source.Contains("_mapper.CreateContext()", StringComparison.Ordinal)), Is.True);
        Assert.That((useCases).Any(source => source.Contains("new CoachingTraineeNoteWriteModel", StringComparison.Ordinal)), Is.False);
        Assert.That((useCases).Any(source => source.Contains("new CoachingTraineeNoteHistoryWriteModel", StringComparison.Ordinal)), Is.False);
        Assert.That((useCases).Any(source => source.Contains("new TraineeNoteReadModel", StringComparison.Ordinal)), Is.False);
        Assert.That((useCases).Any(source => source.Contains("new TraineeNoteHistoryReadModel", StringComparison.Ordinal)), Is.False);
    }

    private static bool IsTypedId(Type type)
        => type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(Id<>)
            && (type.GetGenericArguments().Single() == typeof(User)
                || type.GetGenericArguments().Single() == typeof(TraineeNote));
}
