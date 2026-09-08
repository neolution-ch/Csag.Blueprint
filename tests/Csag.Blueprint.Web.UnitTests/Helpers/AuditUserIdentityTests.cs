namespace Csag.Blueprint.Web.UnitTests.Helpers;

using System.Reflection;
using System.Security.Claims;
using Csag.Blueprint.Web.Middleware;

/// <summary>
/// Unit tests for <c>AuditUserIdentity.FromPrincipal</c> — the single claims-to-identity mapping both
/// audit write paths (EF enrichment and <c>HttpAuditMiddleware</c>) share. The tests pin the claim
/// priority: mapped <see cref="ClaimTypes"/> first, then the raw <c>sub</c>/<c>name</c> claims a
/// service-account token carries when JWT inbound claim mapping is off.
/// The type is internal and the package declares no <c>InternalsVisibleTo</c>, so it is reached via
/// reflection on the Web assembly.
/// </summary>
public sealed class AuditUserIdentityTests
{
    private static readonly Type AuditUserIdentityType = typeof(TenantMiddleware).Assembly
        .GetType("Csag.Blueprint.Web.Helpers.AuditUserIdentity", throwOnError: true)!;

    private static readonly MethodInfo FromPrincipalMethod = AuditUserIdentityType
        .GetMethod("FromPrincipal", BindingFlags.Public | BindingFlags.Static)!;

    [Fact]
    public void FromPrincipal_HumanUserWithMappedClaims_ReadsAllThreeValues()
    {
        var principal = PrincipalWith(
            new Claim(ClaimTypes.NameIdentifier, "user-id-1"),
            new Claim(ClaimTypes.Email, "alice@example.com"),
            new Claim(ClaimTypes.Name, "Alice Example"));

        var identity = FromPrincipal(principal);

        identity.UserId.ShouldBe("user-id-1");
        identity.Email.ShouldBe("alice@example.com");
        identity.DisplayName.ShouldBe("Alice Example");
    }

    [Fact]
    public void FromPrincipal_ServiceAccountWithRawClaims_FallsBackToSubAndName()
    {
        // A client-credentials token carries the client ID in a raw "sub" claim and the account name in
        // a raw "name" claim, and has no email address.
        var principal = PrincipalWith(
            new Claim("sub", "sa-client-id"),
            new Claim("name", "Reporting Robot"));

        var identity = FromPrincipal(principal);

        identity.UserId.ShouldBe("sa-client-id");
        identity.Email.ShouldBeNull();
        identity.DisplayName.ShouldBe("Reporting Robot");
    }

    [Fact]
    public void FromPrincipal_MappedClaimsWinOverRawClaims()
    {
        // The mapped claims carry the values a human user's session was composed with; an external
        // provider's raw claims must not override them.
        var principal = PrincipalWith(
            new Claim(ClaimTypes.NameIdentifier, "mapped-id"),
            new Claim("sub", "raw-sub"),
            new Claim(ClaimTypes.Name, "Mapped Name"),
            new Claim("name", "Raw Name"));

        var identity = FromPrincipal(principal);

        identity.UserId.ShouldBe("mapped-id");
        identity.DisplayName.ShouldBe("Mapped Name");
    }

    [Fact]
    public void FromPrincipal_NullPrincipal_ReturnsAllNullValues()
    {
        var identity = FromPrincipal(null);

        identity.UserId.ShouldBeNull();
        identity.Email.ShouldBeNull();
        identity.DisplayName.ShouldBeNull();
    }

    [Fact]
    public void FromPrincipal_PrincipalWithoutClaims_ReturnsAllNullValues()
    {
        var identity = FromPrincipal(new ClaimsPrincipal(new ClaimsIdentity()));

        identity.UserId.ShouldBeNull();
        identity.Email.ShouldBeNull();
        identity.DisplayName.ShouldBeNull();
    }

    [Fact]
    public void FromPrincipal_UnauthenticatedIdentity_StillReadsClaims()
    {
        // FromPrincipal reads claims without checking IsAuthenticated; the callers decide whether an
        // anonymous request carries a principal at all.
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Email, "ghost@example.com") }));

        var identity = FromPrincipal(principal);

        identity.Email.ShouldBe("ghost@example.com");
    }

    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    private static (string? UserId, string? Email, string? DisplayName) FromPrincipal(ClaimsPrincipal? user)
    {
        var identity = FromPrincipalMethod.Invoke(null, new object?[] { user })!;
        return (ReadProperty(identity, "UserId"), ReadProperty(identity, "Email"), ReadProperty(identity, "DisplayName"));
    }

    private static string? ReadProperty(object identity, string propertyName) =>
        (string?)AuditUserIdentityType.GetProperty(propertyName)!.GetValue(identity);
}
