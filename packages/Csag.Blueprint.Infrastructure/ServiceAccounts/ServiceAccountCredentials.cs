namespace Csag.Blueprint.Infrastructure.ServiceAccounts;

using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

/// <summary>
/// The package's <see cref="IServiceAccountCredentials"/>.
/// </summary>
/// <typeparam name="TContext">The application database context type.</typeparam>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection.")]
internal sealed class ServiceAccountCredentials<TContext> : IServiceAccountCredentials
    where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> dbContextFactory;
    private readonly IServiceAccountSessionManager sessions;
    private readonly ServiceAccountSessionLifecycle lifecycle;
    private readonly ServiceAccountTokenSigning signing;
    private readonly IOptions<BlueprintServiceAccountOptions> options;
    private readonly ServiceAccountCaller caller;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAccountCredentials{TContext}"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The context factory the accounts are read and written through.</param>
    /// <param name="sessions">The service-account sessions.</param>
    /// <param name="lifecycle">Revokes an account's sessions and tells the participants.</param>
    /// <param name="signing">Mints the tokens.</param>
    /// <param name="options">The token and lockout settings.</param>
    /// <param name="caller">Describes the issuing request on the session it creates.</param>
    /// <param name="timeProvider">The clock for issuance, expiry and lockout.</param>
    public ServiceAccountCredentials(
        IDbContextFactory<TContext> dbContextFactory,
        IServiceAccountSessionManager sessions,
        ServiceAccountSessionLifecycle lifecycle,
        ServiceAccountTokenSigning signing,
        IOptions<BlueprintServiceAccountOptions> options,
        ServiceAccountCaller caller,
        TimeProvider timeProvider)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.signing = signing ?? throw new ArgumentNullException(nameof(signing));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.caller = caller ?? throw new ArgumentNullException(nameof(caller));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc/>
    public async Task<ServiceAccountTokenResult> IssueTokenAsync(string clientId, string clientSecret)
    {
        // A check on the request's shape alone, made before any account is read, so it tells nothing about accounts.
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return ServiceAccountTokenResult.Refused(ServiceAccountTokenOutcome.InvalidCredentials);
        }

        var settings = this.options.Value;
        var now = this.timeProvider.GetUtcNow();

        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync();

        // The tenant is not known before authentication, and the tenant filter matches nothing without one.
        var account = await dbContext.Set<BlueprintServiceAccount>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(sa => sa.ClientId == clientId)
            .Select(sa => new IssuingAccount(sa.Id, sa.ClientId, sa.Name, sa.ClientSecretHash, sa.IsActive, sa.LockoutEnd))
            .FirstOrDefaultAsync();

        // Verified before any of the branches below, against a stand-in hash for an unknown client, so every refusal
        // costs the same key derivation and none of them answers faster than the others.
        var secretMatches = ServiceAccountSecretHasher.Verify(account?.ClientSecretHash, clientSecret);

        // A locked account is refused whatever secret it is given, and the attempt does not count.
        if (account is null || account.LockoutEnd > now)
        {
            return ServiceAccountTokenResult.Refused(ServiceAccountTokenOutcome.InvalidCredentials);
        }

        if (!secretMatches)
        {
            await RegisterFailedAttemptAsync(dbContext, account.Id, now, settings);
            return ServiceAccountTokenResult.Refused(ServiceAccountTokenOutcome.InvalidCredentials);
        }

        // Clearing the failure count is also the atomic re-check of the read above: when failures running alongside
        // this request have locked the account since, or a rotation has replaced the secret it verified, the update
        // matches no row. A replaced secret is refused before the account's state is reported, and the failures
        // counted against the new secret stand.
        if (!await TryClearFailedAttemptsAsync(dbContext, account.Id, account.ClientSecretHash, now))
        {
            return ServiceAccountTokenResult.Refused(ServiceAccountTokenOutcome.InvalidCredentials);
        }

        if (!account.IsActive)
        {
            return ServiceAccountTokenResult.Refused(ServiceAccountTokenOutcome.AccountInactive);
        }

        var expiresAt = now + settings.TokenLifetime;
        var sessionKey = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        // Minted before the session is tracked, so a host without a signing key fails here having stored nothing.
        var accessToken = this.signing.CreateToken(account.ClientId, account.Name, sessionKey, now, expiresAt);

        var (userAgent, ipAddress) = this.caller.Describe();
        await this.sessions.TrackSessionAsync(account.Id, sessionKey, expiresAt, userAgent, ipAddress, CancellationToken.None);

        // RotateSecretAsync revokes the account's sessions a second time after committing the new hash, and
        // deactivation and deletion revoke after their commit. Reading the account again now that the session is
        // committed is therefore exhaustive: a change committed before this read is seen here, and one committed
        // after it finds this session already stored when it revokes.
        var current = await dbContext.Set<BlueprintServiceAccount>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(sa => sa.Id == account.Id)
            .Select(sa => new { sa.ClientSecretHash, sa.IsActive })
            .FirstOrDefaultAsync();

        var secretStillCurrent = current is not null && string.Equals(current.ClientSecretHash, account.ClientSecretHash, StringComparison.Ordinal);
        if (!secretStillCurrent || !current!.IsActive)
        {
            await this.sessions.RevokeSessionAsync(sessionKey, CancellationToken.None);
            return ServiceAccountTokenResult.Refused(
                secretStillCurrent ? ServiceAccountTokenOutcome.AccountInactive : ServiceAccountTokenOutcome.InvalidCredentials);
        }

        return ServiceAccountTokenResult.Issued(accessToken, (int)settings.TokenLifetime.TotalSeconds, expiresAt);
    }

    /// <inheritdoc/>
    public Task<ServiceAccountClientCredentials> CreateCredentialsAsync(BlueprintServiceAccount serviceAccount)
    {
        ArgumentNullException.ThrowIfNull(serviceAccount);

        // The guard sits in this non-async wrapper so it surfaces at the call site rather than on the awaited task.
        return this.CreateCredentialsCoreAsync(serviceAccount);
    }

    /// <inheritdoc/>
    public async Task<ServiceAccountClientCredentials?> RotateSecretAsync(Guid serviceAccountId)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync();

        // Through the tenant filter: an account of another tenant is reported as not found.
        var account = await dbContext.Set<BlueprintServiceAccount>()
            .FirstOrDefaultAsync(sa => sa.Id == serviceAccountId);

        if (account is null)
        {
            return null;
        }

        var clientSecret = ServiceAccountSecretHasher.GenerateSecret();
        account.ClientSecretHash = ServiceAccountSecretHasher.Hash(clientSecret);

        // Before the commit, so a failure from here on cannot leave the sessions that existed when the rotation
        // started alive. A rotation that then fails to commit costs the client one re-authentication with its
        // unchanged secret.
        await this.sessions.RevokeServiceAccountSessionsAsync(account.Id, CancellationToken.None);

        await dbContext.SaveChangesAsync();

        // Again after the commit, for a session issued between the first pass and the commit; IssueTokenAsync reads
        // the hash again once its session is stored, so such a session is dropped by its own issuance or found here.
        await this.lifecycle.RevokeAndNotifyAsync(account.Id, SessionRevocationReason.CredentialsChanged);

        return new ServiceAccountClientCredentials(account.ClientId, clientSecret);
    }

    /// <summary>
    /// Counts a wrong secret, and locks the account once the count reaches the threshold, in one statement.
    /// </summary>
    /// <remarks>
    /// The new count is derived from the stored one under the row lock, so concurrent wrong secrets each count and
    /// together reach the threshold. A wrong secret arriving while the account is locked does not count. The
    /// statement bypasses the change tracker, so the attempt writes no audit entry.
    /// </remarks>
    private static Task<int> RegisterFailedAttemptAsync(TContext dbContext, Guid serviceAccountId, DateTimeOffset now, BlueprintServiceAccountOptions settings)
    {
        var maxAttempts = settings.MaxFailedAccessAttempts;
        DateTimeOffset? lockoutEnd = now + settings.LockoutDuration;

        // Both setters read the pre-update AccessFailedCount, so locking and resetting the count agree.
        return dbContext.Set<BlueprintServiceAccount>()
            .IgnoreQueryFilters()
            .Where(sa => sa.Id == serviceAccountId && (sa.LockoutEnd == null || sa.LockoutEnd <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(sa => sa.LockoutEnd, sa => sa.AccessFailedCount + 1 >= maxAttempts ? lockoutEnd : sa.LockoutEnd)
                .SetProperty(sa => sa.AccessFailedCount, sa => sa.AccessFailedCount + 1 >= maxAttempts ? 0 : sa.AccessFailedCount + 1));
    }

    /// <summary>
    /// Resets the failure count and any expired lockout, in one statement that matches only while the account is not
    /// locked and still stores the secret hash the request was verified against.
    /// </summary>
    /// <returns><see langword="true"/> when the account exists, is not locked, and still has the verified secret.</returns>
    private static async Task<bool> TryClearFailedAttemptsAsync(TContext dbContext, Guid serviceAccountId, string verifiedSecretHash, DateTimeOffset now)
    {
        var cleared = await dbContext.Set<BlueprintServiceAccount>()
            .IgnoreQueryFilters()
            .Where(sa => sa.Id == serviceAccountId
                && sa.ClientSecretHash == verifiedSecretHash
                && (sa.LockoutEnd == null || sa.LockoutEnd <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(sa => sa.AccessFailedCount, 0)
                .SetProperty(sa => sa.LockoutEnd, (DateTimeOffset?)null));

        return cleared > 0;
    }

    private async Task<ServiceAccountClientCredentials> CreateCredentialsCoreAsync(BlueprintServiceAccount serviceAccount)
    {
        var clientSecret = ServiceAccountSecretHasher.GenerateSecret();
        serviceAccount.ClientId = string.Create(CultureInfo.InvariantCulture, $"sa-{Guid.NewGuid():N}");
        serviceAccount.ClientSecretHash = ServiceAccountSecretHasher.Hash(clientSecret);
        serviceAccount.AccessFailedCount = 0;
        serviceAccount.LockoutEnd = null;

        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync();
        dbContext.Set<BlueprintServiceAccount>().Add(serviceAccount);
        await dbContext.SaveChangesAsync();

        return new ServiceAccountClientCredentials(serviceAccount.ClientId, clientSecret);
    }

    private sealed record IssuingAccount(
        Guid Id,
        string ClientId,
        string Name,
        string ClientSecretHash,
        bool IsActive,
        DateTimeOffset? LockoutEnd);
}
