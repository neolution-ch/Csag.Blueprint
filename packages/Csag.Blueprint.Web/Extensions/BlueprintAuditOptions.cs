namespace Csag.Blueprint.Web.Extensions;

using Audit.EntityFramework.ConfigurationApi;
using Csag.Blueprint.Web.Middleware;
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
    internal BlueprintAuditOptions(HttpAuditOptions httpAudit)
    {
        this.HttpAudit = httpAudit;
    }

    /// <summary>
    /// Gets or sets an optional callback to configure app-specific entity field exclusions.
    /// Called within the <c>ForContext</c> configurator after standard blueprint field
    /// exclusions (PasswordHash, SecurityStamp, etc.) are applied.
    /// </summary>
    /// <example>
    /// <code>
    /// options.EntityFieldConfigurator = config => config
    ///     .ForEntity&lt;InvoiceDocument&gt;(entity => entity.Ignore(i => i.FileData));
    /// </code>
    /// </example>
    public Action<IContextSettingsConfigurator<TContext>>? EntityFieldConfigurator { get; set; }

    /// <summary>
    /// Gets the configuration for <c>HttpAuditMiddleware</c>. This is the same instance the
    /// middleware reads on every request, so a change here takes effect immediately. Before this
    /// call runs the configure callback, it sets <see cref="HttpAuditOptions.Enabled"/> to
    /// <see langword="true"/>. Set <c>HttpAudit.Enabled</c> back to <see langword="false"/> in the
    /// callback to turn HTTP request auditing off.
    /// </summary>
    public HttpAuditOptions HttpAudit { get; }
}
