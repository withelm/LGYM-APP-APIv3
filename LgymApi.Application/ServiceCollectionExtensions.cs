using LgymApi.Application.Features.AppConfig;
using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Features.Enum;
using LgymApi.Application.Features.Exercise;
using LgymApi.Application.Features.ExerciseScores;
using LgymApi.Application.Features.Gym;
using LgymApi.Application.Features.MainRecords;
using LgymApi.Application.Features.Measurements;
using LgymApi.Application.Features.Plan;
using LgymApi.Application.Features.PlanDay;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Role;
using LgymApi.Application.Features.Supplementation;
using LgymApi.Application.Features.Training;
using LgymApi.Application.Features.TrainerRelationships;
using LgymApi.Application.Features.User;
using LgymApi.Application.Notifications;
using LgymApi.Application.Units;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAppConfigService, AppConfigService>();
        services.AddScoped<IEloRegistryService, EloRegistryService>();
        services.AddScoped<IEnumService, EnumService>();
        services.AddScoped<IExerciseService, ExerciseService>();
        services.AddScoped<IExerciseScoresService, ExerciseScoresService>();
        services.AddScoped<IGymService, GymService>();
        services.AddScoped<IMainRecordsService, MainRecordsService>();
        services.AddScoped<IMeasurementsService, MeasurementsService>();
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<IPlanDayService, PlanDayService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ISupplementationService, SupplementationService>();
        services.AddScoped<ITrainingService, TrainingService>();
        services.AddScoped<ITrainerRelationshipService, TrainerRelationshipService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IInvitationEmailScheduler, InvitationEmailSchedulerService>();
        services.AddScoped<IInvitationEmailJobHandler, InvitationEmailJobHandlerService>();

        services.AddSingleton<ILinearUnitStrategy<WeightUnits>, WeightLinearUnitStrategy>();
        services.AddSingleton<IUnitConverter<WeightUnits>, LinearUnitConverter<WeightUnits>>();
        services.AddSingleton<ILinearUnitStrategy<HeightUnits>, HeightLinearUnitStrategy>();
        services.AddSingleton<IUnitConverter<HeightUnits>, LinearUnitConverter<HeightUnits>>();

        return services;
    }
}
