namespace Csag.Blueprint.Application.Abstractions.Realtime;

/// <summary>
/// The endpoint and short-lived credential a browser needs to open its real-time connection.
/// </summary>
public sealed class RealtimeTicket
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RealtimeTicket"/> class.
    /// </summary>
    /// <param name="url">The absolute client URL to connect to.</param>
    /// <param name="accessToken">The short-lived access token authorizing the connection.</param>
    /// <param name="expiresAt">The instant the access token stops being accepted.</param>
    public RealtimeTicket(Uri url, string accessToken, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        this.Url = url;
        this.AccessToken = accessToken;
        this.ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Gets the absolute client URL to connect to. The browser talks to this host directly and
    /// never routes its connection through the API.
    /// </summary>
    public Uri Url { get; }

    /// <summary>
    /// Gets the short-lived access token authorizing the connection.
    /// </summary>
    public string AccessToken { get; }

    /// <summary>
    /// Gets the instant the access token stops being accepted.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }
}
