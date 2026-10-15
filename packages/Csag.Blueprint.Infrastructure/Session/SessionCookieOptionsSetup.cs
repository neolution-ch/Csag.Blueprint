namespace Csag.Blueprint.Infrastructure.Session;

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

/// <summary>
/// Turns the Identity application cookie into the package's session cookie, in a post-configure step so it runs
/// after every application <c>Configure</c> — including <c>AddIdentity</c>'s own, which replaces the cookie events
/// wholesale — in either registration order.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection.")]
internal sealed class SessionCookieOptionsSetup : IPostConfigureOptions<CookieAuthenticationOptions>
{
    /// <summary>
    /// The framework's own handlers, to recognise a redirect event nobody replaced.
    /// </summary>
    private static readonly CookieAuthenticationEvents FrameworkDefaults = new();

    private readonly ITicketStore ticketStore;
    private readonly IOptions<BlueprintSessionOptions> sessionOptions;
    private readonly IOptions<SessionCookieCallbacks> callbacks;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionCookieOptionsSetup"/> class.
    /// </summary>
    /// <param name="ticketStore">The session ticket store.</param>
    /// <param name="sessionOptions">The session settings carrying the lifetime.</param>
    /// <param name="callbacks">The application's cookie callbacks.</param>
    public SessionCookieOptionsSetup(
        ITicketStore ticketStore,
        IOptions<BlueprintSessionOptions> sessionOptions,
        IOptions<SessionCookieCallbacks> callbacks)
    {
        this.ticketStore = ticketStore ?? throw new ArgumentNullException(nameof(ticketStore));
        this.sessionOptions = sessionOptions ?? throw new ArgumentNullException(nameof(sessionOptions));
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    /// <inheritdoc/>
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Only the application cookie carries the session; the external and two-factor cookies keep their
        // framework behaviour.
        if (!string.Equals(name, IdentityConstants.ApplicationScheme, StringComparison.Ordinal))
        {
            return;
        }

        SessionCookieEventsGuard.EnsureDelegatesAreDispatched(IdentityConstants.ApplicationScheme, options);

        foreach (var configureCookie in this.callbacks.Value.ConfigureCookie)
        {
            configureCookie(options.Cookie);
        }

        // Re-applied after the application's callbacks, which may only shape the cookie, not the session it carries.
        options.Cookie.HttpOnly = true;
        options.SessionStore = this.ticketStore;
        options.ExpireTimeSpan = this.sessionOptions.Value.Lifetime;
        options.SlidingExpiration = true;

        ComposeEvents(options.Events);
    }

    private static void ComposeEvents(CookieAuthenticationEvents events)
    {
        // The framework's redirect handlers send the browser to a login or access-denied page; they are what the
        // package's status codes replace, so only an application's own handler is kept in front of them.
        events.OnRedirectToLogin = Compose(
            ExceptFrameworkDefault(events.OnRedirectToLogin, FrameworkDefaults.OnRedirectToLogin),
            SessionCookieEvents.RedirectToLoginAsync);
        events.OnRedirectToAccessDenied = Compose(
            ExceptFrameworkDefault(events.OnRedirectToAccessDenied, FrameworkDefaults.OnRedirectToAccessDenied),
            SessionCookieEvents.RedirectToAccessDeniedAsync);

        events.OnSigningIn = Compose(Handlers(events.OnSigningIn), SessionCookieEvents.SigningInAsync);
        events.OnCheckSlidingExpiration = Compose(Handlers(events.OnCheckSlidingExpiration), SessionCookieEvents.CheckSlidingExpirationAsync);
        events.OnValidatePrincipal = Compose(WithoutSecurityStampValidation(events.OnValidatePrincipal), SessionCookieEvents.ValidatePrincipalAsync);
    }

    private static Func<TContext, Task> Compose<TContext>(IEnumerable<Func<TContext, Task>> existing, Func<TContext, Task> package)
    {
        var handlers = existing.Append(package).ToArray();
        if (handlers.Length == 1)
        {
            return package;
        }

        // Invoking a multicast delegate hands back only its last handler's task, so each handler is awaited in turn.
        return async context =>
        {
            foreach (var handler in handlers)
            {
                await handler(context);
            }
        };
    }

    private static IEnumerable<Func<TContext, Task>> Handlers<TContext>(Func<TContext, Task>? handler)
        => handler?.GetInvocationList().Cast<Func<TContext, Task>>() ?? [];

    private static IEnumerable<Func<TContext, Task>> ExceptFrameworkDefault<TContext>(Func<TContext, Task> handler, Func<TContext, Task> frameworkDefault)
        => Handlers(handler).Where(candidate => candidate.Method != frameworkDefault.Method);

    private static IEnumerable<Func<CookieValidatePrincipalContext, Task>> WithoutSecurityStampValidation(Func<CookieValidatePrincipalContext, Task> handler)
    {
        // AddIdentity validates the application cookie's security stamp. The session principal deliberately carries
        // no stamp claim, so the validator would reject every session once its validation interval (30 minutes by
        // default) has passed, whatever the configured lifetime. Sessions are revoked through the session lifecycle
        // instead: removing the cached ticket ends a session on its next request.
        return Handlers(handler).Where(candidate => candidate.Method.DeclaringType != typeof(SecurityStampValidator));
    }
}
