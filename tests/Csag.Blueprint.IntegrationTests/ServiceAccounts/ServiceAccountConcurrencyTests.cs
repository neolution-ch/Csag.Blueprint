namespace Csag.Blueprint.IntegrationTests.ServiceAccounts;

using System.Net;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Testing.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Integration tests for issuance running alongside other changes to the same account. The interleavings are made
/// deterministic with SQL Server's own locks: a second connection holds an update lock on the account row, which lets
/// the issuance read the account but makes its first write to it wait; the test commits its change while the
/// issuance waits, then lets it continue.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class ServiceAccountConcurrencyTests(AppFixture app) : ServiceAccountTestBase(app)
{
    private static readonly TimeSpan BlockTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A change committed to the account while an issuance waits for it.
    /// </summary>
    private enum AccountChange
    {
        /// <summary>
        /// Locks the account out for five minutes.
        /// </summary>
        LockOut,

        /// <summary>
        /// Replaces the stored secret hash, as a rotation's commit does.
        /// </summary>
        ReplaceSecretHash,

        /// <summary>
        /// Deactivates the account.
        /// </summary>
        Deactivate,
    }

    [Fact]
    public async Task ConcurrentWrongSecrets_EachCountAndLockTheAccountAsync()
    {
        // Arrange — exactly the lockout threshold of wrong secrets, all at once. Each attempt must count, so together
        // they lock the account.
        var (id, credentials) = await this.CreateServiceAccountAsync("concurrent failures");

        // Act
        var attempts = Enumerable.Range(0, 5)
            .Select(_ => this.RunCredentialsAsync(c => c.IssueTokenAsync(credentials.ClientId, "not-the-secret")));
        var results = await Task.WhenAll(attempts);

        // Assert
        results.ShouldAllBe(r => r.Outcome == ServiceAccountTokenOutcome.InvalidCredentials);
        var account = (await this.ReadAccountAsync(id)).ShouldNotBeNull();
        account.LockoutEnd.ShouldNotBeNull().ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        account.AccessFailedCount.ShouldBe(0);

        using var rightSecret = await this.RequestTokenAsync(credentials.ClientId, credentials.ClientSecret);
        await rightSecret.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ManyConcurrentWrongSecrets_LockOnceAndStopCountingAsync()
    {
        var (id, credentials) = await this.CreateServiceAccountAsync("many concurrent failures");

        var attempts = Enumerable.Range(0, 12)
            .Select(_ => this.RunCredentialsAsync(c => c.IssueTokenAsync(credentials.ClientId, "not-the-secret")));
        await Task.WhenAll(attempts);

        // The fifth counted failure locks and resets the count; failures arriving after it do not count.
        var account = (await this.ReadAccountAsync(id)).ShouldNotBeNull();
        account.LockoutEnd.ShouldNotBeNull().ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        account.AccessFailedCount.ShouldBe(0);
    }

    [Fact]
    public async Task ARightSecret_WhenALockoutLandsBeforeItsFirstWrite_IsRefusedAsync()
    {
        // Arrange
        var (id, credentials) = await this.CreateServiceAccountAsync("lockout race");

        // Act — the issuance reads an unlocked account and verifies the secret; the lockout commits before its write.
        var result = await this.WhileTheAccountRowIsHeldAsync(
            id,
            () => this.RunCredentialsAsync(c => c.IssueTokenAsync(credentials.ClientId, credentials.ClientSecret)),
            AccountChange.LockOut);

        // Assert
        result.Outcome.ShouldBe(ServiceAccountTokenOutcome.InvalidCredentials);
        (await this.CountSessionsAsync(id)).ShouldBe(0);
    }

    [Fact]
    public async Task ARotationCommittedDuringIssuance_LeavesNoSessionForThePreviousSecretAsync()
    {
        // Arrange
        var (id, credentials) = await this.CreateServiceAccountAsync("rotation race");

        // Act — the issuance verified the previous secret; the new hash commits before the issuance stores its
        // session. No revocation follows the commit here, so only the issuance itself can drop that session.
        var result = await this.WhileTheAccountRowIsHeldAsync(
            id,
            () => this.RunCredentialsAsync(c => c.IssueTokenAsync(credentials.ClientId, credentials.ClientSecret)),
            AccountChange.ReplaceSecretHash);

        // Assert
        result.Outcome.ShouldBe(ServiceAccountTokenOutcome.InvalidCredentials);
        result.AccessToken.ShouldBeNull();
        (await this.CountSessionsAsync(id)).ShouldBe(0);
    }

    [Fact]
    public async Task ADeactivationCommittedDuringIssuance_LeavesNoSessionAsync()
    {
        var (id, credentials) = await this.CreateServiceAccountAsync("deactivation race");

        var result = await this.WhileTheAccountRowIsHeldAsync(
            id,
            () => this.RunCredentialsAsync(c => c.IssueTokenAsync(credentials.ClientId, credentials.ClientSecret)),
            AccountChange.Deactivate);

        result.Outcome.ShouldBe(ServiceAccountTokenOutcome.AccountInactive);
        (await this.CountSessionsAsync(id)).ShouldBe(0);
    }

    [Fact]
    public async Task RotationsRacingIssuances_LeaveNoTokenOfAPreviousSecretWorkingAsync()
    {
        // Arrange
        var (id, credentials) = await this.CreateServiceAccountAsync("rotation stress");
        var secret = credentials.ClientSecret;
        var issued = new List<string>();

        // Act — rounds of issuances with the current secret racing a rotation away from it.
        for (var round = 0; round < 3; round++)
        {
            var currentSecret = secret;
            var issuances = Enumerable.Range(0, 6)
                .Select(_ => this.RunCredentialsAsync(c => c.IssueTokenAsync(credentials.ClientId, currentSecret)))
                .ToList();
            var rotation = this.RunCredentialsAsync(c => c.RotateSecretAsync(id));

            await Task.WhenAll(issuances);
            secret = (await rotation).ShouldNotBeNull().ClientSecret;
            issued.AddRange(issuances.Select(i => i.Result).Where(r => r.IsIssued).Select(r => r.AccessToken!));
        }

        // Assert — whichever issuances won their race, every token they returned belongs to a secret that has since
        // been rotated away, so none may authenticate and no session may be left behind.
        (await this.CountSessionsAsync(id)).ShouldBe(0);
        foreach (var token in issued)
        {
            using var client = this.BearerClient(token);
            await WhoAmIAsync(client, HttpStatusCode.Unauthorized);
        }

        await this.IssueAsync(credentials.ClientId, secret);
    }

    private static async Task WaitUntilBlockedAsync(SqlConnection connection, SqlTransaction transaction, Task running, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + BlockTimeout;
        await using var probe = new SqlCommand("SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id = @@SPID", connection, transaction);

        while ((int)(await probe.ExecuteScalarAsync(ct))! == 0)
        {
            running.IsCompleted.ShouldBeFalse("the operation finished without waiting for the held account row");
            DateTimeOffset.UtcNow.ShouldBeLessThan(deadline, "the operation never waited for the held account row");
            await Task.Delay(20, ct);
        }
    }

    private static SqlCommand CreateChangeCommand(AccountChange change, SqlConnection connection, SqlTransaction transaction)
        => change switch
        {
            AccountChange.LockOut => new SqlCommand(
                "UPDATE BlueprintServiceAccounts SET LockoutEnd = DATEADD(MINUTE, 5, SYSDATETIMEOFFSET()) WHERE Id = @id", connection, transaction),
            AccountChange.ReplaceSecretHash => new SqlCommand(
                "UPDATE BlueprintServiceAccounts SET ClientSecretHash = 'rotated' WHERE Id = @id", connection, transaction),
            AccountChange.Deactivate => new SqlCommand(
                "UPDATE BlueprintServiceAccounts SET IsActive = 0 WHERE Id = @id", connection, transaction),
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, null),
        };

    /// <summary>
    /// Runs <paramref name="operation"/> while a second connection holds an update lock on the account row, waits until
    /// the operation is blocked on that lock, commits <paramref name="change"/> through the holding connection, and
    /// returns the operation's result once it has continued.
    /// </summary>
    /// <remarks>
    /// An update lock is compatible with the shared locks of reads, so the operation reads the account unhindered;
    /// its first write to the row is the first statement that waits.
    /// </remarks>
    /// <typeparam name="T">The type of the operation's result.</typeparam>
    /// <param name="serviceAccountId">The account whose row is held.</param>
    /// <param name="operation">The operation to run against the held row.</param>
    /// <param name="change">The change committed while the operation waits.</param>
    /// <returns>What the operation returned.</returns>
    private async Task<T> WhileTheAccountRowIsHeldAsync<T>(Guid serviceAccountId, Func<Task<T>> operation, AccountChange change)
    {
        var ct = TestContext.Current.CancellationToken;

        string connectionString;
        using (var scope = this.App.CreateDbContextScope())
        {
            connectionString = scope.Context.Database.GetConnectionString()!;
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        await using (var hold = new SqlCommand("SELECT Id FROM BlueprintServiceAccounts WITH (UPDLOCK, ROWLOCK) WHERE Id = @id", connection, transaction))
        {
            hold.Parameters.AddWithValue("@id", serviceAccountId);
            (await hold.ExecuteScalarAsync(ct)).ShouldNotBeNull();
        }

        var running = operation();
        await WaitUntilBlockedAsync(connection, transaction, running, ct);

        await using (var update = CreateChangeCommand(change, connection, transaction))
        {
            update.Parameters.AddWithValue("@id", serviceAccountId);
            (await update.ExecuteNonQueryAsync(ct)).ShouldBe(1);
        }

        await transaction.CommitAsync(ct);
        return await running.WaitAsync(BlockTimeout, ct);
    }
}
