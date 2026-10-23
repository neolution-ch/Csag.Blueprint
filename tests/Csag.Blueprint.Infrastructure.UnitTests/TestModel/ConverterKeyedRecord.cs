namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

/// <summary>
/// Entity whose <see cref="Guid"/> key the application stores through a value converter, so its column
/// is not a <c>uniqueidentifier</c>.
/// </summary>
public sealed class ConverterKeyedRecord
{
    public Guid Id { get; set; }
}
