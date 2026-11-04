using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class TrainerApprovalOpenApiTests : IntegrationTestBase
{
    [Test]
    public void ApprovalContract_ExportsActualHostSchemaWithTypedStatesAndOperations()
    {
        var document = Factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        using var text = new StringWriter();
        document.SerializeAsV3(new OpenApiJsonWriter(text));
        var json = text.ToString();
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        root.GetProperty("paths").TryGetProperty("/api/admin/users/trainer-applications", out _).Should().BeTrue();
        root.GetProperty("paths").TryGetProperty("/api/admin/users/{id}/trainer-application/approve", out _).Should().BeTrue();
        root.GetProperty("paths").TryGetProperty("/api/admin/users/{id}/trainer-application/reject", out _).Should().BeTrue();
        root.GetProperty("paths").GetProperty("/api/trainer/application/resubmit").GetProperty("post").GetProperty("responses")
            .TryGetProperty("204", out _).Should().BeTrue();
        var properties = root.GetProperty("components").GetProperty("schemas").GetProperty("TrainerApplicationDto").GetProperty("properties");
        properties.GetProperty("status").GetProperty("enum").EnumerateArray().Select(item => item.GetString())
            .Should().BeEquivalentTo("PendingApproval", "Approved", "Rejected");
        properties.TryGetProperty("reviewedBy", out _).Should().BeTrue();
        properties.TryGetProperty("decidedAt", out _).Should().BeTrue();
        properties.GetProperty("attempt").GetProperty("type").GetString().Should().Be("integer");
        root.GetProperty("components").GetProperty("schemas").GetProperty("UserInfoDto").GetProperty("properties")
            .TryGetProperty("trainerApprovalStatus", out _).Should().BeTrue();
        root.GetProperty("paths").GetProperty("/api/trainer/login").GetProperty("post").GetProperty("responses")
            .TryGetProperty("403", out _).Should().BeTrue();

        // Reproducible local contract export without connecting to any application database.
        var output = Environment.GetEnvironmentVariable("LGYM_OPENAPI_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output, json);
        }
    }
}
