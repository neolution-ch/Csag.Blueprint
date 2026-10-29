namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Root of a TPH hierarchy that adopts <see cref="ISoftDeletable"/>, so the soft-delete filter has a
/// legal place to live.
/// </summary>
public abstract class Document : ISoftDeletable
{
    public Guid Id { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}
