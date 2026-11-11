using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Reporting.ApiAdapters;
using LgymApi.Application.Reporting.Authorization;
using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.Authorization;
using LgymApi.Identity.Contracts.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application.Reporting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services)
    {
        services.AddAuthorizationPolicy<ReportingAuthorizationContext, ReportingAuthorizationPolicy>();
        services.AddScoped<IReportingAuthorizationCoordinator, ReportingAuthorizationCoordinator>();
        services.AddScoped<IAccountAccessOwnerCapabilityExtension, ReportingAccountAccessCapabilitiesExtension>();
        services.AddSingleton<IReportSubmissionAcceptedProgressCommandFactory, ReportSubmissionAcceptedProgressCommandFactory>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IRecurringReportAssignmentService, RecurringReportAssignmentService>();
        services.AddScoped<IRelationshipEndedReportCancellationActionExecutionPort, RelationshipEndedReportCancellationActionExecutionPort>();
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
        services.AddScoped<ITraineeRecurringReportAssignmentApiPort, TraineeRecurringReportAssignmentApiAdapter>();

        return services;
    }
}
