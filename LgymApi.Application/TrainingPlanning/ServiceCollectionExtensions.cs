using LgymApi.Application.Features.PlanDay;
using LgymApi.Application.TrainingPlanning.Plan.CheckIsUserHavePlan;
using LgymApi.Application.TrainingPlanning.Plan.CopyPlan;
using LgymApi.Application.TrainingPlanning.Plan.CreatePlan;
using LgymApi.Application.TrainingPlanning.Plan.DeletePlan;
using LgymApi.Application.TrainingPlanning.Plan.GenerateShareCode;
using LgymApi.Application.TrainingPlanning.Plan.GetPlanConfig;
using LgymApi.Application.TrainingPlanning.Plan.GetPlansList;
using LgymApi.Application.TrainingPlanning.Plan.SetActivePlan;
using LgymApi.Application.TrainingPlanning.Plan.UpdatePlan;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application.TrainingPlanning;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTrainingPlanningModule(this IServiceCollection services)
    {
        services.AddScoped<ICreatePlanUseCase, CreatePlanUseCase>();
        services.AddScoped<IUpdatePlanUseCase, UpdatePlanUseCase>();
        services.AddScoped<IDeletePlanUseCase, DeletePlanUseCase>();
        services.AddScoped<IGetPlanConfigUseCase, GetPlanConfigUseCase>();
        services.AddScoped<IGetPlansListUseCase, GetPlansListUseCase>();
        services.AddScoped<ISetActivePlanUseCase, SetActivePlanUseCase>();
        services.AddScoped<ICopyPlanUseCase, CopyPlanUseCase>();
        services.AddScoped<IGenerateShareCodeUseCase, GenerateShareCodeUseCase>();
        services.AddScoped<ICheckIsUserHavePlanUseCase, CheckIsUserHavePlanUseCase>();
        services.AddScoped<IPlanDayServiceDependencies, PlanDayServiceDependencies>();
        services.AddScoped<IPlanDayService, PlanDayService>();

        return services;
    }
}
