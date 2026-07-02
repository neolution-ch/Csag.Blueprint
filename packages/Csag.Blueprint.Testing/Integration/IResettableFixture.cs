namespace Csag.Blueprint.Testing.Integration;

/// <summary>
/// Marks a test fixture as supporting per-test database reset via snapshot restore.
/// Implement this on your application-specific fixture class alongside
/// <see cref="MsSqlTestContainerOrchestrator.ResetDatabaseAsync"/>.
/// </summary>
public interface IResettableFixture
{
    /// <summary>
    /// Resets the database to the post-seeding snapshot state captured at fixture setup.
    /// Implementations should delegate to <see cref="MsSqlTestContainerOrchestrator.ResetDatabaseAsync"/>.
    /// </summary>
    /// <returns>A task representing the asynchronous reset operation.</returns>
    Task ResetDatabaseAsync();
}
