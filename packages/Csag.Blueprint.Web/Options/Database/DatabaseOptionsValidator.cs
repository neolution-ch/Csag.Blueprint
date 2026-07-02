namespace Csag.Blueprint.Web.Options.Database
{
    using FluentValidation;

    /// <summary>
    /// Validator for DatabaseOptions configuration.
    /// </summary>
    public sealed class DatabaseOptionsValidator : AbstractValidator<DatabaseOptions>
    {
        // Note: Connection string validation is performed in GetValidatedDatabaseOptions
        // because connection strings are stored in a separate configuration section
    }
}
