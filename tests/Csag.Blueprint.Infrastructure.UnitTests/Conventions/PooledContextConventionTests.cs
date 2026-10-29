namespace Csag.Blueprint.Infrastructure.UnitTests.Conventions;

using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The blueprint conventions must survive <c>DbContext</c> pooling on a relational provider — the shape
/// <c>AddPooledDbContextFactory</c> produces, which the test host and real applications use.
/// </summary>
/// <remarks>
/// Anything that registers services from <c>OnConfiguring</c>, such as a replaced
/// <see cref="Microsoft.EntityFrameworkCore.Infrastructure.IModelCustomizer"/>, throws
/// <c>'OnConfiguring' cannot be used to modify DbContextOptions when DbContext pooling is enabled</c> as
/// soon as a pooled factory is involved, yet passes every test that constructs a context directly. These
/// tests build the model through a pooled factory, which needs no database since no connection is opened.
/// </remarks>
public sealed class PooledContextConventionTests
{
    [Fact]
    public void ContextIsUsable_WhenPooled()
    {
        using var provider = BuildPooledProvider();

        var factory = provider.GetRequiredService<IDbContextFactory<LateRegistrationDbContext>>();

        using var context = factory.CreateDbContext();
        context.Model.ShouldNotBeNull();
    }

    [Fact]
    public void ConventionsAreApplied_WhenPooled()
    {
        using var provider = BuildPooledProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<LateRegistrationDbContext>>();

        using var context = factory.CreateDbContext();
        var lateNote = context.Model.FindEntityType(typeof(LateNote))!;

        lateNote.GetDeclaredQueryFilters().Count.ShouldBe(1);
        lateNote.GetDeclaredIndexes().Select(i => i.GetDatabaseName()).ShouldContain("IX_LateNote_DeletedAt");
        lateNote.FindProperty(nameof(LateNote.Id))!.GetDefaultValueSql().ShouldBe("NEWSEQUENTIALID()");
    }

    private static ServiceProvider BuildPooledProvider()
    {
        var services = new ServiceCollection();

        // Mirrors TestHostPersistenceExtensions. Building a model opens no connection, so the
        // unreachable server is never contacted.
        services.AddPooledDbContextFactory<LateRegistrationDbContext>(options =>
            options.UseSqlServer("Server=nowhere;Database=blueprint-conventions;Trusted_Connection=True;"));

        return services.BuildServiceProvider();
    }
}
