namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Abstract localized-text root of a table-per-concrete-type hierarchy, so it maps to no table of its own.
/// </summary>
public abstract class Caption : ILocalizedText
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public string Text { get; set; } = string.Empty;

    public string LanguageCode { get; set; } = string.Empty;
}
