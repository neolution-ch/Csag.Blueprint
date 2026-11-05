namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// The ISO 3166-1 alpha-2 country codes that the application accepts.
/// </summary>
public interface ICountryCatalog
{
    /// <summary>
    /// Gets all accepted country codes in uppercase, sorted in ordinal order, each code one time only.
    /// </summary>
    IReadOnlyList<string> Codes { get; }

    /// <summary>
    /// Determines whether the catalog contains a country code.
    /// </summary>
    /// <param name="code">The value to check. The comparison is case-sensitive.</param>
    /// <returns><see langword="true"/> if <see cref="Codes"/> contains the value; otherwise <see langword="false"/>.</returns>
    bool Contains(string? code);
}
