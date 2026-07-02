namespace Csag.Blueprint.Infrastructure.Translations;

using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Extension methods for <see cref="MigrationBuilder"/> to simplify translation seeding in EF migrations.
/// </summary>
/// <remarks>
/// Example usage in a migration:
/// <code>
/// migrationBuilder.SeedTranslation("Validation.EmailRequired", "de-CH", "E-Mail ist erforderlich");
///
/// migrationBuilder.SeedTranslations("Errors.PedaloNotFound",
///     ("de-CH", "Pedalo nicht gefunden"),
///     ("fr-CH", "Pédalo introuvable"));
/// </code>
/// </remarks>
public static class MigrationBuilderExtensions
{
    /// <summary>
    /// Seeds a single translation entry for a specific key and language.
    /// Uses an UPSERT pattern: inserts if missing, updates if already present.
    /// </summary>
    /// <param name="migrationBuilder">The migration builder.</param>
    /// <param name="key">The translation key (e.g., "Validation.EmailRequired").</param>
    /// <param name="languageCode">The language code (e.g., "de-CH").</param>
    /// <param name="value">The translated text.</param>
    public static void SeedTranslation(this MigrationBuilder migrationBuilder, string key, string languageCode, string value)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        var escapedKey = key.Replace("'", "''");
        var escapedLanguageCode = languageCode.Replace("'", "''");
        var escapedValue = value.Replace("'", "''");

        migrationBuilder.Sql($"""
            IF EXISTS (SELECT 1 FROM [BlueprintTranslations] WHERE [Key] = '{escapedKey}' AND [LanguageCode] = '{escapedLanguageCode}')
            BEGIN
                UPDATE [BlueprintTranslations]
                SET [Value] = '{escapedValue}', [UpdatedAt] = GETUTCDATE()
                WHERE [Key] = '{escapedKey}' AND [LanguageCode] = '{escapedLanguageCode}';
            END
            ELSE
            BEGIN
                INSERT INTO [BlueprintTranslations] ([Key], [LanguageCode], [Value], [CreatedAt])
                VALUES ('{escapedKey}', '{escapedLanguageCode}', '{escapedValue}', GETUTCDATE());
            END
            """);
    }

    /// <summary>
    /// Seeds multiple translations for a single key across different languages.
    /// </summary>
    /// <param name="migrationBuilder">The migration builder.</param>
    /// <param name="key">The translation key (e.g., "Validation.EmailRequired").</param>
    /// <param name="translations">Tuples of (languageCode, translatedValue).</param>
    public static void SeedTranslations(this MigrationBuilder migrationBuilder, string key, params (string LanguageCode, string Value)[] translations)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        foreach (var (languageCode, value) in translations)
        {
            migrationBuilder.SeedTranslation(key, languageCode, value);
        }
    }
}
