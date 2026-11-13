namespace Csag.Blueprint.Realtime.AzureSignalR;

using System.Text.Json;
using Azure.Core.Serialization;
using Microsoft.Azure.SignalR.Management;

/// <summary>
/// Builds the Azure SignalR <see cref="ServiceManager"/> that <see cref="AzureSignalRRealtimeService"/> publishes through.
/// </summary>
internal static class RealtimeServiceManagerFactory
{
    /// <summary>
    /// Builds a service manager for the configured Azure SignalR Service.
    /// </summary>
    /// <param name="options">The validated real-time options.</param>
    /// <returns>The service manager. The caller owns it and disposes it.</returns>
    public static ServiceManager Build(BlueprintAzureSignalROptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new ServiceManagerBuilder()
            .WithOptions(option =>
            {
                option.ConnectionString = options.ConnectionString;

                // Transient sends each message as its own HTTPS request. The alternative holds an
                // outbound websocket, which this deployment cannot keep alive: CPU is allocated
                // only while a request is in flight, so the socket is frozen between requests and
                // torn down on scale-down.
                option.ServiceTransportType = ServiceTransportType.Transient;

                // Retries are off unless this is assigned - the default is null, which resolves to
                // a no-op policy. The first publish after an idle gap is the likeliest to fail,
                // because the pooled connection behind it may already be dead.
                option.RetryOptions = new ServiceManagerRetryOptions();

                // Transient otherwise serializes with Newtonsoft using PascalCase, while every
                // other payload this app produces is camelCase and the generated TypeScript client
                // expects camelCase. Without this the payload arrives with unreadable field names.
                option.UseJsonObjectSerializer(new JsonObjectSerializer(new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                }));
            })
            .BuildServiceManager();
    }
}
