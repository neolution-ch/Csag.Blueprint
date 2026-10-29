namespace Csag.Blueprint.Application.UnitTests.Identity;

using Csag.Blueprint.Application.Claims;

/// <summary>
/// Pins the literal claim names of a service-account token. Issued tokens carry these names, so renaming one makes
/// every outstanding token unreadable — any change here must be a deliberate, breaking decision.
/// </summary>
public sealed class ServiceAccountClaimTypesTests
{
    [Fact]
    public void ClaimTypeValues_AreStable()
    {
        ServiceAccountClaimTypes.Subject.ShouldBe("sub");
        ServiceAccountClaimTypes.Name.ShouldBe("name");
        ServiceAccountClaimTypes.Type.ShouldBe("type");
        ServiceAccountClaimTypes.SessionId.ShouldBe("sid");
        ServiceAccountClaimTypes.ServiceAccountType.ShouldBe("ServiceAccount");
    }
}
