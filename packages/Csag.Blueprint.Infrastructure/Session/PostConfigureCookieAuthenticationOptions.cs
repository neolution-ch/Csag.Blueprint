namespace Csag.Blueprint.Infrastructure.Session;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

/// <summary>
/// Post-configures CookieAuthenticationOptions to inject ITicketStore from the final DI container.
/// This avoids the anti-pattern of calling BuildServiceProvider() during service registration.
/// </summary>
public class PostConfigureCookieAuthenticationOptions : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly ITicketStore ticketStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostConfigureCookieAuthenticationOptions"/> class.
    /// </summary>
    /// <param name="ticketStore">The ticket store implementation.</param>
    public PostConfigureCookieAuthenticationOptions(ITicketStore ticketStore)
    {
        this.ticketStore = ticketStore ?? throw new ArgumentNullException(nameof(ticketStore));
    }

    /// <summary>
    /// Called after CookieAuthenticationOptions are configured to inject the session store.
    /// </summary>
    /// <param name="name">The name of the options instance being configured.</param>
    /// <param name="options">The options instance to configure.</param>
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        options.SessionStore = this.ticketStore;
    }
}
