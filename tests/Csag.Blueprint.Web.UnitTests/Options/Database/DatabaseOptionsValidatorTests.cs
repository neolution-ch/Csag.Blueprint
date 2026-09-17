namespace Csag.Blueprint.Web.UnitTests.Options.Database;

using Csag.Blueprint.Web.Options.Database;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="DatabaseOptionsValidator"/>. The validator intentionally carries no
/// rules (connection strings are validated elsewhere), so both flag states must pass.
/// </summary>
public sealed class DatabaseOptionsValidatorTests
{
    private readonly DatabaseOptionsValidator validator = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Validate_AnyMigrationFlagState_Passes(bool applyMigrationsAutomaticallyDuringStartup)
    {
        var options = new DatabaseOptions
        {
            ApplyMigrationsAutomaticallyDuringStartup = applyMigrationsAutomaticallyDuringStartup,
        };

        var result = this.validator.TestValidate(options);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
