namespace Csag.Blueprint.Web.Validation;

using Csag.Blueprint.Domain.Time;
using FluentValidation;

/// <summary>
/// FluentValidation rules for time zone ids and for dates and local readings that only mean something
/// in a time zone.
/// </summary>
/// <remarks>
/// <para>
/// FastEndpoints registers validators as singletons, so a validator must not capture a time zone (or
/// the scoped service that supplies it) in its constructor. The zone-dependent rules take a delegate
/// instead, which runs on every validation and resolves the zone for that request:
/// </para>
/// <code>
/// public sealed class CreateReservationValidator : Validator&lt;CreateReservationRequest&gt;
/// {
///     public CreateReservationValidator(TimeProvider timeProvider)
///     {
///         this.RuleFor(x => x.StartLocal)
///             .MustExistInTimeZone((_, ct) => this.Resolve&lt;ITenantTimeZoneProvider&gt;().GetTimeZoneAsync(ct));
///
///         this.RuleFor(x => x.PurchaseDate)
///             .MustNotBeAfterToday(timeProvider, (_, ct) => this.Resolve&lt;ITenantTimeZoneProvider&gt;().GetTimeZoneAsync(ct));
///     }
/// }
/// </code>
/// <para>
/// <c>Validator&lt;T&gt;.Resolve</c> resolves from the current request's services, so a scoped
/// <c>ITenantTimeZoneProvider</c> answers for the request's tenant. The delegate also receives the
/// instance being validated, for a zone carried by the request itself. The zone-dependent rules are
/// asynchronous: validate with <c>ValidateAsync</c>, which FastEndpoints does; a synchronous
/// <c>Validate</c> call throws <see cref="AsyncValidatorInvokedSynchronouslyException"/>.
/// </para>
/// <para>
/// Every rule carries an English default message; chain <c>WithLocalizedMessage</c> to translate it.
/// </para>
/// </remarks>
public static class TimeZoneRuleBuilderExtensions
{
    private const string NotAfterTodayMessage = "'{PropertyName}' must not be after today.";

    private const string ExistsInTimeZoneMessage =
        "'{PropertyName}' does not exist in the time zone: clocks skip that time when daylight saving time starts.";

    /// <summary>
    /// Requires an IANA time zone id this host can resolve, such as <c>Europe/Zurich</c>, and rejects Windows
    /// ids such as <c>W. Europe Standard Time</c> (see <see cref="ZonedTime.TryFindIanaTimeZone"/>). A null
    /// value passes, so the rule fits optional fields; add <c>NotEmpty</c> to require one.
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="rule">The rule builder for the zone id property.</param>
    /// <returns>The rule builder options for continued chaining.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeIanaTimeZone<T>(this IRuleBuilder<T, string?> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule
            .Must(id => id is null || ZonedTime.TryFindIanaTimeZone(id, out _))
            .WithMessage("'{PropertyName}' must be an IANA time zone id, such as Europe/Zurich.");
    }

    /// <summary>
    /// Requires a calendar date that is not after today as the clock shows it in the zone, which for part of
    /// every day differs from today in UTC.
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="rule">The rule builder for the date property.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="timeZone">Resolves the zone on every validation, from the instance and a cancellation token.</param>
    /// <returns>The rule builder options for continued chaining.</returns>
    public static IRuleBuilderOptions<T, DateOnly> MustNotBeAfterToday<T>(
        this IRuleBuilder<T, DateOnly> rule,
        TimeProvider timeProvider,
        Func<T, CancellationToken, ValueTask<TimeZoneInfo>> timeZone)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(timeZone);

        return rule
            .MustAsync(async (instance, date, cancellationToken) =>
                date <= ZonedTime.Today(timeProvider, await timeZone(instance, cancellationToken)))
            .WithMessage(NotAfterTodayMessage);
    }

    /// <summary>
    /// Requires a calendar date that is not after today as the clock shows it in the zone. A null value passes.
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="rule">The rule builder for the date property.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="timeZone">Resolves the zone on every validation, from the instance and a cancellation token.</param>
    /// <returns>The rule builder options for continued chaining.</returns>
    public static IRuleBuilderOptions<T, DateOnly?> MustNotBeAfterToday<T>(
        this IRuleBuilder<T, DateOnly?> rule,
        TimeProvider timeProvider,
        Func<T, CancellationToken, ValueTask<TimeZoneInfo>> timeZone)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(timeZone);

        return rule
            .MustAsync(async (instance, date, cancellationToken) =>
                date is not { } value || value <= ZonedTime.Today(timeProvider, await timeZone(instance, cancellationToken)))
            .WithMessage(NotAfterTodayMessage);
    }

    /// <summary>
    /// Requires a local reading that exists in the zone: one that does not fall in the hour clocks skip when
    /// daylight saving time starts. A reading that happens twice when clocks fall back passes; it resolves to
    /// its earlier occurrence (see <see cref="ZonedTime.TryToInstant"/>).
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="rule">The rule builder for the local reading property.</param>
    /// <param name="timeZone">Resolves the zone on every validation, from the instance and a cancellation token.</param>
    /// <returns>The rule builder options for continued chaining.</returns>
    public static IRuleBuilderOptions<T, DateTime> MustExistInTimeZone<T>(
        this IRuleBuilder<T, DateTime> rule,
        Func<T, CancellationToken, ValueTask<TimeZoneInfo>> timeZone)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(timeZone);

        return rule
            .MustAsync(async (instance, reading, cancellationToken) =>
                ZonedTime.TryToInstant(reading, await timeZone(instance, cancellationToken), out _))
            .WithMessage(ExistsInTimeZoneMessage);
    }

    /// <summary>
    /// Requires a local reading that exists in the zone. A null value passes.
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="rule">The rule builder for the local reading property.</param>
    /// <param name="timeZone">Resolves the zone on every validation, from the instance and a cancellation token.</param>
    /// <returns>The rule builder options for continued chaining.</returns>
    public static IRuleBuilderOptions<T, DateTime?> MustExistInTimeZone<T>(
        this IRuleBuilder<T, DateTime?> rule,
        Func<T, CancellationToken, ValueTask<TimeZoneInfo>> timeZone)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(timeZone);

        return rule
            .MustAsync(async (instance, reading, cancellationToken) =>
                reading is not { } value || ZonedTime.TryToInstant(value, await timeZone(instance, cancellationToken), out _))
            .WithMessage(ExistsInTimeZoneMessage);
    }
}
