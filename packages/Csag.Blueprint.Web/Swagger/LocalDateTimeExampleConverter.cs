namespace Csag.Blueprint.Web.Swagger;

using Csag.Blueprint.Application.Json;
using Newtonsoft.Json;

/// <summary>
/// Writes <see cref="DateTime"/> examples in the OpenAPI document as local readings
/// (<c>yyyy-MM-ddTHH:mm:ss</c>, no offset), the way the API's System.Text.Json converter writes them.
/// NSwag serializes examples with Newtonsoft.Json, which would otherwise append the offset of the
/// value's <see cref="DateTime.Kind"/>.
/// </summary>
internal sealed class LocalDateTimeExampleConverter : JsonConverter
{
    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanConvert(Type objectType) => objectType == typeof(DateTime) || objectType == typeof(DateTime?);

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is DateTime reading)
        {
            writer.WriteValue(LocalDateTimeFormat.Format(reading));
        }
        else
        {
            writer.WriteNull();
        }
    }

    /// <inheritdoc/>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        throw new NotSupportedException("Only writes examples into the OpenAPI document.");
    }
}
