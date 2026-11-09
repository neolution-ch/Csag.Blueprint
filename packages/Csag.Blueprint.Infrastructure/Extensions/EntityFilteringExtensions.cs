namespace Csag.Blueprint.Infrastructure.Extensions;

using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Domain.Time;

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
    /// Filters the query to include only entities that are active at the current instant of the clock.
    /// An entity is considered active if:
    /// - ActiveFrom is null or less than or equal to current time
    /// - ActiveUntil is null or greater than current time
    /// </summary>
    /// <typeparam name="T">The entity type that implements <see cref="IHasActiveRange"/>.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <param name="timeProvider">The clock that says what "now" is.</param>
    /// <returns>A filtered query that includes only currently active entities.</returns>
    public static IQueryable<T> WhereActiveNow<T>(this IQueryable<T> query, TimeProvider timeProvider)
        where T : class, IHasActiveRange
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return query.WhereActiveAt(timeProvider.GetUtcNow());
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
    /// Filters the query to include only entities that are active at the start of today in
    /// <paramref name="timeZone"/>, not at the current instant. Use this for day-granular validity such as
    /// "valid on today's date"; use <see cref="WhereActiveNow{T}"/> when the exact instant matters.
    /// <para>
    /// Today is the date a clock in the zone shows now (<see cref="TimeZones.Today"/>), and its start is the
    /// first instant of that date in the zone (<see cref="TimeZones.DayRange"/>), not midnight UTC. Because
    /// the comparison happens at the start of the day, an entity whose <c>ActiveFrom</c> falls later today is
    /// <b>not</b> active for this query — use <see cref="WhereActiveNow{T}"/> or
    /// <see cref="WhereActiveAt{T}"/> if it should be.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The entity type that implements <see cref="IHasActiveRange"/>.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <param name="timeProvider">The clock that says what "now" is.</param>
    /// <param name="timeZone">The zone whose calendar day counts, normally the tenant's.</param>
    /// <returns>A filtered query that includes only entities active at the start of today in the zone.</returns>
    public static IQueryable<T> WhereActiveToday<T>(this IQueryable<T> query, TimeProvider timeProvider, TimeZoneInfo timeZone)
        where T : class, IHasActiveRange
    {
        var (todayStart, _) = TimeZones.DayRange(TimeZones.Today(timeProvider, timeZone), timeZone);
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
    /// Filters the query to include only entities that are inactive at the current instant of the clock.
    /// An entity is considered inactive if:
    /// - ActiveFrom is greater than current time, OR
    /// - ActiveUntil is less than or equal to current time
    /// </summary>
    /// <typeparam name="T">The entity type that implements <see cref="IHasActiveRange"/>.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <param name="timeProvider">The clock that says what "now" is.</param>
    /// <returns>A filtered query that includes only currently inactive entities.</returns>
    public static IQueryable<T> WhereInactiveNow<T>(this IQueryable<T> query, TimeProvider timeProvider)
        where T : class, IHasActiveRange
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var now = timeProvider.GetUtcNow();
        return query.Where(x => (x.ActiveFrom != null && x.ActiveFrom > now) ||
            (x.ActiveUntil != null && x.ActiveUntil <= now));
    }

    /// <summary>
    /// Combines soft deletion and active range filtering to get entities that are not deleted and currently active.
    /// This is useful for entities that implement both <see cref="ISoftDeletable"/> and <see cref="IHasActiveRange"/>.
    /// </summary>
    /// <typeparam name="T">The entity type that implements both <see cref="ISoftDeletable"/> and <see cref="IHasActiveRange"/>.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <param name="timeProvider">The clock that says what "now" is.</param>
    /// <returns>A filtered query that includes only non-deleted and currently active entities.</returns>
    public static IQueryable<T> WhereActiveAndNotDeleted<T>(this IQueryable<T> query, TimeProvider timeProvider)
        where T : class, ISoftDeletable, IHasActiveRange
    {
        return query.WhereNotDeleted().WhereActiveNow(timeProvider);
    }
}
