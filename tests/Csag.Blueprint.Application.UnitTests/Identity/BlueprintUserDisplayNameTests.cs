namespace Csag.Blueprint.Application.UnitTests.Identity;

using Csag.Blueprint.Domain.Entities;

/// <summary>
/// Tests the <see cref="BlueprintUser.DisplayName"/> fallback chain: email first, then user
/// name, then empty string — never null, so callers can render it without a guard.
/// </summary>
public sealed class BlueprintUserDisplayNameTests
{
    [Fact]
    public void DisplayName_PrefersEmailOverUserName()
    {
        var user = new BlueprintUser { Email = "alice@example.com", UserName = "alice" };

        user.DisplayName.ShouldBe("alice@example.com");
    }

    [Fact]
    public void DisplayName_FallsBackToUserName_WhenEmailIsNull()
    {
        var user = new BlueprintUser { UserName = "alice" };

        user.DisplayName.ShouldBe("alice");
    }

    [Fact]
    public void DisplayName_IsEmpty_WhenEmailAndUserNameAreNull()
    {
        var user = new BlueprintUser();

        user.DisplayName.ShouldBe(string.Empty);
    }
}
