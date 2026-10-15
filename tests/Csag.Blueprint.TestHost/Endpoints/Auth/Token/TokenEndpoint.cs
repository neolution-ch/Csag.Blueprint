namespace Csag.Blueprint.TestHost.Endpoints.Auth.Token;

using Csag.Blueprint.Application.Abstractions.Services;
using FastEndpoints;

/// <summary>
/// Issues a service-account bearer token for a client id and secret. The endpoint is the shell an application keeps:
/// the route, the response shapes and their documentation; the credential check, the lockout and the token are
/// <see cref="IServiceAccountCredentials.IssueTokenAsync"/>'s.
/// </summary>
public sealed class TokenEndpoint : Endpoint<TokenRequest, TokenResponse>
{
    private readonly IServiceAccountCredentials credentials;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenEndpoint"/> class.
    /// </summary>
    /// <param name="credentials">The service-account credentials.</param>
    public TokenEndpoint(IServiceAccountCredentials credentials)
    {
        this.credentials = credentials;
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        this.Post("/[namespace]/token");
        this.AllowAnonymous();
        this.Summary(s =>
        {
            s.Summary = "Get a bearer token for a service account";
            s.Description = "Authenticates a service account with its client credentials and returns a bearer token.";
            s.Response(401, "Invalid client credentials");
            s.Response(403, "The service account is inactive");
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(TokenRequest req, CancellationToken ct)
    {
        var result = await this.credentials.IssueTokenAsync(req.ClientId, req.ClientSecret);

        switch (result.Outcome)
        {
            case ServiceAccountTokenOutcome.Issued:
                await this.Send.OkAsync(
                    new TokenResponse
                    {
                        AccessToken = result.AccessToken!,
                        TokenType = result.TokenType,
                        ExpiresIn = result.ExpiresIn,
                    },
                    ct);
                break;

            case ServiceAccountTokenOutcome.AccountInactive:
                await this.Send.ForbiddenAsync(ct);
                break;

            default:
                await this.Send.UnauthorizedAsync(ct);
                break;
        }
    }
}
