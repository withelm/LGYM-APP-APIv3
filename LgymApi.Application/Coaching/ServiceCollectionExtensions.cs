using LgymApi.Application.Features.TraineeNotes;
using LgymApi.Application.Features.TrainerRelationships;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application.Coaching;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoachingModule(this IServiceCollection services)
    {
        services.AddScoped<ITraineeNoteService, TraineeNoteService>();
        services.AddScoped<ITrainerRelationshipServiceDependencies, TrainerRelationshipServiceDependencies>();
        services.AddScoped<ITrainerRelationshipService, TrainerRelationshipService>();

        return services;
    }
}
