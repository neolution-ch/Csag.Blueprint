namespace Csag.Blueprint.Web.Extensions;

using Csag.Blueprint.Web.Middleware;
using Csag.Blueprint.Web.Options.Api.Security;
using Csag.Blueprint.Web.Options.Localization;
using Csag.Blueprint.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// Aggregate extension methods that compose individual Blueprint middleware extensions
/// into a single call for cleaner Program.cs setup.
/// </summary>
public static class BlueprintMiddlewareExtensions
{
    /// <summary>
    /// Applies all Blueprint security header middleware: HTTPS redirection, HSTS,
    /// custom security headers, and server identity header removal.
    /// Safe to call in all modes (including generation mode).
    /// </summary>
    /// <remarks>
    /// This registers <c>ForwardedHeadersMiddleware</c> with <c>KnownProxies</c> and
    /// <c>KnownIPNetworks</c> empty, which turns off its known-proxy check: it applies whatever the
    /// immediate peer sent. Deploying the app so it is reachable only through an edge that rewrites or
    /// appends <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> is therefore a requirement of calling
    /// this method. A caller that can connect to the app directly writes both headers itself, which makes
    /// the entry promoted to <c>RemoteIpAddress</c> that caller's own value and lets a forged
    /// <c>X-Forwarded-Proto: https</c> satisfy the HTTPS redirection and HSTS middleware below.
    /// </remarks>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication UseBlueprintSecurityHeaders(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var securitySettings = app.Services.GetRequiredService<IOptions<SecuritySettings>>().Value;

        // Trust forwarded headers from reverse proxies (e.g. Cloud Run).
        // Must run before HTTPS redirection and HSTS so the original scheme is visible.
        // Clear KnownProxies/KnownNetworks so headers from any proxy are accepted —
        // Cloud Run and similar platforms use internal IPs that aren't on the default loopback list.
        // Clearing them also turns off the middleware's known-proxy check, so what it applies is whatever
        // the immediate peer sent. The ingress requirement that makes this safe — only an edge can reach
        // the app — is stated in the remarks above, and it is what the rest of this reasoning assumes.
        // Given that ingress, ForwardLimit 1 reads only the rightmost X-Forwarded-For entry, which is
        // written by the hop immediately in front of this app and so is never a value a client wrote. It
        // is still NOT the caller's address: an edge that appends its own entry (a Google external load
        // balancer sends "<client>,<balancer>") leaves the balancer's address rightmost, and behind a
        // further reverse proxy it is that proxy's egress address — one constant shared by every
        // caller. Treat RemoteIpAddress as the nearest hop, not as a client identity, and resolve the
        // caller from an edge-stamped header instead (RateLimiterExtensions.TryGetClientPartitionKey).
        // Do not raise this limit without a proxy that validates the header first.
        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1,
        };
        forwardedHeadersOptions.KnownIPNetworks.Clear();
        forwardedHeadersOptions.KnownProxies.Clear();
        app.UseForwardedHeaders(forwardedHeadersOptions);

        app.UseHttpsRedirectionIfEnabled(securitySettings)
            .UseHstsIfEnabled(securitySettings)
            .UseSecurityHeaders(securitySettings)
            .UseServerIdentityHeaderRemoval(securitySettings);

        return app;
    }

    /// <summary>
    /// Applies the core Blueprint middleware pipeline: HTTP audit logging, exception handling,
    /// status code pages, correlation ID tracking, CORS, authentication, request localization,
    /// tenant context, and authorization.
    /// <see cref="HttpAuditMiddleware"/> is placed first so it wraps everything: authentication and
    /// authorization, and also the exception handler and status code pages, so it can see a status
    /// code that the exception handler itself produces. It records nothing until an app calls
    /// <c>ConfigureBlueprintAuditLogging</c>.
    /// Should only be called when not in generation mode.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication UseBlueprintMiddleware(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var securitySettings = app.Services.GetRequiredService<IOptions<SecuritySettings>>().Value;

        app.UseMiddleware<HttpAuditMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseMiddleware<NonProblemDetailsDetectionMiddleware>();
        }

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseMiddleware<OperationCancelledMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseCorsIfConfigured(securitySettings);
        app.UseAuthentication();
        app.UseMiddleware<CsrfMiddleware>();
        app.UseConfiguredRequestLocalization();
        app.UseAuthorization();
        app.UseMiddleware<TenantMiddleware>();
        app.UseMiddleware<CurrentActorMiddleware>();

        return app;
    }

    /// <summary>
    /// Configures request localization using supported languages from settings.
    /// Uses a custom provider chain: session claim (user preference) → Accept-Language header → default language.
    /// Placed after authentication so claims are available,
    /// but before authorization so error responses can be localized.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    private static WebApplication UseConfiguredRequestLocalization(this WebApplication app)
    {
        var localizationOptions = app.Services.GetRequiredService<IOptions<LocalizationOptions>>().Value;

        app.UseRequestLocalization(options =>
        {
            var supportedCultures = localizationOptions.SupportedLanguages.ToArray();
            options.SetDefaultCulture(localizationOptions.DefaultLanguage);
            options.AddSupportedCultures(supportedCultures);
            options.AddSupportedUICultures(supportedCultures);
            options.ApplyCurrentCultureToResponseHeaders = true;

            // Clear default providers and use our custom chain:
            // 1. Session claim (user preference) → 2. Accept-Language header → 3. Default language
            options.RequestCultureProviders.Clear();
            options.RequestCultureProviders.Add(new SessionClaimRequestCultureProvider { Options = options });
        });

        return app;
    }
}
