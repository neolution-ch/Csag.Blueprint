namespace Csag.Blueprint.Infrastructure.UnitTests.Tenancy;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Infrastructure.TableView;
using Csag.Blueprint.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Unit tests for the UTC <see cref="ITenantTimeZoneProvider"/> fallback that the tenancy and table view
/// registrations add with <c>TryAdd</c>, so an application provider wins whichever order it is registered in.
/// </summary>
public sealed class TenantTimeZoneProviderRegistrationTests
{
    [Fact]
    public async Task Fallback_AnswersUtc()
    {
        var zone = await new UtcTenantTimeZoneProvider().GetTimeZoneAsync(TestContext.Current.CancellationToken);

        zone.ShouldBe(TimeZoneInfo.Utc);
    }

    [Fact]
    public void Registrations_WithoutApplicationProvider_RegisterOneUtcFallback()
    {
        var services = new ServiceCollection();

        services.AddBlueprintTenancyRuntime();
        services.AddBlueprintTableViewCore();

        using var provider = services.BuildServiceProvider();
        services.Count(d => d.ServiceType == typeof(ITenantTimeZoneProvider)).ShouldBe(1);
        provider.GetRequiredService<ITenantTimeZoneProvider>().ShouldBeOfType<UtcTenantTimeZoneProvider>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Registrations_WithApplicationProvider_ResolveIt(bool applicationFirst)
    {
        var services = new ServiceCollection();

        if (applicationFirst)
        {
            services.AddScoped<ITenantTimeZoneProvider, FixedTenantTimeZoneProvider>();
        }

        services.AddBlueprintTenancyRuntime();
        services.AddBlueprintTableViewCore();

        if (!applicationFirst)
        {
            services.AddScoped<ITenantTimeZoneProvider, FixedTenantTimeZoneProvider>();
        }

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantTimeZoneProvider>().ShouldBeOfType<FixedTenantTimeZoneProvider>();
        scope.ServiceProvider.GetRequiredService<ITableViewExecutor>().ShouldNotBeNull();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection.")]
    private sealed class FixedTenantTimeZoneProvider : ITenantTimeZoneProvider
    {
        public ValueTask<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(TimeZoneInfo.Utc);
    }
}
