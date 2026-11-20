using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Globalization;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using LgymApi.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class CoachingContractCompatibilityTests : IntegrationTestBase
{
    [TestCase("/api/invitations/not-a-guid?code=unused")]
    [TestCase("/api/invitations/00000000-0000-0000-0000-000000000000")]
    [TestCase("/api/invitations/00000000-0000-0000-0000-000000000001?code=unused")]
    [TestCase("/api/invitations/00000000-0000-0000-0000-000000000001?code=")]
    [TestCase("/api/invitations/00000000-0000-0000-0000-000000000001?code=%20")]
    public async Task PublicInvitationStatus_ReturnsBareNotFoundForMissingOrMalformedInput(string route)
    {
        ClearAuthorizationHeader();

        var response = await Client.GetAsync(route);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("title").GetString(), Is.EqualTo("Not Found"));
        Assert.That(body.RootElement.GetProperty("status").GetInt32(), Is.EqualTo((int)HttpStatusCode.NotFound));
        Assert.That(body.RootElement.TryGetProperty("msg", out _), Is.False);
    }

    [Test]
    public async Task TrainerInvitationResponses_PreserveLegacyIdAndMessageFields()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            var trainer = await SeedTrainerAsync("coaching-contract-trainer", "coaching-contract-trainer@example.com");
            var trainee = await SeedUserAsync("coaching-contract-trainee", "coaching-contract-trainee@example.com", "password123");
            SetAuthorizationHeader(trainer.Id);
            SetIdempotencyKey("coaching-contract-invitation");

            HttpResponseMessage createResponse;
            try
            {
                createResponse = await Client.PostAsJsonAsync("/api/trainer/invitations", new { traineeId = trainee.Id.ToString() });
            }
            finally
            {
                ClearIdempotencyKey();
            }

            Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            using var invitation = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
            Assert.That(string.IsNullOrWhiteSpace(invitation.RootElement.GetProperty("_id").GetString()), Is.False);
            Assert.That(invitation.RootElement.TryGetProperty("id", out _), Is.False);

            var revokeResponse = await Client.PostAsync($"/api/trainer/invitations/{invitation.RootElement.GetProperty("_id").GetString()}/revoke", null);

            Assert.That(revokeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            using var message = JsonDocument.Parse(await revokeResponse.Content.ReadAsStringAsync());
            Assert.That(message.RootElement.GetProperty("msg").GetString(), Is.EqualTo(Messages.Updated));
            Assert.That(message.RootElement.TryGetProperty("message", out _), Is.False);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Test]
    public async Task TrainerInvitation_MalformedTraineeId_ReturnsValidationBadRequest()
    {
        var trainer = await SeedTrainerAsync("coaching-contract-invalid-trainer", "coaching-contract-invalid-trainer@example.com");
        SetAuthorizationHeader(trainer.Id);
        SetIdempotencyKey("coaching-contract-invalid-invitation");

        HttpResponseMessage response;
        try
        {
            response = await Client.PostAsJsonAsync("/api/trainer/invitations", new { traineeId = "not-a-guid" });
        }
        finally
        {
            ClearIdempotencyKey();
        }

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.TryGetProperty("errors", out _), Is.True);
        Assert.That(body.RootElement.TryGetProperty("msg", out _), Is.False);
    }

    [Test]
    public async Task TraineeInvitation_MalformedId_ReturnsLocalizedLegacyMessage()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            var trainee = await SeedUserAsync("coaching-contract-invalid", "coaching-contract-invalid@example.com", "password123");
            SetAuthorizationHeader(trainee.Id);

            var response = await Client.PostAsync("/api/trainee/invitations/not-a-guid/accept", null);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.That(body.RootElement.GetProperty("msg").GetString(), Is.EqualTo(Messages.FieldRequired));
            Assert.That(body.RootElement.TryGetProperty("message", out _), Is.False);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name, email, "password123");
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(db, trainer.Id));
        await db.SaveChangesAsync();
        return trainer;
    }
}
