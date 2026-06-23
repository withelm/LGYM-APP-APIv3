using Hangfire;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.Api.Configuration;

namespace LgymApi.Api;

internal static class ProgramHangfire
{
    public static void ConfigureRecurringJobs(WebApplication app, string testingEnvironment)
    {
        if (app.Environment.IsEnvironment(testingEnvironment))
        {
            return;
        }

        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
        });

        RecurringJob.AddOrUpdate<ICommittedIntentDispatchJob>("reliability-committed-intent-dispatch", job => job.ExecuteAsync(CancellationToken.None), Cron.Minutely);
        RecurringJob.AddOrUpdate<IExpiredPhotoUploadCleanupJob>("reporting-expired-photo-upload-cleanup", job => job.ExecuteAsync(CancellationToken.None), Cron.Minutely);
        RecurringJob.AddOrUpdate<IRecurringReportAssignmentProcessingJob>("reporting-recurring-report-assignments", job => job.ExecuteAsync(CancellationToken.None), Cron.Minutely);
    }
}
