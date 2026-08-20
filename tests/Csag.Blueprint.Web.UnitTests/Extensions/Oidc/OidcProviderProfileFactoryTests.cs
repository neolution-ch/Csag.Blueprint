namespace Csag.Blueprint.Web.UnitTests.Extensions.Oidc;

using Csag.Blueprint.Web.Extensions.Oidc;
using Csag.Blueprint.Web.Options.Api.Security.OAuth;

/// <summary>
/// Unit tests for <see cref="OidcProviderProfileFactory"/> — the mapping from the configured
/// <see cref="OidcProviderProfile"/> kind to its stateless implementation.
/// </summary>
public sealed class OidcProviderProfileFactoryTests
{
    [Fact]
    public void For_Google_ReturnsGoogleProfile()
    {
        OidcProviderProfileFactory.For(OidcProviderProfile.Google).ShouldBeOfType<GoogleOidcProfile>();
    }

    [Fact]
    public void For_Entra_ReturnsEntraProfile()
    {
        OidcProviderProfileFactory.For(OidcProviderProfile.Entra).ShouldBeOfType<EntraOidcProfile>();
    }

    [Fact]
    public void For_Generic_ReturnsGenericProfile()
    {
        OidcProviderProfileFactory.For(OidcProviderProfile.Generic).ShouldBeOfType<GenericOidcProfile>();
    }

    [Fact]
    public void For_UnknownProfileKind_FallsBackToGenericProfile()
    {
        // The switch treats every unrecognized value as Generic rather than throwing; a bogus value in
        // configuration therefore degrades to the standards-compliant behavior.
        OidcProviderProfileFactory.For((OidcProviderProfile)999).ShouldBeOfType<GenericOidcProfile>();
    }
}
