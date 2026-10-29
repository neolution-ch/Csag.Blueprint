namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

/// <summary>A derived type that inherits, rather than introduces, the catalog contracts.</summary>
public sealed class BookCatalog : Catalog
{
    public string? Isbn { get; set; }
}
