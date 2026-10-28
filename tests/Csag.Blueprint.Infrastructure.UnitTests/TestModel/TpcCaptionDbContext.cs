namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Context that does not derive from <c>BlueprintDbContext</c> and calls the public
/// <see cref="LocalizationModelBuilderExtensions.ConfigureLocalizedTextUniqueConstraint{TLocalizedText}"/>
/// helper on the abstract root of a table-per-concrete-type hierarchy.
/// </summary>
public sealed class TpcCaptionDbContext : DbContext
{
    public TpcCaptionDbContext(DbContextOptions<TpcCaptionDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Caption>().UseTpcMappingStrategy();
        modelBuilder.Entity<ImageCaption>();
        modelBuilder.Entity<VideoCaption>();

        modelBuilder.ConfigureLocalizedTextUniqueConstraint<Caption>(nameof(Caption.OwnerId));
    }
}
