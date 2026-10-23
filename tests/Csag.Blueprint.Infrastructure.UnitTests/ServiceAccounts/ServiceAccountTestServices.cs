namespace Csag.Blueprint.Infrastructure.UnitTests.ServiceAccounts;

using Csag.Blueprint.Infrastructure.Authentication;
using Csag.Blueprint.Infrastructure.Enums;
using Csag.Blueprint.Infrastructure.ServiceAccounts;
using Csag.Blueprint.Tests.Shared.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Neolution.Extensions.Caching.Abstractions;

/// <summary>
/// Builds the service-account registration the way an application composes it, over an in-memory database, a mocked
/// cache and a fake clock, so the package's internal wiring is tested through its public registration call.
/// </summary>
internal static class ServiceAccountTestServices
{
    /// <summary>
    /// The signing key the built services use unless a test replaces it.
    /// </summary>
    public const string SigningKey = "UnitTestSigningKeyThatIsAtLeast32CharactersLong";

    /// <summary>
    /// The issuer the built services use.
    /// </summary>
    public const string Issuer = "unit-test-issuer";

    /// <summary>
    /// The audience the built services use.
    /// </summary>
    public const string Audience = "unit-test-audience";

    /// <summary>
    /// The instant the fake clock starts at, far from the wall clock so a check reading the system clock stands out.
    /// </summary>
    public static readonly DateTimeOffset ClockStart = new(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Builds the services.
    /// </summary>
    /// <param name="clock">The clock registered as the <see cref="TimeProvider"/>.</param>
    /// <param name="configure">Changes the settings after the valid defaults are applied.</param>
    /// <param name="after">Registrations made after the package's.</param>
    /// <returns>The service provider.</returns>
    public static ServiceProvider Build(
        FakeTimeProvider? clock = null,
        Action<BlueprintServiceAccountOptions>? configure = null,
        Action<IServiceCollection>? after = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock ?? new FakeTimeProvider(ClockStart));

        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContextFactory<TestDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton(Mock.Of<IDistributedCache<CacheId>>());

        services.AddBlueprintServiceAccountAuthentication<TestDbContext>(options =>
        {
            options.SigningKey = SigningKey;
            options.Issuer = Issuer;
            options.Audience = Audience;
            options.TokenLifetime = TimeSpan.FromHours(1);
            options.MaxFailedAccessAttempts = 5;
            options.LockoutDuration = TimeSpan.FromMinutes(5);
            configure?.Invoke(options);
        });

        after?.Invoke(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Resolves the built options of the service-account bearer scheme.
    /// </summary>
    /// <param name="services">The built services.</param>
    /// <returns>The scheme's options, post-configured.</returns>
    public static JwtBearerOptions GetBearerOptions(this IServiceProvider services)
        => services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(BlueprintAuthenticationSchemes.ServiceAccountToken);

    /// <summary>
    /// Resolves the built options of a policy scheme.
    /// </summary>
    /// <param name="services">The built services.</param>
    /// <param name="scheme">The policy scheme.</param>
    /// <returns>The scheme's options.</returns>
    public static PolicySchemeOptions GetPolicySchemeOptions(this IServiceProvider services, string scheme)
        => services.GetRequiredService<IOptionsMonitor<PolicySchemeOptions>>().Get(scheme);
}
