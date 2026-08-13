namespace Csag.Blueprint.Web.Extensions.Oidc;

using System;
using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

/// <summary>
/// Base class for OIDC provider profiles that applies the configuration common to every provider
/// (authorization-code + PKCE flow, external sign-in scheme, scopes, discovery metadata) and leaves
/// provider-specific policy to the derived profile.
/// </summary>
public abstract class OidcProviderProfileBase : IOidcProviderProfile
{
    /// <inheritdoc/>
    public abstract void Configure(OpenIdConnectOptions options, OidcProviderSettings settings);

    /// <summary>
    /// Applies the OIDC configuration shared by all providers. Only enabled providers reach this code,
    /// so <see cref="OidcProviderSettings.ClientId"/> / <see cref="OidcProviderSettings.ClientSecret"/>
    /// are guaranteed non-null by validation.
    /// </summary>
    protected static void ApplyCommon(OpenIdConnectOptions options, OidcProviderSettings settings)
    {
        options.ClientId = settings.ClientId!;
        options.ClientSecret = settings.ClientSecret!;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = false;

        // Hand the external identity to the shared callback endpoint via the Identity external cookie.
        options.SignInScheme = IdentityConstants.ExternalScheme;

        options.RequireHttpsMetadata = settings.RequireHttpsMetadata;
        options.GetClaimsFromUserInfoEndpoint = settings.GetClaimsFromUserInfoEndpoint;

        // Force the provider's account chooser on every sign-in instead of silently reusing an existing
        // session (prompt=select_account by default). Set on the options so it is the baseline for every
        // challenge; applies to all provider profiles.
        if (!string.IsNullOrWhiteSpace(settings.Prompt))
        {
            options.Prompt = settings.Prompt;
        }

        if (!string.IsNullOrWhiteSpace(settings.MetadataAddress))
        {
            options.MetadataAddress = settings.MetadataAddress;
        }

        options.Scope.Clear();
        foreach (var scope in settings.Scopes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            options.Scope.Add(scope);
        }
    }

    /// <summary>
    /// Applies the generic issuer-validation knobs (used by profiles that validate against the
    /// discovery-derived issuer). Entra manages issuer validation itself and does not call this.
    /// </summary>
    protected static void ApplyIssuerValidation(OpenIdConnectOptions options, OidcProviderSettings settings)
    {
        if (!settings.ValidateIssuer)
        {
            // Issuer validation is disabled only when an operator explicitly opts in via configuration
            // (e.g. a provider that legitimately emits a per-request issuer). The default is true.
#pragma warning disable CA5404 // Do not disable token validation checks
            options.TokenValidationParameters.ValidateIssuer = false;
#pragma warning restore CA5404
        }
        else if (settings.ValidIssuers is { Count: > 0 })
        {
            options.TokenValidationParameters.ValidIssuers = settings.ValidIssuers;
        }
        else
        {
            // Keep the single discovery-derived issuer as the only valid issuer (default behavior).
        }
    }
}
