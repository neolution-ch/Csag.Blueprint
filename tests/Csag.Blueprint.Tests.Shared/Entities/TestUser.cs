namespace Csag.Blueprint.Tests.Shared.Entities;

using Csag.Blueprint.Domain.Entities;

/// <summary>
/// Minimal concrete closure of <see cref="BlueprintUser"/> for unit tests.
/// </summary>
public sealed class TestUser : BlueprintUser
{
    /// <summary>
    /// Gets the memberships linking this user to one or more <see cref="TestTenant"/> instances.
    /// The CLR navigation is required by the Blueprint tenant membership entity configuration.
    /// </summary>
    public ICollection<BlueprintTenantMembership<TestUser, TestTenant>> TenantMemberships { get; } = new List<BlueprintTenantMembership<TestUser, TestTenant>>();
}
