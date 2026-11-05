namespace Csag.Blueprint.Infrastructure.UnitTests.Countries;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Countries;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="Infrastructure.Countries.ServiceCollectionExtensions.AddBlueprintCountries"/>.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBlueprintCountries_RegistersNagerCatalogAsSingleton()
    {
        var services = new ServiceCollection().AddBlueprintCountries();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<ICountryCatalog>();

        first.ShouldBeOfType<NagerCountryCatalog>();
        provider.GetRequiredService<ICountryCatalog>().ShouldBeSameAs(first);
    }

    [Fact]
    public void AddBlueprintCountries_CatalogRegisteredBefore_KeepsThatCatalog()
    {
        var custom = new FixedCountryCatalog();
        var services = new ServiceCollection()
            .AddSingleton<ICountryCatalog>(custom)
            .AddBlueprintCountries();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICountryCatalog>().ShouldBeSameAs(custom);
    }

    private sealed class FixedCountryCatalog : ICountryCatalog
    {
        public IReadOnlyList<string> Codes => ["CH"];

        public bool Contains(string? code) => code == "CH";
    }
}
