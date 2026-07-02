namespace Csag.Blueprint.Infrastructure.TableView;

/// <summary>
/// Represents the result of scanning an assembly for table view definitions.
/// </summary>
public sealed class TableViewDefinitionScanResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableViewDefinitionScanResult"/> class.
    /// </summary>
    /// <param name="definitionTypes">The discovered table view definition types.</param>
    public TableViewDefinitionScanResult(IReadOnlyList<Type> definitionTypes)
    {
        this.DefinitionTypes = definitionTypes;
    }

    /// <summary>
    /// Gets the discovered table view definition types.
    /// </summary>
    public IReadOnlyList<Type> DefinitionTypes { get; }
}
