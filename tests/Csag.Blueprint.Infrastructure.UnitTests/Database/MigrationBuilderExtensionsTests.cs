namespace Csag.Blueprint.Infrastructure.UnitTests.Database;

using Csag.Blueprint.Infrastructure.Translations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

/// <summary>
/// Unit tests for the <see cref="MigrationBuilderExtensions"/> translation seeding helpers,
/// asserting the operations added to the <see cref="MigrationBuilder"/>: the UPSERT shape of the
/// generated SQL and the single-quote escaping of the interpolated values — the seeding values are
/// baked into raw SQL, so the escaping is the only thing standing between a translated text
/// containing an apostrophe and a broken (or injectable) migration.
/// </summary>
public sealed class MigrationBuilderExtensionsTests
{
    [Fact]
    public void SeedTranslation_AddsSingleSqlOperationWithUpsertShape()
    {
        // Arrange
        var migrationBuilder = CreateMigrationBuilder();

        // Act
        migrationBuilder.SeedTranslation("Validation.EmailRequired", "de-CH", "E-Mail ist erforderlich");

        // Assert — one operation containing both branches of the UPSERT against the translations
        // table; the mixed-case input language code is stored in canonical lowercase so the row
        // matches what the translation provider looks up.
        var sql = migrationBuilder.Operations.ShouldHaveSingleItem().ShouldBeOfType<SqlOperation>().Sql;
        sql.ShouldContain("IF EXISTS (SELECT 1 FROM [BlueprintTranslations] WHERE [Key] = 'Validation.EmailRequired' AND [LanguageCode] = 'de-ch')");
        sql.ShouldContain("UPDATE [BlueprintTranslations]");
        sql.ShouldContain("SET [Value] = 'E-Mail ist erforderlich', [UpdatedAt] = GETUTCDATE()");
        sql.ShouldContain("INSERT INTO [BlueprintTranslations] ([Key], [LanguageCode], [Value], [CreatedAt])");
        sql.ShouldContain("VALUES ('Validation.EmailRequired', 'de-ch', 'E-Mail ist erforderlich', GETUTCDATE())");
    }

    [Fact]
    public void SeedTranslation_EscapesSingleQuotesInAllInterpolatedValues()
    {
        // Arrange — apostrophes in the key, the language code, and the value.
        var migrationBuilder = CreateMigrationBuilder();

        // Act
        migrationBuilder.SeedTranslation("Errors.L'Etat", "d'-CH", "L'état de l'art");

        // Assert — every quote is doubled; no unescaped original remains to terminate the literal early.
        var sql = migrationBuilder.Operations.ShouldHaveSingleItem().ShouldBeOfType<SqlOperation>().Sql;
        sql.ShouldContain("'Errors.L''Etat'");
        sql.ShouldContain("'d''-ch'");
        sql.ShouldContain("'L''état de l''art'");
        sql.ShouldNotContain("'L'état");
    }

    [Fact]
    public void SeedTranslation_ValueEndingAttemptedInjection_StaysInsideTheLiteral()
    {
        // Arrange — a value trying to break out of the string literal and append its own statement.
        var migrationBuilder = CreateMigrationBuilder();

        // Act
        migrationBuilder.SeedTranslation("Key.A", "de-CH", "x'; DROP TABLE [BlueprintTranslations]; --");

        // Assert — the payload is inert because its quote is doubled inside the literal.
        var sql = migrationBuilder.Operations.ShouldHaveSingleItem().ShouldBeOfType<SqlOperation>().Sql;
        sql.ShouldContain("'x''; DROP TABLE [BlueprintTranslations]; --'");
        sql.ShouldNotContain("'x';");
    }

    [Fact]
    public void SeedTranslations_AddsOneOperationPerLanguageInOrder()
    {
        // Arrange
        var migrationBuilder = CreateMigrationBuilder();

        // Act
        migrationBuilder.SeedTranslations(
            "Errors.InvoiceNotFound",
            ("de-CH", "Rechnung nicht gefunden"),
            ("fr-CH", "Facture introuvable"));

        // Assert
        migrationBuilder.Operations.Count.ShouldBe(2);
        var first = migrationBuilder.Operations[0].ShouldBeOfType<SqlOperation>().Sql;
        var second = migrationBuilder.Operations[1].ShouldBeOfType<SqlOperation>().Sql;
        first.ShouldContain("'de-ch'");
        first.ShouldContain("'Rechnung nicht gefunden'");
        second.ShouldContain("'fr-ch'");
        second.ShouldContain("'Facture introuvable'");
    }

    [Fact]
    public void SeedTranslation_NullMigrationBuilder_Throws()
    {
        Should.Throw<ArgumentNullException>(() => MigrationBuilderExtensions.SeedTranslation(null!, "Key.A", "de-CH", "x"));
    }

    [Fact]
    public void SeedTranslations_NullMigrationBuilder_Throws()
    {
        Should.Throw<ArgumentNullException>(() => MigrationBuilderExtensions.SeedTranslations(null!, "Key.A", ("de-CH", "x")));
    }

    private static MigrationBuilder CreateMigrationBuilder()
    {
        return new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
    }
}
