namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Abstract, soft-deletable root of a table-per-concrete-type hierarchy, so it maps to no table of
/// its own.
/// </summary>
public abstract class Asset : ISoftDeletable, IHasInternalName
{
    public Guid Id { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string InternalName { get; set; } = string.Empty;
}
