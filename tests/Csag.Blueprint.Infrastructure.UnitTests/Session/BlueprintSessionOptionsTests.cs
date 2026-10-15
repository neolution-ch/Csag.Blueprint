namespace Csag.Blueprint.Infrastructure.UnitTests.Session;

using Csag.Blueprint.Infrastructure.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Unit tests for the startup validation of <see cref="BlueprintSessionOptions"/> that
/// <c>AddBlueprintSessionInfrastructure</c> registers.
/// </summary>
public sealed class BlueprintSessionOptionsTests
{
    [Fact]
    public void Defaults_RunTheExpiredSessionCleanupHourly()
    {
        var options = new BlueprintSessionOptions();

        options.ExpiredSessionCleanup.Enabled.ShouldBeTrue();
        options.ExpiredSessionCleanup.Interval.ShouldBe(TimeSpan.FromHours(1));
        options.ClientIpHeader.ShouldBeNull();
    }

    [Fact]
    public void ValidOptions_PassValidation()
    {
        using var services = SessionTestServices.Build(after: s => s.Configure<BlueprintSessionOptions>(o => o.ClientIpHeader = "X-Client-IP"));

        services.GetRequiredService<IOptions<BlueprintSessionOptions>>().Value.Lifetime.ShouldBe(SessionTestServices.Lifetime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3_600_000)]
    [InlineData(999)]
    public void ALifetimeShorterThanOneSecond_FailsValidation(int milliseconds)
    {
        // The ticket's expiry is kept to whole seconds, so a shorter session could be expired as it is issued.
        using var services = SessionTestServices.Build(after: s => s.Configure<BlueprintSessionOptions>(o => o.Lifetime = TimeSpan.FromMilliseconds(milliseconds)));

        var exception = Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptions<BlueprintSessionOptions>>().Value);

        exception.Message.ShouldContain("Lifetime must be at least one second");
    }

    [Fact]
    public void AOneSecondLifetime_PassesValidation()
    {
        using var services = SessionTestServices.Build(after: s => s.Configure<BlueprintSessionOptions>(o => o.Lifetime = TimeSpan.FromSeconds(1)));

        services.GetRequiredService<IOptions<BlueprintSessionOptions>>().Value.Lifetime.ShouldBe(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ABlankClientIpHeader_FailsValidation()
    {
        using var services = SessionTestServices.Build(after: s => s.Configure<BlueprintSessionOptions>(o => o.ClientIpHeader = " "));

        Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptions<BlueprintSessionOptions>>().Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    [InlineData((24 * 60 * 60) + 1)]
    public void AnEnabledCleanupWithAnIntervalOutOfRange_FailsValidation(int seconds)
    {
        using var services = SessionTestServices.Build(after: s => s.Configure<BlueprintSessionOptions>(o =>
            o.ExpiredSessionCleanup.Interval = TimeSpan.FromSeconds(seconds)));

        var exception = Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptions<BlueprintSessionOptions>>().Value);

        exception.Message.ShouldContain("ExpiredSessionCleanup.Interval");
    }

    [Fact]
    public void ADisabledCleanup_IgnoresTheInterval()
    {
        using var services = SessionTestServices.Build(after: s => s.Configure<BlueprintSessionOptions>(o =>
        {
            o.ExpiredSessionCleanup.Enabled = false;
            o.ExpiredSessionCleanup.Interval = TimeSpan.Zero;
        }));

        Should.NotThrow(() => services.GetRequiredService<IOptions<BlueprintSessionOptions>>().Value);
    }
}
