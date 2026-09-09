namespace Csag.Blueprint.Tests.Shared.Entities;

using Csag.Blueprint.Domain.Entities;

/// <summary>
/// Minimal concrete closure of <see cref="BlueprintTenant"/> for unit tests.
/// </summary>
public sealed class TestTenant : BlueprintTenant
{
    /// <summary>
    /// Gets the memberships linking users to this <see cref="TestTenant"/>.
    /// The CLR navigation is required by the Blueprint tenant membership entity configuration.
    /// </summary>
    public ICollection<BlueprintTenantMembership<TestUser, TestTenant>> Memberships { get; } = new List<BlueprintTenantMembership<TestUser, TestTenant>>();
}
