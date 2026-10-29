namespace Csag.Blueprint.Web.TableView;

using System.Collections.Concurrent;
using Csag.Blueprint.Domain.Time;
using Microsoft.Extensions.Logging;

/// <summary>
/// Reads the optional <c>timeZone</c> field of table view requests, as the table view base endpoints do; an
/// endpoint that handles <see cref="Application.TableView.TableViewDataRequest"/> or
/// <see cref="Application.TableView.TableViewExportRequest"/> itself applies the same rule with it.
/// </summary>
public static class TableViewRequestTimeZone
{
    /// <summary>
    /// The validation message for a <c>timeZone</c> that is not an IANA id.
    /// </summary>
    public static readonly string InvalidMessage = "TimeZone must be an IANA time zone id, such as Europe/Zurich.";

    /// <summary>
    /// How many distinct unknown ids are logged as warnings. A browser ahead of the server sends its unknown
    /// zone on every request; the warning is worth seeing once per id, and the remembered ids stay bounded
    /// whatever clients send.
    /// </summary>
    private const int MaxWarnedUnknownIds = 100;

    private static readonly ConcurrentDictionary<string, bool> WarnedUnknownIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Finds the zone a request names. The field normally carries the viewer's browser zone, and browsers can
    /// know zones this host's time zone data does not yet, so a well-formed id the host cannot resolve is not
    /// an error: the request is evaluated in the fallback zone, and a warning is logged, once per id and
    /// process (at debug level after that, and for every id once 100 distinct ones have been warned about).
    /// </summary>
    /// <param name="id">The request's <c>timeZone</c> value.</param>
    /// <param name="logger">Records an id the host cannot resolve.</param>
    /// <param name="zone">The zone, or null when the request names none or one the host does not know.</param>
    /// <returns><see langword="false"/> when the value does not have the form of an IANA id.</returns>
    public static bool TryFindRequested(string? id, ILogger logger, out TimeZoneInfo? zone)
    {
        ArgumentNullException.ThrowIfNull(logger);

        zone = null;
        if (id is null)
        {
            return true;
        }

        if (!ZonedTime.IsWellFormedIanaTimeZoneId(id))
        {
            return false;
        }

        if (!ZonedTime.TryFindIanaTimeZone(id, out zone))
        {
            LogUnknown(id, logger);
        }

        return true;
    }

    private static void LogUnknown(string id, ILogger logger)
    {
        // The count check and the add are not atomic, so concurrent first requests can overshoot the limit
        // by a few ids; it still bounds the set.
        if (WarnedUnknownIds.Count < MaxWarnedUnknownIds && WarnedUnknownIds.TryAdd(id, true))
        {
            logger.LogWarning(
                "The table view request names the time zone {TimeZone}, which this host's time zone data does not know; the tenant's zone is used instead.",
                id);
        }
        else
        {
            logger.LogDebug(
                "The table view request names the unknown time zone {TimeZone}; the tenant's zone is used instead.",
                id);
        }
    }
}
