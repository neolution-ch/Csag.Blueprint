namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Soft-deletable owned collection element of <see cref="Shipment"/>.
/// </summary>
public sealed class OrderLine : ISoftDeletable
{
    public Guid Id { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
