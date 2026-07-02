namespace Csag.Blueprint.Web.Extensions;

using System.Text.Json.Serialization;
using Csag.Blueprint.Web.Swagger;
using FastEndpoints.Swagger;
using Microsoft.Extensions.DependencyInjection;
using NJsonSchema;
using NJsonSchema.Generation.TypeMappers;

/// <summary>
/// Extension methods for Swagger/OpenAPI documentation registration.
/// </summary>
public static class SwaggerExtensions
{
    /// <summary>
    /// Adds Swagger documentation with FastEndpoints configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.SwaggerDocument(o =>
        {
            o.AutoTagPathSegmentIndex = 1;
            o.ShortSchemaNames = true;
            o.DocumentSettings = s =>
            {
                s.MarkNonNullablePropsAsRequired();
                s.SchemaSettings.SchemaProcessors.Add(new RequiredStringMinLengthProcessor());
                s.OperationProcessors.Add(new ProblemDetailsOperationProcessor());
                s.DocumentProcessors.Add(new UnifiedProblemDetailsDocumentProcessor());

                // NSwag does not natively support sbyte and falls back to "type": "object".
                // Map it to integer with format "int8" so generated clients get the correct type.
                s.SchemaSettings.TypeMappers.Add(
                    new PrimitiveTypeMapper(typeof(sbyte), schema =>
                    {
                        schema.Type = JsonObjectType.Integer;
                        schema.Format = "int8";
                    }));
            };

            o.SerializerSettings = s => s.Converters.Add(new JsonStringEnumConverter());
        });

        return services;
    }
}
