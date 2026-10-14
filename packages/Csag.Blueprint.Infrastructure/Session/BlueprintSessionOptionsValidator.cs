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

        if (options.Lifetime <= TimeSpan.Zero)
        {
            failures.Add(
                "BlueprintSessionOptions.Lifetime must be positive. Csag.Blueprint.Web sets it from "
                + "Blueprint:Security:SessionExpirationHours through AddBlueprintDefaultValidatedOptions; a host without "
                + "those options sets it itself.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
