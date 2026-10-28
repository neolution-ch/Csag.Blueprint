namespace Csag.Blueprint.Infrastructure.UnitTests.Conventions;

using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// <c>NEWSEQUENTIALID()</c> is a SQL Server function, so the key convention must not be applied on other
/// providers. It fails late rather than loudly: SQLite accepts the generated DDL, so
/// <c>EnsureCreated()</c> succeeds and only the first insert fails with a <c>DbUpdateException</c>.
/// </summary>
public sealed class ProviderSpecificConventionTests
{
    [Fact]
    public void GuidKeyDefault_IsApplied_OnSqlServer()
    {
        using var context = new LateRegistrationDbContext(
            new DbContextOptionsBuilder<LateRegistrationDbContext>()
                .UseSqlServer("Server=nowhere;Database=conventions;Trusted_Connection=True;")
                .Options);

        context.Model.FindEntityType(typeof(LateNote))!
            .FindProperty(nameof(LateNote.Id))!
            .GetDefaultValueSql()
            .ShouldBe("NEWSEQUENTIALID()");
    }

    [Fact]
    public void GuidKeyDefault_IsNotApplied_OnSqlite()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        using var context = new LateRegistrationDbContext(
            new DbContextOptionsBuilder<LateRegistrationDbContext>().UseSqlite(connection).Options);

        context.Model.FindEntityType(typeof(LateNote))!
            .FindProperty(nameof(LateNote.Id))!
            .GetDefaultValueSql()
            .ShouldBeNull();
    }

    [Fact]
    public void SoftDeleteFilter_IsStillApplied_OnSqlite()
    {
        // The provider guard must be narrow: only the SQL Server specific key default is skipped.
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        using var context = new LateRegistrationDbContext(
            new DbContextOptionsBuilder<LateRegistrationDbContext>().UseSqlite(connection).Options);

        context.Model.FindEntityType(typeof(LateNote))!.GetDeclaredQueryFilters().Count.ShouldBe(1);
    }
}
