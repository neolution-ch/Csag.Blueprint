namespace Csag.Blueprint.Web.Middleware;

using System.Text.Json;
using Csag.Blueprint.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Refuses a request that carries both the session cookie and a bearer token with <c>400 Bad Request</c>, before
/// authentication runs. Each request is authenticated by exactly one scheme, so it presents exactly one credential.
/// </summary>
/// <remarks>
/// "Carries a bearer token" is the rule the scheme selectors use (<see cref="BearerTokenRequestExtensions.TryGetBearerToken"/>);
/// "carries the session cookie" means the Identity application cookie is present, whether or not it still
/// authenticates.
/// </remarks>
public class SingleCredentialMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<SingleCredentialMiddleware> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleCredentialMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public SingleCredentialMiddleware(RequestDelegate next, ILogger<SingleCredentialMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    /// <summary>
    /// Passes the request on, or refuses it when it carries both credentials.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="cookieOptions">The cookie authentication options, for the session cookie's name.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context, IOptionsMonitor<CookieAuthenticationOptions> cookieOptions)
    {
        if (context.Request.TryGetBearerToken(out _) && CarriesSessionCookie(context, cookieOptions))
        {
            this.logger.LogInformation("Refused a request carrying both a session cookie and a bearer token");

            context.Response.StatusCode = StatusCodes.Status400BadRequest;

            var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdKey] as string;

            var problemDetails = new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                title = "More than one credential",
                status = StatusCodes.Status400BadRequest,
                detail = "The request carries both a session cookie and a bearer token. Send exactly one of them.",
                correlationId,
            };

            await context.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, contentType: "application/problem+json");
            return;
        }

        await this.next(context);
    }

    private static bool CarriesSessionCookie(HttpContext context, IOptionsMonitor<CookieAuthenticationOptions> cookieOptions)
    {
        // A chunked cookie keeps its own name on the first chunk, so the name alone tells whether it was sent.
        var cookieName = cookieOptions.Get(IdentityConstants.ApplicationScheme).Cookie.Name;
        return !string.IsNullOrEmpty(cookieName) && context.Request.Cookies.ContainsKey(cookieName);
    }
}
