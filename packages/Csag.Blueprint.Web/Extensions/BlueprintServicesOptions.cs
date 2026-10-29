namespace Csag.Blueprint.Web.Extensions;

using System.Reflection;
using Microsoft.AspNetCore.Builder;
using NSwag.Generation.AspNetCore;

/// <summary>
/// Optional configuration for <see cref="BlueprintBuilderExtensions.AddBlueprintServices(WebApplicationBuilder, Action{BlueprintServicesOptions})"/>.
/// Add each new customization hook here, as a property. This keeps the aggregate registration
/// method's signature single and stable.
/// </summary>
public sealed class BlueprintServicesOptions
{
    /// <summary>
    /// Gets or sets extra assemblies to scan for FastEndpoints endpoints and command/event handlers.
    /// Use this for handlers outside the entry assembly. Pass the marker type's assembly for each
    /// project, for example <c>typeof(MyMarker).Assembly</c>. The entry assembly stays in scope.
    /// </summary>
    public IList<Assembly> FastEndpointsAdditionalAssemblies { get; set; } = [];

    /// <summary>
    /// Gets or sets an optional callback that applies extra OpenAPI document settings after the
    /// Blueprint defaults (for example registering additional
    /// <see cref="NSwag.Generation.Processors.IDocumentProcessor"/> instances).
    /// </summary>
    public Action<AspNetCoreOpenApiDocumentGeneratorSettings>? ConfigureOpenApiDocument { get; set; }
}
