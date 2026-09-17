namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>Localized texts owned by the <see cref="Catalog"/> hierarchy root.</summary>
public sealed class CatalogText : ILocalizedText
{
    public Guid Id { get; set; }

    public Guid CatalogId { get; set; }

    public string LanguageCode { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
