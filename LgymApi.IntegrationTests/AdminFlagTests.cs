using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class AdminFlagTests : IntegrationTestBase
{
    [Test]
    public async Task IsAdmin_WithAdminUser_ReturnsTrue()
    {
        var adminUser = await SeedUserAsync(name: "adminuser", email: "admin@example.com", isAdmin: true);
        SetAuthorizationHeader(adminUser.Id);

        var response = await Client.GetAsync($"/api/{adminUser.Id}/isAdmin");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<bool>();
        result.Should().BeTrue();
    }

    [Test]
    public async Task IsAdmin_WithNonAdminUser_ReturnsFalse()
    {
        var normalUser = await SeedUserAsync(name: "normaluser", email: "normal@example.com", isAdmin: false);
        SetAuthorizationHeader(normalUser.Id);

        var response = await Client.GetAsync($"/api/{normalUser.Id}/isAdmin");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<bool>();
        result.Should().BeFalse();
    }

    [Test]
    public async Task IsAdmin_WithNullAdminFlag_ReturnsFalse()
    {
        var user = await SeedUserAsync(name: "nulladmin", email: "nulladmin@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/{user.Id}/isAdmin");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<bool>();
        result.Should().BeFalse();
    }

    [Test]
    public async Task IsAdmin_WithNonExistentUser_ReturnsFalse()
    {
        var user = await SeedUserAsync(name: "authuser", email: "auth@example.com");
        SetAuthorizationHeader(user.Id);

        var nonExistentId = Guid.NewGuid();
        var response = await Client.GetAsync($"/api/{nonExistentId}/isAdmin");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<bool>();
        result.Should().BeFalse();
    }

    [Test]
    public async Task IsAdmin_WithInvalidGuidFormat_ReturnsFalse()
    {
        var user = await SeedUserAsync(name: "authuser", email: "auth@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync("/api/invalid-guid/isAdmin");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<bool>();
        result.Should().BeFalse();
    }
}
