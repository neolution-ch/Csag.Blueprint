namespace Csag.Blueprint.Infrastructure.Extensions;

using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for selectively opting out of the named global query filters applied by the
/// Blueprint packages.
/// <para>
/// Prefer these over <see cref="EntityFrameworkQueryableExtensions.IgnoreQueryFilters{TEntity}(IQueryable{TEntity})"/>,
/// which disables <b>every</b> filter on the entity — including tenant isolation — and therefore
/// forces each call site to re-implement the tenant predicate by hand.
/// </para>
/// </summary>
public static class QueryFilterExtensions
{
    private static readonly string[] SoftDeleteFilterKey = [BlueprintQueryFilters.SoftDelete];

    /// <summary>
    /// Disables only the soft deletion global query filter, so the query sees both deleted and
    /// non-deleted rows. Tenant isolation and any other named filter stay in effect.
    /// </summary>
    /// <typeparam name="T">The entity type that implements <see cref="ISoftDeletable"/>.</typeparam>
    /// <param name="query">The query to modify.</param>
    /// <returns>A query that no longer excludes soft deleted entities.</returns>
    public static IQueryable<T> IgnoreSoftDeleteFilter<T>(this IQueryable<T> query)
        where T : class, ISoftDeletable
    {
        return query.IgnoreQueryFilters(SoftDeleteFilterKey);
    }

    /// <summary>
    /// Disables the soft deletion global query filter and keeps only the soft deleted rows.
    /// Tenant isolation and any other named filter stay in effect.
    /// </summary>
    /// <typeparam name="T">The entity type that implements <see cref="ISoftDeletable"/>.</typeparam>
    /// <param name="query">The query to modify.</param>
    /// <returns>A query over soft deleted entities only.</returns>
    public static IQueryable<T> OnlySoftDeleted<T>(this IQueryable<T> query)
        where T : class, ISoftDeletable
    {
        return query.IgnoreSoftDeleteFilter().WhereDeleted();
    }
}
