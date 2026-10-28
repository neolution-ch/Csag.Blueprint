namespace Csag.Blueprint.Web.TableView;

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
    /// Finds the zone a request names. The field normally carries the viewer's browser zone, and browsers can
    /// know zones this host's time zone data does not yet, so a well-formed id the host cannot resolve is not
    /// an error: the request is evaluated in the fallback zone, and a warning is logged.
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
            logger.LogWarning(
                "The table view request names the time zone {TimeZone}, which this host's time zone data does not know; the tenant's zone is used instead.",
                id);
        }

        return true;
    }
}
