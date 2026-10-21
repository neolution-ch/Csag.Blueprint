namespace Csag.Blueprint.Web.TableView;

using Csag.Blueprint.Domain.Time;

/// <summary>
/// Reads the optional <c>timeZone</c> field of table view requests.
/// </summary>
internal static class TableViewRequestTimeZone
{
    /// <summary>
    /// The validation message for a <c>timeZone</c> that is not an IANA id.
    /// </summary>
    public const string InvalidMessage = "TimeZone must be an IANA time zone id, such as Europe/Zurich.";

    /// <summary>
    /// Finds the zone a request names.
    /// </summary>
    /// <param name="id">The request's <c>timeZone</c> value.</param>
    /// <param name="zone">The zone, or null when the request names none.</param>
    /// <returns><see langword="false"/> when the request names a zone that is not a resolvable IANA id.</returns>
    public static bool TryFindRequested(string? id, out TimeZoneInfo? zone)
    {
        zone = null;
        return id is null || ZonedTime.TryFindIanaTimeZone(id, out zone);
    }
}
