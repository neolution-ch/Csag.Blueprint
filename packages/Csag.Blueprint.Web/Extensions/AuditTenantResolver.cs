namespace Csag.Blueprint.Web.Extensions;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Decides which tenant an entity-change audit event is recorded under: the value of its <c>TenantId</c>
/// column, which is what a tenant-scoped audit read filters on.
/// </summary>
/// <param name="httpContext">The request the change was saved in, or <see langword="null"/> outside a request.</param>
/// <param name="activeTenantId">
/// The ambient tenant from <c>TenantContext.Current</c>, which is what is recorded when no resolver is set.
/// </param>
/// <returns>The tenant to record the event under, or <see langword="null"/> to record it outside every tenant.</returns>
public delegate Guid? AuditTenantResolver(HttpContext? httpContext, Guid? activeTenantId);
