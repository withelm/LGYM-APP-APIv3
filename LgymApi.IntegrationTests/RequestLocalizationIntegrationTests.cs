using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LgymApi.Resources;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class RequestLocalizationIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task AuthenticationChallenge_UsesAcceptLanguageAndDoesNotLeakCultureAcrossRequests()
    {
        var englishMessage = GetMessageForCulture("en");
        var polishMessage = GetMessageForCulture("pl");
        Assert.That(englishMessage, Is.Not.EqualTo(polishMessage));

        Assert.That((await GetChallengeMessageAsync()), Is.EqualTo(englishMessage));
        Assert.That((await GetChallengeMessageAsync(request => request.Headers.AcceptLanguage.ParseAdd("pl"))), Is.EqualTo(polishMessage));
        Assert.That((await GetChallengeMessageAsync()), Is.EqualTo(englishMessage));
    }

    [TestCase("de")]
    [TestCase("pl;q=0.1, en;q=0.9")]
    public async Task AuthenticationChallenge_FallsBackToEnglishForUnsupportedOrLowerPriorityAcceptLanguage(string acceptLanguage)
    {
        var englishMessage = GetMessageForCulture("en");

        var message = await GetChallengeMessageAsync(request => request.Headers.AcceptLanguage.ParseAdd(acceptLanguage));

        Assert.That(message, Is.EqualTo(englishMessage));
    }

    [Test]
    public async Task AuthenticationChallenge_IgnoresQueryStringAndCookieCultureProviders()
    {
        var englishMessage = GetMessageForCulture("en");

        var message = await GetChallengeMessageAsync(request =>
        {
            request.RequestUri = new Uri("/api/admin/users/not-a-guid?culture=pl", UriKind.Relative);
            request.Headers.TryAddWithoutValidation("Cookie", ".AspNetCore.Culture=c%3Dpl%7Cuic%3Dpl");
        });

        Assert.That(message, Is.EqualTo(englishMessage));
    }

    [Test]
    public async Task AppConfigUnknownEnum_UsesPolishValidatorResourceInCompatibilityBody()
    {
        var polishMessage = GetMessageForCulture("pl", () => Messages.FieldRequired);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/appConfig/getAppVersion")
        {
            Content = JsonContent.Create(new { platform = "Unknown" })
        };
        request.Headers.AcceptLanguage.ParseAdd("pl");

        using var response = await Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("errors").GetProperty("platform")[0].GetString(), Is.EqualTo(polishMessage));
        Assert.That(body.RootElement.TryGetProperty("msg", out _), Is.False);
        Assert.That(body.RootElement.TryGetProperty("message", out _), Is.False);
    }

    [Test]
    public async Task AppConfigMalformedEnum_PreservesStrictBadRequestCompatibilityBody()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/appConfig/getAppVersion")
        {
            Content = JsonContent.Create(new { platform = "InvalidPlatform" })
        };

        using var response = await Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("status").GetInt32(), Is.EqualTo(400));
        var platformErrors = body.RootElement.GetProperty("errors").GetProperty("$.platform");
        Assert.That(platformErrors.GetArrayLength(), Is.EqualTo(1));
        Assert.That(platformErrors[0].GetString(), Does.Contain("could not be converted to LgymApi.Domain.Enums.Platforms"));
        Assert.That(body.RootElement.TryGetProperty("msg", out _), Is.False);
        Assert.That(body.RootElement.TryGetProperty("message", out _), Is.False);
    }

    private async Task<string> GetChallengeMessageAsync(Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users/not-a-guid");
        configure?.Invoke(request);
        using var response = await Client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("msg").GetString()!;
    }

    private static string GetMessageForCulture(string cultureName)
    {
        return GetMessageForCulture(cultureName, () => Messages.InvalidToken);
    }

    private static string GetMessageForCulture(string cultureName, Func<string> getMessage)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return getMessage();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
