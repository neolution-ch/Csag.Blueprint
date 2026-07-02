namespace Csag.Blueprint.Application.Json;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Single source of truth for the blueprint's JSON serialization conventions.
/// Used by the API host (FastEndpoints) and any persistence layer that round-trips
/// blueprint models through JSON, so the contract stays consistent in both directions.
/// </summary>
public static class BlueprintJsonOptions
{
    /// <summary>
    /// Gets a pre-built <see cref="JsonSerializerOptions"/> instance configured per
    /// <see cref="Configure(JsonSerializerOptions)"/>. Cache this — do not allocate per call.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = CreateDefault();

    /// <summary>
    /// Applies the blueprint's JSON conventions to an existing options instance:
    /// camelCase property names and string-form enum values (case-insensitive on read,
    /// PascalCase on write — matching the wider .NET enum convention).
    /// </summary>
    /// <param name="options">The options to configure.</param>
    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter());
    }

    private static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions();
        Configure(options);
        return options;
    }
}
