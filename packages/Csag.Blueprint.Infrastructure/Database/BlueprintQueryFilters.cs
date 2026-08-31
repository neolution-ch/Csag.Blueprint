namespace Csag.Blueprint.Infrastructure.Database;

/// <summary>
/// Well-known keys for the named global query filters applied by the Blueprint packages.
/// <para>
/// EF Core 10 supports multiple named filters per entity, which means a query can opt out of a
/// single filter instead of calling <c>IgnoreQueryFilters()</c> and losing every filter at once.
/// Use the extension methods in <c>Csag.Blueprint.Infrastructure.Extensions.QueryFilterExtensions</c>
/// rather than passing these keys around by hand.
/// </para>
/// </summary>
public static class BlueprintQueryFilters
{
    /// <summary>
    /// Gets the key of the tenant isolation filter applied to every <c>IMustHaveTenant</c> entity.
    /// <para>
    /// This filter is the multi-tenancy security boundary and is deliberately not exposed through a
    /// convenience opt-out extension. Bypassing it requires an explicit
    /// <c>IgnoreQueryFilters([BlueprintQueryFilters.Tenant])</c> call at the call site.
    /// </para>
    /// </summary>
    public static string Tenant => "Tenant";

    /// <summary>
    /// Gets the key of the soft deletion filter applied to every <c>ISoftDeletable</c> entity.
    /// Opt out with <c>IgnoreSoftDeleteFilter()</c> to query deleted rows while keeping tenant isolation.
    /// </summary>
    public static string SoftDelete => "SoftDelete";
}
