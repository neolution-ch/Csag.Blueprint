namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Root of a TPH hierarchy that adopts the contracts whose conventions inspect
/// <see cref="System.Type.GetInterfaces"/>, which also reports interfaces inherited by derived types.
/// </summary>
public abstract class Catalog : IHasInternalName, IHasLocalizedTexts<CatalogText>
{
    public Guid Id { get; set; }

    public string InternalName { get; set; } = string.Empty;

    public ICollection<CatalogText> LocalizedTexts { get; set; } = [];
}
