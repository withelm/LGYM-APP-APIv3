using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Reporting.ApiAdapters;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application.Reporting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services)
    {
        services.AddSingleton<IReportSubmissionAcceptedProgressCommandFactory, ReportSubmissionAcceptedProgressCommandFactory>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IRecurringReportAssignmentService, RecurringReportAssignmentService>();
        services.AddScoped<IExpiredPhotoUploadCleanupService, ExpiredPhotoUploadCleanupService>();

        return services;
    }

    public static IServiceCollection AddReportingApiAdapters(this IServiceCollection services)
    {
        services.AddScoped<ITrainerReportTemplateApiPort, TrainerReportTemplateApiAdapter>();
        services.AddScoped<ITrainerPhotoTypeApiPort, TrainerPhotoTypeApiAdapter>();
        services.AddScoped<ITrainerReportRequestApiPort, TrainerReportRequestApiAdapter>();
        services.AddScoped<ITraineeReportRequestApiPort, TraineeReportRequestApiAdapter>();
        services.AddScoped<ITrainerReportPhotoApiPort, TrainerReportPhotoApiAdapter>();
        services.AddScoped<ITraineeReportPhotoApiPort, TraineeReportPhotoApiAdapter>();
        services.AddScoped<IRecurringReportAssignmentApiPort, RecurringReportAssignmentApiAdapter>();

        return services;
    }
}
