using FluentAssertions;
using LgymApi.Api.Idempotency;
using LgymApi.Api.Middleware;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ApiIdempotencyReplayAccessEvaluatorTests
{
    [Test]
    public async Task CanReplayAsync_DeniesNoReplayMetadataWithoutCallingAnyPort()
    {
        var port = new CountingReplayAccessPort(ApiIdempotencyReplayOwner.Identity);
        var evaluator = new ApiIdempotencyReplayAccessEvaluator([port]);

        var allowed = await evaluator.CanReplayAsync(
            CreateContext(),
            new ApiIdempotencyAttribute(
                "/api/test",
                ApiIdempotencyScopeSource.AuthenticatedUser,
                ApiIdempotencyReplayOwner.NoReplay),
            CancellationToken.None);

        allowed.Should().BeFalse();
        port.Calls.Should().Be(0);
    }

    [Test]
    public async Task CanReplayAsync_DeniesUnregisteredOwnerWithoutCallingAnotherPort()
    {
        var port = new CountingReplayAccessPort(ApiIdempotencyReplayOwner.Identity);
        var evaluator = new ApiIdempotencyReplayAccessEvaluator([port]);

        var allowed = await evaluator.CanReplayAsync(
            CreateContext(),
            new ApiIdempotencyAttribute(
                "/api/test",
                ApiIdempotencyScopeSource.AuthenticatedUser,
                ApiIdempotencyReplayOwner.Reporting),
            CancellationToken.None);

        allowed.Should().BeFalse();
        port.Calls.Should().Be(0);
    }

    private static HttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IAuthenticatedAccountContextFeature>(
            new TestAuthenticatedAccountContextFeature(
                new AuthenticatedAccountContext(
                    Id<AccountReference>.New(),
                    Id<AccountSessionReference>.New(),
                    [],
                    [],
                    IsBlocked: false,
                    IsDeleted: false)));
        return context;
    }

    private sealed record TestAuthenticatedAccountContextFeature(AuthenticatedAccountContext Context)
        : IAuthenticatedAccountContextFeature;

    private sealed class CountingReplayAccessPort(ApiIdempotencyReplayOwner owner) : IApiIdempotencyReplayAccessPort
    {
        public int Calls { get; private set; }

        public ApiIdempotencyReplayOwner Owner => owner;

        public Task<bool> CanReplayAsync(
            ApiIdempotencyReplayAccessContext context,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(true);
        }
    }
}
