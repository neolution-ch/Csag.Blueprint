namespace Csag.Blueprint.Web.Swagger;

using NJsonSchema;
using NJsonSchema.Generation;

/// <summary>
/// Schema processor that describes the wire formats of <see cref="DateTime"/>, <see cref="TimeOnly"/> and
/// <see cref="TimeSpan"/> as System.Text.Json writes them with the Blueprint conventions, instead of
/// NJsonSchema's defaults.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="DateTime"/> is a local reading without an offset
/// (<see cref="Application.Json.LocalDateTimeJsonConverter"/>), so its schema is
/// <c>format: date-time-local</c> with the pattern of <c>yyyy-MM-ddTHH:mm[:ss]</c> rather than
/// <c>format: date-time</c>, which promises an RFC 3339 timestamp with an offset.
/// </para>
/// <para>
/// A <see cref="TimeOnly"/> is a time of day without an offset, written as <c>HH:mm:ss[.fffffff]</c>, so its
/// schema is <c>format: time-local</c> with that pattern rather than <c>format: time</c>, which promises an
/// RFC 3339 <c>full-time</c> with an offset (<c>09:00:00Z</c>).
/// </para>
/// <para>
/// <c>date-time-local</c> and <c>time-local</c> are the names the
/// <see href="https://spec.openapis.org/registry/format/">OpenAPI Format Registry</see> gives RFC 3339
/// date-times and times without the time zone component.
/// </para>
/// <para>
/// A <see cref="TimeSpan"/> is written in the constant ("c") format <c>[-][d.]hh:mm:ss[.fffffff]</c>,
/// so its schema carries that pattern and no <c>format: duration</c>, which promises ISO 8601 (<c>PT2H30M</c>).
/// </para>
/// </remarks>
public sealed class TemporalSchemaProcessor : ISchemaProcessor
{
    /// <summary>
    /// The OpenAPI format of a local reading.
    /// </summary>
    private const string LocalDateTimeFormat = "date-time-local";

    /// <summary>
    /// The pattern of a local reading: <c>yyyy-MM-ddTHH:mm</c> with optional seconds.
    /// </summary>
    private const string LocalDateTimePattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2})?$";

    /// <summary>
    /// The OpenAPI format of a time of day without an offset.
    /// </summary>
    private const string LocalTimeFormat = "time-local";

    /// <summary>
    /// The pattern of a <see cref="TimeOnly"/>: <c>HH:mm:ss</c> with an optional fraction of up to seven digits.
    /// </summary>
    private const string LocalTimePattern = @"^\d{2}:\d{2}:\d{2}(\.\d{1,7})?$";

    /// <summary>
    /// The pattern of a <see cref="TimeSpan"/> in the constant ("c") format.
    /// </summary>
    private const string TimeSpanPattern = @"^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$";

    /// <inheritdoc/>
    public void Process(SchemaProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var type = context.ContextualType.Type;
        if (type == typeof(DateTime))
        {
            context.Schema.Format = LocalDateTimeFormat;
            context.Schema.Pattern = LocalDateTimePattern;
        }
        else if (type == typeof(TimeOnly))
        {
            context.Schema.Format = LocalTimeFormat;
            context.Schema.Pattern = LocalTimePattern;
        }
        else if (type == typeof(TimeSpan))
        {
            context.Schema.Format = null;
            context.Schema.Pattern = TimeSpanPattern;
        }
        else
        {
            // Every other type keeps the schema NJsonSchema generated.
        }
    }
}
