namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class HangfireDashboardAuthorizationGuardTests
{
    [Test]
    public void DashboardRegistration_FollowsValidatedIdentityPipelineAndPrecedesProtocolMiddleware()
    {
        var repositoryRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(repositoryRoot, "LgymApi.Api", "Program.cs"));

        var authentication = program.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
        var userContext = program.IndexOf("app.UseMiddleware<LgymApi.Api.Middleware.UserContextMiddleware>();", StringComparison.Ordinal);
        var authorization = program.IndexOf("app.UseAuthorization();", StringComparison.Ordinal);
        var dashboard = program.IndexOf("ProgramHangfire.ConfigureDashboard(app, TestingEnvironment);", StringComparison.Ordinal);
        var reportingProtocol = program.IndexOf("app.UseMiddleware<ReportingProtocolMiddleware>();", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(authentication, Is.GreaterThanOrEqualTo(0));
            Assert.That(userContext, Is.GreaterThan(authentication));
            Assert.That(authorization, Is.GreaterThan(userContext));
            Assert.That(dashboard, Is.GreaterThan(authorization));
            Assert.That(reportingProtocol, Is.GreaterThan(dashboard));
        });
    }

    [Test]
    public void DashboardAuthorization_UsesFreshIdentityPermissionWithoutTokenClaimFallback()
    {
        var repositoryRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "LgymApi.Api",
            "Configuration",
            "HangfireDashboardAuthorizationFilter.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("IAuthenticatedAccountAuthorizationService"));
            Assert.That(source, Does.Contain("HasPermission(accountContext, AuthConstants.Permissions.AdminAccess)"));
            Assert.That(source, Does.Not.Contain("IsInRole("));
            Assert.That(source, Does.Not.Contain("HasClaim("));
            Assert.That(source, Does.Not.Contain("RequestServices"));
        });
    }

    [Test]
    public void RecurringScheduling_RemainsSeparateFromDashboardMiddleware()
    {
        var repositoryRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(repositoryRoot, "LgymApi.Api", "Program.Hangfire.cs"));
        var recurringMethod = source[source.IndexOf("public static void ConfigureRecurringJobs", StringComparison.Ordinal)..source.IndexOf("public static void ConfigureDashboard", StringComparison.Ordinal)];
        var dashboardMethod = source[source.IndexOf("public static void ConfigureDashboard", StringComparison.Ordinal)..];

        Assert.Multiple(() =>
        {
            Assert.That(recurringMethod, Does.Contain("BackgroundWorkerRecurringJobs.Configure()"));
            Assert.That(recurringMethod, Does.Not.Contain("UseHangfireDashboard"));
            Assert.That(dashboardMethod, Does.Contain("UseHangfireDashboard"));
            Assert.That(dashboardMethod, Does.Not.Contain("BackgroundWorkerRecurringJobs.Configure()"));
        });
    }
}
