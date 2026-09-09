namespace Csag.Blueprint.Testing.UnitTests.Unit;

/// <summary>
/// Child side of the circular graph; <see cref="Parent"/> points back to <see cref="RecursiveParent"/>.
/// </summary>
public sealed class RecursiveChild
{
    /// <summary>
    /// Gets or sets a regular value property.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the back-reference completing the cycle.
    /// </summary>
    public RecursiveParent? Parent { get; set; }
}
