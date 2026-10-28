namespace Csag.Blueprint.Infrastructure.UnitTests.Conventions;

using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// An abstract table-per-concrete-type root maps to no table of its own, so its contract indexes are
/// created once per concrete table and must be named after that table.
/// </summary>
public sealed class TpcConventionTests
{
    [Fact]
    public void ContractIndexes_AreNamedAfterEachConcreteTable()
    {
        var script = CreateScript();

        script.ShouldContain("[IX_Machine_DeletedAt]");
        script.ShouldContain("[IX_Tool_DeletedAt]");
        script.ShouldContain("[IX_Machine_InternalName]");
        script.ShouldContain("[IX_Tool_InternalName]");
    }

    [Fact]
    public void ContractIndexes_HaveNoEmptyTableNameSegment()
    {
        CreateScript().ShouldNotContain("IX__");
    }

    [Fact]
    public void LocalizedTextUniqueConstraintHelper_NamesTheIndexAfterEachConcreteTable()
    {
        using var context = new TpcCaptionDbContext(new DbContextOptionsBuilder<TpcCaptionDbContext>()
            .UseSqlServer("Server=nowhere;Database=tpc-captions;Trusted_Connection=True;")
            .Options);

        var script = context.Database.GenerateCreateScript();

        script.ShouldContain("[IX_ImageCaption_OwnerId_LanguageCode]");
        script.ShouldContain("[IX_VideoCaption_OwnerId_LanguageCode]");
        script.ShouldNotContain("IX__");
    }

    // Generating the script opens no connection, so the server is never reached.
    private static string CreateScript()
    {
        using var context = new TpcDbContext(new DbContextOptionsBuilder<TpcDbContext>()
            .UseSqlServer("Server=nowhere;Database=tpc;Trusted_Connection=True;")
            .Options);

        return context.Database.GenerateCreateScript();
    }
}
