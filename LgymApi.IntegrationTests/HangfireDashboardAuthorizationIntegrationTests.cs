using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using LgymApi.Api.Configuration;
using LgymApi.Domain.Security;
using LgymApi.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class HangfireDashboardAuthorizationIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task Dashboard_CurrentAdminAccessPermission_AllowsTheValidatedSession()
    {
        var administrator = await SeedUserAsync(
            "hangfire-current-admin",
            "hangfire-current-admin@example.test",
            isAdmin: true);
        SetAuthorizationHeader(administrator.Id);

        using var mountedFactory = CreateDashboardFactory();
        using var client = CreateAuthorizedClient(mountedFactory);
        using var response = await client.GetAsync("/hangfire");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Dashboard_PreIssuedAdminTokenAfterPersistedDemotion_IsForbidden()
    {
        var administrator = await SeedUserAsync(
            "hangfire-demoted-admin",
            "hangfire-demoted-admin@example.test",
            isAdmin: true);
        SetAuthorizationHeader(administrator.Id);
        using var mountedFactory = CreateDashboardFactory();
        using var client = CreateAuthorizedClient(mountedFactory);

        using (var allowed = await client.GetAsync("/hangfire"))
        {
            allowed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var adminRoles = await database.UserRoles
                .Where(userRole => userRole.UserId == administrator.Id && userRole.Role.Name == AuthConstants.Roles.Admin)
                .ToArrayAsync();
            database.UserRoles.RemoveRange(adminRoles);
            await database.SaveChangesAsync();
        }

        using var denied = await client.GetAsync("/hangfire");
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Dashboard_PreIssuedAdminTokenAfterSessionRevocation_IsUnauthorized()
    {
        var administrator = await SeedUserAsync(
            "hangfire-revoked-admin",
            "hangfire-revoked-admin@example.test",
            isAdmin: true);
        SetAuthorizationHeader(administrator.Id);
        using var mountedFactory = CreateDashboardFactory();
        using var client = CreateAuthorizedClient(mountedFactory);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await database.UserSessions.SingleAsync(candidate => candidate.UserId == administrator.Id);
            session.RevokedAtUtc = DateTimeOffset.UtcNow;
            await database.SaveChangesAsync();
        }

        using var denied = await client.GetAsync("/hangfire");
        denied.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private WebApplicationFactory<Program> CreateDashboardFactory() =>
        Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter>(new HangfireDashboardProbeStartupFilter())));

    private HttpClient CreateAuthorizedClient(WebApplicationFactory<Program> factory)
    {
        var token = Client.DefaultRequestHeaders.Authorization?.Parameter;
        token.Should().NotBeNullOrWhiteSpace();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed class HangfireDashboardProbeStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Map("/hangfire", dashboard =>
            {
                dashboard.UseMiddleware<HangfireDashboardAuthorizationMiddleware>();
                dashboard.Run(async context =>
                {
                    var dashboardContext = new AspNetCoreDashboardContext(
                        ProbeJobStorage.Instance,
                        new DashboardOptions(),
                        context);
                    context.Response.StatusCode = new HangfireDashboardAuthorizationFilter().Authorize(dashboardContext)
                        ? StatusCodes.Status204NoContent
                        : StatusCodes.Status403Forbidden;
                    await context.Response.CompleteAsync();
                });
            });
        };
    }

    private sealed class ProbeJobStorage : JobStorage
    {
        public static readonly ProbeJobStorage Instance = new();

        public override IMonitoringApi GetMonitoringApi() => throw new NotSupportedException();

        public override IStorageConnection GetConnection() => throw new NotSupportedException();
    }
}
