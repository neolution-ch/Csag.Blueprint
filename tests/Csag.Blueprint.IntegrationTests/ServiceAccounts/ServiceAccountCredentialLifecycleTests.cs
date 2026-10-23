namespace Csag.Blueprint.IntegrationTests.ServiceAccounts;

using System.Net;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.Testing.Extensions;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Integration tests for what happens to an account's credentials after it exists: creation hands the secret out once
/// and stores its hash, rotation ends the previous secret and its tokens at once, deactivation and deletion end every
/// session, and the application's revocation participants hear about each with the service account as the subject.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class ServiceAccountCredentialLifecycleTests(AppFixture app) : ServiceAccountTestBase(app)
{
    [Fact]
    public async Task CreateCredentials_StoresTheAccountWithAHashAndReturnsTheSecretAsync()
    {
        // Act
        var (id, credentials) = await this.CreateServiceAccountAsync("created");

        // Assert
        credentials.ClientId.ShouldStartWith("sa-");
        credentials.ClientSecret.Length.ShouldBeGreaterThanOrEqualTo(43);
        credentials.ToString().ShouldNotContain(credentials.ClientSecret);

        var account = (await this.ReadAccountAsync(id)).ShouldNotBeNull();
        account.ClientId.ShouldBe(credentials.ClientId);
        account.TenantId.ShouldBe(SeedData.TenantAId);
        account.CreatedAt.ShouldNotBe(default);
        account.ClientSecretHash.ShouldNotContain(credentials.ClientSecret);

        // The stored hash is in the format ASP.NET Core Identity's default hasher reads.
        new PasswordHasher<object>().VerifyHashedPassword(new object(), account.ClientSecretHash, credentials.ClientSecret)
            .ShouldBe(PasswordVerificationResult.Success);

        await this.IssueAsync(credentials.ClientId, credentials.ClientSecret);
    }

    [Fact]
    public async Task Issue_AcceptsAHashWrittenByIdentitysDefaultHasherAsync()
    {
        // Arrange — an account whose hash an application wrote itself with PasswordHasher, before the package owned it.
        var (id, credentials) = await this.CreateServiceAccountAsync("existing hash");
        const string existingSecret = "an-existing-secret-hashed-by-the-application";
        await this.UpdateAccountAsync(id, account => account.ClientSecretHash = new PasswordHasher<object>().HashPassword(new object(), existingSecret));

        // Act & Assert
        await this.IssueAsync(credentials.ClientId, existingSecret);
    }

    [Fact]
    public async Task RotateSecret_EndsThePreviousSecretAndItsTokensAtOnceAsync()
    {
        // Arrange — a token from the current secret that authenticates.
        var (id, credentials) = await this.CreateServiceAccountAsync("rotated");
        var before = await this.IssueAsync(credentials.ClientId, credentials.ClientSecret);
        using var beforeClient = this.BearerClient(before.AccessToken);
        await WhoAmIAsync(beforeClient, HttpStatusCode.OK);

        // Act
        var rotated = (await this.RunCredentialsAsync(c => c.RotateSecretAsync(id))).ShouldNotBeNull();

        // Assert — no grace period: the token and the previous secret are refused on the next request.
        rotated.ClientId.ShouldBe(credentials.ClientId);
        rotated.ClientSecret.ShouldNotBe(credentials.ClientSecret);
        await WhoAmIAsync(beforeClient, HttpStatusCode.Unauthorized);
        using (var oldSecret = await this.RequestTokenAsync(credentials.ClientId, credentials.ClientSecret))
        {
            await oldSecret.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: TestContext.Current.CancellationToken);
        }

        (await this.CountSessionsAsync(id)).ShouldBe(0);

        var after = await this.IssueAsync(credentials.ClientId, rotated.ClientSecret);
        using var afterClient = this.BearerClient(after.AccessToken);
        await WhoAmIAsync(afterClient, HttpStatusCode.OK);

        var revocation = this.RevocationsFor(id).ShouldHaveSingleItem();
        revocation.Reason.ShouldBe(SessionRevocationReason.CredentialsChanged);
        revocation.Subject.ShouldBe(SessionSubject.ServiceAccount(id));
    }

    [Fact]
    public async Task RotateSecret_ForAnAccountOfAnotherTenant_ChangesNothingAsync()
    {
        var (id, credentials) = await this.CreateServiceAccountAsync("other tenant");

        var rotated = await this.RunCredentialsAsync(c => c.RotateSecretAsync(id), SeedData.TenantBId);

        rotated.ShouldBeNull();
        await this.IssueAsync(credentials.ClientId, credentials.ClientSecret);
        this.RevocationsFor(id).ShouldBeEmpty();
    }

    [Fact]
    public async Task RotateSecret_ForAnUnknownAccount_ReturnsNullAsync()
    {
        (await this.RunCredentialsAsync(c => c.RotateSecretAsync(Guid.NewGuid()))).ShouldBeNull();
    }

    [Fact]
    public async Task AccountDeactivated_EndsEverySessionAndTellsTheParticipantsAsync()
    {
        // Arrange — two live tokens.
        var (id, credentials) = await this.CreateServiceAccountAsync("deactivated");
        var first = await this.IssueAsync(credentials.ClientId, credentials.ClientSecret);
        var second = await this.IssueAsync(credentials.ClientId, credentials.ClientSecret);
        (await this.CountSessionsAsync(id)).ShouldBe(2);

        // Act — the application commits the deactivation, then states it.
        await this.UpdateAccountAsync(id, account => account.IsActive = false);
        await this.RunLifecycleAsync(l => l.AccountDeactivatedAsync(id));

        // Assert
        (await this.CountSessionsAsync(id)).ShouldBe(0);
        foreach (var token in new[] { first, second })
        {
            using var client = this.BearerClient(token.AccessToken);
            await WhoAmIAsync(client, HttpStatusCode.Unauthorized);
        }

        var revocation = this.RevocationsFor(id).ShouldHaveSingleItem();
        revocation.Reason.ShouldBe(SessionRevocationReason.AccessRevoked);
        revocation.Subject.ShouldBe(SessionSubject.ServiceAccount(id));
    }

    [Fact]
    public async Task AccountDeleted_EndsEverySessionAndTellsTheParticipantsAsync()
    {
        var (id, credentials) = await this.CreateServiceAccountAsync("deleted");
        var token = await this.IssueAsync(credentials.ClientId, credentials.ClientSecret);

        using (var scope = this.App.CreateDbContextScope(SeedData.TenantAId))
        {
            var account = scope.Context.ServiceAccounts.Single(sa => sa.Id == id);
            scope.Context.ServiceAccounts.Remove(account);
            await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await this.RunLifecycleAsync(l => l.AccountDeletedAsync(id));

        (await this.CountSessionsAsync(id)).ShouldBe(0);
        using var client = this.BearerClient(token.AccessToken);
        await WhoAmIAsync(client, HttpStatusCode.Unauthorized);
        this.RevocationsFor(id).ShouldHaveSingleItem().Subject.ShouldBe(SessionSubject.ServiceAccount(id));
    }

    [Fact]
    public async Task RenamingAnAccount_ShowsOnTheNextRequestWithoutANewTokenAsync()
    {
        var (id, credentials) = await this.CreateServiceAccountAsync("before rename");
        var token = await this.IssueAsync(credentials.ClientId, credentials.ClientSecret);
        using var client = this.BearerClient(token.AccessToken);

        await this.UpdateAccountAsync(id, account => account.Name = "after rename");

        var principal = (await WhoAmIAsync(client, HttpStatusCode.OK)).ShouldNotBeNull();
        principal.Claims.Single(c => c.Type == "name").Value.ShouldBe("after rename");
    }
}
