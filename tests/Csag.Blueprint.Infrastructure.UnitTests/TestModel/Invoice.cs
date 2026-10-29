namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

/// <summary>A derived type inheriting the root's soft-delete filter.</summary>
public sealed class Invoice : Document
{
    public string? Number { get; set; }
}
