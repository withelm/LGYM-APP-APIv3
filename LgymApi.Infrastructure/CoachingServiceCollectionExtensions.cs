using LgymApi.Application.Repositories;
using LgymApi.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoachingInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ITrainerRelationshipRepository, TrainerRelationshipRepository>();
        services.AddScoped<ITraineeNoteRepository, TraineeNoteRepository>();

        return services;
    }
}
