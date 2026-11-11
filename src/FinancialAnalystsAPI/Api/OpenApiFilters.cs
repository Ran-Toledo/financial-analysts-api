using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FinancialAnalystsAPI.Api;

/// <summary>
/// Marks every non-nullable property as required. Without it, generated clients (the dashboard uses openapi-typescript)
/// type every field as optional.
/// </summary>
internal sealed class RequireNonNullablePropertiesFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        foreach (var (name, property) in schema.Properties)
            if (!property.Nullable) schema.Required.Add(name);
    }
}
