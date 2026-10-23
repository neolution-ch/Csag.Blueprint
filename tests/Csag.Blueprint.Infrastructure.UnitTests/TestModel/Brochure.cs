namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Entity whose localized texts are mapped as an owned collection.
/// </summary>
public sealed class Brochure : IHasLocalizedTexts<BrochureText>
{
    public Guid Id { get; set; }

    public ICollection<BrochureText> LocalizedTexts { get; set; } = new List<BrochureText>();
}
