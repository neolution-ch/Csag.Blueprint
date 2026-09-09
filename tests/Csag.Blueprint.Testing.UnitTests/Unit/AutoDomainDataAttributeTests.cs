namespace Csag.Blueprint.Testing.UnitTests.Unit;

using AutoFixture;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Testing.Unit;
using Csag.Blueprint.Tests.Shared.Entities;

/// <summary>
/// Unit tests for <see cref="AutoDomainDataAttribute"/> verifying that it resolves domain entity
/// graphs with circular navigations by omitting the recursion point instead of throwing, while
/// still generating regular specimen values like a plain AutoFixture fixture.
/// </summary>
public sealed class AutoDomainDataAttributeTests
{
    [Theory]
    [AutoDomainData]
    public void AutoDomainData_ResolvesUserAndTenantClosures(TestUser user, TestTenant tenant)
    {
        // Assert — the shared closures resolve with their writable properties populated.
        user.ShouldNotBeNull();
        user.Id.ShouldNotBe(Guid.Empty);
        user.Email.ShouldNotBeNullOrEmpty();
        tenant.ShouldNotBeNull();
        tenant.Name.ShouldNotBeNullOrEmpty();

        // The membership navigations are get-only collections, which AutoFixture leaves
        // untouched — that is what keeps the user⇄tenant cycle from ever being entered here.
        user.TenantMemberships.ShouldBeEmpty();
        tenant.Memberships.ShouldBeEmpty();
    }

    [Theory]
    [AutoDomainData]
    public void AutoDomainData_ResolvesTenantMembershipWithBothNavigations(BlueprintTenantMembership<TestUser, TestTenant> membership)
    {
        // Assert — the writable navigations on the join entity are populated one level deep.
        membership.ShouldNotBeNull();
        membership.User.ShouldNotBeNull();
        membership.Tenant.ShouldNotBeNull();
        membership.UserId.ShouldNotBe(Guid.Empty);
        membership.TenantId.ShouldNotBe(Guid.Empty);

        // The back-references from user/tenant to memberships are get-only and stay empty,
        // so the graph terminates instead of cycling back to another membership.
        membership.User.TenantMemberships.ShouldBeEmpty();
        membership.Tenant.Memberships.ShouldBeEmpty();
    }

    [Theory]
    [AutoDomainData]
    public void AutoDomainData_WithWritableCircularNavigations_OmitsTheRecursionPoint(RecursiveParent parent)
    {
        // Assert — one level of the cycle is materialized...
        parent.ShouldNotBeNull();
        parent.Name.ShouldNotBeNullOrEmpty();
        parent.Child.ShouldNotBeNull();
        parent.Child.Name.ShouldNotBeNullOrEmpty();

        // ...and the recursion point is omitted (left at its default) instead of looping forever.
        parent.Child.Parent.ShouldBeNull();
    }

    [Fact]
    public void PlainFixture_WithWritableCircularNavigations_ThrowsInstead()
    {
        // Arrange — an unmodified fixture keeps ThrowingRecursionBehavior.
        var fixture = new Fixture();

        // Act & Assert — the same graph the attribute resolves fine is rejected outright,
        // which is exactly the failure mode AutoDomainDataAttribute exists to remove.
        Should.Throw<ObjectCreationException>(() => fixture.Create<RecursiveParent>());
    }
}
