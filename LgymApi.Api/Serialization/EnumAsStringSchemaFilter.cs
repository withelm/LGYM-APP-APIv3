using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LgymApi.Api.Serialization;

public sealed class EnumAsStringSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        var enumType = Nullable.GetUnderlyingType(context.Type) ?? context.Type;
        if (!enumType.IsEnum)
        {
            return;
        }

        if (schema is not OpenApiSchema openApiSchema)
        {
            return;
        }

        openApiSchema.Type = JsonSchemaType.String;
        openApiSchema.Format = null;
        openApiSchema.Enum = Enum.GetNames(enumType).Select(x => JsonValue.Create(x)!).ToList<JsonNode>();
    }
}
