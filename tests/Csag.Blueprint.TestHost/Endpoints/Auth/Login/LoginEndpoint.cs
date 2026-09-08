namespace Csag.Blueprint.TestHost.Endpoints.Auth.Login;

using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Infrastructure.Authentication;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Web.Helpers;
using Csag.Blueprint.Web.Options.Api.Security;
using FastEndpoints;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

/// <summary>
/// Password login establishing a distributed session: the principal (profile, tenant, role, and
/// permission claims) is built once at sign-in, stored server-side via the ticket store, and the
/// cookie carries only the session key. The CSRF request token is distributed on the response so
/// clients can send state-changing requests without a separate token fetch.
/// </summary>
public sealed class LoginEndpoint : Endpoint<LoginRequest, LoginResponse>
{
    private readonly UserManager<TestUser> userManager;
    private readonly ITenantManager<TestUser, TestTenant> tenantManager;
    private readonly IAuthenticatedPrincipalBuilder<TestUser> principalBuilder;
    private readonly IAntiforgery antiforgery;
    private readonly SecuritySettings securitySettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginEndpoint"/> class.
    /// </summary>
    /// <param name="userManager">The Identity user manager for credential verification.</param>
    /// <param name="tenantManager">The tenant manager used to load the user's memberships.</param>
    /// <param name="principalBuilder">The builder composing the session principal's claims.</param>
    /// <param name="antiforgery">The antiforgery service used to distribute the CSRF request token.</param>
    /// <param name="securitySettings">The security settings providing session lifetime and CSRF configuration.</param>
    public LoginEndpoint(
        UserManager<TestUser> userManager,
        ITenantManager<TestUser, TestTenant> tenantManager,
        IAuthenticatedPrincipalBuilder<TestUser> principalBuilder,
        IAntiforgery antiforgery,
        IOptions<SecuritySettings> securitySettings)
    {
        this.userManager = userManager;
        this.tenantManager = tenantManager;
        this.principalBuilder = principalBuilder;
        this.antiforgery = antiforgery;
        this.securitySettings = securitySettings.Value;
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        this.Post("/[namespace]/login");
        this.AllowAnonymous();
        this.Summary(s =>
        {
            s.Summary = "Login with email and password";
            s.Description = "Authenticates the user and creates a distributed session with an encrypted session-key cookie.";
            s.Response(401, "Invalid credentials");
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var user = await this.userManager.FindByEmailAsync(req.Email);
        if (user is null || !await this.userManager.CheckPasswordAsync(user, req.Password))
        {
            this.ThrowError("Invalid email or password.", 401);
        }

        var memberships = await this.tenantManager.GetUserMembershipsAsync(user.Id, ct);

        Guid? currentTenantId;
        if (req.TenantId.HasValue)
        {
            if (memberships.All(m => m.TenantId != req.TenantId.Value))
            {
                this.ThrowError("The user is not a member of the requested tenant.", 400);
            }

            currentTenantId = req.TenantId.Value;
        }
        else
        {
            // A user with no membership may still sign in: the session has no active tenant and
            // carries platform-scope roles only.
            currentTenantId = memberships.Count > 0 ? memberships[0].TenantId : null;
        }

        var principal = await this.principalBuilder.BuildAsync(user, currentTenantId, ct);

        // IsPersistent is required: the cookie handler only sets the ticket's ExpiresUtc for
        // persistent sign-ins, and the ticket store refuses tickets without an expiration.
        await this.HttpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(this.securitySettings.SessionExpirationHours),
                AllowRefresh = true,
            });

        // SignInAsync only writes the cookie for future requests; updating HttpContext.User binds
        // the antiforgery tokens generated below to the authenticated identity.
        this.HttpContext.User = principal;
        CsrfTokenDistributor.DistributeRequestToken(this.HttpContext, this.antiforgery, this.securitySettings);

        var response = new LoginResponse
        {
            IsAuthenticated = true,
            Email = user.Email!,
            DisplayName = user.DisplayName,
            Roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
            Permissions = principal.FindAll(IdentityClaimTypes.Permission).Select(c => c.Value).ToList(),
            CurrentTenantId = currentTenantId,
            AvailableTenants = memberships
                .Select(m => new TenantInfo { TenantId = m.TenantId, TenantName = m.Tenant.Name })
                .ToList(),
        };

        await this.Send.OkAsync(response, ct);
    }
}
