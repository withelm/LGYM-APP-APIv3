using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class PasswordRecoveryTests : IntegrationTestBase
{
    private const string ForgotPasswordGenericMessage = "If an account with that email exists, a password reset link has been sent.";
    private const string ResetPasswordSuccessMessage = "Password reset successfully.";
    private const string ResetPasswordInvalidMessage = "Invalid or expired reset token.";

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/forgot-password", "public", "anonymous-intended-behavior")]
    public async Task PasswordRecovery_ForgotPassword_WithExistingEmail_ReturnsOkGenericMessageAndCreatesNotificationRow()
    {
        var user = await SeedUserAsync(
            name: "recover-existing",
            email: "recover-existing@example.com",
            password: "old-password");

        var response = await Client.PostAsJsonAsync("/api/forgot-password", new
        {
            email = "recover-existing@example.com"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo(ForgotPasswordGenericMessage));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var token = await db.PasswordResetTokens.SingleOrDefaultAsync(t => t.UserId == user.Id);
        Assert.That(token, Is.Not.Null);
        Assert.That(token!.IsUsed, Is.False);
        Assert.That(token.ExpiresAt, Is.GreaterThan(DateTimeOffset.UtcNow.AddMinutes(25)));

        var notification = await db.NotificationMessages.SingleOrDefaultAsync(n =>
            n.Type == EmailNotificationTypes.PasswordRecovery
            && n.Recipient == "recover-existing@example.com");
        Assert.That(notification, Is.Not.Null);
    }

    [Test]
    public async Task PasswordRecovery_ForgotPassword_WithNonExistingEmail_ReturnsOkWithSameGenericMessage()
    {
        var response = await Client.PostAsJsonAsync("/api/forgot-password", new
        {
            email = "missing-user@example.com"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo(ForgotPasswordGenericMessage));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tokenCount = await db.PasswordResetTokens.CountAsync();
        Assert.That(tokenCount, Is.EqualTo(0));

        var notificationCount = await db.NotificationMessages
            .CountAsync(n => n.Type == EmailNotificationTypes.PasswordRecovery);
        Assert.That(notificationCount, Is.EqualTo(0));
    }

    [Test]
    public async Task PasswordRecovery_ForgotPassword_WithDeletedUser_ReturnsOkAndDoesNotCreateToken()
    {
        var user = await SeedUserAsync(
            name: "recover-deleted",
            email: "recover-deleted@example.com",
            password: "old-password",
            isDeleted: true);

        var response = await Client.PostAsJsonAsync("/api/forgot-password", new
        {
            email = "recover-deleted@example.com"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo(ForgotPasswordGenericMessage));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tokenCount = await db.PasswordResetTokens.CountAsync(t => t.UserId == user.Id);
        Assert.That(tokenCount, Is.EqualTo(0));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/reset-password", "public", "anonymous-intended-behavior")]
    public async Task PasswordRecovery_ResetPassword_WithValidToken_ReturnsOkAndAllowsLoginWithNewPassword()
    {
        var user = await SeedUserAsync(
            name: "reset-valid",
            email: "reset-valid@example.com",
            password: "old-password");
        const string validToken = "VALID-RESET-TOKEN-001";
        await SeedPasswordResetTokenAsync(user.Id, validToken, DateTimeOffset.UtcNow.AddMinutes(30), isUsed: false);

        var response = await Client.PostAsJsonAsync("/api/reset-password", new
        {
            token = validToken,
            newPassword = "new-password-123",
            confirmPassword = "new-password-123"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo(ResetPasswordSuccessMessage));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tokenEntity = await db.PasswordResetTokens.SingleAsync(t => t.UserId == user.Id);
            Assert.That(tokenEntity.IsUsed, Is.True);
        }

        var oldPasswordLoginResponse = await Client.PostAsJsonAsync("/api/login", new
        {
            name = "reset-valid",
            password = "old-password"
        });
        Assert.That(oldPasswordLoginResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var newPasswordLoginResponse = await Client.PostAsJsonAsync("/api/login", new
        {
            name = "reset-valid",
            password = "new-password-123"
        });
        Assert.That(newPasswordLoginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var loginBody = await newPasswordLoginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(loginBody, Is.Not.Null);
        Assert.That(string.IsNullOrWhiteSpace(loginBody!.Token), Is.False);
    }

    [Test]
    public async Task PasswordRecovery_ResetPassword_WithExpiredToken_ReturnsBadRequest()
    {
        var user = await SeedUserAsync(
            name: "reset-expired",
            email: "reset-expired@example.com",
            password: "old-password");
        const string expiredToken = "EXPIRED-RESET-TOKEN-001";
        await SeedPasswordResetTokenAsync(user.Id, expiredToken, DateTimeOffset.UtcNow.AddMinutes(-1), isUsed: false);

        var response = await Client.PostAsJsonAsync("/api/reset-password", new
        {
            token = expiredToken,
            newPassword = "new-password-123",
            confirmPassword = "new-password-123"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo(ResetPasswordInvalidMessage));
    }

    [Test]
    public async Task PasswordRecovery_ResetPassword_WithUsedToken_ReturnsBadRequest()
    {
        var user = await SeedUserAsync(
            name: "reset-used",
            email: "reset-used@example.com",
            password: "old-password");
        const string usedToken = "USED-RESET-TOKEN-001";
        await SeedPasswordResetTokenAsync(user.Id, usedToken, DateTimeOffset.UtcNow.AddMinutes(30), isUsed: true);

        var response = await Client.PostAsJsonAsync("/api/reset-password", new
        {
            token = usedToken,
            newPassword = "new-password-123",
            confirmPassword = "new-password-123"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo(ResetPasswordInvalidMessage));
    }

    [Test]
    public async Task PasswordRecovery_ResetPassword_WithInvalidToken_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync("/api/reset-password", new
        {
            token = "NOT-A-REAL-TOKEN",
            newPassword = "new-password-123",
            confirmPassword = "new-password-123"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo(ResetPasswordInvalidMessage));
    }

    [Test]
    public async Task PasswordRecovery_ResetPassword_OnSuccess_InvalidatesPreviousSession()
    {
        var user = await SeedUserAsync(
            name: "reset-session",
            email: "reset-session@example.com",
            password: "session-old-password");

        var loginResponse = await Client.PostAsJsonAsync("/api/login", new
        {
            name = "reset-session",
            password = "session-old-password"
        });
        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(loginBody, Is.Not.Null);
        Assert.That(string.IsNullOrWhiteSpace(loginBody!.Token), Is.False);

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.Token);
        var beforeResetCheckToken = await Client.GetAsync("/api/checkToken");
        Assert.That(beforeResetCheckToken.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        const string validToken = "SESSION-RESET-TOKEN-001";
        await SeedPasswordResetTokenAsync(user.Id, validToken, DateTimeOffset.UtcNow.AddMinutes(30), isUsed: false);

        var resetResponse = await Client.PostAsJsonAsync("/api/reset-password", new
        {
            token = validToken,
            newPassword = "session-new-password",
            confirmPassword = "session-new-password"
        });
        Assert.That(resetResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.Token);
        var afterResetCheckToken = await Client.GetAsync("/api/checkToken");
        Assert.That(afterResetCheckToken.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var middlewareError = await afterResetCheckToken.Content.ReadFromJsonAsync<MiddlewareErrorResponse>();
        Assert.That(middlewareError, Is.Not.Null);
        Assert.That(middlewareError!.Message, Is.EqualTo("Unauthorized"));
    }

    [Test]
    public async Task PasswordRecovery_ForgotPassword_DispatchesEmail()
    {
        await SeedUserAsync(
            name: "recover-email",
            email: "recover-email@example.com",
            password: "old-password");

        var response = await Client.PostAsJsonAsync("/api/forgot-password", new
        {
            email = "recover-email@example.com"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo(ForgotPasswordGenericMessage));

        await ProcessPendingCommandsAsync();

        LgymApi.Domain.ValueObjects.Id<NotificationMessage> notificationId;
        using (var notificationScope = Factory.Services.CreateScope())
        {
            var notificationDb = notificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var queuedNotification = await notificationDb.NotificationMessages.SingleAsync(n =>
                n.Type == EmailNotificationTypes.PasswordRecovery
                && n.Recipient == "recover-email@example.com");
            notificationId = queuedNotification.Id;
        }

        using (var handlerScope = Factory.Services.CreateScope())
        {
            var emailHandler = handlerScope.ServiceProvider.GetRequiredService<IEmailJobHandler>();
            await emailHandler.ProcessAsync(notificationId.ToString());
        }

        Assert.That(Factory.EmailSender.SentMessages, Has.Exactly(1).Items);
        var sentMessage = Factory.EmailSender.SentMessages.Single();
        Assert.That(sentMessage.To, Is.EqualTo("recover-email@example.com"));
        Assert.That(string.IsNullOrWhiteSpace(sentMessage.Subject), Is.False);
        Assert.That(string.IsNullOrWhiteSpace(sentMessage.Body), Is.False);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notification = await db.NotificationMessages.SingleOrDefaultAsync(n =>
            n.Type == EmailNotificationTypes.PasswordRecovery
            && n.Recipient == "recover-email@example.com");

        Assert.That(notification, Is.Not.Null);
        Assert.That(notification!.Status, Is.EqualTo(EmailNotificationStatus.Sent));
        Assert.That(notification.SentAt, Is.Not.Null);
    }

    private async Task SeedPasswordResetTokenAsync(
        LgymApi.Domain.ValueObjects.Id<User> userId,
        string plainTextToken,
        DateTimeOffset expiresAt,
        bool isUsed)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = LgymApi.Domain.ValueObjects.Id<PasswordResetToken>.New(),
            UserId = userId,
            TokenHash = ComputeSha256Hex(plainTextToken),
            ExpiresAt = expiresAt,
            IsUsed = isUsed
        });

        await db.SaveChangesAsync();
    }

    private static string ComputeSha256Hex(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private sealed class MessageResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class MiddlewareErrorResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class LoginResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }
}
