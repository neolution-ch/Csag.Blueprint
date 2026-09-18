namespace Csag.Blueprint.Web.Extensions.Oidc;

using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

/// <summary>
/// Makes sure the handlers the package assigns on <see cref="OpenIdConnectOptions.Events"/> are the ones the
/// OpenID Connect handler actually calls.
/// </summary>
internal static class OpenIdConnectEventsGuard
{
    /// <summary>
    /// Throws when the handler would not dispatch the named events through the delegates on
    /// <see cref="OpenIdConnectOptions.Events"/>: when the scheme resolves its events from DI through
    /// <c>EventsType</c>, or when its events object overrides one of those event methods.
    /// </summary>
    /// <param name="scheme">The authentication scheme the options belong to.</param>
    /// <param name="options">The scheme's options.</param>
    /// <param name="consequence">What would be skipped, completing the sentence "…, which …".</param>
    /// <param name="eventMethods">The <see cref="OpenIdConnectEvents"/> methods whose delegates the caller relies on.</param>
    /// <exception cref="InvalidOperationException">The named events would not reach their delegates.</exception>
    public static void EnsureDelegatesAreDispatched(
        string scheme,
        OpenIdConnectOptions options,
        string consequence,
        params string[] eventMethods)
    {
        var reason = options.EventsType is not null
            ? "sets EventsType"
            : DescribeOverrides(options.Events.GetType(), eventMethods);

        if (reason is not null)
        {
            throw new InvalidOperationException(
                $"OpenID Connect provider '{scheme}' {reason}, which {consequence}. " +
                "Assign its handlers to the OpenIdConnectOptions.Events delegates instead.");
        }
    }

    private static string? DescribeOverrides(Type eventsType, string[] eventMethods)
    {
        // A method not overridden below OpenIdConnectEvents is declared on it or on one of its base classes, and
        // those implementations only invoke the matching delegate.
        var overridden = eventMethods
            .Where(name => eventsType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance)?.DeclaringType is { } declaringType
                && !declaringType.IsAssignableFrom(typeof(OpenIdConnectEvents)))
            .ToArray();

        return overridden.Length == 0 ? null : $"uses {eventsType.Name}, which overrides {string.Join(", ", overridden)}";
    }
}
