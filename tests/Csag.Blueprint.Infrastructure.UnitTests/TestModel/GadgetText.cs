namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Localized text rows belonging to <see cref="Gadget"/>, keyed by an unconventional foreign key name.
/// </summary>
public sealed class GadgetText : ILocalizedText
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Gadget Owner { get; set; } = null!;

    public string LanguageCode { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
