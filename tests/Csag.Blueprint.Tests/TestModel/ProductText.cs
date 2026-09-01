namespace Csag.Blueprint.Tests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Localized text rows belonging to <see cref="Product"/>.
/// </summary>
public sealed class ProductText : ILocalizedText
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public string LanguageCode { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
