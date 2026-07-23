using LgymApi.Application.Coaching;
using LgymApi.Application.Identity;
using LgymApi.Application.Notifications;
using LgymApi.Application.Nutrition;
using LgymApi.Application.Platform;
using LgymApi.Application.Reporting;
using LgymApi.Application.TrainingPlanning;
using LgymApi.Application.WorkoutProgress;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddIdentityModule();
        services.AddTrainingPlanningModule();
        services.AddWorkoutAndProgressModule();
        services.AddCoachingModule();
        services.AddNutritionModule();
        services.AddReportingModule();
        services.AddNotificationsModule();
        services.AddPlatformModule();

        return services;
    }
}
