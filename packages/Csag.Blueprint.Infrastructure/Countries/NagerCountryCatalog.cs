namespace Csag.Blueprint.Infrastructure.Countries;

using System.Collections.Frozen;
using Csag.Blueprint.Application.Abstractions.Services;
using Nager.Country;

/// <summary>
/// Country catalog with the codes of the Nager.Country library.
/// </summary>
/// <remarks>
/// The list does not come from <c>CultureInfo</c> or <c>RegionInfo</c>: that list is not complete,
/// and it changes with the ICU data of the host. The lookup of Nager.Country also accepts lowercase
/// values, alpha-3 codes and numeric values. <see cref="Contains"/> accepts only the alpha-2 codes
/// of the list.
/// </remarks>
public sealed class NagerCountryCatalog : ICountryCatalog
{
    private readonly FrozenSet<string> lookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="NagerCountryCatalog"/> class.
    /// </summary>
    public NagerCountryCatalog()
    {
        var codeArray = new CountryProvider()
            .GetCountries()
            .Select(country => country.Alpha2Code.ToString())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        this.Codes = Array.AsReadOnly(codeArray);
        this.lookup = codeArray.ToFrozenSet(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Codes { get; }

    /// <inheritdoc />
    public bool Contains(string? code) => code is not null && this.lookup.Contains(code);
}
