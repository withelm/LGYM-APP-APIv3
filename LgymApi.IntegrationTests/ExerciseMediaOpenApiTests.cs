using FluentAssertions;
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
        json.Should().Contain("/api/exercise/media/upload").And.Contain("multipart/form-data").And.Contain("updateImage")
            .And.NotContain("/api/exercise/media/create").And.NotContain("/api/exercise/{id}/media")
            .And.NotContain("/api/exercise/media/cleanup").And.NotContain("X-Exercise-Media-Operation-Id")
            .And.NotContain("sourceSha256").And.NotContain("expectedImage");
        using var parsed = System.Text.Json.JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("paths").GetProperty("/api/exercise/media/upload").GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data")
            .GetProperty("schema").GetProperty("required").EnumerateArray().Select(x => x.GetString())
            .Should().Equal("file");
        var output = Environment.GetEnvironmentVariable("LGYM_OPENAPI_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output, json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
        }
    }
}
