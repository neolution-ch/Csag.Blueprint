namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Tests.Shared.Entities;

/// <summary>
/// Tenant-owned entity with a <see cref="Tenant"/> navigation the application leaves to EF Core's
/// relationship discovery.
/// </summary>
public sealed class Memo : IMustHaveTenant
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public TestTenant? Tenant { get; set; }
}
