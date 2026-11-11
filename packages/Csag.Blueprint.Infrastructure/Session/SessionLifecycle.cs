namespace Csag.Blueprint.Infrastructure.Session;

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// The package's <see cref="ISessionLifecycle"/>: maps each intent onto the tracked sessions it affects and tells the
/// application's <see cref="ISessionRevocationParticipant"/>s about every revocation.
/// </summary>
/// <typeparam name="TUser">The concrete application user type.</typeparam>
/// <typeparam name="TContext">The application database context type.</typeparam>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection.")]
internal sealed class SessionLifecycle<TUser, TContext> : ISessionLifecycle
    where TUser : BlueprintUser
    where TContext : DbContext
{
    private readonly SessionManager<TUser, TContext> sessions;
    private readonly ISessionEligibility eligibility;
    private readonly ITenantMembershipChecker membershipChecker;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IEnumerable<ISessionRevocationParticipant> participants;
    private readonly ILogger<SessionLifecycle<TUser, TContext>> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionLifecycle{TUser, TContext}"/> class.
    /// </summary>
    /// <param name="sessions">The tracked-session operations, including the caller's ticket a credential change carries into the new session.</param>
    /// <param name="eligibility">Answers whether the account may still hold the caller's new session.</param>
    /// <param name="membershipChecker">Answers whether the caller may switch into a tenant.</param>
    /// <param name="httpContextAccessor">Locates the caller's session.</param>
    /// <param name="participants">The application's revocation participants.</param>
    /// <param name="logger">The logger.</param>
    public SessionLifecycle(
        SessionManager<TUser, TContext> sessions,
        ISessionEligibility eligibility,
        ITenantMembershipChecker membershipChecker,
        IHttpContextAccessor httpContextAccessor,
        IEnumerable<ISessionRevocationParticipant> participants,
        ILogger<SessionLifecycle<TUser, TContext>> logger)
    {
        this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        this.eligibility = eligibility ?? throw new ArgumentNullException(nameof(eligibility));
        this.membershipChecker = membershipChecker ?? throw new ArgumentNullException(nameof(membershipChecker));
        this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        this.participants = participants ?? throw new ArgumentNullException(nameof(participants));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task CredentialsChangedAsync(Guid accountId)
    {
        var httpContext = this.httpContextAccessor.HttpContext;
        var caller = httpContext is null ? null : await httpContext.GetCurrentSessionAsync();
        var failures = new List<Exception>();

        // The caller's own session continues in a new one with the same claims. Its ticket is read before the
        // revocation below removes it, and only while it still authenticates this account: a plain sign-in over a copy
        // of the caller's cookie can have stored another account's principal under the same key since the request
        // authenticated, and continuing with that principal would sign the caller into the other account. A caller of
        // another account, such as an administrator resetting someone else's password, keeps its own session untouched.
        AuthenticationTicket? callerTicket = null;
        if (caller is not null && caller.AccountId == accountId)
        {
            await this.RunStepAsync(failures, async () => callerTicket = await this.sessions.GetOwnTicketAsync(caller.SessionKey, accountId));
        }

        // Every session of the account ends, the caller's included: a copy of the caller's cookie must stop working
        // when the credentials change, and keeping the caller's key would keep that copy valid.
        await this.RevokeSessionsAsync(s => s.UserId == accountId, failures);

        await this.NotifyParticipantsAsync(
            new SessionRevocation { Reason = SessionRevocationReason.CredentialsChanged, Subject = SessionSubject.User(accountId) },
            failures);

        // After any failed step, a participant's included, the caller gets no new session. Ending up signed out is the
        // safe side, and the intent is safe to retry.
        if (httpContext is not null && callerTicket is not null && failures.Count == 0)
        {
            await this.RunStepAsync(failures, () => this.ContinueCallerSessionAsync(httpContext, accountId, callerTicket));
        }

        ThrowIfAnyFailed(failures);
    }

    /// <inheritdoc/>
    public Task AccessRevokedAsync(Guid accountId)
    {
        return this.RevokeAndNotifyAsync(
            s => s.UserId == accountId,
            new SessionRevocation { Reason = SessionRevocationReason.AccessRevoked, Subject = SessionSubject.User(accountId) });
    }

    /// <inheritdoc/>
    public Task AccessRevokedAsync(Guid accountId, Guid tenantId)
    {
        return this.RevokeAndNotifyAsync(
            s => s.UserId == accountId && s.CurrentTenantId == tenantId,
            new SessionRevocation
            {
                Reason = SessionRevocationReason.AccessRevoked,
                Subject = SessionSubject.User(accountId),
                TenantId = tenantId,
            });
    }

    /// <inheritdoc/>
    public async Task ClaimsChangedAsync(Guid accountId)
    {
        await this.MarkCallerSessionRewrittenAsync(accountId);
        await this.sessions.RefreshAsync(accountId, tenantId: null);
    }

    /// <inheritdoc/>
    public async Task ClaimsChangedAsync(Guid accountId, Guid tenantId)
    {
        await this.MarkCallerSessionRewrittenAsync(accountId);
        await this.sessions.RefreshAsync(accountId, tenantId);
    }

    /// <inheritdoc/>
    public Task TenantRemovedAsync(Guid tenantId)
    {
        return this.RevokeAndNotifyAsync(
            s => s.CurrentTenantId == tenantId,
            new SessionRevocation { Reason = SessionRevocationReason.TenantRemoved, TenantId = tenantId });
    }

    /// <inheritdoc/>
    public async Task<SessionTenantSwitchOutcome> SwitchTenantAsync(Guid tenantId)
    {
        var caller = await this.GetCallerSessionAsync();
        if (caller is null)
        {
            return SessionTenantSwitchOutcome.NoActiveSession;
        }

        if (!await this.membershipChecker.IsMemberAsync(caller.AccountId, tenantId, CancellationToken.None))
        {
            return SessionTenantSwitchOutcome.NotAMember;
        }

        this.httpContextAccessor.HttpContext?.MarkSessionRewritten(caller.SessionKey);
        return await this.sessions.SwitchTenantAsync(caller.SessionKey, caller.AccountId, tenantId);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActiveSessionInfo>> ListMySessionsAsync()
    {
        var caller = await this.GetCallerSessionAsync();
        if (caller is null)
        {
            return [];
        }

        var tracked = await this.sessions.GetUserSessionsAsync(caller.AccountId);
        return tracked
            .Select(s => new ActiveSessionInfo
            {
                Id = s.Id,
                CreatedAt = s.CreatedAt,
                ExpiresAt = s.ExpiresAt,
                UserAgent = s.UserAgent,
                IpAddress = s.IpAddress,
                CurrentTenantId = s.CurrentTenantId,
                IsCurrent = string.Equals(s.SessionKey, caller.SessionKey, StringComparison.Ordinal),
            })
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeMySessionAsync(Guid sessionId)
    {
        var httpContext = this.httpContextAccessor.HttpContext;
        var caller = httpContext is null ? null : await httpContext.GetCurrentSessionAsync();
        if (httpContext is null || caller is null)
        {
            return false;
        }

        var failures = new List<Exception>();

        // Ownership is part of the filter, so an id from another account matches nothing and ends nothing.
        var accountId = caller.AccountId;
        var revokedKeys = await this.RevokeSessionsAsync(s => s.Id == sessionId && s.UserId == accountId, failures);
        var revocationCompleted = failures.Count == 0;

        // Participants hear only about a session that was found among the caller's own: an unknown or foreign id
        // must not reach them as a revocation the caller's account performed. A session that was found but whose
        // ticket could not be removed still reaches them; it keeps its row, so a retry finds it again.
        if (revokedKeys.Count == 0)
        {
            ThrowIfAnyFailed(failures);
            return false;
        }

        if (revocationCompleted && revokedKeys.Contains(caller.SessionKey, StringComparer.Ordinal))
        {
            // The session is already gone; signing out clears its cookie from the browser on this response. After a
            // failed revocation the caller stays signed in instead, and the session keeps its row, so retrying the
            // intent finds it again.
            await this.RunStepAsync(failures, () => httpContext.SignOutAsync(IdentityConstants.ApplicationScheme));
        }

        await this.NotifyParticipantsAsync(
            new SessionRevocation
            {
                Reason = SessionRevocationReason.SessionRevokedByOwner,
                Subject = SessionSubject.User(accountId),
                SessionId = sessionId,
            },
            failures);

        ThrowIfAnyFailed(failures);
        return true;
    }

    private static void ThrowIfAnyFailed(List<Exception> failures)
    {
        if (failures.Count > 0)
        {
            throw new AggregateException("Session revocation did not complete. Every step ran; each error is attached.", failures);
        }
    }

    private async Task<SessionHttpContextExtensions.CurrentSession?> GetCallerSessionAsync()
    {
        var httpContext = this.httpContextAccessor.HttpContext;
        return httpContext is null ? null : await httpContext.GetCurrentSessionAsync();
    }

    private async Task ContinueCallerSessionAsync(HttpContext httpContext, Guid accountId, AuthenticationTicket callerTicket)
    {
        // A sign-in asks the same question and fails on a "no". Asking first leaves the caller signed out instead of
        // failing an intent whose revocation already succeeded.
        if (!await this.eligibility.MayHoldSessionAsync(accountId))
        {
            return;
        }

        // The new session gets a fresh lifetime, as any sign-in does.
        await httpContext.SignInNewSessionAsync(callerTicket.Principal);
    }

    private async Task MarkCallerSessionRewrittenAsync(Guid accountId)
    {
        // The cookie handler may already have queued a sliding renewal of the caller's session with the ticket it
        // read when it authenticated this request; the mark keeps that renewal from writing the old claims back.
        var httpContext = this.httpContextAccessor.HttpContext;
        var caller = httpContext is null ? null : await httpContext.GetCurrentSessionAsync();
        if (caller is not null && caller.AccountId == accountId)
        {
            httpContext!.MarkSessionRewritten(caller.SessionKey);
        }
    }

    private async Task RevokeAndNotifyAsync(Expression<Func<BlueprintActiveSession, bool>> filter, SessionRevocation revocation)
    {
        var failures = new List<Exception>();

        await this.RevokeSessionsAsync(filter, failures);

        // Participants hear about the revocation whether or not any package session matched: the intent states what
        // happened to the account, and the application's own credentials may exist without a cookie session. A
        // retried intent therefore reaches every participant again.
        await this.NotifyParticipantsAsync(revocation, failures);

        ThrowIfAnyFailed(failures);
    }

    private async Task<IReadOnlyList<string>> RevokeSessionsAsync(Expression<Func<BlueprintActiveSession, bool>> filter, List<Exception> failures)
    {
        var stepFailures = new List<Exception>();
        IReadOnlyList<string> revokedKeys = [];
        await this.RunStepAsync(failures, async () => revokedKeys = await this.sessions.RevokeAsync(filter, stepFailures));

        foreach (var failure in stepFailures)
        {
            this.logger.LogWarning(failure, "A session revocation step failed; the remaining steps still run");
            failures.Add(failure);
        }

        return revokedKeys;
    }

    private async Task NotifyParticipantsAsync(SessionRevocation revocation, List<Exception> failures)
    {
        foreach (var participant in this.participants)
        {
            try
            {
                await participant.OnSessionsRevokedAsync(revocation);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(
                    ex,
                    "Session revocation participant {Participant} failed for a {Reason} revocation",
                    participant.GetType().FullName,
                    revocation.Reason);
                failures.Add(ex);
            }
        }
    }

    private async Task RunStepAsync(List<Exception> failures, Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "A session revocation step failed; the remaining steps still run");
            failures.Add(ex);
        }
    }
}
