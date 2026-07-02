namespace Csag.Blueprint.Web.Swagger;

using NJsonSchema;
using NJsonSchema.Generation;

/// <summary>
/// Schema processor that enforces a minimum length of 1 on required string properties.
/// </summary>
public class RequiredStringMinLengthProcessor : ISchemaProcessor
{
    /// <inheritdoc/>
    public void Process(SchemaProcessorContext context)
    {
        if (context.Schema.Properties == null)
        {
            return;
        }

        foreach (var property in context.Schema.Properties.Values)
        {
            bool isString = property.Type.HasFlag(JsonObjectType.String);

            if (isString && property.IsRequired && (property.MinLength == null || property.MinLength == 0))
            {
                property.MinLength = 1;
            }
        }
    }
}
