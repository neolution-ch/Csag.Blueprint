namespace Csag.Blueprint.Infrastructure.Tests.Extensions;

using System.Security.Claims;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Infrastructure.Extensions;
using Shouldly;
using Xunit;

/// <summary>
/// Unit tests for <see cref="UserClaimsHelper.SetUserProfileClaims"/>. Used on login, OAuth callback,
/// and session refresh, so the claim set must be consistent and (importantly) a cleared preferred
/// language must remove a previously-set claim rather than leave it stale.
/// </summary>
public sealed class UserClaimsHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SetUserProfileClaims_NullOrEmptyEmail_ThrowsBeforeMutatingIdentity(string? email)
    {
        var identity = new ClaimsIdentity();
        var user = new TestUserProfile { Email = email, DisplayName = "Ada Lovelace" };

        Should.Throw<ArgumentException>(() => identity.SetUserProfileClaims(user));

        identity.Claims.ShouldBeEmpty();
    }

    [Fact]
    public void SetUserProfileClaims_ValidUser_EmitsSingleIdentityClaimEach()
    {
        var identity = new ClaimsIdentity();
        var user = new TestUserProfile
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Email = "ada@example.com",
            DisplayName = "Ada Lovelace",
        };

        identity.SetUserProfileClaims(user);

        identity.FindAll(ClaimTypes.NameIdentifier).Select(c => c.Value)
            .ShouldBe(["11111111-1111-1111-1111-111111111111"]);
        identity.FindAll(ClaimTypes.Email).Select(c => c.Value).ShouldBe(["ada@example.com"]);
        identity.FindAll(ClaimTypes.Name).Select(c => c.Value).ShouldBe(["Ada Lovelace"]);
    }

    [Fact]
    public void SetUserProfileClaims_PreferredLanguagePresent_AddsPreferredLanguageClaim()
    {
        var identity = new ClaimsIdentity();
        var user = new TestUserProfile { Email = "ada@example.com", DisplayName = "Ada", PreferredLanguage = "de-CH" };

        identity.SetUserProfileClaims(user);

        identity.FindFirst(IdentityClaimTypes.PreferredLanguage)!.Value.ShouldBe("de-CH");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SetUserProfileClaims_ClearedPreferredLanguage_RemovesExistingClaim(string? cleared)
    {
        // Simulate a refresh where the user previously had a language set and has since cleared it.
        var identity = new ClaimsIdentity([new Claim(IdentityClaimTypes.PreferredLanguage, "de-CH")]);
        var user = new TestUserProfile { Email = "ada@example.com", DisplayName = "Ada", PreferredLanguage = cleared };

        identity.SetUserProfileClaims(user);

        identity.FindAll(IdentityClaimTypes.PreferredLanguage).ShouldBeEmpty();
    }

    [Fact]
    public void SetUserProfileClaims_CalledTwice_ReplacesRatherThanAccumulates()
    {
        var identity = new ClaimsIdentity();
        identity.SetUserProfileClaims(new TestUserProfile { Email = "old@example.com", DisplayName = "Old Name" });

        identity.SetUserProfileClaims(new TestUserProfile { Email = "new@example.com", DisplayName = "New Name" });

        identity.FindAll(ClaimTypes.Email).Select(c => c.Value).ShouldBe(["new@example.com"]);
        identity.FindAll(ClaimTypes.Name).Select(c => c.Value).ShouldBe(["New Name"]);
    }

    private sealed class TestUserProfile : IUserProfileClaimsSource
    {
        public Guid Id { get; init; }

        public string? Email { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        public string? PreferredLanguage { get; init; }
    }
}
