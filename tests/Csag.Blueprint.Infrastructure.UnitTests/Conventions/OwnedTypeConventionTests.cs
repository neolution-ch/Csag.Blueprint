namespace Csag.Blueprint.Infrastructure.UnitTests.Conventions;

using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Owned entity types are part of the finalized model the conventions walk, but they cannot be
/// configured through <c>ModelBuilder.Entity(Type)</c>, carry no query filters of their own, and take
/// their key from the owner.
/// </summary>
public sealed class OwnedTypeConventionTests
{
    [Fact]
    public void Model_Builds_WhenAnEntityOwnsAType()
    {
        using var context = Build();

        context.Model.FindEntityType(typeof(ShippingAddress)).ShouldNotBeNull();
    }

    [Fact]
    public void OwnedTypeKey_GetsNoSequentialDefault()
    {
        using var context = Build();

        context.Model.FindEntityType(typeof(ShippingAddress))!
            .FindPrimaryKey()!.Properties.ShouldHaveSingleItem()
            .GetDefaultValueSql()
            .ShouldBeNull();
    }

    [Fact]
    public void OwnerKey_StillGetsTheSequentialDefault()
    {
        using var context = Build();

        context.Model.FindEntityType(typeof(Shipment))!
            .FindProperty(nameof(Shipment.Id))!
            .GetDefaultValueSql()
            .ShouldBe("NEWSEQUENTIALID()");
    }

    [Fact]
    public void ContractConventions_ApplyToAnOwnedType()
    {
        using var context = Build();

        context.Model.FindEntityType(typeof(ShippingAddress))!
            .FindProperty(nameof(ShippingAddress.InternalName))!
            .GetMaxLength()
            .ShouldBe(200);
    }

    [Fact]
    public void OwnedCollectionElement_ImplementingISoftDeletable_GetsNoQueryFilterOfItsOwn()
    {
        using var context = Build();

        context.Model.FindEntityType(typeof(OrderLine))!.GetDeclaredQueryFilters().ShouldBeEmpty();
    }

    [Fact]
    public void OwnedLocalizedTexts_GetTheContractConstraintsAndPerLanguageUniqueIndex()
    {
        using var context = Build();

        var texts = context.Model.FindEntityType(typeof(BrochureText))!;

        texts.FindProperty(nameof(BrochureText.LanguageCode))!.GetMaxLength().ShouldBe(10);
        texts.GetIndexes().ShouldContain(i => i.IsUnique
            && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "BrochureId", nameof(BrochureText.LanguageCode) }));
    }

    [Fact]
    public void OwnerAndOwnedType_SharingATable_GetDistinctIndexNames()
    {
        using var context = Build();

        var script = context.Database.GenerateCreateScript();

        script.ShouldContain("[IX_Shipment_InternalName]");
        script.ShouldContain("[IX_Shipment_Address_InternalName]");
    }

    // SQL Server, because the key convention only applies on that provider. Building a model opens no
    // connection, so the server is never reached.
    private static OwnedTypeDbContext Build()
        => new(new DbContextOptionsBuilder<OwnedTypeDbContext>()
            .UseSqlServer("Server=nowhere;Database=owned-types;Trusted_Connection=True;")
            .Options);
}
