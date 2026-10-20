namespace Csag.Blueprint.Infrastructure.Session;

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

/// <summary>
/// Fails startup on <see cref="BlueprintSessionOptions"/> the session infrastructure cannot run with.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection.")]
internal sealed class BlueprintSessionOptionsValidator : IValidateOptions<BlueprintSessionOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, BlueprintSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        // A ticket keeps its expiry to whole seconds, so a shorter session could be expired as it is issued.
        if (options.Lifetime < TimeSpan.FromSeconds(1))
        {
            failures.Add(
                "BlueprintSessionOptions.Lifetime must be at least one second. Csag.Blueprint.Web sets it from "
                + "Blueprint:Security:SessionExpirationHours through AddBlueprintDefaultValidatedOptions; a host without "
                + "those options sets it itself.");
        }

        if (options.ClientIpHeader is not null && string.IsNullOrWhiteSpace(options.ClientIpHeader))
        {
            failures.Add("BlueprintSessionOptions.ClientIpHeader must be null or a header name, not blank.");
        }

        var cleanup = options.ExpiredSessionCleanup;
        if (cleanup.Enabled && (cleanup.Interval <= TimeSpan.Zero || cleanup.Interval > ExpiredSessionCleanupOptions.MaxInterval))
        {
            failures.Add(
                $"BlueprintSessionOptions.ExpiredSessionCleanup.Interval must be positive and at most {ExpiredSessionCleanupOptions.MaxInterval}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
