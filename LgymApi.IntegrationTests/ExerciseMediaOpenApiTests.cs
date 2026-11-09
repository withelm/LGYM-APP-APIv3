using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class ExerciseMediaOpenApiTests : IntegrationTestBase
{
    [Test]
    public void CanonicalContract_DeclaresMediaLifecycleAndRequiredNullablePrecondition()
    {
        var document = Factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        using var text = new StringWriter();
        document.SerializeAsV3(new OpenApiJsonWriter(text));
        var json = text.ToString();
        json.Should().Contain("/api/exercise/media/uploads").And.Contain("/api/exercise/media/create").And.Contain("expectedImage");
        using var parsed = System.Text.Json.JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("ChangeExerciseMediaDto").GetProperty("required").EnumerateArray().Select(x => x.GetString()).Should().Contain("expectedImage");
        var output = Environment.GetEnvironmentVariable("LGYM_OPENAPI_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output, json + Environment.NewLine);
        }
    }
}
