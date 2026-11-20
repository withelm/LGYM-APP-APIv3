using System.Reflection;
using LgymApi.Application.Nutrition;
using LgymApi.Application.Nutrition.Supplementation.AssignTraineePlan;
using LgymApi.Application.Nutrition.Supplementation.CheckOffIntake.Contracts;
using LgymApi.Application.Nutrition.Supplementation.CheckOffIntake.Models;
using LgymApi.Application.Nutrition.Supplementation.CreateTraineePlan;
using LgymApi.Application.Nutrition.Supplementation.DeleteTraineePlan.Contracts;
using LgymApi.Application.Nutrition.Supplementation.DeleteTraineePlan.Models;
using LgymApi.Application.Nutrition.Supplementation.GetComplianceSummary;
using LgymApi.Application.Nutrition.Supplementation.GetSchedule.Contracts;
using LgymApi.Application.Nutrition.Supplementation.GetSchedule.Models;
using LgymApi.Application.Nutrition.Supplementation.GetTraineePlans;
using LgymApi.Application.Nutrition.Supplementation.UnassignTraineePlan;
using LgymApi.Application.Nutrition.Supplementation.UnassignTraineePlan.Contracts;
using LgymApi.Application.Nutrition.Supplementation.UpdateTraineePlan;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class NutritionSupplementSliceArchitectureTests
{
    private static readonly (Type Contract, string ImplementationName)[] Slices =
    [
        (typeof(ICreateTraineeSupplementPlanUseCase), "LgymApi.Application.Nutrition.Supplementation.CreateTraineePlan.CreateTraineeSupplementPlanUseCase"),
        (typeof(IUpdateTraineeSupplementPlanUseCase), "LgymApi.Application.Nutrition.Supplementation.UpdateTraineePlan.UpdateTraineeSupplementPlanUseCase"),
        (typeof(IDeleteTraineeSupplementPlanUseCase), "LgymApi.Application.Nutrition.Supplementation.DeleteTraineePlan.DeleteTraineeSupplementPlanUseCase"),
        (typeof(IAssignTraineeSupplementPlanUseCase), "LgymApi.Application.Nutrition.Supplementation.AssignTraineePlan.AssignTraineeSupplementPlanUseCase"),
        (typeof(IUnassignTraineeSupplementPlanUseCase), "LgymApi.Application.Nutrition.Supplementation.UnassignTraineePlan.UnassignTraineeSupplementPlanUseCase"),
        (typeof(IGetTraineeSupplementPlansUseCase), "LgymApi.Application.Nutrition.Supplementation.GetTraineePlans.GetTraineeSupplementPlansUseCase"),
        (typeof(IGetSupplementScheduleUseCase), "LgymApi.Application.Nutrition.Supplementation.GetSchedule.GetSupplementScheduleUseCase"),
        (typeof(IGetSupplementComplianceSummaryUseCase), "LgymApi.Application.Nutrition.Supplementation.GetComplianceSummary.GetSupplementComplianceSummaryUseCase"),
        (typeof(ICheckOffSupplementIntakeUseCase), "LgymApi.Application.Nutrition.Supplementation.CheckOffIntake.CheckOffSupplementIntakeUseCase")
    ];

    [Test]
    public void SupplementSlices_ExposeOneMethodPublicContractsWithInternalImplementations()
    {
        var assembly = typeof(ICheckOffSupplementIntakeUseCase).Assembly;

        foreach (var slice in Slices)
        {
            Assert.That(slice.Contract.IsPublic, Is.True);
            Assert.That(slice.Contract.GetMethods(BindingFlags.Public | BindingFlags.Instance), Has.Exactly(1).Items);
            Assert.That(assembly.GetType(slice.ImplementationName)!.IsNotPublic, Is.True);
        }
    }

    [Test]
    public void SupplementPublicInputs_AreSealedAndRecursivelyUseApprovedTypes()
    {
        var models = new[]
        {
            typeof(CreateTraineeSupplementPlanCommand), typeof(UpdateTraineeSupplementPlanCommand),
            typeof(DeleteTraineeSupplementPlanCommand), typeof(AssignTraineeSupplementPlanCommand),
            typeof(UnassignTraineeSupplementPlanCommand), typeof(GetTraineeSupplementPlansQuery),
            typeof(GetSupplementScheduleQuery), typeof(GetSupplementComplianceSummaryQuery),
            typeof(CheckOffSupplementIntakeCommand)
        };

        Assert.That((models).All(model => model.IsSealed && model.GetProperties().All(property => IsAllowed(property.PropertyType))), Is.True);
    }

    [Test]
    public void SupplementSlices_ExposeCancellationOnTheirOnlyMethod()
    {
        foreach (var (contract, _) in Slices)
        {
            Assert.That((contract.GetMethods().Single().GetParameters()).Any(parameter => parameter.ParameterType == typeof(CancellationToken)), Is.True);
        }
    }

    [Test]
    public void SupplementSlices_AreRegisteredExactlyOnce()
    {
        var services = new ServiceCollection();
        services.AddNutritionModule();

        foreach (var (contract, _) in Slices)
        {
            Assert.That(services.Count(descriptor => descriptor.ServiceType == contract), Is.EqualTo(1));
        }
    }

    private static bool IsAllowed(Type type)
    {
        var nullableType = Nullable.GetUnderlyingType(type);
        if (nullableType is not null)
        {
            return IsAllowed(nullableType);
        }

        if (type == typeof(string) || type == typeof(bool) || type == typeof(int)
            || type == typeof(decimal) || type == typeof(DateOnly) || type == typeof(DateTimeOffset))
        {
            return true;
        }

        if (type == typeof(LgymApi.Authorization.Contracts.AuthorizationActor))
        {
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(LgymApi.Domain.ValueObjects.Id<>))
        {
            return type.GetGenericArguments().Single() is var argument
                && argument is not null
                && argument.Namespace == "LgymApi.Domain.Entities"
                && argument.Name is "User" or "SupplementPlan" or "SupplementPlanItem";
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            return IsAllowed(type.GetGenericArguments().Single());
        }

        return type.Namespace == "LgymApi.Application.Nutrition.Supplementation.Models"
            && type.GetProperties().All(property => IsAllowed(property.PropertyType));
    }
}
