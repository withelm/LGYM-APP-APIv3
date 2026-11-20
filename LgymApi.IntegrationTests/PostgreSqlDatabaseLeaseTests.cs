using LgymApi.Domain.Security;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
public sealed class PostgreSqlDatabaseLeaseTests
{
    [Test]
    public async Task CreateAsync_WithTwoSimultaneousFactories_UsesDistinctDatabasesAndDropsBothAfterDisposal()
    {
        var databaseNames = await CreateAndDisposeTwoFactoriesAsync();

        Assert.That(databaseNames.First, Is.Not.EqualTo(databaseNames.Second));
        Assert.That((await PostgreSqlDatabaseLease.DatabaseExistsAsync(databaseNames.First)), Is.False);
        Assert.That((await PostgreSqlDatabaseLease.DatabaseExistsAsync(databaseNames.Second)), Is.False);
    }

    [Test]
    public async Task DisposeAsync_WhenTestBodyThrows_DropsTheLeasedDatabase()
    {
        var databaseName = string.Empty;

        try
        {
            await using (var factory = await PostgreSqlWebApplicationFactory.CreateAsync())
            {
                databaseName = factory.DatabaseName;
                throw new InvalidOperationException("Simulated test-body failure");
            }
        }
        catch (InvalidOperationException exception) when (exception.Message == "Simulated test-body failure")
        {
        }

        Assert.That((await PostgreSqlDatabaseLease.DatabaseExistsAsync(databaseName)), Is.False);
    }

    [Test]
    public async Task CreateAsync_UsesNpgsqlMigrationsAndSeedsDefaultRoles()
    {
        await using var factory = await PostgreSqlWebApplicationFactory.CreateAsync();
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.That(database.Database.ProviderName, Is.EqualTo("Npgsql.EntityFrameworkCore.PostgreSQL"));
        Assert.That((await database.Database.GetPendingMigrationsAsync()), Is.Empty);
        var assertedValue55 = (scope.ServiceProvider.GetServices<IEmailSender>()).ToArray();
        Assert.That(assertedValue55, Has.Exactly(1).Items);
        Assert.That(assertedValue55.Single(), Is.SameAs(factory.EmailSender));
        var assertedValue56 = (scope.ServiceProvider.GetServices<IPushProviderSender>()).ToArray();
        Assert.That(assertedValue56, Has.Exactly(1).Items);
        Assert.That(assertedValue56.Single(), Is.SameAs(factory.PushSender));

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await using (var transaction = await unitOfWork.BeginTransactionAsync())
        {
            await transaction.RollbackAsync();
        }

        var roleNames = await database.Roles.Select(role => role.Name).ToListAsync();
        Assert.That(roleNames, Does.Contain(AuthConstants.Roles.User), string.Format(System.Globalization.CultureInfo.InvariantCulture, AuthConstants.Roles.Admin, AuthConstants.Roles.Tester, AuthConstants.Roles.Trainer));
    }

    [Test]
    public async Task CreateAsync_WhenConnectionIsMissing_ThrowsActionableSkipWithoutSecret()
    {
        using var environment = new EnvironmentVariableScope(value: null);

        var action = async () => await PostgreSqlDatabaseLease.CreateAsync();

        var exception = (await Assert.CatchAsync<IgnoreException>(async () => { await action(); }))!;

        Assert.That(exception.Message, Does.Contain("LGYM_TEST_POSTGRES"));
        Assert.That(exception.Message, Does.Contain("admin PostgreSQL connection"));
        Assert.That(exception.Message, Does.Not.Contain("Password"));
    }

    [Test]
    public async Task CreateAsync_WhenConnectionIsMalformed_ThrowsRedactedActionableError()
    {
        const string password = "must-not-appear";
        using var environment = new EnvironmentVariableScope($"Host=127.0.0.1;Port=not-a-port;Password={password}");

        var action = async () => await PostgreSqlDatabaseLease.CreateAsync();

        var exception = (await Assert.CatchAsync<InvalidOperationException>(async () => { await action(); }))!;

        Assert.That(exception.Message, Does.Contain("LGYM_TEST_POSTGRES"));
        Assert.That(exception.Message, Does.Contain("could not be parsed"));
        Assert.That(exception.Message, Does.Not.Contain(password));
    }

    [Test]
    public async Task CreateAsync_WhenNpgsqlFails_RetainsRedactedMessageAndExactInnerCause()
    {
        const string password = "must-not-appear";
        var npgsqlException = new NpgsqlException("Forced create failure.");
        var operations = new ControllableDatabaseLeaseOperations
        {
            CreateException = npgsqlException
        };

        var action = async () => await PostgreSqlDatabaseLease.CreateWithRedactedNpgsqlAsync(
            $"Host=test;Password={password}",
            operations);

        var exception = (await Assert.ThrowsAsync<InvalidOperationException>(async () => { await action(); }))!;

        Assert.That(exception.Message, Is.EqualTo("Could not create an isolated PostgreSQL test database from LGYM_TEST_POSTGRES. Verify the admin connection and CREATE DATABASE permission. The connection value is redacted."));
        Assert.That(exception.InnerException, Is.SameAs(npgsqlException));
        Assert.That(exception.ToString(), Does.Not.Contain(password));
        Assert.That(operations.DropAttempts, Is.EqualTo(1));
        Assert.That(operations.DatabaseNames, Is.Empty);
    }

    [Test]
    public async Task CreateAsync_WhenLeaseConstructionFailsAfterDatabaseCreation_DropsDatabase()
    {
        var operations = new ControllableDatabaseLeaseOperations
        {
            FailConnectionStringBuild = true
        };

        var action = async () => await PostgreSqlDatabaseLease.CreateAsync("Host=test", operations);

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Forced lease construction failure.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        Assert.That(operations.DatabaseNames, Is.Empty);
        Assert.That(operations.DropAttempts, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CreateAsync_WhenCreateThrows_ReconcilesGeneratedNameAndPreservesOriginalError(
        bool databaseCreatedBeforeFailure)
    {
        var createException = new InvalidOperationException("Forced lease creation failure.");
        var operations = new ControllableDatabaseLeaseOperations
        {
            CreateException = createException,
            CreateDatabaseBeforeFailure = databaseCreatedBeforeFailure
        };

        var action = async () => await PostgreSqlDatabaseLease.CreateAsync("Host=test", operations);

        var exception = (await Assert.ThrowsAsync<InvalidOperationException>(async () => { await action(); }))!;

        Assert.That(exception, Is.SameAs(createException));
        Assert.That(operations.DropAttempts, Is.EqualTo(1));
        Assert.That(operations.DatabaseNames, Is.Empty);
    }

    [Test]
    public async Task DisposeAsync_WhenFirstDropFails_RetriesOnNextDispose()
    {
        var operations = new ControllableDatabaseLeaseOperations
        {
            RemainingDropFailures = 1
        };
        var lease = await PostgreSqlDatabaseLease.CreateAsync("Host=test", operations);

        var firstDispose = async () => await lease.DisposeAsync();

        await Assert.ThatAsync(async () => { await firstDispose(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Forced drop failure.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        Assert.That(operations.DatabaseNames, Has.Exactly(1).Items, lease.DatabaseName);

        await lease.DisposeAsync();
        Assert.That(operations.DatabaseNames, Is.Empty);
        Assert.That(operations.DropAttempts, Is.EqualTo(2));

        await lease.DisposeAsync();
        Assert.That(operations.DropAttempts, Is.EqualTo(2));
    }

    [Test]
    public async Task DisposeAsync_WhenNpgsqlFails_RetainsRedactedMessageAndExactInnerCauseThenRetries()
    {
        const string password = "must-not-appear";
        var npgsqlException = new NpgsqlException("Forced drop failure.");
        var operations = new ControllableDatabaseLeaseOperations
        {
            NpgsqlDropException = npgsqlException,
            RemainingNpgsqlDropFailures = 1
        };
        var lease = await PostgreSqlDatabaseLease.CreateAsync(
            $"Host=test;Password={password}",
            operations);

        var firstDispose = async () => await lease.DisposeAsync();

        var exception = (await Assert.ThrowsAsync<InvalidOperationException>(async () => { await firstDispose(); }))!;
        Assert.That(exception.Message, Is.EqualTo($"Could not remove isolated PostgreSQL test database '{lease.DatabaseName}'. The connection value is redacted."));
        Assert.That(exception.InnerException, Is.SameAs(npgsqlException));
        Assert.That(exception.ToString(), Does.Not.Contain(password));
        Assert.That(operations.DatabaseNames, Has.Exactly(1).Items, lease.DatabaseName);

        await lease.DisposeAsync();
        Assert.That(operations.DropAttempts, Is.EqualTo(2));
        Assert.That(operations.DatabaseNames, Is.Empty);
    }

    [Test]
    public async Task CreateAsync_WhenAmbiguousCreateAndCompensationFail_CorrelatesBothErrorsWithoutSecret()
    {
        const string password = "must-not-appear";
        var operations = new ControllableDatabaseLeaseOperations
        {
            CreateException = new InvalidOperationException("Forced lease creation failure."),
            CreateDatabaseBeforeFailure = true,
            RemainingDropFailures = 1
        };

        var action = async () => await PostgreSqlDatabaseLease.CreateAsync(
            $"Host=test;Password={password}",
            operations);

        var exception = (await Assert.CatchAsync<InvalidOperationException>(async () => { await action(); }))!;

        Assert.That(exception.Message, Does.Contain("acquisition and compensating cleanup also failed"));
        var assertedValue57 = exception.InnerException;
        Assert.That(assertedValue57, Is.TypeOf<AggregateException>());
        Assert.That(((AggregateException)assertedValue57).InnerExceptions.Select(error => error.Message), Is.EqualTo(new[] { "Forced lease creation failure.", "Forced drop failure." }));
        Assert.That(exception.ToString(), Does.Not.Contain(password));
    }

    [Test]
    public async Task CreateAllOrDisposeSuccessfulAsync_WhenSiblingCreationFails_DropsSuccessfulSiblingDatabaseOnce()
    {
        var operations = new ControllableDatabaseLeaseOperations();
        var lease = await PostgreSqlDatabaseLease.CreateAsync("Host=test", operations);
        var successfulFactory = new LeaseBackedTestFactory(lease);
        var successfulCreation = Task.FromResult(successfulFactory);
        var creationException = new InvalidOperationException("Forced sibling creation failure.");

        async Task<LeaseBackedTestFactory> FailAfterSuccessfulSiblingAsync()
        {
            await successfulCreation;
            throw creationException;
        }

        var action = async () => await ConcurrentFactoryCreation.CreateAllOrDisposeSuccessfulAsync(
            () => successfulCreation,
            FailAfterSuccessfulSiblingAsync);

        try
        {
            var exception = (await Assert.ThrowsAsync<InvalidOperationException>(async () => { await action(); }))!;

            Assert.That(exception, Is.SameAs(creationException));
            Assert.That(successfulFactory.DisposalAttempts, Is.EqualTo(1));
            Assert.That(operations.DatabaseNames, Is.Empty);
        }
        finally
        {
            if (operations.DatabaseNames.Count != 0)
            {
                await lease.DisposeAsync();
            }
        }
    }

    [Test]
    public async Task CreateAllOrDisposeSuccessfulAsync_WhenSiblingCreationAndCleanupFail_ExposesBothErrors()
    {
        var creationException = new InvalidOperationException("Forced sibling creation failure.");
        var cleanupException = new InvalidOperationException("Forced sibling cleanup failure.");
        var successfulFactory = new FailingDisposalTestFactory(cleanupException);
        var successfulCreation = Task.FromResult(successfulFactory);

        async Task<FailingDisposalTestFactory> FailAfterSuccessfulSiblingAsync()
        {
            await successfulCreation;
            throw creationException;
        }

        var action = async () => await ConcurrentFactoryCreation.CreateAllOrDisposeSuccessfulAsync(
            () => successfulCreation,
            FailAfterSuccessfulSiblingAsync);

        var exception = (await Assert.ThrowsAsync<AggregateException>(async () => { await action(); }))!;

        Assert.That(exception.InnerExceptions, Is.EqualTo(new[] { creationException, cleanupException }));
        Assert.That(successfulFactory.DisposalAttempts, Is.EqualTo(1));
    }

    private static async Task<(string First, string Second)> CreateAndDisposeTwoFactoriesAsync()
    {
        var factories = await ConcurrentFactoryCreation.CreateAllOrDisposeSuccessfulAsync(
            static () => PostgreSqlWebApplicationFactory.CreateAsync(),
            static () => PostgreSqlWebApplicationFactory.CreateAsync());

        await using var first = factories[0];
        await using var second = factories[1];

        Assert.That(first.DatabaseName, Is.Not.EqualTo(second.DatabaseName));
        return (first.DatabaseName, second.DatabaseName);
    }

    private sealed class LeaseBackedTestFactory(PostgreSqlDatabaseLease lease) : IAsyncDisposable
    {
        public int DisposalAttempts { get; private set; }

        public async ValueTask DisposeAsync()
        {
            DisposalAttempts++;
            await lease.DisposeAsync();
        }
    }

    private sealed class FailingDisposalTestFactory(Exception cleanupException) : IAsyncDisposable
    {
        public int DisposalAttempts { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposalAttempts++;
            return ValueTask.FromException(cleanupException);
        }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private const string VariableName = "LGYM_TEST_POSTGRES";
        private readonly string? _originalValue = Environment.GetEnvironmentVariable(VariableName);

        public EnvironmentVariableScope(string? value)
        {
            Environment.SetEnvironmentVariable(VariableName, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(VariableName, _originalValue);
        }
    }

    private sealed class ControllableDatabaseLeaseOperations : IPostgreSqlDatabaseLeaseOperations
    {
        public HashSet<string> DatabaseNames { get; } = [];

        public bool FailConnectionStringBuild { get; init; }

        public Exception? CreateException { get; init; }

        public bool CreateDatabaseBeforeFailure { get; init; }

        public int RemainingDropFailures { get; set; }

        public NpgsqlException? NpgsqlDropException { get; init; }

        public int RemainingNpgsqlDropFailures { get; set; }

        public int DropAttempts { get; private set; }

        public Task CreateDatabaseAsync(
            string adminConnectionString,
            string databaseName,
            CancellationToken cancellationToken)
        {
            if (CreateException != null)
            {
                if (CreateDatabaseBeforeFailure)
                {
                    DatabaseNames.Add(databaseName);
                }

                throw CreateException;
            }

            DatabaseNames.Add(databaseName);
            return Task.CompletedTask;
        }

        public Task DropDatabaseAsync(
            string adminConnectionString,
            string databaseName,
            CancellationToken cancellationToken)
        {
            DropAttempts++;
            if (RemainingNpgsqlDropFailures > 0)
            {
                RemainingNpgsqlDropFailures--;
                throw NpgsqlDropException!;
            }

            if (RemainingDropFailures > 0)
            {
                RemainingDropFailures--;
                throw new InvalidOperationException("Forced drop failure.");
            }

            DatabaseNames.Remove(databaseName);
            return Task.CompletedTask;
        }

        public string BuildDatabaseConnectionString(string adminConnectionString, string databaseName)
        {
            if (FailConnectionStringBuild)
            {
                throw new InvalidOperationException("Forced lease construction failure.");
            }

            return $"Host=test;Database={databaseName}";
        }
    }
}
