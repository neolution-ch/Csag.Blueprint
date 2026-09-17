namespace Csag.Blueprint.Infrastructure.UnitTests.Filtering;

using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core permits a global query filter only on the root of an inheritance hierarchy. Registering one
/// per mapped type threw "A filter may only be applied to the root entity type" and made the context
/// unbuildable for any consumer using TPH or TPT.
/// </summary>
public sealed class InheritanceQueryFilterTests
{
    [Fact]
    public void ModelBuilds_ForAHierarchyWhoseRootIsSoftDeletable()
    {
        using var db = new Harness();

        db.Context.Model.FindEntityType(typeof(Document))!.GetDeclaredQueryFilters().Count.ShouldBe(1);
        db.Context.Model.FindEntityType(typeof(Invoice))!.GetDeclaredQueryFilters().ShouldBeEmpty();
    }

    [Fact]
    public void SoftDeleteFilter_AppliesToDerivedTypesThroughTheRoot()
    {
        using var db = new Harness();
        db.Context.Database.EnsureCreated();

        db.Context.Add(new Invoice { Id = Guid.NewGuid(), Number = "live" });
        db.Context.Add(new Invoice { Id = Guid.NewGuid(), Number = "gone", DeletedAt = DateTimeOffset.UtcNow });
        db.Context.Add(new Receipt { Id = Guid.NewGuid(), Reference = "kept" });
        db.Context.SaveChanges();
        db.Context.ChangeTracker.Clear();

        db.Context.Set<Invoice>().Select(i => i.Number).ShouldBe(["live"]);
        db.Context.Set<Document>().Count().ShouldBe(2);
    }

    [Fact]
    public void DeletedAtIndex_IsDefinedOnceForTheWholeHierarchy()
    {
        using var db = new Harness();

        // GetIndexes() includes the ones a derived type inherits, so only the declared indexes show
        // whether the convention defined it once or once per mapped type.
        var declared = db.Context.Model.GetEntityTypes()
            .SelectMany(e => e.GetDeclaredIndexes())
            .Select(i => i.GetDatabaseName())
            .ToList();

        declared.Count(n => n == "IX_Documents_DeletedAt").ShouldBe(1);
    }

    private sealed class Harness : IDisposable
    {
        private readonly SqliteConnection connection;

        public Harness()
        {
            this.connection = new SqliteConnection("Filename=:memory:");
            this.connection.Open();
            this.Context = new InheritanceDbContext(
                new DbContextOptionsBuilder<InheritanceDbContext>().UseSqlite(this.connection).Options);
        }

        public InheritanceDbContext Context { get; }

        public void Dispose()
        {
            this.Context.Dispose();
            this.connection.Dispose();
        }
    }
}
