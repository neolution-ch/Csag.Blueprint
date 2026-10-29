namespace Csag.Blueprint.Web.UnitTests.Validation;

using Csag.Blueprint.Domain.Time;
using Csag.Blueprint.Web.Validation;
using FluentValidation;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="TimeZoneRuleBuilderExtensions"/>. The clock and every zone are fixed, so the
/// results do not depend on the machine's own zone or the time the tests run.
/// </summary>
public sealed class TimeZoneRuleBuilderExtensionsTests
{
    private static readonly TimeZoneInfo Zurich = FindZone("Europe/Zurich");

    /// <summary>
    /// 22:30 UTC on 21 October 2026: already the 22nd in Zurich, still the 21st in UTC and New York.
    /// </summary>
    private static readonly TimeProvider Clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 21, 22, 30, 0, TimeSpan.Zero));

    [Theory]
    [InlineData("Europe/Zurich")]
    [InlineData("America/New_York")]
    [InlineData("UTC")]
    [InlineData(null)]
    public void MustBeIanaTimeZone_IanaIdOrNull_Passes(string? timeZone)
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(x => x.TimeZone).MustBeIanaTimeZone();

        validator.TestValidate(new Model { TimeZone = timeZone }).ShouldNotHaveValidationErrorFor(x => x.TimeZone);
    }

    [Theory]
    [InlineData("W. Europe Standard Time")]
    [InlineData("Europe/Atlantis")]
    [InlineData("Europe//Zurich")]
    [InlineData("")]
    [InlineData("+02:00")]
    public void MustBeIanaTimeZone_WindowsIdOrUnknownId_Fails(string timeZone)
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(x => x.TimeZone).MustBeIanaTimeZone();

        validator.TestValidate(new Model { TimeZone = timeZone })
            .ShouldHaveValidationErrorFor(x => x.TimeZone)
            .WithErrorMessage("'Time Zone' must be an IANA time zone id, such as Europe/Zurich.");
    }

    [Theory]
    [InlineData("Europe/Zurich")]
    [InlineData("Mars/Olympus_Mons")] // no tz database knows it, but it has the form of an id
    [InlineData(null)]
    public void MustBeWellFormedIanaTimeZone_IdOfTheIanaFormOrNull_Passes(string? timeZone)
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(x => x.TimeZone).MustBeWellFormedIanaTimeZone();

        validator.TestValidate(new Model { TimeZone = timeZone }).ShouldNotHaveValidationErrorFor(x => x.TimeZone);
    }

    [Theory]
    [InlineData("W. Europe Standard Time")]
    [InlineData("Europe//Zurich")]
    [InlineData("right/UTC")]
    [InlineData("")]
    public void MustBeWellFormedIanaTimeZone_ValueOfAnotherForm_Fails(string timeZone)
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(x => x.TimeZone).MustBeWellFormedIanaTimeZone();

        validator.TestValidate(new Model { TimeZone = timeZone }).ShouldHaveValidationErrorFor(x => x.TimeZone);
    }

    [Fact]
    public void MustBeIanaTimeZone_WellFormedButUnknownId_Fails()
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(x => x.TimeZone).MustBeIanaTimeZone();

        validator.TestValidate(new Model { TimeZone = "Mars/Olympus_Mons" }).ShouldHaveValidationErrorFor(x => x.TimeZone);
    }

    [Theory]
    [InlineData("2026-10-22", true)] // today in Zurich, tomorrow in UTC
    [InlineData("2026-10-21", true)]
    [InlineData("2026-10-23", false)]
    public async Task MustNotBeAfterToday_ComparesWithTodayInZone(string date, bool expectedValid)
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(x => x.PurchaseDate).MustNotBeAfterToday(Clock, (_, _) => ValueTask.FromResult(Zurich));

        var result = await validator.TestValidateAsync(new Model { PurchaseDate = DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture) }, cancellationToken: TestContext.Current.CancellationToken);

        result.IsValid.ShouldBe(expectedValid);
    }

    [Fact]
    public async Task MustNotBeAfterToday_SameDateInAnotherZone_CanBeInTheFuture()
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(x => x.PurchaseDate).MustNotBeAfterToday(Clock, (_, _) => ValueTask.FromResult(TimeZoneInfo.Utc));

        var result = await validator.TestValidateAsync(new Model { PurchaseDate = new DateOnly(2026, 10, 22) }, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldHaveValidationErrorFor(x => x.PurchaseDate).WithErrorMessage("'Purchase Date' must not be after today.");
    }

    [Fact]
    public async Task MustNotBeAfterToday_ResolvesZoneOnEveryValidationFromInstance()
    {
        var resolutions = 0;
        var validator = new InlineValidator<Model>();
        validator.RuleFor(x => x.PurchaseDate).MustNotBeAfterToday(Clock, (model, _) =>
        {
            resolutions++;
            return ValueTask.FromResult(FindZone(model.TimeZone!));
        });

        var inZurich = await validator.ValidateAsync(new Model { TimeZone = "Europe/Zurich", PurchaseDate = new DateOnly(2026, 10, 22) }, TestContext.Current.CancellationToken);
        var inNewYork = await validator.ValidateAsync(new Model { TimeZone = "America/New_York", PurchaseDate = new DateOnly(2026, 10, 22) }, TestContext.Current.CancellationToken);

        inZurich.IsValid.ShouldBeTrue();
        inNewYork.IsValid.ShouldBeFalse();
        resolutions.ShouldBe(2);
    }

    [Fact]
    public async Task MustNotBeAfterToday_NullableNull_PassesWithoutResolvingZone()
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(x => x.OptionalDate).MustNotBeAfterToday(Clock, (_, _) => throw new InvalidOperationException("The zone must not be resolved for a null value."));

        var result = await validator.TestValidateAsync(new Model(), cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotHaveValidationErrorFor(x => x.OptionalDate);
    }

    [Fact]
    public async Task MustNotBeAfterToday_NullableWithFutureDate_Fails()
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(x => x.OptionalDate).MustNotBeAfterToday(Clock, (_, _) => ValueTask.FromResult(Zurich));

        var result = await validator.TestValidateAsync(new Model { OptionalDate = new DateOnly(2026, 10, 23) }, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldHaveValidationErrorFor(x => x.OptionalDate);
    }

    [Fact]
    public async Task MustExistInTimeZone_ReadingInSpringForwardGap_Fails()
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(x => x.StartLocal).MustExistInTimeZone((_, _) => ValueTask.FromResult(Zurich));

        var result = await validator.TestValidateAsync(new Model { StartLocal = new DateTime(2027, 3, 28, 2, 30, 0, DateTimeKind.Unspecified) }, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldHaveValidationErrorFor(x => x.StartLocal)
            .WithErrorMessage("'Start Local' does not exist in the time zone: clocks skip that time when daylight saving time starts.");
    }

    [Theory]
    [InlineData(2026, 10, 25, 2, 30)] // happens twice: resolves to the earlier occurrence
    [InlineData(2027, 3, 28, 3, 0)]
    [InlineData(2026, 10, 26, 10, 0)]
    public async Task MustExistInTimeZone_ReadingThatExists_Passes(int year, int month, int day, int hour, int minute)
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(x => x.StartLocal).MustExistInTimeZone((_, _) => ValueTask.FromResult(Zurich));

        var result = await validator.TestValidateAsync(new Model { StartLocal = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified) }, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotHaveValidationErrorFor(x => x.StartLocal);
    }

    [Fact]
    public async Task MustExistInTimeZone_SameReadingInZoneWithoutTransition_Passes()
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(x => x.StartLocal).MustExistInTimeZone((_, _) => ValueTask.FromResult(FindZone("Asia/Tokyo")));

        var result = await validator.TestValidateAsync(new Model { StartLocal = new DateTime(2027, 3, 28, 2, 30, 0, DateTimeKind.Unspecified) }, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotHaveValidationErrorFor(x => x.StartLocal);
    }

    [Fact]
    public async Task MustExistInTimeZone_NullableNullOrGap_PassesOrFails()
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(x => x.EndLocal).MustExistInTimeZone((_, _) => ValueTask.FromResult(Zurich));

        (await validator.TestValidateAsync(new Model(), cancellationToken: TestContext.Current.CancellationToken))
            .ShouldNotHaveValidationErrorFor(x => x.EndLocal);
        (await validator.TestValidateAsync(new Model { EndLocal = new DateTime(2027, 3, 28, 2, 0, 0, DateTimeKind.Unspecified) }, cancellationToken: TestContext.Current.CancellationToken))
            .ShouldHaveValidationErrorFor(x => x.EndLocal);
    }

    [Fact]
    public void ZoneDependentRule_ValidatedSynchronously_Throws()
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(x => x.StartLocal).MustExistInTimeZone((_, _) => ValueTask.FromResult(Zurich));

        Should.Throw<AsyncValidatorInvokedSynchronouslyException>(() => validator.Validate(new Model()));
    }

    private static TimeZoneInfo FindZone(string id)
    {
        return ZonedTime.TryFindIanaTimeZone(id, out var zone)
            ? zone
            : throw new InvalidOperationException($"The host cannot resolve the IANA zone '{id}'; install time zone data.");
    }

    private sealed class Model
    {
        public string? TimeZone { get; init; }

        public DateOnly PurchaseDate { get; init; }

        public DateOnly? OptionalDate { get; init; }

        public DateTime StartLocal { get; init; }

        public DateTime? EndLocal { get; init; }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
