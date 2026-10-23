namespace Csag.Blueprint.Infrastructure.UnitTests.Conventions;

using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// <c>Type.GetInterfaces()</c> reports interfaces inherited from base classes, so every derived type in
/// a hierarchy re-reports its base's contracts. Configuring a localized-text relationship again from the
/// derived type looks for a foreign key whose principal is the derived CLR type — which does not exist,
/// because the relationship belongs to the base — and the convention threw:
/// "Cannot configure localized texts for 'BookCatalog': no foreign key from 'CatalogText' back to it was
/// found". Only the type introducing a contract is configured now.
/// </summary>
public sealed class InheritedContractConventionTests
{
    [Fact]
    public void ModelBuilds_ForAHierarchyWhoseRootOwnsLocalizedTexts()
    {
        using var db = new Harness();

        db.Context.Model.FindEntityType(typeof(BookCatalog)).ShouldNotBeNull();
    }

    [Fact]
    public void SchemaCreationSucceeds_ForTheHierarchy()
    {
        using var db = new Harness();

        Should.NotThrow(() => db.Context.Database.EnsureCreated());
    }

    [Fact]
    public void LocalizedTextRelationship_IsConfiguredOnceAgainstTheRoot()
    {
        using var db = new Harness();

        var texts = db.Context.Model.FindEntityType(typeof(CatalogText))!;
        var unique = texts.GetDeclaredIndexes().Where(i => i.IsUnique).ToList();

        unique.Count.ShouldBe(1);
        unique[0].GetDatabaseName().ShouldBe("IX_CatalogTexts_CatalogId_LanguageCode_Unique");
        texts.GetForeignKeys().ShouldContain(fk => fk.PrincipalEntityType.ClrType == typeof(Catalog));
    }

    [Fact]
    public void InternalNameIndex_IsDeclaredOnceForTheWholeHierarchy()
    {
        using var db = new Harness();

        db.Context.Model.GetEntityTypes()
            .SelectMany(e => e.GetDeclaredIndexes())
            .Count(i => i.GetDatabaseName() == "IX_Catalogs_InternalName")
            .ShouldBe(1);
    }

    private sealed class Harness : IDisposable
    {
        private readonly SqliteConnection connection;

        public Harness()
        {
            this.connection = new SqliteConnection("Filename=:memory:");
            this.connection.Open();
            this.Context = new CatalogHierarchyDbContext(
                new DbContextOptionsBuilder<CatalogHierarchyDbContext>().UseSqlite(this.connection).Options);
        }

        public CatalogHierarchyDbContext Context { get; }

        public void Dispose()
        {
            this.Context.Dispose();
            this.connection.Dispose();
        }
    }
}
