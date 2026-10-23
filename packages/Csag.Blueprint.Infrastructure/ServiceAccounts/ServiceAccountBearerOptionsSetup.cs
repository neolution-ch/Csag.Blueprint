namespace Csag.Blueprint.Infrastructure.ServiceAccounts;

using System.Diagnostics.CodeAnalysis;
using Csag.Blueprint.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

/// <summary>
/// Applies the package's fixed behaviour to the service-account bearer scheme, after every <c>Configure</c>.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection.")]
internal sealed class ServiceAccountBearerOptionsSetup : IPostConfigureOptions<JwtBearerOptions>
{
    private readonly ServiceAccountTokenSigning signing;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAccountBearerOptionsSetup"/> class.
    /// </summary>
    /// <param name="signing">The signing key and validation parameters.</param>
    /// <param name="timeProvider">The clock the handler runs on.</param>
    public ServiceAccountBearerOptionsSetup(ServiceAccountTokenSigning signing, TimeProvider timeProvider)
    {
        this.signing = signing ?? throw new ArgumentNullException(nameof(signing));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc/>
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.Equals(name, BlueprintAuthenticationSchemes.ServiceAccountToken, StringComparison.Ordinal))
        {
            return;
        }

        // The principal carries the raw claim names the token was minted with (ServiceAccountClaimTypes).
        options.MapInboundClaims = false;
        options.TokenValidationParameters = this.signing.CreateValidationParameters();
        options.TimeProvider = this.timeProvider;

        // The reason a token was refused stays in the server log; the client gets a bare bearer challenge.
        options.IncludeErrorDetails = false;
        options.SaveToken = false;

        options.EventsType = null;
        options.Events = new JwtBearerEvents { OnTokenValidated = ServiceAccountTokenValidation.OnTokenValidatedAsync };
    }
}
