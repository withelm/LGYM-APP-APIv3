using LgymApi.Api.Features.Exercise.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LgymApi.Api.Serialization;

public sealed class ExerciseMediaOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(ExerciseMediaController) || context.MethodInfo.Name == nameof(ExerciseMediaController.CleanupExerciseMedia)) return;
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Exercise-Media-Operation-Id", In = ParameterLocation.Header, Required = true,
            Description = "Same UUID as multipart operationId. Required before body reception for safely fenced definitive rejections.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" }
        });
        operation.Description = "Authenticated bounded multipart upload through the API; never direct client-to-R2. "
            + "Send original file plus sourceSha256/contentType/sizeBytes, or no media manifest for a no-media create. "
            + "Retry the exact scalar fields and manifest with the same operationId; a known operation can replay without file. "
            + "428 sourceRequired needs the matching original file, never a new create or silent no-media downgrade. "
            + "Only an error with matching operationId and disposition=rejected proves the operation is durably fenced against creation. "
            + "Unresolved/network/503 and ordinary409conflict retain the receipt. A permanently retired never-bound key may return409rejected. "
            + "For media changes expectedImage is a required form field: send an empty value for explicit expected null. "
            + "Choose file+manifest, remove=true or recorded restoreImage, exactly one. Max file5242880 bytes, request5308416 bytes.";
        if (operation.RequestBody?.Content is { } content && content.TryGetValue("multipart/form-data", out var form) && form.Schema is OpenApiSchema schema)
        {
            schema.Required ??= new HashSet<string>();
            schema.Required.Add("operationId");
            if (context.MethodInfo.Name == nameof(ExerciseMediaController.CreateExerciseWithMedia)) schema.Required.Add("name");
            if (context.MethodInfo.Name == nameof(ExerciseMediaController.ChangeExerciseMedia))
            {
                schema.Required.Add("expectedImage");
                if (schema.Properties is { } properties && properties.TryGetValue("expectedImage", out var expectedSchema) && expectedSchema is OpenApiSchema expectedImage)
                    expectedImage.Description = "Required form field. Send an empty string for expected null; omission is invalid.";
            }
        }
    }
}
