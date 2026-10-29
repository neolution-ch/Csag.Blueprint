namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Test entity whose localized texts use a foreign key that is deliberately not named "GadgetId",
/// exercising the foreign key resolution in <c>ConfigureLocalizedTextConventions</c>.
/// </summary>
public sealed class Gadget : IHasLocalizedTexts<GadgetText>
{
    public Guid Id { get; set; }

    public ICollection<GadgetText> LocalizedTexts { get; set; } = [];
}
