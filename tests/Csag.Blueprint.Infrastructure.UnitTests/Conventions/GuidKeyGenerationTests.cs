namespace Csag.Blueprint.Infrastructure.UnitTests.Conventions;

using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The <c>NEWSEQUENTIALID()</c> column default must not change how EF Core assigns keys at runtime: an
/// added entity still gets its key immediately, so code can use it as a foreign key value before
/// <c>SaveChanges</c>. The database default only serves inserts that bypass EF Core.
/// </summary>
public sealed class GuidKeyGenerationTests
{
    [Fact]
    public void AddedEntity_GetsAPermanentKey_BeforeSaveChanges()
    {
        using var context = Build();

        var note = new LateNote();
        var entry = context.Add(note);

        note.Id.ShouldNotBe(Guid.Empty);
        entry.Property(n => n.Id).IsTemporary.ShouldBeFalse();
    }

    [Fact]
    public void ConsecutiveKeys_AreDistinct()
    {
        using var context = Build();

        var first = context.Add(new LateNote()).Entity.Id;
        var second = context.Add(new LateNote()).Entity.Id;

        first.ShouldNotBe(second);
    }

    // SQL Server, because the key default only applies on that provider. Adding entities opens no
    // connection, so the server is never reached.
    private static LateRegistrationDbContext Build()
        => new(new DbContextOptionsBuilder<LateRegistrationDbContext>()
            .UseSqlServer("Server=nowhere;Database=key-generation;Trusted_Connection=True;")
            .Options);
}
