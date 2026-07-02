namespace Csag.Blueprint.Domain.Entities;

using Csag.Blueprint.Domain.Contracts;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Blueprint-owned canonical role base type. Applications extend this class to add
/// domain-specific role properties.
/// Builds on top of ASP.NET Core Identity's <c>IdentityRole</c>.
/// </summary>
public class BlueprintRole : IdentityRole<Guid>, IAuditable
{
    /// <summary>
    /// Gets or sets the timestamp when the role was created.
    /// Automatically set by <c>AuditableTimestampInterceptor</c> on insert.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the role was last updated.
    /// Automatically set by <c>AuditableTimestampInterceptor</c> on update.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
