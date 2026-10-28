namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Tenant-owned entity choosing its tenant delete behavior with an attribute rather than the fluent API.
/// </summary>
public sealed class Voucher : IMustHaveTenant
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    [DeleteBehavior(DeleteBehavior.Cascade)]
    public TestTenant? Tenant { get; set; }
}
