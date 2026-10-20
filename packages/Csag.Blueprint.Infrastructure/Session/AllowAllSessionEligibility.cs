namespace Csag.Blueprint.Infrastructure.Session;

using System.Diagnostics.CodeAnalysis;
using Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// The <see cref="ISessionEligibility"/> in effect when the application registers none: every account may hold a
/// session.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection.")]
internal sealed class AllowAllSessionEligibility : ISessionEligibility
{
    /// <inheritdoc/>
    public Task<bool> MayHoldSessionAsync(Guid accountId) => Task.FromResult(true);
}
