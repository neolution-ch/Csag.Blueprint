namespace Csag.Blueprint.Web.Services;

using Csag.Blueprint.Application.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

/// <summary>
/// Custom request culture provider that resolves the user's language preference based on the following priority:
/// <list type="number">
///   <item><description>User's preferred language from the authentication session claim (<see cref="IdentityClaimTypes.PreferredLanguage"/>).</description></item>
///   <item><description>The Accept-Language HTTP header.</description></item>
///   <item><description>The default language from configuration (handled by the built-in <see cref="RequestLocalizationOptions"/>).</description></item>
/// </list>
/// </summary>
public sealed class SessionClaimRequestCultureProvider : RequestCultureProvider
{
    /// <inheritdoc/>
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // Priority 1: Check the session claim for user's explicitly chosen language
        // Validate against supported cultures to ensure the claim value is legitimate
        var preferredLanguage = httpContext.User?.FindFirst(IdentityClaimTypes.PreferredLanguage)?.Value;
        if (!string.IsNullOrEmpty(preferredLanguage))
        {
            var supportedCultures = this.Options?.SupportedUICultures;
            if (supportedCultures != null && supportedCultures.Count > 0)
            {
                var matchedCulture = CultureNormalizationHelper.FindMatchingCulture(preferredLanguage, supportedCultures);
                if (!string.IsNullOrEmpty(matchedCulture))
                {
                    return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(matchedCulture));
                }
            }
        }

        // Priority 2: Check the Accept-Language header
        var match = this.MatchAcceptLanguageHeader(httpContext);
        if (match != null)
        {
            return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(match));
        }

        // Priority 3: Return null to let the default culture take effect
        return NullProviderCultureResult;
    }

    /// <summary>
    /// Attempts to match the Accept-Language header values against the configured supported cultures.
    /// Returns the best matching culture name, or null if no match is found.
    /// </summary>
    /// <param name="httpContext">The HTTP context containing the request headers.</param>
    /// <returns>The matching culture name, or null.</returns>
    private string? MatchAcceptLanguageHeader(HttpContext httpContext)
    {
        var acceptLanguageHeader = httpContext.Request.GetTypedHeaders().AcceptLanguage;
        if (acceptLanguageHeader == null || acceptLanguageHeader.Count == 0)
        {
            return null;
        }

        var supportedCultures = this.Options?.SupportedUICultures;
        if (supportedCultures == null || supportedCultures.Count == 0)
        {
            return null;
        }

        foreach (var headerValue in acceptLanguageHeader.OrderByDescending(h => h.Quality ?? 1.0))
        {
            var requestedCulture = headerValue.Value.Value;
            if (string.IsNullOrEmpty(requestedCulture))
            {
                continue;
            }

            var result = CultureNormalizationHelper.FindMatchingCulture(requestedCulture, supportedCultures);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
