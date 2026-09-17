namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Soft-deletable entity exposed as a <c>DbSet</c>, so it was discovered before <c>OnModelCreating</c>
/// and always received the conventions. The control for <see cref="LateNote"/>.
/// </summary>
public sealed class EarlyNote : ISoftDeletable
{
    public Guid Id { get; set; }

    public string? Body { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}
