namespace Csag.Blueprint.Application.UnitTests.Identity;

using Csag.Blueprint.Application.Claims;

/// <summary>
/// Pins the literal claim type strings. These values are baked into issued authentication
/// tickets, so renaming one silently orphans the claims in existing sessions — any change
/// here must be a deliberate, breaking decision.
/// </summary>
public sealed class IdentityClaimTypesTests
{
    [Fact]
    public void ClaimTypeValues_AreStable()
    {
        IdentityClaimTypes.TenantId.ShouldBe("TenantId");
        IdentityClaimTypes.Permission.ShouldBe("Permission");
        IdentityClaimTypes.PreferredLanguage.ShouldBe("PreferredLanguage");
    }
}
