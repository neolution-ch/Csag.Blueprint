namespace Csag.Blueprint.Infrastructure.Extensions;

using System.Security.Claims;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Extension methods for building and refreshing user profile claims on a <see cref="ClaimsIdentity"/>.
/// Use this helper in all places where authentication tickets are created or refreshed
/// to ensure consistent claim sets across login, OAuth callback, and session refresh flows.
/// </summary>
public static class UserClaimsHelper
{
    /// <summary>
    /// Sets (or replaces) all user profile claims on the given identity.
    /// This includes identity claims (NameIdentifier, Email, Name)
    /// and preference claims (PreferredLanguage).
    /// </summary>
    /// <param name="identity">The claims identity to update.</param>
    /// <param name="user">The user profile source whose data populates the claims.</param>
    public static void SetUserProfileClaims(this ClaimsIdentity identity, IUserProfileClaimsSource user)
    {
        ArgumentException.ThrowIfNullOrEmpty(user.Email);

        ReplaceClaim(identity, ClaimTypes.NameIdentifier, user.Id.ToString());
        ReplaceClaim(identity, ClaimTypes.Email, user.Email);
        ReplaceClaim(identity, ClaimTypes.Name, user.DisplayName);
        ReplaceOptionalClaim(identity, IdentityClaimTypes.PreferredLanguage, user.PreferredLanguage);
    }

    /// <summary>
    /// Replaces a claim with a new value, or adds it if it doesn't exist. All existing claims of
    /// the type are removed first — consistent with the role/permission/tenant helpers — so the
    /// identity always carries exactly one claim of the type.
    /// </summary>
    /// <param name="identity">The claims identity.</param>
    /// <param name="claimType">The claim type.</param>
    /// <param name="value">The new claim value.</param>
    private static void ReplaceClaim(ClaimsIdentity identity, string claimType, string value)
    {
        RemoveClaims(identity, claimType);
        identity.AddClaim(new Claim(claimType, value));
    }

    /// <summary>
    /// Replaces an optional claim. All existing claims of the type are removed first; if the value
    /// is present the claim is re-added, and if it is null or empty the identity ends up without
    /// any claim of the type.
    /// </summary>
    /// <param name="identity">The claims identity.</param>
    /// <param name="claimType">The claim type.</param>
    /// <param name="value">The claim value, or null/empty to remove.</param>
    private static void ReplaceOptionalClaim(ClaimsIdentity identity, string claimType, string? value)
    {
        RemoveClaims(identity, claimType);

        if (!string.IsNullOrEmpty(value))
        {
            identity.AddClaim(new Claim(claimType, value));
        }
    }

    /// <summary>
    /// Removes every claim of the given type from the identity, including duplicates left behind
    /// by earlier ticket builds.
    /// </summary>
    /// <param name="identity">The claims identity.</param>
    /// <param name="claimType">The claim type to remove.</param>
    private static void RemoveClaims(ClaimsIdentity identity, string claimType)
    {
        foreach (var claim in identity.FindAll(claimType).ToList())
        {
            identity.RemoveClaim(claim);
        }
    }
}
