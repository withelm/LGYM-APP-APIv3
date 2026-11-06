using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LgymApi.Api.Idempotency;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class AuthorizationReplayIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task CreateInvitation_WhenReplayPortDenies_RejectsCachedRetryAfterEndpointAuthorizationWithoutDuplicateEffect()
    {
        var trainer = await SeedUserAsync(
            "replay-port-trainer",
            "replay-port-trainer@example.com",
            additionalRoles: [AuthConstants.Roles.Trainer]);
        var trainee = await SeedUserAsync("replay-port-trainee", "replay-port-trainee@example.com");
        SetAuthorizationHeader(trainer.Id);
        var authorization = Client.DefaultRequestHeaders.Authorization;
        var denyingPort = new DenyingIdentityReplayAccessPort();

        using var overriddenFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IApiIdempotencyReplayAccessPort>();
            services.AddScoped<IApiIdempotencyReplayAccessPort>(_ => denyingPort);
        }));
        using var client = overriddenFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = authorization;
        client.DefaultRequestHeaders.Add(ApiIdempotencyHeaders.IdempotencyKey, "authorization-replay-port-denial");

        var first = await client.PostAsJsonAsync("/api/trainer/invitations", new { traineeId = trainee.Id.ToString() });
        var firstBody = await first.Content.ReadAsStringAsync();
        var denied = await client.PostAsJsonAsync("/api/trainer/invitations", new { traineeId = trainee.Id.ToString() });
        var deniedBody = await denied.Content.ReadAsStringAsync();

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        deniedBody.Should().NotBe(firstBody);
        deniedBody.Should().NotContain("traineeId");
        denyingPort.Calls.Should().Be(1);
        (await CountInvitationsAsync(trainer.Id, trainee.Id)).Should().Be(1);
    }

    [Test]
    public async Task IdentityReplayPort_UsesCurrentSessionRouteAndPermissionFacts()
    {
        var permittedTrainer = await SeedUserAsync(
            "replay-port-allowed",
            "replay-port-allowed@example.com",
            additionalRoles: [AuthConstants.Roles.Trainer]);
        SetAuthorizationHeader(permittedTrainer.Id);
        var permittedSession = await GetSessionIdAsync(permittedTrainer.Id);

        (await CanIdentityPortReplayAsync(
            permittedTrainer.Id,
            permittedSession,
            ApiIdempotencyReplayResourceBinding.None,
            AuthConstants.Permissions.TrainerAccess)).Should().BeTrue();

        await RevokeSessionAsync(permittedTrainer.Id);

        (await CanIdentityPortReplayAsync(
            permittedTrainer.Id,
            permittedSession,
            ApiIdempotencyReplayResourceBinding.None,
            AuthConstants.Permissions.TrainerAccess)).Should().BeFalse();

        var routeTrainer = await SeedUserAsync("replay-port-route", "replay-port-route@example.com");
        SetAuthorizationHeader(routeTrainer.Id);
        var routeSession = await GetSessionIdAsync(routeTrainer.Id);
        var unrelatedAccount = Id<AccountReference>.New();

        (await CanIdentityPortReplayAsync(
            routeTrainer.Id,
            routeSession,
            ApiIdempotencyReplayResourceBinding.RouteAccount,
            routeAccountId: unrelatedAccount)).Should().BeFalse();

        var demotedTrainer = await SeedUserAsync(
            "replay-port-demoted",
            "replay-port-demoted@example.com",
            additionalRoles: [AuthConstants.Roles.Trainer]);
        SetAuthorizationHeader(demotedTrainer.Id);
        var demotedSession = await GetSessionIdAsync(demotedTrainer.Id);
        await RemoveTrainerRoleAsync(demotedTrainer.Id);

        (await CanIdentityPortReplayAsync(
            demotedTrainer.Id,
            demotedSession,
            ApiIdempotencyReplayResourceBinding.None,
            AuthConstants.Permissions.TrainerAccess)).Should().BeFalse();
    }

    [Test]
    public async Task CreateInvitation_ReplaysForActiveSession_AndDeniedAfterSessionRevocationWithoutDuplicateEffect()
    {
        var trainer = await SeedUserAsync(
            "replay-session-trainer",
            "replay-session-trainer@example.com",
            additionalRoles: [AuthConstants.Roles.Trainer]);
        var trainee = await SeedUserAsync("replay-session-trainee", "replay-session-trainee@example.com");
        SetAuthorizationHeader(trainer.Id);
        const string idempotencyKey = "authorization-replay-session";
        SetIdempotencyKey(idempotencyKey);

        var first = await Client.PostAsJsonAsync("/api/trainer/invitations", new { traineeId = trainee.Id.ToString() });
        var firstBody = await first.Content.ReadAsStringAsync();
        var replay = await Client.PostAsJsonAsync("/api/trainer/invitations", new { traineeId = trainee.Id.ToString() });
        var replayBody = await replay.Content.ReadAsStringAsync();

        await RevokeSessionAsync(trainer.Id);

        var denied = await Client.PostAsJsonAsync("/api/trainer/invitations", new { traineeId = trainee.Id.ToString() });
        var deniedBody = await denied.Content.ReadAsStringAsync();
        ClearIdempotencyKey();

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        replayBody.Should().Be(firstBody);
        denied.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        deniedBody.Should().NotBe(firstBody);
        deniedBody.Should().Contain("Unauthorized");
        (await CountInvitationsAsync(trainer.Id, trainee.Id)).Should().Be(1);
    }

    [Test]
    public async Task CreateInvitation_DeniesCachedReplayAfterTrainerPermissionRemovalWithoutDuplicateEffect()
    {
        var trainer = await SeedUserAsync(
            "replay-permission-trainer",
            "replay-permission-trainer@example.com",
            additionalRoles: [AuthConstants.Roles.Trainer]);
        var trainee = await SeedUserAsync("replay-permission-trainee", "replay-permission-trainee@example.com");
        SetAuthorizationHeader(trainer.Id);
        const string idempotencyKey = "authorization-replay-permission";
        SetIdempotencyKey(idempotencyKey);

        var first = await Client.PostAsJsonAsync("/api/trainer/invitations", new { traineeId = trainee.Id.ToString() });
        var firstBody = await first.Content.ReadAsStringAsync();

        await RemoveTrainerRoleAsync(trainer.Id);

        var denied = await Client.PostAsJsonAsync("/api/trainer/invitations", new { traineeId = trainee.Id.ToString() });
        var deniedBody = await denied.Content.ReadAsStringAsync();
        ClearIdempotencyKey();

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        deniedBody.Should().NotBe(firstBody);
        deniedBody.Should().NotContain("traineeId");
        (await CountInvitationsAsync(trainer.Id, trainee.Id)).Should().Be(1);
    }

    private async Task RevokeSessionAsync(LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User> userId)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await database.UserSessions.SingleAsync(candidate => candidate.UserId == userId);
        session.RevokedAtUtc = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync();
    }

    private async Task<Id<AccountSessionReference>> GetSessionIdAsync(Id<User> userId)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await database.UserSessions.SingleAsync(candidate => candidate.UserId == userId);
        return session.Id.Rebind<AccountSessionReference>();
    }

    private async Task<bool> CanIdentityPortReplayAsync(
        Id<User> userId,
        Id<AccountSessionReference> sessionId,
        ApiIdempotencyReplayResourceBinding resourceBinding,
        string? requiredPermission = null,
        Id<AccountReference>? routeAccountId = null)
    {
        using var scope = Factory.Services.CreateScope();
        var port = scope.ServiceProvider.GetServices<IApiIdempotencyReplayAccessPort>()
            .Single(candidate => candidate.Owner == ApiIdempotencyReplayOwner.Identity);
        var context = new DefaultHttpContext();
        if (routeAccountId is { } routeAccount)
        {
            context.Request.RouteValues["id"] = routeAccount.ToString();
        }

        return await port.CanReplayAsync(
            new ApiIdempotencyReplayAccessContext(
                context,
                new ApiIdempotencyAttribute(
                    "/api/test",
                    ApiIdempotencyScopeSource.AuthenticatedUser,
                    ApiIdempotencyReplayOwner.Identity,
                    resourceBinding,
                    requiredPermission),
                new AuthenticatedAccountContext(
                    userId.Rebind<AccountReference>(),
                    sessionId,
                    [],
                    [],
                    IsBlocked: false,
                    IsDeleted: false)));
    }

    private async Task RemoveTrainerRoleAsync(LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User> userId)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trainerRoleIds = await database.UserRoles
            .Where(candidate => candidate.UserId == userId && candidate.Role.Name == AuthConstants.Roles.Trainer)
            .Select(candidate => candidate.RoleId)
            .ToListAsync();
        database.UserRoles.RemoveRange(database.UserRoles.Where(candidate =>
            candidate.UserId == userId && trainerRoleIds.Contains(candidate.RoleId)));
        await database.SaveChangesAsync();
    }

    private async Task<int> CountInvitationsAsync(
        LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User> trainerId,
        LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User> traineeId)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await database.TrainerInvitations.CountAsync(candidate =>
            candidate.TrainerId == trainerId && candidate.TraineeId == traineeId);
    }

    private sealed class DenyingIdentityReplayAccessPort : IApiIdempotencyReplayAccessPort
    {
        public int Calls { get; private set; }

        public ApiIdempotencyReplayOwner Owner => ApiIdempotencyReplayOwner.Identity;

        public Task<bool> CanReplayAsync(
            ApiIdempotencyReplayAccessContext context,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(false);
        }
    }
}
