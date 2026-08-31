namespace Csag.Blueprint.Infrastructure.Extensions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// String extension methods safe for use in EF Core LINQ queries.
/// EF Core cannot translate <see cref="string.StartsWith(string, System.StringComparison)"/> or
/// <see cref="string.Contains(string, System.StringComparison)"/> overloads to SQL, so the
/// culture-unaware overloads must be used. These wrappers centralise the suppression so individual
/// query call sites stay clean.
/// </summary>
public static class QueryStringExtensions
{
    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> starts with <paramref name="prefix"/>.
    /// Use this inside EF Core LINQ queries instead of <see cref="string.StartsWith(string, System.StringComparison)"/>,
    /// which cannot be translated to SQL.
    /// </summary>
    /// <param name="value">The string to test.</param>
    /// <param name="prefix">The prefix to look for.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> starts with <paramref name="prefix"/>.</returns>
    [SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity", Justification = "StringComparison overloads cannot be translated to SQL by EF Core.")]
    [SuppressMessage("Globalization", "CA1310:Specify StringComparison for correctness", Justification = "StringComparison overloads cannot be translated to SQL by EF Core.")]
    public static bool StartsWithQuery(this string value, string prefix)
        => value.StartsWith(prefix);
}
