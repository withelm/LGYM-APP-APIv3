using Hangfire;
using LgymApi.Api.Configuration;
using LgymApi.BackgroundWorker;

namespace LgymApi.Api;

internal static class ProgramHangfire
{
    public static void ConfigureRecurringJobs(WebApplication app, string testingEnvironment)
    {
        if (app.Environment.IsEnvironment(testingEnvironment))
        {
            return;
        }

        BackgroundWorkerRecurringJobs.Configure();
    }

    public static void ConfigureDashboard(WebApplication app, string testingEnvironment)
    {
        if (app.Environment.IsEnvironment(testingEnvironment))
        {
            return;
        }

        app.UseWhen(
            context => context.Request.Path.StartsWithSegments("/hangfire"),
            branch => branch.UseMiddleware<HangfireDashboardAuthorizationMiddleware>());
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new HangfireDashboardAuthorizationFilter()]
        });
    }
}
