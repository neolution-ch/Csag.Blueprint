namespace Csag.Blueprint.Web.Extensions.Oidc;

using System;
using System.Security.Claims;
using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Pure, provider-specific policy for Microsoft Entra ID: tenant-aware issuer validation and the
/// email-trust / claim-normalization rules that mitigate the "nOAuth" account-takeover class.
/// Extracted from the handler wiring so the security-critical logic can be unit-tested directly.
/// </summary>
public static class EntraClaimPolicy
{
    /// <summary>
    /// Validates that the token's issuer matches the tenant that issued it. Entra multi-tenant apps
    /// (<c>/organizations</c>, <c>/common</c>) accept tokens from any tenant, each with a tenant-specific
    /// issuer, so the single discovery-derived issuer cannot be used. Accepts
    /// <c>https://login.microsoftonline.com/{tid}/v2.0</c> and the legacy <c>https://sts.windows.net/{tid}/</c>.
    /// </summary>
    /// <returns>The validated issuer.</returns>
    /// <exception cref="SecurityTokenInvalidIssuerException">Thrown when the issuer does not match the token's tenant.</exception>
    public static string ValidateMultiTenantIssuer(string issuer, SecurityToken token, TokenValidationParameters parameters)
    {
        if (token is not JsonWebToken jwt)
        {
            throw new SecurityTokenInvalidIssuerException("Unable to validate the token: it is not a JsonWebToken.");
        }

        var tid = jwt.TryGetPayloadValue<string>("tid", out var tenantId) ? tenantId : null;

        if (string.IsNullOrEmpty(tid))
        {
            throw new SecurityTokenInvalidIssuerException("Token does not contain a 'tid' claim.");
        }

        var expectedIssuer = $"https://login.microsoftonline.com/{tid}/v2.0";
        var expectedStsIssuer = $"https://sts.windows.net/{tid}/";

        if (string.Equals(issuer, expectedIssuer, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issuer, expectedStsIssuer, StringComparison.OrdinalIgnoreCase))
        {
            return issuer;
        }

        throw new SecurityTokenInvalidIssuerException($"The iss claim '{issuer}' does not match the expected tenant-based value for tid '{tid}'.");
    }

    /// <summary>
    /// Determines whether the external principal's email may be trusted for account creation/linking.
    /// Entra does NOT emit a standard <c>email_verified</c> claim, and for multi-tenant apps the
    /// <c>email</c> / <c>preferred_username</c> values are mutable and can be set to an arbitrary address
    /// by a (malicious) tenant — the "nOAuth" account-takeover class. The address is therefore only
    /// trustworthy when the app authenticates a single (admin-controlled) tenant, or Entra asserts the
    /// optional <c>xms_edov</c> (Email Domain Owner Verified) claim.
    /// </summary>
    public static bool IsEmailVerified(ClaimsIdentity identity, MicrosoftEntraSignInAudience audience)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return audience == MicrosoftEntraSignInAudience.SingleTenant
            || IsClaimTrue(identity.FindFirst("xms_edov")?.Value);
    }

    /// <summary>
    /// Maps Entra's short OIDC claims to the standard <see cref="ClaimTypes"/> the shared callback reads,
    /// and stamps a trustworthy <c>email_verified</c> claim per <see cref="IsEmailVerified"/>. Because the
    /// Entra handler runs with <c>MapInboundClaims = false</c>, the raw <c>sub</c>/<c>email</c>/etc. claims
    /// need normalizing. The <c>preferred_username</c> email fallback is only applied when verified, so the
    /// callback never links on an attacker-controllable value.
    /// </summary>
    public static void NormalizeClaimsForCallback(ClaimsIdentity identity, MicrosoftEntraSignInAudience audience)
    {
        ArgumentNullException.ThrowIfNull(identity);

        MapClaimIfMissing(identity, "sub", ClaimTypes.NameIdentifier);
        MapClaimIfMissing(identity, "email", ClaimTypes.Email);
        MapClaimIfMissing(identity, "given_name", ClaimTypes.GivenName);
        MapClaimIfMissing(identity, "family_name", ClaimTypes.Surname);

        var emailIsVerified = IsEmailVerified(identity, audience);

        // Fail closed: never trust an email_verified value that arrived in the token/userinfo. Entra emits
        // no standard email_verified claim, so any that reaches us is unexpected, and honouring it would let
        // a value we did not compute drive the account-link email-trust gate (the "nOAuth" class). Strip any
        // incoming claim(s) and always stamp our own computed trust value.
        Claim? staleVerified;
        while ((staleVerified = identity.FindFirst("email_verified")) != null)
        {
            identity.RemoveClaim(staleVerified);
        }

        identity.AddClaim(new Claim("email_verified", emailIsVerified ? "true" : "false"));

        // Entra work/school accounts often omit `email` and expose only `preferred_username`.
        // Only fall back to it when the email is verified, so we never link on an attacker-controllable value.
        if (emailIsVerified && identity.FindFirst(ClaimTypes.Email) == null)
        {
            var preferredUsername = identity.FindFirst("preferred_username")?.Value;
            if (!string.IsNullOrEmpty(preferredUsername) && preferredUsername.Contains('@'))
            {
                identity.AddClaim(new Claim(ClaimTypes.Email, preferredUsername));
            }
        }
    }

    private static bool IsClaimTrue(string? value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "1", StringComparison.Ordinal);
    }

    private static void MapClaimIfMissing(ClaimsIdentity identity, string source, string target)
    {
        if (identity.FindFirst(target) != null)
        {
            return;
        }

        var sourceClaim = identity.FindFirst(source);
        if (sourceClaim != null)
        {
            identity.AddClaim(new Claim(target, sourceClaim.Value));
        }
    }
}
