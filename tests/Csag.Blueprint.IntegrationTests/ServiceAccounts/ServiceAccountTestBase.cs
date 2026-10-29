namespace Csag.Blueprint.IntegrationTests.ServiceAccounts;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Auth.Token;
using Csag.Blueprint.TestHost.Endpoints.Auth.WhoAmI;
using Csag.Blueprint.TestHost.Session;
using Csag.Blueprint.Testing.Extensions;
using Csag.Blueprint.Tests.Shared.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Shared arrangements for the service-account integration tests: the token endpoint, bearer clients, the intents
/// run in their own scope as an endpoint or a background job would, and reads of the account and its sessions.
/// Tests that change an account's state create their own account, so the seeded one stays as seeded for the rest.
/// </summary>
/// <param name="app">The shared application fixture.</param>
public abstract class ServiceAccountTestBase(AppFixture app) : IntegrationTestBase(app)
{
    /// <summary>
    /// The token endpoint.
    /// </summary>
    protected static readonly Uri TokenUri = new("/api/auth/token", UriKind.Relative);

    /// <summary>
    /// Describes the caller, with the host's default scheme.
    /// </summary>
    protected static readonly Uri WhoAmIUri = new("/api/auth/whoami", UriKind.Relative);

    /// <summary>
    /// Describes the caller, accepting bearer tokens only.
    /// </summary>
    protected static readonly Uri WhoAmIBearerUri = new("/api/auth/whoami/bearer", UriKind.Relative);

    /// <summary>
    /// Asserts the status a request to <paramref name="uri"/> gets, and returns the described principal when it is 200.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="expected">The expected status.</param>
    /// <param name="uri">The endpoint, <see cref="WhoAmIUri"/> by default.</param>
    /// <returns>The principal, or <see langword="null"/> for any other status.</returns>
    protected static async Task<WhoAmIResponse?> WhoAmIAsync(HttpClient client, HttpStatusCode expected, Uri? uri = null)
    {
        var ct = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync(uri ?? WhoAmIUri, ct);
        await response.ShouldHaveStatusCodeAsync(expected, cancellationToken: ct);
        return expected == HttpStatusCode.OK ? await response.Content.ReadFromJsonAsync<WhoAmIResponse>(ct) : null;
    }

    /// <summary>
    /// Requests a token on a fresh client.
    /// </summary>
    /// <param name="clientId">The client id.</param>
    /// <param name="clientSecret">The secret.</param>
    /// <returns>The response.</returns>
    protected async Task<HttpResponseMessage> RequestTokenAsync(string clientId, string clientSecret)
    {
        using var client = this.App.CreateClient();
        return await client.PostAsJsonAsync(
            TokenUri,
            new TokenRequest { ClientId = clientId, ClientSecret = clientSecret },
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Requests a token and asserts it was issued.
    /// </summary>
    /// <param name="clientId">The client id.</param>
    /// <param name="clientSecret">The secret.</param>
    /// <returns>The issued token.</returns>
    protected async Task<TokenResponse> IssueAsync(string clientId, string clientSecret)
    {
        var ct = TestContext.Current.CancellationToken;
        using var response = await this.RequestTokenAsync(clientId, clientSecret);
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, $"no token was issued for '{clientId}'", ct);
        return (await response.Content.ReadFromJsonAsync<TokenResponse>(ct)).ShouldNotBeNull();
    }

    /// <summary>
    /// Creates a client sending the token on every request.
    /// </summary>
    /// <param name="accessToken">The token.</param>
    /// <returns>The client.</returns>
    protected HttpClient BearerClient(string accessToken)
    {
        var client = this.App.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>
    /// Creates a service account in tenant A through <see cref="IServiceAccountCredentials.CreateCredentialsAsync"/>.
    /// </summary>
    /// <param name="name">The account's name.</param>
    /// <returns>The account's id and its credentials.</returns>
    protected async Task<(Guid Id, ServiceAccountClientCredentials Credentials)> CreateServiceAccountAsync(string name)
    {
        var serviceAccount = new BlueprintServiceAccount
        {
            Name = name,
            Roles = [TestRoles.TenantViewer],
            IsActive = true,
        };

        var credentials = await this.RunCredentialsAsync(c => c.CreateCredentialsAsync(serviceAccount));
        return (serviceAccount.Id, credentials);
    }

    /// <summary>
    /// Runs a credentials intent in its own scope, with tenant A as the ambient tenant as it is inside a request.
    /// </summary>
    /// <typeparam name="T">The type of the intent's result.</typeparam>
    /// <param name="intent">The intent.</param>
    /// <param name="tenantId">The ambient tenant; tenant A by default.</param>
    /// <returns>What the intent returned.</returns>
    protected async Task<T> RunCredentialsAsync<T>(Func<IServiceAccountCredentials, Task<T>> intent, Guid? tenantId = null)
    {
        TenantContext.SetTenant(tenantId ?? SeedData.TenantAId);
        try
        {
            using var scope = this.App.Services.CreateScope();
            return await intent(scope.ServiceProvider.GetRequiredService<IServiceAccountCredentials>());
        }
        finally
        {
            TenantContext.Clear();
        }
    }

    /// <summary>
    /// Runs a service-account session intent in its own scope.
    /// </summary>
    /// <param name="intent">The intent.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected async Task RunLifecycleAsync(Func<IServiceAccountSessionLifecycle, Task> intent)
    {
        using var scope = this.App.Services.CreateScope();
        await intent(scope.ServiceProvider.GetRequiredService<IServiceAccountSessionLifecycle>());
    }

    /// <summary>
    /// Reads an account as stored.
    /// </summary>
    /// <param name="serviceAccountId">The account.</param>
    /// <returns>The account, or <see langword="null"/> when it is gone.</returns>
    protected async Task<BlueprintServiceAccount?> ReadAccountAsync(Guid serviceAccountId)
    {
        using var scope = this.App.CreateDbContextScope();
        return await scope.Context.ServiceAccounts
            .AsNoTracking()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(sa => sa.Id == serviceAccountId, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Changes an account and commits, as an application endpoint does before it calls an intent.
    /// </summary>
    /// <param name="serviceAccountId">The account.</param>
    /// <param name="change">The change.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected async Task UpdateAccountAsync(Guid serviceAccountId, Action<BlueprintServiceAccount> change)
    {
        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);
        var account = await scope.Context.ServiceAccounts.SingleAsync(sa => sa.Id == serviceAccountId, TestContext.Current.CancellationToken);
        change(account);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Counts the stored sessions of an account.
    /// </summary>
    /// <param name="serviceAccountId">The account.</param>
    /// <returns>The number of session rows.</returns>
    protected async Task<int> CountSessionsAsync(Guid serviceAccountId)
    {
        using var scope = this.App.CreateDbContextScope();
        return await scope.Context.ServiceAccountSessions
            .CountAsync(s => s.ServiceAccountId == serviceAccountId, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Gets the revocations the host's recording participant was told about for a service account.
    /// </summary>
    /// <param name="serviceAccountId">The account.</param>
    /// <returns>The revocations, oldest first.</returns>
    protected List<SessionRevocation> RevocationsFor(Guid serviceAccountId)
    {
        return this.App.Services.GetRequiredService<RecordingRevocationParticipant>().Revocations
            .Where(r => r.Subject == SessionSubject.ServiceAccount(serviceAccountId))
            .ToList();
    }
}
