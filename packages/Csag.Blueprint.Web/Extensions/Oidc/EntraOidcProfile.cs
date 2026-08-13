namespace Csag.Blueprint.Web.Extensions.Oidc;

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

/// <summary>
/// Profile for Microsoft Entra ID (Azure AD). Derives the authority from the configured
/// <see cref="MicrosoftEntraSignInAudience"/> (or an explicit <see cref="OidcProviderSettings.Authority"/>),
/// applies tenant-aware issuer validation for multi-tenant apps, and normalizes claims + computes a
/// trustworthy <c>email_verified</c> value via <see cref="EntraClaimPolicy"/> to avoid the "nOAuth"
/// account-takeover class. Uses the generic <c>AddOpenIdConnect</c> handler — no Entra-specific package.
/// </summary>
public sealed class EntraOidcProfile : OidcProviderProfileBase
{
    /// <inheritdoc/>
    public override void Configure(OpenIdConnectOptions options, OidcProviderSettings settings)
    {
        ApplyCommon(options, settings);

        options.Authority = string.IsNullOrWhiteSpace(settings.Authority)
            ? ResolveAuthority(settings)
            : settings.Authority;

        // Entra id_tokens use short claim names; normalize them ourselves rather than via the
        // inbound map, and expose Entra's role claim.
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "roles";

        // Multi-tenant apps accept tokens from any tenant, so the single discovery issuer cannot be used.
        if (settings.SignInAudience != MicrosoftEntraSignInAudience.SingleTenant)
        {
            options.TokenValidationParameters.IssuerValidator = EntraClaimPolicy.ValidateMultiTenantIssuer;

            if (settings.SignInAudience == MicrosoftEntraSignInAudience.MultiTenantAndPersonal)
            {
                options.TokenValidationParameters.ValidateIssuer = true;
            }
        }

        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is ClaimsIdentity identity)
                {
                    EntraClaimPolicy.NormalizeClaimsForCallback(identity, settings.SignInAudience);
                }

                return Task.CompletedTask;
            },
        };
    }

    private static string ResolveAuthority(OidcProviderSettings settings)
    {
        return settings.SignInAudience switch
        {
            MicrosoftEntraSignInAudience.SingleTenant => $"https://login.microsoftonline.com/{settings.TenantId}/v2.0",
            MicrosoftEntraSignInAudience.MultiTenant => "https://login.microsoftonline.com/organizations/v2.0",
            MicrosoftEntraSignInAudience.MultiTenantAndPersonal => "https://login.microsoftonline.com/common/v2.0",
            _ => throw new InvalidOperationException($"Unsupported SignInAudience: {settings.SignInAudience}"),
        };
    }
}
