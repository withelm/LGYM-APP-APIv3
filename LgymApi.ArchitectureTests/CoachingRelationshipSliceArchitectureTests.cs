using System.Reflection;
using LgymApi.Application.Coaching;
using LgymApi.Application.Coaching.Relationships.DetachFromTrainer;
using LgymApi.Application.Coaching.Relationships.GetCurrentTrainer;
using LgymApi.Application.Coaching.Relationships.UnlinkTrainee;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class CoachingRelationshipSliceArchitectureTests
{
    private static readonly (Type Contract, string ImplementationName)[] Slices =
    [
        (typeof(IUnlinkTraineeUseCase), "LgymApi.Application.Coaching.Relationships.UnlinkTrainee.UnlinkTraineeUseCase"),
        (typeof(IDetachFromTrainerUseCase), "LgymApi.Application.Coaching.Relationships.DetachFromTrainer.DetachFromTrainerUseCase"),
        (typeof(IGetCurrentTrainerUseCase), "LgymApi.Application.Coaching.Relationships.GetCurrentTrainer.GetCurrentTrainerUseCase")
    ];

    [Test]
    public void RelationshipSlices_ExposeOneMethodPublicContractsWithInternalImplementations()
    {
        var assembly = typeof(IUnlinkTraineeUseCase).Assembly;

        foreach (var slice in Slices)
        {
            Assert.That(slice.Contract.IsPublic, Is.True);
            Assert.That(slice.Contract.GetMethods(BindingFlags.Public | BindingFlags.Instance), Has.Exactly(1).Items);
            Assert.That(assembly.GetType(slice.ImplementationName)!.IsNotPublic, Is.True);
        }
    }

    [Test]
    public void RelationshipSlices_AreRegisteredExactlyOnceByTheCoachingModule()
    {
        var services = new ServiceCollection();
        services.AddCoachingModule();

        foreach (var slice in Slices)
        {
            Assert.That(services.Count(descriptor => descriptor.ServiceType == slice.Contract), Is.EqualTo(1));
        }
    }

    [Test]
    public void RelationshipSlicePublicModels_AreSealedRecordsWithTypedIdentifiers()
    {
        Assert.That(typeof(UnlinkTraineeCommand).IsSealed, Is.True);
        Assert.That(typeof(DetachFromTrainerCommand).IsSealed, Is.True);
        Assert.That(typeof(GetCurrentTrainerQuery).IsSealed, Is.True);
        Assert.That(typeof(CurrentTrainerReadModel).IsSealed, Is.True);

        Assert.That((typeof(UnlinkTraineeCommand).GetProperties()).All(property => IsUserId(property.PropertyType)), Is.True);
        Assert.That((typeof(DetachFromTrainerCommand).GetProperties()).All(property => IsUserId(property.PropertyType)), Is.True);
        Assert.That((typeof(GetCurrentTrainerQuery).GetProperties()).All(property => IsUserId(property.PropertyType)), Is.True);
        Assert.That(typeof(CurrentTrainerReadModel).GetProperty(nameof(CurrentTrainerReadModel.TrainerId))!.PropertyType, Is.EqualTo(typeof(LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)));
    }

    [Test]
    public void RelationshipSlices_DoNotDependOnLegacyUserOrRelationshipRepositories()
    {
        var root = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var relationshipDirectory = Path.Combine(root, "LgymApi.Application", "Coaching", "Relationships");
        var source = Directory.GetFiles(relationshipDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();

        Assert.That((source).Any(text => text.Contains("IUserRepository", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("ITrainerRelationshipRepository", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains(" User ", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void GetCurrentTrainer_DelegatesProfileTransformationToTheRegisteredMapper()
    {
        var root = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var useCase = Path.Combine(
            root,
            "LgymApi.Application",
            "Coaching",
            "Relationships",
            "GetCurrentTrainer",
            "GetCurrentTrainerUseCase.cs");
        var mappingProfile = Path.Combine(
            root,
            "LgymApi.Application",
            "Coaching",
            "Relationships",
            "RelationshipMappingProfile.cs");
        var source = File.ReadAllText(useCase);

        Assert.That(source, Does.Not.Contain("new CurrentTrainerReadModel("));
        Assert.That(source, Does.Contain("_mapper.Map<CurrentTrainerSource, CurrentTrainerReadModel>("));
        Assert.That(source, Does.Contain("_mapper.CreateContext()"));
        Assert.That(File.ReadAllText(mappingProfile), Does.Contain("CreateMap<CurrentTrainerSource, CurrentTrainerReadModel>"));
    }

    private static bool IsUserId(Type type)
        => type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(LgymApi.Domain.ValueObjects.Id<>)
            && type.GetGenericArguments().Single() == typeof(LgymApi.Domain.Entities.User);
}
