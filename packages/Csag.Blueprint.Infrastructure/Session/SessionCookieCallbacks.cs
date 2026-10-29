namespace Csag.Blueprint.Infrastructure.Session;

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

/// <summary>
/// The cookie callbacks passed to <see cref="ServiceCollectionExtensions.AddBlueprintCookieSessions"/>, collected
/// through the options pattern so every call contributes and <see cref="SessionCookieOptionsSetup"/> is registered
/// once.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection.")]
internal sealed class SessionCookieCallbacks
{
    /// <summary>
    /// Gets the callbacks, in registration order.
    /// </summary>
    public List<Action<CookieBuilder>> ConfigureCookie { get; } = [];
}
