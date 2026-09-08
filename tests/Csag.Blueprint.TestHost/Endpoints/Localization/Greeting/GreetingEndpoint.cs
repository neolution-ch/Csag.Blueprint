namespace Csag.Blueprint.TestHost.Endpoints.Localization.Greeting;

using System.Globalization;
using Csag.Blueprint.TestHost.Localization;
using FastEndpoints;
using Microsoft.Extensions.Localization;

/// <summary>
/// Returns localized strings resolved through the database-backed localizer for the request
/// culture. The culture comes from the request-localization provider chain: the authenticated
/// session's preferred-language claim first, then the Accept-Language header, then the configured
/// default language. Anonymous access is allowed so header-driven resolution can be observed
/// without a session.
/// </summary>
public sealed class GreetingEndpoint : EndpointWithoutRequest<GreetingResponse>
{
    private readonly IStringLocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="GreetingEndpoint"/> class.
    /// </summary>
    /// <param name="localizer">The database-backed string localizer.</param>
    public GreetingEndpoint(IStringLocalizer localizer)
    {
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        this.Get("/[namespace]/greeting");
        this.AllowAnonymous();
        this.Summary(s =>
        {
            s.Summary = "Get localized greetings for the request culture";
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = new GreetingResponse
        {
            Culture = CultureInfo.CurrentUICulture.Name,
            Hello = this.localizer[TranslationKeys.GreetingHello],
            EnglishOnly = this.localizer[TranslationKeys.GreetingEnglishOnly],
            CodeOnly = this.localizer[TranslationKeys.GreetingCodeOnly],
        };

        await this.Send.OkAsync(response, ct);
    }
}
