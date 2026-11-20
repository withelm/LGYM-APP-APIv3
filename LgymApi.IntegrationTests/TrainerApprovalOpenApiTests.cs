using System.Text.Json;
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
        Assert.That(root.GetProperty("paths").TryGetProperty("/api/admin/users/trainer-applications", out _), Is.True);
        Assert.That(root.GetProperty("paths").TryGetProperty("/api/admin/users/{id}/trainer-application/approve", out _), Is.True);
        Assert.That(root.GetProperty("paths").TryGetProperty("/api/admin/users/{id}/trainer-application/reject", out _), Is.True);
        Assert.That(root.GetProperty("paths").GetProperty("/api/trainer/application/resubmit").GetProperty("post").GetProperty("responses")
            .TryGetProperty("200", out _), Is.True);
        var properties = root.GetProperty("components").GetProperty("schemas").GetProperty("TrainerApplicationDto").GetProperty("properties");
        Assert.That(properties.GetProperty("status").GetProperty("enum").EnumerateArray().Select(item => item.GetString()), Is.EquivalentTo(new[] { "PendingApproval", "Approved", "Rejected" }));
        Assert.That(properties.TryGetProperty("reviewedBy", out _), Is.True);
        Assert.That(properties.TryGetProperty("decidedAt", out _), Is.True);
        Assert.That(properties.GetProperty("attempt").GetProperty("type").GetString(), Is.EqualTo("integer"));
        Assert.That(root.GetProperty("components").GetProperty("schemas").GetProperty("UserInfoDto").GetProperty("properties")
            .TryGetProperty("trainerApprovalStatus", out _), Is.True);
        Assert.That(root.GetProperty("paths").GetProperty("/api/trainer/login").GetProperty("post").GetProperty("responses")
            .TryGetProperty("403", out _), Is.True);

        // Reproducible local contract export without connecting to any application database.
        var output = Environment.GetEnvironmentVariable("LGYM_OPENAPI_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output, json);
        }
    }
}
