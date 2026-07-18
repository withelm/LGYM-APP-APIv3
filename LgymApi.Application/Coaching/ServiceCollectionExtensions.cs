using LgymApi.Application.Features.ExerciseScores;
using LgymApi.Application.Features.MainRecords;
using LgymApi.Application.Features.TraineeNotes;
using LgymApi.Application.Features.TrainerRelationships;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application.Coaching;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoachingModule(this IServiceCollection services)
    {
        services.AddScoped<IExerciseScoresService, ExerciseScoresService>();
        services.AddScoped<IMainRecordsServiceDependencies, MainRecordsServiceDependencies>();
        services.AddScoped<IMainRecordsService, MainRecordsService>();
        services.AddScoped<ITraineeNoteService, TraineeNoteService>();
        services.AddScoped<ITrainerRelationshipServiceDependencies, TrainerRelationshipServiceDependencies>();
        services.AddScoped<ITrainerRelationshipService, TrainerRelationshipService>();

        return services;
    }
}
