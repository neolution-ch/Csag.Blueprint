namespace Csag.Blueprint.Infrastructure.UnitTests.Conventions;

using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The blueprint conventions run after the whole model is built, so an entity type registered after
/// <c>base.OnModelCreating</c> is configured exactly like one exposed as a <c>DbSet</c>. Applying them
/// inline in <c>OnModelCreating</c> skipped such types in silence — for
/// <see cref="Domain.Contracts.ISoftDeletable"/> that meant no query filter, so soft-deleted rows came
/// back in every query.
/// </summary>
public sealed class LateRegistrationConventionTests
{
    [Fact]
    public void SoftDeleteFilter_IsApplied_ToALateRegisteredEntity()
    {
        using var context = Build();

        context.Model.FindEntityType(typeof(LateNote))!.GetDeclaredQueryFilters().Count.ShouldBe(1);
    }

    [Fact]
    public void DeletedAtIndex_IsApplied_ToALateRegisteredEntity()
    {
        using var context = Build();

        context.Model.FindEntityType(typeof(LateNote))!
            .GetDeclaredIndexes()
            .Select(i => i.GetDatabaseName())
            .ShouldContain("IX_LateNote_DeletedAt");
    }

    [Fact]
    public void GuidKeyDefault_IsApplied_ToALateRegisteredEntity()
    {
        using var context = Build();

        context.Model.FindEntityType(typeof(LateNote))!
            .FindProperty(nameof(LateNote.Id))!
            .GetDefaultValueSql()
            .ShouldBe("NEWSEQUENTIALID()");
    }

    [Fact]
    public void LateAndEarlyRegisteredEntities_AreConfiguredIdentically()
    {
        using var context = Build();

        static (int Filters, int Indexes, string? KeyDefault) Describe(Microsoft.EntityFrameworkCore.Metadata.IEntityType e)
            => (e.GetDeclaredQueryFilters().Count, e.GetDeclaredIndexes().Count(), e.FindProperty("Id")!.GetDefaultValueSql());

        Describe(context.Model.FindEntityType(typeof(LateNote))!)
            .ShouldBe(Describe(context.Model.FindEntityType(typeof(EarlyNote))!));
    }

    private static LateRegistrationDbContext Build()
        => new(new DbContextOptionsBuilder<LateRegistrationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
