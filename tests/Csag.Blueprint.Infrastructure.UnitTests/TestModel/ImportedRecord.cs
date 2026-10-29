namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

/// <summary>
/// Entity whose <see cref="Guid"/> key the application always assigns itself, declared with
/// <c>ValueGeneratedNever()</c>.
/// </summary>
public sealed class ImportedRecord
{
    public Guid Id { get; set; }
}
