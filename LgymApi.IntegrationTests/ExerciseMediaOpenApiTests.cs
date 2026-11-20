using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class ExerciseMediaOpenApiTests : IntegrationTestBase
{
    [Test]
    public void CanonicalContract_DeclaresOneFileOnlyUploadAndUpdateIntention()
    {
        var document = Factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        using var text = new StringWriter();
        document.SerializeAsV3(new OpenApiJsonWriter(text));
        var json = text.ToString();
        var assertedChain7 = json;
        Assert.That(assertedChain7, Does.Contain("/api/exercise/media/upload"));
        Assert.That(assertedChain7, Does.Contain("multipart/form-data").And.Contain("updateImage")
            .And.Not.Contain("/api/exercise/media/create").And.Not.Contain("/api/exercise/{id}/media")
            .And.Not.Contain("/api/exercise/media/cleanup").And.Not.Contain("X-Exercise-Media-Operation-Id")
            .And.Not.Contain("sourceSha256").And.Not.Contain("expectedImage"));
        using var parsed = System.Text.Json.JsonDocument.Parse(json);
        Assert.That(parsed.RootElement.GetProperty("paths").GetProperty("/api/exercise/media/upload").GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data")
            .GetProperty("schema").GetProperty("required").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "file" }));
        var output = Environment.GetEnvironmentVariable("LGYM_OPENAPI_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output, json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
        }
    }
}
