using LgymApi.Application.Common.Training.Elo;
using LgymApi.Application.Features.Exercise;
using LgymApi.Application.Features.Gym;
using LgymApi.Application.Features.Measurements;
using LgymApi.Application.Features.Training;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application.WorkoutProgress;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkoutAndProgressModule(this IServiceCollection services)
    {
        services.AddScoped<IExerciseService, ExerciseService>();
        services.AddScoped<IGymService, GymService>();
        services.AddScoped<IMeasurementsServiceDependencies, MeasurementsServiceDependencies>();
        services.AddScoped<IMeasurementsService, MeasurementsService>();
        services.AddScoped<ITrainingServiceDependencies, TrainingServiceDependencies>();
        services.AddScoped<ITrainingService, TrainingService>();
        services.AddScoped<IExerciseEloCalculator, StandardExerciseEloCalculator>();
        services.AddScoped<IExerciseEloCalculator, StrengthWeightedExerciseEloCalculator>();
        services.AddScoped<IExerciseEloCalculator, VolumeWeightedExerciseEloCalculator>();
        services.AddScoped<IExerciseEloCalculator, PullupWeightedExerciseEloCalculator>();

        return services;
    }
}
