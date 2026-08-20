namespace Csag.Blueprint.Testing.UnitTests.Unit;

/// <summary>
/// Parent side of a deliberately circular object graph with writable navigations on both
/// sides, so AutoFixture's recursion handling is actually exercised (unlike the domain
/// entities, whose back-references are get-only collections).
/// </summary>
public sealed class RecursiveParent
{
    /// <summary>
    /// Gets or sets a regular value property.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the child navigation.
    /// </summary>
    public RecursiveChild? Child { get; set; }
}
