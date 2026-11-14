using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Application.Notifications.Contracts.Email;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.AccountDeletion;
using LgymApi.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class AccountDeletionRecoveryTests : IntegrationTestBase
{
    private const string Recovery = "/api/account-deletion/recovery";

    [TestCase(false)]
    [TestCase(true)]
    [Authorization.AuthorizationEvidence("POST", Recovery + "/login", "public", "anonymous-intended-behavior")]
    [Authorization.AuthorizationEvidence("POST", Recovery + "/cancel", "public", "anonymous-intended-behavior")]
    public async Task Recovery_RestoresOriginalAccountDataAndRelationshipsWithoutRevivingOldSessions(bool isAdmin)
    {
        var user = await SeedUserAsync(isAdmin: isAdmin);
        var trainer = await SeedUserAsync("recovery-trainer", "recovery-trainer@example.test");
        using (var setup = Factory.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = user.Id });
            db.Measurements.Add(new Measurement { Id = Id<Measurement>.New(), UserId = user.Id, Value = 77, Unit = "cm" });
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(user.Id);
        var firstSession = Client.DefaultRequestHeaders.Authorization;
        SetAuthorizationHeader(user.Id);
        var secondSession = Client.DefaultRequestHeaders.Authorization;
        (await Client.GetAsync("/api/account/access")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync("/api/deleteAccount")).StatusCode.Should().Be(HttpStatusCode.OK);
        foreach (var session in new[] { firstSession, secondSession })
        {
            Client.DefaultRequestHeaders.Authorization = session;
            (await Client.GetAsync("/api/checkToken")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await Client.GetAsync("/api/account/access")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        ClearAuthorizationHeader();
        (await Client.PostAsJsonAsync("/api/login", new { name = user.Name, password = "password123" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var token = await RecoveryTokenAsync(Client, user.Name);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await Client.GetAsync("/api/checkToken")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync("/api/account/access")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        ClearAuthorizationHeader();

        var cancellation = await Client.PostAsJsonAsync(Recovery + "/cancel", new { recoveryToken = token });

        cancellation.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await Client.PostAsJsonAsync("/api/login", new { name = user.Name, password = "password123" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        using var verify = Factory.Services.CreateScope();
        var database = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var restored = await database.Users.SingleAsync(account => account.Id == user.Id);
        restored.Name.Should().Be(user.Name);
        restored.Email.Should().Be(user.Email);
        restored.DeletionState.Should().Be(AccountDeletionState.Active);
        (await database.Measurements.SingleAsync(measurement => measurement.UserId == user.Id)).Value.Should().Be(77);
        (await database.TrainerTraineeLinks.CountAsync(link => link.TraineeId == user.Id && link.TrainerId == trainer.Id)).Should().Be(1);
        Client.DefaultRequestHeaders.Authorization = firstSession;
        (await Client.GetAsync("/api/checkToken")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    [Authorization.AuthorizationEvidence("POST", Recovery + "/logout", "public", "anonymous-intended-behavior")]
    public async Task RecoveryLogout_InvalidatesTheCapabilityWithoutRestoringAccount()
    {
        var user = await SeedUserAsync();
        SetAuthorizationHeader(user.Id);
        (await Client.GetAsync("/api/deleteAccount")).EnsureSuccessStatusCode();
        ClearAuthorizationHeader();
        var token = await RecoveryTokenAsync(Client, user.Name);

        (await Client.PostAsJsonAsync(Recovery + "/logout", new { recoveryToken = token })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Client.PostAsJsonAsync(Recovery + "/cancel", new { recoveryToken = token })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var scope = Factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(account => account.Id == user.Id))
            .DeletionState.Should().Be(AccountDeletionState.PendingDeletion);
    }

    [TestCase("cancel", -1, HttpStatusCode.OK)]
    [TestCase("cancel", 0, HttpStatusCode.Forbidden)]
    [TestCase("cancel", 1, HttpStatusCode.Forbidden)]
    [TestCase("logout", -1, HttpStatusCode.OK)]
    [TestCase("logout", 0, HttpStatusCode.Forbidden)]
    [TestCase("logout", 1, HttpStatusCode.Forbidden)]
    [Authorization.AuthorizationEvidence("POST", Recovery + "/cancel", "public", "expired-capability-denial")]
    [Authorization.AuthorizationEvidence("POST", Recovery + "/logout", "public", "expired-capability-denial")]
    public async Task Cancellation_UsesStrictServerDeadline(string action, int secondsFromDeadline, HttpStatusCode expected)
    {
        var user = await SeedUserAsync();
        var clock = new RecoveryClock(DateTimeOffset.UtcNow);
        var deadline = clock.Now.AddDays(30);
        using var factory = Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = factory.CreateClient();
        SetAuthorizationHeader(user.Id);
        client.DefaultRequestHeaders.Authorization = Client.DefaultRequestHeaders.Authorization;
        (await client.GetAsync("/api/deleteAccount")).EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;
        clock.Now = deadline.AddMinutes(-1);
        var token = await RecoveryTokenAsync(client, user.Name);
        clock.Now = deadline.AddSeconds(secondsFromDeadline);

        (await client.PostAsJsonAsync(Recovery + "/" + action, new { recoveryToken = token })).StatusCode.Should().Be(expected);
    }

    [TestCase("cancel", false)]
    [TestCase("cancel", true)]
    [TestCase("logout", false)]
    [TestCase("logout", true)]
    [Authorization.AuthorizationEvidence("POST", Recovery + "/cancel", "public", "invalid-capability-denial")]
    [Authorization.AuthorizationEvidence("POST", Recovery + "/cancel", "public", "tampered-capability-denial")]
    [Authorization.AuthorizationEvidence("POST", Recovery + "/logout", "public", "invalid-capability-denial")]
    [Authorization.AuthorizationEvidence("POST", Recovery + "/logout", "public", "tampered-capability-denial")]
    public async Task RecoveryActions_DenyUnknownAndTamperedCapabilities(string action, bool tampered)
    {
        var user = await SeedUserAsync();
        SetAuthorizationHeader(user.Id);
        (await Client.GetAsync("/api/deleteAccount")).EnsureSuccessStatusCode();
        ClearAuthorizationHeader();
        var token = await RecoveryTokenAsync(Client, user.Name);
        var supplied = tampered ? (token[0] == '0' ? "1" : "0") + token[1..] : new string('0', 64);

        (await Client.PostAsJsonAsync(Recovery + "/" + action, new { recoveryToken = supplied })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await Client.PostAsJsonAsync(Recovery + "/cancel", new { recoveryToken = token })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task DuplicateDeletion_KeepsOriginalGenerationAndDeadline()
    {
        var user = await SeedUserAsync();
        SetAuthorizationHeader(user.Id);
        (await Client.GetAsync("/api/deleteAccount")).EnsureSuccessStatusCode();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pending = await db.Users.SingleAsync(account => account.Id == user.Id);
        var generation = pending.DeletionGeneration;
        var deadline = pending.DeletionEffectiveAt;

        await scope.ServiceProvider.GetRequiredService<IAccountDeletionService>().RequestAsync(user.Id.Rebind<AccountReference>());

        await db.Entry(pending).ReloadAsync();
        pending.DeletionGeneration.Should().Be(generation);
        pending.DeletionEffectiveAt.Should().Be(deadline);
        pending.DeletionEffectiveAt.Should().Be(pending.DeletionRequestedAt!.Value.AddDays(30));
        (await db.UserSessions.Where(session => session.UserId == user.Id).AllAsync(session => session.RevokedAtUtc != null)).Should().BeTrue();
    }

    [Test]
    [Authorization.AuthorizationEvidence("POST", Recovery + "/email", "public", "anonymous-intended-behavior")]
    public async Task EmailProof_RestoresOAuthOnlyAccountThroughDurableDelivery()
    {
        var user = await SeedUserAsync();
        using (var setup = Factory.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.Users.SingleAsync(candidate => candidate.Id == user.Id);
            account.LegacyHash = null;
            account.LegacySalt = null;
            db.UserExternalLogins.Add(new UserExternalLogin { Id = Id<UserExternalLogin>.New(), UserId = user.Id, Provider = "Google", ProviderKey = "recovery-provider-subject" });
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(user.Id);
        (await Client.GetAsync("/api/deleteAccount")).EnsureSuccessStatusCode();
        ClearAuthorizationHeader();

        var response = await Client.PostAsJsonAsync(Recovery + "/email", new { email = user.Email.Value });
        var unknown = await Client.PostAsJsonAsync(Recovery + "/email", new { email = "unknown-recovery@example.test" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        unknown.StatusCode.Should().Be(response.StatusCode);
        (await unknown.Content.ReadAsStringAsync()).Should().Be(await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).Should().NotContain("recoveryToken");
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notification = await database.NotificationMessages.SingleAsync(message => message.Type == EmailNotificationTypes.AccountRecovery);
        var payload = JsonSerializer.Deserialize<AccountRecoveryEmail>(notification.PayloadJson, SharedSerializationOptions.Current)!;
        await scope.ServiceProvider.GetRequiredService<IEmailJobExecutionPort>().ProcessAsync(notification.Id.ToString());
        Factory.EmailSender.SentMessages.Should().ContainSingle(message => message.To == user.Email.Value && message.Body.Contains(payload.RecoveryToken));
        (await Client.PostAsJsonAsync(Recovery + "/cancel", new { recoveryToken = payload.RecoveryToken })).StatusCode.Should().Be(HttpStatusCode.OK);
        database.ChangeTracker.Clear();
        (await database.Users.SingleAsync(account => account.Id == user.Id)).DeletionState.Should().Be(AccountDeletionState.Active);
        (await database.UserExternalLogins.SingleAsync(login => login.UserId == user.Id)).ProviderKey.Should().Be("recovery-provider-subject");
    }

    private static async Task<string> RecoveryTokenAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(Recovery + "/login", new { name, password = "password123" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("recoveryToken").GetString()!;
    }

    private sealed class RecoveryClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
