namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Entity owning a <see cref="ShippingAddress"/> and a collection of <see cref="OrderLine"/>s. The owned
/// address's key is the owner's foreign key, so it shares the owner's <see cref="Guid"/> key value
/// rather than generating its own. Owner and address both implement <see cref="IHasInternalName"/>
/// and share a table, so each gets an <c>InternalName</c> index on that table.
/// </summary>
public sealed class Shipment : IHasInternalName
{
    public Guid Id { get; set; }

    public string InternalName { get; set; } = string.Empty;

    public ShippingAddress Address { get; set; } = new();

    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
}
