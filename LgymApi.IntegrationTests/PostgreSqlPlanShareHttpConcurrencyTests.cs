using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
internal sealed class PostgreSqlPlanShareHttpConcurrencyTests : PostgreSqlIntegrationTestBase
{
    [Test]
    public async Task OwnerUpdate_AfterShareReuse_ReturnsRetryableConflictWithoutPartialChanges()
    {
        var owner = await SeedUserAsync();
        var expiresAt = new DateTimeOffset(2090, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Original", ShareCode = "EXISTING01", ShareCodeDurationDays = 7, ShareCodeExpiresAt = expiresAt };
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
        }

        var login = await Client.PostAsJsonAsync("/api/login", new { name = owner.Name, password = "password123" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var gate = new UpdateGate();
        using var factory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(provider => new GatedUnitOfWork(new EfUnitOfWork(provider.GetRequiredService<AppDbContext>()), gate));
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var update = client.PostAsJsonAsync($"/api/{owner.Id}/updatePlan", new { _id = plan.Id.ToString(), name = "Updated" });
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var share = await Client.PostAsync($"/api/{plan.Id}/share", null);
            share.StatusCode.Should().Be(HttpStatusCode.OK);
            (await share.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("shareCode").GetString().Should().Be("EXISTING01");
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        var response = await update.WaitAsync(TimeSpan.FromSeconds(20));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("msg").GetString().Should().NotBeNullOrWhiteSpace();
        await using var verification = Factory.Services.CreateAsyncScope();
        var persisted = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans.AsNoTracking().SingleAsync(p => p.Id == plan.Id);
        persisted.Name.Should().Be("Original");
        persisted.ShareCode.Should().Be("EXISTING01");
        persisted.ShareCodeDurationDays.Should().Be(7);
        persisted.ShareCodeExpiresAt.Should().Be(expiresAt);
        persisted.ShareCodeRevision.Should().Be(1);
    }

    private sealed class UpdateGate
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class GatedUnitOfWork(IUnitOfWork inner, UpdateGate gate) : IUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            gate.Reached.TrySetResult();
            await gate.Release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return await inner.SaveChangesAsync(cancellationToken);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);
    }

    [Test]
    public async Task ConcurrentValidCodeReuse_ReturnsSuccessAndRetryableConflictWithoutChangingCode()
    {
        var owner = await SeedUserAsync();
        var expiresAt = new DateTimeOffset(2090, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Share", ShareCode = "EXISTING01", ShareCodeDurationDays = 7, ShareCodeExpiresAt = expiresAt };
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
        }
        var login = await Client.PostAsJsonAsync("/api/login", new { name = owner.Name, password = "password123" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        var barrier = new SaveBarrier();
        using var factory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(provider => new BarrierUnitOfWork(new EfUnitOfWork(provider.GetRequiredService<AppDbContext>()), barrier));
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var responses = await Task.WhenAll(
            client.PostAsync($"/api/{plan.Id}/share", null),
            client.PostAsync($"/api/{plan.Id}/share", null)).WaitAsync(TimeSpan.FromSeconds(30));

        responses.Select(response => response.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        var success = responses.Single(response => response.StatusCode == HttpStatusCode.OK);
        var body = await success.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateObject().Select(property => property.Name).Should().Equal("shareCode");
        body.GetProperty("shareCode").GetString().Should().Be("EXISTING01");
        var conflict = await responses.Single(response => response.StatusCode == HttpStatusCode.Conflict).Content.ReadFromJsonAsync<JsonElement>();
        conflict.GetProperty("msg").GetString().Should().NotBeNullOrWhiteSpace();
        await using var verification = Factory.Services.CreateAsyncScope();
        var persisted = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans.AsNoTracking().SingleAsync(p => p.Id == plan.Id);
        persisted.ShareCode.Should().Be("EXISTING01");
        persisted.ShareCodeDurationDays.Should().Be(7);
        persisted.ShareCodeExpiresAt.Should().Be(expiresAt);
        persisted.ShareCodeRevision.Should().Be(1);

        var retry = await client.PostAsync($"/api/{plan.Id}/share", null);
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        var retryBody = await retry.Content.ReadFromJsonAsync<JsonElement>();
        retryBody.EnumerateObject().Select(property => property.Name).Should().Equal("shareCode");
        retryBody.GetProperty("shareCode").GetString().Should().Be("EXISTING01");
    }

    private sealed class SaveBarrier
    {
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        }
    }

    private sealed class BarrierUnitOfWork(IUnitOfWork inner, SaveBarrier barrier) : IUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await barrier.WaitAsync(cancellationToken);
            return await inner.SaveChangesAsync(cancellationToken);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);
    }
}
