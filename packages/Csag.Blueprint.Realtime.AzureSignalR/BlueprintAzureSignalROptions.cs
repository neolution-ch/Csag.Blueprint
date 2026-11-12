namespace Csag.Blueprint.Realtime.AzureSignalR;

/// <summary>
/// Configuration for the Azure SignalR Service provider of the real-time channel.
/// </summary>
public sealed class BlueprintAzureSignalROptions
{
    /// <summary>
    /// Gets or sets the Azure SignalR Service connection string. It must carry an <c>Endpoint</c> and an
    /// <c>AccessKey</c>.
    /// </summary>
    /// <remarks>
    /// Against the local emulator this needs a <c>ClientEndpoint</c> in addition to <c>Endpoint</c> when
    /// the API and the browser reach the emulator under different hostnames, for example the compose
    /// network and localhost.
    /// </remarks>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the hub name clients connect to.
    /// </summary>
    public string HubName { get; set; } = "app";

    /// <summary>
    /// Gets or sets how long an issued connection ticket stays valid, in minutes.
    /// </summary>
    public int TicketTokenLifetimeMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets how long a granted group membership survives, in hours.
    /// </summary>
    /// <remarks>
    /// Must be greater than zero and outlast the ticket. The service reads a zero time-to-live as "only
    /// the connections this user has open right now", and at ticket-issuing time that is none, so the
    /// channel would come up and deliver nothing. The service holds no membership longer than a year.
    /// </remarks>
    public int GroupMembershipTtlHours { get; set; } = 12;
}
