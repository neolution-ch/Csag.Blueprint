namespace Csag.Blueprint.Domain.Entities;

using Csag.Blueprint.Domain.Contracts;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Blueprint-owned canonical user base type. Applications extend this class to add
/// domain-specific user properties (e.g., FirstName, LastName, avatar URL).
/// Builds on top of ASP.NET Core Identity so that <c>UserManager</c>
/// and <c>SignInManager</c> continue to work transparently.
/// </summary>
public class BlueprintUser : IdentityUser<Guid>, IAuditable, IUserProfileClaimsSource
{
    /// <summary>
    /// Gets or sets the user's preferred language code (e.g., "de-CH", "en-GB").
    /// Null means no explicit preference — fall back to Accept-Language header, then default language.
    /// </summary>
    public string? PreferredLanguage { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the user was created.
    /// Automatically set by <c>AuditableTimestampInterceptor</c> on insert.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the user was last updated.
    /// Automatically set by <c>AuditableTimestampInterceptor</c> on update.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Gets the user's display name.
    /// Override in derived types to compose from first name, last name, etc.
    /// </summary>
    public virtual string DisplayName => this.Email ?? this.UserName ?? string.Empty;
}
