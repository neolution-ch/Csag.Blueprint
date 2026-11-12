namespace Csag.Blueprint.Realtime.AzureSignalR;

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.Options;

/// <summary>
/// Fails startup on <see cref="BlueprintAzureSignalROptions"/> the provider cannot run with.
/// </summary>
/// <remarks>
/// The hub context is built lazily, on the first ticket or publish, so anything left for the SDK to
/// reject would otherwise surface in production traffic instead of at startup.
/// </remarks>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection.")]
internal sealed partial class BlueprintAzureSignalROptionsValidator : IValidateOptions<BlueprintAzureSignalROptions>
{
    /// <summary>
    /// The longest group membership the service holds, one year in hours.
    /// </summary>
    internal const int MaxGroupMembershipTtlHours = 8760;

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, BlueprintAzureSignalROptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        var connectionStringProblem = DescribeConnectionStringProblem(options.ConnectionString);
        if (connectionStringProblem is not null)
        {
            failures.Add(connectionStringProblem);
        }

        if (string.IsNullOrEmpty(options.HubName) || !HubNamePattern().IsMatch(options.HubName))
        {
            failures.Add("BlueprintAzureSignalROptions.HubName must be 1-64 characters of letters, digits or underscores.");
        }

        if (options.TicketTokenLifetimeMinutes <= 0)
        {
            failures.Add("BlueprintAzureSignalROptions.TicketTokenLifetimeMinutes must be greater than 0.");
        }

        // IssueAsync grants the membership first and mints the token afterwards, so the membership clock
        // is always the older of the two. With equal durations the token would still open a connection
        // for a while after the membership ended, and that connection would receive nothing.
        if (options.TicketTokenLifetimeMinutes > 0
            && options.TicketTokenLifetimeMinutes >= (long)options.GroupMembershipTtlHours * 60)
        {
            failures.Add(
                "BlueprintAzureSignalROptions.TicketTokenLifetimeMinutes must be shorter than GroupMembershipTtlHours, "
                + "because the membership is granted first and has to outlast every ticket issued with it.");
        }

        if (options.GroupMembershipTtlHours <= 0 || options.GroupMembershipTtlHours > MaxGroupMembershipTtlHours)
        {
            failures.Add(
                $"BlueprintAzureSignalROptions.GroupMembershipTtlHours must be between 1 and {MaxGroupMembershipTtlHours} (one year). "
                + "The service reads 0 as 'only the connections open right now', which at ticket time is none.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// Checks a connection string the way the SDK will when the hub context is first built.
    /// </summary>
    /// <param name="connectionString">The configured connection string.</param>
    /// <returns>The failure message, or <c>null</c> when the value is usable.</returns>
    private static string? DescribeConnectionStringProblem(string? connectionString)
    {
        // Without an access key the SDK falls back to Microsoft Entra authentication without saying so,
        // and every REST call fails unauthorized unless the host set that up.
        if (string.IsNullOrWhiteSpace(connectionString) || !HasAccessKey(connectionString))
        {
            return "BlueprintAzureSignalROptions.ConnectionString must contain an Endpoint and an AccessKey.";
        }

        // ServiceEndpoint runs the SDK's own parser without any network call, which covers the endpoint,
        // port, version and client endpoint.
        try
        {
            _ = new ServiceEndpoint(connectionString);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            return $"BlueprintAzureSignalROptions.ConnectionString is not a valid Azure SignalR connection string: {exception.Message}";
        }

        return null;
    }

    /// <summary>
    /// Determines whether the connection string carries a non-empty <c>AccessKey</c> property.
    /// </summary>
    /// <param name="connectionString">The configured connection string.</param>
    /// <returns><c>true</c> when an access key is present.</returns>
    private static bool HasAccessKey(string connectionString)
    {
        // Split exactly as the SDK parser does, so a base64 key ending in '=' is read whole, and a
        // "ClientEndpoint=" is not mistaken for a key.
        return connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(property => property.Split('=', 2))
            .Any(property => property.Length == 2
                && property[0].Equals("AccessKey", StringComparison.OrdinalIgnoreCase)
                && property[1].Length > 0);
    }

    [GeneratedRegex("^[A-Za-z0-9_]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex HubNamePattern();
}
