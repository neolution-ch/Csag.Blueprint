namespace Csag.Blueprint.Realtime.AzureSignalR;

using Csag.Blueprint.Application.Abstractions.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Publishes notifications through Azure SignalR Service and issues client connection tickets.
/// </summary>
/// <remarks>
/// The service runs in serverless mode, so this process holds no connection to it. Publishing is an
/// ordinary outbound HTTPS request made while a request is being handled, which is what lets the
/// API keep scaling to zero: there is no long-lived socket to starve when the instance is
/// CPU-throttled between requests, and any instance can publish without coordination.
/// </remarks>
public sealed class AzureSignalRRealtimeService : IRealtimeNotifier, IRealtimeTicketIssuer, IAsyncDisposable
{
    /// <summary>
    /// The single client-side method every notification is delivered to.
    /// </summary>
    private const string ClientMethodName = "Notify";

    private readonly BlueprintAzureSignalROptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<AzureSignalRRealtimeService> logger;
    private readonly Lazy<Task<ServiceHubContext>> hubContext;

    private ServiceManager? serviceManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureSignalRRealtimeService"/> class.
    /// </summary>
    /// <param name="options">The validated real-time options.</param>
    /// <param name="timeProvider">The clock a ticket's expiry is read from.</param>
    /// <param name="logger">The logger.</param>
    public AzureSignalRRealtimeService(
        IOptions<BlueprintAzureSignalROptions> options,
        TimeProvider timeProvider,
        ILogger<AzureSignalRRealtimeService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.options = options.Value;
        this.timeProvider = timeProvider;
        this.logger = logger;

        // Building the hub context is expensive, so it is created once and shared. Deferring it
        // also keeps it off the startup path, where a cold-started instance is already paying for
        // everything else it has to do before serving its first request.
        this.hubContext = new Lazy<Task<ServiceHubContext>>(
            this.CreateHubContextAsync,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public Task NotifyTenantAsync(Guid tenantId, RealtimeEvent realtimeEvent, CancellationToken cancellationToken = default)
    {
        return this.PublishAsync(
            hub => hub.Clients.Group(RealtimeGroupNames.Tenant(tenantId)),
            realtimeEvent,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task NotifyUserAsync(Guid tenantId, Guid userId, RealtimeEvent realtimeEvent, CancellationToken cancellationToken = default)
    {
        return this.PublishAsync(
            hub => hub.Clients.User(RealtimeGroupNames.ServiceUserId(tenantId, userId)),
            realtimeEvent,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task NotifyUsersAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, RealtimeEvent realtimeEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        ArgumentNullException.ThrowIfNull(realtimeEvent);

        // The REST API has no multi-user send, so on the transient transport the SDK turns this list
        // into one request per user, sent concurrently, each with its own retries; a failure for one
        // user is logged without stopping the others. A repeated id would therefore be a second
        // request, and every connection of that user would receive the event twice.
        var serviceUserIds = userIds
            .Distinct()
            .Select(userId => RealtimeGroupNames.ServiceUserId(tenantId, userId))
            .ToArray();

        if (serviceUserIds.Length == 0)
        {
            return Task.CompletedTask;
        }

        return this.PublishAsync(
            hub => hub.Clients.Users(serviceUserIds),
            realtimeEvent,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RealtimeTicket?> IssueAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var hub = await this.hubContext.Value;
        var serviceUserId = RealtimeGroupNames.ServiceUserId(tenantId, userId);

        // Membership is granted before the ticket is handed out, so it is already in place by the
        // time the browser connects. It is keyed on the user rather than on a connection because in
        // serverless mode there is no connected-callback to join a group from, and the connection
        // id does not exist yet.
        await hub.UserGroups.AddToGroupAsync(
            serviceUserId,
            RealtimeGroupNames.Tenant(tenantId),
            TimeSpan.FromHours(this.options.GroupMembershipTtlHours),
            cancellationToken);

        var tokenLifetime = TimeSpan.FromMinutes(this.options.TicketTokenLifetimeMinutes);

        // Azure starts the token's clock while it mints it, so the expiry is measured from before
        // the call rather than after it. Reading the clock afterwards would report an expiry later
        // than the real one by the duration of the negotiation, and the client schedules its
        // refresh from this value.
        var expiresAt = this.timeProvider.GetUtcNow().Add(tokenLifetime);

        var negotiation = await hub.NegotiateAsync(
            new NegotiationOptions
            {
                UserId = serviceUserId,
                TokenLifetime = tokenLifetime,
            },
            cancellationToken);

        if (string.IsNullOrEmpty(negotiation.Url) || string.IsNullOrEmpty(negotiation.AccessToken))
        {
            throw new InvalidOperationException("Azure SignalR Service returned an incomplete negotiation response.");
        }

        return new RealtimeTicket(
            new Uri(negotiation.Url, UriKind.Absolute),
            negotiation.AccessToken,
            expiresAt);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this.hubContext.IsValueCreated)
        {
            var pending = this.hubContext.Value;
            if (pending.IsCompletedSuccessfully)
            {
                var hub = await pending;
                await hub.DisposeAsync();
            }
        }

        this.serviceManager?.Dispose();
    }

    private async Task<ServiceHubContext> CreateHubContextAsync()
    {
        var manager = RealtimeServiceManagerFactory.Build(this.options);

        this.serviceManager = manager;

        return await manager.CreateHubContextAsync(this.options.HubName, CancellationToken.None);
    }

    private Task PublishAsync(
        Func<ServiceHubContext, IClientProxy> resolveTarget,
        RealtimeEvent realtimeEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(realtimeEvent);

        return this.PublishCoreAsync(resolveTarget, realtimeEvent, cancellationToken);
    }

    private async Task PublishCoreAsync(
        Func<ServiceHubContext, IClientProxy> resolveTarget,
        RealtimeEvent realtimeEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            var hub = await this.hubContext.Value;
            await resolveTarget(hub).SendAsync(ClientMethodName, realtimeEvent, cancellationToken);
        }
#pragma warning disable CA1031 // A notification must never fail the write that triggered it.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            // The database change is already committed. Surfacing this would make the client retry
            // a request that succeeded, and the client recovers on its own anyway: it refetches on
            // reconnect, so a dropped notification costs a delay, not correctness.
            this.logger.LogWarning(
                exception,
                "Failed to publish realtime event {EventName} for entity {EntityId}",
                realtimeEvent.EventName,
                realtimeEvent.EntityId);
        }
    }
}
