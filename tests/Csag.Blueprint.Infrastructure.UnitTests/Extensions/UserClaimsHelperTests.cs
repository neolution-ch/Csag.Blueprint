namespace Csag.Blueprint.Infrastructure.UnitTests.Extensions;

using System.Security.Claims;
using Csag.Blueprint.Infrastructure.Extensions;

/// <summary>
/// Unit tests for <see cref="UserClaimsHelper"/> covering the set/replace semantics of the profile
/// claims, the optional preferred-language claim, the missing-email guard, and pinning the claim
/// types written to the ticket.
/// </summary>
public sealed class UserClaimsHelperTests
{
    private static readonly Guid UserId = new Guid("55555555-0000-0000-0000-000000000001");

    [Fact]
    public void SetUserProfileClaims_AddsIdentityAndPreferenceClaims()
    {
        // Arrange
        var identity = new ClaimsIdentity();
        var user = CreateUser();

        // Act
        identity.SetUserProfileClaims(user);

        // Assert
        identity.FindFirst(ClaimTypes.NameIdentifier).ShouldNotBeNull().Value.ShouldBe(UserId.ToString());
        identity.FindFirst(ClaimTypes.Email).ShouldNotBeNull().Value.ShouldBe("alice@example.com");
        identity.FindFirst(ClaimTypes.Name).ShouldNotBeNull().Value.ShouldBe("Alice Example");

        // Pin the wire-level claim type for the package-owned preference claim: request
        // localization reads this exact string from the ticket.
        identity.FindFirst("PreferredLanguage").ShouldNotBeNull().Value.ShouldBe("de-CH");
    }

    [Fact]
    public void SetUserProfileClaims_ReplacesExistingProfileClaims()
    {
        // Arrange — a stale ticket from before the user edited their profile.
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Email, "old@example.com"));
        identity.AddClaim(new Claim(ClaimTypes.Name, "Old Name"));
        identity.AddClaim(new Claim("PreferredLanguage", "en-GB"));
        var user = CreateUser();

        // Act
        identity.SetUserProfileClaims(user);

        // Assert — each profile claim exists exactly once with the fresh value.
        identity.FindAll(ClaimTypes.NameIdentifier).ShouldHaveSingleItem().Value.ShouldBe(UserId.ToString());
        identity.FindAll(ClaimTypes.Email).ShouldHaveSingleItem().Value.ShouldBe("alice@example.com");
        identity.FindAll(ClaimTypes.Name).ShouldHaveSingleItem().Value.ShouldBe("Alice Example");
        identity.FindAll("PreferredLanguage").ShouldHaveSingleItem().Value.ShouldBe("de-CH");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SetUserProfileClaims_WithMissingPreferredLanguage_RemovesExistingClaim(string? preferredLanguage)
    {
        // Arrange — the user cleared their language preference since the ticket was issued.
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim("PreferredLanguage", "en-GB"));
        var user = CreateUser();
        user.PreferredLanguage = preferredLanguage;

        // Act
        identity.SetUserProfileClaims(user);

        // Assert — the optional claim is removed rather than left stale or written empty.
        identity.FindFirst("PreferredLanguage").ShouldBeNull();
    }

    [Fact]
    public void SetUserProfileClaims_WithNullEmail_Throws()
    {
        // Arrange
        var identity = new ClaimsIdentity();
        var user = CreateUser();
        user.Email = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => identity.SetUserProfileClaims(user));
    }

    [Fact]
    public void SetUserProfileClaims_WithEmptyEmail_Throws()
    {
        // Arrange
        var identity = new ClaimsIdentity();
        var user = CreateUser();
        user.Email = string.Empty;

        // Act & Assert
        Should.Throw<ArgumentException>(() => identity.SetUserProfileClaims(user));
    }

    [Fact]
    public void SetUserProfileClaims_WithDuplicateExistingClaims_RemovesAllDuplicates()
    {
        // Arrange — an identity that already carries two claims of the same profile type.
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(ClaimTypes.Email, "first@example.com"));
        identity.AddClaim(new Claim(ClaimTypes.Email, "second@example.com"));
        var user = CreateUser();

        // Act
        identity.SetUserProfileClaims(user);

        // Assert — every stale duplicate is removed before the fresh value is added, matching the
        // role/permission/tenant helpers, so the identity carries exactly one claim per profile type.
        identity.FindAll(ClaimTypes.Email).ShouldHaveSingleItem().Value.ShouldBe("alice@example.com");
    }

    private static TestUserProfileClaimsSource CreateUser() => new()
    {
        Id = UserId,
        Email = "alice@example.com",
        DisplayName = "Alice Example",
        PreferredLanguage = "de-CH",
    };
}
