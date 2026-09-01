namespace Csag.Blueprint.Tests.TestModel;

using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Localization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// An in-memory SQLite database. SQLite is used rather than the EF in-memory provider because these
/// tests assert that the query extensions translate to SQL — the in-memory provider never translates.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection connection;

    public TestDatabase()
    {
        this.connection = new SqliteConnection("Filename=:memory:");
        this.connection.Open();

        this.Context = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>().UseSqlite(this.connection).Options);
        this.Context.Database.EnsureCreated();
    }

    public TestDbContext Context { get; }

    public static ICurrentLanguageProvider Language(string current, string fallback)
        => new ExplicitLanguageProvider(current, fallback);

    public Product AddProduct(string internalName, params (string LanguageCode, string Text)[] texts)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            InternalName = internalName,
            LocalizedTexts = [.. texts.Select(t => new ProductText { Id = Guid.NewGuid(), LanguageCode = t.LanguageCode, Text = t.Text })],
        };

        this.Context.Products.Add(product);
        return product;
    }

    public void Save() => this.Context.SaveChanges();

    public void Dispose()
    {
        this.Context.Dispose();
        this.connection.Dispose();
    }
}
