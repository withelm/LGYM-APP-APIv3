using System.Reflection;
using LgymApi.Application.Coaching;
using LgymApi.Application.Coaching.ManagedPlans.Assign;
using LgymApi.Application.Coaching.ManagedPlans.Create;
using LgymApi.Application.Coaching.ManagedPlans.Delete;
using LgymApi.Application.Coaching.ManagedPlans.GetActive;
using LgymApi.Application.Coaching.ManagedPlans.List;
using LgymApi.Application.Coaching.ManagedPlans.Unassign;
using LgymApi.Application.Coaching.ManagedPlans.Update;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Identity.Contracts;
using LgymApi.TrainingPlanning.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class CoachingManagedPlanSliceArchitectureTests
{
    private static readonly (Type Contract, string ImplementationName)[] Slices =
    [
        (typeof(IListManagedPlansUseCase), "LgymApi.Application.Coaching.ManagedPlans.List.ListManagedPlansUseCase"),
        (typeof(LgymApi.Application.Coaching.ManagedPlans.Create.ICreateTraineeManagedPlanUseCase), "LgymApi.Application.Coaching.ManagedPlans.Create.CreateTraineeManagedPlanUseCase"),
        (typeof(LgymApi.Application.Coaching.ManagedPlans.Update.IUpdateTraineeManagedPlanUseCase), "LgymApi.Application.Coaching.ManagedPlans.Update.UpdateTraineeManagedPlanUseCase"),
        (typeof(LgymApi.Application.Coaching.ManagedPlans.Delete.IDeleteTraineeManagedPlanUseCase), "LgymApi.Application.Coaching.ManagedPlans.Delete.DeleteTraineeManagedPlanUseCase"),
        (typeof(LgymApi.Application.Coaching.ManagedPlans.Assign.IAssignTraineeManagedPlanUseCase), "LgymApi.Application.Coaching.ManagedPlans.Assign.AssignTraineeManagedPlanUseCase"),
        (typeof(LgymApi.Application.Coaching.ManagedPlans.Unassign.IUnassignTraineeManagedPlanUseCase), "LgymApi.Application.Coaching.ManagedPlans.Unassign.UnassignTraineeManagedPlanUseCase"),
        (typeof(IGetActiveManagedPlanUseCase), "LgymApi.Application.Coaching.ManagedPlans.GetActive.GetActiveManagedPlanUseCase")
    ];

    [Test]
    public void ManagedPlanSlices_ExposeOneMethodContractsWithInternalImplementations()
    {
        var assembly = typeof(IListManagedPlansUseCase).Assembly;

        foreach (var slice in Slices)
        {
            Assert.That(slice.Contract.IsPublic, Is.True);
            Assert.That(slice.Contract.GetMethods(BindingFlags.Public | BindingFlags.Instance), Has.Exactly(1).Items);
            Assert.That(assembly.GetType(slice.ImplementationName)!.IsNotPublic, Is.True);
        }
    }

    [Test]
    public void ManagedPlanSlices_AreRegisteredExactlyOnceByCoachingModule()
    {
        var services = new ServiceCollection();
        services.AddCoachingModule();

        foreach (var slice in Slices)
        {
            Assert.That(services.Count(descriptor => descriptor.ServiceType == slice.Contract), Is.EqualTo(1));
        }
    }

    [Test]
    public void ManagedPlanPublicInputs_AreSealedRecordsWithOnlyScalarsAndTypedIdentifiers()
    {
        var models = new[]
        {
            typeof(ListManagedPlansQuery),
            typeof(LgymApi.Application.Coaching.ManagedPlans.Create.CreateTraineeManagedPlanCommand),
            typeof(LgymApi.Application.Coaching.ManagedPlans.Update.UpdateTraineeManagedPlanCommand),
            typeof(LgymApi.Application.Coaching.ManagedPlans.Delete.DeleteTraineeManagedPlanCommand),
            typeof(LgymApi.Application.Coaching.ManagedPlans.Assign.AssignTraineeManagedPlanCommand),
            typeof(LgymApi.Application.Coaching.ManagedPlans.Unassign.UnassignTraineeManagedPlanCommand),
            typeof(GetActiveManagedPlanQuery)
        };

        foreach (var model in models)
        {
            Assert.That(model.IsSealed, Is.True);
            Assert.That((model.GetProperties().Select(property => property.PropertyType)).All(type => IsAllowedInputType(type)), Is.True);
        }
    }

    [Test]
    public void ManagedPlanSlices_DoNotExposeOrAccessForeignRepositoriesOrEntityValues()
    {
        var assembly = typeof(IListManagedPlansUseCase).Assembly;
        var managedPlanTypes = assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("LgymApi.Application.Coaching.ManagedPlans", StringComparison.Ordinal) == true)
            .ToArray();
        var exposedTypes = managedPlanTypes
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(field => field.FieldType)
                .Concat(type.GetProperties().Select(property => property.PropertyType))
                .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType))))
            .ToArray();

        Assert.That(exposedTypes.Select(type => type.FullName), Does.Not.Contain("LgymApi.Application.Repositories.IUserRepository"));
        Assert.That(exposedTypes, Does.Not.Contain(typeof(Plan)));
        Assert.That(exposedTypes, Does.Not.Contain(typeof(User)));

        var root = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var directory = Path.Combine(root, "LgymApi.Application", "Coaching", "ManagedPlans");
        var source = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();
        Assert.That((source).Any(text => text.Contains("LgymApi.Application.Repositories", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("IPlanRepository", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("IPlanDayRepository", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("IUserRepository", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("LgymApi.Domain.Entities.Plan", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("LgymApi.Domain.Entities.User", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("LgymApi.Application.TrainingPlanning.ManagedPlans", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("TrainerRelationshipService", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("LgymApi.Infrastructure", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("SaveChangesAsync", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("BeginTransactionAsync", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void ManagedPlanSlices_UseRegisteredInputMappingAndDoNotCreateOwnerInputsInline()
    {
        var root = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var directory = Path.Combine(root, "LgymApi.Application", "Coaching", "ManagedPlans");
        var profile = File.ReadAllText(Path.Combine(directory, "ManagedPlanCollaborationMappingProfile.cs"));
        var useCases = Directory.GetFiles(directory, "*UseCase.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).StartsWith("I", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToArray();

        Assert.That((useCases).All(source => source.Contains("_mapper.CreateContext()", StringComparison.Ordinal)), Is.True);
        Assert.That((useCases).Any(source => source.Contains("new Owner", StringComparison.Ordinal)), Is.False);
        Assert.That(profile.Split("configuration.CreateMap<", StringSplitOptions.None).Length, Is.EqualTo(8));
    }

    [Test]
    public void TrainingPlanningManagedPlanOwner_DoesNotDependOnCoaching()
    {
        var sourceFiles = ArchitectureTestHelpers.EnumerateProductionSourceFiles("LgymApi.TrainingPlanning");
        Assert.That(sourceFiles, Is.Not.Empty);
        var source = sourceFiles
            .Select(File.ReadAllText);

        Assert.That((source).Any(text => text.Contains("LgymApi.Application.Coaching", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void TrainingPlanningManagedPlanOwner_UsesTrainingPlanningPlanErrors()
    {
        var root = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var useCaseFiles = ArchitectureTestHelpers.EnumerateProductionSourceFiles("LgymApi.TrainingPlanning")
            .Where(path => ArchitectureTestHelpers.NormalizePath(Path.GetRelativePath(root, path))
                .StartsWith("LgymApi.TrainingPlanning/ManagedPlans/", StringComparison.Ordinal))
            .Where(path => Path.GetFileName(path).EndsWith("UseCase.cs", StringComparison.Ordinal))
            .Where(path => !Path.GetFileName(path).StartsWith("I", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.That(useCaseFiles, Has.Exactly(7).Items);
        var useCaseSources = useCaseFiles
            .Select(File.ReadAllText);

        Assert.That((useCaseSources).All(source =>
            source.Contains("LgymApi.Application.TrainingPlanning.Errors", StringComparison.Ordinal)), Is.True);
    }

    private static bool IsAllowedInputType(Type type)
    {
        if (type == typeof(string))
        {
            return true;
        }

        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(LgymApi.Domain.ValueObjects.Id<>))
        {
            return false;
        }

        return type.GetGenericArguments().Single() is var argument
            && (argument == typeof(AccountReference) || argument == typeof(PlanReference));
    }
}
