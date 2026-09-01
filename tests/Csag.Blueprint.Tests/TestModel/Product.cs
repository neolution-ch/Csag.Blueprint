namespace Csag.Blueprint.Tests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Test entity adopting every domain contract that drives a model convention.
/// </summary>
public sealed class Product : IHasLocalizedTexts<ProductText>, ISoftDeletable, IHasActiveRange, IHasInternalName
{
    public Guid Id { get; set; }

    public string InternalName { get; set; } = string.Empty;

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset? ActiveFrom { get; set; }

    public DateTimeOffset? ActiveUntil { get; set; }

    public ICollection<ProductText> LocalizedTexts { get; set; } = [];
}
