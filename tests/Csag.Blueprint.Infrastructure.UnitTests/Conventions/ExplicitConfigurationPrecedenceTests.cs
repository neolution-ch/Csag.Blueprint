namespace Csag.Blueprint.Infrastructure.UnitTests.Conventions;

using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Infrastructure.Database;
using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// The blueprint conventions run once the model is complete, which is after the application's own
/// <c>OnModelCreating</c>. They must still behave like conventions: whatever the application configured
/// explicitly wins, and only what it left unconfigured is filled in.
/// </summary>
public sealed class ExplicitConfigurationPrecedenceTests
{
    [Fact]
    public void ExplicitMaxLength_WinsOverTheInternalNameConvention()
    {
        using var context = Build();

        TicketType(context).FindProperty(nameof(Ticket.InternalName))!.GetMaxLength()
            .ShouldBe(ExplicitConfigurationDbContext.TicketInternalNameMaxLength);
    }

    [Fact]
    public void InternalNameConvention_StillMakesTheColumnRequired()
    {
        using var context = Build();

        TicketType(context).FindProperty(nameof(Ticket.InternalName))!.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void ExplicitIndexName_WinsOverTheSoftDeleteIndexName()
    {
        using var context = Build();

        TicketType(context).GetDeclaredIndexes()
            .Single(i => i.Properties.Single().Name == nameof(Ticket.DeletedAt))
            .GetDatabaseName()
            .ShouldBe(ExplicitConfigurationDbContext.TicketDeletedAtIndexName);
    }

    [Fact]
    public void ExplicitTenantRelationship_KeepsItsNavigationAndDeleteBehavior()
    {
        using var context = Build();

        var foreignKey = TicketType(context).GetForeignKeys()
            .Where(fk => fk.Properties.Single().Name == nameof(IMustHaveTenant.TenantId))
            .ShouldHaveSingleItem();

        foreignKey.DependentToPrincipal!.Name.ShouldBe(nameof(Ticket.Tenant));
        foreignKey.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }

    [Fact]
    public void DeleteBehaviorAttribute_WinsOverTheTenantRelationshipDefault()
    {
        using var context = Build();

        context.Model.FindEntityType(typeof(Voucher))!.GetForeignKeys()
            .ShouldHaveSingleItem()
            .DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }

    [Theory]
    [InlineData(typeof(StringKeyedRecord))]
    [InlineData(typeof(ConverterKeyedRecord))]
    [InlineData(typeof(BinaryKeyedRecord))]
    public void KeyStoredThroughAValueConverter_GetsNoSequentialDefault(Type entityType)
    {
        using var context = Build();

        // NEWSEQUENTIALID() is only valid as the default of a uniqueidentifier column.
        context.Model.FindEntityType(entityType)!
            .FindProperty("Id")!
            .GetDefaultValueSql()
            .ShouldBeNull();
    }

    [Fact]
    public void KeyDeclaredValueGeneratedNever_GetsNoSequentialDefault()
    {
        using var context = Build();

        context.Model.FindEntityType(typeof(ImportedRecord))!
            .FindProperty(nameof(ImportedRecord.Id))!
            .GetDefaultValueSql()
            .ShouldBeNull();
    }

    [Fact]
    public void QueryFilters_AreStillApplied_ToAnExplicitlyConfiguredEntity()
    {
        using var context = Build();

        TicketType(context).GetDeclaredQueryFilters()
            .Select(f => f.Key)
            .ShouldBe([BlueprintQueryFilters.Tenant, BlueprintQueryFilters.SoftDelete], ignoreOrder: true);
    }

    // SQL Server, so the provider-specific key convention runs too. Building a model opens no
    // connection, so the server is never reached.
    private static ExplicitConfigurationDbContext Build()
        => new(new DbContextOptionsBuilder<ExplicitConfigurationDbContext>()
            .UseSqlServer("Server=nowhere;Database=explicit-configuration;Trusted_Connection=True;")
            .Options);

    private static IEntityType TicketType(DbContext context) => context.Model.FindEntityType(typeof(Ticket))!;
}
