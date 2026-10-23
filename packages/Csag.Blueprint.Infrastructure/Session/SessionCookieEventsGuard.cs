namespace Csag.Blueprint.Infrastructure.Session;

using System.Reflection;
using Microsoft.AspNetCore.Authentication.Cookies;

/// <summary>
/// Makes sure the handlers the package assigns on <see cref="CookieAuthenticationOptions.Events"/> are the ones the
/// cookie handler actually calls.
/// </summary>
internal static class SessionCookieEventsGuard
{
    /// <summary>
    /// The <see cref="CookieAuthenticationEvents"/> methods whose delegates carry the package's session handling.
    /// </summary>
    private static readonly string[] PackageEventMethods =
    [
        nameof(CookieAuthenticationEvents.RedirectToLogin),
        nameof(CookieAuthenticationEvents.RedirectToAccessDenied),
        nameof(CookieAuthenticationEvents.SigningIn),
        nameof(CookieAuthenticationEvents.CheckSlidingExpiration),
        nameof(CookieAuthenticationEvents.ValidatePrincipal),
    ];

    /// <summary>
    /// Throws when the cookie handler would not dispatch the package's events through the delegates on
    /// <see cref="CookieAuthenticationOptions.Events"/>: when the scheme resolves its events from DI through
    /// <c>EventsType</c>, or when its events object overrides one of those event methods.
    /// </summary>
    /// <param name="schemeName">The authentication scheme the options belong to.</param>
    /// <param name="options">The scheme's options.</param>
    /// <exception cref="InvalidOperationException">The package's events would not reach their delegates.</exception>
    public static void EnsureDelegatesAreDispatched(string schemeName, CookieAuthenticationOptions options)
    {
        var reason = options.EventsType is not null
            ? "sets EventsType"
            : DescribeOverrides(options.Events.GetType());

        if (reason is not null)
        {
            throw new InvalidOperationException(
                $"Session cookie scheme '{schemeName}' {reason}, which would skip the package's session handling "
                + "(401/403 responses, session eligibility, security-stamp suppression). "
                + "Assign handlers to the CookieAuthenticationOptions.Events delegates instead.");
        }
    }

    private static string? DescribeOverrides(Type eventsType)
    {
        // A method not overridden below CookieAuthenticationEvents is declared on it, and that implementation only
        // invokes the matching delegate.
        var overridden = PackageEventMethods
            .Where(name => eventsType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance)?.DeclaringType is { } declaringType
                && !declaringType.IsAssignableFrom(typeof(CookieAuthenticationEvents)))
            .ToArray();

        return overridden.Length == 0 ? null : $"uses {eventsType.Name}, which overrides {string.Join(", ", overridden)}";
    }
}
