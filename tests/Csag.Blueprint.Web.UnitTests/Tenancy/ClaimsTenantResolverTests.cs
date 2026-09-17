namespace Csag.Blueprint.Web.UnitTests.Tenancy;

using System.Security.Claims;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Web.Tenancy;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Unit tests for <see cref="ClaimsTenantResolver"/>, the blueprint's default tenant-addressing
/// implementation. These pin the contract the resolver seam documents: a tenant is returned only for
/// an authenticated principal carrying a parseable <c>TenantId</c> claim, and every other case returns
/// <see langword="null"/> rather than throwing — "no tenant context" is a normal state, not an error.
/// </summary>
public sealed class ClaimsTenantResolverTests
{
    private readonly ClaimsTenantResolver resolver = new();

    [Fact]
    public async Task AuthenticatedWithTenantClaim_ReturnsTenantIdAsync()
    {
        var tenantId = Guid.NewGuid();
        var context = CreateContext(authenticated: true, tenantIdClaim: tenantId.ToString());

        var resolved = await this.resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        resolved.ShouldBe(tenantId);
    }

    [Fact]
    public async Task Anonymous_ReturnsNullAsync()
    {
        // Anonymous endpoints (sign-in, onboarding links, health checks) all run without a principal.
        // They must not throw.
        var context = CreateContext(authenticated: false, tenantIdClaim: Guid.NewGuid().ToString());

        var resolved = await this.resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        resolved.ShouldBeNull();
    }

    [Fact]
    public async Task AuthenticatedWithoutTenantClaim_ReturnsNullAsync()
    {
        // A platform-scope administrator who belongs to no tenant is a legitimate state.
        var context = CreateContext(authenticated: true, tenantIdClaim: null);

        var resolved = await this.resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        resolved.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000")]
    public async Task AuthenticatedWithUnparseableTenantClaim_ReturnsNullAsync(string claimValue)
    {
        // Fail closed: an unreadable claim must not leak into TenantContext, because the query filters
        // would then be scoped to a tenant nobody verified.
        var context = CreateContext(authenticated: true, tenantIdClaim: claimValue);

        var resolved = await this.resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        resolved.ShouldBeNull();
    }

    [Fact]
    public async Task EmptyGuidClaim_ResolvesToEmptyGuidAsync()
    {
        // Guid.Empty parses, so it resolves. It never matches a real tenant row, so the filters still
        // yield nothing — documenting the behaviour rather than asserting it is desirable.
        var context = CreateContext(authenticated: true, tenantIdClaim: Guid.Empty.ToString());

        var resolved = await this.resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        resolved.ShouldBe(Guid.Empty);
    }

    [Fact]
    public async Task NullContext_ThrowsAsync()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await this.resolver.ResolveAsync(null!, TestContext.Current.CancellationToken));
    }

    private static DefaultHttpContext CreateContext(bool authenticated, string? tenantIdClaim)
    {
        var claims = new List<Claim>();
        if (tenantIdClaim is not null)
        {
            claims.Add(new Claim(IdentityClaimTypes.TenantId, tenantIdClaim));
        }

        // An identity with a non-null authentication type reports IsAuthenticated == true; passing null
        // is how ClaimsIdentity models an unauthenticated principal.
        var identity = authenticated
            ? new ClaimsIdentity(claims, authenticationType: "Test")
            : new ClaimsIdentity(claims);

        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }
}
