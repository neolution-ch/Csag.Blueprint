namespace Csag.Blueprint.Web.Extensions;

using Audit.EntityFramework.ConfigurationApi;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Options for configuring blueprint audit logging.
/// Allows applications to add entity-specific field exclusions and entity ignores
/// on top of the standard blueprint audit configuration.
/// </summary>
/// <typeparam name="TContext">The application's DbContext type.</typeparam>
public sealed class BlueprintAuditOptions<TContext>
    where TContext : DbContext
{
    /// <summary>
    /// Gets or sets an optional callback to configure app-specific entity field exclusions.
    /// Called within the <c>ForContext</c> configurator after standard blueprint field
    /// exclusions (PasswordHash, SecurityStamp, etc.) are applied.
    /// </summary>
    /// <example>
    /// <code>
    /// options.EntityFieldConfigurator = config => config
    ///     .ForEntity&lt;PedaloImage&gt;(entity => entity.Ignore(i => i.ImageData));
    /// </code>
    /// </example>
    public Action<IContextSettingsConfigurator<TContext>>? EntityFieldConfigurator { get; set; }
}
