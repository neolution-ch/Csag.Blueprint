namespace Csag.Blueprint.Infrastructure.UnitTests.Extensions;

using System.Security.Claims;
using Csag.Blueprint.Infrastructure.Extensions;

/// <summary>
/// Unit tests for <see cref="TenantClaimsHelper"/> verifying the identity always carries exactly one
/// tenant claim and pinning the claim type and value format written to the ticket.
/// </summary>
public sealed class TenantClaimsHelperTests
{
    [Fact]
    public void SetTenantClaim_AddsSingleTenantClaimWithPinnedTypeAndFormat()
    {
        // Arrange
        var identity = new ClaimsIdentity();
        var tenantId = new Guid("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE");

        // Act
        identity.SetTenantClaim(tenantId);

        // Assert — pin the wire-level claim type and the "D" lowercase GUID format: tenant
        // resolution middleware parses this exact shape from the ticket.
        var claim = identity.Claims.ShouldHaveSingleItem();
        claim.Type.ShouldBe("TenantId");
        claim.Value.ShouldBe("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    }

    [Fact]
    public void SetTenantClaim_ReplacesAllExistingTenantClaims()
    {
        // Arrange — even a corrupted identity carrying multiple tenant claims is normalized.
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim("TenantId", Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim("TenantId", Guid.NewGuid().ToString()));
        var newTenantId = Guid.NewGuid();

        // Act
        identity.SetTenantClaim(newTenantId);

        // Assert
        var claim = identity.FindAll("TenantId").ShouldHaveSingleItem();
        claim.Value.ShouldBe(newTenantId.ToString());
    }

    [Fact]
    public void SetTenantClaim_LeavesUnrelatedClaimsIntact()
    {
        // Arrange
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(ClaimTypes.Email, "alice@example.com"));

        // Act
        identity.SetTenantClaim(Guid.NewGuid());

        // Assert
        identity.FindFirst(ClaimTypes.Email).ShouldNotBeNull();
    }

    [Fact]
    public void SetTenantClaim_WithNullIdentity_Throws()
    {
        Should.Throw<ArgumentNullException>(() => TenantClaimsHelper.SetTenantClaim(null!, Guid.NewGuid()));
    }
}
