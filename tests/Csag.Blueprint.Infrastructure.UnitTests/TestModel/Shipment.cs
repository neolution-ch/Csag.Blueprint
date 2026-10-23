namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

/// <summary>
/// Entity owning a <see cref="ShippingAddress"/> and a collection of <see cref="OrderLine"/>s. The owned
/// address's key is the owner's foreign key, so it shares the owner's <see cref="Guid"/> key value
/// rather than generating its own.
/// </summary>
public sealed class Shipment
{
    public Guid Id { get; set; }

    public ShippingAddress Address { get; set; } = new();

    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
}
