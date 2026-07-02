namespace Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Provides the minimal user profile data required to populate identity claims.
/// </summary>
public interface IUserProfileClaimsSource
{
    /// <summary>
    /// Gets the unique user identifier.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets the user's email address.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Gets the user's display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets an optional preferred language value.
    /// </summary>
    string? PreferredLanguage { get; }
}
