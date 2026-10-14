namespace Csag.Blueprint.Infrastructure.ServiceAccounts;

using System.Diagnostics.CodeAnalysis;
using Csag.Blueprint.Infrastructure.Extensions;
using Csag.Blueprint.Infrastructure.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

/// <summary>
/// Describes the request a service-account token is issued on, for the session it creates: the user agent, and the
/// client address read with the rule session tracking and rate limiting share.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection.")]
internal sealed class ServiceAccountCaller
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IOptions<BlueprintSessionOptions> sessionOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAccountCaller"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">Locates the issuing request.</param>
    /// <param name="sessionOptions">The session settings carrying the client address header.</param>
    public ServiceAccountCaller(IHttpContextAccessor httpContextAccessor, IOptions<BlueprintSessionOptions> sessionOptions)
    {
        this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        this.sessionOptions = sessionOptions ?? throw new ArgumentNullException(nameof(sessionOptions));
    }

    /// <summary>
    /// Describes the current request.
    /// </summary>
    /// <returns>The user agent and client address, each <see langword="null"/> when unknown or outside a request.</returns>
    public (string? UserAgent, string? IpAddress) Describe()
    {
        var httpContext = this.httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return (null, null);
        }

        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        var ipAddress = httpContext.TryGetClientAddress(this.sessionOptions.Value.ClientIpHeader, out var address)
            ? address.ToString()
            : null;

        return (string.IsNullOrEmpty(userAgent) ? null : userAgent, ipAddress);
    }
}
