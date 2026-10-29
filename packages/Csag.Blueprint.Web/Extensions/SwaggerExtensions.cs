namespace Csag.Blueprint.Web.Extensions;

using System.Text.Json.Serialization;
using Csag.Blueprint.Web.Swagger;
using FastEndpoints.Swagger;
using Microsoft.Extensions.DependencyInjection;
using NJsonSchema;
using NJsonSchema.Generation.TypeMappers;
using NSwag.Generation.AspNetCore;

/// <summary>
/// Extension methods for Swagger/OpenAPI documentation registration.
/// </summary>
public static class SwaggerExtensions
{
    /// <summary>
    /// Adds Swagger documentation with FastEndpoints configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureAdditionalDocumentSettings">
    /// Optional callback to apply extra document settings after the Blueprint defaults (for example registering
    /// additional <see cref="NSwag.Generation.Processors.IDocumentProcessor"/> instances).
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSwaggerDocumentation(
        this IServiceCollection services,
        Action<AspNetCoreOpenApiDocumentGeneratorSettings>? configureAdditionalDocumentSettings = null)
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

                configureAdditionalDocumentSettings?.Invoke(s);
            };

            o.SerializerSettings = s => s.Converters.Add(new JsonStringEnumConverter());
        });

        return services;
    }
}
