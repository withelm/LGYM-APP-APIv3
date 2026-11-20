using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
        Assert.That((await Client.GetAsync("/api/account/access")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await Client.GetAsync("/api/deleteAccount")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        foreach (var session in new[] { firstSession, secondSession })
        {
            Client.DefaultRequestHeaders.Authorization = session;
            Assert.That((await Client.GetAsync("/api/checkToken")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That((await Client.GetAsync("/api/account/access")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }
        ClearAuthorizationHeader();
        Assert.That((await Client.PostAsJsonAsync("/api/login", new { name = user.Name, password = "password123" })).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var token = await RecoveryTokenAsync(Client, user.Name);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.That((await Client.GetAsync("/api/checkToken")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await Client.GetAsync("/api/account/access")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        ClearAuthorizationHeader();

        var cancellation = await Client.PostAsJsonAsync(Recovery + "/cancel", new { recoveryToken = token });

        Assert.That(cancellation.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var login = await Client.PostAsJsonAsync("/api/login", new { name = user.Name, password = "password123" });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var verify = Factory.Services.CreateScope();
        var database = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var restored = await database.Users.SingleAsync(account => account.Id == user.Id);
        Assert.That(restored.Name, Is.EqualTo(user.Name));
        Assert.That(restored.Email, Is.EqualTo(user.Email));
        Assert.That(restored.DeletionState, Is.EqualTo(AccountDeletionState.Active));
        Assert.That((await database.Measurements.SingleAsync(measurement => measurement.UserId == user.Id)).Value, Is.EqualTo(77));
        Assert.That((await database.TrainerTraineeLinks.CountAsync(link => link.TraineeId == user.Id && link.TrainerId == trainer.Id)), Is.EqualTo(1));
        Client.DefaultRequestHeaders.Authorization = firstSession;
        Assert.That((await Client.GetAsync("/api/checkToken")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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

        Assert.That((await Client.PostAsJsonAsync(Recovery + "/logout", new { recoveryToken = token })).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Assert.That((await Client.PostAsJsonAsync(Recovery + "/cancel", new { recoveryToken = token })).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        using var scope = Factory.Services.CreateScope();
        Assert.That((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(account => account.Id == user.Id))
            .DeletionState, Is.EqualTo(AccountDeletionState.PendingDeletion));
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

        Assert.That((await client.PostAsJsonAsync(Recovery + "/" + action, new { recoveryToken = token })).StatusCode, Is.EqualTo(expected));
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

        Assert.That((await Client.PostAsJsonAsync(Recovery + "/" + action, new { recoveryToken = supplied })).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        Assert.That((await Client.PostAsJsonAsync(Recovery + "/cancel", new { recoveryToken = token })).StatusCode, Is.EqualTo(HttpStatusCode.OK));
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
        Assert.That(pending.DeletionGeneration, Is.EqualTo(generation));
        Assert.That(pending.DeletionEffectiveAt, Is.EqualTo(deadline));
        Assert.That(pending.DeletionEffectiveAt, Is.EqualTo(pending.DeletionRequestedAt!.Value.AddDays(30)));
        Assert.That((await db.UserSessions.Where(session => session.UserId == user.Id).AllAsync(session => session.RevokedAtUtc != null)), Is.True);
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

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(unknown.StatusCode, Is.EqualTo(response.StatusCode));
        Assert.That((await unknown.Content.ReadAsStringAsync()), Is.EqualTo(await response.Content.ReadAsStringAsync()));
        Assert.That((await response.Content.ReadAsStringAsync()), Does.Not.Contain("recoveryToken"));
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notification = await database.NotificationMessages.SingleAsync(message => message.Type == EmailNotificationTypes.AccountRecovery);
        var payload = JsonSerializer.Deserialize<AccountRecoveryEmail>(notification.PayloadJson, SharedSerializationOptions.Current)!;
        await scope.ServiceProvider.GetRequiredService<IEmailJobExecutionPort>().ProcessAsync(notification.Id.ToString());
        Assert.That((Factory.EmailSender.SentMessages).Where(message => message.To == user.Email.Value && message.Body.Contains(payload.RecoveryToken)), Has.Exactly(1).Items);
        Assert.That((await Client.PostAsJsonAsync(Recovery + "/cancel", new { recoveryToken = payload.RecoveryToken })).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        database.ChangeTracker.Clear();
        Assert.That((await database.Users.SingleAsync(account => account.Id == user.Id)).DeletionState, Is.EqualTo(AccountDeletionState.Active));
        Assert.That((await database.UserExternalLogins.SingleAsync(login => login.UserId == user.Id)).ProviderKey, Is.EqualTo("recovery-provider-subject"));
    }

    private static async Task<string> RecoveryTokenAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(Recovery + "/login", new { name, password = "password123" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("recoveryToken").GetString()!;
    }

    private sealed class RecoveryClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
