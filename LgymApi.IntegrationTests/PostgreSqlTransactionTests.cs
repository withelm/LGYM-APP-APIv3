using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.Plan;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
internal sealed class PostgreSqlTransactionTests : PostgreSqlIntegrationTestBase
{
    [Test]
    public async Task EfUnitOfWorkTransaction_CommitAsync_PersistsFlushedRecordAfterCommit()
    {
        var idempotencyKey = CreateIdempotencyKey("commit");

        await using (var writeScope = Factory.Services.CreateAsyncScope())
        {
            var database = writeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unitOfWork = new EfUnitOfWork(database);

            await using (var transaction = await unitOfWork.BeginTransactionAsync())
            {
                await database.ApiIdempotencyRecords.AddAsync(CreateProbe(idempotencyKey));
                await unitOfWork.SaveChangesAsync();

                (await ExistsInFreshContextAsync(idempotencyKey)).Should().BeFalse();

                await transaction.CommitAsync();
            }
        }

        (await ExistsInFreshContextAsync(idempotencyKey)).Should().BeTrue();
    }

    [Test]
    public async Task CopyPlanAsync_WhenRepositoryFlushesThenThrows_RollsBackProbeAndReturnsPlanNotFound()
    {
        var idempotencyKey = CreateIdempotencyKey("rollback");
        var currentUser = new User { Id = Id<User>.New() };
        var probeWasVisibleBeforeRollback = true;

        await using (var serviceScope = Factory.Services.CreateAsyncScope())
        {
            var database = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var planRepository = Substitute.For<IPlanRepository>();

            planRepository.CopyPlanByShareCodeAsync(
                    Arg.Any<string>(),
                    Arg.Any<Id<User>>(),
                    Arg.Any<CancellationToken>())
                .Returns(async _ =>
                {
                    await database.ApiIdempotencyRecords.AddAsync(CreateProbe(idempotencyKey));
                    await database.SaveChangesAsync();
                    probeWasVisibleBeforeRollback = await ExistsInFreshContextAsync(idempotencyKey);
                    return await Task.FromException<Plan>(
                        new InvalidOperationException("Forced post-save failure."));
                });

            var service = new PlanService(
                Substitute.For<IUserRepository>(),
                planRepository,
                Substitute.For<IPlanDayRepository>(),
                new EfUnitOfWork(database));

            var result = await service.CopyPlanAsync(currentUser, "missing-share-code");

            result.IsFailure.Should().BeTrue();
            result.Error.Should().BeOfType<PlanNotFoundError>();
            _ = planRepository.Received(1).CopyPlanByShareCodeAsync(
                "missing-share-code",
                currentUser.Id,
                CancellationToken.None);
        }

        probeWasVisibleBeforeRollback.Should().BeFalse();
        (await ExistsInFreshContextAsync(idempotencyKey)).Should().BeFalse();
    }

    private async Task<bool> ExistsInFreshContextAsync(string idempotencyKey)
    {
        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var database = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await database.ApiIdempotencyRecords
            .AnyAsync(record => record.IdempotencyKey == idempotencyKey);
    }

    private static ApiIdempotencyRecord CreateProbe(string idempotencyKey)
    {
        return new ApiIdempotencyRecord
        {
            Id = Id<ApiIdempotencyRecord>.New(),
            IdempotencyKey = idempotencyKey,
            ScopeTuple = $"POST|/postgresql-transaction-tests|{idempotencyKey}",
            RequestFingerprint = new string('f', 64),
            ResponseStatusCode = 200,
            ResponseBodyJson = "{}",
            ProcessedAt = DateTimeOffset.UtcNow
        };
    }

    private static string CreateIdempotencyKey(string scenario)
    {
        return $"postgresql-transaction-{scenario}-{Id<ApiIdempotencyRecord>.New()}";
    }
}
