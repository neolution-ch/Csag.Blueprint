namespace Csag.Blueprint.Realtime.AzureSignalR;

using Csag.Blueprint.Application.Abstractions.Realtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Registers the Azure SignalR Service provider of the real-time channel.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IRealtimeNotifier"/> and <see cref="IRealtimeTicketIssuer"/> on Azure SignalR
    /// Service in serverless mode, and validates <see cref="BlueprintAzureSignalROptions"/> at startup.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Sets the options, typically by binding a configuration section.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// A host that runs without real-time registers <see cref="NoOpRealtimeService"/> for both interfaces
    /// instead of calling this.
    /// </remarks>
    public static IServiceCollection AddBlueprintAzureSignalRRealtime(
        this IServiceCollection services,
        Action<BlueprintAzureSignalROptions>? configure = null)
    {
        // Clock seam for ticket expiry and event stamps. TryAdd, so a TimeProvider registered before this
        // call (a FakeTimeProvider in tests) wins.
        services.TryAddSingleton(TimeProvider.System);

        var options = services.AddOptions<BlueprintAzureSignalROptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options.ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BlueprintAzureSignalROptions>, BlueprintAzureSignalROptionsValidator>());

        // Both interfaces resolve to the one singleton, so the hub context behind it is built once.
        services.TryAddSingleton<AzureSignalRRealtimeService>();
        services.TryAddSingleton<IRealtimeNotifier>(sp => sp.GetRequiredService<AzureSignalRRealtimeService>());
        services.TryAddSingleton<IRealtimeTicketIssuer>(sp => sp.GetRequiredService<AzureSignalRRealtimeService>());

        return services;
    }
}
