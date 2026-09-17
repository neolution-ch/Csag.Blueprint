namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Soft-deletable entity registered only after <c>base.OnModelCreating</c>, with no <c>DbSet</c> —
/// the shape that silently missed every blueprint convention.
/// </summary>
public sealed class LateNote : ISoftDeletable
{
    public Guid Id { get; set; }

    public string? Body { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}
