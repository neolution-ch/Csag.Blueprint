namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Tests.Shared.Entities;

/// <summary>
/// Entity implementing several blueprint contracts whose mapping the application also configures
/// explicitly, so the explicit configuration must survive the conventions.
/// </summary>
public sealed class Ticket : IMustHaveTenant, ISoftDeletable, IHasInternalName
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public TestTenant? Tenant { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string InternalName { get; set; } = string.Empty;
}
