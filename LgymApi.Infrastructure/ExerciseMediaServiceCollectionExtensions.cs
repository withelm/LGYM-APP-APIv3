using LgymApi.Infrastructure.Options;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    internal static IServiceCollection AddExerciseMediaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("ExerciseMediaStorage").Get<ExerciseMediaStorageOptions>() ?? new();
        options.Validate();
        var reportsBucket = configuration["PhotoStorage:BucketName"];
        if (options.Enabled && (options.Bucket == reportsBucket || options.AccessKeyId == configuration["PhotoStorage:AccessKeyId"])) throw new InvalidOperationException("Exercise media and private report storage must use separate buckets and credentials.");
        services.AddSingleton(options);
        services.AddScoped<IExerciseMediaStorage, ExerciseMediaStorage>();
        services.AddScoped<IExerciseMediaVerifier, ExerciseMediaVerifier>();
        return services;
    }
}
