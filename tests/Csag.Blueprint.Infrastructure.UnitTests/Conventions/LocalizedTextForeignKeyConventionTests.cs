namespace Csag.Blueprint.Infrastructure.UnitTests.Conventions;

using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The localization and key conventions must cope with models that do not follow the
/// "&lt;Owner&gt;Id" foreign key naming, and must leave explicitly configured key defaults alone.
/// </summary>
public sealed class LocalizedTextForeignKeyConventionTests
{
    [Fact]
    public void UniqueIndex_IsCreated_ForAForeignKeyThatIsNotNamedAfterTheOwner()
    {
        using var db = new ConventionProbe();

        var textType = db.Context.Model.FindEntityType(typeof(GadgetText))!;
        var unique = textType.GetIndexes().Single(i => i.IsUnique);

        unique.Properties.Select(p => p.Name).ShouldBe([nameof(GadgetText.OwnerId), nameof(ILocalizedText.LanguageCode)]);
        unique.GetDatabaseName().ShouldBe("IX_GadgetTexts_OwnerId_LanguageCode_Unique");
    }

    [Fact]
    public void Relationship_CascadeDeletes_ForAForeignKeyThatIsNotNamedAfterTheOwner()
    {
        using var db = new ConventionProbe();

        var textType = db.Context.Model.FindEntityType(typeof(GadgetText))!;
        var foreignKey = textType.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(Gadget));

        foreignKey.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }

    [Fact]
    public void GuidKeyDefault_IsApplied_WhenNoDefaultIsConfigured()
    {
        using var db = new ConventionProbe();

        db.Context.Model.FindEntityType(typeof(Gadget))!
            .FindProperty(nameof(Gadget.Id))!
            .GetDefaultValueSql()
            .ShouldBe("NEWSEQUENTIALID()");
    }

    [Fact]
    public void GuidKeyDefault_LeavesAnExplicitClrDefaultAlone()
    {
        // Overwriting it would make EF Core throw at model build: a property cannot carry both a
        // DefaultValue and a DefaultValueSql. Building the model at all is the assertion.
        using var db = new ConventionProbe();

        var key = db.Context.Model.FindEntityType(typeof(Widget))!.FindProperty(nameof(Widget.Id))!;

        key.GetDefaultValueSql().ShouldBeNull();
        key.GetDefaultValue().ShouldBe(Widget.SeededId);
    }

    private sealed class ConventionProbe : IDisposable
    {
        private readonly SqliteConnection connection;

        public ConventionProbe()
        {
            this.connection = new SqliteConnection("Filename=:memory:");
            this.connection.Open();
            this.Context = new ProbeDbContext(
                new DbContextOptionsBuilder<ProbeDbContext>().UseSqlite(this.connection).Options);
        }

        public ProbeDbContext Context { get; }

        public void Dispose()
        {
            this.Context.Dispose();
            this.connection.Dispose();
        }
    }
}
