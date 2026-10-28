namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Localized text owned by <see cref="Brochure"/>.
/// </summary>
public sealed class BrochureText : ILocalizedText
{
    public string Text { get; set; } = string.Empty;

    public string LanguageCode { get; set; } = string.Empty;
}
