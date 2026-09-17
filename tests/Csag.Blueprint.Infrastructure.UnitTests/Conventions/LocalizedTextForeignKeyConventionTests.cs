namespace Csag.Blueprint.Infrastructure.UnitTests.Conventions;

using System.Globalization;
using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Infrastructure.Database;
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

/// <summary>An entity whose Guid key already declares an explicit CLR default.</summary>
public sealed class Widget
{
    public static readonly Guid SeededId = Guid.Parse("11111111-1111-1111-1111-111111111111", CultureInfo.InvariantCulture);

    public Guid Id { get; set; }
}

/// <summary>An owner whose localized texts use a foreign key that is not named "GadgetId".</summary>
public sealed class Gadget : IHasLocalizedTexts<GadgetText>
{
    public Guid Id { get; set; }

    public ICollection<GadgetText> LocalizedTexts { get; set; } = [];
}

/// <summary>Localized texts keyed by a deliberately unconventional foreign key name.</summary>
public sealed class GadgetText : ILocalizedText
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Gadget Owner { get; set; } = null!;

    public string LanguageCode { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

/// <summary>Minimal context exercising the conventions against an unconventional model.</summary>
public sealed class ProbeDbContext : DbContext
{
    public ProbeDbContext(DbContextOptions<ProbeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Widget> Widgets => this.Set<Widget>();

    public DbSet<Gadget> Gadgets => this.Set<Gadget>();

    public DbSet<GadgetText> GadgetTexts => this.Set<GadgetText>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Widget>().Property(w => w.Id).HasDefaultValue(Widget.SeededId);
        modelBuilder.Entity<GadgetText>().HasOne(t => t.Owner).WithMany(g => g.LocalizedTexts).HasForeignKey(t => t.OwnerId);

        modelBuilder.ConfigureLocalizedTextConventions();
        modelBuilder.ConfigureGuidPrimaryKeyDefaults();
    }
}
