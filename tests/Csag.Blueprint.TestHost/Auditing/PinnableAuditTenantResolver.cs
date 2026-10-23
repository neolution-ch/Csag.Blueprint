namespace Csag.Blueprint.TestHost.Auditing;

using Csag.Blueprint.Web.Extensions;

/// <summary>
/// The host's <see cref="BlueprintAuditOptions{TContext}.EntityChangeTenantResolver"/>: it records the active
/// tenant, as the package does with no resolver set, until a test pins a resolver of its own.
/// </summary>
/// <remarks>
/// <c>ConfigureBlueprintAuditLogging</c> writes Audit.NET's process-wide configuration once at startup, so a test
/// cannot hand it a different resolver later. It pins one here instead, the same way
/// <see cref="Time.PinnableTimeProvider"/> drives the audit clock.
/// </remarks>
public sealed class PinnableAuditTenantResolver
{
    private AuditTenantResolver? pinnedResolver;

    /// <summary>
    /// Resolves the tenant an entity-change audit event is recorded under.
    /// </summary>
    /// <param name="httpContext">The request the change was saved in, or <see langword="null"/> outside a request.</param>
    /// <param name="activeTenantId">The ambient tenant.</param>
    /// <returns>The pinned resolver's answer, or <paramref name="activeTenantId"/> while none is pinned.</returns>
    public Guid? Resolve(HttpContext? httpContext, Guid? activeTenantId) =>
        this.pinnedResolver is { } resolver ? resolver(httpContext, activeTenantId) : activeTenantId;

    /// <summary>
    /// Answers every resolution with <paramref name="resolver"/> until the returned scope is disposed.
    /// </summary>
    /// <param name="resolver">The resolver to use while the scope is held.</param>
    /// <returns>A scope that releases the pin when disposed.</returns>
    public IDisposable Pin(AuditTenantResolver resolver)
    {
        this.pinnedResolver = resolver;
        return new PinScope(this);
    }

    private sealed class PinScope(PinnableAuditTenantResolver owner) : IDisposable
    {
        public void Dispose() => owner.pinnedResolver = null;
    }
}
