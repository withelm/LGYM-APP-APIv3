using System.Security.Claims;
using FluentAssertions;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using LgymApi.Api.Configuration;
using LgymApi.Api.Middleware;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class HangfireDashboardAuthorizationFilterTests
{
    [Test]
    public void Authorize_WithoutFreshPermissionFeature_DeniesStaleAdminClaims()
    {
        var filter = new HangfireDashboardAuthorizationFilter();
        var httpContext = CreateHttpContext(CreateAccountContext(AuthConstants.Permissions.AdminAccess));
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, AuthConstants.Roles.Admin),
            new Claim(AuthConstants.PermissionClaimType, AuthConstants.Permissions.AdminAccess)
        ], "stale-token"));
        var context = CreateDashboardContext(httpContext);

        var result = filter.Authorize(context);

        result.Should().BeFalse();
    }

    [Test]
    public void Authorize_WithValidatedContextAndFreshPermissionFeature_AllowsDashboard()
    {
        var filter = new HangfireDashboardAuthorizationFilter();
        var httpContext = CreateHttpContext(CreateAccountContext(AuthConstants.Permissions.AdminAccess));
        httpContext.Features.Set(new HangfireDashboardAuthorizationFeature());
        var context = CreateDashboardContext(httpContext);

        var result = filter.Authorize(context);

        result.Should().BeTrue();
    }

    [Test]
    public async Task Middleware_CurrentIdentityPermission_StampsFreshFeatureAndContinues()
    {
        var accountContext = CreateAccountContext(AuthConstants.Permissions.AdminAccess);
        var httpContext = CreateHttpContext(accountContext);
        var authorizationService = Substitute.For<IAuthenticatedAccountAuthorizationService>();
        authorizationService.HasPermission(accountContext, AuthConstants.Permissions.AdminAccess).Returns(true);
        var continued = false;
        var middleware = new HangfireDashboardAuthorizationMiddleware(_ =>
        {
            continued = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, authorizationService);

        continued.Should().BeTrue();
        httpContext.Features.Get<HangfireDashboardAuthorizationFeature>().Should().NotBeNull();
        authorizationService.Received(1).HasPermission(accountContext, AuthConstants.Permissions.AdminAccess);
        var filter = new HangfireDashboardAuthorizationFilter();
        filter.Authorize(CreateDashboardContext(httpContext)).Should().BeTrue();
    }

    [Test]
    public async Task Middleware_StaleTokenClaimsWithoutCurrentPermission_ReturnsForbidden()
    {
        var accountContext = CreateAccountContext();
        var httpContext = CreateHttpContext(accountContext);
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, AuthConstants.Roles.Admin),
            new Claim(AuthConstants.PermissionClaimType, AuthConstants.Permissions.AdminAccess)
        ], "stale-token"));
        var authorizationService = Substitute.For<IAuthenticatedAccountAuthorizationService>();
        authorizationService.HasPermission(accountContext, AuthConstants.Permissions.AdminAccess).Returns(false);
        var continued = false;
        var middleware = new HangfireDashboardAuthorizationMiddleware(_ =>
        {
            continued = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, authorizationService);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        continued.Should().BeFalse();
        httpContext.Features.Get<HangfireDashboardAuthorizationFeature>().Should().BeNull();
    }

    private static HttpContext CreateHttpContext(AuthenticatedAccountContext accountContext)
    {
        var services = new ServiceCollection()
            .BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Features.Set<IAuthenticatedAccountContextFeature>(new AuthenticatedAccountContextFeature(accountContext));
        return context;
    }

    private static AuthenticatedAccountContext CreateAccountContext(params string[] permissions) =>
        new(
            Id<AccountReference>.New(),
            Id<AccountSessionReference>.New(),
            [],
            permissions,
            IsBlocked: false,
            IsDeleted: false);

    private static DashboardContext CreateDashboardContext(HttpContext httpContext)
    {
        return new AspNetCoreDashboardContext(FakeJobStorage.Instance, new DashboardOptions(), httpContext);
    }

    private sealed class FakeJobStorage : JobStorage
    {
        public static FakeJobStorage Instance { get; } = new();

        private FakeJobStorage()
        {
        }

        public override IMonitoringApi GetMonitoringApi() => new FakeMonitoringApi();

        public override IStorageConnection GetConnection() => new FakeStorageConnection();
    }

    private sealed class FakeMonitoringApi : IMonitoringApi
    {
        public IList<QueueWithTopEnqueuedJobsDto> Queues() => new List<QueueWithTopEnqueuedJobsDto>();

        public IList<ServerDto> Servers() => new List<ServerDto>();

        public JobDetailsDto JobDetails(string jobId) => new();

        public StatisticsDto GetStatistics() => new();

        public JobList<EnqueuedJobDto> EnqueuedJobs(string queue, int from, int perPage) =>
            new(new List<KeyValuePair<string, EnqueuedJobDto>>());

        public JobList<FetchedJobDto> FetchedJobs(string queue, int from, int perPage) =>
            new(new List<KeyValuePair<string, FetchedJobDto>>());

        public JobList<ProcessingJobDto> ProcessingJobs(int from, int count) =>
            new(new List<KeyValuePair<string, ProcessingJobDto>>());

        public JobList<ScheduledJobDto> ScheduledJobs(int from, int count) =>
            new(new List<KeyValuePair<string, ScheduledJobDto>>());

        public JobList<SucceededJobDto> SucceededJobs(int from, int count) =>
            new(new List<KeyValuePair<string, SucceededJobDto>>());

        public JobList<FailedJobDto> FailedJobs(int from, int count) =>
            new(new List<KeyValuePair<string, FailedJobDto>>());

        public JobList<DeletedJobDto> DeletedJobs(int from, int count) =>
            new(new List<KeyValuePair<string, DeletedJobDto>>());

        public long ScheduledCount() => 0;

        public long EnqueuedCount(string queue) => 0;

        public long FetchedCount(string queue) => 0;

        public long FailedCount() => 0;

        public long ProcessingCount() => 0;

        public long SucceededListCount() => 0;

        public long DeletedListCount() => 0;

        public IDictionary<DateTime, long> SucceededByDatesCount() => new Dictionary<DateTime, long>();

        public IDictionary<DateTime, long> FailedByDatesCount() => new Dictionary<DateTime, long>();

        public IDictionary<DateTime, long> HourlySucceededJobs() => new Dictionary<DateTime, long>();

        public IDictionary<DateTime, long> HourlyFailedJobs() => new Dictionary<DateTime, long>();
    }

    private sealed class FakeStorageConnection : IStorageConnection
    {
        public void Dispose()
        {
        }

        public IWriteOnlyTransaction CreateWriteTransaction() => new FakeWriteOnlyTransaction();

        public IDisposable AcquireDistributedLock(string resource, TimeSpan timeout) => new FakeDistributedLock();

        public string CreateExpiredJob(Job job, IDictionary<string, string> parameters, DateTime createdAt, TimeSpan expireIn) => string.Empty;

        public IFetchedJob FetchNextJob(string[] queues, CancellationToken cancellationToken) => new FakeFetchedJob();

        public void SetJobParameter(string id, string name, string value)
        {
        }

        public string GetJobParameter(string id, string name) => string.Empty;

        public JobData? GetJobData(string jobId) => null;

        public StateData? GetStateData(string jobId) => null;

        public void AnnounceServer(string serverId, ServerContext context)
        {
        }

        public void RemoveServer(string serverId)
        {
        }

        public void Heartbeat(string serverId)
        {
        }

        public int RemoveTimedOutServers(TimeSpan timeOut) => 0;

        public HashSet<string> GetAllItemsFromSet(string key) => new();

        public string GetFirstByLowestScoreFromSet(string key, double fromScore, double toScore) => string.Empty;

        public Dictionary<string, string>? GetAllEntriesFromHash(string key) => new();

        public void SetRangeInHash(string key, IEnumerable<KeyValuePair<string, string>> keyValuePairs)
        {
        }
    }

    private sealed class FakeWriteOnlyTransaction : IWriteOnlyTransaction
    {
        public void Dispose()
        {
        }

        public void ExpireJob(string jobId, TimeSpan expireIn)
        {
        }

        public void PersistJob(string jobId)
        {
        }

        public void SetJobState(string jobId, IState state)
        {
        }

        public void AddJobState(string jobId, IState state)
        {
        }

        public void AddToQueue(string queue, string jobId)
        {
        }

        public void IncrementCounter(string key)
        {
        }

        public void IncrementCounter(string key, TimeSpan expireIn)
        {
        }

        public void DecrementCounter(string key)
        {
        }

        public void DecrementCounter(string key, TimeSpan expireIn)
        {
        }

        public void AddToSet(string key, string value)
        {
        }

        public void AddToSet(string key, string value, double score)
        {
        }

        public void RemoveFromSet(string key, string value)
        {
        }

        public void InsertToList(string key, string value)
        {
        }

        public void RemoveFromList(string key, string value)
        {
        }

        public void TrimList(string key, int keepStartingFrom, int keepEndingAt)
        {
        }

        public void SetRangeInHash(string key, IEnumerable<KeyValuePair<string, string>> keyValuePairs)
        {
        }

        public void RemoveHash(string key)
        {
        }

        public void Commit()
        {
        }
    }

    private sealed class FakeFetchedJob : IFetchedJob
    {
        public string JobId => string.Empty;

        public void Dispose()
        {
        }

        public void RemoveFromQueue()
        {
        }

        public void Requeue()
        {
        }
    }

    private sealed class FakeDistributedLock : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
