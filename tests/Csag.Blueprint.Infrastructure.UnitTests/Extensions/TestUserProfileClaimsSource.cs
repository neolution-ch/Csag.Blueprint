namespace Csag.Blueprint.Infrastructure.UnitTests.Extensions;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Mutable <see cref="IUserProfileClaimsSource"/> stand-in so each test controls every profile value
/// independently (the Blueprint user entity derives its display name from email/user name).
/// </summary>
public sealed class TestUserProfileClaimsSource : IUserProfileClaimsSource
{
    /// <summary>
    /// Gets or sets the unique user identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the user's display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional preferred language value.
    /// </summary>
    public string? PreferredLanguage { get; set; }
}
