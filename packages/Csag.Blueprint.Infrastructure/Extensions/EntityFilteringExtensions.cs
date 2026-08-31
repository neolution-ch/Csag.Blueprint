namespace Csag.Blueprint.Infrastructure.Extensions;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Extension methods for filtering entities that implement soft deletion and active range contracts.
/// These extensions provide convenient methods for querying entities based on their deletion status
/// and active date ranges.
/// </summary>
public static class EntityFilteringExtensions
{
    /// <summary>
    /// Filters the query to include only entities that are not soft deleted.
    /// </summary>
    /// <typeparam name="T">The entity type that implements <see cref="ISoftDeletable"/>.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <returns>A filtered query that excludes soft deleted entities.</returns>
    public static IQueryable<T> WhereNotDeleted<T>(this IQueryable<T> query)
        where T : class, ISoftDeletable
    {
        return query.Where(x => x.DeletedAt == null);
    }

    /// <summary>
    /// Filters the query to include only entities that are soft deleted.
    /// </summary>
    /// <typeparam name="T">The entity type that implements <see cref="ISoftDeletable"/>.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <returns>A filtered query that includes only soft deleted entities.</returns>
    public static IQueryable<T> WhereDeleted<T>(this IQueryable<T> query)
        where T : class, ISoftDeletable
    {
        return query.Where(x => x.DeletedAt != null);
    }

    /// <summary>
    /// Filters the query to include only entities that were soft deleted before the specified date.
    /// </summary>
    /// <typeparam name="T">The entity type that implements <see cref="ISoftDeletable"/>.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <param name="before">The date before which entities should be deleted.</param>
    /// <returns>A filtered query that includes only entities deleted before the specified date.</returns>
    public static IQueryable<T> WhereDeletedBefore<T>(this IQueryable<T> query, DateTimeOffset before)
        where T : class, ISoftDeletable
    {
        return query.Where(x => x.DeletedAt != null && x.DeletedAt < before);
    }

    /// <summary>
    /// Filters the query to include only entities that are active at the current UTC time.
    /// An entity is considered active if:
    /// - ActiveFrom is null or less than or equal to current time
    /// - ActiveUntil is null or greater than current time
    /// </summary>
    /// <typeparam name="T">The entity type that implements <see cref="IHasActiveRange"/>.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <returns>A filtered query that includes only currently active entities.</returns>
    public static IQueryable<T> WhereActiveNow<T>(this IQueryable<T> query)
        where T : class, IHasActiveRange
    {
        var now = DateTimeOffset.UtcNow;
        return query.WhereActiveAt(now);
    }

    /// <summary>
    /// Filters the query to include only entities that are active at the specified time.
    /// An entity is considered active if:
    /// - ActiveFrom is null or less than or equal to the specified time
    /// - ActiveUntil is null or greater than the specified time
    /// </summary>
    /// <typeparam name="T">The entity type that implements <see cref="IHasActiveRange"/>.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <param name="at">The timestamp at which to check if entities are active.</param>
    /// <returns>A filtered query that includes only entities active at the specified time.</returns>
    public static IQueryable<T> WhereActiveAt<T>(this IQueryable<T> query, DateTimeOffset at)
        where T : class, IHasActiveRange
    {
        return query.Where(x => (x.ActiveFrom == null || x.ActiveFrom <= at) &&
            (x.ActiveUntil == null || x.ActiveUntil > at));
    }

    /// <summary>
    /// Filters the query to include only entities that are active today (based on UTC date).
    /// An entity is considered active today if it's active at the start of the current UTC day.
    /// </summary>
    /// <typeparam name="T">The entity type that implements <see cref="IHasActiveRange"/>.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <returns>A filtered query that includes only entities active today.</returns>
    public static IQueryable<T> WhereActiveToday<T>(this IQueryable<T> query)
        where T : class, IHasActiveRange
    {
        var today = DateTimeOffset.UtcNow.Date;
        var todayStart = new DateTimeOffset(today, TimeSpan.Zero);
        return query.WhereActiveAt(todayStart);
    }

    /// <summary>
    /// Filters the query to include only entities that are active within the specified date range.
    /// An entity is considered active in the range if its active period overlaps with the specified range.
    /// </summary>
    /// <typeparam name="T">The entity type that implements <see cref="IHasActiveRange"/>.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <param name="rangeStart">The start of the date range to check.</param>
    /// <param name="rangeEnd">The end of the date range to check.</param>
    /// <returns>A filtered query that includes only entities active within the specified range.</returns>
    public static IQueryable<T> WhereActiveInRange<T>(this IQueryable<T> query, DateTimeOffset rangeStart, DateTimeOffset rangeEnd)
        where T : class, IHasActiveRange
    {
        return query.Where(x => (x.ActiveFrom == null || x.ActiveFrom < rangeEnd) &&
            (x.ActiveUntil == null || x.ActiveUntil > rangeStart));
    }

    /// <summary>
    /// Filters the query to include only entities that are currently inactive.
    /// An entity is considered inactive if:
    /// - ActiveFrom is greater than current time, OR
    /// - ActiveUntil is less than or equal to current time
    /// </summary>
    /// <typeparam name="T">The entity type that implements <see cref="IHasActiveRange"/>.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <returns>A filtered query that includes only currently inactive entities.</returns>
    public static IQueryable<T> WhereInactiveNow<T>(this IQueryable<T> query)
        where T : class, IHasActiveRange
    {
        var now = DateTimeOffset.UtcNow;
        return query.Where(x => (x.ActiveFrom != null && x.ActiveFrom > now) ||
            (x.ActiveUntil != null && x.ActiveUntil <= now));
    }

    /// <summary>
    /// Combines soft deletion and active range filtering to get entities that are not deleted and currently active.
    /// This is useful for entities that implement both <see cref="ISoftDeletable"/> and <see cref="IHasActiveRange"/>.
    /// </summary>
    /// <typeparam name="T">The entity type that implements both <see cref="ISoftDeletable"/> and <see cref="IHasActiveRange"/>.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <returns>A filtered query that includes only non-deleted and currently active entities.</returns>
    public static IQueryable<T> WhereActiveAndNotDeleted<T>(this IQueryable<T> query)
        where T : class, ISoftDeletable, IHasActiveRange
    {
        return query.WhereNotDeleted().WhereActiveNow();
    }
}
