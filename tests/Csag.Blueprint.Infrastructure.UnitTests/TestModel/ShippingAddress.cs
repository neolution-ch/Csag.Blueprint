namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Owned by <see cref="Shipment"/>. Implements <see cref="IHasInternalName"/> so the contract
/// conventions are exercised on an owned entity type too.
/// </summary>
public sealed class ShippingAddress : IHasInternalName
{
    public string InternalName { get; set; } = string.Empty;

    public string Street { get; set; } = string.Empty;
}
