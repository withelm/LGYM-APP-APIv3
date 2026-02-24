using System.Security.Claims;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using LgymApi.Api.Configuration;
using LgymApi.Domain.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class HangfireDashboardAuthorizationFilterTests
{
    [Test]
    public void Authorize_Should_Return_False_When_User_Is_Not_Authenticated()
    {
        var filter = new HangfireDashboardAuthorizationFilter();
        var context = CreateDashboardContext(CreateHttpContext(new ClaimsIdentity()));

        var result = filter.Authorize(context);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Authorize_Should_Return_True_For_Admin_Role()
    {
        var filter = new HangfireDashboardAuthorizationFilter();
        var identity = CreateIdentity(new Claim(ClaimTypes.Role, AuthConstants.Roles.Admin));
        var context = CreateDashboardContext(CreateHttpContext(identity));

        var result = filter.Authorize(context);

        Assert.That(result, Is.True);
    }

    [Test]
    public void Authorize_Should_Return_True_For_Admin_Access_Permission()
    {
        var filter = new HangfireDashboardAuthorizationFilter();
        var identity = CreateIdentity(new Claim(AuthConstants.PermissionClaimType, AuthConstants.Permissions.AdminAccess));
        var context = CreateDashboardContext(CreateHttpContext(identity));

        var result = filter.Authorize(context);

        Assert.That(result, Is.True);
    }

    [Test]
    public void Authorize_Should_Return_False_When_No_Admin_Access()
    {
        var filter = new HangfireDashboardAuthorizationFilter();
        var identity = CreateIdentity(new Claim(ClaimTypes.Name, "user"));
        var context = CreateDashboardContext(CreateHttpContext(identity));

        var result = filter.Authorize(context);

        Assert.That(result, Is.False);
    }

    private static ClaimsIdentity CreateIdentity(params Claim[] claims)
    {
        return new ClaimsIdentity(claims, "Test");
    }

    private static HttpContext CreateHttpContext(ClaimsIdentity identity)
    {
        var services = new ServiceCollection()
            .BuildServiceProvider();

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            RequestServices = services
        };
    }

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
