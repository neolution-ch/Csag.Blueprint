namespace Csag.Blueprint.Web.Middleware;

using System.Text.Json;
using Csag.Blueprint.Web.Helpers;
using Csag.Blueprint.Web.Options.Api.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Middleware that handles CSRF token distribution and validation for cookie-authenticated requests.
/// Distributes a non-HttpOnly request token cookie for JavaScript to read, and validates
/// state-changing requests by checking the antiforgery token pair.
/// </summary>
public class CsrfMiddleware
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options,
        HttpMethods.Trace,
    };

    private readonly RequestDelegate next;
    private readonly ILogger<CsrfMiddleware> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsrfMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public CsrfMiddleware(RequestDelegate next, ILogger<CsrfMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    /// <summary>
    /// Processes the HTTP request for CSRF protection.
    /// Validates antiforgery tokens on state-changing requests from cookie-authenticated users,
    /// and distributes the request token cookie on every response.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="antiforgery">The antiforgery service.</param>
    /// <param name="securityOptions">The security configuration options.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery, IOptions<SecuritySettings> securityOptions)
    {
        var securitySettings = securityOptions.Value;

        if (!securitySettings.Csrf.Enabled)
        {
            await this.next(context);
            return;
        }

        var isCookieAuthenticated = IsCookieAuthenticated(context);

        // Validate state-changing requests from cookie-authenticated users
        if (isCookieAuthenticated && !SafeMethods.Contains(context.Request.Method))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException e)
            {
                this.logger.LogWarning(e, "CSRF validation failed");

                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdKey] as string;

                var problemDetails = new
                {
                    type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                    title = "CSRF validation failed",
                    status = 403,
                    detail = "The CSRF token is missing or invalid. Ensure the request includes a valid CSRF token.",
                    correlationId,
                };

                await context.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, contentType: "application/problem+json");
                return;
            }
        }

        // Distribute request token cookie for cookie-authenticated users
        if (isCookieAuthenticated)
        {
            context.Response.OnStarting(() =>
            {
                CsrfTokenDistributor.DistributeRequestToken(context, antiforgery, securitySettings);
                return Task.CompletedTask;
            });
        }

        await this.next(context);
    }

    private static bool IsCookieAuthenticated(HttpContext context)
    {
        var user = context.User;
        if (user.Identity is not { IsAuthenticated: true })
        {
            return false;
        }

        // Only cookie-authenticated requests (Identity.Application) need CSRF protection.
        // JWT Bearer-authenticated requests are stateless and not vulnerable to CSRF.
        var authScheme = user.Identity.AuthenticationType;
        return string.Equals(authScheme, IdentityConstants.ApplicationScheme, StringComparison.OrdinalIgnoreCase);
    }
}
